# INDEX.md — Todo Voice Dokümantasyon Haritası

Bu dosya, `.claude/` klasöründeki tüm dokümantasyonun **giriş kapısıdır**.
Bir ajan bu klasöre ilk adım attığında bu dosyayı okur ve yoluna devam eder.

---

## 1. Nereden Başlarım?

| Durum | Dosya |
|-------|-------|
| Projeye yeni başladım, kuralları öğrenmek istiyorum | **`AGENT.md`** — önce bunu oku (bağlayıcı, regression guard dahil) |
| "Şu an ne çalışıyor?" diye soruyorum | **`learning.md`** |
| Mimari / teknik kararlar (ADR) | **`architecture.md`** |
| "Sırada ne var?" (aktif iş listesi) | **`roadmap.md`** |
| Görsel/tasarım kararı alacağım | **`design-system.md`** (palet/tipografi/cam) |
| Animasyon / Liquid Glass efekti yapacağım | **`transition-framework.md`** (hikaye odaklı, MAUI) |

---

## 2. Dosya Envanteri

| Dosya | Rol | Güncellenir ne zaman? |
|-------|-----|-----------------------|
| `AGENT.md` | AI anayasası, güven seviyeleri, regression guard | Kural/karar yetkisi değişince |
| `architecture.md` | Mimari + ADR'ler + build ortamı | Mimari/teknik karar değişince |
| `learning.md` | Şu an çalışan özellikler (aktif) | Özellik eklendi/kaldırıldı/bozuldu |
| `roadmap.md` | Yol haritası + aktif sprint iş listesi | Görev eklenince/tamamlanınca |
| `design-system.md` | Tasarım sistemi (renk/tipografi/boşluk/cam) | Tasarım kararı değişince |
| `transition-framework.md` | Animasyon + Liquid Glass altyapısı | Animasyon/efekt kararı değişince |

---

## 3. Okuma Kuralı (AGENT.md özeti)

1. Önce **AGENT.md** okunur (bağlayıcı kurallar).
2. Görevle ilgili dokümanlar (`architecture.md`, `learning.md`,
   `roadmap.md`, `design-system.md`, `transition-framework.md`) okunur.
3. Görev tamamlanınca **ilgili dokümanlar güncellenir** (doküman sahibi ol).
4. Regression guard (AGENT.md): her görev bitişi 4 soru — XAML/MAUI 8
   kısıtları, ses/transkripsiyon akışı, sync/local-first, tema.
5. **Audit → Drift Detection → Mini-Slice** (AGENT.md §6.2): mimari/sınır ihlali
   fark edilince uygulamaya devam etmeden önce kısa audit yapılır; sapma en küçük
   mini-slice ile düzeltilir, build doğrulanır, doküman güncellenir.
6. `C:\Users\sniya\Desktop\chicky-pos` **yalnız okunur** — asla değiştirilmez.

---

## 4. Harici / Repo Dışı Kaynaklar

| Kaynak | Yol | Amaç |
|--------|-----|------|
| Test kimlikleri / token | `C:\temp\opencode\test-creds.txt` | Supabase test |
| Supabase fonksiyon logları | `C:\temp\opencode\functions-serve.log` / `.err` | Edge fn hata takibi |
| Tasarım ilhamı | `C:\Users\sniya\Downloads\Compressed\front-end.md` | Ayırt edici tasarım ilkeleri |
| Animasyon ilhamı (chicky-pos) | `C:\Users\sniya\Desktop\chicky-pos\.claude\transition-framework.md` | Hikaye odaklı geçiş felsefesi (OKUMA) |
| Skill (ui-ux-pro-max) | `C:\Users\sniya\Desktop\chicky-pos\.claude\skills\ui-ux-pro-max-skill` | Tasarım kataloğu (OKUMA) |

---

*Bu harita dağılır/dosya eklenirse güncellenir.*
