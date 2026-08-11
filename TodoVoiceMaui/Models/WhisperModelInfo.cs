namespace TodoVoiceMaui.Models;

/// <summary>
/// Ayarlar → "Ses Tanıma" bölümünde kullanıcının seçebileceği Whisper modeli.
/// Her model için dürüst boyut / hız / doğruluk bilgisi kullanıcıya gösterilir:
/// en küçük model en hızlı ama "seni tam anlamayabilir"; 1GB+ modeller çok daha
/// iyi Türkçe anlar (büyük modeller 680.000+ saatlik çok dilli veriyle eğitilmiştir).
/// </summary>
public class WhisperModelInfo
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string FileName { get; init; }
    public required string SizeLabel { get; init; }
    public required string SpeedLabel { get; init; }
    public required string AccuracyLabel { get; init; }
    public required string Description { get; init; }
    public bool IsRecommended { get; init; }

    public string DownloadUrl => $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/{FileName}";

    /// <summary>Büyük model (1GB+) — ayrı uyarı rozeti gösterilir.</summary>
    public bool IsLargeModel => SizeMb >= 1024;

    public int SizeMb { get; init; }

    public override string ToString() => DisplayName;
}

/// <summary>Kullanıcının seçebileceği tüm modeller (Ayarlar ekranında bu liste gösterilir).</summary>
public static class WhisperModelCatalog
{
    /// <summary>
    /// Ağırlıklı kullanım sırası: hafif → ağır. Varsayılan "small-q5_1" (dengeli).
    /// GGML quantize dosyaların yaklaşık disk boyutları gerçektir.
    /// </summary>
    public static readonly IReadOnlyList<WhisperModelInfo> All = new[]
    {
        new WhisperModelInfo
        {
            Id = "tiny",
            DisplayName = "Tiny",
            FileName = "ggml-tiny.bin",
            SizeMb = 75,
            SizeLabel = "75 MB",
            SpeedLabel = "Çok hızlı",
            AccuracyLabel = "Düşük",
            Description = "En hafif model. Anında çalışır ama seni tam anlamayabilir — " +
                          "kısa, basit komutlar için yeterli."
        },
        new WhisperModelInfo
        {
            Id = "base",
            DisplayName = "Base",
            FileName = "ggml-base.bin",
            SizeMb = 142,
            SizeLabel = "142 MB",
            SpeedLabel = "Hızlı",
            AccuracyLabel = "Orta-altı",
            Description = "Hafif ve hızlı. Günlük kullanımda fena değil, ama Türkçe'de " +
                          "özel isimleri ve eklemeli kelimeleri sık karıştırabilir."
        },
        new WhisperModelInfo
        {
            Id = "small-q5_1",
            DisplayName = "Small",
            FileName = "ggml-small-q5_1.bin",
            SizeMb = 190,
            SizeLabel = "190 MB",
            SpeedLabel = "Dengeli",
            AccuracyLabel = "İyi",
            IsRecommended = true,
            Description = "Önerilen varsayılan. Hız ve doğruluk dengesi en iyi olan model; " +
                          "base'e göre Türkçe'de %10-20 daha az hata yapar."
        },
        new WhisperModelInfo
        {
            Id = "medium-q5_0",
            DisplayName = "Medium",
            FileName = "ggml-medium-q5_0.bin",
            SizeMb = 1500,
            SizeLabel = "1,5 GB",
            SpeedLabel = "Yavaş",
            AccuracyLabel = "Yüksek",
            Description = "İlk 1GB+ model. Türkçe'yi (diyalekt dahil) belirgin şekilde daha " +
                          "iyi anlar; düşük kaliteli mikrofonlarda bile güvenilir. Daha fazla " +
                          "bellek ve işlemci gücü ister."
        },
        new WhisperModelInfo
        {
            Id = "large-v3-turbo-q5_0",
            DisplayName = "Large v3 Turbo",
            FileName = "ggml-large-v3-turbo-q5_0.bin",
            SizeMb = 1600,
            SizeLabel = "1,6 GB",
            SpeedLabel = "Hızlı*",
            AccuracyLabel = "Çok yüksek",
            Description = "En iyi doğruluk seviyesi, ama Turbo sayesinde medium'dan daha hızlı " +
                          "çalışır. *Turbo: large kalitesinde, small hızında. 1GB+ bölgesinde en " +
                          "akıllı seçim."
        },
        new WhisperModelInfo
        {
            Id = "large-v3-q5_0",
            DisplayName = "Large v3",
            FileName = "ggml-large-v3-q5_0.bin",
            SizeMb = 2900,
            SizeLabel = "2,9 GB",
            SpeedLabel = "Yavaş",
            AccuracyLabel = "En yüksek",
            Description = "En büyük ve en doğru model (680.000+ saat veriyle eğitildi). " +
                          "Karmaşık cümleler, aksanlar ve isimler için en iyisi — ama en çok " +
                          "bellek ve zaman ister."
        }
    };

    public static WhisperModelInfo GetById(string id) =>
        All.FirstOrDefault(m => m.Id == id)
        ?? All.FirstOrDefault(m => m.Id == DefaultId)
        ?? All[0]; // bilinmeyen id → varsayılan (Small)

    public static readonly string DefaultId = "small-q5_1";
}
