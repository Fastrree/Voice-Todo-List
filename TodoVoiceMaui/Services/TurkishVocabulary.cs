using System.Text;

namespace TodoVoiceMaui.Services;

/// <summary>
/// Türkçe özel isim sözlüğü + transkripsiyon sonrası otomatik düzeltme katmanı.
///
/// Whisper özel isimleri (şirket/adam isimleri) sık yanlış yazar ("goolgle",
/// "turk hava yollari", "elon mask"). Bu sınıf iki şekilde devreye girer:
///   1. `InitialPrompt` → whisper decode'una kelime önyükleme (WithPrompt).
///   2. `Correct()` → çıktı metninde sözlükteki isimleri tanır ve kanonik
///      yazımla düzeltir. Eşleştirme diyakritiksiz normalize (ç→c, ğ→g, ı→i,
///      ş→s, ü→u, ö→o) + Levenshtein bulanık eşleştirme ile yapılır.
///
/// TÜRKÇE İYELİK EKLERİ: "Google'dan", "Trendyol'a", "Migros'tan" gibi kesme
/// işaretli tokenlar kök + ek olarak ayrılır ve kök sözlükte aranır — böylece
/// "goolgle'dan" → "Google'dan" düzeltmesi de yapılır.
///
/// Kısa/çift anlamlı sözcükler (BİM, Şok, Zara, Nike...) yalnızca TAM eşleşir
/// (Fuzzy=false) — Türkçe günlük sözcükleri (para, sonu, kuma, mavi, garanti...)
/// yanlış düzeltmemek için. `MatchBlocklist` ise sözlükte olmasına rağmen Türkçe
/// yaygın sözcükle birebir çakışanların tamamen atlanmasını sağlar (samsun,
/// garanti, deniz, sila, ziraat, mavi, eti, yapi, meta).
/// </summary>
public static class TurkishVocabulary
{
    // =====================================================================
    // SÖZLÜK — Çok kelimeli ifadeler (tam normalize eşleşme, kanonik yazımla
    // değiştirilir). Türkçe'nin en yaygın özel isimleri Türkçe öncelikli.
    // =====================================================================
    private static readonly string[] PhraseEntries =
    {
        // ---- Havayolu / ulaşım ----
        "Türk Hava Yolları", "Atatürk Havalimanı", "Sabiha Gökçen", "İstanbul Havalimanı",
        "AnadoluJet", "Onur Air",
        // ---- Telekom / teknoloji ----
        "Türk Telekom",
        // ---- Bankalar ----
        "İş Bankası", "Türkiye İş Bankası", "Yapı Kredi", "Garanti BBVA", "QNB Finansbank",
        "Kuveyt Türk", "Türkiye Finans", "Vakıf Katılım", "Ziraat Katılım", "Albaraka Türk",
        "Aktif Bank", "CEPTETEB", "ING Bank", "Ziraat Bankası",
        // ---- Holding / sanayi ----
        "Koç Holding", "Sabancı Holding", "Doğuş Grubu", "Anadolu Grubu", "Zorlu Holding",
        "Türkiye Petrolleri", "Borsa İstanbul", "Ford Otosan",
        // ---- Market / gıda / moda ----
        "LC Waikiki", "Kahve Dünyası", "Simit Sarayı", "Burger King", "Pizza Hut",
        "Taco Bell", "Little Caesars", "Migros Yemek", "Trendyol Yemek", "Onur Market",
        "Çiçek Sepeti", "New Balance", "Under Armour", "Calvin Klein", "Tommy Hilfiger",
        "Coca-Cola", "Cola Turka",
        // ---- Medya / TV ----
        "CNN Türk", "Bloomberg HT", "Kanal D", "Show TV", "Star TV", "Halk TV",
        "A Haber", "A Spor", "Ulusal Kanal", "Gazete Duvar", "Artı Gerçek", "beIN Sports",
        "Kanal 7", "Beyaz TV", "Flash TV", "TRT 1", "TRT Haber", "TRT Spor", "TRT Çocuk",
        // ---- Siyaset ----
        "Mustafa Kemal Atatürk", "Recep Tayyip Erdoğan", "Kemal Kılıçdaroğlu", "Özgür Özel",
        "Meral Akşener", "Devlet Bahçeli", "Ekrem İmamoğlu", "Mansur Yavaş", "Abdullah Gül",
        "Süleyman Demirel", "Tansu Çiller", "Binali Yıldırım", "Ahmet Davutoğlu",
        "Ali Babacan", "Fatih Erbakan", "Temel Karamollaoğlu", "Ümit Özdağ", "Sinan Oğan",
        "İsmet İnönü", "Adnan Menderes", "Turgut Özal", "Necmettin Erbakan",
        "Alparslan Türkeş", "Deniz Baykal", "Muharrem İnce", "Selahattin Demirtaş",
        "AK Parti", "İYİ Parti", "DEM Parti", "Yeniden Refah", "Memleket Partisi",
        "Saadet Partisi", "Gelecek Partisi", "DEVA Partisi",
        // ---- Küresel iş insanları / liderler ----
        "Elon Musk", "Steve Jobs", "Bill Gates", "Jeff Bezos", "Mark Zuckerberg",
        "Sundar Pichai", "Satya Nadella", "Warren Buffett", "Donald Trump", "Joe Biden",
        "Vladimir Putin", "Xi Jinping",
        // ---- Sporcular ----
        "Cristiano Ronaldo", "Lionel Messi", "Kylian Mbappé", "LeBron James",
        "Arda Güler", "Kenan Yıldız", "Hakan Çalhanoğlu", "Çağlar Söyüncü", "Merih Demiral",
        "Ferdi Kadıoğlu", "Orkun Kökçü", "Kerem Aktürkoğlu", "Barış Alper Yılmaz",
        "Altay Bayındır", "Uğurcan Çakır", "Burak Yılmaz", "Emre Belözoğlu", "Arda Turan",
        "Selçuk İnan", "Fatih Terim", "Şenol Güneş", "Okan Buruk", "Jose Mourinho",
        "Vincenzo Montella", "Mircea Lucescu", "Hakan Şükür", "Volkan Demirel",
        "Cengiz Ünder", "Yusuf Yazıcı", "Enes Ünal", "Mert Günok", "İrfan Can Kahveci",
        "Ozan Kabak", "Abdülkadir Ömür",
        // ---- Kulüpler (yurt içi) ----
        "Adana Demirspor", "Gaziantep FK", "Bodrum FK", "Manisa FK", "Fatih Karagümrük",
        "Yeni Malatyaspor", "Çaykur Rizespor", "Batman Petrolspor", "Norm Ender",
        // ---- Kulüpler (yurt dışı) ----
        "Real Madrid", "Manchester United", "Manchester City", "Bayern Munich",
        "Borussia Dortmund", "Atlético Madrid", "Boca Juniors", "River Plate",
        "Aston Villa", "West Ham", "Newcastle United", "Inter Milan", "AC Milan",
        // ---- Sanatçılar ----
        "Taylor Swift", "Aziz Sancar", "Orhan Pamuk", "Elif Şafak", "Kıvanç Tatlıtuğ",
        "Aras Bulut İynemli", "Cem Yılmaz", "Yılmaz Erdoğan", "Acun Ilıcalı",
        "Selçuk Bayraktar", "Haluk Bayraktar", "Sezen Aksu", "Ajda Pekkan", "Barış Manço",
        "Murat Boz", "Kenan Doğulu", "Demet Akalın", "Ebru Gündeş", "Mustafa Sandal",
        "Serdar Ortaç", "Hande Yener", "Aleyna Tilki", "Mabel Matiz", "Şebnem Ferah",
        "Sagopa Kajmer", "İbrahim Tatlıses", "Ferdi Tayfur", "Müslüm Gürses",
        "Orhan Gencebay", "Neşet Ertaş", "Zeki Müren", "Bülent Ersoy", "Emel Sayın",
        "Yıldız Tilbe", "Hülya Avşar", "Gülben Ergen", "Seda Sayan", "Şahan Gökbakar",
        "Ata Demirer", "Kemal Sunal", "Şener Şen", "Cüneyt Arkın", "Türkan Şoray",
        "Burak Özçivit", "Hande Erçel", "Kerem Bürsin", "Demet Özdemir", "Meryem Uzerli",
        "Halit Ergenç", "Kenan İmirzalıoğlu", "Oktay Kaynarca", "Yılmaz Güney",
        "Mor ve Ötesi",
        // ---- Yazarlar / bilim ----
        "Yaşar Kemal", "Nazım Hikmet", "Sabahattin Ali", "Orhan Kemal", "Oğuz Atay",
        "Yusuf Atılgan", "Ahmet Hamdi Tanpınar", "Peyami Safa", "Reşat Nuri Güntekin",
        "Halide Edip Adıvar", "İlber Ortaylı", "Celal Şengör", "Canan Karatay",
        "Ender Saraç", "Mehmet Öz", "Erdal Demirtaş", "Mahfi Eğilmez", "Oktay Sinanoğlu",
        "Fuat Sezgin", "Cahit Arf", "Feza Gürsey", "Fatih Sultan Mehmet",
        "Kanuni Sultan Süleyman", "Mimar Sinan", "Evliya Çelebi", "Nasreddin Hoca",
        // ---- Oyun / eğlence ----
        "League of Legends", "Dota 2", "Counter-Strike", "Call of Duty",
        "Grand Theft Auto", "EA Sports", "Epic Games", "Riot Games", "Stable Diffusion",
        "Star Wars", "Harry Potter", "Game of Thrones", "Yüzüklerin Efendisi",
        "Kuruluş Osman", "Diriliş Ertuğrul"
    };

