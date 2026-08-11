using System.Runtime.InteropServices;
using System.Text;

namespace TodoVoiceMaui.Services;

/// <summary>
/// Hafif, anlamlı arayüz ses efektleri (transition-framework.md §6 / §11).
///
/// Ses dosyası kullanılmaz — tonlar çalışma zamanında sentezlenir (WAV 22.05kHz mono 16-bit)
/// ve WinMM PlaySound (P/Invoke) ile asenkron oynatılır. Unpackaged WinUI 3 uygulamasında
/// paket kimliği gerektirmez, asset gerekmez. Amplitüdler bilinçli olarak düşük tutulur
/// ("anlamlı ve kaliteli", asla rahatsız edici değil).
///
/// Ayarlar → "Ses efektleri" ile kapatılabilir (Preferences: enable_sounds).
/// </summary>
public static class SoundEffectService
{
    public enum SoundKind
    {
        Click,      // genel arayüz tıklaması (çok hafif)
        Success,    // görev oluştu / kaydedildi (iki tonlu zil)
        Error,      // hata (alçak çift ton)
        Delete,     // silme (yumuşak iniş)
        MicStart,   // mikrofon açıldı (yükselen nefes)
        MicStop,    // mikrofon kapandı (inen nefes)
        Reminder    // hatırlatıcı (yumuşak davet tonu — alarm değil)
    }

    private const int SampleRate = 22050;
    private const uint SndAsync = 0x0001;
    private const uint SndMemory = 0x0004;
    private const uint SndNoDefault = 0x0002;

    private static readonly Dictionary<SoundKind, byte[]> Sounds = new();

    /// <summary>Ses efektleri açık mı? (Ayarlar'dan değiştirilir, Preferences'ta saklanır.)</summary>
    public static bool Enabled { get; set; } = true;

    static SoundEffectService()
    {
        try
        {
            Sounds[SoundKind.Click] = Synth(
                Tone(880, 0.045, 0.14),
                Tone(1320, 0.030, 0.09));

            Sounds[SoundKind.Success] = Synth(
                Tone(659.25, 0.075, 0.16),
                Tone(987.77, 0.110, 0.16));

            Sounds[SoundKind.Error] = Synth(
                Tone(196, 0.090, 0.15),
                Tone(147, 0.130, 0.15));

            Sounds[SoundKind.Delete] = Synth(
                Tone(330, 0.050, 0.13),
                Tone(247, 0.085, 0.13));

            Sounds[SoundKind.MicStart] = Synth(
                Sweep(440, 660, 0.090, 0.14));

            Sounds[SoundKind.MicStop] = Synth(
                Sweep(660, 440, 0.090, 0.12));

            Sounds[SoundKind.Reminder] = Synth(
                Tone(659.25, 0.090, 0.13),
                Tone(987.77, 0.070, 0.13),
                Tone(1318.51, 0.140, 0.12));
        }
        catch
        {
            // Ses üretimi başarısız olursa sessizce devam et (uygulama asla kırılmaz)
        }
    }

    public static void Play(SoundKind kind)
    {
        if (!Enabled)
            return;

        try
        {
            if (Sounds.TryGetValue(kind, out var wav) && wav != null && wav.Length > 0)
            {
                // SND_ASYNC + SND_MEMORY: veri çalma bitene kadar bellekte kalmalı —
                // Sounds sözlüğü uygulama ömrü boyunca yaşadığı için güvende.
                PlaySound(wav, IntPtr.Zero, SndAsync | SndMemory | SndNoDefault);
            }
        }
        catch
        {
            // best-effort — ses asla akışı kırmaz
        }
    }

    // ---- Sentez ----

    private readonly record struct ToneSpec(double FreqStart, double FreqEnd, double Duration, double Amp);

    private static ToneSpec Tone(double freq, double duration, double amp)
        => new(freq, freq, duration, amp);

    private static ToneSpec Sweep(double from, double to, double duration, double amp)
        => new(from, to, duration, amp);

    private static byte[] Synth(params ToneSpec[] tones)
    {
        var totalSamples = (int)tones.Sum(t => t.Duration * SampleRate);
        var pcm = new short[totalSamples];
        var offset = 0;

        foreach (var t in tones)
        {
            var count = (int)(t.Duration * SampleRate);
            var phase = 0.0;
            var attackSamples = 0.005 * SampleRate; // 5ms attack

            for (var i = 0; i < count; i++)
            {
                var pos = (double)i / count;
                var currentFreq = t.FreqStart + (t.FreqEnd - t.FreqStart) * pos;

                phase += 2.0 * Math.PI * currentFreq / SampleRate;

                // Zarf: 5ms attack + üstel release (tıklama/tıkırtı önler)
                var attack = Math.Min(1.0, i / attackSamples);
                var release = Math.Pow(1.0 - pos, 2.2);
                var sample = Math.Sin(phase) * t.Amp * attack * release;

                pcm[offset + i] = (short)(sample * short.MaxValue);
            }

            offset += count;
        }

        return BuildWav(pcm);
    }

    private static byte[] BuildWav(short[] pcm)
    {
        var dataSize = pcm.Length * 2;
        using var ms = new MemoryStream(44 + dataSize);
        using var w = new BinaryWriter(ms);

        w.Write(Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataSize);
        w.Write(Encoding.ASCII.GetBytes("WAVE"));
        w.Write(Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);
        w.Write((short)1);                  // PCM
        w.Write((short)1);                  // mono
        w.Write(SampleRate);
        w.Write(SampleRate * 2);            // byte rate
        w.Write((short)2);                  // block align
        w.Write((short)16);                 // bits per sample
        w.Write(Encoding.ASCII.GetBytes("data"));
        w.Write(dataSize);
        foreach (var s in pcm)
            w.Write(s);

        w.Flush();
        return ms.ToArray();
    }

    [DllImport("winmm.dll")]
    private static extern bool PlaySound(byte[] pcm, IntPtr hmod, uint flags);
}
