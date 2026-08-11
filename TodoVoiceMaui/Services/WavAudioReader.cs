using System.Text;

namespace TodoVoiceMaui.Services;

/// <summary>
/// WAV dosyasını Whisper'ın beklediği 16kHz mono float32 diziye çevirir.
/// - RIFF/WAVE başlığını çözer (PCM16 / PCM8 / IEEE float32)
/// - Çok kanallı kayıtları mono'ya karıştırır
/// - 44.1k/48k gibi örnekleme hızlarını 16kHz'e yeniden örnekler (doğrusal interpolasyon)
/// </summary>
public static class WavAudioReader
{
    public const int TargetSampleRate = 16000;

    public static float[]? ReadMono16kHz(string wavPath)
    {
        if (!File.Exists(wavPath))
            return null;

        using var stream = File.OpenRead(wavPath);
        using var reader = new BinaryReader(stream);

        // RIFF başlığı
        if (new string(reader.ReadChars(4)) != "RIFF")
            return null;
        _ = reader.ReadInt32();          // dosya boyutu (önemsiz)
        if (new string(reader.ReadChars(4)) != "WAVE")
            return null;

        int audioFormat = 1;
        int channels = 1;
        int sampleRate = 44100;
        int bitsPerSample = 16;
        byte[]? data = null;

        while (stream.Position < stream.Length - 8)
        {
            var chunkId = new string(reader.ReadChars(4));
            var chunkSize = reader.ReadInt32();
            if (chunkSize < 0 || chunkSize > stream.Length - stream.Position)
                return null;

            switch (chunkId)
            {
                case "fmt ":
                {
                    var fmtStart = stream.Position;
                    audioFormat = reader.ReadInt16();        // 1 = PCM, 3 = IEEE float
                    channels = reader.ReadInt16();
                    sampleRate = reader.ReadInt32();
                    _ = reader.ReadInt32();                  // byte rate
                    _ = reader.ReadInt16();                  // block align
                    bitsPerSample = reader.ReadInt16();

                    // WAVE_FORMAT_EXTENSIBLE (0xFFFE): gerçek format, fmt chunk sonundaki
                    // SubFormat GUID'inin ilk baytlarında (1 = PCM, 3 = IEEE float).
                    // Windows MediaCapture bu formatı üretebilir.
                    if (audioFormat == 0xFFFE && chunkSize >= 40)
                    {
                        var rest = reader.ReadBytes(chunkSize - 16);
                        if (rest.Length >= 24)
                        {
                            audioFormat = BitConverter.ToInt16(rest, 8); // GUID ilk 2 baytı
                        }
                    }

                    stream.Position = fmtStart + chunkSize;
                    break;
                }
                case "data":
                    data = reader.ReadBytes(chunkSize);
                    break;
                default:
                    stream.Position += chunkSize;
                    break;
            }
        }

        if (data == null || data.Length == 0 || sampleRate <= 0 || channels <= 0)
            return null;

        var mono = DecodeToMonoFloat(data, audioFormat, channels, bitsPerSample);
        if (mono == null || mono.Length == 0)
            return null;

        return Resample(mono, sampleRate, TargetSampleRate);
    }