    // =====================================================================
    // Tek kelimeler: Fuzzy=true → Levenshtein bulanık eşleşme;
    // false → yalnız tam eşleşme (günlük Türkçe sözcük çakışması riski).
    // =====================================================================
    private static readonly (string Word, bool Fuzzy)[] SingleEntries =
    {
        // ---- Teknoloji / küresel şirketler ----
        ("Google", true), ("Microsoft", true), ("Apple", true), ("Amazon", true),
        ("Netflix", true), ("Spotify", true), ("Samsung", true), ("Huawei", true),
        ("Xiaomi", true), ("Tesla", true), ("Toyota", true), ("Mercedes", true),
        ("Volkswagen", true), ("Intel", true), ("Nvidia", true), ("Oracle", true),
        ("Cisco", true), ("Dell", true), ("Lenovo", true), ("Asus", true),
        ("Sony", false), ("Philips", true), ("Siemens", true), ("Bosch", true),
        ("Nestlé", true), ("Pepsi", true), ("Starbucks", true), ("Airbnb", true),
        ("Uber", true), ("LinkedIn", true), ("Instagram", true), ("WhatsApp", true),
        ("YouTube", true), ("Telegram", true), ("TikTok", true), ("PayPal", true),
        ("Shopify", true), ("Salesforce", true), ("Adobe", true), ("Zoom", true),
        ("OpenAI", true), ("ChatGPT", true), ("DeepSeek", true), ("Gemini", true),
        ("Copilot", true), ("Claude", true), ("Midjourney", true),
        // ---- Daha fazla teknoloji / platform ----
        ("Oppo", true), ("Vivo", true), ("Realme", true), ("OnePlus", true),
        ("Infinix", true), ("Tecno", true), ("Nokia", true), ("Motorola", true),
        ("Windows", true), ("iPhone", true), ("iPad", true), ("MacBook", true),
        ("Android", true), ("PlayStation", true), ("Xbox", true), ("Nintendo", true),
        ("Steam", true), ("Roblox", true), ("Minecraft", true), ("Fortnite", true),
        ("Valorant", true), ("Twitch", true), ("Discord", true), ("Reddit", true),
        ("Pinterest", true), ("Snapchat", true), ("Facebook", true), ("Skype", true),
        ("Teams", true), ("Outlook", true), ("Gmail", false), ("Firefox", true),
        ("Chrome", true), ("Opera", false), ("Bing", true), ("Yahoo", true),
        ("eBay", true), ("AliExpress", true), ("Temu", true), ("Shein", true),
        ("Bolt", true), ("Deezer", true), ("fizy", true), ("Muud", true),
        ("Waze", true), ("Github", true), ("Canva", true), ("Figma", true),
        // ---- Türk şirketleri / markalar ----
        ("Togg", true), ("Turkcell", true), ("Trendyol", true), ("Hepsiburada", true),
        ("Yemeksepeti", true), ("Sahibinden", true), ("Arçelik", true), ("Vestel", true),
        ("Beko", true), ("Akbank", true), ("Garanti", true), ("Ziraat", true),
        ("Halkbank", true), ("Vakıfbank", true), ("Papara", true), ("Pegasus", true),
        ("ASELSAN", true), ("TUSAŞ", true), ("ROKETSAN", true), ("HAVELSAN", true),
        ("Otokar", true), ("Tofaş", true), ("Tüpraş", true), ("Baykar", true),
        ("Migros", true), ("DeFacto", true), ("Koton", true),
        // ---- Daha fazla Türk markası ----
        ("TEB", false), ("QNB", false), ("DenizBank", true), ("Fibabanka", true),
        ("Odeabank", true), ("Albaraka", true), ("ING", false), ("Paycell", true),
        ("ininal", true), ("Avea", true), ("THY", false), ("SunExpress", true), ("Corendon", true),
        ("Gittigidiyor", true), ("Teknosa", true), ("MediaMarkt", true), ("Carrefour", true),
        ("Kipa", true), ("Zara", false), ("Mango", false), ("Bershka", true),
        ("Stradivarius", true), ("Adidas", true), ("Nike", false), ("Puma", false),
        ("Reebok", true), ("Converse", true), ("Guess", true), ("Diesel", true),
        ("Vakko", true), ("Beymen", true), ("Altınyıldız", true), ("Kığılı", true),
        ("Ülker", true), ("Torku", true), ("Sütaş", true), ("İçim", false),
        ("Sek", false), ("Dimes", false), ("Cappy", true), ("Fruko", true),
        ("Cola Turka", true),        ("Fanta", true), ("Sprite", true), ("Lipton", true),
        ("Nescafé", true), ("Mado", true), ("Magnum", true), ("Algida", true),
        ("Godiva", true), ("Danone", true), ("Çaykur", true), ("Doğadan", true),
        ("McDonald's", true), ("KFC", false), ("Domino's", true), ("Subway", true),
        ("Popeyes", true), ("Digiturk", true), ("BluTV", true), ("Exxen", true),
        ("MUBI", true), ("IKEA", true), ("Decathlon", true),
        // ---- Medya / TV / gazete ----
        ("TRT", false), ("Habertürk", true), ("NTV", false), ("Hürriyet", false),
        ("ATV", false), ("TV8", false),
        ("TV100", false), ("Tele1", false), ("T24", false), ("OdaTV", true),
        ("Bianet", true), ("Medyascope", true), ("Akit", false),
        // ---- Oyun ----
        ("GTA", false), ("PUBG", false), ("FIFA", false), ("PSG", false),
        ("Ubisoft", true), ("Rockstar", true), ("Valve", true), ("Blizzard", true),
        // ---- Spor kulüpleri (yurt içi) ----
        ("Galatasaraylı", false), ("Galatasaray", true), ("Fenerbahçeli", false), ("Fenerbahçe", true),
        ("Beşiktaşlı", false), ("Beşiktaş", true), ("Trabzonspor", true),
        ("Başakşehir", true), ("Sivasspor", true), ("Konyaspor", true), ("Antalyaspor", true),
        ("Alanyaspor", true), ("Kayserispor", true), ("Hatayspor", true), ("Kasımpaşa", true),
        ("İstanbulspor", true), ("Ümraniyespor", true), ("Pendikspor", true), ("Eyüpspor", true),
        ("Göztepe", true), ("Karşıyaka", false), ("Bursaspor", true), ("Eskişehirspor", true),
        ("Ankaragücü", true), ("Gençlerbirliği", true), ("Denizlispor", true),
        ("Giresunspor", true), ("Adanaspor", true), ("Sakaryaspor", true),
        ("Kocaelispor", true), ("Tuzlaspor", true), ("Altay", true), ("Altınordu", true),
        ("Menemenspor", true), ("Bandırmaspor", true), ("Balıkesirspor", true),
        ("Amed", true),
        // ---- Spor kulüpleri (yurt dışı) ----
        ("Barcelona", true), ("Liverpool", true), ("Arsenal", true), ("Chelsea", true),
        ("Tottenham", true), ("Everton", true), ("Leeds", true), ("Juventus", true),
        ("Napoli", true), ("Roma", false), ("Lazio", true), ("Ajax", true),
        ("Porto", true), ("Benfica", true), ("Sporting", true), ("Celtic", true),
        ("Rangers", true), ("Flamengo", true), ("Inter", false), ("Milan", true),
        // ---- Kişiler (tek ad) ----
        // ("Getir" bilinçli olarak YOK: "süt getir" gibi en yaygın fiil çakışır;
        //  marka yalnızca InitialPrompt'ta kalır.)
        ("Atatürkçü", false), ("Atatürk", true), ("İmamoğlu", true), ("Yavaş", true), ("Sancar", true),
        ("Musk", true), ("Elon", true), ("Trump", true), ("Putin", true),
        ("Ronaldo", true), ("Messi", true), ("Neymar", true), ("Shakira", true),
        ("Beyoncé", true), ("Tarkan", true), ("Sıla", true), ("Hadise", true),
        ("Mbappé", true), ("Nadella", true), ("Pichai", true), ("Buffett", true),
        ("Gülşen", true), ("Bergen", true), ("Ceza", false), ("Ezhel", true),
        ("Gazapizm", true), ("Şanışer", true), ("Teoman", true), ("Duman", false),
        ("maNga", false),
        // ---- Yalnız tam eşleşme (günlük Türkçe sözcük çakışması riski) ----
        ("BİM", false), ("A101", false), ("Şok", false), ("TAV", false),
        ("N11", false), ("Pınar", false), ("Meta", false),
        ("Vodafone", false), ("BMW", false), ("IBM", false), ("LG", false),
        // ---- Şehirler (tam eşleşme — "İstanbul" yazımı doğru olsun) ----
        ("İstanbul", false), ("Ankara", false), ("İzmir", false), ("Bursa", false),
        ("Antalya", false), ("Adana", false), ("Konya", false), ("Gaziantep", false),
        ("Kayseri", false), ("Mersin", false), ("Diyarbakır", false), ("Hatay", false),
        ("Manisa", false), ("Kocaeli", false), ("Sakarya", false), ("Denizli", false),
        ("Eskişehir", false), ("Trabzon", false), ("Erzurum", false), ("Malatya", false),
        ("Van", false), ("Elazığ", false), ("Kahramanmaraş", false), ("Balıkesir", false),
        ("Tekirdağ", false), ("Çanakkale", false), ("Edirne", false), ("Aydın", false),
        ("Muğla", false), ("Bodrum", false), ("Fethiye", false), ("Marmaris", false),
        ("Alanya", false), ("Kapadokya", false), ("Nevşehir", false), ("Rize", false),
        ("Sivas", false), ("Tokat", false), ("Çorum", false), ("Aksaray", false),
        ("Niğde", false), ("Karaman", false), ("Isparta", false), ("Burdur", false),
        ("Uşak", false), ("Afyon", false), ("Kütahya", false), ("Düzce", false),
        ("Bolu", false), ("Zonguldak", false), ("Kastamonu", false), ("Sinop", false),
        ("Karabük", false), ("Yalova", false), ("Bilecik", false), ("Kırklareli", false),
        ("Kırşehir", false), ("Şırnak", false), ("Siirt", false), ("Batman", false),
        ("Mardin", false), ("Hakkari", false),        ("Iğdır", false), ("Ardahan", false), ("Kars", false), ("Bayburt", false), ("Gümüşhane", false),
        ("Tunceli", false), ("Bingöl", false), ("Muş", false), ("Bitlis", false),
        ("Kilis", false), ("Osmaniye", false), ("Adıyaman", false), ("Amasya", false),
        ("Giresun", false), ("Ordu", false), ("Bartın", false), ("Yozgat", false),
        // ---- İstanbul semtleri / ünlü yerler ----
        ("Taksim", false), ("Kadıköy", false), ("Üsküdar", false), ("Şişli", false),
        ("Beyoğlu", false), ("Fatih", false), ("Bağcılar", false), ("Esenyurt", false),
        ("Maltepe", false), ("Pendik", false), ("Kartal", false), ("Ataşehir", false),
        ("Ümraniye", false), ("Çekmeköy", false), ("Beylikdüzü", false), ("Avcılar", false),
        ("Küçükçekmece", false), ("Bakırköy", false), ("Zeytinburnu", false),
        ("Bayrampaşa", false), ("Gaziosmanpaşa", false), ("Eyüpsultan", false),
        ("Sarıyer", false), ("Silivri", false), ("Arnavutköy", false), ("Sultanbeyli", false),
        ("Esenler", false), ("Bahçelievler", false), ("Güngören", false), ("Galata", false),
        ("Karaköy", false), ("Nişantaşı", false), ("Levent", false), ("Maslak", false),
        ("Sultanahmet", false),
        // ---- Siyasi parti kısaltmaları ----
        ("CHP", false), ("MHP", false), ("HDP", false)
    };

