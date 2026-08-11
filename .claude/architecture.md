# architecture.md — Todo Voice Mimari ve Kritik Kararlar

Bu dosya, sistemin güncel mimarisini ve kritik kararları (nedenleriyle) içerir.
Mimari veya önemli bir teknik karar değiştiğinde güncellenir.

İlk kez çalışan bir ajan önce `AGENT.md` (kurallar), sonra bu dosyayı okur.

---

## 1. Genel Bakış

Todo Voice bir **.NET MAUI 8** Windows masaüstü uygulamasıdır. MVVM deseni
kullanır. Veri **local-first** olarak SQLite'a yazılır; çevrimiçi olduğunda
Supabase'e senkronize edilir.

Mimari model: **Feature-as-an-App** (bkz. §1.1). Uygulama sekmeleri "ekran"
değil, aynı platform üzerinde çalışan bağımsız feature'lardır. Bugün tek
feature var (Voice Todo); yapı, ikinci bir feature'ı (ör. Translator) mevcut
feature'lara dokunmadan eklemeye izin verir.

```
              PLATFORM (ortak yetenekler)
   DesignSystem · Audio · Speech · Storage · Permissions · Motion
                         │
        ┌────────────────┼────────────────┐
        │                                │
   Feature: VoiceTodo               Feature: (ileride)
   kendi Domain/App/UI state        kendi Domain/App/UI state
```

### 1.1 Feature-as-an-App & Platform ≠ Feature

- **Platform** ortak capability sağlar (Design System, Audio, Speech, Storage,
  Permissions, Motion, Accessibility, Common Contracts).
- **Feature** kendi ürün davranışından sorumludur (state, workflow, UI,
  error/recovery).
- **Feature isolation:** Feature'lar birbirinin implementation detayına bağımlı
  olamaz. Paylaşım yalnız shared contract / platform capability üzerinden.
- **Sekme = feature/product surface:** kendi navigation entry'si, UI
  composition'ı, application state'i, workflow'u olabilir.
- Aşırı mühendislik YASAK (`IPlugin`, `IFeatureRegistry`, `IFeatureLifecycle`
  vb. üretilmeyecek; prensip + klasör yeterli).

