namespace TodoVoiceMaui.Services;

/// <summary>
/// Ses tanıma işlemlerinin (bağlantı testleri, indirme, çevrimdışı transkripsiyon)
/// CANLI konsol çıktısı. Transkriberlar `Write` ile satır üretir; Ayarlar sayfası ve
/// Model Yönetimi modalı `Line` event'ine abone olup terminal görünümünde akıtır.
///
/// Aynı satırlar teşhis için `app.log`'a da yazılır (uygulama yanındaki dosya).
/// Statik olduğundan abonelerin Dispose'da ayrılması gerekir (sızıntı yok).
/// </summary>
public static class SttTestLog
{
    /// <summary>Yeni konsol satırı (zaman damgalı). UI aboneleri bunu dinler.</summary>
    public static event Action<string>? Line;

    public static void Write(string line)
    {
        var timestamped = $"[{DateTime.Now:HH:mm:ss}] {line}";

        // Aboneleri TEK TEK çağır — bir abone hata atarsa diğerleri etkilenmesin
        var handler = Line;
        if (handler != null)
        {
            foreach (Action<string> subscriber in handler.GetInvocationList())
            {
                try
                {
                    subscriber(timestamped);
                }
                catch { }
            }
        }

        try
        {
            System.IO.File.AppendAllText(
                System.IO.Path.Combine(AppContext.BaseDirectory, "app.log"),
                timestamped + Environment.NewLine);
        }
        catch { }
    }
}
