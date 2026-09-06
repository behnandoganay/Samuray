# Samuray — Tasarım Dokümanı

> Yaşayan doküman. Sayılar `data/rules.json`'dan gelir; buradaki tablolar o dosyanın
> anlatımıdır. Denge ayarı koda değil, o dosyaya yapılır.

## 1. Fikrin çıkış noktası ve çözülen gerilim

Başlangıç fikri: dokunmatik ekrana saldırı/savunma deseni çiz, rakibi oku, öldür.
İçinde çözülmesi gereken bir gerilim vardı:

- Çizim doğası gereği bir **refleks** mekaniğidir (Fruit Ninja).
- Planlama ise bir **düşünme** mekaniğidir.

Çözüm, çizimi hız testi olmaktan çıkarıp **komut diline** dönüştürmek. Parmağınla
refleks göstermiyorsun, parmağınla emir yazıyorsun. Zaman planlama fazında
neredeyse durur, niyetini çizersin, sonra iki taraf eşzamanlı çözümlenir.

### Neden samuray düellosu, dövüş oyunu değil

| | Samuray düellosu | Dövüş oyunu |
|---|---|---|
| Girdi | Tur başına birkaç jest | Sürekli gerçek zamanlı girdi |
| Duraklama | Anlatısal olarak haklı (vuruş öncesi bakışma türün kendisi) | Türü bozar |
| Ölümcüllük | Yüksek, az vuruş — "bir kere doğru vur" | Uzun kombolar, HP çubuğu |
| Üretim | İki karakter, tek düzlem, sabit kamera | Kadro + moveset |

Dokunmatik ekran sürekli girdide kötüdür ve gerçek zamanlı olmak planlamayı öldürür.
Samuray düellosu ise duraklamayı zaten hikâyenin parçası yapar.

