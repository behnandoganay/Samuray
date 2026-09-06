# Samuray

Mobil bir samuray düellosu. Dokunmatik ekrana **niyetini çizersin**, iki taraf eşzamanlı
çözümlenir. Refleks değil planlama oyunu.

> **Tasarımın kalbindeki tek kural:** kılıcının bittiği yer, bir sonraki turun
> başladığı yerdir. Bu, oyunu jest oyunundan pozisyon oyununa çevirir.

Tam tasarım: **[docs/tasarim.md](docs/tasarim.md)**

## Bu repoda ne var

Bu aşamada Unity projesi yok. Mekaniğin kendisi, Unity'den bağımsız çalışan ve
test edilebilir bir çekirdek olarak duruyor — sayılar oturmadan sunum katmanına
geçmenin anlamı yok.

```
docs/tasarim.md         Oyun tasarım dokümanı
data/rules.json         TEK ayar kaynağı: duruş tablosu, Ki maliyetleri, hasar
sim/samuray/model.py    Hatlar, duruşlar, eylemler, savaşçı durumu
sim/samuray/rules.py    rules.json yükleyici ve kural sorguları
sim/samuray/resolver.py Tur çözümlemesi - motorun kalbi (saf fonksiyon)
sim/samuray/brains.py   Düşman arketipleri
sim/samuray/gestures.py Jest tanıyıcı (Unity'ye birebir portlanacak modül)
sim/samuray/duel.py     Düello döngüsü
sim/cli.py              Terminalde oynanabilir düello
sim/balance.py          Denge laboratuvarı
sim/tests/              47 test (stdlib unittest, ek bağımlılık yok)
```

`resolve_beat()` saf bir fonksiyondur: rastgelelik yok, dosya yok, Unity yok. Bu saflık
hem testi hem de C# portunu düz iş haline getiriyor.

## Çalıştırma

Bağımlılık yok, Python 3.11+ yeterli. Hepsi `sim/` içinden çalıştırılır:

```bash
cd sim

# 1) Testler
python3 -m unittest discover tests -v

# 2) Terminalde bir düello oyna
python3 cli.py --dusman RONIN          # OGRENCI | BLOFCU | USTA

# 3) Denge raporu
python3 balance.py --runs 2000
```

### Denge ayarı

Sayılar **koda değil** `data/rules.json`'a yazılıdır. Ayar yaparken sadece o dosyaya
dokunulur; testler kuralları sınar, sayıları değil, dolayısıyla ayar testleri kırmaz.

`balance.py` şunlara bakar: hiçbir duruşun kullanımı ezici olmamalı, beş hat da
kullanılmalı, ortalama düello 5–12 tur sürmeli, ilk oyuncu avantajı olmamalı.

## Unity

C# portu `unity/` altında: çekirdek mantık, jest tanıyıcı, dört düşman arketipi ve
testlerin tamamı. Çekirdek **UnityEngine'e hiç dokunmuyor** (asmdef'te
`noEngineReferences`), yani Editor'süz test edilebiliyor.

Kurulum ve doğrulama adımları: **[unity/README.md](unity/README.md)**

`data/rules.json` tek kaynaktır; Unity kopyası `python3 tools/sync_rules.py` ile
eşitlenir.

## Sonraki adım

Unity sunum katmanı (`unity/Assets/Scripts/Game/`): girdi yakalama, fırça izleri,
animasyon, mürekkep estetiği. Ayrıntılar tasarım dokümanının 12. bölümünde.
