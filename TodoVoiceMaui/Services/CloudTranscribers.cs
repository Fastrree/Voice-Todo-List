using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace TodoVoiceMaui.Services;

/// <summary>
/// Bulut STT sağlayıcıları — hepsi standart REST (NuGet SDK'sı yok, hafif).
/// Ortak desen: WAV'i multipart/raw olarak gönder, JSON'dan metni çek.
/// Türkçe sözlüğümüzün önyüklemesi (`InitialPrompt`) prompt alanlarına yazılır.
/// </summary>
public static class CloudTranscribers
{
    public const string ApiKeyPreferencePrefix = "stt_apikey_";
    public const string RegionPreferencePrefix = "stt_region_";
    private const string EncryptedPrefix = "enc:";

    private static string CredentialTarget(string providerId) => $"TodoVoiceMaui/{providerId}";

    /// <summary>
    /// Anahtarı çözer. Depo önceliği:
    ///   1) Windows Credential Manager (Vault) — birincil, OS tarafından şifreli
    ///   2) Preferences (eski sürümler): DPAPI "enc:" veya düz metin → bulunursa
    ///      Vault'a GÖÇ edilir ve Preferences'tan silinir (tek yönlü, güvenli).
    /// </summary>
    public static string GetStoredApiKey(string providerId)
    {
        if (string.IsNullOrEmpty(providerId))
            return string.Empty;

        // 1) Birincil: Windows Credential Manager
        var vault = WindowsCredentialStore.Read(CredentialTarget(providerId));
        if (!string.IsNullOrEmpty(vault))
            return vault;

        // 2) Eski Preferences kayıtları → Vault'a göç et
        var raw = Preferences.Default.Get(ApiKeyPreferencePrefix + providerId, string.Empty);
        if (string.IsNullOrEmpty(raw))
            return string.Empty;

        var legacy = raw.StartsWith(EncryptedPrefix, StringComparison.Ordinal)
            ? SecureKeyStore.Unprotect(raw.Substring(EncryptedPrefix.Length))
            : raw; // legacy düz metin

        if (string.IsNullOrEmpty(legacy))
        {
            Preferences.Default.Remove(ApiKeyPreferencePrefix + providerId);
            return string.Empty;
        }

        // Göç başarılıysa Preferences'tan temizle (anahtar yalnız Vault'ta kalır)
        if (WindowsCredentialStore.Save(CredentialTarget(providerId), legacy))
            Preferences.Default.Remove(ApiKeyPreferencePrefix + providerId);

        return legacy;
    }

    /// <summary>
    /// Anahtarı Windows Vault'a yazar (OS şifreli). Vault yazılamazsa DPAPI-in-
    /// Preferences fallback'i kullanılır (nadir) — anahtar asla kaybolmaz.
    /// Boş anahtar = kaydı siler.
    /// </summary>
    public static void SaveApiKey(string providerId, string key)
    {
        var trimmed = (key ?? string.Empty).Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            DeleteApiKey(providerId);
            return;
        }

        if (WindowsCredentialStore.Save(CredentialTarget(providerId), trimmed))
        {
            // Başarılı → eski Preferences kaydını temizle (göç tamamlandı)
            Preferences.Default.Remove(ApiKeyPreferencePrefix + providerId);
            return;
        }