    // Türkçe yaygın sözcük/şehir karalistesi (katlanmış hâlde): bu tokenlar için
    // sözlük eşleşmesi TAMAMEN atlanır — bulanık eşleştirme "samsun"→Samsung,
    // "garanti"→Garanti, "deniz"→DenizBank gibi yanlış pozitifler üretmesin.
    private static readonly HashSet<string> MatchBlocklist = new(StringComparer.Ordinal)
    {
        "samsun", "garanti", "sila", "ziraat", "yapi", "meta", "mavi", "eti",
        "deniz", "param", "sozcu", "metro", "onur", "para", "lazim", "yavas",
        "agri", "tokat", "gelsin", "hadise", "beka", "ordu", "hurriyet"
    };

    // =====================================================================
    // KULLANICI SÖZLÜĞÜ — kullanıcı transkripsiyon geçmişinden öğrenilen
    // düzeltmeler (TranscriptionHistoryService.Correct → AddUserCorrection).
    // Kullanıcı yanlış anlaşılan bir kelimeyi elle düzelttiğinde "yanlış→doğru"
    // çifti buraya eklenir ve Correct() bu eşleşmeleri İLK önce uygular —
    // zamanla kişiye özel tanıma oluşur. Kalıcılık: user_vocabulary.json
    // =====================================================================
    private static readonly object UserLock = new();
    private static Dictionary<string, string> _userWords = new(); // katlanmış → kanonik
    private static List<(string Folded, string Canonical)> _userPhrases = new();
    private static List<(string Folded, string Canonical)> _userSingles = new();
    private static bool _userDirty = true;

