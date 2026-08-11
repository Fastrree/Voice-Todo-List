using TodoVoiceMaui.Models;

namespace TodoVoiceMaui.Services;

/// <summary>
/// Tek bir modelin indirme işi. Çoklu eşzamanlı indirme desteği: her modelin
/// KENDİ ilerleme çubuğu, byte/hız ve iptal akışı vardır (Model Yönetimi modalı
/// her satırı kendi işine bağlar). `SpeechToTextService` bu işleri listelerde
/// tutar ve `DownloadStateChanged` ile UI'ı uyarır.
///
/// Thread modeli: iş, UI bağlamında await edilen `RunJobAsync` içinde ilerlediği
/// için güncellemeler UI thread'inde yayınlanır (mevcut tek-indirme kodunun
/// davranışının aynısı). `Completion.Task` işin sonucunu (başarı/başarısızlık)
/// bekleyen herkese verir — kuyruktaki aynı modele yapılan ikinci istek bu
/// göreve bağlanır, ikinci indirme başlamaz.
/// </summary>
public sealed class ModelDownloadJob
{
    public ModelDownloadJob(WhisperModelInfo model)
    {
        Model = model;
    }

    /// <summary>İndirilen model (katalog referansı).</summary>
    public WhisperModelInfo Model { get; }

    /// <summary>İptal belirteci (indirme döngüsü tarafından atanır).</summary>
    public CancellationTokenSource? Cts { get; set; }

    /// <summary>İşin sonucu — başarı/başarısızlık. İndirme başlayınca tamamlanmamıştır.</summary>
    public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private bool _isActive;

    /// <summary>İndirme şu an sürüyor mu?</summary>
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
                return;
            _isActive = value;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private double _progress;

    /// <summary>İndirme ilerlemesi (0..1).</summary>
    public double Progress
    {
        get => _progress;
        set
        {
            if (Math.Abs(_progress - value) < 0.0001)
                return;
            _progress = value;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private long _downloadedBytes;

    /// <summary>Şu ana kadar inen byte.</summary>
    public long DownloadedBytes
    {
        get => _downloadedBytes;
        set
        {
            if (_downloadedBytes == value)
                return;
            _downloadedBytes = value;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private long _totalBytes;

    /// <summary>Toplam indirme boyutu (byte; bilinmiyorsa 0).</summary>
    public long TotalBytes
    {
        get => _totalBytes;
        set
        {
            if (_totalBytes == value)
                return;
            _totalBytes = value;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private double _speedBytesPerSecond;

    /// <summary>Anlık indirme hızı (byte/sn).</summary>
    public double SpeedBytesPerSecond
    {
        get => _speedBytesPerSecond;
        set
        {
            if (Math.Abs(_speedBytesPerSecond - value) < 0.01)
                return;
            _speedBytesPerSecond = value;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>İlerleme / hız / aktiflik değişince tetiklenir (UI tazelemesi için).</summary>
    public event EventHandler? StateChanged;

    /// <summary>Bu işin indirmesini iptal eder (kısmi dosya servis tarafında temizlenir).</summary>
    public void Cancel() => Cts?.Cancel();
}