        // Fallback: DPAPI şifreli Preferences (Vault yoksa/erişilemezse)
        var encrypted = SecureKeyStore.Protect(trimmed);
        // DİKKAT: Protect null dönerse ÖNEKSİZ düz metin sakla — "enc:" önekli düz metin
        // okumada Unprotect'e gider, base64 parse hatasıyla anahtar boşalır (bug).
        var stored = encrypted != null ? EncryptedPrefix + encrypted : trimmed;
        Preferences.Default.Set(ApiKeyPreferencePrefix + providerId, stored);
    }

    /// <summary>Anahtar kaydını her iki depodan da siler.</summary>
    public static void DeleteApiKey(string providerId)
    {
        if (string.IsNullOrEmpty(providerId))
            return;
        WindowsCredentialStore.Delete(CredentialTarget(providerId));
        Preferences.Default.Remove(ApiKeyPreferencePrefix + providerId);
    }

    /// <summary>Bölge bilgisi (Azure vb. — gizli değil, Preferences'ta tutulur).</summary>
    public static string GetStoredRegion(string providerId) =>
        Preferences.Default.Get(RegionPreferencePrefix + providerId, string.Empty);

    /// <summary>Konsol çıktısı için metni kısaltır (çok uzunsa).</summary>
    internal static string TrimText(string? text) =>
        text is null ? "(metin yok)" : text.Length > 90 ? text.Substring(0, 90) + "…" : text;

    public static void SaveRegion(string providerId, string region)
    {
        var trimmed = (region ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(trimmed))
            Preferences.Default.Remove(RegionPreferencePrefix + providerId);
        else
            Preferences.Default.Set(RegionPreferencePrefix + providerId, trimmed);
    }
}

/// <summary>OpenAI-compatible /audio/transcriptions uç noktası (OpenAI + Groq + Fireworks...).</summary>
public sealed class OpenAiCompatibleTranscriber : ISpeechTranscriber
{
    private readonly string _providerId;
    private readonly string _baseUrl;   // örn. https://api.openai.com/v1
    private readonly string _model;     // örn. gpt-4o-mini-transcribe

    public OpenAiCompatibleTranscriber(string providerId, string baseUrl, string model)
    {
        _providerId = providerId;
        _baseUrl = baseUrl.TrimEnd('/');
        _model = model;
    }

    public string ProviderId => _providerId;
    public bool RequiresApiKey => true;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(CloudTranscribers.GetStoredApiKey(_providerId));

    public async Task<string?> TranscribeAsync(string wavPath)
    {
        var key = CloudTranscribers.GetStoredApiKey(_providerId);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        SttTestLog.Write($"→ {_providerId} transkripsiyon: {_model} ({_baseUrl})");
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Bearer {key}");

        using var form = new MultipartFormDataContent();
        await using var fs = File.OpenRead(wavPath);
        var fileContent = new StreamContent(fs);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(fileContent, "file", Path.GetFileName(wavPath));
        form.Add(new StringContent(_model), "model");
        form.Add(new StringContent("tr"), "language");
        form.Add(new StringContent(TurkishVocabulary.InitialPrompt), "prompt");

        using var response = await client.PostAsync($"{_baseUrl}/audio/transcriptions", form);
        SttTestLog.Write($"← HTTP {(int)response.StatusCode}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var text = doc.RootElement.TryGetProperty("text", out var t) ? t.GetString() : null;
        SttTestLog.Write($"✓ Metin: {CloudTranscribers.TrimText(text)}");
        return text;
    }

    /// <summary>
    /// Bağlantı testi: HTTP 2xx dönmesi = anahtar geçerli demektir. Sessizlik için
    /// API boş metin dönebilir — boş metin başarısızlık SAYILMAZ (null = anahtar
    /// yok, exception = anahtar hatalı).
    /// </summary>
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            SttTestLog.Write($"Test başlatıldı ({_providerId}) — test WAV: 0,2 sn sessizlik");
            var text = await TranscribeAsync(TestWavPath());
            return text != null; // "" (2xx + boş metin) da geçerli anahtar demektir
        }
        catch (Exception ex)
        {
            SttTestLog.Write($"✗ Test hatası: {ex.Message}");
            return false;
        }
    }

    /// <summary>Boş bir WAV üretir (bağlantı testi için 0.2 sn sessizlik).</summary>
    internal static string TestWavPath()
    {
        var path = Path.Combine(Path.GetTempPath(), "todovoice_test.wav");
        var rate = 16000;
        var pcm = new short[rate / 5]; // 0.2 sn
        var dataSize = pcm.Length * 2;
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + dataSize);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16);
        w.Write((short)1); w.Write((short)1); w.Write(rate); w.Write(rate * 2);
        w.Write((short)2); w.Write((short)16);
        w.Write(Encoding.ASCII.GetBytes("data")); w.Write(dataSize);
        foreach (var s in pcm) w.Write(s);
        w.Flush();
        File.WriteAllBytes(path, ms.ToArray());
        return path;
    }
}

