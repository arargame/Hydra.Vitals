# Google Play Crash & ANR — Kurulum

`Hydra.Vitals` konsol aracına Google Play Developer Reporting API bağlantısı.
Kapsam bilerek dar: **yalnızca crash ve ANR.** Büyük yönetim paneli ileride,
şartnamesi `ARAR_GAMES_PLAY_MANAGER_SPEC.md` içinde.

---

## Ne yapıyor

```
[G] Google Play'den CANLI Crash & ANR Getir
   → Google ile giriş (tarayıcı açılır, bir kez)
   → apps:search  → hesabın eriştiği uygulamalar
   → uygulama seç
        ├─ 1) ANR listesi      → errorIssues:search (tek istek)
        ├─ 2) Crash listesi    → errorIssues:search (tek istek)
        └─ 3) Oranlar          → anrRate + crashRate (tazelik + sorgu, 4 istek)
   → liste geldikten sonra
        ├─ d) tek konunun YIĞIN İZİ  → errorReports:search (tek istek)
        └─ y) hepsini yaz            → konu başına bir errorReports isteği
   → gün sayısını sen giriyorsun
```

**Neden iki adım:** önce her şey tek seferde çekiliyordu — dört HTTP çağrısı
oran için, iki tane de konular için. Çoğu zaman istenen tek bir liste:
"hangi ANR'ler var". Play Console'un kendisi de böyle çalışıyor.

---

## Doğrulanmış uç noktalar

Aşağıdakiler 2026-09'da resmi dokümandan teyit edildi. Uydurma yok.

| İş | Uç nokta |
|---|---|
| Uygulama keşfi | `GET /v1beta1/apps:search` |
| Tazelik (veri ne kadar güncel) | `GET /v1beta1/apps/{pkg}/{metricSet}` |
| ANR oranı | `POST /v1beta1/apps/{pkg}/anrRateMetricSet:query` |
| Crash oranı | `POST /v1beta1/apps/{pkg}/crashRateMetricSet:query` |
| Hata konuları (özet) | `GET /v1beta1/apps/{pkg}/errorIssues:search` |
| **Hata raporları (yığın izi)** | `GET /v1beta1/apps/{pkg}/errorReports:search` |

Kök: `https://playdeveloperreporting.googleapis.com`
Kapsam: `https://www.googleapis.com/auth/playdeveloperreporting`

Kullanılan metrikler: `anrRate`, `userPerceivedAnrRate`, `crashRate`,
`userPerceivedCrashRate`, `distinctUsers`.

### Ayrıntı nerede — issue mi report mu

Bu ayrım bir kez yanlış anlaşıldı ve bilgi bankası boş göründü:

| Kaynak | Ne veriyor |
|---|---|
| `errorIssues` | **Özet.** Kaç kullanıcı, kaç olay, hangi sürüm, kaba konum. Yığın izi **yok**. |
| `errorReports` | **Ayrıntı.** `reportText` = asıl yığın izi, cihaz modeli, Android API, olay zamanı. |

Play Console'un listesi issue'ları gösteriyor; bir satıra tıklayınca gördüğün
uzun metin ise report'tan geliyor. Araç da artık aynı şeyi yapıyor.

Google'ın `reportText` için uyarısı var: *"makine tüketimi için tasarlanmadı,
biçimi değişebilir."* Bu yüzden metin **ayrıştırılmıyor** — olduğu gibi
saklanıyor, sadece ilk birkaç satırı "imza" olarak kırpılıyor. Biçim değişse
bile bu bozulmaz.

### Bilinmesi gereken üç kısıt

**Saat dilimi seçilemiyor.** Günlük toplamada desteklenen tek değer
`America/Los_Angeles`. Bu bizim tercihimiz değil, dokümanda "tarihsel kısıtlar
nedeniyle" yazıyor. Başka bir değer göndermek hata döndürür.

**Veri gecikmeli ve gecikme sabit değil.** Bu hata bir kez alındı:

