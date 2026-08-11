namespace TodoVoiceMaui.Services;

/// <summary>
/// Ses tanıma sağlayıcı sözleşmesi (ADR-012'deki değerli seam).
/// Bir sağlayıcı = çevrimdışı Whisper ya da bir bulut STT API'si.
/// `SpeechToTextService` seçili sağlayıcıya yönlendirir; bulut başarısız olursa
/// çevrimdışı Whisper'a düşer (fallback). Tüm sağlayıcılar WAV yolu alır ve
/// düz metin döner; Türkçe özel isim düzeltmesi (`TurkishVocabulary.Correct`)
/// sağlayıcıdan bağımsız olarak üst katmanda uygulanır.
/// </summary>
public interface ISpeechTranscriber
{
    /// <summary>Katalogdaki sağlayıcı kimliği (örn. "openai").</summary>
    string ProviderId { get; }

    /// <summary>API anahtarı gerekli mi (bulut sağlayıcılar)?</summary>
    bool RequiresApiKey { get; }

    /// <summary>Anahtar ayarlanmış ve kullanıma hazır mı?</summary>
    bool IsConfigured { get; }

    /// <summary>WAV dosyasını transkript eder. Boş/başarısızsa null.</summary>
    Task<string?> TranscribeAsync(string wavPath);

    /// <summary>Bağlantıyı/anahtarı doğrular (Ayarlar → Bağlantıyı Test Et).</summary>
    Task<bool> TestConnectionAsync();
}