    /// <summary>Kullanıcı düzeltmesinden ÖĞRENİLMEMESİ gereken yaygın sözcükler.</summary>
    private static readonly HashSet<string> CommonWordBlocklist = new(StringComparer.Ordinal)
    {
        "beni", "bana", "bir", "ve", "bu", "su", "icin", "sonra", "lazim", "gibi",
        "kadar", "tamam", "yap", "yapma", "et", "gel", "git", "al", "ver", "ol",
        "var", "yok", "ama", "cok", "daha", "ne", "kim", "hangi", "bugun", "yarin",
        "hatirlat", "hatirlatma", "reminder", "alarm", "ben", "sen", "o", "biz",
        "siz", "onlar", "bunu", "sunu", "kendi", "belki", "artik", "hemen",
        "sadece", "basla", "bitir", "tamamla", "ekle", "sil", "goster", "ac", "kapat"
    };

    private static string UserWordsPath => Path.Combine(FileSystem.AppDataDirectory, "user_vocabulary.json");

    static TurkishVocabulary()
    {
        try
        {
            if (File.Exists(UserWordsPath))
            {
                var loaded = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(
                    File.ReadAllText(UserWordsPath));
                if (loaded != null)
                    _userWords = loaded;
            }
        }
        catch
        {
            _userWords = new Dictionary<string, string>();
        }
        RebuildUserIndexLocked();
    }

