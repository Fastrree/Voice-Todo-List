using System.Text;

namespace TodoVoiceMaui.Services;

/// <summary>
/// Türkçe özel isim sözlüğü + transkripsiyon sonrası otomatik düzeltme katmanı.
///
/// Whisper küçük modelde bile özel isimleri (şirket/adam isimleri) sık yanlış
/// yazar ("goolgle", "turk hava yollari", "elon mask"). Bu sınıf iki şekilde
/// devreye girer:
///   1. `InitialPrompt` → whisper decode'una kelime önyükleme (WithPrompt).
///   2. `Correct()` → çıktı metninde sözlükteki isimleri tanır ve kanonik
///      yazımla düzeltir. Eşleştirme diyakritiksiz normalize (ç→c, ğ→g, ı→i,
///      ş→s, ü→u, ö→o) + Levenshtein bulanık eşleştirme ile yapılır; böylece
///      "goolgle"→Google, "is bankasi"→İş Bankası gibi ASR hataları yakalanır.
///
/// Kısa/çift anlamlı sözcükler (BİM, Şok, Getir, Meta...) yalnızca TAM eşleşir
/// (Fuzzy=false) — Türkçe günlük sözcükleri yanlış düzeltmemek için.
/// </summary>
public static class TurkishVocabulary
{
    // ---- Sözlük ----

    // Çok kelimeli ifadeler: tam (normalize) eşleşme aranır, kanonik yazımla değiştirilir.
    private static readonly string[] PhraseEntries =
    {
        "Türk Hava Yolları", "Türk Telekom", "İş Bankası", "Yapı Kredi",
        "Mustafa Kemal Atatürk", "Recep Tayyip Erdoğan", "Kemal Kılıçdaroğlu",
        "Meral Akşener", "Devlet Bahçeli", "Ekrem İmamoğlu", "Mansur Yavaş",
        "Elon Musk", "Steve Jobs", "Bill Gates", "Jeff Bezos", "Mark Zuckerberg",
        "Sundar Pichai", "Satya Nadella", "Donald Trump", "Joe Biden",
        "Vladimir Putin", "Cristiano Ronaldo", "Lionel Messi", "Kylian Mbappé",
        "LeBron James", "Taylor Swift", "Aziz Sancar", "Orhan Pamuk", "Elif Şafak",
        "Kıvanç Tatlıtuğ", "Aras Bulut İynemli", "Cem Yılmaz", "Yılmaz Erdoğan",
        "Acun Ilıcalı", "Selçuk Bayraktar", "Haluk Bayraktar", "LC Waikiki",
        "Ford Otosan", "Sezen Aksu", "Süleyman Demirel", "Tansu Çiller",
        "Abdullah Gül", "Özgür Özel", "Warren Buffett", "Xi Jinping",
        "Coca-Cola"
    };

