# AGENT.md — Todo Voice AI Anayasası

Bu dosya, projede çalışan HER yapay zeka ajanı (opencode, Claude Code, Codex CLI,
Cline, Cursor, Roo Code vb.) için bağlayıcı kuralları içerir.

Her göreve başlamadan önce bu dosyayı oku. Her görevi bitirmeden önce bu
dosyanın kurallarına uyduğunu doğrula.

Dokümantasyon haritası için `INDEX.md` dosyasına bak.
Tasarım / görsel / deneyim ile ilgili bir görev aldığında `design-system.md`
dosyasını oku.
Geçiş / animasyon sistemiyle ilgili bir görev aldığında `transition-framework.md`
dosyasını oku.

---

## 1. Proje Tanımı

**Proje:** Todo Voice — Sesli görev yönetimi (voice-first todo listesi)
**Kapsam:** Kullanıcı mikrofonuyla konuşur, uygulama söyleneni gerçek zamanlı
transkripsiyon ile metne çevirir ve görev olarak oluşturur. "Mikrofon → Konuş →
Canlı metin → Görev" akışı ürünün kalbidir.
**Teknolojiler:** C# / .NET MAUI 8 (Windows desktop), CommunityToolkit.Mvvm,
Plugin.Maui.Audio, SQLite (sqlite-net-pcl), Supabase (Auth / Postgres / Edge
Functions / Storage)
**Mimari:** MVVM (CommunityToolkit.Mvvm) + **Feature-as-an-App** (Platform +
bağımsız feature'lar) + Services + Supabase Edge Functions
**Çalıştırma platformu (şu an):** `net8.0-windows10.0.19041.0` (yalnızca Windows)
**Mimari kararlar:** bağlayıcı 10 karar için `AGENT.md` §10.1'e bak.

---

## 2. Kural Önceliği

Çelişki olursa aşağıdaki sıra geçerlidir. Alt sıradaki hiçbir kural üst sıradaki
kuralı geçersiz kılamaz.

1. Kullanıcının son talimatı
2. AGENT.md
3. architecture.md
4. design-system.md
5. learning.md
6. roadmap.md

---

## 3. Kaynak Gerçeklik Kuralı

Yalnızca doğrulanmış bilgiyi rapor et. ASLA:

- Var olmayan dosyayı varmış gibi gösterme.
- Çalıştırmadığın testi çalıştırılmış gibi raporlama.
- Okumadığın kodu okunmuş gibi anlatma.
- Uygulanmamış özelliği uygulanmış gibi yazma.

Bir şeyi doğrulamadıysan, "doğrulanmadı" de. Tahminini gerçek gibi sunma.

---

## 4. Güven Seviyesi

Her önemli iddiaya bir güven seviyesi ekle:

- `HIGH`   — Kodu okudum / testi çalıştırdım / doğruladım.
- `MEDIUM` — Güçlü kanıt var ama tam doğrulamadım.
- `LOW`    — Tahmin / çıkarım.
- `UNKNOWN`— Bilgim yok.

Kod incelenmemiş veya doğrulanmamışsa `HIGH` KULLANMA.

---

## 5. Karar Yetkisi

Kullanıcı onayı OLMADAN yapabilirsin:

- Hata düzeltmeleri
- Refactor
- Dokümantasyon güncellemeleri
- Kod temizliği

Kullanıcı onayı GEREKTİRİR:

- Özellik kaldırma
- Mimari değişiklik
- Klasör yapısını değiştirme
- Teknoloji / bağımlılık değişikliği
- Dosya taşıma veya silme
- Sürüm (tag) oluşturma
- Yeni entity / tablo ekleme
- **Başka projeye dokunma** (özellikle `C:\Users\sniya\Desktop\chicky-pos`
  asla değiştirilmez; referans amaçlı kullanılabilir, yazma amacıyla ASLA)

---

## 6. Görev İş Akışı

Her görevi bu sırayla yürüt:

1. `learning.md` oku (şu an ne çalışıyor?).
2. `architecture.md` oku (sistem nasıl kurulu, hangi kararlar alındı?).
3. Gerekliyse `roadmap.md` oku.
4. Tasarım / deneyim işiyse `design-system.md` oku.
5. Problemi analiz et, çözüm planını çıkar.
6. Kullanıcı onayı gerekip gerekmediğini belirle (bkz. Madde 5).
7. Değişikliği uygula.
8. Mümkünse doğrula (build / test / çalıştır).
9. Etkilenen dokümanları güncelle (`learning.md`, `architecture.md`, varsa `README.md`).
10. Yapılanı, güven seviyeleriyle birlikte raporla.

Doğrulanmamış hiçbir iş "tamamlandı" sayılmaz.

---

## 6.1 Regression Guard (Zorunlu Tamamlama Kapısı)

Feature tamamlandıktan sonra KOD YAZMAYI BIRAK ve şu 4 soruyu cevapla:

1. **Bu değişiklik eski çalışan hangi sistemi etkiliyor?**
   (Ses kaydı? Transkripsiyon? Sync? Filtre? Tema? Navigasyon? Listele.)
2. **En kolay bozabileceğin yer neresi?** Tek bir yer seç, o yeri yeniden oku.
3. **Kendi kodunda en az 1 olası bug bul.** Bug arayan kişi gibi davran:
   "Kendi yazdığım kod neden çalışmayabilir?"
4. **Kullanıcının ilk yapacağı işlemi zihinsel olarak test et.** Kod bitmeden
   önce dene.

Bu proje bir Windows MAUI masaüstü uygulamasıdır. Özel doğrulama noktaları:

- **XAML değiştiyse:** `dotnet build` hatasız olmalı; `AppThemeBinding` ve
  StaticResource referanslarının hepsi mevcut anahtarları kullanmalı.
  **MAUI 8.0.100'de `AppThemeColor` ve `Color` içinde `OnTheme` DESTEKLENMEZ**
  (XamlC XFC0000). Tema farkındalığı için `AppThemeBinding` kullan.
- **Ses / transkripsiyon değiştiyse:** Kayıt → durdur → oynat ve
  mikrofon → konuş → canlı metin → görev oluşma akışı birlikte doğrulanmalı.
  `SpeechToTextService` önce Dispose sonra tekrar başlatma döngüsünü kırmamalı.
- **Sync / veri değiştiyse:** Local-first çalışma bozulmamalı; login yokken de
  uygulama çalışabilmeli (`local-user` fallback). Supabase ayakta değilken çökme
  olmamalı. **Local dirty veri asla sessizce silinmemeli** (Karar 4).
- **Tema değiştiyse:** Açık + koyu tema ikisi de ayrı ayrı doğrulanmalı.
- **Feature/sınır değiştiyse:** Feature isolation (Madde 10.1.1) korunmalı —
  `FeatureA → FeatureB/Services` bağımlılığı, `Voice → Domain model`
  bağımlılığı (Karar 2) eklenmemiş olmalı.

Eski feature'ın bozulmadığını KANITLAMADAN görev tamamlanmış sayılmaz.
"Kontrol et" demiyorum: kanıtla.

Yeni kod yazmadan önce mevcut davranışı oku; yeni kod bittikten sonra aynı
davranışın hâlâ korunduğunu doğrula.

---

## 6.2 Audit → Drift Detection → Mini-Slice

Audit sadece mimariyi "incelemek" değildir: mevcut implementasyonun dokümante
edilmiş mimariyle uyuşup uyuşmadığını sistematik olarak kontrol etmektir
(drift detection). Dokümantasyon (architecture.md, ADR'ler, AGENT.md bağlayıcı
kararları) ile gerçek kod arasında sapma bulunduğunda:

```
Dokümante edilmiş karar → Audit → Sapma/bug → Mini-slice → Build + doğrulama
                                        → Dokümantasyonu güncelle
```

- **Tetikleyici:** mimari veya sınır ihlali (ADR ihlali, abstraction eksikliği,
  veri integrity riski) fark edildiğinde, uygulamaya devam etmeden ÖNCE kısa
  bir audit yapılır. Her küçük bug için tam audit yapılmaz.
- **Mini-slice kapsamı:** sapma en küçük uygulanabilir düzeltme olarak
  gerçekleştirilir (ör. `ITodoStore` + implementasyon + ViewModel yönlendirme +
  build). Gereksiz genişletme YASAK.
- **Bitirme:** build/doğrulama başarılı olmalı; etkilenen doküman
  (architecture.md / ADR / learning.md) sapmanın giderildiği şekilde güncellenir.
  Kararın kendisi değiştiyse ADR güncellenir (kullanıcı onayı gerekebilir,
  Madde 5).

Bu akış, mevcut çalışma tarzını resmileştirir; yeni framework veya seremoni
yaratmaz.

---

## 7. Sürüm ve Geçmiş (Git)

Bu proje git kullanır. Sürüm ve geçmiş boyutunu GİT taşır — elle dosya
kopyalayarak değil.

- Sürümler git tag'idir: `v1.0.0`, `v2.0.0`. Kod klasör kopyalanarak
  çoğaltılmaz.
- Değişiklik günlüğü = `git log`. Elle timestamp'li günlük dosyası tutma.
- Geri alma = `git revert`. Büyük değişiklikten önce çalışan hali commit'lediğinden
  emin ol.

Yeni tag (sürüm) oluşturmak kullanıcı onayı gerektirir (Madde 5).

---

## 8. Commit Kuralı

- Küçük ve anlamlı commit'ler yap.
- Commit mesajı ne yapıldığını açık anlatsın.
- Bir commit tek bir mantıksal değişiklik içersin.
- Çalışmayan / build etmeyen kodu ana dala commit'leme; edersen mesajda belirt.
- Commit'ler kullanıcı istemediği sürece otomatik atılmaz.

---

## 9. Dokümantasyon Sorumluluğu

| Dosya | İçerik | Ne zaman güncellenir |
|-------|--------|----------------------|
| `learning.md` | Şu an ne çalışıyor (yalnız aktif özellikler) | Özellik eklen/kaldırıldığında |
| `architecture.md` | Güncel mimari + kritik kararlar (neden) | Mimari veya önemli teknik karar değiştiğinde |
| `roadmap.md` | Gelecek planı (todo listesi) | Plan değiştiğinde |
| `design-system.md` | Tasarım sistemi, palet, tipografi, efekt kuralları | Tasarım kararı değiştiğinde |
| `transition-framework.md` | Animasyon / geçiş altyapısı | Animasyon sistemi değiştiğinde |

`learning.md` geçmiş TUTMAZ — sadece bugünü anlatır. Kaldırılan özellik buradan
çıkarılır (tarihçesi git'te kalır).

**Proje kökündeki `README.md` de güncel tutulur** — özellik listesi, mimari
özeti ve çalıştırma komutları projenin gerçek durumunu yansıtmalı. README'de
uygulanmamış özellik reklamı yapma (Kaynak Gerçeklik Kuralı, Madde 3).

---

## 10. Mimari İlkeler

- **Voice-first.** Ürünün kalbi sesli görev oluşturma akışıdır: mikrofona bas →
  konuş → canlı transkripsiyon → görev oluşur.
- **Local-first.** Veri önce SQLite'a yazılır; online ise Supabase'e senkronize
  edilir. Çevrimdışıyken uygulama tam çalışır.
- **UI karar vermez.** İş mantığı ViewModel / Service katmanında olur.
- **Tema token tabanlıdır.** Renkler XAML'da `AppThemeBinding` ile token'dan
  gelir; ham hex değeri doğrudan component'lerde kullanılmaz.
- **Önce çalışan sistem, sonra polish.** Ama görsel kalite bu ürünün satış
  noktasıdır — "çalışıyor ama çirkin" kabul edilemez.
- **Performans şart.** Cam efektleri, gölgeler ve animasyonlar ölçülü kullanılır;
  60fps'in altına düşüren görsel abartıdan kaçınılır.

---

## 10.1 Mimari Baseline — Bağlayıcı 10 Karar

Aşağıdaki kararlar tartışmaya kapalıdır — **bağlayıcı mimari prensiplerdir.**
Her yeni kod bu kararlara uygun yazılır; bir karar değişecekse kullanıcı onayı
gerekir (Madde 5). Detaylar ve nedenleri `architecture.md`'de.

1. **Saf Domain modeli.** `Todo` UI sunumu ve JSON/JSON'dan ayrı saf bir domain
   modelidir. UI'a özel sunum (ikon, biçimlendirme) ayrı bir tipte yaşar
   (ör. `TodoListItem`). `architecture.md` §Todo üç şapka.
2. **VoiceCommand → Handler → Domain operation.** Voice asla domain modelini
   doğrudan değiştirmez. Voice üretir `VoiceCommand`; Application handler
   yorumlar; Domain operation'ı çağırır. Üçlü komut katmanı yok — tek input
   contract.
2a. **Voice Core Todo'yu bilmez.** `Core/Application/Voice` içinde Todo kavramı
   geçmez; `VoiceIntent` generic'tir (`Create`, `Complete`, `SetReminder`).
   Intent→Todo action eşlemesi Todo adaptasyon katmanında yapılır
   (`TodoVoiceCommandHandler`). Bağımlılık yönü: Todo → Voice, asla tersi.
3. **UnknownIntent birinci sınıf.** Parser ürün kararı vermez. Anlayamadığı
   giriş için `UnknownIntent` (ham transcript'i taşır) üretir; **Application
   policy** karar verir (v1: `CreateTodo(transcript)` fallback — "asla
   utterance kaybetme").
4. **Local dirty asla sessizce silinmez.** Client protection ≠ Conflict
   resolution. Sync indirirken local `NeedsSync` ise local korunur (önce push).
5. **Sync modeli: dirty + tombstone + local_version.** Boolean flag tek başına
   değil; per-entity envelope: `dirty, deleted/tombstone, local_version`.
   Event outbox / CRDT / event-sourcing yok.
6. **local_version ≠ server_version.** Client-local revision, küresel conflict
   çözümü değildir. Conflict çözümü server-side LWW (`updated_at`/`version`).
7. **Semantik design-token sözleşmesi.** Tasarım token'ları semantik adlar
   taşır (`color.surface.primary`, `space.md`, `radius.card`, `motion.page`).
   Format (JSON/XAML/CSS) ikinci tüketici gelince seçilir; sözleşme bugün
   `design-system.md`'de yazılır.
8. **Abstraction politikası:** gerçek değişim noktası veya test ihtiyacı yoksa
   abstraction yok. Interface yalnızca 2+ gerçek implementasyon / test
   edilebilirlik gerektiğinde. (İstisnasız tek değerli abstraction'lar:
   `ISpeechTranscriber`, `IVoiceCommandParser`, `ITodoStore`.)
9. **VoiceFlowState = Application ↔ UI sözleşmesi.** UI state'i kendi türetmez;
   Application'dan alır ve görsel dile çevirir
   (`Idle → Listening → Processing → Recognized → Failed` → Liquid Glass + motion).
10. **Feature-as-an-App.** Her sekme "ekran" değil, platform içinde yaşayan
    bağımsız bir uygulamadır. Feature kendi state/logic/UI'sine sahiptir; ortak
    yetenekler (Design System, Audio, Speech, Storage, Permissions, Motion)
    platform tarafından sağlanır. Feature'lar birbirinin implementation
    detayını tüketemez.

### 10.1.1 Feature-as-an-App — Modüler Tab Mimarisi

Kullanıcı kararı: **her uygulama sekmesi, aynı platform üzerinde çalışan
bağımsız bir ürün/feature olarak tasarlanabilmelidir.**

```
                    PLATFORM (ortak yetenekler)
   Design System · Audio · Speech · Storage · Permissions · Motion
                         │
        ┌────────────────┼────────────────┐
        │                │                │
  Feature A         Feature B        Feature C
  (VoiceTodo)      (Translator)     (gelecek)
  kendi state       kendi state      kendi state
  kendi workflow    kendi workflow   kendi workflow
  kendi UI          kendi UI         kendi UI
        │                │                │
        └────────────────┼────────────────┘
                  SHARED PLATFORM
```

- **Platform ≠ Feature:** Platform ortak capability sağlar; Feature kendi ürün
  davranışından sorumludur.
- **Feature isolation:** Feature'lar birbirinin implementation detayına bağımlı
  olamaz. Bir feature diğerinden bir şey kullanacaksa → shared contract /
  platform capability üzerinden. `FeatureA → FeatureB/Services` YASAK.
- **Yeni feature eklemek mevcut feature'ları yeniden tasarlamayı
  gerektirmemeli.** (Mimari stres testi: "Voice Todo kapatılıp yerine başka
  feature açılsa platform omurgası çöker mi?" → çökmemeli.)
- **Ortak capability tek kopya:** Feature'lar kendi küçük platformunu yaratmaz
  (üç ayrı AudioService olmaz); platformdan tüketir.
- **Sekme = feature/product surface:** kendi navigation entry'si, UI
  composition'ı, application state'i, error/recovery davranışı olabilir.
- **Aşırı mühendislik YASAK:** `IPlugin`, `IFeatureManifest`, `IFeatureRegistry`,
  `IFeatureLifecycle`, `IFeatureHost` vb. üretmeyiz. Prensip + klasör yapısı
  yeterli; abstraction ihtiyaç doğduğunda üretilir (Madde 8).

### 10.1.2 Future-proof ≠ future-feature-ready

Sistemi geleceğe AÇIK tasarlarız (sınır/sözleşme/seam bırakırız), ama
"belki lazım olur" diye bugünden feature/altyapı inşa etmeyiz. Gelecekteki
mimariler dokümana spekülatif not olarak da yazılmaz (kendi içinde borçtur).

---

## 11. Ortam Bilgisi (Windows)

- Build: `Remove-Item Env:\MSBuildSDKsPath` yapılmadan `dotnet build` çağrılır.
  Build komutu detayı `architecture.md`'de.
- Çalışan exe ve `app.log` yolu `architecture.md`'de.
- Lokal Supabase `http://127.0.0.1:54321` üzerinde çalışır; Supabase ayakta
  değilken uygulama çökmemelidir.

---

## 12. Güvenlik Kuralları

- Supabase servis rolü / JWT secret değerleri dökümantasyona yazılmaz; test
  token'ları `C:\temp\opencode\test-creds.txt` gibi çalışma alanı dışında tutulur.
- Şifreler hash'lenir (Supabase Auth).
- Mikrofon izni kullanıcı onayı ile istenir; ret durumunda uygulama zarif bir
  hata mesajı gösterir.
- Kişisel veriler (kayıtlı sesler) KVKK/aydınlatma metnine uygun işlenir.