    /// <summary>
    /// Kullanıcı bir transkripsiyonu düzelttiğinde çağrılır: "yanlış→doğru"
    /// çiftini sözlüğe ekler (kısa/yaygın sözcükler ve aynı yazımlar atlanır).
    /// </summary>
    public static void AddUserCorrection(string wrong, string correct)
    {
        var w = CleanToken(wrong);
        var c = CleanToken(correct);
        if (w.Length == 0 || c.Length == 0) return;

        var fw = Fold(w);
        var fc = Fold(c);
        if (fw.Length < 3 || fc.Length < 3) return;
        if (fw == fc) return;
        if (CommonWordBlocklist.Contains(fw)) return;

        lock (UserLock)
        {
            _userWords[fw] = c;
            _userDirty = true;
            SaveUserWordsLocked();
        }
    }

    /// <summary>Tek bir doğru yazımı sözlüğe ekler (elle ekleme / güvenli yazım).</summary>
    public static void AddUserWord(string word)
    {
        var w = CleanToken(word);
        if (w.Length < 3) return;
        lock (UserLock)
        {
            _userWords[Fold(w)] = w;
            _userDirty = true;
            SaveUserWordsLocked();
        }
    }

    /// <summary>Kullanıcı sözlüğünden bir kelimeyi kaldırır.</summary>
    public static void RemoveUserWord(string word)
    {
        var w = CleanToken(word);
        if (w.Length == 0) return;
        lock (UserLock)
        {
            if (_userWords.Remove(Fold(w)))
            {
                _userDirty = true;
                SaveUserWordsLocked();
            }
        }
    }