**Referanslar:** Into the Breach (düşmanın niyetini görüp cevabı planlamak),
Sekiro (parry'nin tadı), Bushido Blade (ölümcüllük ve duruşlar), Slay the Spire
(roguelite döngü).

---

## 2. Tasarımın kalbindeki tek kural

> **Kılıcının bittiği yer, bir sonraki turun başladığı yerdir.**

Bu kural oyunu jest oyunundan **pozisyon oyununa** çevirir. Büyük bir tepeden vuruş
yaparsan kılıcın aşağıda kalır ve yukarısı açılır. Düşman bunu okur. Derinlik çizimin
güzelliğinden değil, konumun sonuçlarından doğar — satrançtaki taş konumu gibi.

Bitiş duruşu **sadece hatta bağlıdır**, başlangıç duruşuna değil. Oyuncunun
ezberleyeceği beş kural:

| Kesim | Biter |
|---|---|
| SHOMEN (dikey) | GEDAN |
| KESA (inen çapraz) | HASSO |
| GYAKU_KESA (yükselen çapraz) | JODAN |
| YOKO (yatay) | HASSO |
| TSUKI (saplama) | CHUDAN |

---

## 3. Beş hat ve beş duruş

Beş hat bir **çember** oluşturur: `SHOMEN – KESA – YOKO – GYAKU_KESA – TSUKI –` (başa).
Her duruş bu çemberden **farklı bir komşu çifti** savunur. Bu kural sayesinde hiçbir
duruş bir diğerinin kopyası değildir.

| Kamae | Savunur | Ucuza keser (1 Ki) | Komşuları |
|---|---|---|---|
| `JODAN` (tepede) | SHOMEN, KESA | SHOMEN, KESA | HASSO, CHUDAN |
| `CHUDAN` (ortada) | SHOMEN, TSUKI | TSUKI, YOKO | JODAN, HASSO, GEDAN, WAKI |
| `GEDAN` (aşağıda) | YOKO, GYAKU_KESA | GYAKU_KESA, TSUKI | CHUDAN, HASSO, WAKI |
| `HASSO` (sağ omuz) | KESA, YOKO | KESA, YOKO | JODAN, CHUDAN, GEDAN |
| `WAKI` (gizli) | **hiçbir şey** | hepsi, +1 hasar, okunamaz | GEDAN, CHUDAN |

### Akış grafiği

Ucuz kesimleri takip edince iki doğal döngü çıkar:

```
  GEDAN --gyaku_kesa--> JODAN --shomen--> GEDAN          (kiriage / kirioroshi döngüsü)
  GEDAN --tsuki--> CHUDAN --yoko--> HASSO --kesa--> HASSO (uzun döngü)
```

Oyuncu önce bu döngüleri keşfeder, sonra bunları ezberleyen düşmanlar (Usta) onu
döngüden çıkmaya zorlar. Döngüden çıkmak pahalıdır — işte stratejik derinlik burada.

**SHOMEN özel durumu:** ucuza sadece JODAN'dan atılır, JODAN'a ise sadece yükselen bir
kesimle varılır, üstelik iki duruş birden onu savunur. Yani tepeden inen ölümcül kesim
gerçek bir kurulum ister. Tasarım niyeti bu; denge raporunda en az kullanılan hat
olması (%4) beklenen sonuç, ama fazla nadir kalırsa `natural` tablosundan gevşetilebilir.

### WAKI — gizli duruş

Hiçbir hat WAKI'de bitmez. Oraya ancak bir tur **gard alarak** girilir ve düşman bunu
görür. Yani "çekilişe gidiyorum" diye açıkça ilan edersin, bir tur tamamen savunmasız
kalırsın; karşılığında her kesim ucuz, +1 hasarlı ve telegrafın okunamaz olur.
Turda tek eylem hakkın vardır. Görünür, dramatik, geri dönüşü olmayan bir kumar.

---

## 4. Ki ekonomisi (nefes)

- Maksimum 4, başlangıç 3, tur başı **+2** yenilenir, gard alırsan **+1** daha.
- Bir turda en fazla **3** harcanır.
- Kesim maliyeti iki **bağımsız eksenin** toplamıdır:

| | Hızlı (kısa çizgi) | Ağır (uzun çizgi) |
|---|---|---|
| **Doğal hat** (duruşuna uygun) | 1 Ki → 1 yara | 2 Ki → 2 yara |
| **Zorlanan hat** | 2 Ki → 1 yara | 3 Ki → 2 yara |

Konum ekseni ve bağlılık ekseni ayrı kararlardır. Nereye vuracağın kadar *ne kadar
yükleneceğin* de ayrı bir seçim.

> **Yenilenme (+2) < harcama tavanı (3).** Bu tek eşitsizlik oyunun temposunu kuruyor:
> her tur tam güç saldıramazsın, nefes almak zorundasın. Saldır–nefeslen–saldır ritmi
> buradan doğuyor.

Diğer maliyetler: parry 1, feint 1, okuma 1, gard 0.

---

## 5. Jest dili

Tek bir çizgi aynı anda birden fazla karar taşır. Menü değil, dil.

Ekran iki bölgeye ayrılır — bu ayrım en büyük belirsizliği kaldırır (aşağı inen bir
çizgi hem SHOMEN kesimi hem "kendine doğru gard" olabilirdi):

- **Üst bölge (%68):** kesim alanı
- **Alt şerit (%32):** duruş seçici, beş yatay yuva. Baş parmağın zaten orada durur.

| Çizim | Eylem |
|---|---|
| Bir hat boyunca düz çizgi | O hatta kesim. **Uzunluk = bağlılık** |
| Düşmana doğru kısa dürtme | Tsuki |
| Kapalı halka | O bölgedeki hattı parry et |
| Alt şeritte dokunuş/kaydırma | Duruş değiştir |
| **Yarım bırakılmış çizgi**, ardından ikinci çizgi | İlki **feint** |

**Feint'in zarafeti:** jestin *tamamlanmamışlığı* feint'in kendisidir. Ayrı bir buton
yok — yarım çizersen yalan, tam çizersen gerçek. Yarım çizgi tek başına kalırsa sadece
zayıf bir kesimdir.

**Feint kuralı:** yalan ancak düşmanın **savunmadığı** bir hatta işe yarar; gardı oraya
kayar ve önceden koruduğu hat boşalır. Var olan bir garda yalan söyleyemezsin.

Uzunluk eşikleri ekran köşegenine göre değil, **ulaşılabilir referansa** göre ölçülür:
`min(genişlik, kesim bölgesi yüksekliği)`. Köşegen kullanmak ağır kesimi baş parmakla
çizilemez hale getiriyordu.

---

## 6. Tur çözümlemesi

Her tur üç faz: **oku → planla → çözümle.**

Planlama fazında zaman durmaz, çok yavaşlar (~4-5 sn). Süre biterse kılıcın olduğu
yerde gard alırsın, cezalandırılmazsın.

Çözümleme sırası:

1. **Ki harcanır.** Elindekinden fazlasını harcamak serbesttir — bedeli SUKI.
2. **Gard kümeleri** belirlenir (duruşa göre; savunmasızsan boş).
3. **Feint'ler** gardı kaydırır.
4. **Parry'ler** kesimleri iptal eder: doğru hattı yakalarsan saldıran sersemler,
   tüm nefesini kaybeder, sen bir sonraki turda **riposte** kazanırsın (ilk kesimin
   ne gardla ne parry ile durdurulabilir).
5. **Aynı hatta karşılıklı kesim = çatışma.** İkisi de savrulur, az yüklenen dengesini
   kaybeder.
6. **Kalan kesimler** çözülür.
7. **Duruşlar** güncellenir.
8. **Ki yenilenir**, bayraklar bir sonraki tura taşınır.

### İki farklı savunmasızlık — bilerek eşit değiller

| | Nasıl olur | Sonucu |
|---|---|---|
| **SUKI** | Kendi hatan: nefesinin ötesine geçtin, çatışmayı kaybettin | Gard yok **ve gelen hasar iki kat** |
| **AÇILMA** | Rakip gardını ağır kesimle kırdı | Gard yok, hasar katlanmaz |

Kendi hatan, zorlanmaktan daha pahalıya mal olur.

### Gard bir pat butonu değil

Ağır kesim gardlı bir hatta gelirse gardı kırar: 1 yara sızdırır, savunanı GEDAN'a
iter, nefesini azaltır ve gelecek tur gard alamaz hale getirir.

**Gard kırmanın asıl değeri hasar değil konumdur** — rakibin gelecek tur nerede
duracağını sen seçersin. Bu, tek hamle ileri bakan bir botun ölçemeyeceği bir değer;
denge raporundaki düşük oranı bu yüzden gerçek değerin alt sınırı sayılmalı.

---

## 7. Ölümcüllük — HP yok, yara var

**3 yara = ölüm**, iki taraf için de. Her yara ayrıca kalıcı olarak bir şeyi kapatır,
ve türü **vuran hatta** göre belirlenir:

| Hat | Yara | Etkisi |
|---|---|---|
| SHOMEN, KESA | `ARM` | Ağır kesim (2+ Ki) yapamazsın |
| YOKO, GYAKU_KESA | `LEG` | Gard eylemiyle duruş değiştiremezsin |
| TSUKI | `LUNG` | Ki yenilenmesi −1 |

Bu, her teması korkutucu yapar ve "sünger düşmanı yontma" hissini tamamen siler.
Düellolar ortalama ~6 tur sürer; bu kısalık hatanın değil **niyetin** sonucudur.

---

## 8. Düşman arketipleri

Her arketip oyuncuyu farklı bir alışkanlığından vazgeçmeye zorlar.

| Arketip | Yapısı | Neyi öğretir |
|---|---|---|
| **Ronin** | Dürüst telegraf, dar bütçe (3 Ki), basit akış | Temel okuma ve akış grafiği |
| **Öğrenci** | CHUDAN'a döner, aşırıya kaçmayı cezalandırır | Ki disiplini |
| **Blöfçü** | Telegrafının yarısı yalan; açık hatta feint atıp korunan hattan vurur | Okuma eylemini kullanmayı |
| **Usta** (boss) | Feint görmez; senin akış grafiğini okur, bir duruşu tekrarlarsan önceden savurur | Çeşitlilik |

Boss'un "tekrarı cezalandırma" davranışı finalin bütün ağırlığını taşıyor: oyuncunun
ezberini ona karşı silaha çeviriyor.

**Henüz yazılmamış, tasarlanmış arketipler:** Mızraklı (menzil, yaklaşmak 1 Ki),
Çift kılıç (3 hat savunur, 2 Ki), Zincirli tırpan (silah düşürür; WAKI'de bağışıksın).

---

## 9. Roguelite yapı

Bir koşu = yol boyunca 8–12 düello (*musha shugyō*). 5–10 dakika, mobil oturumuna birebir.

Düğüm tipleri: **düello**, **tapınak** (bir yarayı iyileştir), **dojo** (yeni kata
öğren), **tüccar** (kılıç değiştir).

- **Kata** = tek jest olarak tanınan çok vuruşlu form. Sekiz: iki hat birden, parry
  edilemez, 3 Ki. Z: üç hızlı kesim. Spiral: bir tur her şeyi parry, çok pahalı.
- **Kılıçlar** = menzil/Ki takası. Nodachi: uzun, +hasar, akış yavaş.
  Wakizashi: ucuz Ki, düşük hasar, turda bir ekstra eylem.

---

## 10. Sanat ve his

**Sumi-e (mürekkep) estetiği:** siyah, kirli beyaz kâğıt, tek bir kırmızı. Oyuncunun
parmağı ekrana gerçekten mürekkep sürüyor; kılıç izleri fırça darbesi, ölümde mürekkep
sıçrıyor. Ucuz üretilir, pahalı görünür ve **girdiyle sanatı aynı şeyde birleştirir**.

- **Planlama fazı:** ağır slow-motion, renk çekilir, nefes/kalp sesi. Sen çizerken
  hayalet bir fırça izi kesimini önizler; gardlı bir hatta gidiyorsa o hat kızarır.
- **Çözümleme fazı:** 0.6–1.2 sn hızlı ve şiddetli hareket, temasta hit-stop, ekran
  sarsıntısı, sonra sessizlik ve tek bir taiko vuruşu.
- **Düelloda hiç sayı gösterme.** Ki = duruşun gerginliği ya da bir sıra mürekkep
  noktası. Yaralar = siluetin üstünde kırmızı mürekkep.

> En büyük risk sıra tabanlı çizimin ölü hissettirmesi. Panzehir tamamen bu maddede:
> çözümleme kısa ve vahşi olmalı, planlama fazı "zaman duruyor" değil
> **"zaman ağırlaşıyor"** gibi görünmeli.

---

## 11. Denge laboratuvarının bulduğu tasarım hataları

Motor yazıldıktan sonra `sim/balance.py` üç gerçek hata yakaladı. Kayda geçiriyorum
çünkü ikisi kâğıt üstünde fark edilmemişti:

1. **JODAN ile HASSO birebir aynıydı.** Aynı hatları savunuyor, aynı kesimleri ucuza
   veriyorlardı — beş duruşun ikisi kopyaydı. HASSO %1 kullanımda görününce yakalandı.
   Düzeltme: beş hattı çembere dizip her duruşa farklı bir komşu çift vermek.
   Artık `test_hicbir_durus_bir_digerinin_kopyasi_degil` bunu koruyor.
2. **GEDAN kara deliğiydi (%55).** Üç hat birden orada bitiyor, üstüne çatışma /
   sersemleme / gard kırma da oraya itiyordu. Düzeltme: KESA'nın bitişini HASSO'ya almak.
3. **Blöfçü feint'i ters kullanıyordu.** Rakibin zaten koruduğu hatta yalan atıyordu,
   yani her turda 1 Ki'yi çöpe atıyordu. Düzeltmeden sonra zorluk merdiveni monotonik
   hale geldi: Ronin < Öğrenci < Blöfçü < Usta.

### Açık kalan sorular

- **WAKI botlarda ~%0.** Gizli duruş bir oyuncu aracı; açgözlü botlar bir turluk
  savunmasızlığı hiç göze almıyor. Gerçek oyuncuyla test edilmeli.
- **SHOMEN %4** ile en nadir hat. Tasarım niyeti bu (kurulum ister) ama fazla nadir
  kalıyorsa `natural` tablosundan gevşetilebilir.
- **Öğrenci–Öğrenci ~20 tur.** İki savunmacı karşılaşınca pat oluyor. Tek bir eşleşmede
  olduğu için şimdilik kabul edilebilir.

---

## 12. Sonraki fazlar

- **Faz 2:** `Samuray.Core` C# portu (UnityEngine bağımlılığı sıfır, asmdef ile ayrık).
  `data/rules.json` aynen kullanılır. `sim/tests/` portun sözleşmesidir.
- **Faz 3:** Unity sunum katmanı — girdi yakalama, TrailRenderer fırça izleri,
  animasyon, ScriptableObject düşman tanımları.
- **Faz 4:** Dikey dilim — tek düşman, üç tur, mürekkep estetiği, dokunulabilir prototip.
