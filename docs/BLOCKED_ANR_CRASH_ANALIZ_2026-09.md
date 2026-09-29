# Blocked — Play Console ANR & Crash Analizi (Eylül 2026)

Kaynak: Play Developer Reporting API üzerinden çekilen 33 konu.
Toplam **73 etkilenen kullanıcı**, 36 ANR + 9 crash.
Sürüm: `608260339` (1.0.2026.08.26)

---

## 1. Aileler

| Aile | Konu | Kullanıcı | Pay |
|---|---:|---:|---:|
| `nativePollOnce` (boşta) | 5 | 31 | **42%** |
| **GPU / EGL** | 11 | 15 | **21%** |
| .NET kilit bekleme | 2 | 7 | 10% |
| MonoGame yönetilen (crash) | 3 | 6 | 8% |
| libc / I/O | 5 | 5 | 7% |
| Mono / ART (JIT, tip yükleme) | 5 | 5 | 7% |
| Google Play Services | 1 | 3 | 4% |
| SIGABRT | 1 | 1 | 1% |

Otuz üç konunun **onu tek bir aileye ait** ve o aile bilgi bankasında zaten
"müdahale gerektirmez" olarak kapatılmış.

---

## 2. Bilgi bankasıyla eşleşenler — yeni iş yok

### 2.1 `nativePollOnce` — %42 ama sahte

31 kullanıcı, dört ayrı konu. Bankada zaten iki kayıt var:

- `anr-nativepollonce-late-dump` — "Geç yığın dökümü / yanlış pozitif"
- `anr-input-lock-free-optimization` — `AndroidKeysDown` kilidi
  `ConcurrentDictionary`'ye çevrilmişti

`nativePollOnce` ana thread'in **boşta** olduğu yerdir; looper olay bekliyor.
ANR raporu bu kareyi gösteriyorsa ana thread tıkanan taraf **değildir**.
Google'ın kendi kılavuzu bu kümeyi yok saymayı öneriyor.

**Aksiyon: yok.** Sayının büyüklüğü aldatıcı — listenin başında duruyor ama
düzeltilecek bir şey yok.

Tek istisna: konulardan biri
`Broadcast of Intent { cmp=...NotificationReceiver }` diyor. Bu farklı — bir
broadcast alıcısının süre aşımı. **`NotificationReceiver.cs` bakılmalı**
(1 kullanıcı, düşük öncelik ama gerçek).

### 2.2 `SystemNative_LowLevelMonitor_TimedWait` — 7 kullanıcı

Bankada: `anr-mainthread-lock-contention`. MonoGame'in pause/resume el
sıkışması; ana thread `WaitOne()` ile oyun döngüsünü bekliyor.
`OnPause` içindeki iş 404 ms'den 2.2 ms'ye indirilmişti.

Play Console bu konuları "Native lock contention" etiketiyle işaretlemiş —
teşhis doğrulanmış oluyor.

**Aksiyon: izle.** Yeni sürümde sayı düşmezse el sıkışma yeniden bakılmalı.

### 2.3 `KEGLGetDrawableParameters` — 2 kullanıcı

Bankada: `anr-egl-bufferqueue-lock-contention`. BufferQueue açlığı, sürücü
seviyesi. Yüzey yeniden oluşturma zaten en aza indirilmiş.

---

## 3. GPU / EGL ailesi — **en büyük gerçek iş**

11 konu, 15 kullanıcı. Bankada bu aileden yalnızca bir kayıt var
(`KEGLGetDrawableParameters`), geri kalan **on konu yeni**.

```
[libIMGegl.so]   IMGeglSwapBuffersWithDamageKHR      3
[libGLES_mali.so] glClear                            2
[libIMGegl.so]   KEGLGetDrawableParameters           2
[libGLESv2_mtk.so] glDrawElements                    1+1
[libsrv_um.so]   RGXKickTA                           1
[libsrv_um.so]   PVRSRVAcquireDeviceMapping          1
[libgsl.so]      ioctl_kgsl_cmdstream_freememontimestamp  1
[libgsl.so]      (adsız kernel karesi)               1
[libged.so]      ged_cond_lock                       1
[libGLES_mali.so] __egl_platform_queue_buffer_android 1
```

