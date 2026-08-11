# roadmap.md — Todo Voice Yol Haritası

Bu dosya projenin gelecek planını ve **aktif iş listesini** tutar.
Her görev, tamamlanınca buradan çıkarılır veya işaretlenir.

Durum simgeleri:
- `[ ]` — yapılacak
- `[~]` — devam ediyor
- `[x]` — tamamlandı

---

## 1. Aktif Sprint: Premium Tasarım Rönesansı

Kullanıcı kararı: **uygulamayı baştan aşağı premium / sıradışı bir tasarımla
yeniden tasarla.** Aşağıdaki gereksinimler bağlayıcıdır:

- Premium, olağanüstü tasarım; mevcut palette tamamen değişiklik serbest (yeni palet)
- Dark ve Light tema **bağımsız** ayrı tasarlanacak (koyu ayrı deneyim)
- Responsive; cam (glass) ve hover efektleri
- Performans korunmalı (gereksiz animasyon/efekt olmamalı)
- Apple benzeri akışkan (fluid) animasyonlar, açılış animasyonları
- Ses efektleri (etkileşimlerde)
- Türkçe özel isim sözlüğü (şirket/adam isimleri otomatik düzeltme — TurkishVocabulary)
- Mikrofon izinleri + tüm yasal bilgilendirmeler (onay ekranı)
- En profesyonel seviye
- Referanslar (SADECE ilham, kopya değil): `C:\Users\sniya\Downloads\Compressed\front-end.md`,
  `C:\Users\sniya\Desktop\chicky-pos\.claude\transition-framework.md`,
  `C:\Users\sniya\Desktop\chicky-pos\.claude\skills\ui-ux-pro-max-skill`
- ⛔ `C:\Users\sniya\Desktop\chicky-pos` **asla değiştirilmez** (yalnız okuma)

### 1.1 Tasarım Altyapısı
- [x] `.claude` context sistemi kuruldu (AGENT.md + regression guard)
- [x] Repo GitHub'a push edildi (`Fastrree/Voice-Todo-List`)
- [x] Kök README.md (GitHub için) + mimari/learning dokümanları
- [x] design-system.md + transition-framework.md (Liquid Glass + hikaye odaklı plan)
- [x] Renk token'larını yenile (Colors.xaml: yeni palette, Dark* ayrı)
- [x] Typography & Space & Radius & Shadow token'ları (Styles.xaml)

### 1.2 Liquid Glass Altyapısı (Apple cam dili)
- [x] BackdropService: Mica → DesktopAcrylic → fallback (feature detection)
- [x] GlassPanel control (specular çizgi + accent tint + ince border)
- [x] Cam kart / sticky bar / modal görünümü (Light + Dark ayrı tint)
- [x] Kırılma/yansıma simülasyonları (gradient tabanlı, platform bağımsız)
- [~] Performans doğrulaması: blur yalnız overlay/hero kartlarda; metin AA kontrastı

