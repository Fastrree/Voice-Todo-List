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
  Çevrimdışı Whisper (anahtar yok, varsayılan) veya bulut (8 kaynak): OpenAI
  (gpt-4o-mini-transcribe), Groq (whisper-large-v3-turbo), Fireworks (whisper-v3),
  Deepgram (Nova-3), ElevenLabs (Scribe v2), AssemblyAI (Universal-2),
  **Google (Chirp 2 — v1 `speech:recognize`, `?key=` + LINEAR16 base64; hata halinde
  `latest_short` fallback)**, **Azure AI Speech (bölge + Ocp-Apim-Subscription-Key,
  16kHz WAV gövde — bölge Ayarlar'da ayrı alan)**. Katalogda ayrıca
  Cloudflare/Soniox/Sestek "yakında" olarak listelenir.
  **API anahtarları Windows Credential Manager (Vault) ile saklanır**
  (`WindowsCredentialStore` — advapi32 CredWrite/CredRead/CredDelete P/Invoke,
  blob OS tarafından şifreli, unpackaged'ta çalışır). DPAPI-in-Preferences fallback;
  eski `enc:`/düz metin kayıtları okumada Vault'a GÖÇ eder (tek yönlü).
  **Uygulama kilidi (GÜVENLİK — PIN / Windows Hello):** Ayarlar'da GÜVENLİK
  bölümü: kilit yöntemi Kapalı / PIN / Windows Hello (AppLockMethod, kalıcı).
  **PIN:** şifre ayarlayıcı modalı (`PinSetupPopup` — kur: yeni+tekrar; değiştir:
  mevcut doğrulamalı), 4-8 hane, tuzlu SHA-256 özet olarak Preferences'ta saklanır
  (düz metin yazılmaz; `AppLockService` — sabit zamanlı karşılaştırma).
  **Windows Hello:** `BiometricService` (UserConsentVerifier — parmak izi/yüz/PIN),
  unpackaged'ta kullanılamıyorsa PIN'e düşer. Kilit aktifken API anahtarı gizli
  kalır — görüntüleme ve bağlantı testi aktif yöntemle doğrulama ister (PIN →
  `PinVerifyPopup`, Hello → biyometri). "Ayarlar sekmesine geçerken kilidi sor"
  anahtarı açıkken Ayarlar girişinde kilit overlay'i gösterilir (ilk kareden aktif,
  flaş yok); PIN formu + Windows Hello fallback + "Kiliti sıfırla" içerir. Oturum
  içinde bir kez açılınca tekrar sorulmaz (`MarkUnlocked` — uygulama yeniden
  başlayana dek). API anahtarı alanında **gizle/göster (göz) butonu** vardır
  (kilitliyken gizli).
- **Canlı konsol (STT):** `SttTestLog` statik logger'ı — bağlantı testleri, indirme
  ve çevrimdışı transkripsiyon satırları Ayarlar'daki koyu terminal kutusunda ve
  Model Yönetimi modalında CANLI ve RENKLİ akar (`→ istek` mavi / `✓ başarı` yeşil /
  `✗ hata` kırmızı / `⚠ uyarı` sarı / `⬇ indirme` camgöbeği). Satır tipleri
  `SttLogKind` enum'ıyla SttTestLog'da ayrıştırılır (Write/Success/Error/Warning/
  Download metotları); UI `FormattedString` + renkli Span'lerle çizer. Otomatik
  kaydırma + Temizle; aynı satırlar `app.log`'a yazılır. Thread güvenli (MainThread
  marshal), satır sayısı sınırlı (200). **Filtre çipleri:** Tümü / ✓ Başarı / ✗ Hata /
  ⚠ Uyarı — yalnız RENDER'ı filtreler, satırlar toplanmaya devam eder (seçili çip
  DataTrigger ile vurgulanır); **filtre seçimi kalıcıdır** (`stt_console_filter`
  Preferences — Settings ile popup aynı anahtarı paylaşır, biri değişince diğeri
  senkronize olur). **İndirme satırları:** her %10'da bir renkli `⬇ %X · MB/MB`
  satırı; bitince `✓ tamamlandı · süre · ort. hız/sn` satırı. **Dışa Aktar:**
  konsol satırları `[TİP] [saat] metin` biçiminde `TodoVoice_console_*.log`
  dosyasına yazılır (ms damgalı, çakışmaz) ve Explorer'da seçili açılır. Çevrimdışı
  "Test" butonu seçili modelle GERÇEK transkripsiyon çalıştırır (büyük model onaylı).
  Ayarlar'da "Bağlantıyı Test Et" (HTTP 2xx = geçerli) ile doğrulanır.
  **Fallback:** bulut başarısız/anahtar yoksa otomatik çevrimdışı Whisper'a düşülür.
  `TurkishVocabulary.Correct()` tüm kaynaklarda çalışır; prompt önyüklemesi bulut
  API'lerinin prompt/keyterm alanlarına da yazılır.
- **İndirme deneyimi:** "İndir ve Kullan" butonu indirme sırasında **yeşil ilerleme
  çubuğuna** dönüşür (% + indirilen/toplam MB içinde); tıklanınca **detaylı modal**
  açılır (büyük yüzde, yeşil bar, miktar, anlık hız, güvenlik notu, iptal). İndirme
  iptal edilebilir (`CancellationTokenSource`), kısmi `.part` temizlenir; modal
  dışına tıklanınca arka planda devam eder, bitince kendini kapatır.