    /// <summary>Kullanıcı sözlüğündeki kelimeler (alfabetik, kanonik yazımla).</summary>
    public static IReadOnlyList<string> GetUserWords()
    {
        lock (UserLock)
        {
            return _userWords.Values.OrderBy(v => v, StringComparer.OrdinalIgnoreCase).ToList();
        }
    }

    public static int UserWordCount
    {
        get { lock (UserLock) { return _userWords.Count; } }
    }

    private static void EnsureUserIndex()
    {
        lock (UserLock)
        {
            if (_userDirty)
                RebuildUserIndexLocked();
        }
    }

    private static void RebuildUserIndexLocked()
    {
        var phrases = new List<(string, string)>();
        var singles = new List<(string, string)>();
        foreach (var kv in _userWords)
        {
            if (kv.Key.Contains(' '))
                phrases.Add((kv.Key, kv.Value));
            else
                singles.Add((kv.Key, kv.Value));
        }
        _userPhrases = phrases.OrderByDescending(p => p.Item1.Count(c => c == ' ')).ToList();
        _userSingles = singles;
        _userDirty = false;
    }

    private static void SaveUserWordsLocked()
    {
        try
        {
            File.WriteAllText(UserWordsPath, System.Text.Json.JsonSerializer.Serialize(_userWords));
        }
        catch
        {
            // sözlük asla uygulamayı kırmaz
        }
    }

