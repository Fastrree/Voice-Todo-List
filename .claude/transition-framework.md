# transition-framework.md — Todo Voice Animasyon & Liquid Glass Altyapısı

Bu dosya, Todo Voice'un **animasyon felsefesini**, **hikaye odaklı geçiş
sistemini** ve **Apple Liquid Glass (Sıvı Cam) tasarım dilinin MAUI/Windows
uygulamasını** tanımlar.

İlk kez çalışan bir ajan, animasyon veya görsel efekt görevi aldığında bu
dosyayı okur.

> İlham (kopya değil): `C:\Users\sniya\Desktop\chicky-pos\.claude\transition-framework.md`
> — Blazor View Transition API orada çalışır; bu dosya o felsefeyi **.NET MAUI
> primitiflerine** uyarlar. Kullanıcı kararı: **her animasyon bir hikaye anlatır**,
> sıcak karşılama + premium gösteriş.

---

## 1. Felsefe: Her Animasyon Bir Hikaye Anlatır

Animasyon "süs" değil, **hikaye anlatım aracıdır**. Her efektin üç cümlesi
olmalıdır:

1. **Neden var?** (kullanıcıya ne hissettirir)
2. **Neyi anlatıyor?** (uygulamanın hangi davranışını güçlendirir)
3. **Ne kadar sürer?** (süre + easing, hikayenin ritmi)

### 1.1 Todo Voice'un Ana Hikayesi: "SES"

Uygulamanın kimliği **ses**tir — mikrofon, dalga, transkripsiyon. Tüm animasyon
dili bu temayı yansıtır:

| Tema öğesi | Hikaye | Görsel karşılığı |
|-----------|--------|------------------|
| Mikrofon | "seni dinliyorum" | Nefes alan halka (breathing ring), dinlerken pulse |
| Ses dalgası | "sesini görüyorum" | Canlı amplitude çizgisi (konuşurken titreşir) |
| Yayılma | "duydum, kaydedildi" | Görev oluşunca ince dalga ripple |
| Cam | "saydam, güvenilir, premium" | Liquid Glass paneller (içerik arkada yaşar) |
| Işık | "canlı, fark ediliyor" | Accent ışık, hover'da shift, specular yansıma |

### 1.2 Sıcak Karşılama (Kullanıcı Bağlama)

Karşılama bir "hoş geldin" seremonisidir — uygulama kullanıcıya ilk saniyede
"seni dinlemeye hazırım, sıcak bir yer burası" der. Tek başına gösteriş değil,
**kullanıcıyı karşılayan** bir akıştır (bkz. Bölüm 4 storyboard).

---

## 2. Liquid Glass — Apple Tasarım Dili (MAUI/Windows Mühendislik Planı)

> **Liquid Glass** (WWDC 2025, iOS 26 / macOS Tahoe): cam efekti, ışık
> kırılmaları, yansımalar ve **akışkan saydamlık** hissi. Arayüz öğeleri,
> arkasındaki içeriği bulanıklaştırıp "gerçek cam gibi" büker.

### 2.1 Liquid Glass'ın 6 İlkesi (uygulama hedefi)

1. **İçerik tint (content tinting)** — cam paneller, arkasındaki içerikten
   renk/ışık emer; renk daima arkadaki içeriğe göre yaşar.
2. **Işık kırılması (refraction)** — cam kenarlarından renk sızması; katman
   hareket edince içerik hafif bükülür/kayar.
3. **Yansımalar (specular highlight)** — camın üst kenarında ışık çizgisi;
   imleç/pencere hareketiyle yansıma kayar.
4. **Akışkan saydamlık (fluid translucency)** — blur/opaklık durum değişince
   yumuşakça animasyonlanır (anında değil).
5. **Görsel netlik (clarity)** — cam üzerine yerleşen metin/ikon daima okunaklı
   kalır (cam hiçbir zaman okunabilirliği feda etmez).
