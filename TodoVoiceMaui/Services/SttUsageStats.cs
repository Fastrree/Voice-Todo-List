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
/// - Günlük sayaç: başarılı transkripsiyonlar `yyyyMMdd` anahtarıyla birikir
///   (son 7 gün çubuk grafiği için); 30 günden eski günler temizlenir.
/// - Değişim: `Changed` event'i ile UI tazelenir (thread-safe; UI thread'ine
///   marshal edilmesi abone tarafından yapılır — kayıtlar Task.Run içinden gelebilir).
/// </summary>
public static class SttUsageStats
{
    private sealed class StatsFile
    {
        public Dictionary<string, ProviderStat> Stats { get; set; } = new();
        public Dictionary<string, int> Daily { get; set; } = new();
    }

    private static readonly object _lock = new();
    private static Dictionary<string, ProviderStat> _stats = new();
    private static Dictionary<string, int> _dailyCounts = new();

    /// <summary>İstatistikler değişince tetiklenir (deneme/başarı/hata/sıfırlama).</summary>
    public static event Action? Changed;

    private static string StatsPath => Path.Combine(FileSystem.AppDataDirectory, "stt_usage_stats.json");

    static SttUsageStats()
    {
        try
        {
            if (File.Exists(StatsPath))
            {
                var json = File.ReadAllText(StatsPath);
                var file = JsonSerializer.Deserialize<StatsFile>(json);

                // Göç: eski format (kökünde düz sağlayıcı sözlüğü) yeni StatsFile
                // sarmalayıcısına boş dönerdi — eski istatistikler SESSİZCE kaybolmasın.
                if (file == null || (file.Stats.Count == 0 && file.Daily.Count == 0))
                {
                    var legacy = JsonSerializer.Deserialize<Dictionary<string, ProviderStat>>(json);
                    if (legacy is { Count: > 0 })
                        file = new StatsFile { Stats = legacy };
                }

                _stats = file?.Stats ?? new Dictionary<string, ProviderStat>();
                _dailyCounts = file?.Daily ?? new Dictionary<string, int>();
            }
        }
        catch
        {
            _stats = new Dictionary<string, ProviderStat>();
            _dailyCounts = new Dictionary<string, int>();
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

    /// <summary>Başarılı transkripsiyon kaydeder (+ günlük sayaç artırır).</summary>
    public static void RecordSuccess(string providerId, int chars)
    {
        lock (_lock)
        {
            var stat = GetOrCreate(providerId);
            stat.Successes++;
            stat.TotalChars += Math.Max(0, chars);
            stat.LastUsed = DateTime.Now;

            var dayKey = DateTime.Now.ToString("yyyyMMdd");
            _dailyCounts[dayKey] = _dailyCounts.GetValueOrDefault(dayKey) + 1;
            PruneDailyLocked();

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

    /// <summary>Tüm istatistikleri (sağlayıcı + günlük sayaçlar) siler ve kaydeder.</summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _stats = new Dictionary<string, ProviderStat>();
            _dailyCounts = new Dictionary<string, int>();
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

    /// <summary>Günlük başarılı transkripsiyon sayılarının kopyası (`yyyyMMdd` → sayı).</summary>
    public static IReadOnlyDictionary<string, int> GetDailyCounts()
    {
        lock (_lock)
        {
            return new Dictionary<string, int>(_dailyCounts);
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

    /// <summary>30 günden eski günlük kayıtları temizler (dosya küçük kalır).</summary>
    private static void PruneDailyLocked()
    {
        var cutoff = DateTime.Today.AddDays(-30).ToString("yyyyMMdd");
        var stale = _dailyCounts.Where(kv => string.CompareOrdinal(kv.Key, cutoff) < 0).Select(kv => kv.Key).ToList();
        foreach (var key in stale)
            _dailyCounts.Remove(key);
    }

    private static void SaveLocked()
    {
        try
        {
            var file = new StatsFile { Stats = _stats, Daily = _dailyCounts };
            File.WriteAllText(StatsPath, JsonSerializer.Serialize(file));
        }
        catch
        {
            // istatistik birikimi uygulamayı asla kırmasın
        }
    }
}
