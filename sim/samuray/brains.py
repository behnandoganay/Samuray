"""Dusman arketipleri.

Her arketip oyuncuyu farkli bir aliskanligindan vazgecmeye zorlar. Beyinler
karar verirken rastgelelik kullanabilir (tohumlanmis), ama resolve_beat saf
kalir - rastgelelik hep burada, cozumlemede degil.

telegraph() oyuncunun planlama fazinda GORDUGU niyeti dondurur. Durust
arketiplerde bu gercek niyettir; blofcude yalan olabilir. READ eylemi bu
yalani acar.
"""

from __future__ import annotations

import random

from .model import Action, ActionType, Fighter, Injury, Kamae, Line, cut, feint, guard, parry
from .rules import Rules


class Brain:
    """Ortak arayuz. choose() bir turluk niyet uretir."""

    key = "BRAIN"
    label = "Beyin"
    lies = False
    sees_through_feints = False
    aggression = 0.6

    def __init__(self, rules: Rules):
        self.rules = rules
        cfg = rules.brain_config(self.key)
        self.label = cfg["label"]
        self.ki_max = cfg["ki_max"]
        self.lies = cfg["lies"]
        self.sees_through_feints = cfg["sees_through_feints"]
        self.aggression = cfg["aggression"]

    # --- yardimcilar ------------------------------------------------------

    def affordable_cuts(self, me: Fighter, budget: int) -> list[Action]:
        """Butcenin yettigi butun kesimler."""
        out = []
        for line in self.rules.all_lines:
            for heavy in (False, True):
                if heavy and Injury.ARM in me.injuries:
                    continue
                if self.rules.cut_cost(me.kamae, line, heavy) <= budget:
                    out.append(cut(line, heavy))
        return out

    def open_lines(self, foe: Fighter) -> set[Line]:
        """Dusmanin su an savunmadigi hatlar."""
        return set(self.rules.all_lines) - self.rules.guards_of(foe.kamae)

    def best_cut(self, me: Fighter, foe: Fighter, budget: int) -> Action | None:
        """Butcedeki en iyi kesim.

        Acik hatta agir kesim (2 yara) bitirici; gardli hatta agir kesim (1 yara
        ama rakip gelecek tur acikta) kurulum. Ikisi de tek bir tercih fonksiyonunda.
        """
        acik = self.open_lines(foe)
        adaylar = self.affordable_cuts(me, budget)
        if not adaylar:
            return None

        def degerlendir(c: Action) -> tuple[float, float, bool]:
            """(skor, bu turki yara, kurulum mu) dondurur.

            Skor skalerdir cunku maliyet gercekten tartilmali. Sirali bir demet
            kullanmak ham hasari maliyetin onune koyuyordu ve gard kirma hicbir
            zaman secilemiyordu: acik hatta agir kesim (2 yara) her zaman gardli
            hatta agir kesimi (1 yara) yener. Oysa gard kirma tam olarak acik
            hatlarin ZORLANAN, gardli hattin DOGAL oldugu durumda karlidir -
            orada ucuzdur. Bu ancak maliyet tartilirsa ortaya cikar.
            """
            hedef_acik = c.line in acik
            if hedef_acik:
                yara = self.rules.dmg("heavy_cut" if c.heavy else "fast_cut")
            else:
                yara = self.rules.dmg("guard_break_chip") if c.heavy else 0
            kurulum = c.heavy and not hedef_acik and self.rules.guard_break_opens
            maliyet = self.rules.cut_cost(me.kamae, c.line, c.heavy)

            skor = float(yara)
            if foe.wounds + yara >= self.rules.wounds_to_die:
                skor += 100.0  # bitirici her seyin onunde
            if kurulum:
                # Gard kirmanin asil degeri hasar degil KONUM: rakibi zorla
                # GEDAN'a iter, yani gelecek tur nerede duracagini sen secersin.
                # Ustune gard alamaz ve nefes kaybeder.
                ek = float(self.rules.dmg("fast_cut"))  # gard alamaz -> kesim gecer
                if self.rules.dmg("guard_break_ki_drain") >= foe.ki:
                    ek += self.rules.dmg("fast_cut")  # rakip turunu tamamen kaybeder

                # Konum kazanci: GEDAN'a itmek benim ucuz kesimlerimden kacini
                # savunmaktan cikariyor?
                simdi = len(self.rules.natural_lines(me.kamae) & self.rules.guards_of(foe.kamae))
                sonra = len(self.rules.natural_lines(me.kamae) & self.rules.guards_of(Kamae.GEDAN))
                ek += max(0, simdi - sonra)
                skor += ek * 0.6
            return (skor - 0.5 * maliyet, yara, kurulum)

        en_iyi = max(adaylar, key=lambda c: degerlendir(c)[0])
        _, yara, kurulum = degerlendir(en_iyi)
        return en_iyi if yara > 0 or kurulum else None

    def waki_gambit(self, me: Fighter, foe: Fighter) -> bool:
        """Gizli durusa girmeye deger mi?

        WAKI bir tur tamamen savunmasiz kalmak demek, ama sonrasinda her kesim
        ucuz, +1 hasarli ve okunamaz. Yani ancak geride kalindiginda ve rakip
        o turu cezalandiracak nefese sahip degilken mantikli bir kumar.
        """
        if me.kamae is Kamae.WAKI or Kamae.WAKI not in self.rules.adjacent(me.kamae):
            return False
        if Injury.LEG in me.injuries:
            return False
        geride = me.wounds > foe.wounds
        rakip_yorgun = foe.ki <= 1
        return geride and rakip_yorgun

    def make_fighter(self, name: str, kamae: Kamae = Kamae.CHUDAN) -> Fighter:
        return Fighter(
            name=name, kamae=kamae, ki=min(self.rules.ki_start, self.ki_max),
            ki_max=self.ki_max, sees_through_feints=self.sees_through_feints,
        )

    # --- arayuz -----------------------------------------------------------

    def choose(self, me: Fighter, foe: Fighter, rng: random.Random) -> list[Action]:
        raise NotImplementedError

    def telegraph(self, intent: list[Action], me: Fighter, rng: random.Random) -> list[Action]:
        """Oyuncuya gosterilen niyet. Yalan soylemeyen beyinlerde gercegin aynisi."""
        if me.kamae is Kamae.WAKI:
            return []  # gizli durustan niyet okunmaz
        return intent


