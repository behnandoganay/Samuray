"""Jest tanima: ekrandaki nokta listesini bir Action'a cevirir.

Hazir $1 Unistroke Recognizer yerine kendi hafif siniflandiricimiz. Jestlerimiz
neredeyse hep duz cizgi ve basit sekil oldugu icin bu hem daha kolay ayarlanir
hem de anlasilir kod olur. Unity tarafina birebir portlanacak olan modul budur.

Ekran iki bolgeye ayrilir:
  - Ust bolge  : kesim alani (cizgiler ve halkalar)
  - Alt serit  : durus secici (bes yatay yuva)
Bu ayrim en buyuk belirsizligi kaldiriyor: asagi dogru bir cizgi hem SHOMEN
kesimi hem "kendine dogru gard" olabilirdi; bolge bunu tek basina cozuyor.
"""

from __future__ import annotations

import math
from dataclasses import dataclass

from .model import Action, ActionType, Kamae, Line
from .rules import Rules

Point = tuple[float, float]  # (x, y), y ekrana asagi dogru buyur


class Tier:
    """Cizginin uzunlugunun anlami."""

    FEINT = "FEINT"  # yarim birakilmis - arkasindan baska cizgi gelirse yalan
    FAST = "FAST"
    HEAVY = "HEAVY"


@dataclass
class Gesture:
    action: Action | None
    tier: str | None = None
    confidence: float = 0.0
    reason: str = ""


# --- geometri yardimcilari -------------------------------------------------

def _path_length(pts: list[Point]) -> float:
    return sum(math.dist(pts[i], pts[i + 1]) for i in range(len(pts) - 1))


def resample(pts: list[Point], n: int) -> list[Point]:
    """Cizgiyi esit araliklarla n noktaya yeniden ornekle."""
    if len(pts) < 2:
        return list(pts)
    toplam = _path_length(pts)
    if toplam <= 0:
        return [pts[0]] * n
    adim = toplam / (n - 1)
    out = [pts[0]]
    birikmis = 0.0
    i = 1
    p = pts[0]
    while i < len(pts) and len(out) < n:
        d = math.dist(p, pts[i])
        if birikmis + d >= adim:
            t = (adim - birikmis) / d if d else 0.0
            yeni = (p[0] + t * (pts[i][0] - p[0]), p[1] + t * (pts[i][1] - p[1]))
            out.append(yeni)
            p = yeni
            birikmis = 0.0
        else:
            birikmis += d
            p = pts[i]
            i += 1
    while len(out) < n:
        out.append(pts[-1])
    return out


def total_turning(pts: list[Point]) -> float:
    """Toplam donus acisi (derece). Duz cizgide ~0, kapali halkada >270."""
    acilar = []
    for i in range(len(pts) - 1):
        dx, dy = pts[i + 1][0] - pts[i][0], pts[i + 1][1] - pts[i][1]
        if dx or dy:
            acilar.append(math.atan2(dy, dx))
    toplam = 0.0
    for i in range(len(acilar) - 1):
        d = acilar[i + 1] - acilar[i]
        while d > math.pi:
            d -= 2 * math.pi
        while d < -math.pi:
            d += 2 * math.pi
        toplam += abs(d)
    return math.degrees(toplam)


def reference_length(width: float, height: float, self_band: float) -> float:
    """Uzunluk esiklerinin olculdugu referans.

    Ekran kosegenini kullanmak yanlisti: bas parmak o boyu cizemez, o yuzden
    AGIR kesim pratikte ulasilamaz oluyordu. Dogru referans, kesim alaninin
    gercekten kat edilebilir olcusu - genislik ile kesim bolgesi yuksekliginin
    kucugu.
    """
    return min(width, height * (1.0 - self_band))


def line_from_angle(deg: float) -> Line:
    """Net vektorun acisini bes hattan birine esle (y ekrana asagi dogru).

    Inen capraz -> KESA, yukselen capraz -> GYAKU_KESA. Iki koseden de gelse
    fark etmez; onemli olan kesimin inip yukselmesi.
    """
    d = (deg + 180.0) % 360.0 - 180.0  # -180..180
    if abs(d) <= 22.5 or abs(d) >= 157.5:
        return Line.YOKO
    if 67.5 < d < 112.5 or -112.5 < d < -67.5:
        return Line.SHOMEN
    return Line.KESA if d > 0 else Line.GYAKU_KESA