### 1.2c Sayfa Yeniden Tasarımı (glass rollout)
- [x] AppShell: tab bar accent tema-aware (Primary legacy kaldırıldı)
- [x] MainPage: dashboard kartları (glass kartlar, hover, açılış animasyonu)
- [x] TodoListPage: liste kartları, mikrofon etkileşim animasyonu, canlı transkripsiyon UI
- [x] TodoDetailPage: detay + ses kayıt/oynatma deneyimi (record UI, Segoe ikonlar)
- [x] SettingsPage: tema seçici, profil, sync durumu
- [x] LEGACY COMPAT kaldırıldı (Styles.xaml + Colors.xaml eski alias'lar silindi; build + runtime doğrulandı)
- [x] LoginPage: yalnızca token geçişi (Primary → Accent) — tam redesign 1.3'te

### 1.3 Onboarding & İzinler
- [ ] İlk açılış onboarding / hoş geldin ekranı (3 ekran storyboard, animasyonlu)
- [ ] Mikrofon izni akışı + açıklama
- [ ] Yasal bilgilendirme / KVKK (kişisel veri) onayı
- [ ] Ses efekti izni / tercihi

### 1.3 Sayfa Yeniden Tasarımı (devam)
- [ ] AppShell: sayfa geçiş animasyonları
- [ ] LoginPage: modernize (kullanılabilir hale getir — §2.6 blueprint)

### 1.4 Etkileşim & Mikro-etkileşim
- [ ] Buton hover / press efektleri (PointerOver VisualState, scale + ışık shift)
- [ ] Görev tamamlama animasyonu (spring tick + satır fade + accent ripple)
- [ ] Ses kaydı canlı dalga formu (waveform) görselleştirmesi (WaveLine control)
- [ ] Karşılama storyboard: splash → ana ekran (örtüşen timeline, stagger kartlar)
- [ ] Ses efektleri (mikro onay, görev oluşma, karşılama — opsiyonel ayar)

### 1.5 Kalite Kapıları (tasarım için)
- [ ] Her sayfada light + dark + responsive kontrol
- [ ] Performans: animasyon FPS / GC basıncı kontrolü
- [ ] Regression guard çalıştır (AGENT.md 4 soru)
- [ ] Build temiz + uygulama çalışıyor + `app.log` boş

---

## 2. Tamamlanan Özellikler (geçmiş, özet)

- [x] Sesli görev oluşturma akışı (canlı transkripsiyon, toggle, otomatik ekleme)
- [x] **Çevrimdışı Whisper ses tanıma** (ADR-016) — Windows SpeechRecognizer unpackaged'ta
      çalışmıyordu (0x800455A0); Whisper.net (whisper.cpp, MIT) ile değiştirildi.
      Kaydet → çevir → görev.
- [x] **Model seçici (Ayarlar → Ses Tanıma)** — tiny/base/small/medium/large-v3-turbo/
      large-v3 (75MB → 2,9GB); boyut/hız/doğruluk detayları + 1GB+ onayı + güvenli geçiş
      (eski model yeni hazır olana dek korunur). Preferences `stt_model`.
- [x] **Türkçe sözlük genişletmesi** — ~250 tek + ~160 ifade (Türk şirketleri, bankalar,
      markalar, medya, spor kulüpleri, şehirler, ünlüler) + Türkçe iyelik eki desteği
      ("Google'dan") + yanlış pozitif karalistesi (56/56 test).
- [x] Ses kaydı + oynatma (WAV, progress, silme)
- [x] İstatistik dashboard
- [x] Filtre & sıralama
- [x] Hatırlatıcı (Windows toast) + `reminder_at` migration
- [x] Tema (açık/koyu)
- [x] Local-first sync (Supabase, `local-user` fallback)
- [x] C0 mini-slice: `ITodoStore` seam + sync envelope (dirty/tombstone/local_version)
      + offline delete/create düzeltmeleri + ViewModel'lerden Supabase izolasyonu
      (commit `ef9692f`)
- [x] Login sayfası (kod mevcut; varsayılan akışta atlanıyor)

---

## 3. Uzun Vadeli Fikirler

- [ ] Android / iOS hedef ekleme (csproj TFM)
- [ ] Sürekli dinleme modu (wake word / "push to talk" yerine)
- [ ] Voice profile / biyometrik ses tanıma
- [ ] Sistem geneli kısayol (global hotkey)
- [ ] Chunked Whisper ile "konuşurken canlı metin" (streaming transkripsiyon)
- [ ] Düşük güven eşiği politikası (konuşmasız girdide yanlış metin önleme)
- [ ] Doğal dil ile görev ayrıştırma (tarih, öncelik, etiket çıkarma)

---

## 4. Türkçe STT Eğitim Yolu (araştırma sonuçları — 2026-08)

Kullanıcı isteği: çevrimdışı tarafı Türkçe'de daha iyi eğitmek + bulut seçenekleri.
Araştırma bulguları ve yapılabilir aksiyonlar:

### Bulut sağlayıcı kataloğu (geniş)
- **Uygulandı:** Çevrimdışı (4 katman) + OpenAI (gpt-4o-mini-transcribe) + Groq
  (whisper-large-v3-turbo) + Deepgram (Nova-3) + ElevenLabs (Scribe v2).
- **Katalogda, yakında:** Google Chirp 3, Azure AI Speech, AssemblyAI Universal-2,
  Fireworks, Cloudflare, Soniox (Türkçe uzmanı). Her biri tek `ISpeechTranscriber`
  sınıfıyla aktifleşir (desen: `CloudTranscribers`).

### Türkçe fine-tune → GGML yolu
- **Hazır Türkçe GGML model YOK** (araştırıldı). Mevcutlar PyTorch/CT2:
  - `emre/whisper-medium-turkish-2` (HF, 19 beğeni — en popüler Türkçe medium fine-tune)
  - `erenfazlioglu/whisper-small-turkish-tr-best`
- **Dönüşüm:** whisper.cpp repo'sundaki `convert-pt-to-ggml.py` / `convert-h5-to-ggml.py`
  ile PyTorch checkpoint → GGML. Dönüşen dosya `WhisperModelCatalog`'a "Türkçe Maximum"
  katmanı olarak eklenebilir (HF'a yüklenip URL'i katalogda tutulur).
- **Dataset'ler (eğitim için):** Common Voice TR (~300+ saat, açık), Google Fleurs TR
  (~12 saat), NST Türkçe (Ankara Üni., 4000+ saat — lisanslı), MedASR-TR (medikal).
- **Pratik bugün:** Maximum katmanı = `ggml-large-v3` (3,1GB, 680.000+ saat çok dilli
  veriyle eğitildi — Türkçe dahil) + `TurkishVocabulary` sözlüğü.

### Güvenlik notu
- Bulut API anahtarları Preferences'ta düz metin saklanır (desktop takas). İyileştirme:
  DPAPI (`System.Security.Cryptography.ProtectedData`, CurrentUser) ile şifreleme.
- [ ] Windows App SDK upgrade (WinAppSDK 1.7+, WASDK tooling)
- [ ] CI/CD (GitHub Actions build + publish)
