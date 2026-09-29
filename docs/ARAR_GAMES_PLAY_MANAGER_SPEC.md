# Arar Games Play Manager — Şartname (İLERİYE DÖNÜK, HENÜZ YAPILMIYOR)

> **DURUM: PLANLI, UYGULANMADI.**
>
> Bu belge ileride yapılacak büyük projenin şartnamesidir. Şu an **hiçbir
> kısmı uygulanmıyor.** Bugün yapılan iş yalnızca `Hydra.Vitals` içindeki
> **crash + ANR çekme konsol aracıdır** (bkz. bölüm "Bugün yapılan").
>
> Belge burada duruyor ki karar ve gerekçeler kaybolmasın; büyük projeye
> geçildiğinde sıfırdan düşünmek gerekmesin.

---

## Bugün yapılan (bu şartnamenin dışında)

`Hydra.Vitals` zaten ANR/Crash bilgi bankası olarak var. Bugünkü ekleme:
Google Play Developer Reporting API'den **crash ve ANR** verisini çekip bu
bankaya yazan bir konsol aracı.

Neden burada: veri modeli (`VitalIssue`, `AppProject`, `VitalDevice`), JSON
deposu ve repository katmanı hazır. Aynı işi ayrı bir projede yapmak, aynı
kavramları ikinci kez modellemek olurdu.

Büyük proje geldiğinde bu araç, Web katmanının altındaki `Infrastructure`
servisine kolayca dönüşür — bugün yazılan istemci sınıfları o gün taşınabilir.

---

## 1. Amaç

Google Play Console uygulamalarını tek bir panelden yönetmek ve incelemek
için **özel/dahili** bir yönetim uygulaması.

**Bu bir SaaS ürünü değildir.** Dahili bir geliştirici aracıdır. Mimari basit,
bakımı kolay ve üretim kalitesinde kalmalı; gereksiz soyutlama eklenmemeli.

## 2. Teknoloji yığını

- .NET 9
- ASP.NET Core
- Blazor Web App
- C#
- Bootstrap veya sade native Blazor CSS
- `HttpClient`
- `System.Text.Json`
- Entity Framework Core
- Geliştirmede SQLite, sonradan PostgreSQL'e geçmeye hazır mimari

**Yapılmayacaklar:** CQRS, MediatR, event sourcing, mikroservis. Bu bir dahili
yönetim aracı; bu kalıplar burada maliyet, fayda değil.

## 3. Entegre edilecek API'ler

1. Google Play Android Developer API (Android Publisher)
2. Google Play Developer Reporting API

**Kural: Resmi dokümantasyon tek doğru kaynaktır.** Uç nokta uydurulmayacak.
Bir uç nokta uygulanmadan önce güncel resmi dokümana karşı doğrulanacak.
Desteklenmeyen bir şey varsa hayali API yazmak yerine "desteklenmiyor" denecek.

## 4. Google kimlik doğrulama

OAuth 2.0, ASP.NET Core sunucu tarafına uygun **Authorization Code Flow**.

Kimlik kapsamları:
```
openid
email
profile
```

Play API kapsamları:
```
https://www.googleapis.com/auth/androidpublisher
https://www.googleapis.com/auth/playdeveloperreporting
```

**Tarayıcı ASLA şunları görmez:** client secret, refresh token, API kimlik
bilgileri. Token saklama ve yenileme tamamen sunucuda.

Refresh token alınabilmesi için offline erişim istenecek.

### Soyutlamalar

```csharp
IGoogleTokenService
    GetAccessTokenAsync()
    RefreshAccessTokenAsync()
    HasValidCredentialsAsync()
    RevokeAsync()

ITokenProtector          // ilk uygulama: ASP.NET Core Data Protection
```

**Asla loglanmaz:** access token, refresh token, client secret.

## 5. Uygulama keşfi

Uygulamalar başta sabit kodlanmayacak. Reporting API'nin `apps.search`
çağrısıyla, kimliği doğrulanmış kullanıcının erişebildiği uygulamalar
bulunacak.

```csharp
IPlayApplicationService
    Task<IReadOnlyList<PlayApplicationDto>> GetApplicationsAsync();
```

`PlayApplicationDto`: `PackageName`, `DisplayName`, `AppResourceName`,
`IconUrl` (varsa), `IsAccessible`.

