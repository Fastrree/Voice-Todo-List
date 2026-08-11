using System.ComponentModel;
using System.Runtime.CompilerServices;
using TodoVoiceMaui.Models;
using Whisper.net;

namespace TodoVoiceMaui.Services;

/// <summary>
/// Çevrimdışı ses tanıma — Whisper (whisper.cpp) tabanlı.
///
/// NEDEN Windows SpeechRecognizer DEĞİL?
/// Uygulama unpackaged WinUI 3 olarak çalışıyor (WindowsPackageType=None).
/// Windows.Media.SpeechRecognition WinRT paket kimliği gerektirir; unpackaged
/// uygulamada her koşulda "0x800455A0 Internal Speech Error" ile başarısız olur
/// (app.log'da kanıtlandı). Whisper tamamen yerel, ücretsiz (MIT) ve paket
/// kimliği gerektirmez — ayrıca Türkçe doğruluğu çok daha yüksektir.
///
/// Akış: Mikrofonla kayıt (AudioService) → WAV 16kHz mono → Whisper → metin →
/// TurkishVocabulary.Correct (özel isim otomatik düzeltme).
///
/// MODEL SEÇİMİ: Kullanıcı Ayarlar → Ses Tanıma bölümünden model seçebilir
/// (Minimum 190MB → Maximum 3,1GB, 4 katman). Seçim Preferences'da ("stt_model")
/// saklanır, varsayılan "small-q5_1" (hız/doğruluk dengesi). İlk kullanımda
/// HuggingFace'ten indirilir, uygulama veri klasöründe önbelleğe alınır. Model
/// geçişinde eski model yalnızca yeni model HAZIR olduktan sonra silinir —
/// çevrimdışı kullanıcı çalışan modelini kaybetmesin. Model Yönetimi modalından
/// kurulu modeller silinebilir (aktif model hariç).
/// </summary>
public class SpeechToTextService : INotifyPropertyChanged
{
    private const string ModelPreferenceKey = "stt_model";
    private const string ProviderPreferenceKey = "stt_provider";
    private const long MinModelSizeBytes = 1_000_000; // indirilmiş/bozuk dosya koruması

    // Bulut sağlayıcılar (tek örnek — DI'sız, anahtarlar Preferences'tan okunur)
    private readonly IReadOnlyDictionary<string, ISpeechTranscriber> _cloudTranscribers;

    private bool _isAvailable;
    private bool _isModelReady;
    private bool _isDownloading;
    private double _modelDownloadProgress;
    private long _modelDownloadedBytes;
    private long _modelDownloadTotalBytes;
    private double _modelDownloadSpeedBytesPerSecond;
    private string _statusMessage = string.Empty;
    private CancellationTokenSource? _downloadCts;
    // Uygulama ömrü boyunca singleton olarak tutulur (model bellekte ~200MB+);
    // bilinçli olarak dispose edilmez — process çıkışıyla temizlenir. Servis transient
    // yapılırsa bu alanın IDisposable ile temizlenmesi gerekir.
    private WhisperFactory? _factory;
    private readonly object _factoryLock = new();
    private WhisperModelInfo _selectedModel;
    private SpeechProviderInfo _selectedProvider;

    public SpeechToTextService()
    {
        var savedModel = Preferences.Default.Get(ModelPreferenceKey, WhisperModelCatalog.DefaultId);
        _selectedModel = WhisperModelCatalog.GetById(savedModel);
        var savedProvider = Preferences.Default.Get(ProviderPreferenceKey, SpeechProviderCatalog.DefaultId);
        _selectedProvider = SpeechProviderCatalog.GetById(savedProvider);

        _cloudTranscribers = new Dictionary<string, ISpeechTranscriber>
        {
            ["openai"] = new OpenAiCompatibleTranscriber("openai", "https://api.openai.com/v1", "gpt-4o-mini-transcribe"),
            ["groq"] = new OpenAiCompatibleTranscriber("groq", "https://api.groq.com/openai/v1", "whisper-large-v3-turbo"),
            ["fireworks"] = new OpenAiCompatibleTranscriber("fireworks", "https://api.fireworks.ai/inference/v1", "accounts/fireworks/models/whisper-v3"),
            ["deepgram"] = new DeepgramTranscriber(),
            ["elevenlabs"] = new ElevenLabsTranscriber(),
            ["assemblyai"] = new AssemblyAiTranscriber(),
            ["google"] = new GoogleTranscriber(),
            ["azure"] = new AzureTranscriber()
        };

#if WINDOWS
        EnsureNativeLibrary();
        // whisper.cpp native bu pakette geliyor → Windows'ta her zaman kullanılabilir.
        // Model dosyası gerektiğinde EnsureModelAsync ile indirilir.
        _isAvailable = true;
        IsModelReady = IsModelFileReady(SelectedModel);
        StatusMessage = IsModelReady ? "Hazır" : "Henüz indirilmedi";

        Log($"STT init: available={IsAvailable} model={SelectedModel.Id} provider={SelectedProvider.Id} modelReady={IsModelReady} modelPath={ModelPath}");
#else
        _isAvailable = false;
#endif
    }

