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
**Mimari:** MVVM (CommunityToolkit.Mvvm) + Services + Supabase Edge Functions
**Çalıştırma platformu (şu an):** `net8.0-windows10.0.19041.0` (yalnızca Windows)

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
  olmamalı.
- **Tema değiştiyse:** Açık + koyu tema ikisi de ayrı ayrı doğrulanmalı.

Eski feature'ın bozulmadığını KANITLAMADAN görev tamamlanmış sayılmaz.
"Kontrol et" demiyorum: kanıtla.

Yeni kod yazmadan önce mevcut davranışı oku; yeni kod bittikten sonra aynı
davranışın hâlâ korunduğunu doğrula.

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
