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
    private string _statusMessage = string.Empty;

    // Çoklu eşzamanlı indirme: her model kendi işiyle (ModelDownloadJob) iner.
    private readonly List<ModelDownloadJob> _downloads = new();
    private readonly object _downloadsLock = new();
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

    /// <summary>Şu an herhangi bir model indiriliyor mu? (Seçili olması gerekmez — çoklu.)</summary>
    public bool IsDownloading
    {
        get
        {
            lock (_downloadsLock)
                return _downloads.Any(j => j.IsActive);
        }
    }

    /// <summary>SEÇİLİ modelin indirme ilerlemesi (0..1) — Ayarlar kartı için geriye dönük uyumlu.</summary>
    public double ModelDownloadProgress => SelectedModelJob?.Progress ?? 0;

    /// <summary>SEÇİLİ modelin şu ana kadar inen byte'ı.</summary>
    public long ModelDownloadedBytes => SelectedModelJob?.DownloadedBytes ?? 0;

    /// <summary>SEÇİLİ modelin toplam indirme boyutu (byte; bilinmiyorsa 0).</summary>
    public long ModelDownloadTotalBytes => SelectedModelJob?.TotalBytes ?? 0;

    /// <summary>SEÇİLİ modelin anlık indirme hızı (byte/sn).</summary>
    public double ModelDownloadSpeedBytesPerSecond => SelectedModelJob?.SpeedBytesPerSecond ?? 0;

    /// <summary>Seçili modelin aktif indirme işi (yoksa null).</summary>
    public ModelDownloadJob? SelectedModelJob => GetDownloadJob(SelectedModel);

    /// <summary>Bir modele ait indirme işi (aktif ya da bitmiş; yoksa null).</summary>
    public ModelDownloadJob? GetDownloadJob(WhisperModelInfo model)
    {
        if (model == null)
            return null;
        lock (_downloadsLock)
            return _downloads.FirstOrDefault(j => j.Model.Id == model.Id);
    }

    /// <summary>Şu anki tüm indirme işlerinin kopyası (UI için).</summary>
    public IReadOnlyList<ModelDownloadJob> Downloads
    {
        get
        {
            lock (_downloadsLock)
                return _downloads.ToList();
        }
    }

    /// <summary>Bir modelin indirmesi sürüyor mu? (Silme/geçiş korumaları için.)</summary>
    public bool IsModelDownloading(WhisperModelInfo model) => GetDownloadJob(model)?.IsActive == true;

    /// <summary>
    /// İndirmeyi iptal eder. `model` verilirse yalnız o modelin işi iptal edilir;
    /// verilmezse (null) TÜM aktif işler iptal edilir. Kısmi dosyalar işin
    /// finally bloğunda temizlenir.
    /// </summary>
    public void CancelModelDownload(WhisperModelInfo? model = null)
    {
        try
        {
            lock (_downloadsLock)
            {
                var jobs = model == null
                    ? _downloads.Where(j => j.IsActive).ToList()
                    : _downloads.Where(j => j.Model.Id == model.Id && j.IsActive).ToList();
                foreach (var job in jobs)
                    job.Cancel();
            }
        }
        catch { }
    }

    /// <summary>Ayarlar ekranı için son durum mesajı ("Hazır", "İndiriliyor %45" ...).</summary>
    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    /// <summary>Geriye dönük uyumluluk: seçili modelin ilerlemesi (0..1) her chunk'ta yayınlanır.</summary>
    public event EventHandler<double>? ModelDownloadProgressChanged;

    /// <summary>Herhangi bir indirme işi başlayınca / ilerleyince / bitince tetiklenir.</summary>
    public event EventHandler? DownloadStateChanged;

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
        SttTestLog.Write($"Kaynak değişti: {provider.DisplayName}");
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

        // Bu modelin indirmesi sürüyorsa silinemez (çoklu indirmede yalnız kendi işi engeller)
        if (IsModelDownloading(model))
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

        var success = await DownloadModelAsync(model);

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

    /// <summary>Seçili modeli indirir (arka plan işi). Eşzamanlı diğer indirmeleri engellemez.</summary>
    public Task<bool> EnsureModelAsync() => DownloadModelAsync(SelectedModel);

    /// <summary>
    /// İstenen katalog modelini arka planda indirir — SEÇİMİ DEĞİŞTİRMEZ.
    /// Çoklu eşzamanlı indirme: her model kendi işiyle iner (her birinin kendi
    /// ilerleme çubuğu vardır — Model Yönetimi modalı her satırı kendi işine bağlar).
    /// Model zaten kuruluysa anında true; aynı modelin indirmesi sürüyorsa o işin
    /// tamamlanmasına bağlanır (çift indirme olmaz).
    /// </summary>
    public Task<bool> DownloadModelAsync(WhisperModelInfo model)
    {
#if WINDOWS
        EnsureNativeLibrary();

        if (model == null)
            return Task.FromResult(false);

        // Zaten kurulu → anında başarı (seçiliyse hazırlığı da güncelle)
        if (IsModelFileReady(model))
        {
            if (model.Id == SelectedModel.Id)
            {
                IsModelReady = true;
                StatusMessage = "Hazır";
            }
            return Task.FromResult(true);
        }

        lock (_downloadsLock)
        {
            var existing = _downloads.FirstOrDefault(j => j.Model.Id == model.Id);
            if (existing != null)
            {
                // Bitmiş bir iş ise listeden çıkar ve temiz başlat
                if (existing.Completion.Task.IsCompleted)
                    _downloads.Remove(existing);
                else
                    return existing.Completion.Task; // süren işe bağlan — ikinci indirme yok
            }

            var job = new ModelDownloadJob(model);
            _downloads.Add(job);
            // Fire-and-forget: tüm hatalar RunJobAsync içinde ele alınır,
            // sonuç job.Completion üzerinden yayınlanır.
            _ = RunJobAsync(job);
            return job.Completion.Task;
        }
#else
        return Task.FromResult(false);
#endif
    }

    /// <summary>
    /// Bir indirme işini yürütür: indir → doğrula → taşı → tamamla.
    /// `model` SEÇİLİYSE IsModelReady/StatusMessage güncellenir; değilse yalnız
    /// dosya kurulur (kullanıcı dilediği zaman seçer). Kısmi `.part` dosyası
    /// her durumda temizlenir; akışlar kapsam sonunda kapanır (File.Move kilitsiz).
    /// </summary>
    private async Task RunJobAsync(ModelDownloadJob job)
    {
        var model = job.Model;
        var modelPath = Path.Combine(FileSystem.AppDataDirectory, "models", model.FileName);
        var tempPath = modelPath + ".part";
        var downloadSeconds = 0.0;

        // NOT: `isSelected` değişkeni YAKALANMAZ — indirme sürerken kullanıcı seçimi
        // değiştirebilir (popup'ta indirilen model Ayarlar'dan seçilebilir). Tamamlama
        // noktalarında SEÇİM CANLI okunur, böylece IsModelReady her durumda doğru set edilir.

        try
        {
            job.Cts = new CancellationTokenSource();
            job.IsActive = true;
            job.Progress = 0;
            job.DownloadedBytes = 0;
            job.TotalBytes = 0;
            job.SpeedBytesPerSecond = 0;
            if (model.Id == SelectedModel.Id)
                StatusMessage = "Model indiriliyor…";
            SttTestLog.WriteDownload($"⬇ İndirme başladı: {model.DisplayName} ({model.SizeLabel})");
            RaiseDownloadStateChanged();

            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);

            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) })
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TodoVoice/1.0");

                using var response = await client.GetAsync(model.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? 0L;
                job.TotalBytes = total;
                using var source = await response.Content.ReadAsStreamAsync();
                using var destination = File.Create(tempPath);

                var buffer = new byte[81920];
                long read = 0;
                int bytesRead;
                var lastPercentLogged = 0;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                while ((bytesRead = await source.ReadAsync(buffer, job.Cts.Token)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead));
                    read += bytesRead;
                    job.DownloadedBytes = read;
                    if (total > 0)
                    {
                        job.Progress = (double)read / total;
                        if (model.Id == SelectedModel.Id)
                            StatusMessage = $"Model indiriliyor %{(int)(job.Progress * 100)}…";

                        // Canlı konsola her %10'da bir renkli satır (büyük modelde
                        // ilerleme terminalde izlenebilir; 3,1GB'ta ~10 satır).
                        var percent = (int)(job.Progress * 100);
                        if (percent >= lastPercentLogged + 10)
                        {
                            SttTestLog.WriteDownload(
                                $"⬇ {model.DisplayName} %{percent} · {FormatBytes(read)}/{FormatBytes(total)}");
                            lastPercentLogged = percent;
                        }
                    }

                    var elapsed = stopwatch.Elapsed.TotalSeconds;
                    if (elapsed > 0.4)
                        job.SpeedBytesPerSecond = read / elapsed;

                    ModelDownloadProgressChanged?.Invoke(this, SelectedModelJob?.Progress ?? 0);
                    RaiseDownloadStateChanged();
                }

                await destination.FlushAsync();
                downloadSeconds = stopwatch.Elapsed.TotalSeconds;
            }

            // Akışlar kapandı → kalıcı adına taşı ve boyutunu doğrula
            if (File.Exists(modelPath))
                File.Delete(modelPath);
            File.Move(tempPath, modelPath);

            if (new FileInfo(modelPath).Length <= MinModelSizeBytes)
            {
                File.Delete(modelPath);
                Log($"STT model download failed: dosya çok küçük");
                SttTestLog.WriteError("✗ İndirme başarısız: dosya çok küçük (muhtemelen bozuk/engellendi)");
                if (model.Id == SelectedModel.Id)
                    StatusMessage = "İndirme başarısız";
                job.Completion.TrySetResult(false);
                return;
            }

            if (model.Id == SelectedModel.Id)
            {
                IsModelReady = true;
                StatusMessage = "Hazır";
            }
            var finalSize = new FileInfo(modelPath).Length;
            var avgSpeed = downloadSeconds > 0 ? finalSize / downloadSeconds : 0;
            SttTestLog.WriteSuccess($"✓ İndirme tamamlandı: {model.DisplayName} ({FormatBytes(finalSize)}) · " +
                                    $"{FormatSeconds(downloadSeconds)} · ort. {FormatBytes((long)avgSpeed)}/sn");
            job.Completion.TrySetResult(true);
        }
        catch (OperationCanceledException)
        {
            Log($"STT model download cancelled: {model.Id}");
            SttTestLog.WriteWarning($"✗ İndirme iptal edildi: {model.DisplayName} (kısmi dosya temizlendi)");
            if (model.Id == SelectedModel.Id)
                StatusMessage = "İndirme iptal edildi";
            TryDelete(tempPath);
            job.Completion.TrySetResult(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Whisper model download failed: {ex.Message}");
            Log($"STT model download failed: {ex}");
            if (model.Id == SelectedModel.Id)
                StatusMessage = "İndirme başarısız";
            TryDelete(tempPath);
            job.Completion.TrySetResult(false);
        }
        finally
        {
            job.IsActive = false;
            job.SpeedBytesPerSecond = 0;
            job.Cts?.Dispose();
            job.Cts = null;
            lock (_downloadsLock)
                _downloads.Remove(job);
            RaiseDownloadStateChanged();
        }
    }

    /// <summary>Dosya varsa siler (kısmi `.part` temizliği) — hata yutulur.</summary>
    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { }
    }

    /// <summary>İndirme durumu değişince: türetilmiş özellikleri bildir + event'i yay (UI tazelemesi).</summary>
    private void RaiseDownloadStateChanged()
    {
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(ModelDownloadProgress));
        OnPropertyChanged(nameof(ModelDownloadedBytes));
        OnPropertyChanged(nameof(ModelDownloadTotalBytes));
        OnPropertyChanged(nameof(ModelDownloadSpeedBytesPerSecond));
        DownloadStateChanged?.Invoke(this, EventArgs.Empty);
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
            var audioSeconds = WavAudioReader.GetDurationSeconds(wavPath);
            SttTestLog.Write($"Kaynak: {SelectedProvider.DisplayName} — bulut deneniyor");
            try
            {
                SttUsageStats.RecordAttempt(SelectedProvider.Id, audioSeconds);
                var cloudText = await transcriber.TranscribeAsync(wavPath);
                if (!string.IsNullOrWhiteSpace(cloudText))
                {
                    SttUsageStats.RecordSuccess(SelectedProvider.Id, cloudText.Length);
                    Log($"STT cloud OK: provider={SelectedProvider.Id}");
                    SttTestLog.WriteSuccess($"✓ Bulut transkripsiyon tamam ({SelectedProvider.Id})");
                    return TurkishVocabulary.Correct(cloudText);
                }
                SttUsageStats.RecordFailure(SelectedProvider.Id);
                SttTestLog.WriteWarning("⚠ Bulut boş metin döndü — çevrimdışı deneniyor");
            }
            catch (Exception ex)
            {
                SttUsageStats.RecordFailure(SelectedProvider.Id);
                Log($"STT cloud failed ({SelectedProvider.Id}), offline fallback: {ex.Message}");
                SttTestLog.WriteError($"✗ Bulut hatası: {ex.Message} — çevrimdışı fallback");
            }
        }
        else if (SelectedProvider.Id != "offline")
        {
            SttTestLog.WriteWarning($"Kaynak {SelectedProvider.DisplayName} için anahtar yok — çevrimdışı kullanılıyor");
        }
        else
        {
            SttTestLog.Write($"Kaynak: Çevrimdışı Whisper ({SelectedModel.DisplayName})");
        }

        // 2) Çevrimdışı Whisper (her zaman kullanılabilir)
        return await TranscribeOfflineAsync(wavPath);
