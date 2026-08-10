# UI Viewport Contract — Todo Voice

Bu doküman UI'ın **gerçek ölçümlere dayalı** responsive sözleşmesidir.
Tahmin ürünü değildir; geliştirme makinesindeki gerçek uygulama penceresi ölçülmüştür.

## 1. Ölçümler (10 Ağu 2026 — gerçek uygulama)

| Metrik | Değer |
|---|---|
| Pencere (Window) | 1152 × 587 |
| Client (kullanılabilir) | **1136 × 579** |
| Ekran çözünürlüğü | 1536 × 864 |
| WorkArea | 1536 × 816 |
| Shell üst çubuğu | ~48 px (client'tan düşer) |

**Hesaplanan içerik viewport'u: ~1136 × 531 px** (Shell bar düşüldükten sonra).

## 2. Viewport sınıfları

| Sınıf | Genişlik | Kullanıcı Senaryosu |
|---|---|---|
| **Compact** | < 700 | Küçük pencere, yan tarafta snap |
| **Target** | 700–1400 | Mevcut geliştirme penceresi (1136) |
| **Expanded** | > 1400 | Geniş pencere, maximize (1536) |

## 3. Ortak kurallar (tüm sayfalar)

- **Horizontal padding:** 24 px (Compact 20, Expanded 32–48)
- **Content max-width:** 960 px; Expanded'ta içerik ortalanır, dışta atmosfer görünür
- **Vertical spacing:** 12–16 px kart arası
- **Scroll kuralları:**
  - `ScrollView` YALNIZCA içerik doğası gereği viewport'u aşıyorsa.
  - Layout'u gizlemek için scroll **YASAK**.
- **Zemin:** Sayfalar opak değil; pencere Mica + katmanlı gradient/glow atmosferi.
- **Cam:** yarı saydam yüzey + specular kenar + gölge; gri kutu değil.

## 4. Sayfa sözleşmeleri

| Sayfa | Compact | Target | Expanded | Scroll | Sabit (fixed) | Esneyen |
|---|---|---|---|---|---|---|
| **MainPage** | kompozisyon dikey sıkışır; hero küçülür | tek viewport kimlik ekranı | merkezlenir, max-width | tercihen yok | alt aksiyon bölgesi | hero + istatistik oranı |
| **TodoList** | filtreler compact chip'e döner | kontrol üstte sabit, liste scroll | daha geniş satır | sadece liste | üst kontrol + alt add-bar | liste |
| **TodoDetail** | stacked | normal | max-width merkez | içerik gerçekten uzunsa | yok | form alanı |
| **Settings** | stacked | normal | 2 sütun grid | sayfa (gerçek taşma) | üst başlık | bölüm kartları |
| **Login** | centered tek sütun | centered | centered max-width | yok | merkez | kart |

## 5. Sayfa bazlı davranış detayları

### 5.1 MainPage — "tek ekran kimlik"
- `Grid RowDefinitions="Auto,*,Auto"`: üst kimlik / orta hero+istatistik / alt aksiyon.
- Hero bölümü (ortada): marka mesajı + birincil voice aksiyon.
- İstatistikler hero'nun altında 2'li grid; Compact'ta 1 satıra sığar, daha kısa.
- Alt sabit aksiyon çubuğu `Surface` zeminli, `CornerRadius="0"`.
- Expanded: içerik `HorizontalOptions=Center`, max 960; kenarlarda atmosfer.
- **Scroll YOK.**

### 5.2 TodoList
- `Grid RowDefinitions="Auto,Auto,*,Auto"`: sync / kontrol / liste / add-bar.
- Kontrol bölgesi (arama+filtre+öncelik) **sabit** üstte.
- Liste (`CollectionView`) yıldızlı satırda **scroll eder** — doğal davranış.
- Alt add-bar sabit.
- Compact: öncelik/sıralama picker'ları alt alta; Expanded: tek satır yan yana.

### 5.3 TodoDetail
- İçerik uzunsa scroll (mevcut davranış korunur) — içerik gerçekten uzayabilir.
- Expanded: max-width 720, ortalanmış.

### 5.4 Settings
- Başlık sabit üstte; bölüm kartları scroll edebilir (içerik doğal olarak uzun).
- Expanded: 2 sütun grid ile taşma azaltılır.

### 5.5 Login
- Her boyutta ortalanmış tek kart; scroll yok.

## 6. Kalite kapıları (her sayfa için)

| # | Test | Geçer |
|---|---|---|
| A | İlk bakışta ana içerik ve birincil aksiyon görünüyor mu? | Evet |
| B | Grayscale'de hierarchy korunuyor mu? | Evet |
| C | Cam kaldırılınca layout bozulmuyor mu? | Evet |
| D | Scroll yalnızca doğal taşmada mı? | Evet |
| E | Navigation içerikten daha fazla mı dikkat çekiyor? | Hayır |
| F | Compact/Target/Expanded'da ayrı ayrı doğrulandı mı? | Evet |

## 7. KRİTİK MAUI KURALI — LineHeight çarpımsaldır

`Label.LineHeight` MAUI'de **piksel değil, çarpımsaldır** (1.0 = normal).
`LineHeight="38"` yazmak **38 kat satır yüksekliği** demektir → 32pt label 1216px
ölçülür → tüm layout sayfa dışına taşar, `IsClippedToBounds` yüzünden içerik
kesilir ve "hero altta kalıyor" görüntüsü oluşur.

Doğru kullanım: `LineHeight="1.2"` (1.15–1.4 arası).

**2026-08-11 ölçümü:** Bu hata tüm Styles.xaml tipografi stillerinde vardı ve
10 kat şişmeye yol açıyordu. Düzeltildikten sonra gerçek ölçümler:

| Bölge | Önce (hatalı) | Sonra (doğru) |
|---|---|---|
| Header | 563 px | 39 px |
| Hero Y | 577 px (sayfa dışı) | 128 px (ortada) |
| Stats Y | 2199 px (sayfa dışı) | 373 px (görünür) |
| Label 11pt | 154 px | 15 px |
| Label 32pt | 1216 px | 41 px |

## 8. Ölçüm tarihi
- Son ölçüm: 2026-08-10 (uygulama çalışırken `GetWindowRect`/`GetClientRect`)
- 2026-08-11: LineHeight düzeltmesi sonrası PageH=499, tüm içerik sığıyor.
- 2026-08-11: Tema yeniden tasarımı (Light "Morning Pearl" / Dark "Obsidian Aurora").
- Pencere yeniden boyutlandırılırsa contract güncellenmelidir.

## 9. TEMA SÖZLEŞMESİ (2026-08-11)

**İlke: cam, arkasındaki renkli ışıkla görünür.** Zemin asla düz değildir.

| Katman | Light "Morning Pearl" | Dark "Obsidian Aurora" |
|---|---|---|
| Zemin gradient | krem → gül-mist → indigo-mist | obsidyen → derin → indigo-tinged |
| Ambient glow'lar | şeftali / lavanta / gökyüzü / nane | indigo / amethyst / turkuaz / mor |
| Aksan | mürekkep indigo `#4F46E5` | lüminesan indigo `#8B93FF` |
| İkincil | premium turkuaz `#0D9488` | parlak turkuaz `#2DD4BF` |
| Cam yüzey | sıcak beyaz gradient (üstte ışık) | indigo tonlu beyaz gradient (görünür) |
| Kart tint'leri | indigo / yeşil / turkuaz / mor | aynı renk ailesi, koyu değerler |

**Kurallar:**
- Tüm sayfalar ortak `Aurora*` / `DarkAurora*` token'larını kullanır (inline hex yok).
- Cam yüzeyler `GlassSurface*` + `GlassTint*` paylaşılan fırçalarından gelir.
- İstatistik kartlarının her biri kendi ambient rengini yansıtır (tint'li cam).
- `--theme=light|dark|system` komut satırı argümanı tema doğrulaması içindir.

**AMBIENT OPACITY KURALI (2026-08-11):**
Arka plan glow blob'ları TEMA-BAĞIMLI opacity kullanır:
- **Light: 0.34–0.42** — görünür pastel aurora (beyaz zemine uyarlanmış).
- **Dark: 0.45–0.6** — güçlü kromatik ambient (camın arkasındaki ışık).
Aynı nesne iki temada farklı yoğunlukta olur; light'ta asla koyu leke değil.
MIC glow halkaları: Light 0.55–0.6 / Dark 1.0.

**ZEMİN GARANTİSİ KURALI (2026-08-11):**
Sayfa `BackgroundColor` asla `Transparent` DEĞİLDİR; doğrudan tema token'ına
bağlıdır (`Background` / `DarkBackground`). Böylece gradient render edilmese
bile arka plan garanti doğru renkte olur (Light: açık gri #F4F5F8, Dark: obsidyen).
Aurora gradient'i + glow'lar bu garantili zeminin ÜZERİNE biner.

**GRADIENT FADE KURALI (2026-08-11):**
Gradient fade'lerde `Transparent` (#00000000) YASAK — ortaya siyah smear çizer.
Her renk için alpha-0 'Clear' token kullan: `#00RRGGBB`.

**GRADIENT BRUSH KURALI (2026-08-11):**
`GradientStop.Color` içinde `AppThemeBinding` RENDER EDİLMEZ (Grid arka planı
şeffaf kalır → koyu pencere görünür). Tüm sayfa/glow gradient'leri hazır
brush çiftlerine (`AuroraBackgroundLight/Dark`, `Glow*Light/Dark`) bağlanır.

**Doğrulama (2026-08-11):** Dark ve Light tema ayrı ayrı başlatıldı; her ikisinde
de PageH=499, taşma yok. Screenshot: `redesign_dark.png`, `redesign_light.png`.
