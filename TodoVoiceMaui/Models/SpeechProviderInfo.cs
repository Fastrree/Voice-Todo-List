namespace TodoVoiceMaui.Models;

/// <summary>
/// Ses tanıma "kaynağı" (transkripsiyon sağlayıcısı). Katalog bilinçli olarak
/// GENİŞ tutuldu: çevrimdışı Whisper + piyasadaki tüm önemli bulut STT'leri.
/// `IsImplemented=false` olanlar katalogda görünür (kapsam belli olsun) ama
/// arayüzde "Yakında" olarak işaretlenir — mimari aynı olduğundan her biri
/// tek bir sınıf yazılarak aktifleştirilebilir.
/// </summary>
public class SpeechProviderInfo
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string ModelLabel { get; init; }
    public required string CostLabel { get; init; }
    public required string Description { get; init; }
    public bool RequiresApiKey { get; init; }
    public bool IsImplemented { get; init; } = true;

    /// <summary>Ek bölge bilgisi gerekir mi (örn. Azure AI Speech).</summary>
    public bool RequiresRegion { get; init; }

    /// <summary>Bölge alanının varsayılan değeri / placeholder'ı.</summary>
    public string? DefaultRegion { get; init; }

    public override string ToString() => DisplayName;
}

/// <summary>Tüm sağlayıcılar. Fiyatlar 2026 araştırmasına dayanır (saat başına, USD).</summary>
public static class SpeechProviderCatalog
{
    public static readonly IReadOnlyList<SpeechProviderInfo> All = new[]
    {
        new SpeechProviderInfo
        {
            Id = "offline",
            DisplayName = "Çevrimdışı Whisper",
            ModelLabel = "Yerel model (Minimum→Maximum)",
            CostLabel = "Ücretsiz",
            Description = "Hiçbir anahtar gerekmez, internet gerekmez, sesin cihazdan çıkmaz. " +
                          "4 katmanlı yerel model (190MB → 3,1GB). Varsayılan ve her zaman çalışır."
        },
        new SpeechProviderInfo
        {
            Id = "openai",
            DisplayName = "OpenAI",
            ModelLabel = "gpt-4o-mini-transcribe",
            CostLabel = "~$0.18/saat",
            Description = "En yeni OpenAI transkripsiyon modeli — Türkçe'de çok yüksek doğruluk, " +
                          "İngilizce-Türkçe karışık konuşmayı bile temiz çözer. Anahtar: platform.openai.com"
        },
        new SpeechProviderInfo
        {
            Id = "groq",
            DisplayName = "Groq",
            ModelLabel = "whisper-large-v3-turbo",
            CostLabel = "~$0.04/saat",
            Description = "En ucuz + en hızlı bulut seçeneği (LPU donanımı). Whisper large-v3 " +
                          "kalitesi, saniyeler içinde sonuç. Anahtar: console.groq.com (ücretsiz tier var)"
        },
        new SpeechProviderInfo
        {
            Id = "deepgram",
            DisplayName = "Deepgram",
            ModelLabel = "Nova-3",
            CostLabel = "~$0.46/saat",
            Description = "En düşük gecikmeli STT (<300ms), Türkçe eklemeli yapıda güçlü. " +
                          "Yeni hesaba $200 kredi. Anahtar: deepgram.com"
        },
        new SpeechProviderInfo
        {
            Id = "elevenlabs",
            DisplayName = "ElevenLabs",
            ModelLabel = "Scribe v2",
            CostLabel = "~$0.22/saat",
            Description = "Türkçe'de WER ≤%5 (mükemmel sınıfı). 1000 kelimelik özel kelime " +
                          "yönlendirmesi — marka/isim tanımada çok iyi. Anahtar: elevenlabs.io"
        },
        new SpeechProviderInfo
        {
            Id = "google",
            DisplayName = "Google",
            ModelLabel = "Chirp 2 (V1 API)",
            CostLabel = "~$0.96/saat",
            Description = "Google'ın en yeni ses modeli Chirp 2 — `tr-TR` resmî destekli, gürbüz. " +
                          "Google Cloud API anahtarı (Service Account gerekmez). İlk 60 dk/ay ücretsiz, " +
                          "yeni hesaba $300 kredi."
        },
        new SpeechProviderInfo
        {
            Id = "azure",
            DisplayName = "Azure AI Speech",
            ModelLabel = "Standard (kısa ses)",
            CostLabel = "~$1.00/saat",
            RequiresRegion = true,
            DefaultRegion = "westeurope",
            Description = "Kurumsal, güvenilir. Bölge bilgisi gerekir (örn. westeurope, northeurope, " +
                          "turkiyecentral). Ücretsiz tier: 5 saat/ay. Anahtar: Azure portal → Speech resource."
        },
        new SpeechProviderInfo
        {
            Id = "assemblyai",
            DisplayName = "AssemblyAI",
            ModelLabel = "Universal-2",
            CostLabel = "~$0.21/saat",
            Description = "Güçlü biçimlendirme, noktalama ve büyük harf düzeltmesi. Türkçe eklemeli " +
                          "yapıda başarılı. Yeni hesaba $50 kredi. Anahtar: assemblyai.com"
        },
        new SpeechProviderInfo
        {
            Id = "fireworks",
            DisplayName = "Fireworks AI",
            ModelLabel = "whisper-large-v3",
            CostLabel = "~$0.09/saat",
            Description = "OpenAI uyumlu API üzerinde optimize GPU'larla çok hızlı large-v3. " +
                          "Ucuz ve hızlı. Anahtar: fireworks.ai"
        },
        new SpeechProviderInfo
        {
            Id = "cloudflare",
            DisplayName = "Cloudflare",
            ModelLabel = "whisper-large-v3-turbo",
            CostLabel = "~$0.03/saat",
            IsImplemented = false,
            Description = "Yakında: Piyasanın en ucuzu — uç (edge) sunucularda Whisper turbo."
        },
        new SpeechProviderInfo
        {
            Id = "soniox",
            DisplayName = "Soniox",
            ModelLabel = "TR uzman modeli",
            CostLabel = "~$0.11/saat",
            IsImplemented = false,
            Description = "Yakında: Türkçe'ye özel uzmanlaşmış düşük maliyetli STT."
        },
        new SpeechProviderInfo
        {
            Id = "sestek",
            DisplayName = "Sestek",
            ModelLabel = "Yerli Türkçe ASR",
            CostLabel = "Kurumsal",
            IsImplemented = false,
            Description = "Yakında: Türkiye merkezli yerli ASR sağlayıcısı — Türkçe'ye tam uzmanlaşma."
        }
    };

    public static SpeechProviderInfo GetById(string id) =>
        All.FirstOrDefault(p => p.Id == id) ?? All[0];

    public static readonly string DefaultId = "offline";
}