Dört farklı GPU ailesi: PowerVR (IMG/libsrv_um), Mali, Adreno (libgsl),
MediaTek (libged, GLESv2_mtk). Yani tek bir sürücü hatası değil.

### Bunların hepsi aynı şeyi söylüyor

`eglSwapBuffers`, `glClear`, `glDrawElements` ana thread'i **GPU kuyruğu
dolduğunda bloke eder**. Kuyruk dolu kalırsa girdi gönderimi zaman aşımına
uğrar ve Android ANR yazar.

Bu, `PERFORMANCE_INVESTIGATION.md` içinde ölçtüğümüz şeyin ta kendisi:

> A10, yağmur + CRT açıkken kare başına **30 ms GPU beklemesi**.
> `upd + drw = 18 ms` ama kare 48 ms.

Yani ANR'lerin bu ailesi ile ölçtüğümüz dolgu oranı duvarı **aynı olay**.
Biri profilde, diğeri sahada görünüyor.

### Test edilebilir tahmin

CRT Lite + yüzey katmanlarının tek dokuya pişirilmesi (~900 çizim → 1)
yayına çıktığında **bu ailedeki ANR sayısı düşmeli**. Düşmezse teşhis yanlış
demektir ve GPU tarafına yeniden bakılır.

Bu, bilgi bankasının asıl işi: bir düzeltmenin işe yarayıp yaramadığını
sahadaki sayıdan okumak.

---

## 4. Yeni ve gerçek — ana thread'de I/O

```
[libc.so] write        1
[libc.so] __faccessat  1
```

Play Console bunlardan birini **"I/O in main thread"** diye etiketlemiş.

Bunun karşılığı ölçümde zaten var:

```
[PERF-YAVAS-IS] GameSettings.Save=1553ms
```

`GameSettings.Save()` senkron, `lock` altında ve JSON yazıyor. Oyun ortasında
yetenek kullanımı ve ödül alımında tetikleniyor. **Hâlâ düzeltilmedi.**

Bir buçuk saniyelik ana thread bloğu, girdi zaman aşımı eşiğinin (5 sn) altında
ama üst üste gelirse ya da yavaş bir eMMC'de uzarsa ANR üretir. Sahada iki
kullanıcıda görünmesi tesadüf değil.

**Aksiyon: kaydetmeyi ana thread'den çıkar.** Ya arka plana al ya da bölüm
sonuna ertele.

---

## 5. Mono / ART — tip yükleme ve JIT

```
mono_class_is_subclass_of   2
art::JNI DeleteLocalRef     1
nterp_op_sget               1
art::JniMethodFastStart     1
```

Bir tipin **ilk kez** kullanılması sırasında Mono sınıfı yüklüyor ve ART
yorumluyor. Ana thread'de olursa donma.

Ölçümde karşılığı olan donmalar:

| Ölçülen | Süre |
|---|---|
| `GameSettings.Save` (ilk) | 1553 ms |
| `MusicPrepare` | 324 ms |
| `MusicPrewarm` | 120 ms |
| `BannerShow` | 100 ms |
| `Sfx:InSelectionSound` (ilk) | 97 ms |
| `Draw.ArcadeEnd` (ilk, shader derleme) | 399 ms |

Hepsi **ilk kullanım** maliyeti. Isıtma kuyruğu zaten var; bu satırlar oraya
taşınacak adayları gösteriyor.

**Aksiyon:** shader'ı, müziği ve ilk ses efektini açılışta ısıt.

---

## 6. Google Play Services — 3 kullanıcı

```
[base.apk] com.google.android.gms.dynamite.zza.run
```

GMS Dynamite modülü ana thread'de yükleniyor. Reklam (`AdsBootstrap`) ya da
Play Games başlatması. Ölçümde `AdsStart=1x2ms` görünüyor — yani bizim
çağrımız ucuz; pahalı olan GMS'in kendi modül yüklemesi.

**Aksiyon:** başlatmayı ilk kareden sonraya ertele. Kod bizde değil, ne zaman
çağırdığımız bizde.

---

## 7. Crash'ler