#else
        return null;
#endif
    }

    /// <summary>
    /// Çevrimdışı Whisper transkripsiyonu — mevcut kanıtlanmış yol.
    /// `trackStats=false` ise kullanım istatistiklerine yazılmaz (Ayarlar'daki
    /// sessiz test transkripsiyonu gibi teşhis amaçlı çağrılar istatistiği kirletmesin).
    /// </summary>
    public async Task<string?> TranscribeOfflineAsync(string wavPath, bool trackStats = true)
    {
#if WINDOWS
        if (!await EnsureModelAsync())
            throw new InvalidOperationException("Ses tanıma modeli indirilemedi. İnternet bağlantınızı kontrol edip tekrar deneyin.");

        var samples = WavAudioReader.ReadMono16kHz(wavPath);
        if (samples == null || samples.Length == 0)
        {
            SttTestLog.WriteError("✗ WAV okunamadı veya boş");
            return null;
        }
        var audioSeconds = samples.Length / 16000.0;
        if (trackStats)
            SttUsageStats.RecordAttempt("offline", audioSeconds);
        SttTestLog.Write($"WAV → 16kHz mono ({audioSeconds:0.0} sn)");

        return await Task.Run(() =>
        {
            EnsureNativeLibrary();
            var text = string.Empty;
            SttTestLog.Write($"Whisper işleniyor ({SelectedModel.DisplayName}, {SelectedModel.QuantizationLabel})…");

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
            {
                if (trackStats)
                    SttUsageStats.RecordFailure("offline");
                SttTestLog.WriteWarning("⚠ Whisper boş sonuç (konuşma algılanamadı)");
                return null;
            }

            // Özel isimleri kanonik yazımla düzelt (Google, Türk Hava Yolları, Elon Musk...)
            var corrected = TurkishVocabulary.Correct(text);
            if (trackStats)
                SttUsageStats.RecordSuccess("offline", corrected.Length);
            SttTestLog.WriteSuccess($"✓ Metin: {CloudTranscribers.TrimText(corrected)}");
            return corrected;
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

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024L * 1024L)
            return $"{bytes / (1024.0 * 1024.0 * 1024.0):0.0} GB";
        if (bytes >= 1024L * 1024L)
            return $"{bytes / (1024.0 * 1024.0):0.0} MB";
        return $"{bytes / 1024.0:0} KB";
    }

    private static string FormatSeconds(double seconds) =>
        seconds >= 60 ? $"{seconds / 60.0:0.0} dk" : $"{seconds:0.0} sn";

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
