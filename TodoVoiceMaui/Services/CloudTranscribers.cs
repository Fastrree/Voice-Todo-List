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

    public static string GetStoredApiKey(string providerId) =>
        Preferences.Default.Get(ApiKeyPreferencePrefix + providerId, string.Empty);

    public static void SaveApiKey(string providerId, string key) =>
        Preferences.Default.Set(ApiKeyPreferencePrefix + providerId, (key ?? string.Empty).Trim());
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
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("text", out var t) ? t.GetString() : null;
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
            var text = await TranscribeAsync(TestWavPath());
            return text != null; // "" (2xx + boş metin) da geçerli anahtar demektir
        }
        catch
        {
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

        var url = "https://api.deepgram.com/v1/listen?model=nova-3&language=tr&smart_format=true&punctuate=true";
        var bytes = await File.ReadAllBytesAsync(wavPath);
        using var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        using var response = await client.PostAsync(url, content);
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
                return alt[0].TryGetProperty("transcript", out var tr) ? tr.GetString() : null;
        }
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
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("text", out var t) ? t.GetString() : null;
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