- **ÇOKLU EŞZAMANLI İNDİRME:** `SpeechToTextService` artık tek indirme yerine her
  model için ayrı iş tutar (`ModelDownloadJob` — kendi ilerleme/byte/hız/iptal/
  tamamlanma görevi). `DownloadModelAsync(model)` seçimi değiştirmeden arka planda
  indirir; birden fazla model aynı anda inebilir (Model Yönetimi modalında her satır
  kendi çubuğunu gösterir, başlıkta "N model indiriliyor" bilgisi). Aynı modelin
  ikinci indirme isteği mevcut işe bağlanır (çift indirme yok). `IsDownloading`
  "herhangi bir iş aktif" demektir; Ayarlar kartı yalnız SEÇİLİ modelin ilerlemesini
  gösterir. İndirme sırasında seçim değişirse tamamlanmada seçim canlı okunur.
- **Kullanım istatistikleri:** her gerçek transkripsiyon denemesi sağlayıcı bazında
  kaydedilir (deneme/başarı/hata/toplam ses süresi/karakter/son kullanım) —
  `SttUsageStats` (JSON kalıcı, `Changed` event). Ayarlar'da KULLANIM İSTATİSTİKLERİ
  kartı her sağlayıcıyı başarı oranı + süre + karakterle listeler; **son 7 günün
  başarılı transkripsiyon sayısı mini çubuk grafikle** gösterilir (günlük sayaç
  `yyyyMMdd`, 30 gün eskiği temizlenir). Eski JSON formatı göçle korunur.
  "Sıfırla" ile tümü temizlenir. Sessiz test transkripsiyonları sayılmaz
  (`trackStats:false`).
- **Aktif indirmeler şeridi + disk temizliği (Model Yönetimi):** birden fazla model
  inerken üstte her iş için mini çubuk + İptal; altta **"Kullanılmayan Modelleri
  Sil"** — aktif model ve en küçük KURULU model korunur, kalanlar tek onayla silinir.
- **Model Yönetimi modalı:** 4 katman tek ekranda — her model için boyut/RAM/WER
  (tahminî)/dil/kuantizasyon/hız/öneri, kurulu durum + disk boyutu, toplam disk,
  indir (yeşil bar + iptal) ve **sil** (aktif model silinemez) + canlı konsol.
  Ayarlar detay kartı da RAM/WER/kuantizasyon/öneri gösterir.
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
- **Sesli hatırlatıcı komutu:** "10 dakika sonra süt almayı hatırlat" → görev
  "Süt al" + `ReminderAt` (şimdi+10dk) oluşturulur. `RuleBasedVoiceCommandParser`
  zaman kalıplarını çözer (`N dakika/saat sonra|içinde`, `saat HH:MM`, `yarın [HH'da]`,
  sabah/öğlen/akşam, bugün — geçen saat dilimleri yarına kayar) ve çerçeveyi
  başlıktan sıyar ("hatırlat/beni hatırlat" + zaman ifadeleri + "-mayı/-meyi" eki).
  Reminder kontrolü Complete'ten ÖNCE çalışır — "bitirmeyi hatırlat"/"...tamam"
  gibi ifadeler hedefsiz Complete komutuna dönüşmez. `reminder_at` uçtan uca taşınır
  (sink → Sync → Supabase → edge fn; create'te `dueDate/reminderAt` camelCase okuma).
- `ReminderService`: 15 sn döngü, SQLite tarama, **ses tonu** (`SoundEffectService.Reminder`
  — yumuşak üç tonlu davet) + Windows toast bildirimi
- Listedeki görev satırında 🔔 rozeti + hatırlatma zamanı görünür (`HasReminder`)
- Login yokken başlamaz (yalnız `_reminderService.Start()` login branch'inde)
- Güven: `MEDIUM` — API E2E (create+delete `reminder_at`) doğrulandı, toast runtime test edilmedi

### 🕘 Transkripsiyon geçmişi + düzeltme (kişisel sözlük)
- Her başarılı ses tanıması kalıcı geçmişe yazılır (`transcription_history.json`,
  en fazla 100 kayıt) — Görevler alt barındaki 🕘 butonu `TranscriptionHistoryPopup`'u açar
- Kayıt başına Düzelt/Vazgeç/Kaydet/Sil; düzeltme metni günceller VE değişen kelime
  çiftlerini **kullanıcı sözlüğüne öğretir** (`TurkishVocabulary.AddUserCorrection`)
- Öğrenme yalnızca eşit token sayılı düzenlemelerde (yapısal değişiklikte atlanır),
  engel listesi dışı (yaygın sözcükler öğrenilmez) ve ≥3 harf; `user_vocabulary.json`
  kalıcıdır. `Correct()` kullanıcı eşleşmelerini yerleşik sözlükten ÖNCE uygular
- Modalda "KİŞİSEL SÖZLÜK" bölümü öğrenilen kelimeleri çiplerde gösterir (dokun→kaldır)
- Güven: `HIGH` — build 0 hata, thread güvenli (liste referans değişimi + lock)

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
