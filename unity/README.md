# Unity tarafı — kurulum ve çekirdek port

Bu klasör Unity projesinin **kaynak dosyalarını** taşır. Unity projesinin kendisi
(`ProjectSettings/`, `Packages/`, `Library/`) sende oluşur ve `Library/` gibi
üretilen klasörler git'e girmez.

> Çekirdek mantık `Assets/Scripts/Core/` altında ve **UnityEngine'e hiç dokunmaz.**
> Bu tesadüf değil: `Samuray.Core.asmdef` içinde `noEngineReferences: true` var,
> yani motora bağımlılık derleyici seviyesinde imkânsız. Mantık böylece Editor'süz
> test edilebilir ve taşınabilir kalıyor.

---

## 1. Projeyi oluştur

Unity Hub → New Project:

- **Unity 6 LTS** (6000.x)
- Şablon: **Universal 2D**
- Konum: bu deponun içinde, klasör adı **`unity`**

Unity `unity/Assets`, `unity/ProjectSettings` vb. oluşturacak. Bu depodaki
`unity/Assets/...` dosyaları onunla birleşecek.

## 2. Platformu hemen çevir

`File → Build Profiles → Android → Switch Platform`

**Şimdi yap.** Sonra çevirirsen bütün asset'ler yeniden import olur, uzun sürer.

## 3. Project Settings

| Ayar | Değer | Neden |
|---|---|---|
| Player → Resolution → Default Orientation | **Portrait** | Oyun dikey |
| Player → Other → Color Space | **Linear** | Kâğıt/mürekkep geçişleri doğru görünsün |
| Player → Other → Scripting Backend | **IL2CPP** | Play Store zorunlu |
| Player → Other → Target Architectures | **ARM64** | Play Store zorunlu |
| Player → Other → Api Compatibility Level | **.NET Standard 2.1** | Çekirdeğin kullandığı API'ler |

## 4. Newtonsoft.Json paketini ekle

`Window → Package Manager → + → Add package by name...`

```
com.unity.nuget.newtonsoft-json
```

**Neden gerekli:** Unity'nin kendi `JsonUtility`'si sözlük okuyamaz, bizim
`rules.json` ise baştan aşağı isimle anahtarlanmış nesneler.

## 5. rules.json'u eşitle

`data/rules.json` **tek kaynaktır.** Unity `Resources/` klasöründen okumak zorunda
olduğu için bir kopya tutuyoruz. Kaynağı her değiştirdiğinde depo kökünde:

```bash
python3 tools/sync_rules.py           # kopyayı güncelle
python3 tools/sync_rules.py --check   # farklıysa hata ver
```

## 6. Testleri çalıştır — kilometre taşı

`Window → General → Test Runner → EditMode → Run All`

**Görsel işe başlamadan önce buranın yeşil olması gerekiyor.** Yeşilse mantığın
Python'dan birebir taşındığından eminsin ve bundan sonraki her hata görsel
katmandadır. Bu, hata ayıklamayı ikiye böler.

## 7. Düello sahnesini kur

Testler yeşilse artık oyunu kurabilirsin. Menüden:

**`Samuray → Düello Sahnesini Kur`**

Bu komut sahneyi sıfırdan kurar ve `Assets/Scenes/Duello.unity` olarak kaydeder:
kamera, iki savaşçı silueti, arena hatları, çizim yüzeyi, HUD ve `DuelController` —
referansları da bağlanmış hâlde.