class Ronin(Brain):
    """Durust telegraf, dar butce, basit akis. Oyuncuya okuma ve akisi ogretir."""

    key = "RONIN"

    def choose(self, me, foe, rng):
        acik = self.open_lines(foe)
        secenek = [c for c in self.affordable_cuts(me, me.ki) if c.line in acik]
        if not secenek or rng.random() > self.aggression:
            if me.ki <= 1:
                return [guard(rng.choice(sorted(self.rules.adjacent(me.kamae), key=lambda k: k.value)))]
            return [guard()]
        # En ucuz acik kesimi tercih eder - nefesini idareli kullanir
        secenek.sort(key=lambda c: self.rules.cut_cost(me.kamae, c.line, c.heavy))
        return [secenek[0]]


class Ogrenci(Brain):
    """Surekli CHUDAN'a doner, asiriya kacmayi cezalandirir. Ki disiplinini ogretir.

    Rakip acik (SUKI) verdiginde en agir kesimi indirir; aksi halde bekler ve
    en olasi hatti savurmaya calisir.
    """

    key = "OGRENCI"

    def choose(self, me, foe, rng):
        if foe.suki:
            vurus = self.best_cut(me, foe, me.ki)
            if vurus is not None:
                return [vurus]

        if self.waki_gambit(me, foe):
            return [guard(Kamae.WAKI)]

        # Rakip nefes topluyorsa savurma bosa gider; bunun yerine pozisyon al
        if foe.ki <= 1:
            if me.kamae is not Kamae.CHUDAN and Kamae.CHUDAN in self.rules.adjacent(me.kamae):
                return [guard(Kamae.CHUDAN)]
            return [guard()]

        if me.ki >= 2 and rng.random() < self.aggression:
            vurus = self.best_cut(me, foe, me.ki - self.rules.cost("parry"))
            if vurus is not None:
                # Bir Ki'yi savurmaya ayirir - hem vurur hem korunur
                tahmin = rng.choice(sorted(self.rules.natural_lines(foe.kamae), key=lambda x: x.value))
                return [vurus, parry(tahmin)]

        if me.ki >= 1:
            tahmin = rng.choice(sorted(self.rules.natural_lines(foe.kamae), key=lambda x: x.value))
            return [parry(tahmin)]
        return [guard()]


