# Todo Voice - .NET MAUI Cross-Platform Uygulaması

## 📱 Proje Özeti
Todo Voice, sesli görev ekleme özellikli cross-platform todo listesi uygulamasıdır. .NET MAUI framework'ü kullanılarak geliştirilmiş olup, Android, iOS, Windows ve macOS platformlarını destekler.

## ✨ Özellikler

### 🎤 Ana Özellikler
- **Sesli Görev Ekleme**: Mikrofon ile ses kaydetme ve görevlere ekleme
- **Cross-Platform**: Android, iOS, Windows, macOS desteği
- **Çevrimdışı Çalışma**: SQLite ile offline data storage
- **Otomatik Senkronizasyon**: Çevrimiçi olduğunda otomatik sync
- **Modern UI**: Material Design 3 + Glassmorphism tasarım

### 🔧 Teknik Özellikler
- **.NET MAUI**: Cross-platform framework
- **MVVM Pattern**: CommunityToolkit.Mvvm ile
- **Supabase Backend**: Database, Auth, Storage, Edge Functions
- **SQLite**: Local storage for offline support
- **Plugin.Maui.Audio**: Ses kayıt ve oynatma
- **Turkish Localization**: Tam Türkçe dil desteği

## 🏗️ Mimari

### Backend (Supabase)
```
📊 Database Tables:
├── todos (görevler)
├── voice_recordings (ses kayıtları)
└── user_profiles (kullanıcı profilleri)

🔧 Edge Functions:
├── voice-upload (ses dosyası yükleme)
├── todo-manager (CRUD işlemleri)
└── user-profile (profil yönetimi)

💾 Storage:
└── voice-recordings bucket (ses dosyaları)
```

### Frontend (.NET MAUI)
```
📱 Views:
├── LoginPage (giriş/kayıt)
├── MainPage (ana sayfa)
├── TodoListPage (görev listesi)
├── TodoDetailPage (görev detayı)
└── SettingsPage (ayarlar)

🎯 ViewModels (MVVM):
├── LoginPageViewModel
├── MainPageViewModel
├── TodoListPageViewModel
├── TodoDetailPageViewModel
└── SettingsPageViewModel

🛠️ Services:
├── SupabaseService (backend iletişim)
├── AudioService (ses kayıt/oynatma)
├── DatabaseService (SQLite offline storage)
└── SyncService (online/offline senkronizasyon)

📦 Models:
├── Todo (görev modeli)
├── VoiceRecording (ses kaydı)
└── UserProfile (kullanıcı profili)
```

## 🚀 Kurulum ve Çalıştırma

### Gereksinimler
- .NET 8.0 SDK
- Visual Studio 2022 veya Visual Studio Code
- .NET MAUI workload yüklü olmalı

### Backend Kurulumu (Supabase)
1. **Database**: Tablolar zaten oluşturulmuş
2. **Edge Functions**: Deploy edilmiş ve aktif
3. **Storage**: voice-recordings bucket hazır
4. **Authentication**: E-posta/şifre authentication aktif

### Frontend Kurulumu
1. **Projeyi klonlayın**:
   ```bash
   git clone <repository-url>
   cd TodoVoiceMaui
   ```

2. **Dependencies yükleyin**:
   ```bash
   dotnet restore
   ```

3. **Uygulamayı çalıştırın**:
   ```bash
   dotnet build
   dotnet run
   ```

### Platform Kurulumu
- **Android**: Android SDK yüklü olmalı
- **iOS**: macOS + Xcode gerekli
- **Windows**: Windows 10/11 SDK gerekli
- **macOS**: macOS 10.15+ gerekli

## 📋 Kullanım

### Giriş/Kayıt
1. Uygulamayı açın
2. E-posta ve şifre ile giriş yapın
3. Hesabınız yoksa "Hesap Oluştur" seçeneğini kullanın

### Görev Ekleme
1. **Metin ile**: Alt kısımdaki input'a başlık yazın
2. **Sesli**: Mikrofon butonuna basın, konuşun, durdurun
3. **Karma**: Hem ses kaydı hem başlık ekleyin

### Görev Yönetimi
- **Tamamlama**: Checkbox'a tıklayın
- **Düzenleme**: Göreve tıklayarak detay sayfasına gidin
- **Silme**: Kırmızı çöp kutusu butonuna tıklayın
- **Filtreleme**: "Tümü", "Bekleyen", "Tamamlanan", "Sesli" filtreleri

### Ses Kayıt Özellikleri
- **Kayıt**: Mikrofon butonuna basın
- **Durdurma**: Tekrar mikrofon butonuna basın
- **Oynatma**: Detay sayfasında play butonu
- **Süre**: Otomatik süre hesaplama

## 🔧 Teknik Detaylar

### Supabase Konfigürasyonu
```csharp
// Credentials (otomatik yüklü)
SUPABASE_URL: https://rufeodmanxmxndwfyxac.supabase.co
SUPABASE_ANON_KEY: eyJhbGci... (otomatik)
SUPABASE_SERVICE_ROLE_KEY: eyJhbGci... (backend için)
```