    private static float[]? DecodeToMonoFloat(byte[] data, int audioFormat, int channels, int bitsPerSample)
    {
        if (audioFormat == 3 && bitsPerSample == 32)
        {
            // IEEE float32 (kanal sayısına göre ortala)
            var frameBytes = channels * 4;
            var frames = data.Length / frameBytes;
            var mono = new float[frames];
            for (var i = 0; i < frames; i++)
            {
                var sum = 0f;
                for (var c = 0; c < channels; c++)
                {
                    var offset = i * frameBytes + c * 4;
                    sum += BitConverter.ToSingle(data, offset);
                }
                mono[i] = sum / channels;
            }
            return mono;
        }

        if (audioFormat == 1 && bitsPerSample == 16)
        {
            var frameBytes = channels * 2;
            var frames = data.Length / frameBytes;
            var mono = new float[frames];
            for (var i = 0; i < frames; i++)
            {
                var sum = 0f;
                for (var c = 0; c < channels; c++)
                {
                    var offset = i * frameBytes + c * 2;
                    sum += BitConverter.ToInt16(data, offset) / 32768f;
                }
                mono[i] = sum / channels;
            }
            return mono;
        }

        if (audioFormat == 1 && bitsPerSample == 8)
        {
            var frameBytes = channels;
            var frames = data.Length / frameBytes;
            var mono = new float[frames];
            for (var i = 0; i < frames; i++)
            {
                var sum = 0f;
                for (var c = 0; c < channels; c++)
                {
                    var offset = i * frameBytes + c;
                    sum += (data[offset] - 128) / 128f;
                }
                mono[i] = sum / channels;
            }
            return mono;
        }

        return null; // desteklenmeyen format
    }

    private static float[] Resample(float[] source, int sourceRate, int targetRate)
    {
        if (sourceRate == targetRate)
            return source;

        var ratio = (double)sourceRate / targetRate;
        var outLength = (int)(source.Length / ratio);
        var result = new float[outLength];
        for (var i = 0; i < outLength; i++)
        {
            var pos = i * ratio;
            var i0 = (int)pos;
            var i1 = Math.Min(i0 + 1, source.Length - 1);
            var frac = (float)(pos - i0);
            result[i] = source[i0] * (1f - frac) + source[i1] * frac;
        }
        return result;
    }

    /// <summary>
    /// WAV dosyasının ses süresini saniye cinsinden döndürür (yalnız başlık okunur —
    /// hızlıdır, tüm veriyi decode etmez). Kullanım istatistikleri için.
    /// </summary>
    public static double GetDurationSeconds(string wavPath)
    {
        try
        {
            using var stream = File.OpenRead(wavPath);
            if (stream.Length < 44)
                return 0;
            using var reader = new BinaryReader(stream);
            if (new string(reader.ReadChars(4)) != "RIFF")
                return 0;

            // Standart 44B başlık: channels=22, sampleRate=24, byteRate=28, dataSize=40
            reader.BaseStream.Position = 28; // byteRate
            var byteRate = reader.ReadInt32();
            reader.BaseStream.Position = 40; // data boyutu
            var dataSize = reader.ReadInt32();
            if (byteRate <= 0 || dataSize <= 0)
                return 0;
            return dataSize / (double)byteRate;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>float32 mono diziyi 16-bit signed little-endian PCM byte dizisine çevirir.</summary>
    public static byte[] ToPcm16(float[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        for (var i = 0; i < samples.Length; i++)
        {
            var s = Math.Clamp(samples[i], -1f, 1f);
            var v = (short)(s * 32767f);
            bytes[i * 2] = (byte)(v & 0xFF);
            bytes[i * 2 + 1] = (byte)((v >> 8) & 0xFF);
        }
        return bytes;
    }

    /// <summary>16kHz mono PCM'yi tam bir WAV dosyasına (RIFF başlıklı) sarar — Azure vb. için.</summary>
    public static byte[] BuildWav16kHz(byte[] pcm)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write(Encoding.ASCII.GetBytes("RIFF")); w.Write(36 + pcm.Length);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt ")); w.Write(16);
        w.Write((short)1); w.Write((short)1); w.Write(TargetSampleRate); w.Write(TargetSampleRate * 2);
        w.Write((short)2); w.Write((short)16);
        w.Write(Encoding.ASCII.GetBytes("data")); w.Write(pcm.Length);
        w.Write(pcm);
        w.Flush();
        return ms.ToArray();
    }

    /// <summary>WAV dosyasını 16kHz mono PCM16 byte dizisine çevirir (bulut API'leri için).</summary>
    public static byte[]? ReadMono16kHzPcm(string wavPath)
    {
        var samples = ReadMono16kHz(wavPath);
        return samples == null ? null : ToPcm16(samples);
    }
}
