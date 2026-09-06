"""Temel veri modelleri: hatlar, duruslar, eylemler, savasci durumu.

Bu modul sadece veri tutar. Kural bilgisi rules.py'de, kural uygulamasi
resolver.py'de. Bu ayrim C# portunu birebir ceviri haline getiriyor.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum


class Line(str, Enum):
    """Bes kesim hatti."""

    SHOMEN = "SHOMEN"          # dikey, yukaridan asagi
    KESA = "KESA"              # inen capraz
    GYAKU_KESA = "GYAKU_KESA"  # yukselen capraz (kiriage)
    YOKO = "YOKO"              # yatay
    TSUKI = "TSUKI"            # saplama


class Kamae(str, Enum):
    """Bes durus. Hem neyi savundugunu hem hangi kesimin ucuz oldugunu belirler."""

    JODAN = "JODAN"    # kilic tepede
    CHUDAN = "CHUDAN"  # kilic ortada, uc bogazda
    GEDAN = "GEDAN"    # kilic asagida
    HASSO = "HASSO"    # kilic sag omuzda
    WAKI = "WAKI"      # kilic gizli - hicbir sey savunmaz, her kesim surpriz


class ActionType(str, Enum):
    CUT = "CUT"
    PARRY = "PARRY"
    FEINT = "FEINT"
    GUARD = "GUARD"
    READ = "READ"


class Injury(str, Enum):
    ARM = "ARM"    # agir kesim yapilamaz
    LEG = "LEG"    # durus degistirilemez
    LUNG = "LUNG"  # Ki yenilenmesi -1


@dataclass(frozen=True)
class Action:
    """Tek bir jestin karsiligi. Oyuncu bunlari cizerek olusturur."""

    type: ActionType
    line: Line | None = None
    heavy: bool = False
    to_kamae: Kamae | None = None  # sadece GUARD icin

    def __str__(self) -> str:
        if self.type is ActionType.CUT:
            return f"{'AGIR' if self.heavy else 'HIZLI'} kesim: {self.line.value}"
        if self.type is ActionType.GUARD:
            hedef = f" -> {self.to_kamae.value}" if self.to_kamae else " (yerinde)"
            return f"Gard{hedef}"
        if self.line is not None:
            return f"{self.type.value}: {self.line.value}"
        return self.type.value


# Bir turda cizilen jestlerin sirali listesi.
Intent = list[Action]


def cut(line: Line, heavy: bool = False) -> Action:
    return Action(ActionType.CUT, line=line, heavy=heavy)


def parry(line: Line) -> Action:
    return Action(ActionType.PARRY, line=line)


def feint(line: Line) -> Action:
    return Action(ActionType.FEINT, line=line)


def guard(to_kamae: Kamae | None = None) -> Action:
    return Action(ActionType.GUARD, to_kamae=to_kamae)


def read() -> Action:
    return Action(ActionType.READ)


@dataclass
class Fighter:
    """Bir savascinin tam durumu. resolve_beat bunu kopyalayip yenisini dondurur."""

    name: str
    kamae: Kamae = Kamae.CHUDAN
    ki: int = 3
    ki_max: int = 4
    wounds: int = 0
    injuries: set[Injury] = field(default_factory=set)

    # Tur arasi tasinan bayraklar
    suki: bool = False       # kendi hatan: gard alamaz VE gelen hasar iki kat
    exposed: bool = False    # gardin kirildi: gard alamaz, ama hasar katlanmaz
    riposte: bool = False    # basarili parry odulu: ilk kesim engellenemez
    staggered: bool = False  # bu turda parry yedi, Ki'si sifirlandi

    sees_through_feints: bool = False

    @property
    def alive(self) -> bool:
        return self.wounds < 3

    def clone(self) -> "Fighter":
        return Fighter(
            name=self.name,
            kamae=self.kamae,
            ki=self.ki,
            ki_max=self.ki_max,
            wounds=self.wounds,
            injuries=set(self.injuries),
            suki=self.suki,
            exposed=self.exposed,
            riposte=self.riposte,
            staggered=self.staggered,
            sees_through_feints=self.sees_through_feints,
        )

    def summary(self) -> str:
        yara = "x" * self.wounds + "." * (3 - self.wounds)
        etiket = [self.kamae.value, f"Ki {self.ki}", f"yara [{yara}]"]
        if self.injuries:
            etiket.append("+".join(sorted(i.value for i in self.injuries)))
        if self.suki:
            etiket.append("SUKI")
        if self.exposed:
            etiket.append("ACIK")
        if self.riposte:
            etiket.append("RIPOSTE")
        return f"{self.name}: " + " | ".join(etiket)