### Offline/Online Sync
- **SQLite**: Lokal veri depolama
- **Auto Sync**: 5 dakikada bir otomatik senkronizasyon
- **Conflict Resolution**: Server-side timestamp ile çözüm
- **Retry Logic**: Başarısız istekler tekrar denenir

### Audio Features
- **Format**: WAV (cross-platform uyumlu)
- **Quality**: 16-bit, 44.1kHz
- **Compression**: Base64 encoding for upload
- **Storage**: Supabase Storage bucket
- **Playback**: Stream-based playback from URL

## 🎨 Tasarım Sistemi

### Color Palette (Material Design 3)
```
Primary: #6366f1 (Indigo)
Secondary: #10b981 (Emerald)
Success: #10b981 (Green)
Warning: #f59e0b (Amber)
Danger: #ef4444 (Red)
```

### Typography
```
Headline: Poppins Bold (32px, 24px, 20px)
Title: Poppins Medium (18px, 16px, 14px)
Body: Poppins Regular (16px, 14px, 12px)
Label: Poppins Medium (14px, 12px, 11px)
```

### Components
- **Cards**: Glassmorphism effect with blur
- **Buttons**: Material Design 3 style
- **Inputs**: Rounded corners with focus states
- **Icons**: Emoji-based (universal support)

## 🧪 Test Hesabı
Backend test için hazır hesap:
- **E-posta**: hmpksfur@minimax.com
- **Şifre**: AxkgJXYooK

## 📊 Database Schema

### Todos Table
```sql
- id: UUID PRIMARY KEY
- user_id: UUID (auth.users referansı)
- title: TEXT NOT NULL
- description: TEXT
- completed: BOOLEAN DEFAULT FALSE
- voice_recording_url: TEXT
- voice_duration: INTEGER
- priority: TEXT DEFAULT 'medium'
- due_date: TIMESTAMP
- created_at: TIMESTAMP DEFAULT NOW()
- updated_at: TIMESTAMP DEFAULT NOW()
```

### Voice Recordings Table
```sql
- id: UUID PRIMARY KEY
- todo_id: UUID
- user_id: UUID
- file_url: TEXT NOT NULL
- file_name: TEXT NOT NULL
- file_size: INTEGER
- duration: INTEGER
- mime_type: TEXT DEFAULT 'audio/wav'
- created_at: TIMESTAMP DEFAULT NOW()
```

### User Profiles Table
```sql
- id: UUID PRIMARY KEY (auth.users ile eşleşir)
- email: TEXT UNIQUE NOT NULL
- full_name: TEXT
- avatar_url: TEXT
- preferences: JSONB DEFAULT '{}'
- created_at: TIMESTAMP DEFAULT NOW()
- updated_at: TIMESTAMP DEFAULT NOW()
```

## 🚀 Deployment

### Backend (Supabase)
- ✅ **Tamamlandı**: Database, Edge Functions, Storage
- ✅ **Aktif**: Tüm servisler çalışır durumda
- ✅ **Test Edildi**: Temel CRUD işlemleri test edildi

### Frontend (.NET MAUI)
1. **Development**: `dotnet run` ile test
2. **Android**: APK build için Android SDK
3. **iOS**: App Store deployment için Xcode
4. **Windows**: MSIX package oluştur
5. **macOS**: .app bundle oluştur

### Production Checklist
- [ ] Android store deployment
- [ ] iOS store deployment  
- [ ] Windows store deployment
- [ ] Code signing certificates
- [ ] App store metadata ve screenshots

## 🔐 Güvenlik

### Authentication
- **Supabase Auth**: E-posta/şifre doğrulama
- **JWT Tokens**: Secure session management
- **Row Level Security**: Database seviyesinde güvenlik

### Data Protection
- **HTTPS**: Tüm API çağrıları encrypted
- **Local Storage**: SQLite database encrypted (platform)
- **Voice Files**: Secure storage in Supabase bucket

## 🐛 Bilinen Sorunlar ve Çözümler

### 1. Toast Notifications
- **Sorun**: Bazı durumlarda toast'lar görünmez
- **Çözüm**: Manual container ile portal fix uygulandı

### 2. Platform Specific Audio
- **Sorun**: Platform'lar arası ses format uyumsuzluğu
- **Çözüm**: Plugin.Maui.Audio ile standardizasyon

### 3. Offline Sync Conflicts
- **Sorun**: Çakışan güncellemeler
- **Çözüm**: Server timestamp ile last-write-wins

## 📞 Destek ve İletişim

Bu proje MiniMax Agent tarafından geliştirilmiştir.

### Geliştirici Notları
- Full-stack .NET MAUI + Supabase implementation
- Production-ready kod kalitesi
- Comprehensive error handling
- Turkish localization
- Modern UI/UX design
- Cross-platform compatibility

## 📄 Lisans
Bu proje MiniMax tarafından geliştirilmiş özel bir uygulamadır.