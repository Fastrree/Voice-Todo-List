# architecture.md — Todo Voice Mimari ve Kritik Kararlar

Bu dosya, sistemin güncel mimarisini ve kritik kararları (nedenleriyle) içerir.
Mimari veya önemli bir teknik karar değiştiğinde güncellenir.

İlk kez çalışan bir ajan önce `AGENT.md` (kurallar), sonra bu dosyayı okur.

---

## 1. Genel Bakış

Todo Voice bir **.NET MAUI 8** Windows masaüstü uygulamasıdır. MVVM deseni
kullanır. Veri **local-first** olarak SQLite'a yazılır; çevrimiçi olduğunda
Supabase'e senkronize edilir.

```
Views (XAML) ──► ViewModels (CommunityToolkit.Mvvm) ──► Services
                                                           │
                        ┌──────────────────────────────────┤
                        ▼                                  ▼
              DatabaseService (SQLite)           SupabaseService (HTTP)
              SyncService (orchestrator)         AudioService (ses kayıt/oynatma)
              ReminderService (Windows toast)    SpeechToTextService (canlı transkripsiyon)
```

---

## 2. Teknoloji Yığını

| Bileşen | Karar | Neden |
|---------|-------|-------|
| Framework | .NET MAUI 8 (`MauiVersion=8.0.100`) | Tek codebase, Windows native |
| Hedef platform | `net8.0-windows10.0.19041.0` (yalnız Windows) | Şu an geliştirme hedefi Windows |
| MVVM | CommunityToolkit.Mvvm 8.3.2 (`[ObservableProperty]`, `[RelayCommand]`) | Az boilerplate, source generator |
| UI toolkit | CommunityToolkit.Maui 9.1.0 | Hazır konvertör / behavior desteği |
| Ses | Plugin.Maui.Audio 3.0.1 | Kayıt + oynatma, `IAudioPlayer.Duration/CurrentPosition` `double?` (saniye) |
| Transkripsiyon | Windows `SpeechRecognizer` (WASDK) | `ContinuousRecognitionSession` ile canlı (ara) sonuçlar |
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

### SQLite (DatabaseService)
Tablolar (Local* sınıfları):
- `LocalTodo` — id, user_id, title, description, completed, priority, due_date,
  reminder_at, voice_recording_url, voice_duration, needs_sync, created/updated_at
- `LocalVoiceRecording` — id, todo_id, user_id, file_url, file_name, file_size,
  duration, mime_type, created_at, needs_sync
- `LocalUserProfile` — id, email, full_name, avatar_url, preferences_json
- Sync durumu ayrı tabloda tutulur.

Migration: `MigrateAsync` PRAGMA + `ALTER TABLE` ile `reminder_at` sütunu
mevcut DB'lere eklenir (mevcut veriyi korur).

### Supabase (SupabaseService)
- Auth: `SignInAsync`, `SignUpAsync`, `SignOutAsync`, `GetCurrentUser`
- Edge function'lar doğrudan HttpClient ile:
  - `todo-manager` (CRUD + `reminder_at` destekli)
  - `user-profile` (profil + istatistik)
  - `voice-upload` (base64 ses yükleme)
- Storage: `voice-recordings` bucket

### Sync (SyncService)
- `SyncAllAsync`: profil → ses yükleme → todo push → todo pull (4 adım)
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

### SpeechToTextService (canlı transkripsiyon)
- `IsAvailable` — Windows'ta SpeechRecognizer oluşturulabilir mi?
- `StartListeningAsync` — `ContinuousRecognitionSession.StartAsync`
- `LiveTranscript` — `ResultGenerated` ara sonuçlar (PropertyChanged)
- `TranscriptionCompleted` — `Completed` event'inde final metin
- `StopListeningAsync` / `StopListening` — durdur + `CleanupRecognizer`
- Constructor'da `#if WINDOWS` guard'ı; Windows dışı build'de false döner

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

---

## 8. DI Kayıtları (MauiProgram.cs)

Singleton: `IAudioManager`, `SupabaseService`, `AudioService`, `DatabaseService`,
`SyncService`, `ReminderService`, `SpeechToTextService`, `MainPage`,
`MainPageViewModel`.
Transient: `LoginPageViewModel`, `TodoListPageViewModel`, `TodoDetailPageViewModel`,
`SettingsPageViewModel` + ilgili sayfalar.
`AddHttpClient()` kayıtlı.

---

## 9. Güvenlik Notları

- Supabase anon key koddadır (public client için normaldir); **service role key
  ve JWT secret asla koda / dokümantasyona yazılmaz.**
- Test token'ları repo DIŞINDA tutulur (`C:\temp\opencode\test-creds.txt`).
- Mikrofon izni Windows tarafından istenir; unpackaged (`WindowsPackageType=None`)
  uygulamada OS privacy ayarı üzerinden çalışır.