    // Tek kelimeler: Fuzzy=true → Levenshtein bulanık eşleşme; false → yalnız tam eşleşme.
    private static readonly (string Word, bool Fuzzy)[] SingleEntries =
    {
        // ---- Teknoloji / küresel şirketler ----
        ("Google", true), ("Microsoft", true), ("Apple", true), ("Amazon", true),
        ("Netflix", true), ("Spotify", true), ("Samsung", true), ("Huawei", true),
        ("Xiaomi", true), ("Tesla", true), ("Toyota", true), ("Mercedes", true),
        ("Volkswagen", true), ("Intel", true), ("Nvidia", true), ("Oracle", true),
        ("Cisco", true), ("Dell", true), ("Lenovo", true), ("Asus", true),
        ("Sony", true), ("Philips", true), ("Siemens", true), ("Bosch", true),
        ("Nestlé", true), ("Coca-Cola", true), ("Pepsi", true), ("Starbucks", true),
        ("Airbnb", true), ("Uber", true), ("LinkedIn", true), ("Instagram", true),
        ("WhatsApp", true), ("YouTube", true), ("Telegram", true), ("TikTok", true),
        ("PayPal", true), ("Shopify", true), ("Salesforce", true), ("Adobe", true),
        ("Zoom", true), ("OpenAI", true), ("ChatGPT", true), ("DeepSeek", true),
        // ---- Türk şirketleri / markalar ----
        ("Togg", true), ("Turkcell", true), ("Trendyol", true), ("Hepsiburada", true),
        ("Yemeksepeti", true), ("Sahibinden", true), ("Arçelik", true), ("Vestel", true),
        ("Beko", true), ("Akbank", true), ("Garanti", true), ("Ziraat", true),
        ("Halkbank", true), ("Vakıfbank", true), ("Papara", true), ("Pegasus", true),
        ("ASELSAN", true), ("TUSAŞ", true), ("ROKETSAN", true), ("HAVELSAN", true),
        ("Otokar", true), ("Tofaş", true), ("Tüpraş", true), ("Baykar", true),
        ("Migros", true), ("DeFacto", true), ("Koton", true),
        // ---- Kişiler ----
        // ("Getir" bilinçli olarak YOK: "süt getir" gibi en yaygın fiil çakışır;
        //  marka yalnızca InitialPrompt'ta kalır.)
        ("Atatürk", true), ("İmamoğlu", true), ("Yavaş", true), ("Sancar", true),
        ("Musk", true), ("Elon", true), ("Trump", true), ("Putin", true),
        ("Ronaldo", true), ("Messi", true), ("Neymar", true), ("Shakira", true),
        ("Beyoncé", true), ("Tarkan", true), ("Sıla", true), ("Hadise", true),
        ("Mbappé", true), ("Nadella", true), ("Pichai", true), ("Buffett", true),
        // ---- Yalnız tam eşleşme (günlük Türkçe sözcük çakışması riski) ----
        ("BİM", false), ("A101", false), ("Şok", false), ("TAV", false),
        ("N11", false), ("Pınar", false), ("Meta", false),
        ("Vodafone", false), ("BMW", false), ("IBM", false), ("LG", false)
    };

    // Türkçe yaygın sözcük/şehir karalistesi (katlanmış hâlde): bu tokenlar için
    // sözlük eşleşmesi TAMAMEN atlanır — bulanık eşleştirme "samsun"→Samsung,
    // "garanti"→Garanti gibi yanlış pozitifler üretmesin. (Normalize edilmiş biçim.)
    private static readonly HashSet<string> MatchBlocklist = new(StringComparer.Ordinal)
    {
        "samsun", "garanti", "sila", "ziraat", "yapi", "meta", "mavi", "eti"
    };

    /// <summary>
    /// Whisper decode'una kelime önyükleme (WithPrompt) — en bilinen isimler.
    /// <224 token sınırının altında tutulur; tam sözlük Correct() ile uygulanır.
    /// </summary>
    public const string InitialPrompt =
        "Google, Microsoft, Apple, Amazon, Netflix, Spotify, Samsung, Tesla, Toyota, " +
        "Mercedes, BMW, Intel, Nvidia, Sony, Siemens, Bosch, Coca-Cola, Starbucks, Uber, " +
        "Instagram, WhatsApp, YouTube, TikTok, PayPal, Zoom, OpenAI, ChatGPT, Togg, " +
        "Turkcell, Vodafone, Türk Telekom, Trendyol, Getir, Hepsiburada, Yemeksepeti, " +
        "Sahibinden, Türk Hava Yolları, BİM, Migros, A101, Şok, Arçelik, Vestel, Beko, " +
        "Akbank, İş Bankası, Garanti, Yapı Kredi, Ziraat, Halkbank, Vakıfbank, Papara, " +
        "Pegasus, ASELSAN, Baykar, TUSAŞ, ROKETSAN, Elon Musk, Steve Jobs, Bill Gates, " +
        "Mark Zuckerberg, Atatürk, Ekrem İmamoğlu, Mansur Yavaş, Selçuk Bayraktar, " +
        "Aziz Sancar, Orhan Pamuk, Cem Yılmaz, Tarkan, Ronaldo, Messi, Taylor Swift";

    private static readonly string[] PhraseFolded = PhraseEntries.Select(Fold).ToArray();
    private static readonly (string Folded, string Canonical, bool Fuzzy)[] Singles =
        SingleEntries.Select(e => (Fold(e.Word), e.Word, e.Fuzzy)).ToArray();

