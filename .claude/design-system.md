# design-system.md — Todo Voice Tasarım Sistemi

Bu dosya uygulamanın **hedef tasarım sistemini** tanımlar (premium redesign).
Uygulanan her sayfa bu token'lara ve ilkelere uyar.

> Durum: **PLAN** — renk/tipografi kararları burada netleştirildikten sonra
> `Colors.xaml` / `Styles.xaml`'a uygulanacak. Light ve Dark tema BAĞIMSIZ
> tasarlanır (dark, light'ın koyu hali değil; ayrı bir deneyimdir).

> **Liquid Glass = Platformun görsel dili.** Feature-as-an-App mimarisinde
> (architecture.md §1.1) Liquid Glass "Voice Todo'nun tasarımı" değil,
> **tüm feature'ların** ortak görsel dilidir. Feature'lar bu dili kullanarak
> kendi deneyimlerini kurar. Token → Component → Pattern zinciri bu yüzden
> platform katmanında yaşar. (AGENT.md Karar 7 + 10)

---

## 1. Tasarım Dili (Özet)

- **Hedef:** "Liquid Glass kullanan bir Todo uygulaması" değil; **kendi görsel
  kimliği olan, cam/ışık/renk davranışıyla ilk bakışta tanınan** bir Todo
  uygulaması. Apple'ın dilini kopyalamak değil; o dilin *disiplininden* kendi
  kimliğimizi kurmak.
- **Tasarım sentezi (tek akıma kilitlenme):** güncel akımların iyi tarafları
  bilinçli bir bileşimle birleştirilir (kullanıcı kararı, 2026-08):
  - **Spatial (VisionOS tarzı)** — katmanlar ve derinlik; yüzeyler aynı düzlemde
    değil, foreground/background ayrımı net. Tüm etkileşimin iskeleti.
  - **Liquid Glass** — ışık taşıyan yarı saydam yüzeyler; uygulamanın malzemesi
    (§6). Cam kenar specular, içeriği geçirir.
  - **Aurora / Mesh Gradient** — zemin atmosferi: yumuşak, akışkan renk
    geçişleri (mavi-mor-cyan-beyaz birbirine akar). Arka planı "canlı" yapar;
    camla bütünleşir. Zeminin çoğu düşük kontrast, yalnız belirli bölgelerde
    görünür.
  - **Editorial / Swiss typography** — güçlü tipografi, büyük başlıklar, bol
    whitespace, grid, az ama bilinçli renk. **Sora** burada ana karakter.
  - **Chromatic / Iridescent accent (çok kontrollü)** — beyaz/gümüş yüzeyde
    ışığa göre cyan-violet-blue kırılması; yalnız mikrofon halkası, aktif
    vurgu, cam kenarı gibi **az sayıda noktada** sedefli his verir.
  - **Bento UI (Dashboard/MainPage)** — farklı boyutlarda modüler, asimetrik
    ama düzenli kart grid'i; büyük bilgi kartları + küçük aksiyonlar.
  - **Organic / Fluid (voice anı)** — mikrofon ve canlı transkripsiyonda keskin
    dikdörtgenler yerine yumuşak blob/flow formları; ses hikayesine doğal hareket.
- **Estetik:** "yüksek kaliteli ses aleti" — fütüristik ama sıcak. Cam (glass),
  soft gölgeler, akışkan animasyonlar, ışık taşıyan yüzeyler.
- **Hissiyat:** profesyonel, minimal ama karakterli; AI ses asistanı modernliği.
- **İlham kaynakları (kopya değil):** front-end.md'nin ayırt edici tasarım
  ilkeleri; ui-ux-pro-max'ın 50+ stil / 161 palet / 57 font çifti kataloğu;
  chicky-pos transition-framework.md'nin animasyon dili.
- **Design system kısıt koyar, yaratıcılığı kısıtlamaz.** Token'lar ve kurallar
  bir **tabandır**, tavan değil. Bir ekranı yaparken "bunu daha ilginç nasıl
  yaparım?" sorusu daima sorulur; blueprint'te yazmayan ama ürünü gerçekten
  güzelleştiren bir fikir, **kendiliğinden önerilir ve uygulanır.**
  (Kullanıcı kararı, 2026-08.)
- **Kalite kriterleri:** güzel + anlaşılır + hızlı kullanılabilir. "Fütüristik"
  uğruna kullanılabilirlik bozulmaz; erişilebilirlik, tıklanabilirlik,
  performans, MAUI 8 sınırları ve mevcut mimari korunur.
- **Savunma kuralı:** ilk tasarım iyi görünmüyorsa "token sistemine uyuyor"
  diye savunulmaz — ürünü güzelleştirecek karar değiştirilir.
- **İlk açılan ekran ve görevler ekranı** ürünün karakterini taşır; bu
  ekranlarda kimlik göstermekten çekinilmez.

---

## 2. Renk Sistemi

### 2.0 Semantik Token Sözleşmesi (Kayıt)

**AGENT.md Karar 7:** Tasarım token'ları semantik adlar taşır; format
(JSON/XAML/CSS) ikinci tüketici gelince seçilir, sözleşme bugün burada yazılır.

**İsimlendirme:** `kategori.öğe.nitelik` (dot-notation, küçük harf).
Hex değerleri asla component'lerde inline olmaz — token'dan gelir.
Dark tema ayrı kayıttır (`color.surface.primary.dark` MAUI'de ayrı anahtar).

Kayıt bugün şu sözlükle başlar (eksik kategoriler eklendikçe genişler):

```
color.surface.primary      sayfa zemini
color.surface.secondary    bölüm/alt zemin
color.surface.glass        cam panel zemin (Liquid Glass)
color.surface.elevated     yükseltilmiş katman
color.text.primary         ana metin
color.text.secondary       ikincil metin
color.text.tertiary        silik / yer tutucu
color.border.default       ince çizgi
color.border.glass         cam panel kenarı
color.accent.default       imza rengi (buton, aktif, mikrofon halkası)
color.accent.soft          accent dolgu (chip/tag)
color.accent.contrast      accent üzerindeki metin
color.state.success        görev tamam, başarı
color.state.warning        uyarı
color.state.danger         hata
space.xs  · space.sm  · space.md  · space.lg  · space.xl   4·8·12·16·24 (4pt)
radius.sm · radius.md · radius.lg · radius.pill           10·16·24·999
typography.display · title · body · caption · micro        (ölçek §3)
motion.micro · motion.element · motion.page                (150·300·600ms, §9)
shadow.sm · shadow.md · shadow.lg                          (§6)
```

Atmosfer token'ları (Malzeme Sistemi — §6; Light/Dark AYRI kayıt):

```
color.atmosphere.background    zemin gradyan tabanı (temel ton)
color.atmosphere.highlight     zemin üst tonu / ışık lekesi
color.atmosphere.tint          hafif cyan/graphite yansıma (gradyan sonu)
color.glass.background         cam yüzey (yarı saydam)
color.glass.border             cam kenar (yarı saydam beyaz/açık gri)
color.glass.specular           üst kenar ışık çizgisi
color.glass.light              cam içine sızan accent/turkuaz ışık (state/motion)
```

MAUI karşılığı: `Colors.xaml` / `Styles.xaml` bu adları birebir kullanır;
ikinci tüketici (web/CSS) gelirse aynı sözlükten üretilir.

### 2.1 Ana Palet (yeni — mevcut mavi/koyu mavi değişecek)

**İmza (accent):** Derin "mürekkep mavisi + canlı turkuaz" — ses dalgası/
mikrofon hissi. Tek 1 ana accent + 1 vurgu (secondary) yeterli; gerisi nötr.

Önerilen token'lar (her tema ayrı):

```
Accent (birincil)      # imza rengi (butonlar, aktif öğe, mikrofon halkası)
AccentSoft             # accent'in %12-18 dolgu arka planı (chip, tag)
AccentContrast         # accent üzerindeki metin/beyaz
Secondary              # ikincil vurgu (gradient eşi, durum ışığı)
Background             # sayfa zemini
Surface                # kart / cam panel zemini
SurfaceElevated        # yükseltilmiş katman
SurfaceMuted           # hafif dolgu (input, chip)
Border                 # ince çizgiler
TextPrimary            # ana metin
TextSecondary          # ikincil metin
TextTertiary           # silik / yer tutucu
Success / Warning / Danger  # durum renkleri (görev tamam, uyarı, hata)
```

- **Light tema (White / Pearl / Mist glass):** saf beyaz değil; çok açık soğuk
  nötr zemin + çok hafif cyan-gray (turkuazımsı/mavimsi) gradyan yansıması.
  Cam paneller beyaz-%40-60 yarı saydam; kontrast koyu "mürekkep" metin.
  Accent mürekkep mavisi (dark'taki parlak karşılığıyla aynı hex olmak zorunda
  değil — §6.5).
- **Dark tema (Black / Graphite / Smoked glass):** tam siyah değil; siyah →
  grafit → füme/gri gradyan zemin. Cam paneller smoked-glass (beyaz-%5-10);
  accent ışık saçar (glow). Metinler buz beyazı. "Dark = Light'in koyusu"
  değil, ayrı bir atmosfer — her bileşen her temada ayrı ele alınır (§6.5).

### 2.2 Renk Kuralları

- Renk daima token olarak (AppThemeBinding Light/Dark ayrı); **asla** ham hex inline.
- `Dark*` anahtarları ayrı tanımlanır (MAUI 8 `OnTheme` kısıtı, ADR-005).
- Gradients: yalnız accent + secondary arası (Soft/contrast hedefli).
- **Vurgu ekonomisi (kilit karar):** turkuaz (`secondary`) **vurgu olmaktan
  çıkmamalı** — yalnız interaction/state/motion'da kullanılır:
  `voice/listening/active/success`, cam içi ışık ve hareket, mikrofon halkası.
  Butonlar, başlıklar, aktif nav, ana CTA'lar **mürekkep mavisi (accent)** taşır.
  Turkuaz her yere girerse vurgu olmaktan çıkar. (Kullanıcı onayı, 2026-08.)
- **Tema-farkında kontrast:** hover/press/aktif'te arka plan değişiyorsa
  **text/icon kontrastı da beraber değişir** (yalnızca arka planı koyulaştırıp
  metni aynı renkte bırakmak yasak). Accent'in Light/Dark hex'i aynı olmak
  zorunda değildir; her tema kendi kontrastını ayrı doğrular (§6.5).

---

## 3. Tipografi

- **Gövde / UI:** modern grotesk — `Segoe UI Variable` (Windows native),
  yedek `Segoe UI`. Nefesli letter-spacing, okunaklı boyutlar.
- **Başlık / İmza (display):** tüm gövdeden ayrışan karakterli bir font
  (ör. `Poppins`, `Sora`, `Space Grotesk` — Windows'ta mevcut değilse
  MAUI `EmbeddedFont` ile gömülür veya Segoe UI Variable Display kullanılır).
- Ölçek (px, Windows DPI):
  ```
  Display 1     34 / 40  (karşılama, büyük rakamlar)
  Display 2     28 / 34  (sayfa başlığı)
  Title         22 / 28  (kart başlığı)
  Subtitle      17 / 24  (bölüm başlığı)
  Body          15 / 22  (gövde)
  Caption       13 / 18  (alt not)
  Micro         11 / 14  (etiket, rozet, ALL-CAPS)
  ```
- Numaralar (istatistik): tabular-lining (monospace rakam) → stabil hizalama.
- ALL-CAPS + geniş letter-spacing: bölüm etiketleri ve mikro butonlar.

---

## 4. Boşluk & Grid

- 4pt taban: `Space-2..64` (4, 8, 12, 16, 20, 24, 32, 48, 64).
- Sayfa: sol/sağ `24`, içerik maks genişlik (ör. 720) + merkezleme.
- Kart içi: `16`; kartlar arası: `12`; bölümler arası: `32`.

---

## 5. Şekil (Radius) & Kenarlık

- `Radius-Sm` 10 · `Radius-Md` 16 · `Radius-Lg` 24 · `Radius-Pill` 999.
- Kartlar `Radius-Lg`; butonlar `Radius-Pill` veya `Radius-Md`; input `Radius-Md`.
- Kenarlık: 1px `Border` token, cam panellerde 1px yarı saydam beyaz (light) /
  yarı saydam açık gri (dark).

---

## 6. Gölge, Işık & Cam — White Glass / Black Glass (Malzeme Sistemi)

> **Liquid Glass bir bileşen değil; uygulamanın görsel malzemesidir.**
> "Uygulamaya birkaç `GlassPanel` koymak" değil; zemin + yüzey + ışık +
> gradient + border + elevation + state'in **birlikte** oluşturduğu görsel dil.
> Cam kartlar zeminden bağımsız beyaz kutular değildir — zeminle optik olarak
> bütünleşir. (Kullanıcı kararı, 2026-08.)

### 6.1 Felsefe

- **Cam = malzeme, bileşen değil.** Aynı malzeme yoğunluğuna göre farklı
  yüzeyler üretir: zemin → hafif cam → derin cam → solid. Bileşenler bu
  malzemenin yoğunluk dereceleridir; ayrı "şeyler" değil.
- **Arka plan da camın parçasıdır.** Zemin düz solid değil; hafif katmanlı /
  gradyanlı bir atmosferdir. Cam paneller bu zeminin ışığını geçirir, zemini
  yansıtır; "beyaz kutu üstüne cam" olmaz.
- **Turkuaz = camın içine giren ışık.** Her şeyi turkuaza boyamak değil;
  belirli bölgelerde cam yüzeyine sızan bir renk yansıması / ışık tint'i
  (state, aktif, mikrofon).
- **Kontrast temaya göre yaşar.** Light ≠ Dark'in koyusu. Camın ışığı ve
  kontrastı temaya göre değişir; bir bileşenin Light'ta iyi görünmesi Dark'ta
  da otomatik iyi olacağı anlamına gelmez.

### 6.2 Atmosfer (iki ayrı dünya)

**Light — "White / Pearl / Mist":**
- Zemin: beyaz → çok açık gri → **çok hafif turkuazımsı/mavimsi (cyan-gray)**
  geçiş; büyük alanlarda çok düşük kontrastlı ışık lekeleri.
- Aurora: birkaç büyük, yumuşak mesh-gradient leke (çok açık cyan + çok açık
  violet + beyaz) zemine derinlik katar — "canlı" ama düşük kontrast.
- Cam: inci beyazı, yarı saydam (%40-60), arkasındaki gradyanı gösterir.
- Işık: yumuşak, dağınık; kenarlarda ince beyaz/specular highlight; çok hafif
  derinlik. Gölgeler sert siyah değil; **soğuk, yumuşak, dağılmış** (mavi-alt tonlu).
- Chromatic: yalnız mikrofon halkası / aktif vurgu / cam kenarı gibi **birkaç
  noktada** sedefli cyan-violet kırılması (çok kontrollü).

**Dark — "Black / Graphite / Smoked Glass":**
- Zemin: siyah → grafit → füme/gri geçişler; **tamamen düz siyah değil**.
- Aurora: karanlıkta beyazımsı + çok hafif cyan/indigo ışık lekeleri (koyu ama
  ölü değil); accent glow bölgeleri.
- Cam: smoked-glass (buzlu füme), beyaz-%5-10; arkasındaki gradyanı gösterir.
- Işık: beyazımsı + hafif turkuazımsı ışık cam yüzeylerde dolaşır; accent glow.
- Kenarlar: ince açık gri / beyazımsı ışık çizgisi (karanlıkta daha görünür);
  az sayıda noktada hafif kromatik kenar.
- Gölge: siyah tabanlı, derin ama yumuşak.

### 6.3 Cam Malzeme Formülü (yönlendirici — bileşen bazlı değil)

- Zemin gradyanı her sayfanın arkasında yaşar (sayfa zemini token'ı + üst katman).
- Cam yüzey: zeminin ışığını taşıyan yarı saydam katman + 1px yarı saydam kenar
  (light: beyaz %70 · dark: açık gri %8) + üst kenarda specular ışık çizgisi.
- Tint: accent/turkuaz yalnız durum/aktif/mikrofon bölgelerinde cam içine sızar.
- Elevation: yükseldikçe cam parlaklaşır (light) / aydınlanır (dark); gölge
  büyür ama yumuşak kalır.

### 6.4 Gradient Kuralı

- Gradient "gradient olsun diye" değil; **yüzeyin ışık davranışını kurmak için**
  kullanılır: zemin atmosferi, cam sızıntısı, hover ışık shift'i.
- Işık yönü tutarlıdır (üstten/soldan) — tüm yüzeyler aynı ışık kaynağına inanır.
- Dark'ta gradient daha görünür olur (ışık karanlıkta konuşur).

### 6.5 Kontrast & State (tema farkındalığı)

- Hover/press'te arka plan değişiyorsa **text/icon kontrastı da beraber değişir**.
- Accent'in Light/Dark hex'i aynı olmak zorunda değildir (dark'ta parlar).
- Okunabilirlik her koşulda WCAG AA (cam asla kontrastı feda etmez).
- Her bileşen her temada ayrı ele alınır; "Light'ta güzel → Dark'ta da güzel"
  varsayımı yasaktır.

### 6.6 Blur & Perf Stratejisi (önceki karar korunur)

- Gerçek blur pencere seviyesinde: `Mica` → `DesktopAcrylic` → fallback.
- Panel seviyesinde blur yalnız overlay/sticky bar + hero kartlarda; zemin
  gradyan + cam saydamlık geri kalanını karşılar (perf).
- `Low/Medium` tier'da system backdrop + ağır gölge kapalı; cam simülasyonuna düş.
- Gölge: MAUI Windows'ta `Shadow` (GraphicsView) maliyetli → kontrollü kullan;
  gradyan/specular ile derinlik hissi çoğu zaman gölgeden daha ucuzdur.

---

## 7. Efektler

- **Hover:** ışık shift (accent → accent+koyu), 0.15s ease-out; cam panelde
  parlaklık +1-2%; imleç değişimi (`Hand`).
- **Press:** scale 0.97 + gölge küçülme.
- **Focus ring:** accent çizgi, radius ile uyumlu.
- **Mikrofon etkileşimi:** dinlerken accent halka nabzı (breathing glow),
  canlı dalga çizgisi (amplitude) — performans için düşük FPS kare.

---

## 8. Bileşen Kütüphanesi (plan)

- `AppCard` (cam kart: title + subtitle + action)
- `StatCard` (büyük rakam + etiket + accent vurgu çizgisi)
- `PrimaryButton` / `GhostButton` / `IconButton` (radius-pill, hover/press)
- `PillTag` (durum/öncelik chip'leri)
- `GlassBar` (sticky üst cam bar)
- `LiveTranscriptPanel` (mikrofon: dalga + canlı metin + Bitir)
- `TaskRow` (görev satırı: checkbox animasyonu, başlık, meta, ses rozeti)
- `EmptyState` (boş liste: illüstratif ikon + mesaj + CTA)
- `SegmentedFilter` (filtre segmentleri)
- `PermissionSheet` (mikrofon / KVKK onay)
- `ToastInApp` (bildirim çubuğu)

---

## 9. Animasyon İlkeleri (özet — detay transition-framework.md'de)

- Yumuşak/akışkan: Apple hissi → easeOutQuint `t => 1 - Math.Pow(1 - t, 5)`,
  custom `(0.22, 1, 0.36, 1)` ana ritim; `SpringOut` mikro toggle'lar.
- Süre: mikro 150-200ms · element 250-300ms · sayfa/hero 400-600ms ·
  splash→ana karşılama 700-900ms.
- Hareket yalnız Transform/Opacity (layout animasyonları FPS yer).
- **Her animasyon bir hikaye anlatır** (bkz. transition-framework.md §1): ses
  teması (mikrofon nefesi, dalga, ripple) + sıcak karşılama + premium gösteriş.
- Örtüşen (overlapping) timeline — ardışık değil; hiç donuk an yok.
- Azaltılmış hareket tercihi (reduced motion) → yalnız opacity toggle.
- Açılış: splash → sayfalar opacity+y-up (fade-slide), kartlar stagger (30ms).

---

## 10. Onay & Uygulama Sırası

1. Kullanıcı onayı: accent renk çifti + display font seçimi.
2. `Colors.xaml` yeniden (Light + Dark* ayrı token'lar).
3. `Styles.xaml` (typography, space, radius, shadow, button/card styles).
4. Bileşen `ContentView`'ları + animasyon servisleri.
5. Sayfa sıralaması: AppShell → MainPage → TodoListPage → TodoDetail →
   Settings → Login.
6. Regression guard + build + `app.log` kontrolü.
