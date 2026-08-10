# ui-blueprint.md — Todo Voice Design Audit & UI Blueprint

> Durum: **REVİZYON** (2026-08) — Liquid Glass felsefesi "malzeme sistemi"ne
> genişledi (design-system.md §6). KN-1..6 kapalıydı; **KN-7 ile Blueprint'in
> tasarım felsefesi güncellendi.** Kodlama bu revizyondan sonra blueprint'e göre
> yürütülür. Bu dosya bir tavan değil tabandır: model, blueprint'te yazmasa da
> ürünü gerçekten güzelleştirecek tasarım kararlarını **kendiliğinden önerir**.

---

## 1. Design Audit — Mevcut UI'ın Denetimi

Kapsam: 5 sayfa (Main, TodoList, TodoDetail, Settings, Login) + AppShell + 5 converter.

### 1.1 Genel bulgular

| # | Sorun | Ciddiyet | Etkilenen |
|---|-------|----------|-----------|
| A1 | `Shell.BackgroundColor` accent = **turkuaz değil ama MainPage örnek dönüşümü sonrası accent mavi kullanıyor**; AppShell hâlâ eski `Primary` (legacy mavi) — tutarlı ama tab bar "marka karakteri" taşımıyor | Orta | AppShell |
| A2 | **Emoji tabanlı ikonografi** (🎤 🗑️ 📝 ⚙️ ✅ ⏳ 🔴🟡🟢) — platform-native görünüm değil; renk/hover/state uyumu zayıf; Windows'ta font/boşluk tutarsızlığı | Yüksek | Tüm sayfalar |
| A3 | Hover state'leri yalnız yeni `Styles.xaml` butonlarında var; **eski XAML inline stiller** (`BackgroundColor="White"`, `TextColor="{StaticResource Primary}"`) hover/pressed/disabled yoksun | Yüksek | TodoList, TodoDetail, Login, Settings |
| A4 | Görsel hiyerarşi zayıf: `LabelSmall`/`LabelMedium`/`BodyMedium` arası fark küçük; başlıklar emoji'ye dayanıyor; **display tipografi yalnız MainPage'de** | Orta | Tümü |
| A5 | **Kart dili tutarsız:** MainPage artık `GlassPanel`/`Border`, diğer 4 sayfa hâlâ `Frame` (`CardFrame`) — aynı uygulamada iki farklı kart dili | Yüksek | 1.2c hedefi |
| A6 | Sync durum banner'ı **kırmızı/solid accent dolgulu tam genişlik Frame** (`CornerRadius=0`) — Liquid Glass'a aykırı; okunabilir ama tasarım dilinin dışı | Orta | TodoList |
| A7 | Login: zemin `{StaticResource Primary}` (solid mavi), beyaz kartlar — design-system "çok açık soğuk nötr zemin + cam" kararıyla çelişir; **Login artık kullanılmıyor** (prototip akışı AppShell'e gidiyor, App.xaml.cs) | Düşük | Login |
| A8 | Empty state, loading, error **düzensiz**: EmptyView var (TodoList) ama tasarım dili yok; loading spinner'lar düz ActivityIndicator; error hep `DisplayAlert` | Yüksek | Tümü |
| A9 | **Responsive yok**: sabit `Padding=20`, `ColumnDefinitions="*,*"`, tek sütun; pencere daralınca sıkışma | Orta | Tümü |
| A10 | Uzun başlıklar satır taşması riski: TodoList title `TitleMedium` + strikethrough trigger; açıklama `MaxLines=2` iyi ama durum ikonu emoji'ye kayıyor | Düşük | TodoList |
| A11 | `Button` default style yeni `Styles.xaml`'da **accentsiz**, ama TodoList "➕" butonu inline `Secondary` (turkuaz) — vurgu ekonomisi kuralına (design-system §2.2) **aykırı**: turkuaz ekleme butonuna giriyor | Orta | TodoList |
| A12 | Çift silme riski (C0'da düzeltildi) — delete butonu `Danger` inline, onay `DisplayAlert`; tasarım olarak "tehlikeli eylem" dili tutarlı | Düşük | TodoList, TodoDetail |
| A13 | `ProgressBar` progress rengi legacy `Primary` (mavi) — tutarlı; ama playback row'da `BodyMedium`+`BodySmall` ayrımı zayıf | Düşük | TodoDetail |
| A14 | A11 ile ilgili: filtre butonları `SecondaryButton` (ghost) — hover yok, aktif filtre **vurgulanmıyor** (seçili durum belirsiz) | Yüksek | TodoList |

### 1.2 Sayfa bazlı audit

#### MainPage (yeniden tasarlandı, 1.2 örneği)
- **İyi:** GlassPanel'ler, Sora display rakamlar, micro-label bölüm başlığı, turkuaz yalnız "Tamamlanan" vurgusunda.
- **Sorunlar:** Emoji header (🎤); "Yenile" ghost buton; versiyon micro-label zemine gömülüyor; Grid `RowDefinitions="Auto,*,Auto"` ScrollView içinde — yükseklik boşa kullanılıyor; stat kartları cam ama quick-action kartları aynı cam dilinde — ayrım yok.

#### TodoListPage (en karmaşık, en çok kırılgan)
- 4 filtre + öncelik + sıralama = **3 kontrol katmanı** üst üste (arama, filtre chip'leri, 2 picker) — görsel gürültü.
- Sync banner solid, `CornerRadius=0`, tam genişlik.
- Görev kartı: checkbox + içerik + durum emoji + sil butonu — 4 sütun, dar pencerede sıkışır.
- "Ses kaydı hazır" ve "dinliyor" panelleri solid renkli Frame'ler; canlı transkripsiyon metni italic.
- Ekleme çubuğu solid `SurfacePrimary` Frame + 3 element (input + 🎤 + ➕).

#### TodoDetailPage
- 2 kart (bilgi + ses kayıtları) + 4 buton listesi. Düzenleme modu gizle/göster ile; picker yok.
- Ses kayıt satırı: dosya adı + süre + play + sil. `ProgressBar` yalnız çalarken görünür — satır yüksekliği atlıyor (layout kayması).

#### SettingsPage
- 5 kart üst üste (Profil, Tercihler, Sync, İstatistik, İşlemler) — uzun scroll, başlık hiyerarşisi zayıf.
- Tercihler `Switch` + label satırları; tema seçici `SelectedTheme` var ama **XAML'de yok** (ThemeOptions tanımlı, UI'da değil) — boşluk.
- `SignOut` inline `Danger` buton; "Yerel Verileri Temizle" ghost — tehlike hiyerarşisi tersine çevrilebilir.

#### LoginPage
- Solid mavi zemin, beyaz kartlar — design-system'a aykırı; artık varsayılan akışta kullanılmıyor (App.xaml.cs prototip akışı). 1.3 onboarding ile birlikte değerlendirilecek.

---

## 2. UI Blueprint — Sayfa Bazlı Hedef Tasarım

> Ortak ilkeler: her sayfa `Background` token zeminli, üst `GlassBar` (sticky, cam),
> ana içerik `GlassPanel` kartlar, display tipografi Sora, emoji yerine **metin+icon
> glyph** (A2), hover/press/disabled VisualState her etkileşimde, `Card`/`GlassCard`
> ayrımı, azaltılmış hareket desteği (transition-framework §8).

### 2.1 AppShell (tab bar)
- **Amaç:** 3 ana bölüm (Ana Sayfa / Görevler / Ayarlar) arasında geçiş; marka kimliği taşıyıcısı.
- **Hiyerarşi:** TabBar en altta sabit, cam/tint zemin (Mica üzerinde `Surface` %yüksek opak).
- **Görünüm:** Seçili tab accent mavi + ince accent alt çizgi; seçili değil `TextTertiary`.
- **Glass:** tab bar zemininde cam (backdrop yansıtır); aktif tab accent glow (yalnız hover/active).
- **Karar Noktası KN-1:** Tab bar ikonları için hangi glyph seti? (SegmentAppIcon/Tabler/Fluent) — varsayılan öneri: **Segoe Fluent Icons** (Windows native, font olarak gömülebilir).

### 2.2 MainPage (Ana Sayfa — ürünün karakter yüzü)
- **Amaç:** anlık özet + iki ana aksiyona (Görevler, Ayarlar) yönlendirme; "hoş
  geldin" sıcaklığı. **Bento grid:** farklı boyutlarda modüler kartlar,
  asimetrik ama düzenli yerleşim (design-system §1).
- **Yerleşim:** ScrollView → `StackLayout` (24px padding, maks 720 ortalanmış).
  - Zemin: aurora mesh-gradient (çok açık cyan/violet/beyaz leke; dark'ta koyu
    ışık lekeleri) — düz `Background` değil.
  - Header: mikrofon ikonu (SVG/glyph, emoji değil) + `DisplayLarge` "Merhaba,
    {ad}" + `CaptionText` alt.
  - Bölüm "GENEL BAKIŞ": **Bento** — büyük "toplam görev" kartı (display rakam,
    accent) + daha küçük 3 stat kartı (tamamlanan=turkuaz ışık, bekleyen,
    sesli) → asimetrik grid, tümü hafif cam (§6 yoğunluk).
  - Bölüm "HIZLI ERİŞİM": 2 `Card` (solid, cam değil — aksiyon kartları),
    tıklanabilir, hover yükselme + gölge, ikon + `SubtitleMedium` + `CaptionText`.
  - Footer: ghost "Yenile" + versiyon micro-label.
- **Empty/loading:** yüklenirken 4 skeleton `GlassPanel` (opacity pulse); hata
  varsa inline mesaj kartı.
- **Dark:** aynı yapı ama **ayrı atmosfer**: smoked-glass zemin, rakam renkleri
  Dark* token'ları; turkuaz ışık yalnız "tamamlanan" vurgu kartında (glow).

### 2.3 TodoListPage (Görevler — ürünün kalbi)
- **Amaç:** görevleri ara/filtrele/sırala, hızlı ekle, sesle oluştur, tamamla/sil.
- **Yerleşim:** Grid `RowDefinitions="Auto,*,Auto"`.
  - **Üst `GlassBar` (cam, sticky):** arama `Entry` (cam içi, radius-pill) + filtre **`SegmentedFilter`** chip'leri (Tümü/Bekleyen/Tamamlanan/Sesli) + öncelik/sıralama picker'ları (icon+compact). A1/A14 çözümü: aktif filtre accent dolgu, hover.
  - **Orta:** `RefreshView` + `CollectionView`; satır = `GlassCard`:
    - sol: animasyonlu checkbox (spring tick, transition-framework §6)
    - içerik: `SubtitleMedium` başlık (+ strikethrough & `TextSecondary` tamamlanınca) + `CaptionText` açıklama (2 satır) + meta satırı (`MicroLabel`: öncelik glyph + süre + tarih)
    - sağ: durum glyph + sil (icon buton, hover danger dolgu)
  - **Alt `GlassBar` (cam):** ses akışı + ekleme.
    - Dinlerken: `GlassPanel` içinde `BreathRing` + canlı transkripsiyon + "Bitir"; `VoiceFlowState`→cam glow (Listening=secondary turkuaz halka, Processing=accent, Recognized=success, Failed=danger).
    - Kayıt hazır: ince `VoiceReadySoft` kart.
    - Ekleme satırı: `Entry` (radius-pill) + mikrofon `BreathRing` butonu + accent "Ekle" butonu (turkuaz DEĞİL — A11 düzeltmesi).
- **Empty/loading/error:** EmptyView = `GlassCard` içinde ikon + `SubtitleMedium` + CTA; loading skeleton satırlar; error inline `DangerSoft` kart (DisplayAlert azalt).
- **Responsive:** dar pencerede 4 sütunluk satır 2 satıra; filtre chip'leri yatay scroll; 720px üstü merkezlenir.
- **Karar Noktası KN-2:** Filtre chip'leri `SegmentedFilter` (tek seçim, accent kayan çubuk) mu, `PillTag` toggles mı? Öneri: **SegmentedFilter** (transition-framework §6 "filtre segment accent çubuk kayar").

### 2.4 TodoDetailPage (Görev Detayı)
- **Amaç:** görevin tam bilgisi, ses kayıtlarını oynat/sil, düzenle.
- **Yerleşim:** ScrollView → StackLayout (24px).
  - Üst `GlassCard`: `TitleLarge` başlık + `BodyText` açıklama + meta satırları (`MicroLabel` etiket + değer), edit modunda `DatePicker`'lar.
  - "SES KAYITLARI": bölüm `MicroLabel` + kayıt satırları (`GlassPanel`): dosya adı + süre + `ProgressBar` (oynatma ilerlemesi, accent) + play/durdur icon butonu + sil.
  - Aksiyonlar: `PrimaryButton` (Düzenle/Kaydet), `GhostButton` (İptal), danger "Sil" (onay sheet).
- **State:** düzenleme modu — kart yumuşakça accent border'a geçer; kaydetme success pulse.
- **Karar Noktası KN-3:** Oynatma dalga formu (transition-framework `WaveLine`) bu sayfada mı, yalnız listenin mikrofon panelinde mi? Öneri: **iki yerde de**, ama bu sayfada oynatma ilerlemesi olarak (WaveLine progress).

### 2.5 SettingsPage (Ayarlar)
- **Amaç:** profil, tercihler, sync, istatistik, işlemler — tek yerde.
- **Yerleşim:** ScrollView → `StackLayout`; her bölüm ayrı `GlassCard` + `MicroLabel` bölüm başlığı (bölüm hiyerarşisi A5 düzeltmesi).
  1. KULLANICI PROFİLİ: avatar (glyph) + ad girişi + email (`CaptionText`) + "Güncelle".
  2. TERCİHLER: satır = label + `Switch`; **tema seçici UI'a eklenmeli** (A eksik) — `SegmentedFilter` (Açık/Koyu/Sistem) veya picker.
  3. SENKRONİZASYON: durum (`SyncStatusText`) + son sync (`CaptionText`) + "Şimdi Senkronize Et".
  4. İSTATİSTİKLER: 2×2 `GlassPanel` (MainPage diliyle uyumlu).
  5. İŞLEMLER: "Yerel Verileri Temizle" (ghost), "Hakkında" (ghost), "Çıkış Yap" (danger) — tehlike hiyerarşisi netleştirildi.
- **Glass:** bölüm kartları cam; zemin Mica'yı gösterir.
- **Karar Noktası KN-4:** Tema değişimi anında crossfade mi (transition-framework §5) yoksa anlık mı? Öneri: **300ms crossfade** (1.4'e).

### 2.6 LoginPage
- **Amaç:** (varsayılan akışta kullanılmıyor) email/şifre giriş + kayıt.
- **Blueprint:** design-system'a uygun — çok açık nötr zemin + cam kart içinde form; emoji logo yerine glyph; solid mavi zemin kaldırılır. 1.3 onboarding ile birlikte yeniden ele alınır; bu sprintte düşük öncelik.

---

## 3. Component Sözlüğü

> Bileşenler §6 yoğunluk spektrumunun somutlaşmasıdır: aynı cam malzeme,
> kullanım amacına göre yoğunluk alır. Bu tablo bir katalogdur, tavan değil.

| Component | Yoğunluk | Kullanım | State'ler |
|-----------|----------|----------|-----------|
| `GlassPanel` | hafif cam (Controls/) | cam kart, sticky bar, mikrofon paneli | hover parlaklık |
| `GlassBar` | derin cam | üst sticky arama/filtre, alt ekleme çubuğu | cam + border |
| `GlassFrame` | hafif cam (`Style` var) | bölüm kartları (Frame tabanlı ekranlar) | — |
| `GlassCard` | hafif cam (`Style` var) | liste satırı, bölüm kartı | hover parlaklık |
| `Card` | solid (`Style x:Key="Card"` var) | aksiyon kartı, detay kartı | hover yükselme |
| `StatCard` | hafif cam (`Style` var) | istatistik hücresi (Main/Settings) | — |
| `PrimaryButton` | solid accent | ana CTA | normal/hover/pressed/disabled/focused |
| `GhostButton` | solid saydam | ikincil | + |
| `IconButton` | solid saydam | ikon aksiyonları | + |
| `PillTag` | solid | öncelik/durum chip'leri | seçili/seçili değil |
| `SegmentedFilter` | derin cam | filtre + tema segmentleri | kayan accent çubuk |
| `BreathRing` | organic (derin cam + blob) | mikrofon nefes halkası | idle/listening/processing/recognized/failed |
| `WaveLine` | organic | canlı amplitude + oynatma dalgası | recording/playback |
| `TaskRow` | hafif cam | liste satırı | normal/completed(tick)/swipe-to-delete |
| `EmptyState` | hafif cam | boş liste | — |
| `PermissionSheet` | derin cam (modal) | mikrofon/KVKK onay | — |
| `ToastInApp` | derin cam | sync/success/error bildirim çubuğu | — |
| `SkeletonCard` | hafif cam | loading iskeleti | pulse |

---

## 4. State / Interaction Sözlüğü

### 4.1 VoiceFlowState → görsel eşleme (B3 zinciri + 1.4)
| State | Görsel | Renk | Animasyon |
|-------|--------|------|-----------|
| `Idle` | BreathRing kapalı | `TextTertiary` | — |
| `Listening` | BreathRing nabız + WaveLine canlı | **secondary (turkuaz)** | breathing glow + amplitude (150ms loop) |
| `Processing` | ring dolgusu + spinner | accent | 300ms dönüş |
| `Recognized` | ring → success tick | success | spring tick (250ms) |
| `Failed` | ring kırmızı titreme | danger | shake (200ms) |
- Kaynak: TodoListPageViewModel `VoiceFlowState`, `SpeechStatus`, `IsSpeechListening` (satır 583-592).

### 4.2 Sync durumu (TodoList top bar + Settings)
| Durum | Görsel |
|-------|--------|
| çevrimiçi | accent pulse nokta + "Çevrimiçi" |
| çevrimdışı | gri nokta + "Çevrimdışı" |
| senkronize ediliyor | ince accent progress + mesaj |
| son senkron | `CaptionText` zaman damgası |
- Kaynak: `SyncStatus`, `IsOnline`, `IsSyncing`, `LastSyncTime`.

### 4.3 Global etkileşimler (transition-framework §6)
| Etkileşim | Animasyon | Süre |
|-----------|-----------|------|
| hover | accent shift + translateY -2 | 150ms |
| press | scale 0.97 + gölge küçülür | 100ms |
| checkbox tamamla | spring tick + satır fade | 250ms |
| görev eklendi | satır yukarıdan + accent ripple | 300ms |
| silme | satır sola + opacity 0 | 300ms |
| filtre segment | accent çubuk kayar | 200ms |
| tema değişimi | pencere crossfade | 300ms |
- Reduced motion: tüm slide/scale → opacity toggle (transition-framework §8).

---

## 5. Light / Dark Tema Kuralları

- **Kaynak:** `Colors.xaml` (yeni sözlük) + `design-system.md §2, §6` — Light ve
  Dark **ayrı atmosfer** (dark ≠ light'ın koyusu; her bileşen her temada ayrı
  ele alınır).
- **Light (White / Pearl / Mist glass):** `Background #F6F8FC` (çok açık soğuk
  nötr) + **çok hafif cyan-gray aurora mesh-gradient zemin** + `Surface #FFFFFF`
  + cam `#99FFFFFF` (%40-60 yarı saydam); metin `TextPrimary #0F1B2E`; accent
  mürekkep mavisi `#2563EB`; gölgeler mavi-alt tonlu (`ShadowSm #140F1B2E`).
- **Dark (Black / Graphite / Smoked glass):** tam siyah değil; `Background
  #0B1220` üzerine siyah→grafit→füme gradyan + koyu aurora ışık lekeleri; cam
  smoked-glass `#0FFFFFFF`; accent glow `#5B8CFF`; metin buz beyazı `#E9EEF8`;
  gölgeler siyah tabanlı. Accent hex'i light'takiyle aynı olmak zorunda değildir.
- **Kural:** metin cam üzerinde daima `TextPrimary/Secondary` (okunabilirlik asla
  feda edilmez — Liquid Glass ilkesi 5); vurgu renkleri (turkuaz) yalnız
  state/motion/success; accent her yerde ana karakter. **Hover/press/aktif'te
  arka plan değişiyorsa text/icon kontrastı da beraber değişir.**
- **Chromatic:** yalnız mikrofon halkası / aktif vurgu / cam kenarı gibi az
  sayıda noktada sedefli cyan-violet kırılması (kontrollü; "her yere değil").
- **Aykırılıklar (audit A7, A11):** Login solid mavi + todo ekleme turkuaz →
  düzeltilecek.
- Tema seçici: Settings'te `ThemeOptions` (Açık/Koyu/Sistem) UI'a bağlanmalı (KN-4).

---

## 6. Liquid Glass Kullanım Kuralları — Malzeme Sistemi (KN-7)

> **Liquid Glass bir bileşen değil; uygulamanın görsel malzemesidir.** Aynı
> malzeme yoğunluğuna göre farklı yüzeyler üretir. Bileşen adları (GlassPanel,
> GlassCard, GlassFrame, Card) bu malzemenin **yoğunluk dereceleridir** — ayrı
> "şeyler" değil.

### 6.1 Yoğunluk Spektrumu (zeminden solid'e)

| Yoğunluk | Malzeme | Kullanım | Işık davranışı |
|----------|---------|----------|----------------|
| **Zemin** | `Background` + atmosfer gradyanı (aurora lekesi) | her sayfanın arkası; düz solid değil, katmanlı | Mica'yı gösterir; aurora ışık lekeleri zeminde yaşar |
| **Hafif cam** | `GlassFrame`/`GlassPanel`, yarı saydam %40-60 (light) / %5-10 (dark) | bölüm kartları, stat kartları, liste satırları, mikrofon paneli | arkasındaki zemini geçirir; ince specular kenar |
| **Derin cam** | `GlassBar` (sticky), modal/sheet, mini oynatıcı | overlay / yapışkan üst-alt barlar, odak yüzeyleri | daha belirgin kenar + accent tint sızması |
| **Solid** | `Card`, `Surface` | tıklanabilir aksiyon kartları, form input'ları, uzun metin blokları | net zemin; okunabilirlik garantisi |

- Cam paneller zeminden bağımsız beyaz kutu **değildir**; zeminin gradyanını
  geçirir, onunla optik olarak bütünleşir.
- Turkuaz (secondary) **cam içine giren ışık/tint** olarak kullanılır — yalnız
  state/aktif/mikrofon bölgelerinde; her yere sürülmez (vurgu ekonomisi §2.2).
- Chromatic sedef kırılması: mikrofon halkası + aktif vurgu + cam kenarı gibi
  **az sayıda noktada** (kontrollü, §5).

### 6.2 Kontrast & Tema (zorunlu)

- Hover/press/aktif'te arka plan değişirse **text/icon kontrastı beraber
  değişir** (yalnızca arka planı koyulaştırıp metni sabit bırakmak yasak).
- Her bileşen Light ve Dark'ta ayrı ele alınır; accent hex'i temaya göre
  değişebilir. Cam üzerinde metin daima okunaklı (WCAG AA).

### 6.3 Perf Sınırı (korunur)

- Gerçek blur yalnız pencere (Mica → Acrylic → fallback); panel blur yalnız
  overlay/sticky + hero. Zemin gradyan + cam saydamlık geri kalanını karşılar.
- `Low/Medium` tier'da system backdrop + ağır gölge kapalı; cam simülasyonu.

### 6.4 Sayfa bazlı yoğunluk kararları (model kendisi seçer)

- MainPage: **Bento grid** — zemin aurora, stat kartları hafif cam, aksiyon
  kartları solid.
- TodoList: üst/alt **GlassBar** (derin cam) + satırlar hafif cam.
- TodoDetail: üst bilgi **hafif cam**, ses kayıtları **solid satırlar** (okunabilirlik).
- Settings: bölüm kartları hafif cam, işlem butonları solid/ghost.
- Voice anı: mikrofon paneli derin cam + **organic blob** (BreathRing) — keskin
  dikdörtgen değil.

---

## 7. Öncelikli Redesign Listesi

| Öncelik | Öğe | Kaynak |
|---------|-----|--------|
| P0 | 1.2c yaygınlaştırma: TodoList/Settings/TodoDetail'i GlassPanel+token'lara taşı; legacy compat'ı sil | A5 |
| P0 | Filtre/öncelik/sıralama kontrol katmanlarını sadeleştir + `SegmentedFilter` (aktif durum) | A14, A1 |
| P0 | Emoji → glyph (KN-1) en azından kritik aksiyonlarda | A2 |
| P1 | Ekleme butonu turkuaz → accent (vurgu ekonomisi) | A11 |
| P1 | Sync banner'ı cam/ince duruma çevir | A6 |
| P1 | Hover/press/disabled tüm etkileşimli öğelere | A3 |
| P1 | Empty/loading/error bileşenleri (`EmptyState`, `SkeletonCard`, inline error) | A8 |
| P2 | Settings tema seçici UI'ı (KN-4) | eksik |
| P2 | Playback `ProgressBar` layout kaymasını sabitle (sabit satır yüksekliği) | A13 |
| P2 | Responsive: 720px merkez + dar pencere düzenleri | A9 |
| P2 | Login + onboarding birlikte (1.3) | A7 |
| P3 | `WaveLine`/`BreathRing` state bağlama (1.4) | §4.1 |

---

## 8. Karar Noktaları (KN-7 ile revize, 2026-08)

- **KN-1 ✅** İkon glyph seti: **Segoe Fluent Icons** (Windows native; font gömülür).
- **KN-2 ✅** Filtre bileşeni: **`SegmentedFilter`** (kayan accent çubuk).
- **KN-3 ✅** WaveLine: **mikrofon paneli + oynatma progress** (iki yerde).
- **KN-4 ✅** Tema değişimi: **300ms crossfade** (1.4'te uygulanır).
- **KN-5 ✅** Swipe-to-delete: **yok**; silme butonla (onay sheet).
- **KN-6 ✅** Login: **şimdilik bekle**, 1.3'te onboarding ile yeniden değerlendirilir; prototip akışı (AppShell) korunur.
- **KN-7 ✅** **Liquid Glass = malzeme sistemi** (bileşen değil): yoğunluk
  spektrumu (zemin → hafif cam → derin cam → solid) + iki ayrı atmosfer
  (White/Pearl/Mist ve Black/Graphite/Smoked) + aurora zemin + kontrollü
  chromatic + tema-farkında kontrast. Blueprint bir **taban**dır, tavan değil;
  model, mimari/UX/erişilebilirlik/perf sınırları içinde daha iyi görsel
  kararları **kendiliğinden üretir** (design-system.md §1, §6).

---

## 9. Onay & Uygulama Sırası (design-system §10 ile uyumlu)

1. **Bu blueprint revizyonu onayı** (KN-7 dahil).
2. 1.2c yaygınlaştırma blueprint'e göre tamamlanır (TodoList → Settings → TodoDetail).
3. MainPage **Bento** düzeni + aurora zemin (kimlik yüzü).
4. `WaveLine`/`BreathRing` (organic formlar) → voice state bağlama.
5. 1.3 Onboarding & izinler → 1.4 mikro-etkileşimler → 1.5 kalite kapıları.
6. Regression guard + build + `app.log` boş + elle çökme testi.
