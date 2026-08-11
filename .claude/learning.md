# learning.md — Todo Voice Çalışan Özellikler

Bu dosya, **şu an gerçekten çalışan** özellikleri listeler. Yalnız aktif
özellikler burada durur; kaldırılan özellik buradan çıkarılır (tarihçesi
git'te kalır). Geçmiş TUTMAZ — sadece bugünü anlatır.

İlk kez çalışan bir ajan bu dosyayı okur: "Şu an ne çalışıyor?"

---

## 1. Çalışan Özellikler

### 🎤 Voice-first görev oluşturma (ÜRÜNÜN KALBİ)
- TodoListPage alt barında mikrofon butonu: `StartSpeechToTextCommand`
- Bas → dinle → konuş → bitir → **çevrimdışı Whisper** transkripsiyon → görev oluşur
- Buton toggle'dır: dinlerken tekrar bas → durdur ve transkript et
- `SpeechToTextService`: Whisper.net (whisper.cpp) — `TranscribeFileAsync` ile
  WAV → 16kHz mono → metin (ADR-016: unpackaged'ta Windows SpeechRecognizer çalışmaz)
- **Model:** `ggml-small-q5_1.bin` (~190 MB, quantized small) — ilk kullanımda arka
  planda indirilir (App başlangıcı), sonra önbellekte
  (`%LOCALAPPDATA%\TodoVoiceMaui\TodoVoiceMaui\Data\models\`)
- **Model kataloğu — 4 katman (Ayarlar → Ses Tanıma):** Minimum 190MB (small-q5_1,
  varsayılan) / Orta 539MB (medium-q5_0) / Yüksek 874MB (large-v3-turbo-q8_0) /
  Maximum 3,1GB (large-v3). Her modelin boyut/hız/doğruluk çipleri + dürüst açıklaması
  gösterilir. Seçim Preferences (`stt_model`) saklanır; 1GB+ model indirilmeden önce
  onay sorulur. Model geçişinde ESKİ model yalnızca YENİ model hazır olduktan sonra
  silinir; geçiş başarısız olursa seçim geri alınır.
- **Transkripsiyon KAYNAĞI seçici (bulut destek):** Ayarlar'da kaynak seçilir —
  Çevrimdışı Whisper (anahtar yok, varsayılan) veya bulut: OpenAI
  (gpt-4o-mini-transcribe), Groq (whisper-large-v3-turbo), Deepgram (Nova-3),
  ElevenLabs (Scribe v2). Katalogda ayrıca Google/Azure/AssemblyAI/Fireworks/
  Cloudflare/Soniox "yakında" olarak listelenir. API anahtarları Preferences
  (`stt_apikey_{id}`) saklanır; Ayarlar'da "Bağlantıyı Test Et" ile doğrulanır.
  **Fallback:** bulut başarısız/anahtar yoksa otomatik çevrimdışı Whisper'a düşülür.
  `TurkishVocabulary.Correct()` tüm kaynaklarda çalışır; prompt önyüklemesi bulut
  API'lerinin prompt/keyterm alanlarına da yazılır.
- **Decode önyükleme:** `WithPrompt(TurkishVocabulary.InitialPrompt)` — ünlü
  şirket/kişi isimleri whisper token seçimine yönlendirilir.
- **Özel isim otomatik düzeltme:** `TurkishVocabulary.Correct()` transkripsiyon
  sonrası çalışır — ~250 tek + ~160 çok kelimeli Türkçe özel isim sözlüğü
  (Türk şirketleri/bankalar/markalar/medya/spor kulüpleri/şehirler/ünlüler),
  Türkçe normalize + Levenshtein bulanık eşleştirme + **Türkçe iyelik eki desteği**
  ("Google'dan"→Google, "Trendyol'a"→Trendyol) + yanlış pozitif karalistesi.
  "goolgle"→Google, "is bankasi"→İş Bankası, "elon mask"→Elon Musk
  (konsol testi: 56/56 — iyelik ekleri + yanlış pozitif korumaları dahil).
- Dinlerken `VoiceFlowState=Listening`; işlenirken "Ses tanınıyor..." gösterilir
- Güven: `HIGH` — pipeline konsol testi + canlı akış kullanıcı testi; düzeltme katmanı
  56 senaryoluk test setiyle doğrulandı

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

### ✨ Arayüz cilası (animasyon + ses efektleri)
- `AnimationService`: sayfa girişi fade+rise (easeOutQuint), kart/satır hover lift,
  mikrofon nefes döngüsü (`BreathHandle` — dinlerken Görevler'deki 🎤 butonu nabız atar)
- `SoundEffectService`: çalışma zamanında **sentezlenen** WAV tonları
  (winmm `PlaySound` P/Invoke — asset yok, paket kimliği gerekmez): Click/Success/Error/
  Delete/MicStart/MicStop. Görev oluşunca başarı tonu, mikrofona basınca nefes tonu vb.
- Ayarlar → **"Ses efektleri"** anahtarı (Preferences: `enable_sounds`, cihazda saklanır)
- Tüm butonlarda `Pressed` scale (0.92–0.97) + `PointerOver` durumları (Styles.xaml)
- Güven: `HIGH` — build 0 hata, açık + koyu temada runtime doğrulandı, `app.log` temiz

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

- **Konuşurken canlı metin (streaming) yok:** Whisper tek atış (kaydet → çevir)
  çalışır; "konuşurken anlık metin" deneyimi için chunked whisper gerekir (roadmap).
- **İlk kullanım / model değişimi indirmesi:** seçili model (varsayılan Small ~190 MB;
  1GB+ modeller için onay istenir); çevrimdışıyken indirilemez, hata mesajı gösterilir,
  uygulama çökmez ve mevcut model korunur.
- **Konuşmasız girdide** Whisper yanlış metin üretebilir (ör. "[...müzik çalıyor...]");
  düşük güven eşiği politikası roadmap'te.
- **Android / iOS / macOS hedefleri**: csproj'da yalnız Windows TFM aktif.
- **Supabase ayakta değilse**: veri local'de çalışır, sync sessizce düşer (çökme yok).

---

## 3. Test Kimlikleri / Ortam

- Lokal Supabase: `http://127.0.0.1:54321`
- Test hesabı ve token: `C:\temp\opencode\test-creds.txt` (repo dışı)
- Edge function serve logları: `C:\temp\opencode\functions-serve.log` / `.err`
- Docker logları UTC'dir (yerel UTC+3)
