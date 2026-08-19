# Hydra.Vitals — Unified AI & Mobile Diagnostics Knowledge Base

Mobil oyun ve uygulama projelerimizde (**Blocked**, **PaintTrek**, **PanzerLab**, **Bugjong**, **CosmoCrab** vb.) karşılaşılan **ANR**, **Crash (Çökme)**, **Memory Leak (Bellek Sızıntısı)**, **Cold Start / Hot Start Gecikmeleri** ve **Jank (Kare Düşüşü)** sorunlarının merkezi olarak arşivlendiği, çözümlerinin ve kök nedenlerinin modellendiği kurumsal bilgi bankasıdır.

---

## 🎯 Projenin Amacı

1. **Hızlı Teşhis & Eylem:** Benzer bir ANR veya Crash (örneğin `nativePollOnce`, `EglManager`, `SpriteFont`, `Environment.Exit`) tekrar ortaya çıktığında sıfırdan analiz yapmadan, test edilmiş çözüme ve derslere saniyeler içinde ulaşmak.
2. **AI Asistanı Entegrasyonu:** AI kodlama asistanlarının (Antigravity vb.) stack trace ve hata imzalarına göre geçmiş çözümleri sorgulayabileceği makine-okunur (JSON/Object) ve ilişkisel bir veri tabanı sağlamak.
3. **Regresyonları Önleme:** Savunma kodlarının neden yazıldığını, hangi kütüphanelerin (`libhwui.so`, `libmonosgen.so`, `libSystem.Native.so`) hangi katmanda patladığını kayıt altına alarak yanlışlıkla kodların silinmesini engellemek.

---

## 🏛️ Mimari ve Tasarım İlkeleri

Proje **SOLID**, **OOP**, **DRY** ve **YAGNI** prensiplerine tam uyumlu olarak tasarlanmıştır:

- **`BaseObject<T>`**: `Hydra.Core` mimarisinden türetilmiş, `Guid` tabanlı (UUIDv7/v4), denetim alanları (`AddedDate`, `ModifiedDate`), `RowVersion` ve eşitlik mantığını içeren temel nesne.
- **İlişkisel Nesne Modeli**:
  - `AppProject`: Proje bilgileri (Paket adı, hedef mağaza, kullanılan teknolojiler: MonoGame, C#, OpenAL vb.).
  - `VitalDevice`: Cihaz modelleri, üreticiler (Samsung, Vivo, Oppo, Motorola), Android sürümleri (8.1, 13, 14), SoC/GPU ve kütüphaneler.
  - `VitalIssue`: ANR ve Crash detayları, etki sayıları, stack signature kareleri, kök neden (`RootCause`), çözüm yaklaşımı (`FixApproach`) ve kritik çıkarımlar (`LessonsLearned`).
- **Veri Depolama (JSON DB & Repository Pattern)**:
  - `IVitalRepository<T>` generic arayüzü ile gevşek bağlılık (Loose Coupling).
  - `JsonDatabaseContext`: Thread-safe, asenkron okuma/yazma ve şema versiyonlama desteği.
- **Genişletilebilirlik**: İleride Console App'ten ASP.NET Core Web API / Blazor Web App'e dönüştürülmeye hazır servis katmanı (`IVitalAnalysisService`).

---

## 📊 Desteklenen Vital Türleri & Durumlar

### Türler (`VitalType`)
- ⏳ **ANR (Application Not Responding):** Input dispatching timeout, main thread lock contention, heavy I/O, slow service startup.
- 💥 **Crash:** Managed exceptions (JavaProxyThrowable, NPE) ve Native crashes (SIGABRT, __android_log_assert, EGL failures).
- 🧠 **MemoryLeak:** GC baskısı, bellek sızıntıları, doku temizlenememe sorunları.
- 🚀 **ColdStart / HotStart:** Uygulama açılış süreleri, varlık açma (inflate) darboğazları.
- 🎬 **Jank / Slow Render:** Pahalı kareler, GPU kilitlenmeleri.

### Durumlar (`VitalStatus`)
- `FixedVerified`: Düzeltildi ve sahada doğrulandı.
- `FixedAwaitingRelease`: Düzeltildi, bir sonraki yayında bekleniyor.
- `Mitigated`: Kök neden dışarıda; uygulama tarafında etkisi kademeli azaltıldı.
- `ClosedNotActionable`: Geç yığın dökümü (Late dump / nativePollOnce) veya aksiyon alınamaz.
- `ClosedOsDriverBug`: AOSP veya OEM sürücü seviyesinde donanım/framework hatası.
- `FrameworkMonitored`: MonoGame / .NET framework izleme altında.

---

## 🚀 Başlangıç ve Çalıştırma

Projeyi derlemek ve çalıştırmak için:

```bash
# Projeyi derleyin
dotnet build

# Konsol bilgi bankasını başlatın
dotnet run
```

---

## 🛠️ Konsol Kontrol Paneli Yetenekleri

1. **Tüm Vitals Kayıtlarını Listeleme:** Renkli konsol formatında durum ve önem seviyesine göre listeleme.
2. **Kategoriye Göre Filtreleme:** Sadece ANR'lar, sadece Crash'ler veya Performans kayıtları.
3. **Projeye Göre Filtreleme:** Blocked, PaintTrek, PanzerLab vb.
4. **Cihaz & Android Sürümü Arama:** Örneğin `Vivo`, `Samsung`, `Android 8.1`, `Android 14`.
5. **AI Akıllı Arama:** Stack trace veya metod adı (örn: `EglManager`, `nativePollOnce`, `SpriteFont`, `inflate_fast`) girerek geçmiş çözümleri anında getirme.
6. **Yeni Kayıt Ekleme:** İnteraktif sihirbaz ile yeni bir Vital vakasını veritabanına işleme.