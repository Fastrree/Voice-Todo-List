# ui-blueprint.md — Todo Voice Design Audit & UI Blueprint

> Durum: **ONAYLANDI** (2026-08) — KN-1..6 kapatıldı. 1.2c uygulaması bu
> dosyadaki hedeflere göre yürütülür; sürekli mikro-müdahale yerine blueprint'e
> uyulur. Değişiklik gerekirse bu dosya güncellenir, sonra kodlanır.

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

### 2.2 MainPage (Ana Sayfa)
- **Amaç:** anlık özet + iki ana aksiyona (Görevler, Ayarlar) yönlendirme; "hoş geldin" sıcaklığı.
- **Yerleşim:** ScrollView → `StackLayout` (24px padding, maks 720 ortalanmış).
  - Header: mikrofon ikonu (SVG/glyph, emoji değil) + `DisplayLarge` "Merhaba, {ad}" + `CaptionText` alt.
  - Bölüm 1 "GENEL BAKIŞ": 4 `GlassPanel` stat kartı (2×2), her biri `DisplayLarge` rakam + `CaptionText` etiket; rakam renkleri: toplam=accent, tamamlanan=secondary(turkuaz), bekleyen=warning, sesli=danger — **vurgu ekonomisi kontrolü** (yalnız bu 4 vurgu rengi).
  - Bölüm 2 "HIZLI ERİŞİM": 2 `Card` (solid, cam değil — aksiyon kartları) → tıklanabilir, hover yükselme + gölge, ikon + `SubtitleMedium` + `CaptionText`.
  - Footer: ghost "Yenile" + versiyon micro-label.
- **Empty/loading:** yüklenirken 4 skeleton `GlassPanel` (opacity pulse); hata varsa inline mesaj kartı.
- **Dark:** aynı yapı; rakam renkleri Dark* token'ları (turkuaz glow yalnız vurgu kartında).

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

| Component | Kaynak | Kullanım | State'ler |
|-----------|--------|----------|-----------|
| `GlassPanel` | ✔ var (Controls/) | cam kart, sticky bar, mikrofon paneli | — (statik) |
| `GlassBar` | plan | üst sticky arama/filtre, alt ekleme çubuğu | cam + border |
| `Card` | `Style x:Key="Card"` var | solid aksiyon kartı, detay kartı | hover yükselme |
| `GlassCard` | `Style x:Key="GlassCard"` var | liste satırı, bölüm kartı | hover parlaklık |
| `StatCard` | `Style x:Key="StatCard"` var | istatistik hücresi (Main/Settings) | — |
| `PrimaryButton` | default Button style | ana CTA | normal/hover/pressed/disabled/focused |
| `GhostButton` | `Style x:Key="GhostButton"` var | ikincil | + |
| `IconButton` | `Style x:Key="IconButton"` var | ikon aksiyonları | + |
| `PillTag` | plan | öncelik/durum chip'leri | seçili/seçili değil |
| `SegmentedFilter` | plan (KN-2) | filtre + tema segmentleri | kayan accent çubuk |
| `BreathRing` | plan (1.2) | mikrofon nefes halkası | idle/listening/processing/recognized/failed |
| `WaveLine` | plan (1.2) | canlı amplitude + oynatma dalgası | recording/playback |
| `TaskRow` | plan (1.2c) | liste satırı | normal/completed(tick)/swipe-to-delete |
| `EmptyState` | plan | boş liste | — |
| `PermissionSheet` | plan (1.3) | mikrofon/KVKK onay | — |
| `ToastInApp` | plan | sync/success/error bildirim çubuğu (DisplayAlert yerine) | — |
| `SkeletonCard` | plan (1.4) | loading iskeleti | pulse |

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

- **Kaynak:** `Colors.xaml` (yeni sözlük) + `design-system.md §2` — Light ve Dark **ayrı deneyim** (dark ≠ light'ın koyusu).
- **Light:** `Background #F6F8FC` (çok açık soğuk nötr) + `Surface #FFFFFF` + cam `#99FFFFFF`; metin `TextPrimary #0F1B2E`; accent `#2563EB`; gölgeler mavi-alt tonlu (`ShadowSm #140F1B2E`).
- **Dark:** `Background #0B1220` (grafit/indigo, tam siyah değil) + cam `#0FFFFFFF` + accent glow `#5B8CFF`; metin buz beyazı `#E9EEF8`; gölgeler siyah tabanlı.
- **Kural:** metin cam üzerinde daima `TextPrimary/Secondary` (okunabilirlik asla feda edilmez — Liquid Glass ilkesi 5); vurgu renkleri (turkuaz) yalnız state/motion/success; accent her yerde ana karakter.
- **Aykırılıklar (audit A7, A11):** Login solid mavi + todo ekleme turkuaz → düzeltilecek.
- Tema seçici: Settings'te `ThemeOptions` (Açık/Koyu/Sistem) UI'a bağlanmalı (KN-4).

---

## 6. Liquid Glass Kullanım Kuralları (taslak — onaylanacak)

| Alan | Kullanım |
|------|----------|
| **Zemin** | `Background` token, yarı saydam değil; Mica arkadan parlar (BackdropService) |
| **Sticky bar / overlay / mini oynatıcı** | cam — HER ZAMAN |
| **Ana içerik kartları** | cam — içerik yoğun liste/detay dahil; zeminle kontrast için `GlassBorder` |
| **Aksiyon kartları (tıklanabilir)** | `Card` (solid) — cam değil; hover/gölge netliği |
| **Mikrofon paneli / canlı transkripsiyon** | cam + BreathRing/WaveLine |
| **Form input'ları** | `Surface` (solid) — cam üzerinde okunabilirlik; radius-pill |
| **Modal/sheet** | cam panel yukarı kayarak (transition-framework §5) |
| **KULLANILMAZ** | tüm sayfa yüzeyleri, uzun metin blokları (perf + okunabilirlik) |
| **Perf** | blur yalnız pencere (Mica); panel blur yok; `Low/Medium` tier'da cam simülasyonuna düş |

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

## 8. Karar Noktaları (KAPALI — onaylandı, 2026-08)

- **KN-1 ✅** İkon glyph seti: **Segoe Fluent Icons** (Windows native; font gömülür).
- **KN-2 ✅** Filtre bileşeni: **`SegmentedFilter`** (kayan accent çubuk).
- **KN-3 ✅** WaveLine: **mikrofon paneli + oynatma progress** (iki yerde).
- **KN-4 ✅** Tema değişimi: **300ms crossfade** (1.4'te uygulanır).
- **KN-5 ✅** Swipe-to-delete: **yok**; silme butonla (onay sheet).
- **KN-6 ✅** Login: **şimdilik bekle**, 1.3'te onboarding ile yeniden değerlendirilir; prototip akışı (AppShell) korunur.

---

## 9. Onay & Uygulama Sırası (design-system §10 ile uyumlu)

1. **Bu blueprint onayı** (karar noktaları + öncelik listesi netleşir).
2. KN-1/2 onayı → 1.2c yaygınlaştırma (TodoList → Settings → TodoDetail → Login).
3. `WaveLine`/`BreathRing` (1.2 tamamlama) → voice state bağlama.
4. 1.3 Onboarding & izinler → 1.4 mikro-etkileşimler → 1.5 kalite kapıları.
5. Regression guard + build + `app.log` boş + elle çökme testi.