    /// <summary>Uçlardaki noktalama/boşlukları temizler (öğrenme öncesi).</summary>
    private static string CleanToken(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var t = s.Trim();
        while (t.Length > 0 && char.IsPunctuation(t[0])) t = t[1..];
        while (t.Length > 0 && char.IsPunctuation(t[^1])) t = t[..^1];
        return t.Trim();
    }

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
        "Aziz Sancar, Orhan Pamuk, Cem Yılmaz, Tarkan, Ronaldo, Messi, Taylor Swift, " +
        "Galatasaray, Fenerbahçe, Beşiktaş, Trabzonspor, Arda Güler, Hakan Çalhanoğlu, " +
        "İstanbul, Ankara, İzmir, TRT, Digiturk, Borsa İstanbul";

    // Uzun ifadeler önce eşleşsin: "Türkiye İş Bankası" → "İş Bankası",
    // "Adana Demirspor" → "Adana" gibi kısmi eşleşmeler kazanmaz.
    private static readonly (string Phrase, string Folded)[] Phrases = PhraseEntries
        .Select(p => (Phrase: p, Folded: Fold(p)))
        .OrderByDescending(p => p.Folded.Count(c => c == ' '))
        .ToArray();
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

        // 0) Kullanıcı sözlüğü — çok kelimeli ifadeler (kullanıcı düzeltmeleri önce uygulanır)
        EnsureUserIndex();
        foreach (var (key, canonical) in _userPhrases)
        {
            var wordCount = key.Count(c => c == ' ') + 1;

            for (var i = 0; i + wordCount <= tokens.Count; i++)
            {
                if (removed[i])
                    continue;

                var window = string.Join(" ", tokens.Skip(i).Take(wordCount).Select(Fold));
                if (window == key)
                {
                    tokens[i] = canonical;
                    for (var j = i + 1; j < i + wordCount; j++)
                        removed[j] = true;
                    i += wordCount - 1;
                }
            }
        }

