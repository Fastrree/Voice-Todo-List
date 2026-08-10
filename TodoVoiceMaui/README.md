# Todo Voice - .NET MAUI Uygulaması

## 📱 Proje Özeti
Todo Voice, **voice-first** bir görev yönetimi uygulamasıdır. Mikrofona bas,
konuş; uygulama söylediklerini **gerçek zamanlı transkripsiyon** ile metne
çevirir ve görev olarak oluşturur.

> **Güncel platform:** Windows (net8.0-windows10.0.19041.0). Android/iOS
> hedefleri henüz etkin değildir.

## ✨ Özellikler

- 🎤 **Voice-first görev oluşturma** — bas, konuş, görev otomatik oluşur
- 🔴 **Canlı transkripsiyon** — konuşurken metin anlık görünür (continuous recognition)
- 🔁 **Ses kaydı + oynatma** — göreve ses notu, detay sayfasında oynatma
- 📊 **İstatistik dashboard** — toplam / tamamlanan / bekleyen / sesli görev
- 🔍 **Filtre & sıralama** — durum, öncelik, teslim tarihi, arama
- ⏰ **Hatırlatıcılar** — Windows toast bildirimi (`reminder_at`)
- 🌙 **Açık / Koyu tema** — `AppThemeBinding` token tabanlı
- 📴 **Local-first** — SQLite; online olunca Supabase'e senkron
- 🔐 **Supabase backend** — Auth / Postgres / Storage / Edge Functions

## 🛠️ Teknoloji

| Katman | Teknoloji |
|--------|-----------|
| UI | .NET MAUI 8 (XAML + MVVM) |
| MVVM | CommunityToolkit.Mvvm (source generators) |
| Ses | Plugin.Maui.Audio (WAV 16-bit 44.1kHz) |
| Transkripsiyon | Windows SpeechRecognizer (continuous) |
| Local veri | sqlite-net-pcl |
| Backend | Supabase (HttpClient ile doğrudan çağrı) |

## 📂 Proje Yapısı

```
TodoVoiceMaui/
├── .claude/                  # AI context & dokümantasyon (AGENT.md başlangıç noktası)
├── TodoVoiceMaui/            # MAUI uygulaması
│   ├── Views/                #   XAML sayfaları
│   ├── ViewModels/           #   MVVM viewmodelleri
│   ├── Services/             #   Supabase, Audio, Sync, DB, Speech, Reminder, Theme
│   ├── Models/               #   Todo, VoiceRecording, UserProfile
│   ├── Converters/           #   IValueConverter'lar
│   └── Resources/            #   Stiller, ikonlar, splash
└── server/                   # Node mock API (geliştirme)
```

## 🚀 Kurulum ve Çalıştırma

### Gereksinimler
- .NET 8 SDK + MAUI workload
- Windows 10/11 + Windows App SDK (self-contained build dahil)

### Build (Windows)
```powershell
Remove-Item Env:\MSBuildSDKsPath -ErrorAction SilentlyContinue
dotnet build TodoVoiceMaui\TodoVoiceMaui.csproj -c Debug `
  -f net8.0-windows10.0.19041.0 `
  -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true `
  -p:MauiVersion=8.0.100 --nologo -v q
```

Çıktı: `TodoVoiceMaui\bin\Debug\net8.0-windows10.0.19041.0\win10-x64\TodoVoiceMaui.exe`

> **Not:** Build öncesi `MSBuildSDKsPath` temizlenmezse restore hataları oluşur.
> Detay: `.claude/architecture.md`.

## 📋 Kullanım

1. **Görev ekleme (sesli):** Mikrofon butonuna bas → konuş → canlı metni
   izle → final metin otomatik görev olarak eklenir. Dinlerken tekrar
   basarak durdur ("⏹️ Bitir").
2. **Görev yönetimi:** Checkbox ile tamamla; göreve tıklayarak detay;
   çöp kutusu ile sil; filtre/sıralama çubukları.
3. **Ses notu:** Detay sayfasında kayıt başlat/durdur; kayıt listesinden
   oynat.
4. **Hatırlatıcı:** Detay sayfasında `reminder_at` seç; uygulama açıkken
   Windows toast gösterir.
5. **Tema:** Ayarlar sayfasından açık/koyu; tercih saklanır.

## 🎨 Tasarım

Tasarım sistemine yönelik planlar ve **Liquid Glass** (cam / ışık kırılması /
yansıma / akışkan saydamlık) yaklaşımı şurada tanımlıdır:
- `.claude/design-system.md` — palet, tipografi, boşluk, radius, cam
- `.claude/transition-framework.md` — hikaye odaklı animasyon + Liquid Glass mühendislik planı

> Mevcut uygulama **premium yeniden tasarım** aşamasındadır; şu anki tema
> eski (Material) stillerdir ve yenilenecektir.

## 🔐 Güvenlik Notları

- Supabase anon key koddadır (public client için normal); **service role key
  ve JWT secret asla repo'ya yazılmaz.**
- Test token / kimlik bilgileri repo DIŞINDA tutulur
  (`C:\temp\opencode\test-creds.txt`).
- Mikrofon izni Windows privacy ayarları üzerinden işler.

## 🧠 AI / Ajan Notları

Depo `.claude/` context sistemi içerir. Bu proje üzerinde çalışan tüm AI
ajanları **`.claude/AGENT.md`** okumak zorundadır — bağlayıcı kurallar,
güven seviyeleri ve **regression guard** oradadır.

## 📄 Lisans
Özel / tescilli uygulama.
