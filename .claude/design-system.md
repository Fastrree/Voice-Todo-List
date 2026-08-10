# design-system.md — Todo Voice Tasarım Sistemi

Bu dosya uygulamanın **hedef tasarım sistemini** tanımlar (premium redesign).
Uygulanan her sayfa bu token'lara ve ilkelere uyar.

> Durum: **PLAN** — renk/tipografi kararları burada netleştirildikten sonra
> `Colors.xaml` / `Styles.xaml`'a uygulanacak. Light ve Dark tema BAĞIMSIZ
> tasarlanır (dark, light'ın koyu hali değil; ayrı bir deneyimdir).

---

## 1. Tasarım Dili (Özet)

- **Estetik:** "yüksek kaliteli ses aleti" — fütüristik ama sıcak. Cam (glass),
  soft gölgeler, akışkan animasyonlar.
- **Hissiyat:** profesyonel, minimal ama karakterli; AI ses asistanı modernliği.
- **İlham kaynakları (kopya değil):** front-end.md'nin ayırt edici tasarım
  ilkeleri; ui-ux-pro-max'ın 50+ stil / 161 palet / 57 font çifti kataloğu;
  chicky-pos transition-framework.md'nin animasyon dili.

---

## 2. Renk Sistemi

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

- **Light tema:** saf beyaz yerine çok açık soğuk nötr zemin; cam paneller
  beyaz-%60 blur; metinler koyu "mürekkep" tonu.
- **Dark tema:** koyu grafit/indigo zemin (tam siyah değil); cam paneller
  beyaz-%6; accent ışık saçar (glow). Metinler buz beyazı.

### 2.2 Renk Kuralları

- Renk daima token olarak (AppThemeBinding Light/Dark ayrı); **asla** ham hex inline.
- `Dark*` anahtarları ayrı tanımlanır (MAUI 8 `OnTheme` kısıtı, ADR-005).
- Gradients: yalnız accent + secondary arası (Soft/contrast hedefli).

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

## 6. Gölge & Cam (Glass)

- Gölgeler yumuşak, çift katmanlı (ambient + key):
  - `Shadow-Sm` 0 4 12 @ %8 → hafif kartlar
  - `Shadow-Md` 0 8 24 @ %12 → açılır, modals
  - `Shadow-Lg` 0 16 40 @ %16 → hero / aktif durum
- **Liquid Glass (Apple tasarım dili) hedeflenir:** cam efekti, ışık
  kırılmaları, yansımalar ve akışkan saydamlık hissi (kullanıcı kararı).
  Detaylı mühendislik planı `transition-framework.md` §2'dedir.
- **Cam panel formülü:**
  - Light: `rgba(255,255,255,0.6)` + pencere `Mica/Acrylic` backdrop + 1px `rgba(255,255,255,0.7)` border
  - Dark: `rgba(255,255,255,0.05)` + pencere `Mica/Acrylic` backdrop + 1px `rgba(255,255,255,0.08)` border
  - Specular yansıma: panelin üst kenarında 1px beyaz degrade ışık çizgisi
    (kırılma/yansıma simülasyonu — gradient tabanlı, platform bağımsız).
  - Işık kırılması simülasyonu: panel kenarlarında hafif accent tint sızması.
- **Blur stratejisi:** gerçek blur pencere seviyesinde (`MicaBackdrop` →
  `DesktopAcrylicBackdrop` → fallback yarı saydam solid). Panel seviyesinde
  per-element blur yalnız overlay/sticky bar + hero kartlarda (perf); Mica
  zaten arkayı bulanıklaştırdığı için paneller solid+border ile yeterli.
- Cam KULLANILACAK: sayfa üstü sticky barlar, mini oynatıcı, mikrofon paneli,
  modals/sheets. Cam KULLANILMAYACAK: tüm sayfa yüzeyleri (perf + okunabilirlik).
- **Okunabilirlik:** cam üzerindeki metin daima `TextPrimary/Secondary` token'ı
  ile okunaklı kalır (cam asla kontrastı feda etmez — Liquid Glass ilkesi).
- Gölge: MAUI Windows'ta `Shadow` (GraphicsView tabanlı) maliyetli → sayfa
  geçişinde aşırı kullanma; kartlarda `Shadow` radius kontrollü kullan.
  `Low/Medium` performans katmanında cam (SystemBackdrop) ve ağır gölge kapalıdır.

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
