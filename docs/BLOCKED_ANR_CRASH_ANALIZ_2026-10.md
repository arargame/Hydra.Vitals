# Blocked — Play Console ANR & Crash Analizi (Ekim 2026)

Kaynak: Play Console ekran görüntüleri (2 Ekim 2026) + Eylül analizi karşılaştırma.
Yeni sürüm: `609260005` (1.0.2026.09.26) — 2 gün önce yayınlanmış.

---

## 1. Genel Tablo

| Metrik | Eylül Sonu | Ekim Başı | Değişim |
|--------|-----------|-----------|---------|
| User-perceived ANR rate | ~2.63% | **1.49%** | ⬇️ -1.14% |
| User-perceived Crash rate | ~0.19% | **0.17%** | ⬇️ -0.02% |
| Bad behavior eşiği (ANR) | 0.47% | 0.47% | — |
| Toplam ANR konusu | 33 | ~10 | ⬇️ |

ANR oranı belirgin düştü ama hâlâ eşiğin 3.2 katı üzerinde.

---

## 2. Konu Listesi (Ekim Ekranından)

### nativePollOnce — %67.7 pay, sahte pozitif

17 kullanıcı, 21 olay. Eylül'deki %42'den %67.7'ye çıktı — ama bu oran
yükseldi değil, diğer konular azaldı. nativePollOnce sayısı benzer.

Aksiyon: yok. Eylül analizindeki teşhis (geç yığın dökümü) hâlâ geçerli.

### GPU / EGL — 4-5 kullanıcı

```
[libIMGegl.so]     IMGeglSwapBuffersWithDamageKH   2 konu (eski ve yeni sürüm)
[libGLES_mali.so]  glClear                         1 konu
[libGLES_mali.so]  osup_sync_object_wait            1 konu (YENİ)
[libIMGegl.so]     KEGLGetDrawableParameters        1 konu
```

Eylüldeki teşhis aynı: eglSwapBuffers GPU kuyruğu dolduğunda ana thread'i
bloke ediyor. CRT Lite yayınlandığında bu ailedeki sayının düşmesi bekleniyor.

**Yeni:** `osup_sync_object_wait` (Mali GPU sync wait) ilk kez görülüyor.
Aynı GPU darboğaz ailesinin farklı bir görünümü.

### GC / Mono Runtime — 2 kullanıcı (YENİ AİLE)

```
[libart.so] art::gc::Heap::WaitForGcToCompleteLo  1 kullanıcı (v609132220)
[split_config.armeabi_v7a:libmonosgen-2.0.so]      1 kullanıcı (v608260339)
```

Bu aile Eylül analizinde **yoktu**. İki ayrı GC mekanizması:
- ART GC: Java tarafında GC tamamlanma beklemesi
- Mono SGen GC: 32-bit armeabi_v7a cihazlarda bellek basıncı

Aksiyon: Bellek profili çıkarılmalı. Level geçişlerinde kontrolsüz
Texture2D/SoundEffect yaşam döngüsü var mı bakılmalı.

### Google Play Services — 1 kullanıcı

`com.google.android.gms.dynamite.zza` — MobileAds.Initialize zaten arka
thread'e taşınmış. 1 kullanıcı/olay — kabul edilebilir.

### Crash — 1 kullanıcı

`Microsoft.Xna.Framework.Grap` (v607240706, 20 gün önce) — TeardownGuard
filtreleme zaten mevcut. Yeni sürümde görünmemesi bekleniyor.

---

## 3. Yapılan Düzeltme: NotificationReceiver ANR

Eylül analizinde #6 olarak işaretlenmiş NotificationReceiver broadcast
timeout sorunu şimdi düzeltildi:

- `NotificationChannel` oluşturma `MainApplication.OnCreate`'e taşındı
- `NotificationReceiver.OnReceive` artık sadece bildirim gösteriyor
- Kanal ID'si `MainApplication.NotificationChannelId` sabiti ile paylaşılıyor

---

## 4. Güncel Öncelik Sırası

| # | İş | Etki | Durum |
|---|---|------|-------|
| 1 | CRT Lite yayınla, GPU ANR'lerini izle | 4-5 kullanıcı | kod hazır |
| 2 | GC basıncını azalt (bellek profili) | 2 kullanıcı (yeni) | araştırılacak |
| 3 | Yeni sürüm ANR oranını izle | genel oran | izleme |
| 4 | NotificationReceiver timeout | 1 kullanıcı | **düzeltildi** |
| 5 | Shader/müzik/SFX ısıtma genişletme | ilk kullanım donmaları | kısmen var |