/// <summary>Deepgram Nova-3 — basit REST: Authorization: Token, raw WAV gövde.</summary>
public sealed class DeepgramTranscriber : ISpeechTranscriber
{
    public string ProviderId => "deepgram";
    public bool RequiresApiKey => true;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(CloudTranscribers.GetStoredApiKey(ProviderId));

    public async Task<string?> TranscribeAsync(string wavPath)
    {
        var key = CloudTranscribers.GetStoredApiKey(ProviderId);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", $"Token {key}");

        SttTestLog.Write("→ Deepgram listen (nova-3, tr, smart_format)");
        var url = "https://api.deepgram.com/v1/listen?model=nova-3&language=tr&smart_format=true&punctuate=true";
        var bytes = await File.ReadAllBytesAsync(wavPath);
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        using var response = await client.PostAsync(url, content);
        SttTestLog.Write($"← HTTP {(int)response.StatusCode}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        // results.channels[0].alternatives[0].transcript
        if (doc.RootElement.TryGetProperty("results", out var results) &&
            results.TryGetProperty("channels", out var channels) &&
            channels.GetArrayLength() > 0)
        {
            var alt = channels[0].GetProperty("alternatives");
            if (alt.GetArrayLength() > 0)
            {
                var tr = alt[0].TryGetProperty("transcript", out var t) ? t.GetString() : null;
                SttTestLog.Write($"✓ Metin: {CloudTranscribers.TrimText(tr)}");
                return tr;
            }
        }
        SttTestLog.Write("⚠ Sonuç yok");
        return null;
    }

    public Task<bool> TestConnectionAsync() => Task.Run(async () =>
    {
        try
        {
            var text = await TranscribeAsync(OpenAiCompatibleTranscriber.TestWavPath());
            return text != null; // 2xx + boş metin = geçerli anahtar
        }
        catch
        {
            return false;
        }
    });
}

/// <summary>
/// AssemblyAI Universal-2 — 3 adımlı REST: yükle → transkript iste → sonucu bekle.
/// Ses önce /v2/upload ile geçici URL'ye yüklenir, sonra transkribe edilir.
/// </summary>
public sealed class AssemblyAiTranscriber : ISpeechTranscriber
{
    private const string BaseUrl = "https://api.assemblyai.com/v2";

    public string ProviderId => "assemblyai";
    public bool RequiresApiKey => true;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(CloudTranscribers.GetStoredApiKey(ProviderId));