### 7.1 `Microsoft.Xna.Framework.Threading.Run + 0xa` — 4 kullanıcı, en büyük crash

`android.runtime.JavaProxyThrowable` olarak yüzeye çıkıyor: oyun döngüsü
içinde atılan **yönetilen bir istisna**, Java tarafına proxy'lenmiş.

Yığın izi olmadan içteki gerçek istisna görünmüyor. **Öncelik: bunun raporunu
çek.** (bkz. bölüm 9)

### 7.2 `AndroidGamePlatform.Activity_...` — 1 kullanıcı

Yaşam döngüsü (pause/resume) sırasında. Bankadaki
`crash-touchpanel-teardown-npe` ile aynı aileden olma ihtimali yüksek — o kayıt
aktivite yıkımında gelen son dokunuş olayını anlatıyor.

### 7.3 `Audio.Microphone.UpdateMicrophones` — 1 kullanıcı

**Bu ilginç.** Oyun mikrofon kullanmıyor. MonoGame'in Android platformu
`OnResume` sırasında mikrofonları sayıyor ve bazı cihazlarda patlıyor.

Kendi kodumuzda karşılığı yok; MonoGame içinde. Denenecek: mikrofon
sayımını tetikleyen yaşam döngüsü çağrısını sarmalamak ya da MonoGame
sürümünde bu yolun kapatılabilir olup olmadığına bakmak.

### 7.4 `[libc.so] abort` / SIGABRT — 1 kullanıcı

Bankada iki aday var: `crash-egl-not-initialized-hwui` (sürücü hatası, kapalı)
ve `anr-quit-exit-abort` (`Environment.Exit` düzeltilmişti). Yığın izi
gerekiyor.

---

## 8. Öncelik sırası

| # | İş | Etki | Durum |
|---|---|---|---|
| 1 | `GameSettings.Save`'i ana thread'den çıkar | 1553 ms blok + sahada I/O ANR | **yapılmadı** |
| 2 | CRT Lite'ı yayınla, GPU ANR'lerini izle | 15 kullanıcı | kod hazır, ölçüm bekliyor |
| 3 | `Threading.Run` crash'inin yığın izini çek | en büyük crash | veri eksik |
| 4 | Shader / müzik / ilk SFX ısıtması | ilk kullanım donmaları | kısmen var |
| 5 | GMS başlatmayı ilk kareden sonraya al | 3 kullanıcı | yapılmadı |
| 6 | `NotificationReceiver` süre aşımı | 1 kullanıcı | bakılmadı |
| 7 | `Microphone.UpdateMicrophones` | 1 kullanıcı | MonoGame içi |

---

## 9. Veri eksiği — yığın izleri gelmedi

Bu çekimde **33 kaydın hiçbirinde `FullStackTrace` yok**, `Devices` de boş.
Yani içe aktarma `errorReports` olmadan yapılmış.

`SignatureFrames` yalnızca konunun `location` alanını taşıyor —
`android.runtime.JavaProxyThrowable` gibi. Bu, kümeyi tanımaya yeter ama
**hata ayıklamaya yetmez**.

Yeniden çek ve listeden sonra **`y`** seç: her konu için `errorReports:search`
çağrılır, `reportText` (asıl yığın izi), cihaz modeli ve Android sürümü
kayda girer.

Öncelikli olarak yığın izi gereken üç konu:

1. `Microsoft.Xna.Framework.Threading.Run + 0xa` (4 kullanıcı)
2. `[libc.so] abort` / SIGABRT
3. `SystemNative_LowLevelMonitor_TimedWait` (7 kullanıcı — hangi kilit?)

---

## 10. Bu analizden çıkan yapısal fikir

Otuz üç konunun onu bankada zaten çözülmüş bir kayda karşılık geliyordu ve
bunu **elle** eşleştirdim. Araç bunu kendisi yapabilir:

Yeni bir Play konusu geldiğinde `SignatureFrames` ve `Name` alanları mevcut
kayıtlarla karşılaştırılıp "bu muhtemelen `anr-nativepollonce-late-dump` ile
aynı" denebilir. Bankanın kuruluş amacı da tam olarak buydu — aynı imzayı
ikinci kez gördüğünde sıfırdan analiz yapmamak.