Hedef klasör yapısı (henüz uygulanmadı — mevcut yapı §7, geçiş roadmap'te):

```
Features/
    VoiceTodo/
        Domain/  Application/  Infrastructure/  UI/
    (Translator/ vb. ileride — şimdi açılmıyor)
Platform/
    DesignSystem/  Audio/  Speech/  Storage/  Permissions/  Accessibility/
```

Aynı platform yeteneklerini farklı workflow'larla kullanan iki örnek:

```
Voice Todo:                          Live Translation (ileride):
  Audio → Transcribe → Command        Audio A → Transcribe → Detect Language
       → Handler → Todo                   → Translate → TTS → Audio B
```

Her ikisi de platformun `Audio`, `Speech`, `TTS`, `DesignSystem`, `Motion`,
`Permissions` yeteneklerini tüketir; hiçbiri diğerinin detayını bilmez.

### 1.2 Application State → UI State → Visual Language

State üç katmanda akar; UI state'i kendi türetmez, Application'dan alır:

```
Platform
   ↓
Feature (Application State)
   ↓  VoiceFlowState (contract)
UI State
   ↓
Visual Language (Liquid Glass + motion + component'ler)
```

Örnek: `VoiceFlowState` → `Listening` → cam panel + breathing ring + waveform.
Başka bir feature kendi `TranslationFlowState`'i ile aynı Liquid Glass sistemini
farklı bir hikâyeyle kullanabilir.

---

## 2. Teknoloji Yığını

| Bileşen | Karar | Neden |
|---------|-------|-------|
| Framework | .NET MAUI 8 (`MauiVersion=8.0.100`) | Tek codebase, Windows native |
| Hedef platform | `net8.0-windows10.0.19041.0` (yalnız Windows) | Şu an geliştirme hedefi Windows |
| MVVM | CommunityToolkit.Mvvm 8.3.2 (`[ObservableProperty]`, `[RelayCommand]`) | Az boilerplate, source generator |
| UI toolkit | CommunityToolkit.Maui 9.1.0 | Hazır konvertör / behavior desteği |
| Ses | Plugin.Maui.Audio 3.0.1 | Kayıt + oynatma, `IAudioPlayer.Duration/CurrentPosition` `double?` (saniye) |
| Transkripsiyon | **Whisper.net** (çevrimdışı, MIT) + **bulut STT seçenekleri** | 4 katmanlı yerel model (190MB→3,1GB) + OpenAI/Groq/Deepgram/ElevenLabs (API anahtarı ile, `ISpeechTranscriber`); kayıt → çevir akışı. Windows `SpeechRecognizer` unpackaged'ta çalışmaz (ADR-016) |
| Local DB | sqlite-net-pcl 1.9.172 | Basit, thread-safe, offline |
| Backend | Supabase (Auth, Postgres, Storage, Edge Functions) | Hosted + RLS + storage |
| HTTP | `System.Net.Http.HttpClient` | **Supabase SDK iki kere URL prefix'liyor (bug); doğrudan HttpClient kullanıyoruz** |

---

## 3. Kritik Teknik Kararlar (ADR)

### ADR-001: Supabase SDK yerine doğrudan HttpClient
`SupabaseService` (`InvokeFunctionAsync`) Supabase C# SDK'sını kullanmaz; Edge
Function çağrılarını doğrudan `HttpClient` ile yapar. SDK'nın URL'i iki kere
prefix'leme bug'ı vardı (`HIGH`, kodda doğrulandı).

### ADR-002: Local-first veri akışı
Tüm yazma işlemleri önce SQLite'a yazılır (`NeedsSync` bayrağı), sonra online
ise Supabase'e anlık sync denenir. Sync başarısızsa bayrak `true` kalır ve
periyodik sync (5 dk) dener. `SyncService` orchestrator'dür.

### ADR-003: Login'siz prototip akışı
Kullanıcı kararı: "basitleştir, login olmadan çalışsın." `App.InitializeAsync`
login sayfasına yönlendirmez; doğrudan `AppShell` açılır. `SyncService` login
yokken `local-user` fallback user id kullanır. Supabase erişimi denendiğinde
sessizce düşer, çökme olmaz.

### ADR-004: Gerçek zamanlı transkripsiyon (canlı metin)
`SpeechToTextService` tek atış `RecognizeAsync` yerine
`ContinuousRecognitionSession` kullanır. `ResultGenerated` event'i ara
sonuçları `LiveTranscript` olarak yayınlar; `Completed` event'i final metni
`TranscriptionCompleted` olarak fırlatır. Bu, "konuşurken metin görünür"
deneyimini verir (modern AI ses modları gibi).

### ADR-005: Tema MAUI 8 kısıtı
MAUI 8.0.100'de `AppThemeColor` ve `Color` içinde `OnTheme` DESTEKLENMEZ
(XamlC XFC0000). Tema farkındalığı yalnızca `AppThemeBinding Light={StaticResource X}, Dark={StaticResource DarkX}`
ile yapılır. Koyu tema renkleri `Colors.xaml`'da `Dark*` anahtarları olarak ayrı
tanımlıdır (`HIGH`, derleme hatasıyla doğrulandı).

### ADR-006: Reminder Windows toast
`ReminderService` 15 sn'de bir SQLite'da `reminder_at <= now && !fired` kayıtları
tarar ve `Windows.UI.Notifications.ToastNotificationManager` ile bildirim gösterir.

### ADR-007: Mock server (server/)
`server/` altında Node tabanlı JSON mock API (todos, users, profiles, voice)
geliştirme dönemi için tutulur. `server/data/` dosyaları `.gitignore`'dadır.

### ADR-008: Feature-as-an-App (modüler tab mimarisi)
Her sekme "ekran" değil, platform içinde bağımsız feature'dır. Feature kendi
state/logic/UI'sine sahiptir; ortak yetenekler platformda tek kopyadır.
Feature'lar birbirinin implementation detayına bağımlı olamaz. Ayrıntı §1.1.

### ADR-009: Voice seam — VoiceCommand → Handler → Domain operation
Voice, domain modelini doğrudan değiştirmez. `VoiceCommand` üretir; Application
handler yorumlar; Domain operation'ı çağırır. **UnknownIntent birinci sınıf**
(parser ürün kararı vermez; Application policy karar verir — v1:
`CreateTodo(transcript)` fallback). `TranscriptionResult { Text, Confidence,
Alternates, Provider }` — provider-neutral, adapter'larla beslenir. Üçlü
komut katmanı YOK (tek input contract).

### ADR-010: Sync envelope — dirty + tombstone + local_version
`NeedsSync` boolean'ı tek başına yetersiz (create→edit→delete sırası). Model:
per-entity envelope `dirty, deleted/tombstone, local_version`. Entity içi
coalescing serbest (son state önemli). Tombstone zorunlu (silme temsili).
Event outbox / CRDT / event-sourcing YOK — gerçek ihtiyaç gelirse düşünülür.

### ADR-011: local_version ≠ server_version
`local_version` client-local revision'dır; küresel conflict çözümü değildir.
Conflict çözümü server-side LWW (`updated_at`/`version`). Client tarafı koruma
kuralı: **local dirty asla sessizce silinmez** — sync indirirken `NeedsSync`
ise local korunur, önce push edilir (client protection ≠ conflict resolution).

### ADR-012: Abstraction politikası
Gerçek değişim noktası veya test ihtiyacı yoksa abstraction yok. İstisnasız
değerli olan tekler: `ISpeechTranscriber` (Windows→Azure/Whisper),
`IVoiceCommandParser` (rule→LLM), `ITodoStore` (SQLite→in-memory test).
`SupabaseService`'e interface YOK — Application onu bilmez, `ITodoStore`/sync
abstraction'ının arkasında altta kalır.

Uygulandı (C0 mini-slice): `ITodoStore` = `DatabaseService`; `SyncService`
remote facade olarak SupabaseService'i sarmalar; ViewModel'ler yalnızca
`SyncService` + `ITodoStore` görür.

### ADR-013: VoiceFlowState = Application ↔ UI sözleşmesi
`Idle → Listening → Processing → Recognized → Failed`. UI bu state'i render
eder, kendi türetmez. Visual language (Liquid Glass + motion) bu state'in
üzerine kurulur. (§1.2)

### ADR-014: Todo üç şapka ayrımı
`Todo` üç sorumluluğu tek sınıfta birleştirmemeli:
- **Domain:** saf `Todo` (iş kuralı, JSON attribute'suz, UI property'siz)
- **Persistence:** `LocalTodo` (SQLite eşleşmesi) + **transport:** `TodoDto`
  (Supabase wire — snake_case `[JsonProperty]`)
- **UI sunum:** `TodoListItem` (ikon/biçimlendirme — PriorityIcon, StatusIcon,
  FormattedDuration burada)

Uygulandı (B2): `Core/Domain/Entities/Todo.cs`, `Models/TodoDto.cs`,
`Models/TodoListItem.cs`. `Models/Todo.cs` kaldırıldı.

### ADR-016: Unpackaged'ta Windows SpeechRecognizer YASAK → Whisper + bulut STT
`Windows.Media.SpeechRecognition.SpeechRecognizer` WinRT paket kimliği gerektirir.
Uygulama unpackaged WinUI 3 (`WindowsPackageType=None`) olduğundan, SpeechRecognizer
her koşulda `0x800455A0 Internal Speech Error` ile başarısız olur (app.log'da kanıtlandı).
Çözüm: **Whisper.net** (whisper.cpp, MIT, çevrimdışı) — 4 katmanlı yerel model
(Maximum 3,1GB `large-v3` Türkçe dahil 680.000+ saat veriyle eğitildi) + kullanıcı
seçimli **bulut STT kaynakları**: OpenAI/Groq/Deepgram/ElevenLabs (API anahtarı ile,
`ISpeechTranscriber` soyutlaması — ADR-012 seam'i). Bulut başarısızsa otomatik
çevrimdışı fallback. Google/Azure/AssemblyAI/Fireworks/Cloudflare/Soniox katalogda
"yakında".

### ADR-015: Voice Core domain-agnostik
`Core/Application/Voice` içinde uygulama (ör. Todo) kavramı geçmez.
`VoiceIntent` generic'tir (`Create`, `Complete`, `SetReminder`); intent→Todo
eşlemesi Todo adaptasyon katmanında yapılır (`TodoVoiceCommandHandler`).
Bağımlılık yönü **Todo → Voice**, asla tersi. `IApplication`/`Registry`/
`Manifest`/generic command bus gibi gelecek-uygulama framework'leri şu an
**yazılmaz**; ikinci gerçek uygulama geldiğinde gerçek kullanımdan genelleştirilir.

---

## 4. Build Ortamı (Windows — KRİTİK)

Bu projede `dotnet build` çağrılmadan ÖNCE `MSBuildSDKsPath` ortam değişkeni
temizlenmelidir. Aksi halde restore/build hataları oluşur.

```powershell
Remove-Item Env:\MSBuildSDKsPath -ErrorAction SilentlyContinue
dotnet build TodoVoiceMaui\TodoVoiceMaui.csproj -c Debug `
  -f net8.0-windows10.0.19041.0 `
  -p:WindowsPackageType=None -p:WindowsAppSDKSelfContained=true `
  -p:MauiVersion=8.0.100 --nologo -v q
```

Tam temiz rebuild: `-t:Rebuild` eklenir.

- **Çalışan exe:** `TodoVoiceMaui\bin\Debug\net8.0-windows10.0.19041.0\win10-x64\TodoVoiceMaui.exe`
- **Log:** exe ile aynı klasörde `app.log` (App.InitializeAsync hataları buraya yazılır)

### csproj kalıcı ayarlar (geçici, dikkat)
- `WindowsPackageType=None`
- `WindowsAppSDKSelfContained=true`
- `<MauiVersion>8.0.100</MauiVersion>`
- `DefineConstants` içinde `DISABLE_XAML_GENERATED_MAIN`
- `global.json` → SDK `8.0.422` pinli

---

## 5. Veri Katmanı

### SQLite (DatabaseService : ITodoStore)
Tablolar (Local* sınıfları):
- `LocalTodo` — id, user_id, title, description, completed, priority, due_date,
  reminder_at, voice_recording_url, voice_duration, needs_sync, is_deleted,
  local_version, created/updated_at
- `LocalVoiceRecording` — id, todo_id, user_id, file_url, file_name, file_size,
  duration, mime_type, created_at, needs_sync
- `LocalUserProfile` — id, email, full_name, avatar_url, preferences_json
- Sync durumu ayrı tabloda tutulur.

**Sync envelope (ADR-010/011):** `NeedsSync` (dirty) + `IsDeleted` (tombstone) +
`LocalVersion` (client-local revision). Silme önce tombstone işaretlenir; server
DELETE onaylarsa purge edilir. Offline silme kaybolmaz. `GetTodosAsync` tombstone
kayıtları gizler (UI listesi temiz); `GetPendingTodosAsync` dahil eder (sync).

Migration: `MigrateAsync` PRAGMA + `ALTER TABLE` ile `reminder_at`, `is_deleted`,
`local_version` sütunları mevcut DB'lere eklenir (mevcut veriyi korur).

### Supabase (SupabaseService)
- Auth: `SignInAsync`, `SignUpAsync`, `SignOutAsync`, `GetCurrentUser`
- Edge function'lar doğrudan HttpClient ile:
  - `todo-manager` (CRUD + `reminder_at` destekli)
  - `user-profile` (profil + istatistik)
  - `voice-upload` (base64 ses yükleme)
- Storage: `voice-recordings` bucket
- **ADR-012:** ViewModel'ler SupabaseService'i GÖRMEZ. Remote erişim `SyncService`
  facade'ı üzerinden; local erişim `ITodoStore` üzerinden.

### Sync (SyncService)
- `SyncAllAsync`: profil → ses yükleme → todo push → todo pull (4 adım)
- Push önce, pull sonra (ADR-011: local dirty asla sessizce silinmez).
  Pull'da `NeedsSync || IsDeleted` ise local korunur.
- `IsOnline` / `LastSyncTime` / `IsSyncing` PropertyChanged ile UI'a yansır
- Connectivity değişince (çevrimdışı→online) otomatik `SyncAllAsync`
- `UpdateTodoAsync` reflect'te `BindingFlags.IgnoreCase` kullanır
  (case-sensitive bug düzeltildi)

---

## 6. Ses ve Transkripsiyon

### AudioService
- `StartRecordingAsync` / `StopRecordingAsync` (WAV, 16-bit 44.1kHz)
- `PlayRecordingFromUrlAsync` — URL'den stream playback
- `PlaybackPosition` / `PlaybackDuration` (`double?` saniye, `TimeSpan` çevrimi)
- `PlaybackPositionUpdated` event'i ile canlı progress

### SpeechToTextService (kaynak yönlendirmeli — kaydet → çevir)
- `IsAvailable` — Windows'ta her zaman true (whisper.cpp native pakette gelir)
- `SelectedProvider` / `SwitchProvider` — Ayarlar'dan kaynak seçilir (çevrimdışı veya
  bulut); seçim Preferences `stt_provider` (varsayılan `offline`)
- `TranscribeFileAsync(wavPath)` — seçili kaynak bulut + anahtar tanımlıysa bulut
  dener; boş metin VEYA hata → **otomatik çevrimdışı fallback** (`TranscribeOfflineAsync`)
- `SelectedModel` / `SwitchModelAsync` — çevrimdışı 4 katmandan modeli indirir; eski
  model yalnızca yeni model HAZIR olunca temizlenir, geçiş başarısızsa geri alınır.
  Factory `_factoryLock` altında dispose edilir; transkripsiyon aynı kilit içinde
  çalışır (yarış yok). Seçim Preferences `stt_model` (varsayılan small-q5_1).
- Bulut sağlayıcılar `ISpeechTranscriber` sözleşmesiyle (`CloudTranscribers`):
  OpenAI + Groq aynı OpenAI-compatible sınıf (farklı base URL), Deepgram raw WAV,
  ElevenLabs Scribe v2 multipart. Anahtarlar Preferences `stt_apikey_{id}`
- `TestProviderConnectionAsync` — Ayarlar'daki "Bağlantıyı Test Et" (HTTP 2xx = geçerli)
- Akış (TodoListPageViewModel): `StartSpeechToTextAsync` → `AudioService` ile kayıt →
  `StopSpeechToTextAsync` → `TranscribeFileAsync` → `VoiceCommandParser` → görev
- Klasik "konuşurken canlı metin" (ContinuousRecognitionSession) bu kurulumda yok;
  segment tabanlı anlık akış ileride chunked whisper ile eklenebilir (roadmap)
- `#if WINDOWS` guard'ı korunur; Windows dışı build'de false döner

---

## 7. Sayfa / ViewModel Haritası

| Sayfa | ViewModel | Rol |
|-------|-----------|-----|
| `AppShell` | — | TabBar: Ana Sayfa / Görevler / Ayarlar |
| `MainPage` | `MainPageViewModel` | İstatistik dashboard + kısayollar |
| `TodoListPage` | `TodoListPageViewModel` | Görev listesi + voice-first görev ekleme |
| `TodoDetailPage` | `TodoDetailPageViewModel` | Detay, düzenleme, ses kaydı/oynatma |
| `LoginPage` | `LoginPageViewModel` | Giriş/kayıt (şu an varsayılan akışta atlanıyor) |
| `SettingsPage` | `SettingsPageViewModel` | Profil, tercihler, sync, çıkış |

Navigasyon: Shell tab + `Routing.RegisterRoute(nameof(TodoDetailPage))`.

> Hedef: bu sayfalar `Features/VoiceTodo/UI` içinde feature'a bağlanacak;
> platform ortak yetenekleri `Platform/` altında. Geçiş roadmap'te.

---

## 8. DI Kayıtları (MauiProgram.cs)

Singleton: `IAudioManager`, `SupabaseService`, `AudioService`, `ITodoStore`
(`DatabaseService`), `SyncService`, `ReminderService`, `SpeechToTextService`,
`MainPage`, `MainPageViewModel`.
Transient: `LoginPageViewModel`, `TodoListPageViewModel`, `TodoDetailPageViewModel`,
`SettingsPageViewModel` + ilgili sayfalar.
`AddHttpClient()` kayıtlı.

Seam abstraction'ları (uygulandı): `ITodoStore` (local), `IVoiceCommandParser`,
`ISpeechTranscriber`, `IVoiceCommandHandler` DI'ya bağlanır (ADR-012).

---

## 9. Güvenlik Notları

- Supabase anon key koddadır (public client için normaldir); **service role key
  ve JWT secret asla koda / dokümantasyona yazılmaz.**
- Test token'ları repo DIŞINDA tutulur (`C:\temp\opencode\test-creds.txt`).
- Mikrofon izni Windows tarafından istenir; unpackaged (`WindowsPackageType=None`)
  uygulamada OS privacy ayarı üzerinden çalışır.
