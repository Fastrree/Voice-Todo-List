using System;
using System.Text.RegularExpressions;

namespace TodoVoiceMaui.Core.Application.Voice;

public sealed class RuleBasedVoiceCommandParser : IVoiceCommandParser
{
    private static readonly string[] CompleteKeywords =
    {
        "tamamla", "tamamlandı", "bitir", "yaptım", "tamamlanmış", "tamam"
    };

    private static readonly string[] RemindKeywords =
    {
        "hatırlat", "hatırlatma", "reminder", "alarm kur"
    };

    public VoiceCommand Parse(TranscriptionResult transcription)
    {
        var text = transcription.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
            return VoiceCommand.Unknown(string.Empty);

        var lower = text.ToLowerInvariant();

        // Hatırlatıcı komutu ÖNCE kontrol edilir — "sunumu bitirmeyi hatırlat" veya
        // "...hatırlat tamam" gibi ifadeler Complete'teki "bitir/tamam" kelimelerine
        // takılıp hedefsiz Complete komutuna dönüşmesin. "10 dakika sonra süt almayı
        // hatırlat" → { "Süt al", ReminderAt = şimdi+10dk }
        if (ContainsAny(lower, RemindKeywords))
        {
            var reminderAt = ExtractReminderAt(lower);
            var title = StripReminderFraming(text);
            if (string.IsNullOrWhiteSpace(title))
                title = reminderAt.HasValue ? "Hatırlatıcı" : text;

            return new VoiceCommand(VoiceIntent.Create, Capitalize(title), reminderAt: reminderAt);
        }

        if (ContainsAny(lower, CompleteKeywords))
            return new VoiceCommand(VoiceIntent.Complete, text);

        return new VoiceCommand(VoiceIntent.Create, text);
    }

    /// <summary>
    /// Metinden hatırlatma zamanını çözer. Desteklenen kalıplar:
    ///   "N dakika/saat sonra|içinde" · "saat HH[:MM]" · "yarın [saat HH[:MM]]" ·
    ///   "sabah|öğlen|akşam" (geçtiyse yarına kayar) · "bugün" · "yarın"
    /// Bulunamazsa null (hatırlatma zamanı yok — yalnızca görev oluşturulur).
    /// </summary>
    private static DateTime? ExtractReminderAt(string lower)
    {
        var now = DateTime.Now;
        var today = DateTime.Today;

        // "yarın 9'da" / "bugün 14'te" — gün + saat (Türkçe ekli form)
        var dayHour = Regex.Match(lower, @"(yarın|bugün)\s*(\d{1,2})\s*'?(?:da|de|ta|te)");
        if (dayHour.Success)
        {
            var h = int.Parse(dayHour.Groups[2].Value);
            if (h is >= 0 and <= 23)
            {
                var baseDay = dayHour.Groups[1].Value == "yarın" ? today.AddDays(1) : today;
                var candidate = baseDay.AddHours(h);
                return dayHour.Groups[1].Value == "yarın"
                    ? candidate
                    : (candidate <= now ? candidate.AddDays(1) : candidate);
            }
        }

        // "saat 14:30" — saat dilimi (geçtiyse yarın)
        var hm = Regex.Match(lower, @"saat\s*(\d{1,2})(?:[.:](\d{2}))?");
        if (hm.Success)
        {
            var h = int.Parse(hm.Groups[1].Value);
            var m = hm.Groups[2].Success ? int.Parse(hm.Groups[2].Value) : 0;
            if (h is >= 0 and <= 23 && m is >= 0 and <= 59)
            {
                var baseDay = lower.Contains("yarın", StringComparison.Ordinal) ? today.AddDays(1) : today;
                var candidate = baseDay.AddHours(h).AddMinutes(m);
                return lower.Contains("yarın", StringComparison.Ordinal)
                    ? candidate
                    : (candidate <= now ? candidate.AddDays(1) : candidate);
            }
        }

        // "10 dakika sonra" / "2 saat içinde"
        var dur = Regex.Match(lower, @"(\d+)\s*(dakika|dk|saat|sa)\s*(sonra|içinde|icinde)");
        if (dur.Success)
        {
            var n = int.Parse(dur.Groups[1].Value);
            var unit = dur.Groups[2].Value;
            return unit.StartsWith("sa", StringComparison.Ordinal)
                ? now.AddHours(n)
                : now.AddMinutes(n);
        }

        if (lower.Contains("yarın", StringComparison.Ordinal))
            return today.AddDays(1).AddHours(9);

        if (lower.Contains("öğlen", StringComparison.Ordinal) || lower.Contains("öğle", StringComparison.Ordinal))
        {
            var noon = today.AddHours(12);
            return noon <= now ? noon.AddDays(1) : noon;
        }

        if (lower.Contains("akşam", StringComparison.Ordinal))
        {
            var eve = today.AddHours(18);
            return eve <= now ? eve.AddDays(1) : eve;
        }

        if (lower.Contains("sabah", StringComparison.Ordinal))
        {
            var mor = today.AddHours(8);
            return mor <= now ? mor.AddDays(1) : mor;
        }

        if (lower.Contains("bugün", StringComparison.Ordinal))
        {
            var nine = today.AddHours(9);
            return nine <= now ? now.AddMinutes(30) : nine;
        }

        return null;
    }

    /// <summary>
    /// Hatırlatıcı çerçevesini ve zaman ifadelerini başlıktan sıyırır:
    /// "10 dakika sonra süt almayı hatırlat" → "Süt al". Zaman kalıbı yoksa
    /// sadece çerçeve çıkarılır; "süt almayı" gibi fiilleşmiş ifadelerin
    /// "-mayı/-meyi" eki de kırpılır (doğal komut formu için).
    /// </summary>
    private static string StripReminderFraming(string text)
    {
        var t = text;

        t = Regex.Replace(t, @"\d+\s*(dakika|dk|saat|sa)\s*(sonra|içinde|icinde)", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"saat\s*\d{1,2}(?:[.:]\d{2})?", " ", RegexOptions.IgnoreCase);

        foreach (var word in new[] { "yarın", "bugün", "öğlen", "öğle", "akşam", "sabah" })
            t = Regex.Replace(t, $@"\b{word}\b", " ", RegexOptions.IgnoreCase);

        // "9'da" / "14'te" gibi Türkçe ekli saat tokenlarını sıyır (düz sayıları değil)
        t = Regex.Replace(t, @"\d{1,2}'?(?:da|de|ta|te)\b", " ", RegexOptions.IgnoreCase);

        t = Regex.Replace(t, @"\breminder\b", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\balarm\s*kur\b", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\bbeni\s+hatırlat\b", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\bbana\s+hatırlat\b", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\bhatırlatma\s*kur\b", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\bhatırlat\b", " ", RegexOptions.IgnoreCase);
        t = Regex.Replace(t, @"\bkur\b", " ", RegexOptions.IgnoreCase);

        t = string.Join(" ", t.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim();

        // "süt almayı" → "süt al" (Türkçe fiilleşmiş ad tamlaması)
        if (t.EndsWith("mayı", StringComparison.OrdinalIgnoreCase))
            t = t[..^4];
        else if (t.EndsWith("meyi", StringComparison.OrdinalIgnoreCase))
            t = t[..^4];

        return t.Trim();
    }

    private static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s[1..];
    }

    private static bool ContainsAny(string value, string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (value.Contains(keyword, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