class Blofcu(Brain):
    """Telegrafinin yarisi yalan. Oyuncuyu READ eylemini kullanmaya zorlar."""

    key = "BLOFCU"

    def choose(self, me, foe, rng):
        # Feint'in dogru kullanimi: ACIK bir hatta yalan soyle ki gard oraya
        # kaysin, sonra bosalan KORUNAN hattan gercek kesimi indir. Tersi
        # (korunan hatta yalan) hicbir zaman ise yaramaz - var olan bir garda
        # yalan soyleyemezsin.
        acik = self.open_lines(foe)
        korunan = self.rules.guards_of(foe.kamae)

        if korunan and rng.random() < self.aggression:
            butce = me.ki - self.rules.cost("feint")
            hedefler = [c for c in self.affordable_cuts(me, butce) if c.line in korunan]
            yalanlar = sorted(acik, key=lambda x: x.value)
            if hedefler and yalanlar:
                gercek = min(hedefler,
                             key=lambda c: self.rules.cut_cost(me.kamae, c.line, c.heavy))
                return [feint(rng.choice(yalanlar)), gercek]

        # Yalan kurulamiyorsa duz saldiri
        vurus = self.best_cut(me, foe, me.ki)
        if vurus is not None and rng.random() < self.aggression:
            return [vurus]
        if self.waki_gambit(me, foe):
            return [guard(Kamae.WAKI)]
        if me.ki <= 1:
            return [guard()]
        return [parry(rng.choice(sorted(self.rules.natural_lines(foe.kamae), key=lambda x: x.value)))]

    def telegraph(self, intent, me, rng):
        if me.kamae is Kamae.WAKI:
            return []
        if not self.lies or not intent or rng.random() > 0.5:
            return intent
        # Gercek kesimi baska bir hatta gosterir
        sahte = []
        for act in intent:
            if act.type is ActionType.CUT:
                baska = [l for l in self.rules.all_lines if l != act.line]
                sahte.append(cut(rng.choice(baska), act.heavy))
            else:
                sahte.append(act)
        return sahte


class Usta(Brain):
    """Boss. Feint gormez ve oyuncunun akis grafigini okur: bir durus dizisini
    tekrarlarsan o durustan gelen dogal kesimleri onceden savurur.

    Oyuncunun ezberini ona karsi silaha cevirir - final dovusunun butun agirligi
    bu davranista.
    """

    key = "USTA"

    def __init__(self, rules: Rules):
        super().__init__(rules)
        self.gecmis: list[Kamae] = []

    def choose(self, me, foe, rng):
        self.gecmis.append(foe.kamae)
        tekrar = self.gecmis.count(foe.kamae) >= 3

        if foe.suki or foe.staggered:
            vurus = self.best_cut(me, foe, me.ki)
            if vurus is not None:
                return [vurus]

        if self.waki_gambit(me, foe):
            return [guard(Kamae.WAKI)]

        # Oyuncu ayni durusa saplanmissa oradan gelecek dogal kesimi savurur
        if tekrar and me.ki >= self.rules.cost("parry") + 1:
            dogal = sorted(self.rules.natural_lines(foe.kamae), key=lambda x: x.value)
            acik = self.open_lines(foe)
            karsilik = self.best_cut(me, foe, me.ki - self.rules.cost("parry"))
            if dogal and karsilik is not None:
                return [parry(dogal[0]), karsilik]

        vurus = self.best_cut(me, foe, me.ki)
        if vurus is not None and rng.random() < self.aggression:
            return [vurus]
        if me.ki <= 1:
            return [guard()]
        return [parry(rng.choice(sorted(self.rules.natural_lines(foe.kamae), key=lambda x: x.value)))]

    def telegraph(self, intent, me, rng):
        if me.kamae is Kamae.WAKI:
            return []
        if rng.random() > 0.5:
            return intent
        sahte = [cut(rng.choice(self.rules.all_lines), a.heavy) if a.type is ActionType.CUT else a
                 for a in intent]
        return sahte


ARKETIPLER: dict[str, type[Brain]] = {
    "RONIN": Ronin,
    "OGRENCI": Ogrenci,
    "BLOFCU": Blofcu,
    "USTA": Usta,
}


def make_brain(key: str, rules: Rules) -> Brain:
    return ARKETIPLER[key.upper()](rules)
