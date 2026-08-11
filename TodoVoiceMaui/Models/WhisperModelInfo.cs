namespace TodoVoiceMaui.Models;

/// <summary>
/// Ayarlar → "Ses Tanıma" bölümünde kullanıcının seçebileceği çevrimdışı Whisper modeli.
/// Katmanlar kullanıcının isteğine göre büyütüldü:
///   Minimum ≥100MB · Orta ≥300MB · Yüksek ≥750MB · Maximum 2-5GB (Türkçe odaklı).
/// Her model için dürüst boyut / hız / doğruluk / RAM / Türkçe WER bilgisi gösterilir.
/// </summary>
public class WhisperModelInfo
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string TierLabel { get; init; }
    public required string FileName { get; init; }
    public required string SizeLabel { get; init; }
    public required string SpeedLabel { get; init; }
    public required string AccuracyLabel { get; init; }
    public required string Description { get; init; }

    // ---- Zengin bilgi alanları (Model Yönetimi modalı + detay kartı) ----

    /// <summary>Tahminî bellek (RAM) kullanımı.</summary>
    public required string RamLabel { get; init; }

    /// <summary>Tahminî Türkçe kelime hata oranı (WER) — whisper.cpp ölçümleri/topluluk verisi.</summary>
    public required string WerLabel { get; init; }

    /// <summary>Dil kapsamı.</summary>
    public required string LanguagesLabel { get; init; }

    /// <summary>Kuantizasyon (q5_1/q5_0/q8_0/fp16).</summary>
    public required string QuantizationLabel { get; init; }

    /// <summary>Hız (gerçek zaman çarpanı, modern CPU — tahminî).</summary>
    public required string SpeedFactorLabel { get; init; }

    /// <summary>Kimin için önerilir?</summary>
    public required string RecommendationText { get; init; }

    public bool IsRecommended { get; init; }

    public string DownloadUrl => $"https://huggingface.co/ggerganov/whisper.cpp/resolve/main/{FileName}";

    public int SizeMb { get; init; }

    /// <summary>Büyük model (1GB+) — indirme öncesi ayrı onay gösterilir.</summary>
    public bool IsLargeModel => SizeMb >= 1024;

    public override string ToString() => DisplayName;
}

/// <summary>
/// Çevrimdışı Whisper model kataloğu — 4 katman.
/// Boyutlar HuggingFace'ten doğrulanmış gerçek GGML dosya boyutlarıdır:
///   Minimum 190MB · Orta 539MB · Yüksek 874MB · Maximum 3,1GB.
/// (5GB'lık gerçek bir whisper.cpp GGML dosyası yok; Maximum en iyi Türkçe
/// kapsamı olan large-v3 = 3,1GB — 680.000+ saatlik çok dilli veriyle eğitildi.)
/// RAM/WER/speed değerleri whisper.cpp tipik masaüstü ölçümlerine dayalı TAHMİNİDİR.
/// </summary>
public static class WhisperModelCatalog
{
    /// <summary>Ağırlıklı kullanım sırası: hafif → ağır. Varsayılan "small-q5_1" (Minimum).</summary>
    public static readonly IReadOnlyList<WhisperModelInfo> All = new[]
    {
        new WhisperModelInfo
        {
            Id = "small-q5_1",
            DisplayName = "Minimum",
            TierLabel = "Minimum",
            FileName = "ggml-small-q5_1.bin",
            SizeMb = 190,
            SizeLabel = "190 MB",
            SpeedLabel = "Hızlı",
            AccuracyLabel = "İyi",
            RamLabel = "~1 GB RAM",
            WerLabel = "Türkçe WER ≈ %12-15",
            LanguagesLabel = "99+ dil",
            QuantizationLabel = "q5_1 (5-bit)",
            SpeedFactorLabel = "4-6× gerçek zaman",
            RecommendationText = "Günlük kısa komutlar için dengeli seçim — çoğu kullanıcıya yeterli.",
            IsRecommended = true,
            Description = "En hafif katman (≥100MB şartını karşılar). Hızlı ve az bellek ister; " +
                          "günlük kısa komutlar için yeterli, Türkçe'de base'e göre %10-20 daha iyi."
        },
        new WhisperModelInfo
        {
            Id = "medium-q5_0",
            DisplayName = "Orta",
            TierLabel = "Orta",
            FileName = "ggml-medium-q5_0.bin",
            SizeMb = 539,
            SizeLabel = "539 MB",
            SpeedLabel = "Dengeli",
            AccuracyLabel = "Yüksek",
            RamLabel = "~2 GB RAM",
            WerLabel = "Türkçe WER ≈ %8-10",
            LanguagesLabel = "99+ dil",
            QuantizationLabel = "q5_0 (5-bit)",
            SpeedFactorLabel = "2-3× gerçek zaman",
            RecommendationText = "Gürültülü ortam veya uzun komutlar için önerilir.",
            Description = "Orta katman (≥300MB). Gerçek medium mimarisi — Türkçe'yi (diyalekt, " +
                          "gürültülü mikrofon) belirgin şekilde daha iyi anlar."
        },
        new WhisperModelInfo
        {
            Id = "large-v3-turbo-q8_0",
            DisplayName = "Yüksek",
            TierLabel = "Yüksek",
            FileName = "ggml-large-v3-turbo-q8_0.bin",
            SizeMb = 874,
            SizeLabel = "874 MB",
            SpeedLabel = "Hızlı*",
            AccuracyLabel = "Çok yüksek",
            RamLabel = "~3 GB RAM",
            WerLabel = "Türkçe WER ≈ %6-8",
            LanguagesLabel = "99+ dil",
            QuantizationLabel = "q8_0 (8-bit)",
            SpeedFactorLabel = "4-6× gerçek zaman (turbo)",
            RecommendationText = "En iyi hız/doğruluk dengesi — turbo mimarisi, büyük model kalitesi.",
            Description = "Yüksek katman (≥750MB). large-v3 mimarisi, yüksek hassasiyet (q8_0) " +
                          "— en iyi doğruluk, *turbo sayesinde hâlâ hızlı."
        },
        new WhisperModelInfo
        {
            Id = "large-v3",
            DisplayName = "Maximum",
            TierLabel = "Maximum",
            FileName = "ggml-large-v3.bin",
            SizeMb = 3095,
            SizeLabel = "3,1 GB",
            SpeedLabel = "Yavaş",
            AccuracyLabel = "En yüksek",
            RamLabel = "~6 GB RAM",
            WerLabel = "Türkçe WER ≈ %4-6",
            LanguagesLabel = "99+ dil (680k saat eğitim)",
            QuantizationLabel = "fp16 (tam hassasiyet)",
            SpeedFactorLabel = "0,5-1× gerçek zaman",
            RecommendationText = "Maksimum doğruluk — uzun/karmaşık konuşmalar için. Güçlü CPU ve 16GB+ RAM önerilir.",
            Description = "Maximum katman (2-5GB aralığında mümkün olan en iyi). 680.000+ saatlik " +
                          "çok dilli veriyle eğitilmiş en büyük Whisper — Türkçe dahil tüm dillerde " +
                          "en düşük hata oranı. En çok bellek ve zaman ister."
        }
    };

    public static WhisperModelInfo GetById(string id) =>
        All.FirstOrDefault(m => m.Id == id)
        ?? All.FirstOrDefault(m => m.Id == DefaultId)
        ?? All[0];

    public static readonly string DefaultId = "small-q5_1";
}