    public async Task<string?> TranscribeAsync(string wavPath)
    {
        var key = CloudTranscribers.GetStoredApiKey(ProviderId);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", key);

        // 1) Yükle
        SttTestLog.Write($"→ AssemblyAI /v2/upload ({new FileInfo(wavPath).Length / 1024} KB)");
        var bytes = await File.ReadAllBytesAsync(wavPath);
        using (var uploadContent = new ByteArrayContent(bytes))
        {
            uploadContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
            using var uploadResp = await client.PostAsync($"{BaseUrl}/upload", uploadContent);
            SttTestLog.Write($"← HTTP {(int)uploadResp.StatusCode}");
            uploadResp.EnsureSuccessStatusCode();
            var uploadJson = await uploadResp.Content.ReadAsStringAsync();
            var audioUrl = JsonDocument.Parse(uploadJson).RootElement.GetProperty("upload_url").GetString();

            // 2) Transkript iste
            var payload = JsonSerializer.Serialize(new
            {
                audio_url = audioUrl,
                language_code = "tr",
                punctuate = true,
                format_text = true
            });
            using var reqContent = new StringContent(payload, Encoding.UTF8, "application/json");
            using var reqResp = await client.PostAsync($"{BaseUrl}/transcript", reqContent);
            SttTestLog.Write($"← HTTP {(int)reqResp.StatusCode}");
            reqResp.EnsureSuccessStatusCode();
            var reqJson = await reqResp.Content.ReadAsStringAsync();
            var id = JsonDocument.Parse(reqJson).RootElement.GetProperty("id").GetString()!;
            SttTestLog.Write($"→ Transkript isteği gönderildi (id={id}) — sonuç bekleniyor…");

            // 3) Sonucu bekle (poll)
            var deadline = DateTime.UtcNow.AddMinutes(2);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(1000);
                using var pollResp = await client.GetAsync($"{BaseUrl}/transcript/{id}");
                pollResp.EnsureSuccessStatusCode();
                var pollJson = await pollResp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(pollJson);
                var status = doc.RootElement.GetProperty("status").GetString();
                SttTestLog.Write($"← durum: {status}");
                if (status == "completed")
                {
                    var text = doc.RootElement.TryGetProperty("text", out var t) ? t.GetString() : null;
                    SttTestLog.Write($"✓ Metin: {CloudTranscribers.TrimText(text)}");
                    return text;
                }
                if (status == "error")
                {
                    SttTestLog.Write("✗ Transkript hatası");
                    return null;
                }
            }
            SttTestLog.Write("✗ Zaman aşımı (2 dk)");
            return null;
        }
    }

    public Task<bool> TestConnectionAsync() => Task.Run(async () =>
    {
        try
        {
            var text = await TranscribeAsync(OpenAiCompatibleTranscriber.TestWavPath());
            return text != null; // 2xx + boş metin = geçerli anahtar
        }
        catch
        {
            return false;
        }
    });
}

/// <summary>ElevenLabs Scribe v2 — multipart POST, xi-api-key başlığı.</summary>
public sealed class ElevenLabsTranscriber : ISpeechTranscriber
{
    public string ProviderId => "elevenlabs";
    public bool RequiresApiKey => true;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(CloudTranscribers.GetStoredApiKey(ProviderId));

    public async Task<string?> TranscribeAsync(string wavPath)
    {
        var key = CloudTranscribers.GetStoredApiKey(ProviderId);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        SttTestLog.Write("→ ElevenLabs Scribe v2 (tr, keyterm yönlendirmeli)");
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
        client.DefaultRequestHeaders.TryAddWithoutValidation("xi-api-key", key);

        using var form = new MultipartFormDataContent();
        await using var fs = File.OpenRead(wavPath);
        var fileContent = new StreamContent(fs);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(fileContent, "file", Path.GetFileName(wavPath));
        form.Add(new StringContent("scribe_v2"), "model_id");
        form.Add(new StringContent("tr"), "language_code");
        // Scribe v2 keyterm yönlendirmesi JSON DİZİ bekler — sözlüğümüzü dizi olarak gönder
        var keyTerms = JsonSerializer.Serialize(
            TurkishVocabulary.InitialPrompt.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray());
        form.Add(new StringContent(keyTerms), "custom_words");

        using var response = await client.PostAsync("https://api.elevenlabs.io/v1/speech-to-text", form);
        SttTestLog.Write($"← HTTP {(int)response.StatusCode}");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var text = doc.RootElement.TryGetProperty("text", out var t) ? t.GetString() : null;
        SttTestLog.Write($"✓ Metin: {CloudTranscribers.TrimText(text)}");
        return text;
    }

    public Task<bool> TestConnectionAsync() => Task.Run(async () =>
    {
        try
        {
            var text = await TranscribeAsync(OpenAiCompatibleTranscriber.TestWavPath());
            return !string.IsNullOrWhiteSpace(text);
        }
        catch
        {
            return false;
        }
    });
}