```
'timeline_spec.end_date' field should be at most the current freshness 2026-09-02 00:00
```

Yani bitiş günü Google'ın hazırladığı son günden ileride olamaz. "Dün" demek
yetmiyor; gecikme metrik setine göre değişiyor. Araç artık tahmin etmiyor,
**soruyor**: `GET /v1beta1/apps/{pkg}/{metricSet}` ile
`freshnessInfo.freshness[].latestEndTime` okunup bitiş günü ona kırpılıyor.

Boş günü 0 olarak göstermek "çökme yok" gibi yanlış bir izlenim verirdi — o
yüzden boş olmayan en son gün gösteriliyor.

**Aynı API, iki farklı zaman kuralı.** Deneyerek öğrenildi, dokümanda yan yana
yazmıyor:

| Uç nokta | Saat dilimi | Bitiş |
|---|---|---|
| `*MetricSet:query` | `America/Los_Angeles` **zorunlu** | kapsayıcı |
| `errorIssues` / `errorReports` | **gönderilmez** (UTC varsayılıyor) | **dışlayıcı** |

Metrik setine Los Angeles göndermezsen hata alırsın; issue/report'a
gönderirsen çalışmaz. Bitiş günü de dışlayıcı olduğu için bir gün eklenmesi
gerekiyor, yoksa son günün kayıtları kayboluyor. İkisi de tek bir
`Interval()` yardımcısında toplandı ki zamanla birbirinden ayrışmasınlar.

---

## Google Cloud kurulumu

### 1. Proje ve API

1. [Google Cloud Console](https://console.cloud.google.com/) → proje oluştur
   (ör. `arar-games-play`).
2. **APIs & Services → Library** → şunu etkinleştir:
   - **Google Play Developer Reporting API**

> Android Publisher API şu an gerekmiyor — bu araç yorum/sürüm/mağaza
> işlemleri yapmıyor. Büyük projeye geçildiğinde eklenecek.

### 2. OAuth onay ekranı

**APIs & Services → OAuth consent screen**

- User type: **External** (kişisel Google hesabıysa) veya Internal (Workspace)
- Uygulama adı: `Hydra Vitals`
- Kullanıcı destek e-postası: kendi adresin
- Scopes: burada eklemene gerek yok, uygulama isteme anında istiyor
- **Test users:** Play Console'a bağlı Google hesabını ekle

> Yayınlanmamış (Testing) bir OAuth uygulamasında refresh token **7 gün**
> sonra geçersiz olur. Araç bunu fark edip yeniden giriş ister. Sürekli
> giriş yapmak istemiyorsan onay ekranını **Publish** et; dahili bir araç
> için doğrulama süreci gerekmez çünkü hassas kapsam kullanmıyoruz.

### 3. OAuth client

**APIs & Services → Credentials → Create Credentials → OAuth client ID**

- Application type: **Desktop app**
- Ad: `Hydra Vitals Console`

> **Desktop app** olmalı, Web application değil. Araç loopback
> (`http://127.0.0.1:<port>`) akışını kullanıyor; Desktop istemcide loopback
> yönlendirmesi için ayrıca URI kaydetmen gerekmiyor, port her çalıştırmada
> değişiyor. Web istemcide sabit URI kaydı zorunlu olurdu ve port çakışması
> yaşardın.

JSON'u indir.

### 4. Play Console izni

**Play Console → Users and permissions**

Google hesabının şu uygulamalara erişimi olmalı, en az:
- **View app information and download bulk reports**

İzin yoksa API `403` döner. Araç bunu teknik hata olarak değil, ne yapman
gerektiğini söyleyen bir mesaj olarak gösterir.

---

## Yerel ayar

İki yoldan biri. **İkisi de kaynak kontrolüne girmez.**

### A) İndirdiğin JSON'u yerine koy

```
%LOCALAPPDATA%\HydraVitals\google_oauth.json
```

Google'ın verdiği dosyayı olduğu gibi kopyala; araç `installed` ve `web`
sarmalayıcılarının ikisini de tanıyor.

### B) Ortam değişkeni

```cmd
setx HYDRA_GOOGLE_CLIENT_ID "xxxxx.apps.googleusercontent.com"
setx HYDRA_GOOGLE_CLIENT_SECRET "GOCSPX-xxxxx"
```

Ortam değişkeni öncelikli. `setx` sonrası yeni bir terminal aç.

---

## Çalıştırma

```cmd
cd C:\Users\ararg\source\AIRepos\Hydra.Vitals
dotnet restore
dotnet run
```

Menüde **G**.

İlk çalıştırmada tarayıcı açılır. İzin verdikten sonra sekme
"Giriş tamam" der ve konsola dönebilirsin.

---

## Jeton nerede duruyor

```
%LOCALAPPDATA%\HydraVitals\google_token.bin
```

Yenileme jetonu **Windows DPAPI** ile şifreleniyor — anahtar Windows kullanıcı
hesabına bağlı, başka kullanıcı veya başka makine dosyayı çözemez.

DPAPI olmayan platformlarda jeton **hiç saklanmıyor**; her çalıştırmada
yeniden giriş isteniyor. Bilerek böyle: düz metin yazmaktansa tekrar giriş
yapmak iyidir. "Geçici olarak düz yazalım" diye başlayan şey kalıcılaşır.

Menüden **3) Google oturumunu kapat** dosyayı siler ve jetonu Google
tarafında da iptal eder.