# --- ana siniflandirici ----------------------------------------------------

def classify(
    pts: list[Point], width: float, height: float, rules: Rules,
    kamae_slots: list[Kamae] | None = None,
) -> Gesture:
    cfg = rules.gesture
    if len(pts) < 2:
        return Gesture(None, reason="cizgi cok kisa")

    self_band = cfg("self_band_ratio")
    ref = reference_length(width, height, self_band)
    n = int(cfg("resample_points"))
    ornek = resample(pts, n)

    net_dx = ornek[-1][0] - ornek[0][0]
    net_dy = ornek[-1][1] - ornek[0][1]
    net = math.hypot(net_dx, net_dy)
    yol = _path_length(ornek)
    oran = yol / ref
    merkez_y = sum(p[1] for p in ornek) / len(ornek)

    # 1. Alt serit: durus secici. Uzunluk kontrolunden ONCE bakilir, cunku
    #    serit uzerindeki bir dokunus bilerek kisadir.
    if merkez_y >= height * (1.0 - self_band):
        yuvalar = kamae_slots or rules.all_kamae
        merkez_x = sum(p[0] for p in ornek) / len(ornek)
        idx = min(int(merkez_x / width * len(yuvalar)), len(yuvalar) - 1)
        return Gesture(
            Action(ActionType.GUARD, to_kamae=yuvalar[max(0, idx)]),
            confidence=0.9, reason="alt seritte cizildi - durus secimi",
        )

    if oran < cfg("min_length_ratio"):
        return Gesture(None, reason="cizgi cok kisa")

    # 2. Kapali halka: parry
    donus = total_turning(ornek)
    kapali = net <= yol * cfg("loop_closure_ratio")
    if donus >= cfg("loop_turn_degrees") and kapali:
        # Halkanin merkezinin ekrandaki yeri hangi hattin savurulacagini soyler
        cx = sum(p[0] for p in ornek) / len(ornek)
        cy = merkez_y
        aci = math.degrees(math.atan2(cy - height * 0.5, cx - width * 0.5))
        hat = Line.TSUKI if math.hypot(cx - width * 0.5, cy - height * 0.5) < ref * 0.12 \
            else line_from_angle(aci)
        return Gesture(Action(ActionType.PARRY, line=hat), confidence=0.8, reason="kapali halka")

    # 3. Kisa ve dusmana dogru (yukari): saplama
    aci = math.degrees(math.atan2(net_dy, net_dx))
    if net <= ref * cfg("tsuki_max_length_ratio") and net_dy < 0:
        return Gesture(
            Action(ActionType.CUT, line=Line.TSUKI), tier=Tier.FAST,
            confidence=0.75, reason="kisa ileri durtme",
        )

    # 4. Kesim: hat aciyla, baglilik uzunlukla
    hat = line_from_angle(aci)
    if oran < cfg("feint_max_length_ratio"):
        tier = Tier.FEINT
    elif oran >= cfg("heavy_min_length_ratio"):
        tier = Tier.HEAVY
    else:
        tier = Tier.FAST

    duzluk = max(0.0, 1.0 - donus / 180.0)  # duz cizgide 1'e yakin
    return Gesture(
        Action(ActionType.CUT, line=hat, heavy=(tier == Tier.HEAVY)),
        tier=tier, confidence=0.4 + 0.6 * duzluk, reason=f"{tier} kesim",
    )


def assemble_intent(gestures: list[Gesture]) -> list[Action]:
    """Bir turda cizilen jestleri niyete cevir.

    Yarim birakilmis (FEINT kademesi) bir kesim ANCAK arkasindan baska bir jest
    geliyorsa yalan sayilir. Tek basina cizilmisse sadece zayif bir kesimdir.
    Jestin *tamamlanmamisligi* feint'in kendisidir - ayri bir buton yok.
    """
    gecerli = [g for g in gestures if g.action is not None]
    out: list[Action] = []
    for i, g in enumerate(gecerli):
        act = g.action
        son_mu = i == len(gecerli) - 1
        if act.type is ActionType.CUT and g.tier == Tier.FEINT and not son_mu:
            out.append(Action(ActionType.FEINT, line=act.line))
        else:
            out.append(act)
    return out
