# learning.md — Todo Voice Çalışan Özellikler

Bu dosya, **şu an gerçekten çalışan** özellikleri listeler. Yalnız aktif
özellikler burada durur; kaldırılan özellik buradan çıkarılır (tarihçesi
git'te kalır). Geçmiş TUTMAZ — sadece bugünü anlatır.

İlk kez çalışan bir ajan bu dosyayı okur: "Şu an ne çalışıyor?"

---

## 1. Çalışan Özellikler

### 🎤 Voice-first görev oluşturma (ÜRÜNÜN KALBİ)
- TodoListPage alt barında mikrofon butonu: `StartSpeechToTextCommand`
- Bas → dinle → konuş → canlı transkripsiyon → final metin otomatik görev olarak oluşur
- Buton toggle'dır: dinlerken tekrar bas → durdur (`StopSpeechToTextCommand`)
- `SpeechToTextService`: `ContinuousRecognitionSession` ile canlı `LiveTranscript`
- Dinlerken canlı metin ve "⏹️ Bitir" butonu görünür
- Güven: `MEDIUM` — runtime'da elle test edilmedi (son build hatasız, uygulama açıldı)

### 🔁 Ses kaydı + oynatma (göreve not)
- `AudioService` WAV kaydı (16-bit 44.1kHz), durdur, base64 upload
- TodoDetailPage'de kayıt listesi, oynatma, progress bar, silme
- Playback position canlı güncellenir (`PlaybackPositionUpdated`)
- Güven: `HIGH` — önceki sesli görev özelliği çalışıyordu; plugin API'leri doğrulandı

### 📊 İstatistik dashboard (Ana Sayfa)
- Toplam / Tamamlanan / Bekleyen / Sesli görev kartları
- `MainPageViewModel.LoadStatsAsync` SQLite'dan okur
- Güven: `HIGH`

### 🔍 Filtre & sıralama (Görevler)
- Durum filtreleri: Tümü / Bekleyen / Tamamlanan / Sesli
- Öncelik filtresi + sıralama (en yeni, en eski, teslim tarihi, öncelik)
- Arama (`SearchText`)
- Güven: `HIGH`

### ⏰ Hatırlatıcılar
- Görevde `reminder_at`; detail sayfasında düzenlenir
- `ReminderService`: 15 sn döngü, SQLite tarama, Windows toast bildirimi
- Login yokken başlamaz (yalnız `_reminderService.Start()` login branch'inde)
- Güven: `MEDIUM` — API E2E (create+delete `reminder_at`) doğrulandı, toast runtime test edilmedi

### 🌙 Tema (açık / koyu)
- `ThemeService.ApplyTheme/SaveTheme/GetSavedTheme/ApplySavedTheme`
- `AppThemeBinding` token tabanlı; `Dark*` renk anahtarları Colors.xaml'da
- Ayarlar sayfasında tema seçimi; kaydedilip uygulanır
- Güven: `HIGH` — build doğrulandı, tema geçişi kodda tanımlı

### 📴 Local-first veri + senkronizasyon
- Tüm yazma önce SQLite, sonra Supabase
- `SyncService` 4 adım sync (profil → ses → todo push → todo pull)
- Çevrimdışı→online geçişte otomatik sync
- Login yokken `local-user` fallback ile çalışır
- **Sync envelope (ADR-010/011):** `LocalTodo` `NeedsSync` (dirty) +
  `IsDeleted` (tombstone) + `LocalVersion` taşır. Silme önce tombstone, server
  onaylayınca purge edilir — offline silme kaybolmaz.
- `ITodoStore` (ADR-012) veri katmanını soyutlar; ViewModel'ler `SupabaseService`'i
  bilmez, `SyncService` facade'ı üzerinden remote erişir.
- Güven: `HIGH` (build doğrulandı; supabase çökmeden düşüş test edildi)

### 🔐 Giriş / kayıt (LoginPage)
- Kod tamam, ancak **varsayılan akışta atlanıyor** (login'siz prototip kararı)
- Ayarlar sayfasından çıkış yapılınca LoginPage gösterilir
- Güven: `HIGH` (kod mevcut, varsayılan akışta kullanılmıyor)

---

## 2. Çalışmayan / Ertelenen / Bilinen Sorunlar

- **Ara sıra çökme:** "Çöküyor hala arada bir" — kök neden tespit edilemedi;
  son iki başlatmada çökme yok, `app.log` boş. Şüpheli: SpeechRecognizer yaşam
  döngüsü veya SyncService HTTP zaman aşımı. `MEDIUM`/`LOW`
- **Sesli görevlerin transkripsiyonu**: eski ses kaydı akışı sesi sadece .wav
  yüklüyordu, metne çevirmiyordu; şimdi canlı transkripsiyon ekleniyor.
- **Android / iOS / macOS hedefleri**: csproj'da yalnız Windows TFM aktif.
- **Supabase ayakta değilse**: veri local'de çalışır, sync sessizce düşer (çökme yok).

---

## 3. Test Kimlikleri / Ortam

- Lokal Supabase: `http://127.0.0.1:54321`
- Test hesabı ve token: `C:\temp\opencode\test-creds.txt` (repo dışı)
- Edge function serve logları: `C:\temp\opencode\functions-serve.log` / `.err`
- Docker logları UTC'dir (yerel UTC+3)