---

## Bilgi bankasına yazma

Getirilen konular istersen `vitals_database.json` içine `VitalIssue` olarak
yazılır. Kod öneki `PLAY-ANR-` / `PLAY-CRASH-` — elle girilen kayıtlardan
ayırt edilebilsin diye.

Kayıt şunları içeriyor: yığın izi (`FullStackTrace`), imza satırları
(`SignatureFrames`), etkilenen cihazlar (`Devices` — marka, model, API), olay
zamanı, sürüm, kullanıcı ve olay sayısı.

**Aynı kod ikinci kez gelirse üzerine yazılmaz.** Yalnızca `EventCount`,
`AffectedUsers` ve `ReportedVersion` tazelenir. Yığın izi ise **yalnızca boşsa**
doldurulur — elle düzenlenmiş bir izi ezmek, insanın yaptığı işi silmek olurdu.

Sebep: bu bankanın değeri `RootCause`, `FixApproach` ve `LessonsLearned`
alanlarında ve onları insan yazıyor. Yeniden çekim bunları silseydi bankanın
tek değerli kısmı kaybolurdu.

Paket adıyla eşleşen bir `AppProject` yoksa yazma yapılmaz — önce projeyi
eklemen gerekir.

---

## Sorun giderme

| Belirti | Sebep |
|---|---|
| `403` | Play Console izni yok ya da Reporting API etkin değil |
| `401` sürekli | Refresh token süresi dolmuş (Testing modunda 7 gün). Çıkış yapıp yeniden gir |
| Uygulama listesi boş | Google hesabı Play Console'da bu uygulamalara bağlı değil |
| Oranlar "veri yok" | Aralıkta yeterli kullanıcı yok, ya da veri henüz hazır değil |
| Konu listesi boş ama oranlar dolu | Konu listesi ayrı yetki isteyebiliyor; oran verisi yine geçerli |

---

## Bilerek yapılmayanlar

- **Yorum, sürüm, mağaza listeleme, para kazanma:** Android Publisher API
  gerektirir; bugünkü ihtiyaç crash ve ANR.
- **Yazma işlemi yok.** Araç hiçbir Play kaynağını değiştirmiyor.
- **Cihaz/sürüm kırılımı yok.** API destekliyor (`dimensions`), ama önce tek
  toplam sayının doğru geldiğini görmek gerekiyor. Kırılım ayrı bir sorgu
  olarak sonra eklenir.