    /// <summary>Uygulamanın ses tanıma yeteneği var mı (Windows: her zaman evet — Whisper yerel).</summary>
    public bool IsAvailable
    {
        get => _isAvailable;
        private set => SetProperty(ref _isAvailable, value);
    }

    /// <summary>Seçili Whisper modeli indirilmiş ve kullanıma hazır mı?</summary>
    public bool IsModelReady
    {
        get => _isModelReady;
        private set => SetProperty(ref _isModelReady, value);
    }

    /// <summary>Model indiriliyor mu?</summary>
    public bool IsDownloading
    {
        get => _isDownloading;
        private set => SetProperty(ref _isDownloading, value);
    }

    /// <summary>Model indirme ilerlemesi (0..1).</summary>
    public double ModelDownloadProgress
    {
        get => _modelDownloadProgress;
        private set => SetProperty(ref _modelDownloadProgress, value);
    }

    /// <summary>Şu ana kadar indirilen byte (modal detayı).</summary>
    public long ModelDownloadedBytes
    {
        get => _modelDownloadedBytes;
        private set => SetProperty(ref _modelDownloadedBytes, value);
    }

    /// <summary>Toplam indirme boyutu (byte; bilinmiyorsa 0).</summary>
    public long ModelDownloadTotalBytes
    {
        get => _modelDownloadTotalBytes;
        private set => SetProperty(ref _modelDownloadTotalBytes, value);
    }

    /// <summary>Anlık indirme hızı (byte/sn).</summary>
    public double ModelDownloadSpeedBytesPerSecond
    {
        get => _modelDownloadSpeedBytesPerSecond;
        private set => SetProperty(ref _modelDownloadSpeedBytesPerSecond, value);
    }

    /// <summary>İndirmeyi iptal eder (kısmi dosya temizlenir).</summary>
    public void CancelModelDownload()
    {
        try
        {
            _downloadCts?.Cancel();
        }
        catch { }
    }

    /// <summary>Ayarlar ekranı için son durum mesajı ("Hazır", "İndiriliyor %45" ...).</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public event EventHandler<double>? ModelDownloadProgressChanged;

    /// <summary>Şu an seçili model (Ayarlar'da gösterilir).</summary>
    public WhisperModelInfo SelectedModel => _selectedModel;

    /// <summary>Şu an seçili transkripsiyon kaynağı (çevrimdışı/bulut).</summary>
    public SpeechProviderInfo SelectedProvider => _selectedProvider;

    /// <summary>Bulut sağlayıcı anahtarını kaydeder/okur (katalog id'sine göre).</summary>
    public void SetProviderApiKey(string providerId, string key)
    {
        CloudTranscribers.SaveApiKey(providerId, key);
        OnPropertyChanged(nameof(SelectedProvider));
    }

    public bool IsProviderConfigured(SpeechProviderInfo provider) =>
        provider.Id == "offline" ||
        (_cloudTranscribers.TryGetValue(provider.Id, out var t) && t.IsConfigured);

    /// <summary>Sağlayıcı değiştirir (Ayarlar). Aynı sağlayıcıda yazım/log atlanır.</summary>
    public void SwitchProvider(SpeechProviderInfo provider)
    {
        if (provider.Id == _selectedProvider.Id)
            return;

        _selectedProvider = provider;
        Preferences.Default.Set(ProviderPreferenceKey, provider.Id);
        OnPropertyChanged(nameof(SelectedProvider));
        Log($"STT provider switched: {provider.Id}");
    }