6. **Dinamik (dynamic)** — arkadaki içerik değişince cam da değişir (tint/blur
   canlı güncellenir).

Ek ilkeler (KN-7, 2026-08):

7. **Malzeme bütünlüğü** — Liquid Glass bir bileşen değil, uygulamanın görsel
   malzemesidir; aynı malzeme yoğunluğa göre farklı yüzeyler üretir
   (zemin → hafif cam → derin cam → solid).
8. **Atmosfer zemin** — zemin düz solid değil; aurora mesh-gradient + ışık
   lekeleriyle katmanlıdır ve cam yüzeyler bu zemini geçirir.
9. **Spatial derinlik** — yüzeyler aynı düzlemde değildir; gölge + specular +
   yoğunluk foreground/background ayrımını kurar.
10. **Tema farkındalığı** — Light ≠ Dark'in koyusu; cam ışığı, accent hex'i ve
    kontrast her temada ayrı doğrulanır (hover'da bg değişince text/icon da değişir).

### 2.2 Windows/MAUI Gerçeklik Haritası (mühendislik dürüstlüğü)

| Liquid Glass bileşeni | Windows native karşılığı | MAUI erişimi | Yapılabilirlik |
|----------------------|--------------------------|--------------|----------------|
| Pencere seviyesi blur/tint | **Mica** (masaüstü arka planını bulanıklaştırır) | `Window` → `MicaBackdrop` (WinAppSDK 1.3+) | ✅ Windows 11 22H2+ |
| Panel yarı saydam "donmuş cam" | **Desktop Acrylic** (arka plan blur + tint) | `DesktopAcrylicBackdrop` | ✅ Win11 |
| İçerik tabanlı canlı tint | Acrylic `TintColor`/`TintOpacity` + `BackgroundSource` | SystemBackdrop + tint | ✅ (statik tint; canlı içerik-emme sınırlı) |
| Per-element blur (mikro cam) | `BackdropMaterial` / Composition `BackdropBlurBrush` | MAUI'de handler/PlatformView ile | ⚠️ Karmaşık, maliyetli → **Mica üzerine bindirme simülasyonu** |
| Işık kırılması | Native yok | **Simülasyon**: kenar border + hafif accent tint + gradient | 🎨 Görsel simülasyon |
| Specular yansıma | Native yok | **Simülasyon**: üst kenar 1px degrade ışık çizgisi, hover'da kayma | 🎨 Görsel simülasyon |
| Akışkan saydamlık animasyonu | Native sınırlı | Opacity/FadeTo animasyonu ile `SystemBackdrop` üstüne bindirme katmanı | ✅ Fade/opacity ile |

### 2.3 Katmanlı Yaklaşım (sıralı uygulama)

```
KATMAN 0: SystemBackdrop (pencere)        → Mica (varsa) / DesktopAcrylic (varsa) / yoksa sayfa rengi
KATMAN 1: Atmosfer zemini                 → Background token + aurora mesh-gradient
                                             (açık: beyaz→açık gri→çok hafif cyan; koyu: siyah→grafit→füme)
                                             + büyük düşük kontrastlı ışık lekeleri (spatial derinlik)
KATMAN 2: Cam yüzeyler (yoğunluk §6)      → hafif cam %40-60 (light) / %5-10 (dark): bölüm kartları, satırlar
                                             derin cam: sticky bar, modal, mikrofon paneli
                                             + ince border (0.7 / 0.08 beyaz)
                                             + üst kenar specular çizgisi (simülasyon)
KATMAN 3: İçerik                          → metin/ikon; camın üstünde okunaklı token'lar
KATMAN 4: Işık & state                    → accent/turkuaz cam içi sızıntı (state, hover, mikrofon)
                                             + kontrollü chromatic kenar (microfon halkası, aktif vurgu)
```

- **Gerçek blur** pencere seviyesinde (Mica/Acrylic) uygulanır; panel
  seviyesinde blur'a ihtiyaç duyulmaz çünkü Mica zaten arkayı bulanıklaştırır.
- **Aurora zemin** gradyan tabanlıdır (platform bağımsız) — blur yok, ucuz,
  camın arkasından sızar; "düz beyaz/yeşil kutu" hissini kırar (design-system §6.2).
- **Spatial derinlik:** yüzeyler aynı düzlemde durmaz — zemin en altta, hafif cam
  orta, derin cam (sticky/modal) üstte; foreground/background ayrımı gölge +
  specular + yoğunlukla verilir (design-system §1).
- **Fallback zinciri** (feature detection, `TargetPlatform`/AppWindow denemesi):
  1. `MicaBackdrop` dene → başarılıysa kullan
  2. `DesktopAcrylicBackdrop` dene → Mica yoksa kullan
  3. İkisi de yoksa → cam panel = yarı saydam solid + border (blur'suz cam hissi)
- **Simülasyon efektleri** (aurora, kırılma/yansıma, chromatic) her koşulda
  çalışır — bunlar gradient/border tabanlı olduğu için platforma bağımlı değildir.

### 2.4 Mühendislik Doğrulama Listesi (cam için)

- [ ] Cam panel üzerinde metin kontrastı WCAG AA (Liquid Glass ilkesi 5)
- [ ] Pencere boyutu değişince Mica yeniden boyanıyor mu (durum yarışı yok)?
- [ ] Uygulama arka plana düşünce/geri gelince backdrop kaybolmuyor mu?
- [ ] Blur performansı: yalnız overlay/sticky bar + hero kartlarda; tüm sayfa değil
- [ ] Light/Dark ayrı cam tint değerleri (design-system §6)
- [ ] Fallback path'te (blur yok) da sayfa okunaklı ve şık mı?
- [ ] Aurora zemin gradyanı düşük kontrast (metin okunurluğunu bozmuyor mu)?
- [ ] Cam yüzey zeminle optik olarak bütünleşiyor mu (beyaz kutu hissi yok mu)?
- [ ] Hover/press'te bg değişince text/icon kontrastı beraber değişiyor mu?

---

## 3. MAUI Animasyon Primitifleri (Kullanılacak API'ler)

| API | Açıklama | Kullanım |
|-----|----------|----------|
| `View.FadeTo(opacity, ms, easing)` | Opacity | Tüm fade'ler |
| `View.TranslateTo(x, y, ms, easing)` | Konum | y-up girişler, stagger |
| `View.ScaleTo(scale, ms, easing)` | Ölçek | nefes, press, mikrofon halkası |
| `View.RotateTo(deg, ms, easing)` | Dönüş | mikro elementler (dikkatli) |
| `new Animation(...)` + `Commit` | Kompozit timeline | örtüşen çoklu aşama |
| `Easing` (`CubicInOut`, `SpringOut`, custom Func) | Eğri | Apple hissi → **easeOutQuint** `t => 1 - Math.Pow(1 - t, 5)` |
| `VisualStateManager` | State | `PointerOver` (hover), `Pressed`, `Disabled` |
| `CommunityToolkit.Maui.Animations` `AnimationBehavior` | XAML binding'li | basit mikro etkileşimler |
| `CancellationToken` | İptal | sayfa kapanırken animasyonu bırak (leak yok) |

**Kural:**
- Animasyonlar yalnız **Transform (scale/translate) + Opacity** kullanır.
  Layout/frame değiştiren animasyon (HeightRequest, Margin) FPS düşürür → yasak.
- `Rate/AnimationRate` aşırı kullanılmaz; değer başına tek animasyon.
- Sayfa ayrılırken aktif animasyonlar `CancellationTokenSource.Cancel()` ile
  iptal edilir (OOM/thread leak önlemi).

### 3.1 Apple Hissi Easing

```
AppleCurves:
  easeOutQuint  = t => 1 - Math.Pow(1 - t, 5)      // genel girişler (yumuşak yavaşlama)
  easeOutCubic  = t => 1 - Math.Pow(1 - t, 3)      // küçük elementler
  springSoft    = Easing.SpringOut (fiziksel)      // toggle, checkbox
  custom (cubic-bezier 0.22,1,0.36,1)              // örtüşen timeline ana ritmi
```

Süre tablosu:
- mikro 150-200ms (hover, press, toggle)
- element 250-300ms (fade-in, stagger adımı)
- sayfa/hero 400-600ms (açılış, sayfa geçişi)
- splash→ana 700-900ms (karşılama seremonisi)

---

## 4. Hikaye Storyboard: Sıcak Karşılama (Karşılama Akışı)

> Hedef: ilk açılışta kullanıcı "hoş geldin" duygusu yaşar; uygulama gösterişini
> sergiler; **her adım ses hikayesine bağlıdır** (chicky-pos'taki örtüşen
> timeline felsefesi — aşamalar ardışık değil, örtüşür).

### 4.1 Akış (Splash → Onboarding → Ana Ekran)

```
0ms      PENCERE AÇILIR
         │
         │  Splash (SystemBackdrop görünür; sayfa cam)
         ├── Logo/mikrofon nefes alır        0-450ms   scale 1→1.05→1 (easeOutQuint)
         ├── Halka fade (ışık halkası)       150-400ms opacity 1→0
         │
350ms    Splash → Ana ekran geçişi başlar (CategoryPage değil; AppShell)
         ├── Mikrofon ikonu morph → accent küçük halka  350-700ms
         ├── Dalga çizgisi çizilir (amplitude)          400-750ms  scaleX 0→1 + alpha
         ├── Sayfa içerik fade+rise                     450-850ms  opacity + translateY 24→0
         ├── "Merhaba" başlık (stagger)                550-850ms  delay + 300ms
         ├── Kartlar (3-4 adet) stagger                650-1150ms her 30ms gecikme
         └── Alt bar cam'a oturur                       900-1200ms translateY 30→0
```

- **Örtüşme kuralı:** bir aşama bitmeden diğeri başlar; **hiç donuk an yok**.
- Bittiğinde kullanıcı "sıcak karşılandım, bu uygulama premium" hisseder.
- Bu **yeniden karşılama** (back to front) senaryosunda tekrar oynamaz;
  onboarding yalnızca ilk açılışta (veritabanı boş + izin onayı gerekmiyorsa).

### 4.2 Onboarding (İlk Kurulum Hikayesi — 3 Ekran)

Her ekran tek cümle + tek görsel + tek CTA; geçişler akışkan (yukarı kayma + fade):

1. **"Merhaba! Seni dinliyorum."** — mikrofon ikonu + dalga çizgisi animasyonlu.
   CTA: "Başla"
2. **"Konuş, ben yazayım."** — canlı transkripsiyon önizleme (sahte metin yazar).
   CTA: "Devam"
3. **"Sana izin vereceğim."** — Mikrofon izni + KVKK/veri onayı + ses efekti tercihi.
   CTA: "Başlayalım"

- Onboarding bitince `Preferences`'da `hasOnboarded=true`; bir daha gösterilmez.
- Onboarding gösterilmezse varsayılan: doğrudan AppShell (mevcut davranış korunur).

### 4.3 Mikrofon Anı (ana ürün hikayesi)

```
Bas → halka pulse başlar (accent glow, breathing)   + mikro ses efekti
Konuş → canlı dalga çizgisi amplitude'ye tepki verir
Bitir → dalga "gelgit" gibi yatışır (scaleY 1→0, 300ms) + onay sesi
Görev oluşur → satıra ince accent ripple (yayılma)   + başarı sesi (hafif)
```

Hikaye: "Sen konuşuyorsun, uygulama **görüyor ve duyuyor**, görev canlanıyor."

---

## 5. Sayfa Geçişleri

| Geçiş | Teknik | Süre | Hikaye |
|-------|--------|------|--------|
| AppShell tab değişimi | Shell varsayılan veya custom fade | 250ms | "içerik saydam cam üzerinde yumuşakça değişir" |
| TodoList → TodoDetail | push: sağdan gel + hafif fade | 300ms | "detaya dalıyorsun" |
| Detail → List | pop: sola gider | 250ms | "gerçek dünyaya dönüyorsun" |
| Modal/sheet (izin) | yukarı kayarak cam panel oturur | 300ms | "karar penceresi seni bekliyor" |
| Tema değişimi | tüm pencere 300ms crossfade | 300ms | "dünya değişiyor ama yerinde" (arka plan + token'lar animate) |

- Shell tab geçişlerinde varsayılan animasyon bozulursa basit `FadeTo` wrapper
  (ContentPage üzerinde) kullanılır; **tab bar'ın kendisi animasyonlanmaz** (kayma yok).
- Sayfa geçişlerinde shared-element morph (chicky-pos logosu gibi) yok —
  MAUI native'de birebir yok; bunun yerine fade+rise kullanılır. İleride
  CommunityToolkit'te shared element varsa değerlendirilir.

---

## 6. Mikro-Etkileşimler (Mini Hikayeler)

| Öğe | Hikaye | Animasyon | Süre |
|-----|--------|-----------|------|
| Buton hover | "ben buradayım, basılabilirim" | accent shift + hafif yükselme (translateY -2) | 150ms |
| Buton press | "tamam, algıladım" | scale 0.97 + gölge küçülür | 100ms |
| Checkbox (görev tamamla) | "bitti!" | spring tick + satır hafif fade/solma + accent renk | 250ms |
| Kart hover | "bu kart canlı" | yükselme + gölge büyür + specular ışık kayar | 200ms |
| Filtre segment | "seçim bu" | accent çubuk kayar (slide) | 200ms |
| Görev eklendi | "yeni ses geldi" | satır yukarıdan akar + accent ripple | 300ms |
| Silme | "veda" | satır sola kayar + opacity 0 (300ms) sonra kaldır | 300ms |
| Oynatma toggle | "ses başladı/bitti" | ikon morph + dalga çizgisi animasyonlu ilerler | 200ms |
| Sync durumu | "güvendeyim" | küçük accent pulse (online) / gri (offline) | 250ms |
| Tema toggle | "dünya değişiyor" | ikon morph + genel crossfade | 300ms |

**Kural:** mikro etkileşimler asla birbirini ezmez (üst üste binince kuyruğa
alınır veya iptal edilir). Erişilebilirlik: azaltılmış hareket tercihinde tüm
slide'lar fade'e düşer.

---

## 7. Performans Katmanları (Adaptif, chicky-pos felsefesi)

Ağır efektler cihaz/GPU durumuna göre düşürülür (her animasyon hikayesini
korur, sadece "gösteriş" azalır):

```
Tier Ultra  : 8+ core, 4GB+      → tüm efektler açık, süre 1.0x
Tier High   : 4+ core            → tüm efektler açık, süre 1.0x (varsayılan)
Tier Medium : 2-3 core           → süre 0.8x, blur yok (system backdrop kapalı), panel cam simülasyonu
Tier Low    : 1-2 core           → süre 0.5x, splash timeline kısaltılır, hero animasyonları tek fade
Tier Reduced: prefers-reduced-motion → animasyon yok; yalnız opacity toggle (erişilebilirlik)
```

- Tespit: `Environment.ProcessorCount` + RAM + `reduced motion` (`AccessibilitySettings` /
  `SystemInformation` veya Windows `UISettings.AnimationsEnabled`).
- Post-animasyon ölçüm: 300ms FPS (Stopwatch) < 30 → otomatik düşür (bir sonraki
  animasyondan itibaren).
- **Kritik:** cam (SystemBackdrop) ve ağır gölge `Low/Medium`'da kapalıdır.

---

## 8. Reduced Motion (Erişilebilirlik)

- `UISettings.AnimationsEnabled == false` veya kullanıcı tercihi → `Tier Reduced`.
- Paralaks, slide, scale, ripple **kaldırılır**; yerine 150ms opacity toggle.
- Storyboard aynı kalır (hikaye kaybolmaz), sadece daha sessiz anlatılır.
- Onboarding'de de aynı davranış.

---

## 9. Uygulama Sırası (mühendislik süreci)

> Durum (2026-08-11): aşağıdaki adımların **5 ve 6'sı tamamlandı**
> (`AnimationService.cs` + `SoundEffectService.cs` çalışıyor; tüm sayfalarda giriş
> animasyonu, kart/buton hover + press, mikrofon nefes döngüsü ve anlamlı ses
> efektleri var — Ayarlar → "Ses efektleri" ile kapatılabilir).

1. `TransitionService` (orchestrator) + `Appear/Exit` yardımcıları + tier detection
2. SystemBackdrop entegrasyonu (Mica→Acrylic→fallback) + cam panel simülasyonu
3. Karşılama storyboard (splash → onboarding → ana ekran)
4. Sayfa geçişleri + Shell tab davranışı
5. ✅ **Mikro-etkileşimler** (hover/press/ripple/checkbox/onay sesleri) — 2026-08-11
6. ✅ **Ses efektleri** (AudioService üzerinden, hafif, opsiyonel — settings'te kapatılabilir)
7. Reduced motion + performans katmanları
8. Regression guard + build + `app.log` boş + çökme testi (elle)

---

## 10. Troubleshooting (MAUI/Windows)

### Blur yok (SystemBackdrop çalışmıyor)
- Windows 11 + WinAppSDK 1.3+ gerekli. Değilse → fallback (yarı saydam solid).
- `AppWindow` erişimi `MauiWinUIWindow` handler'ından; try/catch içinde.

### Animasyon sarsıntılı (FPS düşük)
- Layout animasyonu kullanılıyor olabilir → Transform/Opacity'e çevir.
- Gölge/`BackdropBlur` fazlalığı → tier düşür veya blur'u kapat.

### Sayfa değişirken eski animasyon devam ediyor
- `CancellationToken` iptal edilmedi → `OnDisappearing`'de cancel.

### Hover (PointerOver) çalışmıyor
- `VisualStateManager` PointerOver yalnız Windows'ta çalışır; state tanımı
  `{VisualState Name="PointerOver"}` içinde `Setter` ile. Tıklama yerine
  pointer girişi gerekir; uygulanmazsa fallback: mouse yok varsayımı (dokunma).

### Tema geçişinde animasyon kopuyor
- Renk token'ları AppThemeBinding anlık değişir; yumuşak geçiş için sayfa
  opacity 0→1 crossfade (300ms) wrapper'ı kullan.

---

## 11. Dosya Planı

```
TodoVoiceMaui/Services/
├── AnimationService.cs          # ✅ MEVCUT — fade/rise, lift, BreathHandle (nefes döngüsü)
├── TransitionService.cs         # karşılama storyboard, sayfa geçişleri
├── BackdropService.cs           # Mica → Acrylic → fallback system backdrop
├── SoundEffectService.cs        # ✅ MEVCUT — sentezlenmiş WAV tonları (winmm P/Invoke, asset yok)
TodoVoiceMaui/Views/Onboarding/
├── OnboardingPage.xaml(.cs)     # 3 ekranlı karşılama
TodoVoiceMaui/Controls/
├── GlassPanel.cs(xaml)          # cam panel (blur simülasyonu + specular)
├── WaveLine.cs                  # canlı amplitude dalga çizgisi
├── BreathRing.cs                # mikrofon nefes halkası
TodoVoiceMaui/Resources/Animations/
└── (easing/timing sabitleri, belki AppConstants)
```

---

*Bu dosya, animasyon veya Liquid Glass altyapısında önemli bir karar
değişince güncellenir. Geçmiş git'te kalır.*