Kullanıcının en son seçtiği uygulama hatırlanacak.

## 6. Ana yerleşim

Sol kenar çubuğu: Dashboard, Applications, Reviews, Android Vitals, Releases,
Store Listings, Monetization, Settings.

Üst çubuk: "Arar Games Play Manager", seçili uygulama, Google hesabı avatarı,
e-posta, çıkış.

## 7. Uygulama panosu

Kartlar: Production Release, Latest Version Code, Latest Release Status,
ANR Rate, User Perceived ANR Rate, Crash Rate, User Perceived Crash Rate,
Open Crash Issues, Open ANR Issues, Recent Reviews, Average Review Score,
Store Listing Languages, In-App Products, son senkronizasyon tarihi.

**Metrik uydurulmayacak.** API bir değeri sunmuyorsa:
`"Not available through current API"` yazılacak.

## 8. Android Vitals

```csharp
IPlayVitalsService
```

En az: ANR oranı, crash oranı, hata sayıları, crash konuları, ANR konuları,
hata raporları, anomaliler. API destekliyorsa: yavaş açılış, yavaş render,
LMK oranı, aşırı uyandırma.

Sorgu aralıkları: son 24 saat, 7 gün, 30 gün, özel aralık.
Destekleniyorsa gruplama/filtreleme: sürüm kodu, Android API seviyesi, cihaz
modeli, ülke.

Genel bakış ekranı: ANR Rate, Crash Rate, Slow Start, Active Issues + ANR ve
crash trend grafikleri. **Ağır grafik kütüphanesi eklenmeyecek**; hafif bir
grafik bileşeni tercih edilecek.

## 9. Hata konuları (Issues)

Sekmeler: ANRs, Crashes.

Gösterilecek: Issue ID, tür, ilk görülme, son görülme, olay sayısı, etkilenen
kullanıcı (varsa), sürüm kodları, cihaz bilgisi (varsa), stack trace /
exception bilgisi, durum. Tıklayınca detay görünümü.

## 10. Yorumlar

```csharp
IPlayReviewService     // reviews.list, reviews.get, reviews.reply
```

Filtreler: tümü, 1-5 yıldız, cevaplanmamış, cevaplanmış.

Kart içeriği: yorumcu adı (varsa), yıldız, dil, cihaz, uygulama sürümü, tarih,
orijinal yorum, geliştirici cevabı. Butonlar: Reply, Edit Reply, Copy Review.

**V1'de cevap açık onay ister. Üretilen metin otomatik yayınlanmaz.**
İleride yapay zekâ destekli cevap için `IReviewReplyGenerator` arayüzü
bırakılacak — ama ilk sürümde hiçbir AI sağlayıcı entegre edilmeyecek.

## 11. Sürümler

```csharp
IPlayReleaseService
```

Önce **yalnızca okuma**. Track'ler: internal, closed testing, open testing,
production. Gösterilecek: track adı, sürüm kodu, sürüm adı, durum, kademeli
dağıtım oranı (varsa), sürüm notları.

### Yayınlama güvenliği

İki mod:
```
ReadOnly            (varsayılan)
PublishingEnabled
```

```yaml
PlayManager:
  AllowPublishing: false
```

Yayınlama kapalıyken **yayınlama yapabilen hiçbir API işlemi çalıştırılamaz.**
Arayüz "Publishing disabled" gösterir.

### AAB yükleme

Resmi Android Publisher Edit akışı:

```
Create Edit → Upload AAB → Update track → Validate Edit
           → Kullanıcıya özet → Açık onay → Commit Edit
```

**Yükleme sonrası asla otomatik commit yok.** Commit öncesi gösterilecek:
paket, track, sürüm kodu, sürüm durumu, dağıtım yüzdesi, sürüm notları,
yüklenen dosya. Sonra `CONFIRM RELEASE` istenecek.

**Üretime otomatik yayınlama uygulanmayacak.**

### Track işlemleri (yayınlama açıkken)

Internal / Closed / Open testing'e yükleme, production sürümü oluşturma,
kademeli dağıtım başlatma, yüzde güncelleme, durdurma ve tamamlama
(destekleniyorsa). Hepsi güncel resmi API'ye karşı doğrulanacak.

## 12. Mağaza listelemeleri

```csharp
IPlayStoreListingService
```