        // 1) Çok kelimeli ifadeler (önce — daha uzun eşleşmeler kazanır)
        foreach (var (phrase, key) in Phrases)
        {
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

        // 2) Tek kelimeler — tam + bulanık (Levenshtein) + Türkçe iyelik ekleri.
        //    "Google'dan", "Trendyol'a", "goolgle'dan" → kök sözlükte aranır,
        //    ek korunur: "Google'dan", "Trendyol'a", "Google'dan".
        for (var i = 0; i < tokens.Count; i++)
        {
            if (removed[i])
                continue;

            var token = tokens[i];

            // Kesme işaretli iyelik/ekli form: kök + ek ayrıştır
            var stem = token;
            var suffix = string.Empty;
            var apIdx = token.IndexOf('\'');
            if (apIdx > 0)
            {
                stem = token.Substring(0, apIdx);
                suffix = token.Substring(apIdx); // "'dan", "'a" ...
            }

            var folded = Fold(stem);

            // Yaygın Türkçe sözcükler/şehirler: sözlük eşleşmesini atla (yanlış pozitif koruması)
            if (MatchBlocklist.Contains(folded))
                continue;

            // 2a) Kullanıcı sözlüğü — tek kelimeler (tam eşleşme, yerleşik sözlükten önce)
            var userMatched = false;
            foreach (var userEntry in _userSingles)
            {
                if (folded == userEntry.Folded)
                {
                    tokens[i] = userEntry.Canonical + suffix;
                    userMatched = true;
                    break;
                }
            }
            if (userMatched)
                continue;

            foreach (var entry in Singles)
            {
                if (entry.Folded.Length > folded.Length + 2)
                    continue;

                var match = folded == entry.Folded ||
                            (entry.Fuzzy && Levenshtein(folded, entry.Folded) <= DistanceLimit(entry.Folded.Length));

                // Ek almış ama kesme işareti yok: "trendyoldan" → "Trendyol'dan".
                // Yalnızca uzun özel isimlerde (>=6 harf) ve kısa eklerde (<=3) —
                // "cezası"→"Ceza'si", "milliyetçi"→"Milliyet'çi" gibi yanlış
                // pozitifleri önlemek için sınırlı tutulur.
                if (!match && suffix.Length == 0 && folded.Length > entry.Folded.Length &&
                    entry.Folded.Length >= 6 &&
                    folded.StartsWith(entry.Folded, StringComparison.Ordinal) &&
                    folded.Length - entry.Folded.Length <= 3)
                {
                    tokens[i] = entry.Canonical + "'" + token.Substring(entry.Folded.Length);
                    break;
                }

                if (match)
                {
                    tokens[i] = entry.Canonical + suffix;
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