/// <summary>
/// Google Cloud Speech-to-Text v1 — senkron `speech:recognize` (kısa ses ≤1 dk).
/// API anahtarı sorgu parametresi (`?key=`), gövde JSON içinde base64 LINEAR16 PCM
/// (16kHz mono). Model: `chirp_2` (GA); bölge/eski hesaplarda desteklenmezse
/// `latest_short` ile bir kez geri dener — ikisi de tr-TR destekler.
/// </summary>
public sealed class GoogleTranscriber : ISpeechTranscriber
{
    private const string Endpoint = "https://speech.googleapis.com/v1/speech:recognize";

    public string ProviderId => "google";
    public bool RequiresApiKey => true;
    public bool IsConfigured => !string.IsNullOrWhiteSpace(CloudTranscribers.GetStoredApiKey(ProviderId));

    public async Task<string?> TranscribeAsync(string wavPath)
    {
        var key = CloudTranscribers.GetStoredApiKey(ProviderId);
        if (string.IsNullOrWhiteSpace(key))
            return null;

        var pcm = WavAudioReader.ReadMono16kHzPcm(wavPath);
        if (pcm == null || pcm.Length == 0)
            return null;

        // chirp_2 önce; İSTEK HATASI alınırsa (eski proje/bölge, 400/401) latest_short
        // ile bir kez dene. 2xx döndüyse boş metin bile başarıdır — ikinci çağrı yapılmaz
        // (sessiz ses için gereksiz ikinci fatura olmaz).
        SttTestLog.Write("→ Google speech:recognize (chirp_2, tr-TR)");
        var (ok1, text1) = await TryRecognizeAsync(key, pcm, "chirp_2");
        if (ok1)
            return text1;

        SttTestLog.Write("→ chirp_2 isteği hatalı — latest_short deneniyor");
        var (_, text2) = await TryRecognizeAsync(key, pcm, "latest_short");
        return text2;
    }

    private static async Task<(bool Ok, string? Text)> TryRecognizeAsync(string key, byte[] pcm, string model)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            var payload = JsonSerializer.Serialize(new
            {
                config = new
                {
                    encoding = "LINEAR16",
                    sampleRateHertz = 16000,
                    languageCode = "tr-TR",
                    model
                },
                audio = new { content = Convert.ToBase64String(pcm) }
            });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var response = await client.PostAsync($"{Endpoint}?key={Uri.EscapeDataString(key)}", content);
            SttTestLog.Write($"← HTTP {(int)response.StatusCode}");
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            // results[0].alternatives[0].transcript
            if (doc.RootElement.TryGetProperty("results", out var results) && results.GetArrayLength() > 0)
            {
                var alt = results[0].GetProperty("alternatives");
                if (alt.GetArrayLength() > 0)
                {
                    var text = alt[0].TryGetProperty("transcript", out var t) ? t.GetString() : null;
                    SttTestLog.Write($"✓ Metin: {CloudTranscribers.TrimText(text)}");
                    return (true, text);
                }
            }
            return (true, null); // 2xx + sonuç yok → yine de geçerli istek
        }
        catch (HttpRequestException ex)
        {
            // DİKKAT: URL'de `?key=` olduğundan ex.Message'ı loglamayız — yalnızca HTTP kodu
            SttTestLog.Write($"✗ {model} isteği hatalı (HTTP {(int?)ex.StatusCode ?? 0})");
            return (false, null); // 400/401/403/ağ → fallback model veya çevrimdışı
        }
        catch (Exception ex)
        {
            SttTestLog.Write($"✗ {model} isteği hatalı: {ex.Message}");
            return (false, null);
        }
    }

    public Task<bool> TestConnectionAsync() => Task.Run(async () =>
    {
        try
        {
            var text = await TranscribeAsync(OpenAiCompatibleTranscriber.TestWavPath());
            return text != null; // 2xx = geçerli anahtar (boş metin de geçerli)
        }
        catch
        {
            return false;
        }
    });
}

