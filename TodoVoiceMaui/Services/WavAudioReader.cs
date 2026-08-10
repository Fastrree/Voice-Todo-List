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
}