Dil başına: dil kodu, başlık, kısa açıklama, tam açıklama, video URL,
görseller, feature graphic, telefon ve tablet ekran görüntüleri — **yalnızca
API bunları sunuyorsa.**

Düzenleme Play Console Edit'i oluşturur; hemen commit edilmez:
`Create Edit → Modify → Validate → Diff göster → Onay → Commit`

### Yerelleştirme matrisi

| Dil | Başlık | Kısa | Tam |
|---|---|---|---|
| English | ✓ | ✓ | ✓ |
| Türkçe | ✓ | ✓ | ✓ |
| Deutsch | ✓ | ✓ | ✓ |

Yerelleştirilmiş listeleme İngilizce ile karşılaştırılabilecek. İleride çeviri
entegrasyonu için `ITranslationService` arayüzü bırakılacak — üçüncü taraf
çeviri servisi **şimdilik uygulanmayacak.**

### Mağaza görselleri

Yalnızca resmi API'de tanımlı görsel tipleri kullanılacak; tip adı
uydurulmayacak. **V1 salt okunur.**

## 13. Para kazanma

```csharp
IPlayMonetizationService
```

Tek seferlik ürünler, abonelikler, base plan'lar, teklifler, ürün kimliği,
durum, bölgesel bilgi, fiyat. **MVP'de salt okunur.**

### Satın alma denetleyicisi

Girdiler: paket adı, ürün kimliği, satın alma token'ı. Resmi doğrulama uç
noktası sorgulanır.

**Satın alma token'ları saklanmaz ve loglanmaz.**

### İptal edilen satın almalar

İzinler elveriyorsa gösterilir. API'nin verdiğinin ötesinde iptal sebebi
hakkında varsayım yapılmaz.

## 14. API istemci mimarisi

Tüm Google çağrıları tek bir dev sınıfa konmayacak.

```
GoogleApiClientBase
GooglePlayPublisherClient
GooglePlayReportingClient
```

Alan servisleri: `PlayApplicationService`, `PlayVitalsService`,
`PlayReviewService`, `PlayReleaseService`, `PlayStoreListingService`,
`PlayMonetizationService`.

`IHttpClientFactory` kullanılacak. Adlandırılmış istemciler: `GooglePublisher`,
`GoogleReporting`. Yetkilendirme başlığı `GoogleAccessTokenHandler :
DelegatingHandler` ile eklenecek; token `IGoogleTokenService`'ten gelecek.

## 15. Hata yönetimi

Google API'leri 401, 403, 404, 409, 429, 5xx döndürebilir. Merkezi yönetim:

```csharp
GoogleApiException
    StatusCode
    GoogleErrorCode
    Message
    RequestId
```

- **401** → bir kez token yenileme denenir.
- **429** → `Retry-After` varsa ona uyulur. **Sınırlı** yeniden deneme; sonsuz
  döngü yok.
- **403** → anlamlı mesaj: "Google hesabınızın bu işlem için gerekli Google
  Play Console izni yok."

Access token asla dışarı sızdırılmaz.

## 16. Loglama

`ILogger` kullanılacak.

Loglanır: API uç nokta kategorisi, HTTP metodu, süre, durum kodu, paket adı,
işlem.

**Loglanmaz:** Authorization başlığı, access token, refresh token, purchase
token, client secret, hassas istek gövdeleri.

## 17. Veritabanı

EF Core, başlangıçta SQLite. Varlıklar: `ApplicationUser`, `GoogleCredential`,
`PlayApplicationPreference`, `ApiSyncLog`, `AuditLog`.

**Tüm Google Play verisi veritabanına kopyalanmayacak.** Google API'leri tek
doğru kaynaktır; yalnızca faydalı olduğu yerde önbelleklenir.

## 18. Denetim kaydı

Her yazma işlemi bir denetim kaydı üretir: kullanıcı, işlem, paket, UTC tarih,
varlık tipi, özet, başarı/başarısızlık.

Örnek işlem adları: `STORE_LISTING_UPDATED`, `REVIEW_REPLIED`, `AAB_UPLOADED`,
`RELEASE_COMMITTED`, `ROLLOUT_UPDATED`.

**OAuth kimlik bilgileri denetim kaydına yazılmaz.**

## 19. Güvenlik