/// <summary>
/// Azure AI Speech — senkron kısa ses uç noktası (tek cümle ≤15 sn ideal).
/// Bölge + `Ocp-Apim-Subscription-Key` gerekir. Gövde: 16kHz mono WAV.
/// Yanıt: `DisplayText` (format=detailed).
/// </summary>
public sealed class AzureTranscriber : ISpeechTranscriber
{
    public string ProviderId => "azure";
    public bool RequiresApiKey => true;
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(CloudTranscribers.GetStoredApiKey(ProviderId)) &&
        !string.IsNullOrWhiteSpace(CloudTranscribers.GetStoredRegion(ProviderId));

    public async Task<string?> TranscribeAsync(string wavPath)
    {
        var key = CloudTranscribers.GetStoredApiKey(ProviderId);
        var region = CloudTranscribers.GetStoredRegion(ProviderId);
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(region))
            return null;

        SttTestLog.Write($"→ Azure {region} (conversation, tr-TR, detailed)");
        var pcm = WavAudioReader.ReadMono16kHzPcm(wavPath);
        if (pcm == null || pcm.Length == 0)
            return null;
        var wav = WavAudioReader.BuildWav16kHz(pcm);

        var (ok, json) = await TryRequestAsync(key, region, wav);
        if (!ok || json == null)
            return null;

        using var doc = JsonDocument.Parse(json);
        // RecognitionStatus + DisplayText
        if (doc.RootElement.TryGetProperty("RecognitionStatus", out var status) &&
            status.GetString() == "Success" &&
            doc.RootElement.TryGetProperty("DisplayText", out var text))
        {
            var result = text.GetString();
            SttTestLog.Write($"✓ Metin: {CloudTranscribers.TrimText(result)}");
            return result;
        }
        SttTestLog.Write("⚠ RecognitionStatus ≠ Success (NoMatch — sessiz ses olabilir)");
        return null; // NoMatch (sessiz) → null → çevrimdışı fallback
    }

    private static async Task<(bool Ok, string? Json)> TryRequestAsync(string key, string region, byte[] wav)
    {
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            client.DefaultRequestHeaders.TryAddWithoutValidation("Ocp-Apim-Subscription-Key", key);

            var url = $"https://{region}.stt.speech.microsoft.com/speech/recognition/conversation/cognitiveservices/v1" +
                      $"?language=tr-TR&format=detailed&profanity=raw";
            using var content = new ByteArrayContent(wav);
            content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav; codecs=audio/pcm; samplerate=16000");

            using var response = await client.PostAsync(url, content);
            SttTestLog.Write($"← HTTP {(int)response.StatusCode}");
            response.EnsureSuccessStatusCode();
            return (true, await response.Content.ReadAsStringAsync());
        }
        catch (HttpRequestException ex)
        {
            SttTestLog.Write($"✗ Azure isteği hatalı (HTTP {(int?)ex.StatusCode ?? 0}) — bölge/anahtar kontrol edin");
            return (false, null);
        }
        catch (Exception ex)
        {
            SttTestLog.Write($"✗ Azure isteği hatalı: {ex.Message}");
            return (false, null);
        }
    }

    public Task<bool> TestConnectionAsync() => Task.Run(async () =>
    {
        var key = CloudTranscribers.GetStoredApiKey(ProviderId);
        var region = CloudTranscribers.GetStoredRegion(ProviderId);
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(region))
            return false;
        var pcm = WavAudioReader.ReadMono16kHzPcm(OpenAiCompatibleTranscriber.TestWavPath());
        if (pcm == null || pcm.Length == 0)
            return false;
        // 2xx = geçerli anahtar + bölge (RecognitionStatus fark etmez — sessiz ses NoMatch dönebilir)
        var (ok, _) = await TryRequestAsync(key, region, WavAudioReader.BuildWav16kHz(pcm));
        return ok;
    });
}
