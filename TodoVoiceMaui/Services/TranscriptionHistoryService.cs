using System.Text.Json;

namespace TodoVoiceMaui.Services;

/// <summary>Geçmişteki tek bir transkripsiyon kaydı.</summary>
public sealed class TranscriptionEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Text { get; set; } = string.Empty;

    /// <summary>Kullanıcı elle düzelttiyse düzeltilmiş metin (aksi hâlde null).</summary>
    public string? CorrectedText { get; set; }

    public string Provider { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}

/// <summary>
/// Transkripsiyon geçmişi — son başarılı ses tanımalarını tutar (kalıcı JSON).
///
/// Kullanıcı bir kaydı elle düzelttiğinde (`Correct`) metin güncellenir VE
/// yanlış→doğru kelime çiftleri <see cref="TurkishVocabulary"/> kullanıcı
/// sözlüğüne öğrenilir — zamanla kişiye özel tanıma oluşur.
///
/// Kalıcılık: AppDataDirectory/transcription_history.json (System.Text.Json),
/// en fazla <see cref="MaxEntries"/> kayıt tutulur (eskiler düşer).
/// </summary>
public static class TranscriptionHistoryService
{
    private const int MaxEntries = 100;

    private static readonly object _lock = new();
    private static List<TranscriptionEntry> _entries = new();

    /// <summary>Geçmiş değişince tetiklenir (UI tazeleme; UI thread'ine marshal abone yapar).</summary>
    public static event Action? Changed;

    private static string HistoryPath => Path.Combine(FileSystem.AppDataDirectory, "transcription_history.json");

    static TranscriptionHistoryService()
    {
        try
        {
            if (File.Exists(HistoryPath))
            {
                var loaded = JsonSerializer.Deserialize<List<TranscriptionEntry>>(File.ReadAllText(HistoryPath));
                if (loaded != null)
                    _entries = loaded;
            }
        }
        catch
        {
            _entries = new List<TranscriptionEntry>();
        }
    }

    /// <summary>Yeni bir başarılı transkripsiyon kaydeder (en yeni en üstte).</summary>
    public static void Add(string text, string provider)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        lock (_lock)
        {
            _entries.Insert(0, new TranscriptionEntry { Text = text.Trim(), Provider = provider });
            if (_entries.Count > MaxEntries)
                _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
            SaveLocked();
        }
        Changed?.Invoke();
    }

    /// <summary>Geçmişin kopyası (yeni → eski).</summary>
    public static IReadOnlyList<TranscriptionEntry> GetAll()
    {
        lock (_lock)
        {
            return _entries
                .Select(e => new TranscriptionEntry
                {
                    Id = e.Id,
                    Text = e.Text,
                    CorrectedText = e.CorrectedText,
                    Provider = e.Provider,
                    CreatedAt = e.CreatedAt
                })
                .ToList();
        }
    }

    /// <summary>
    /// Bir kaydı kullanıcı düzeltmesiyle günceller ve kelime çiftlerini
    /// kullanıcı sözlüğüne öğretir. Dönen: kayıt bulundu mu?
    /// </summary>
    public static bool Correct(string id, string correctedText)
    {
        if (string.IsNullOrWhiteSpace(correctedText))
            return false;

        lock (_lock)
        {
            var entry = _entries.FirstOrDefault(e => e.Id == id);
            if (entry == null)
                return false;

            var before = entry.CorrectedText ?? entry.Text;
            entry.CorrectedText = correctedText.Trim();
            LearnCorrections(before, entry.CorrectedText);
            SaveLocked();
        }
        Changed?.Invoke();
        return true;
    }

    /// <summary>Tek bir kaydı siler (öğrenilen sözlük korunur).</summary>
    public static bool Remove(string id)
    {
        var removed = false;
        lock (_lock)
        {
            removed = _entries.RemoveAll(e => e.Id == id) > 0;
            if (removed)
                SaveLocked();
        }
        if (removed)
            Changed?.Invoke(); // lock dışında — aboneler GetAll gibi kilitli yöntemleri güvenle çağırabilir
        return removed;
    }

    /// <summary>Geçmişi tamamen temizler (öğrenilen sözlük korunur).</summary>
    public static void Clear()
    {
        lock (_lock)
        {
            _entries = new List<TranscriptionEntry>();
            SaveLocked();
        }
        Changed?.Invoke();
    }

    /// <summary>
    /// Eski ve yeni metni karşılaştırıp güvenli kelime çiftlerini öğrenir:
    /// yalnızca aynı sayıda kelime varsa (yapısal düzenleme değil, kelime
    /// değişimi) ve farklılaşan tokenlar sözlüğün engel listesinde değilse.
    /// </summary>
    private static void LearnCorrections(string before, string after)
    {
        var a = Tokenize(before);
        var b = Tokenize(after);
        if (a.Count != b.Count || a.Count == 0)
            return; // yapısal değişiklik — tahmin etmek riskli, öğrenme

        for (var i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
                TurkishVocabulary.AddUserCorrection(a[i], b[i]);
        }
    }

    private static List<string> Tokenize(string s)
        => s.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim(',', '.', ';', ':', '!', '?', '"', '\'', '(', ')', '-'))
            .Where(t => t.Any(char.IsLetter))
            .ToList();

    private static void SaveLocked()
    {
        try
        {
            File.WriteAllText(HistoryPath, JsonSerializer.Serialize(_entries));
        }
        catch
        {
            // geçmiş asla uygulamayı kırmaz
        }
    }
}
