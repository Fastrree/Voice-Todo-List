using System.ComponentModel;
using System.Runtime.CompilerServices;
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
/// Model: ggml-small-q5_1 (~181 MB, 96 dil, quantized small) — base'e göre
/// Türkçe'de %10-20 daha düşük WER (eklemeli dil yapısında küçük model belirgin
/// daha doğru); decode'a InitialPrompt ile bilinen şirket/kişi isimleri önyüklenir.
/// İlk kullanımda HuggingFace'ten indirilir, uygulama veri klasöründe önbelleğe alınır.
/// </summary>
public class SpeechToTextService : INotifyPropertyChanged
{
    private const string ModelUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small-q5_1.bin";
    private const string ModelFileName = "ggml-small-q5_1.bin";
    private const string LegacyModelFileName = "ggml-base.bin";
    private const long MinModelSizeBytes = 1_000_000; // indirilmiş/bozuk dosya koruması

    private bool _isAvailable;
    private bool _isModelReady;
    private bool _isDownloading;
    private double _modelDownloadProgress;
    // Uygulama ömrü boyunca singleton olarak tutulur (model ~142 MB bellekte);
    // bilinçli olarak dispose edilmez — process çıkışıyla temizlenir. Servis transient
    // yapılırsa bu alanın IDisposable ile temizlenmesi gerekir.
    private WhisperFactory? _factory;
    private readonly object _factoryLock = new();

    public SpeechToTextService()
    {
#if WINDOWS
        EnsureNativeLibrary();
        // whisper.cpp native bu pakette geliyor → Windows'ta her zaman kullanılabilir.
        // Model dosyası gerektiğinde EnsureModelAsync ile indirilir.
        _isAvailable = true;
        IsModelReady = File.Exists(ModelPath) && new FileInfo(ModelPath).Length > MinModelSizeBytes;

        // Eski ggml-base yalnızca yeni model ZATEN hazırsa temizlenir — çevrimdışı
        // kullanıcı çalışan modelini kaybetmesin (indirme başarısız olursa base kalır).
        if (IsModelReady)
            TryDeleteLegacyModel();

        Log($"STT init: available={IsAvailable} modelReady={IsModelReady} modelPath={ModelPath}");
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

    /// <summary>Whisper modeli indirilmiş ve kullanıma hazır mı?</summary>
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

    public event EventHandler<double>? ModelDownloadProgressChanged;

    public string ModelPath => Path.Combine(FileSystem.AppDataDirectory, "models", ModelFileName);

    /// <summary>
    /// Model dosyasını (yoksa) indirir ve önbelleğe alır. Zaten varsa anında döner.
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
        try
        {
            IsDownloading = true;
            ModelDownloadProgress = 0;

            Directory.CreateDirectory(Path.GetDirectoryName(modelPath)!);
            var tempPath = modelPath + ".part";

            // İndirme kapsam bloğu içinde yapılır; akışlar kapsam sonunda kapanır,
            // böylece File.Move kilitli dosya hatası almaz.
            using (var client = new HttpClient { Timeout = TimeSpan.FromMinutes(20) })
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TodoVoice/1.0");

                using var response = await client.GetAsync(ModelUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                var total = response.Content.Headers.ContentLength ?? 0L;
                using var source = await response.Content.ReadAsStreamAsync();
                using var destination = File.Create(tempPath);

                var buffer = new byte[81920];
                long read = 0;
                int bytesRead;
                while ((bytesRead = await source.ReadAsync(buffer)) > 0)
                {
                    await destination.WriteAsync(buffer.AsMemory(0, bytesRead));
                    read += bytesRead;
                    if (total > 0)
                    {
                        ModelDownloadProgress = (double)read / total;
                        ModelDownloadProgressChanged?.Invoke(this, ModelDownloadProgress);
                    }
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
                Log($"STT model download failed: dosya çok küçük ({new FileInfo(modelPath).Length})");
                return false;
            }

            IsModelReady = true;
            TryDeleteLegacyModel();
            return true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Whisper model download failed: {ex.Message}");
            Log($"STT model download failed: {ex}");
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
        }
#else
        return false;
#endif
    }

    /// <summary>
    /// WAV dosyasını transkript eder. Model hazır değilse önce indirir.
    /// Sonuç boş/başarısızsa null döner.
    /// </summary>
    public async Task<string?> TranscribeFileAsync(string wavPath)
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
            var factory = GetFactory();
            var text = string.Empty;

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

    /// <summary>Eski ggml-base önbelleğini temizle (yalnızca yeni model hazırken).</summary>
    private static void TryDeleteLegacyModel()
    {
        try
        {
            var legacyPath = Path.Combine(FileSystem.AppDataDirectory, "models", LegacyModelFileName);
            if (File.Exists(legacyPath))
                File.Delete(legacyPath);
        }
        catch { /* best-effort */ }
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