- Sırlar kaynak kontrolüne girmez.
- Yerel geliştirmede User Secrets.
- Üretimde ortam değişkenleri.
- `ClientId` ve `ClientSecret` yapılandırma değeridir.
- **`ClientSecret` asla Git'e giren `appsettings.json` içine konmaz.**
- HTTPS, güvenli çerezler, OAuth yönlendirmesine uygun SameSite.
- Uygun yerlerde anti-forgery.
- Refresh token'lar Data Protection ile korunur.

## 20. Proje yapısı

```
ArarGames.PlayManager.sln
src/
  ArarGames.PlayManager.Web
  ArarGames.PlayManager.Application
  ArarGames.PlayManager.Infrastructure
  ArarGames.PlayManager.Domain
tests/
  ArarGames.PlayManager.Tests
```

## 21. Arayüz stili

Modern, koyu geliştirici panosu. Gösterişli değil. İlham: GitHub, Azure
Portal, Google Play Console.

Kartlar dikkatli kullanılacak. Okunabilirlik ve **bilgi yoğunluğu** öncelikli.
Yeşil/kırmızı/sarı yalnızca durum göstergesi olarak. Pazarlama sitelerindeki
devasa boşluklardan kaçınılacak — bu veri yoğun bir geliştirici aracı.

### Örnek uygulama kartı

```
Blocked: Pixel Panzer
com.arargames.blocked
Production  1.4.2
ANR 0.24%   Crash 0.08%
Recent Reviews 7            [Open]
```

## 22. Rotalar

```
/
/apps
/apps/{packageName}
/apps/{packageName}/vitals
/apps/{packageName}/issues
/apps/{packageName}/reviews
/apps/{packageName}/releases
/apps/{packageName}/store
/apps/{packageName}/monetization
/tools/purchase-inspector
/settings
```

## 23. Uygulama sırası — fazlar

Tek bir dev geçişte hepsi yapılmayacak.

| Faz | Kapsam |
|---|---|
| 1 | Çözüm yapısı, Google kimlik doğrulama, token yönetimi, uygulama keşfi, uygulama seçici, pano kabuğu |
| 2 | Android Vitals: ANR, crash oranı, konular, hata raporları |
| 3 | Yorumlar: liste, detay, cevap |
| 4 | Sürümler (salt okunur): track'ler, sürüm bilgisi |
| 5 | Mağaza listelemeleri (salt okunur), yerelleştirme matrisi |
| 6 | Mağaza listelemesi düzenleme, Play Edit akışı |
| 7 | AAB yükleme, test track'leri, `AllowPublishing` arkasında üretim yayını |
| 8 | Para kazanma, satın alma denetleyicisi, iptal edilen satın almalar |

### İlk çalışan kilometre taşı

```
Uygulamayı çalıştır
  ↓ "Sign in with Google"
Google OAuth
  ↓ geri dön
Reporting API apps.search
  ↓
Erişilebilir uygulamaları listele
  ↓ seç
Uygulama panosunu aç
```

**Bu güvenilir şekilde çalışmadan yayınlama işlevine geçilmeyecek.**

## 24. İlk görev — araştırma

Kod yazmadan önce şunlar güncel resmi dokümandan doğrulanacak:

- Google Play Android Developer API
- Google Play Developer Reporting API
- OAuth yetkilendirme
- `apps.search`
- `vitals.anrrate`, `vitals.crashrate`
- `vitals.errors.issues`, `vitals.errors.reports`
- `reviews`
- `edits`, `edits.bundles`, `edits.tracks`, `edits.listings`
- `monetization`, `purchases`

Google'ın **şu an önerdiği sürümler** teyit edilecek.

Sonra sırasıyla: çözüm mimarisi, klasör yapısı, NuGet paketleri, yapılandırma
modeli, Google OAuth uygulaması, token servisi, HTTP istemcileri,
`apps.search`, uygulama seçici arayüzü, ilk pano.

Kod üretildikten sonra Google Cloud proje kurulumu adım adım anlatılacak:
OAuth onay ekranı, OAuth client ID, yönlendirme URI'si, iki API'nin
etkinleştirilmesi, kapsamlar, Play Console API/kullanıcı izinleri, yerel
geliştirme sırları. Ardından yerel çalıştırma komutları.

**Açıkça "mock" diye işaretlenmedikçe sahte veri kullanılmayacak.**