    /// <summary>Bağlantı testi (yalnız bulut sağlayıcılar için).</summary>
    public Task<bool> TestProviderConnectionAsync(string providerId) =>
        _cloudTranscribers.TryGetValue(providerId, out var t)
            ? t.TestConnectionAsync()
            : Task.FromResult(false);

    /// <summary>Seçili modelin diskteki yolu.</summary>
    public string ModelPath => Path.Combine(FileSystem.AppDataDirectory, "models", SelectedModel.FileName);

    /// <summary>Seçili modelin diskteki güncel boyutu (byte) — 0 ise indirilmemiş.</summary>
    public long SelectedModelSizeOnDisk
    {
        get
        {
            try
            {
                var path = ModelPath;
                return File.Exists(path) ? new FileInfo(path).Length : 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    // ---- Model yönetimi (Model Yönetimi modalı) ----

    /// <summary>Herhangi bir katalog modelinin diskte kurulu olup olmadığı.</summary>
    public bool IsModelInstalled(WhisperModelInfo model)
    {
        try
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, "models", model.FileName);
            return File.Exists(path) && new FileInfo(path).Length > MinModelSizeBytes;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Herhangi bir katalog modelinin diskteki boyutu (byte) — 0 ise kurulu değil.</summary>
    public long GetModelSizeOnDisk(WhisperModelInfo model)
    {
        try
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, "models", model.FileName);
            return File.Exists(path) ? new FileInfo(path).Length : 0;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>Models klasörünün toplam disk kullanımı (byte) — tüm kurulu modeller.</summary>
    public long ModelDirectoryTotalBytes
    {
        get
        {
            try
            {
                var dir = Path.Combine(FileSystem.AppDataDirectory, "models");
                if (!Directory.Exists(dir))
                    return 0;
                long total = 0;
                foreach (var f in Directory.EnumerateFiles(dir))
                {
                    try { total += new FileInfo(f).Length; } catch { }
                }
                return total;
            }
            catch
            {
                return 0;
            }
        }
    }

    /// <summary>
    /// Kurulu bir modeli diskten siler. GÜVENLİK: indirme sürüyorsa veya model ŞU AN
    /// SEÇİLİ ise silinmez (aktif model silinirse ses tanıma bozulur) — önce başka
    /// bir modele geçilmelidir. Başarılıysa true.
    /// </summary>
    public bool DeleteModel(WhisperModelInfo model)
    {
        if (model == null)
            return false;

        if (IsDownloading)
            return false;

        // Aktif model silinemez — kullanıcı önce başka modele geçmeli
        if (model.Id == SelectedModel.Id)
            return false;

        try
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, "models", model.FileName);
            if (!File.Exists(path))
                return true; // zaten yok — başarılı say

            // whisper.cpp model dosyası transkripsiyon sırasında belleğe alınır;
            // kilitli kalırsa (nadir) kısa bir bekleyişle tekrar dene.
            for (var i = 0; i < 3; i++)
            {
                try
                {
                    File.Delete(path);
                    break;
                }
                catch (IOException) when (i < 2)
                {
                    Thread.Sleep(300);
                }
            }

            OnPropertyChanged(nameof(ModelDirectoryTotalBytes));
            return !File.Exists(path);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Model değiştirir: yeni modeli indirir, hazır olunca factory'yi sıfırlar,
    /// eski modeli temizler. Başarısız olursa eski model korunur (çevrimdışı güvenlik).
    /// </summary>
    public async Task<bool> SwitchModelAsync(WhisperModelInfo model)
    {
        if (model.Id == SelectedModel.Id && IsModelReady)
            return true;

        if (IsDownloading)
            return false;

        var previous = SelectedModel;
        _selectedModel = model;
        Preferences.Default.Set(ModelPreferenceKey, model.Id);
        OnPropertyChanged(nameof(SelectedModel));
        OnPropertyChanged(nameof(ModelPath));

        // KRİTİK: IsModelReady'yi yeni modelin durumuna göre sıfırla — aksi halde
        // eski model hazırsa EnsureModelAsync anında true döner, yeni model hiç
        // indirilmez ve factory var olmayan dosyayı açmaya çalışır.
        IsModelReady = IsModelFileReady(model);
        StatusMessage = IsModelReady ? "Hazır" : "Model indiriliyor…";

        var success = await EnsureModelAsync();

        if (success)
        {
            lock (_factoryLock)
            {
                _factory?.Dispose();
                _factory = null;
            }
            Log($"STT model switched: {previous.Id} → {model.Id}");
            return true;
        }

        // Başarısız: eski modele geri dön (çalışan model silinmesin)
        _selectedModel = previous;
        Preferences.Default.Set(ModelPreferenceKey, previous.Id);
        OnPropertyChanged(nameof(SelectedModel));
        OnPropertyChanged(nameof(ModelPath));
        IsModelReady = IsModelFileReady(previous);
        return false;
    }

    /// <summary>
    /// Seçili model dosyasını (yoksa) indirir ve önbelleğe alır. Zaten varsa anında döner.
    /// </summary>
    public async Task<bool> EnsureModelAsync()
    {
#if WINDOWS
        EnsureNativeLibrary();

        if (IsModelReady)
            return true;

        if (IsDownloading)
            return false;

        var modelPath = ModelPath;
        var model = SelectedModel;
        try
        {
            IsDownloading = true;
            ModelDownloadProgress = 0;
            ModelDownloadedBytes = 0;
            ModelDownloadTotalBytes = 0;
            ModelDownloadSpeedBytesPerSecond = 0;
            StatusMessage = "Model indiriliyor…";
            _downloadCts = new CancellationTokenSource();

            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            var tempPath = modelPath + ".part";

            // İndirme kapsam bloğu içinde yapılır; akışlar kapsam sonunda kapanır,
            // böylece File.Move kilitli dosya hatası almaz.
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TodoVoice/1.0");

                using var response = await client.GetAsync(model.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? 0L;
                ModelDownloadTotalBytes = total;
                using var source = await response.Content.ReadAsStreamAsync();
                using var destination = File.Create(tempPath);

                var buffer = new byte[81920];
                long read = 0;
                int bytesRead;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                while ((bytesRead = await source.ReadAsync(buffer, _downloadCts.Token)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead));
                    read += bytesRead;
                    ModelDownloadedBytes = read;
                    if (total > 0)
                    {
                        ModelDownloadProgress = (double)read / total;
                        StatusMessage = $"Model indiriliyor %{(int)(ModelDownloadProgress * 100)}…";
                        ModelDownloadProgressChanged?.Invoke(this, ModelDownloadProgress);
                    }

                    var elapsed = stopwatch.Elapsed.TotalSeconds;
                    if (elapsed > 0.4)
                        ModelDownloadSpeedBytesPerSecond = read / elapsed;
                }

                await destination.FlushAsync();
            }

            // Akışlar kapandı → modeli kalıcı adına taşı ve boyutunu doğrula
            if (File.Exists(modelPath))
                File.Delete(modelPath);
            File.Move(tempPath, modelPath);

            if (new FileInfo(modelPath).Length <= MinModelSizeBytes)
            {
                File.Delete(modelPath);
                Log($"STT model download failed: dosya çok küçük");
                StatusMessage = "İndirme başarısız";
                return false;
            }

            IsModelReady = true;
            StatusMessage = "Hazır";
            return true;
        }
        catch (OperationCanceledException)
        {
            Log($"STT model download cancelled: {model.Id}");
            StatusMessage = "İndirme iptal edildi";
            try
            {
                var tempPath = modelPath + ".part";
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch { }
            return false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Whisper model download failed: {ex.Message}");
            Log($"STT model download failed: {ex}");
            StatusMessage = "İndirme başarısız";
            try
            {
                var tempPath = modelPath + ".part";
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
            catch { }
            return false;
        }
        finally
        {
            IsDownloading = false;
            _downloadCts?.Dispose();
            _downloadCts = null;
            ModelDownloadSpeedBytesPerSecond = 0;
        }
#else
        return false;
#endif
    }

    /// <summary>
    /// WAV dosyasını transkript eder. Seçili kaynak bulut + anahtar tanımlıysa bulut
    /// kullanılır; bulut hatası/anahtar yoksa otomatik olarak çevrimdışı Whisper'a
    /// düşer (fallback) — kullanıcı hiçbir zaman çalışamaz durumda kalmaz.
    /// Sonuç boş/başarısızsa null döner.
    /// </summary>
    public async Task<string?> TranscribeFileAsync(string wavPath)
    {
#if WINDOWS
        // 1) Seçili kaynak bulutsa ve anahtar tanımlıysa önce bulutu dene
        if (SelectedProvider.Id != "offline" &&
            _cloudTranscribers.TryGetValue(SelectedProvider.Id, out var transcriber) &&
            transcriber.IsConfigured)
        {
            try
            {
                var cloudText = await transcriber.TranscribeAsync(wavPath);
                if (!string.IsNullOrWhiteSpace(cloudText))
                {
                    Log($"STT cloud OK: provider={SelectedProvider.Id}");
                    return TurkishVocabulary.Correct(cloudText);
                }
            }
            catch (Exception ex)
            {
                Log($"STT cloud failed ({SelectedProvider.Id}), offline fallback: {ex.Message}");
            }
        }

        // 2) Çevrimdışı Whisper (her zaman kullanılabilir)
        return await TranscribeOfflineAsync(wavPath);
#else
        return null;
#endif
    }

    /// <summary>Çevrimdışı Whisper transkripsiyonu — mevcut kanıtlanmış yol.</summary>
    public async Task<string?> TranscribeOfflineAsync(string wavPath)
    {
#if WINDOWS
        if (!await EnsureModelAsync())
            throw new InvalidOperationException("Ses tanıma modeli indirilemedi. İnternet bağlantınızı kontrol edip tekrar deneyin.");

        var samples = WavAudioReader.ReadMono16kHz(wavPath);
        if (samples == null || samples.Length == 0)
            return null;

        return await Task.Run(() =>
        {
            EnsureNativeLibrary();
            var text = string.Empty;

            // Factory'yi KİLİT altında al ve Process boyunca kilidi tut: SwitchModelAsync
            // aynı kilidi kullanarak dispose eder — transkripsiyon sürerken model
            // değişirse dispose edilmiş factory üzerinde işlem yapılmaz (yarış giderildi).
            lock (_factoryLock)
            {
                var factory = GetFactory();

                // Whisper.net 1.9: Process senkron ve void — segmentler event handler ile gelir.
                // WithPrompt: bilinen şirket/kişi isimleri decode'a önyüklenir (Türkçe doğruluğu artırır).
                using var processor = factory.CreateBuilder()
                    .WithLanguage("tr")
                    .WithThreads(Math.Max(2, Environment.ProcessorCount / 2))
                    .WithPrompt(TurkishVocabulary.InitialPrompt)
                    .WithSegmentEventHandler(segment =>
                    {
                        var part = segment.Text?.Trim();
                        if (!string.IsNullOrWhiteSpace(part))
                            text += part + " ";
                    })
                    .Build();

                processor.Process(samples);
            }
            text = text.Trim();

            if (string.IsNullOrWhiteSpace(text))
                return null;

            // Özel isimleri kanonik yazımla düzelt (Google, Türk Hava Yolları, Elon Musk...)
            return TurkishVocabulary.Correct(text);
        });
#else
        return null;
#endif
    }

    private bool IsModelFileReady(WhisperModelInfo model)
    {
        try
        {
            var path = Path.Combine(FileSystem.AppDataDirectory, "models", model.FileName);
            return File.Exists(path) && new FileInfo(path).Length > MinModelSizeBytes;
        }
        catch
        {
            return false;
        }
    }

#if WINDOWS
    private static void Log(string message)
    {
        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "app.log"),
                DateTime.Now.ToString("HH:mm:ss") + " " + message + Environment.NewLine);
        }
        catch { }
    }

    private WhisperFactory GetFactory()
    {
        if (_factory == null)
        {
            lock (_factoryLock)
            {
                _factory ??= WhisperFactory.FromPath(ModelPath);
            }
        }
        return _factory;
    }

    /// <summary>
    /// Whisper.net.Runtime paketi native DLL'i runtimes/win-x64/ altına koyar;
    /// DllImport'ın bulabilmesi için exe'nin yanına kopyalanır (tek seferlik).
    /// </summary>
    private static void EnsureNativeLibrary()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var target = Path.Combine(baseDir, "whisper.dll");
            if (File.Exists(target))
                return;

            var candidates = new[]
            {
                Path.Combine(baseDir, "runtimes", "win-x64", "whisper.dll"),
                Path.Combine(baseDir, "runtimes", "win-x64", "native", "whisper.dll"),
            };

            foreach (var candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    File.Copy(candidate, target);
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"EnsureNativeLibrary failed: {ex.Message}");
        }
    }
#endif

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T backingStore, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(backingStore, value))
            return false;

        backingStore = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