    /// <summary>
    /// Transkripsiyon metnini düzeltir: sözlükteki isimleri kanonik yazımla değiştirir.
    /// </summary>
    public static string Correct(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text ?? string.Empty;

        var tokens = text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        var removed = new bool[tokens.Count];

        // 1) Çok kelimeli ifadeler (önce — daha uzun eşleşmeler kazanır)
        for (var p = 0; p < PhraseEntries.Length; p++)
        {
            var phrase = PhraseEntries[p];
            var key = PhraseFolded[p];
            // Kelime sayısı kanonik metinden değil, katlanmış anahtardan alınır
            // ("Coca-Cola" → "coca cola" 2 kelime, ama kanonikte tire var).
            var wordCount = key.Count(c => c == ' ') + 1;

            for (var i = 0; i + wordCount <= tokens.Count; i++)
            {
                if (removed[i])
                    continue;

                var window = string.Join(" ", tokens.Skip(i).Take(wordCount).Select(Fold));
                if (window == key)
                {
                    tokens[i] = phrase;
                    for (var j = i + 1; j < i + wordCount; j++)
                        removed[j] = true;
                    i += wordCount - 1;
                }
            }
        }

        // 2) Tek kelimeler — tam + bulanık (Levenshtein). Değiştirilen token
        //    çıktıda kalır (removed yalnızca ifade devamı tokenları için kullanılır).
        for (var i = 0; i < tokens.Count; i++)
        {
            if (removed[i])
                continue;

            var token = tokens[i];
            var folded = Fold(token);

            // Yaygın Türkçe sözcükler/şehirler: sözlük eşleşmesini atla (yanlış pozitif koruması)
            if (MatchBlocklist.Contains(folded))
                continue;

            foreach (var entry in Singles)
            {
                if (entry.Folded.Length > folded.Length + 2)
                    continue;

                var match = folded == entry.Folded ||
                            (entry.Fuzzy && Levenshtein(folded, entry.Folded) <= DistanceLimit(entry.Folded.Length));

                if (match)
                {
                    tokens[i] = entry.Canonical;
                    break;
                }
            }
        }

        return string.Join(" ", tokens.Where((_, i) => !removed[i]));
    }

    /// <summary>Bulanık eşleşme limiti: kısa sözcüklerde sıkı, uzunlarda toleranslı.</summary>
    private static int DistanceLimit(int keyLength) => keyLength switch
    {
        <= 3 => 0,   // 3 harften kısa sözcüklerde bulanık yok
        <= 5 => 1,
        _ => 2
    };

    /// <summary>
    /// Eşleştirme için normalize: küçük harf + Türkçe diyakritikleri düz ASCII'ye indir
    /// (ç→c, ğ→g, ı→i, ö→o, ş→s, ü→u). Boşluklar korunur (ifade eşleşmesi için),
    /// noktalama sıyrılır (kesme işareti ve tire korunur: İş Bankası, Coca-Cola).
    /// </summary>
    private static string Fold(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch == 'İ')
            {
                // char.ToLowerInvariant('İ') her runtime'da 'i' dönmez → elle
                sb.Append('i');
                continue;
            }

            if (char.IsWhiteSpace(ch))
            {
                sb.Append(' ');
                continue;
            }

            if (char.IsPunctuation(ch))
            {
                if (ch == '\'')
                    sb.Append(ch);
                else if (ch == '-')
                    sb.Append(' '); // "Coca-Cola" ↔ "coca cola" aynı anahtara insin
                continue;
            }

            var lower = char.ToLowerInvariant(ch);
            switch (lower)
            {
                case 'ç': lower = 'c'; break;
                case 'ğ': lower = 'g'; break;
                case 'ı': lower = 'i'; break;
                case 'ö': lower = 'o'; break;
                case 'ş': lower = 's'; break;
                case 'ü': lower = 'u'; break;
                default:
                    if (lower > 127)
                    {
                        // Aksanlı harfleri düz ASCII'ye indir (é→e, â→a ...):
                        // FormD ayrıştır + birleşen işaretleri at.
                        foreach (var c2 in lower.ToString().Normalize(NormalizationForm.FormD))
                        {
                            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c2)
                                != System.Globalization.UnicodeCategory.NonSpacingMark)
                                sb.Append(char.ToLowerInvariant(c2));
                        }
                        continue;
                    }
                    break;
            }
            sb.Append(lower);
        }

        // Çoklu boşlukları tekilleştir + uçlardan temizle (ifade anahtarlarıyla uyum)
        return string.Join(" ", sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>Klasik iki satırlı Levenshtein mesafesi.</summary>
    private static int Levenshtein(string a, string b)
    {
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++)
            prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            (prev, curr) = (curr, prev);
        }

        return prev[b.Length];
    }
}
