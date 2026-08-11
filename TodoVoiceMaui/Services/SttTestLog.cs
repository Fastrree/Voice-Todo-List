namespace TodoVoiceMaui.Services;

/// <summary>Konsol satırı tipi — UI'da satır rengini belirler.</summary>
public enum SttLogKind
{
    /// <summary>Bilgi / istek / akış (mavi).</summary>
    Info,

    /// <summary>Başarı (yeşil).</summary>
    Success,

    /// <summary>Hata (kırmızı).</summary>
    Error,

    /// <summary>Uyarı / dikkat (sarı).</summary>
    Warning,

    /// <summary>İndirme (camgöbeği).</summary>
    Download
}

/// <summary>Zaman damgalı konsol satırı + tipi.</summary>
public readonly record struct SttLogEntry(string Text, SttLogKind Kind);

/// <summary>
/// Ses tanıma işlemlerinin (bağlantı testleri, indirme, çevrimdışı transkripsiyon)
/// CANLI, RENKLİ konsol çıktısı. Transkriberlar tipli metotlarla satır üretir
/// (Write=bilgi, WriteSuccess, WriteError, WriteWarning, WriteDownload); Ayarlar
/// sayfası ve Model Yönetimi modalı `Line` event'ine abone olup satır tipine göre
/// renklendirilmiş terminal görünümünde akıtır.
///
/// Aynı satırlar teşhis için `app.log`'a da yazılır. Statik olduğundan abonelerin
/// Dispose'da ayrılması gerekir (sızıntı yok).
/// </summary>
public static class SttTestLog
{
    /// <summary>Yeni konsol satırı (zaman damgalı + tip). UI aboneleri bunu dinler.</summary>
    public static event Action<SttLogEntry>? Line;

    public static void Write(string line) => Emit(line, SttLogKind.Info);
    public static void WriteSuccess(string line) => Emit(line, SttLogKind.Success);
    public static void WriteError(string line) => Emit(line, SttLogKind.Error);
    public static void WriteWarning(string line) => Emit(line, SttLogKind.Warning);
    public static void WriteDownload(string line) => Emit(line, SttLogKind.Download);

    private static void Emit(string line, SttLogKind kind)
    {
        var entry = new SttLogEntry($"[{DateTime.Now:HH:mm:ss}] {line}", kind);

        // Aboneleri TEK TEK çağır — bir abone hata atarsa diğerleri etkilenmesin
        var handler = Line;
        if (handler != null)
        {
            foreach (Action<SttLogEntry> subscriber in handler.GetInvocationList())
            {
                try
                {
                    subscriber(entry);
                }
                catch { }
            }
        }

        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "app.log"),
                entry.Text + Environment.NewLine);
        }
        catch { }
    }
}

/// <summary>Konsol satır tipleri için renk paleti (koyu terminal temasına uygun).</summary>
public static class SttConsolePalette
{
    public static readonly Color Info = Color.FromArgb("#60A5FA");     // mavi
    public static readonly Color Success = Color.FromArgb("#4ADE80");  // yeşil
    public static readonly Color Error = Color.FromArgb("#F87171");    // kırmızı
    public static readonly Color Warning = Color.FromArgb("#FBBF24");  // sarı
    public static readonly Color Download = Color.FromArgb("#22D3EE"); // camgöbeği

    public static Color For(SttLogKind kind) => kind switch
    {
        SttLogKind.Success => Success,
        SttLogKind.Error => Error,
        SttLogKind.Warning => Warning,
        SttLogKind.Download => Download,
        _ => Info
    };
}
