using System.Text.Json;

namespace TodoVoiceMaui.Services;

/// <summary>Tek bir sağlayıcının (çevrimdışı Whisper veya bulut) birikmiş kullanımı.</summary>
public sealed class ProviderStat
{
    public int Attempts { get; set; }
    public int Successes { get; set; }
    public int Failures { get; set; }

    /// <summary>İşleme alınan toplam ses süresi (saniye) — tüm denemeler.</summary>
    public double TotalAudioSeconds { get; set; }

    /// <summary>Başarılı transkripsiyonlardan dönen toplam karakter.</summary>
    public int TotalChars { get; set; }

    public DateTime LastUsed { get; set; }

    /// <summary>Başarı oranı (0..100) — deneme yoksa 0.</summary>
    public double SuccessRatePercent => Attempts <= 0 ? 0 : Successes * 100.0 / Attempts;
}

/// <summary>
/// Sağlayıcı başına kullanım istatistikleri (Ayarlar → KULLANIM İSTATİSTİKLERİ).
/// - Kayıt: SpeechToTextService her transkripsiyon denemesinde çağırır.
/// - Kalıcılık: AppDataDirectory/stt_usage_stats.json (System.Text.Json).
/// - Değişim: `Changed` event'i ile UI tazelenir (thread-safe; UI thread'ine
///   marshal edilmesi abone tarafından yapılır — kayıtlar Task.Run içinden gelebilir).
/// </summary>
public static class SttUsageStats
{
    private static readonly object _lock = new();
    private static Dictionary<string, ProviderStat> _stats = new();

    /// <summary>İstatistikler değişince tetiklenir (deneme/başarı/hata/sıfırlama).</summary>
    public static event Action? Changed;

    private static string StatsPath => Path.Combine(FileSystem.AppDataDirectory, "stt_usage_stats.json");

    static SttUsageStats()
    {
        try
        {
            if (File.Exists(StatsPath))
            {
                _stats = JsonSerializer.Deserialize<Dictionary<string, ProviderStat>>(File.ReadAllText(StatsPath))
                         ?? new Dictionary<string, ProviderStat>();
            }
        }
        catch
        {
            _stats = new Dictionary<string, ProviderStat>();
        }
    }

    /// <summary>Bir transkripsiyon denemesi kaydeder (süre toplama + son kullanım).</summary>
    public static void RecordAttempt(string providerId, double audioSeconds)
    {
        lock (_lock)
        {
            var stat = GetOrCreate(providerId);
            stat.Attempts++;
            stat.TotalAudioSeconds += Math.Max(0, audioSeconds);
            stat.LastUsed = DateTime.Now;
            SaveLocked();
        }
        Changed?.Invoke();
    }

    /// <summary>Başarılı transkripsiyon kaydeder.</summary>
    public static void RecordSuccess(string providerId, int chars)
    {
        lock (_lock)
        {
            var stat = GetOrCreate(providerId);
            stat.Successes++;
            stat.TotalChars += Math.Max(0, chars);
            stat.LastUsed = DateTime.Now;
            SaveLocked();
        }
        Changed?.Invoke();
    }

    /// <summary>Başarısız/boş transkripsiyon kaydeder.</summary>
    public static void RecordFailure(string providerId)
    {
        lock (_lock)
        {
            var stat = GetOrCreate(providerId);
            stat.Failures++;
            stat.LastUsed = DateTime.Now;
            SaveLocked();
        }
        Changed?.Invoke();
    }

    /// <summary>Tüm istatistikleri siler ve kaydeder (Ayarlar → Sıfırla).</summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _stats = new Dictionary<string, ProviderStat>();
            SaveLocked();
        }
        Changed?.Invoke();
    }

    /// <summary>Deneme sayısına göre sıralı, sağlayıcı id → istatistik anlık görüntüsü.</summary>
    public static IReadOnlyList<KeyValuePair<string, ProviderStat>> GetAll()
    {
        lock (_lock)
        {
            return _stats
                .OrderByDescending(kv => kv.Value.Attempts)
                .ThenByDescending(kv => kv.Value.LastUsed)
                .Select(kv => new KeyValuePair<string, ProviderStat>(kv.Key, new ProviderStat
                {
                    Attempts = kv.Value.Attempts,
                    Successes = kv.Value.Successes,
                    Failures = kv.Value.Failures,
                    TotalAudioSeconds = kv.Value.TotalAudioSeconds,
                    TotalChars = kv.Value.TotalChars,
                    LastUsed = kv.Value.LastUsed
                }))
                .ToList();
        }
    }

    private static ProviderStat GetOrCreate(string providerId)
    {
        if (!_stats.TryGetValue(providerId, out var stat))
        {
            stat = new ProviderStat();
            _stats[providerId] = stat;
        }
        return stat;
    }

    private static void SaveLocked()
    {
        try
        {
            File.WriteAllText(StatsPath, JsonSerializer.Serialize(_stats));
        }
        catch
        {
            // istatistik birikimi uygulamayı asla kırmasın
        }
    }
}
