# 🎤 Todo Voice

**Sesli görev yönetimi — konuş, uygulama senin için görevi yazsın.**

Todo Voice, voice-first bir todo listesi uygulamasıdır. Mikrofona bas, konuş;
uygulama söylediklerini **gerçek zamanlı transkripsiyon** ile metne çevirir ve
görev olarak oluşturur. Modern AI sesli asistanlarının konuşma modu deneyimi,
görev yönetimine uyarlanmıştır.

---

## ✨ Ürün Akışı

```
🎤 Mikrofona bas  →  🗣️ Konuş  →  📝 Canlı transkripsiyon  →  ✅ Görev oluştu
```

Bu akış ürünün kalbidir. Ses kaydı aynı zamanda göreve eklenir ve daha sonra
oynatılabilir.

## 🚀 Özellikler

- 🎤 **Voice-first görev oluşturma** — konuş, görev olarak kaydedilsin
- 🔴 **Gerçek zamanlı transkripsiyon** — konuşurken metin anlık görünür
- 🔁 **Ses kaydı + oynatma** — göreve ses notu ekle, sonra dinle
- 📊 **İstatistik dashboard** — toplam / tamamlanan / bekleyen / sesli görev
- 🔍 **Filtre & sıralama** — durum, öncelik, teslim tarihi, sesli görev
- ⏰ **Hatırlatıcılar** — Windows bildirimi ile görev hatırlatması
- 🌙 **Açık / Koyu tema** — `AppThemeBinding` token tabanlı, bağımsız tasarım
- 📴 **Local-first** — SQLite ile çevrimdışı tam çalışır; çevrimiçi olunca Supabase'e senkronize olur
- 🔐 **Supabase backend** — Auth / Postgres / Storage / Edge Functions

## 🛠️ Teknoloji

| Katman | Teknoloji |
|--------|-----------|
| UI | .NET MAUI 8 (C# / XAML) — şu an Windows desktop |
| Mimari | MVVM (CommunityToolkit.Mvvm) |
| Ses | Plugin.Maui.Audio, Windows SpeechRecognizer (canlı transkripsiyon) |
| Local veri | SQLite (sqlite-net-pcl) |
| Backend | Supabase (Auth, Postgres, Storage, Edge Functions) |
| Sync | Local-first SyncService (online olduğunda otomatik senkron) |

## 📂 Proje Yapısı

```
TodoVoiceMaui/
├── .claude/                  # AI context & dokümantasyon sistemi
│   ├── AGENT.md              #   AI anayasası + regression guard
│   ├── INDEX.md              #   Dokümantasyon haritası
│   ├── architecture.md       #   Mimari + kritik kararlar
│   ├── learning.md           #   Çalışan özellikler
│   ├── roadmap.md            #   Gelecek planı (todo listesi)
│   ├── design-system.md      #   Tasarım sistemi
│   └── transition-framework.md # Animasyon altyapısı
├── TodoVoiceMaui/            # MAUI uygulaması
│   ├── Views/                #   XAML sayfaları
│   ├── ViewModels/           #   MVVM viewmodelleri
│   ├── Services/             #   Supabase, Audio, Sync, DB, Speech, Reminder
│   ├── Models/               #   Todo, VoiceRecording, UserProfile
│   ├── Converters/           #   IValueConverter'lar
│   └── Resources/            #   Stiller, ikonlar, splash
├── server/                   # Node mock API (geliştirme)
└── global.json
```

## 🔧 Geliştirme

### Gereksinimler
- .NET 8 SDK + MAUI workload (Windows)
- Lokal Supabase (isteğe bağlı — uygulama çevrimdışı da çalışır)

### Build & Çalıştırma
```powershell
# Not: MAUI Windows build öncesi MSBuildSDKsPath env temizlenmeli
Remove-Item Env:\MSBuildSDKsPath
dotnet build TodoVoiceMaui\TodoVoiceMaui.csproj -c Debug `
  -f net8.0-windows10.0.19041.0 `
  -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true `
  -p:MauiVersion=8.0.100 --nologo -v q
```

Çıktı exe: `TodoVoiceMaui\bin\Debug\net8.0-windows10.0.19041.0\win10-x64\TodoVoiceMaui.exe`

> **Önemli:** Build komutu `MSBuildSDKsPath` ortam değişkeni temizlenmeden çağrılırsa
> restore hataları oluşabilir. Detay: `.claude/architecture.md`.

## 🧠 AI / Ajan Notları

Bu depo bir `.claude/` context sistemi içerir. Proje üzerinde çalışan her AI
ajanı (opencode, Claude Code, Codex vb.) **`AGENT.md`'yi okumak zorundadır** —
orada bağlayıcı kurallar, güven seviyeleri, karar yetkisi ve **regression guard**
(tamamlanma kapısı) tanımlıdır.

## 🧪 Test Hesabı (lokalde)

- E-posta / şifre backend test hesabı dökümantasyonda gizli tutulur; token'lar
  repo dışında (`C:\temp\opencode\test-creds.txt`) saklanır.

---

## 📄 Lisans

Özel / tescilli uygulama.