> Neden menü komutu? Yapı tamamen idiomatik kalıyor (gerçek GameObject'ler, gerçek
> bileşenler, Inspector'dan düzenlenebilir alanlar) ama ilk kurulumdaki onlarca
> sürükle-bırak otomatikleşiyor. Sahne oluştuktan sonra her şeyi elle değiştirebilirsin.

Ayrıca `Assets/Settings/KamaePoseTable.asset` oluşur — **kılıç açıları burada.**
Inspector'dan oynayıp duruşların nasıl göründüğünü ayarlayabilirsin.

## 8. Oyna

**Play** → Ronin karşında. İkiniz **profilden karşı karşıya**: sen solda, düşman sağda.

Düşmanın niyeti artık yazı değil — **senin gövdende** yanıp sönen kırmızı hat,
gelen darbenin nereye ineceğini gösteriyor. Düşmanın kendi gövdesindeki kırmızı
hatlar ise savunduğu yerler.

Kılıç açıları duruşu anlatıyor: JODAN'da tepede, CHUDAN'da uç boğazda,
GEDAN'da alçak, HASSO'da omuzda dik, WAKI'de arkada gizli.

- Üst bölgede fare/parmakla **hat boyunca çiz**
- Kırmızı hatları düşman savunuyor
- Kısa çizgi yarım kalır (yalan), orta hızlı kesim, uzun ağır kesim
- Alttaki duruş butonlarıyla pozisyon değiştir
- **TURU BAŞLAT**

Sayaç 5 saniye. Süre dolarsa gard alırsın, cezalandırılmazsın. Sayacı kapatmak için
`DuelController` Inspector'ında **Turn Seconds** değerini `0` yap.

## 9. Vuruş hissini ayarla

Asıl soru — *vuruş tatmin edici mi* — bu değerlerde cevaplanıyor. Hierarchy'de
`Duello → BeatAnimator` seç, Inspector'dan oyna. **Play sırasında değiştirebilirsin**,
etkisini anında görürsün (Play'den çıkınca değerler sıfırlanır, beğendiğini not al).

| Alan | Ne yapar | Başlangıç |
|---|---|---|
| `Windup` | Darbeden önceki geri çekilme | 0.17 sn |
| `Strike` | Savurma süresi | 0.34 sn |
| `Contact At` | Savurmanın kaçıncı anında temas | 0.68 |
| `Follow` | Kılıçların durulması | 0.33 sn |
| `Hit Stop Normal` | Normal isabette donma | 0.10 sn |
| `Hit Stop Lethal` | Öldürücü vuruşta donma | 0.19 sn |
| `Hit Stop Clash` | Çatışma/parry'de donma | 0.15 sn |
| `Shake Base` / `Shake Per Wound` | Sarsıntı şiddeti | 0.28 / 0.22 |

Hit-stop bilerek `Time.timeScale` ile yapılmıyor — o HUD'u ve sayacı da dondururdu.
`BeatAnimator` kendi saatini tutuyor, donma yalnızca animasyonu etkiliyor.

Duruşlar ayrı yerde: **`Assets/Settings/KamaePoseTable.asset`**. Her duruş için
kılıcın kabza/uç konumu, gövde eğimi, ayak açıklığı ve kalça yüksekliği var.
Kollar iki kemikli IK ile kılıcı takip ediyor — yani sadece kılıcı taşıyorsun,
kollar kendiliğinden uzanıyor.

Ses `Duello → DuelAudio` altında, tamamen kodla üretiliyor (dosya yok).
`Muted` ile kapatılır.

---

## Klasör yapısı

```
Assets/
  Resources/
    rules.json                  data/rules.json'un kopyası (sync_rules.py)
  Scripts/
    Core/                       Samuray.Core.asmdef — MOTORDAN BAĞIMSIZ
      Model.cs                  CutLine, Kamae, Action, Fighter, Hit, BeatResult
      Rules.cs                  rules.json ayrıştırıcı + kural sorguları
      BeatResolver.cs           tur çözümlemesi (saf fonksiyon)
      GestureClassifier.cs      jest tanıma
      Brains.cs                 dört düşman arketipi
    Game/                       Samuray.Game.asmdef — sunum katmanı
      DuelController.cs         faz makinesi, tur döngüsü, sayaç
      StrokeInput.cs            çizim yakalama (YENİ Input System)
      ArenaView.cs              beş hat + gard vurgusu
      FighterView.cs            siluet + kılıç açısı
      KamaePoseTable.cs         ScriptableObject: duruş → kılıç konumu
      HudView.cs                arayüz
      InkTrail.cs               parmağı takip eden mürekkep izi
      Art.cs                    runtime sprite/materyal üretimi
      RulesProvider.cs          rules.json yükleyici
  Editor/                       Samuray.Editor.asmdef
    DuelSceneBuilder.cs         sahneyi kuran menü komutu
  Tests/
    EditMode/                   Samuray.Tests.asmdef
      TestRules.cs              ortak kural yükleyici
      ResolverTests.cs          çözümleme kuralları
      GestureTests.cs           jest tanıma
```

## Çekirdeği kullanma

Çekirdek dosya okuyamaz (motora bağımlı değil), JSON metnini dışarıdan alır:

```csharp
using Samuray.Core;

var json  = Resources.Load<TextAsset>("rules").text;   // sunum katmanı yükler
var rules = Rules.FromJson(json);

var me  = new Fighter("Sen")  { Kamae = Kamae.CHUDAN, Ki = rules.KiStart };
var foe = Brain.Make("RONIN", rules).MakeFighter("Ronin");

var intent = new List<Action> { Action.Cut(CutLine.YOKO) };
var result = BeatResolver.Resolve(me, intent, foe, foeIntent, rules);

me = result.A;  foe = result.B;
foreach (var e in result.Events) Debug.Log(e);
```

Jest tanıma da motordan bağımsız — `Vector2` yerine `Pt` alır:

```csharp
var pts = touchPoints.Select(v => new Pt(v.x, v.y)).ToList();
var g = GestureClassifier.Classify(pts, screenW, screenH, rules);
if (g.Action.HasValue) kuyruk.Add(g);
var niyet = GestureClassifier.AssembleIntent(kuyruk);
```

---

## Bu port nasıl doğrulandı — dürüst not

Bu C# kodu **yazıldığı ortamda derlenemedi** (.NET SDK kurulamadı, Unity yok).
Yapılan doğrulamalar şunlarla sınırlı:

- Python motorunun satır satır birebir aynası olarak yazıldı; olay metinleri dahil
- C# kodunun okuduğu **her JSON anahtarının** `rules.json`'da var olduğu betikle kontrol edildi
- `CutLine` ve `Kamae` enum isimlerinin JSON değerleriyle birebir aynı olduğu doğrulandı
  (`Enum.Parse` bu yüzden çalışıyor)
- Parantez dengesi ve `System.Action` ad çakışması riski tarandı

**Gerçek doğrulama 6. adımda, sende.** Test Runner yeşile dönene kadar bu port
doğrulanmış sayılmaz. Kırmızı bir şey görürsen olduğu gibi yapıştır, düzeltirim.
