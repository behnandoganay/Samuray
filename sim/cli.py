#!/usr/bin/env python3
"""Terminalde oynanabilir duello.

Mekanigi kagit ustunde degil ELDE hissetmek icin. Jest cizmek yerine komut
yaziyorsun ama altta calisan motor Unity'de calisacak olanin aynisi.

Kullanim:  python3 cli.py [--dusman RONIN|OGRENCI|BLOFCU|USTA] [--seed 42]
"""

from __future__ import annotations

import argparse
import random
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from samuray.brains import ARKETIPLER, make_brain
from samuray.model import (Action, ActionType, Fighter, Kamae, Line, cut, feint,
                           guard, parry, read)
from samuray.resolver import action_cost, intent_cost, resolve_beat
from samuray.rules import load_rules

HAT_KISA = {"sh": Line.SHOMEN, "shomen": Line.SHOMEN,
            "ke": Line.KESA, "kesa": Line.KESA,
            "gy": Line.GYAKU_KESA, "gyaku": Line.GYAKU_KESA, "gyaku_kesa": Line.GYAKU_KESA,
            "yo": Line.YOKO, "yoko": Line.YOKO,
            "ts": Line.TSUKI, "tsuki": Line.TSUKI}

DURUS_KISA = {"jo": Kamae.JODAN, "jodan": Kamae.JODAN,
              "ch": Kamae.CHUDAN, "chudan": Kamae.CHUDAN,
              "ge": Kamae.GEDAN, "gedan": Kamae.GEDAN,
              "ha": Kamae.HASSO, "hasso": Kamae.HASSO,
              "wa": Kamae.WAKI, "waki": Kamae.WAKI}

YARDIM = """
  KOMUTLAR
    <hat>            hizli kesim        ornek: shomen   (kisa: sh ke gy yo ts)
    agir <hat>       agir kesim         ornek: agir kesa
    savur <hat>      parry              ornek: savur tsuki
    yalan <hat>      feint              ornek: yalan yoko
    gard [durus]     gard al / durus degistir   (kisa: jo ch ge ha wa)
    oku              dusmanin telegrafi yalan mi, ac
    geri             son eylemi geri al
    <bos satir>      turu baslat
    ?  yardim        q  cik

  BES HAT      SHOMEN dikey | KESA inen capraz | GYAKU_KESA yukselen capraz
               YOKO yatay   | TSUKI saplama
"""


def hat_coz(s: str) -> Line | None:
    return HAT_KISA.get(s.lower())


def durum_paneli(rules, oyuncu: Fighter, dusman: Fighter, telegraf, yalan_acildi: bool):
    print("\n" + "=" * 64)
    print(f"  DUSMAN  {dusman.summary()}")
    korur = ", ".join(sorted(x.value for x in rules.guards_of(dusman.kamae))) or "hicbir sey"
    print(f"          savundugu hatlar: {korur}")
    if telegraf is None:
        print("          niyet: OKUNAMIYOR (gizli durus)")
    else:
        okuma = ", ".join(str(a) for a in telegraf) or "bekliyor"
        etiket = "  <- okundu, GERCEK niyet" if yalan_acildi else ""
        print(f"          niyet: {okuma}{etiket}")
    print("-" * 64)
    print(f"  SEN     {oyuncu.summary()}")
    korur = ", ".join(sorted(x.value for x in rules.guards_of(oyuncu.kamae))) or "hicbir sey"
    ucuz = ", ".join(sorted(x.value for x in rules.natural_lines(oyuncu.kamae)))
    print(f"          savundugun: {korur}")
    print(f"          ucuz kesim: {ucuz}")
    print("=" * 64)


def niyet_al(rules, oyuncu: Fighter, dusman_beyin, dusman: Fighter, rng) -> list[Action] | None:
    """Oyuncudan bir turluk niyet topla. None = cikis."""
    niyet: list[Action] = []
    okundu = False
    while True:
        harcanan = intent_cost(oyuncu, niyet, rules)
        kalan = oyuncu.ki - harcanan
        kuyruk = " > ".join(str(a) for a in niyet) or "(bos)"
        print(f"\n  niyet: {kuyruk}")
        print(f"  harcanan {harcanan} / nefes {oyuncu.ki}"
              f"{'  [ACIK VERIYORSUN]' if kalan < 0 else ''}"
              f"  (tur tavani {rules.max_spend})")
        try:
            ham = input("  > ").strip().lower()
        except (EOFError, KeyboardInterrupt):
            return None

        if ham in ("q", "cik", "quit"):
            return None
        if ham in ("?", "yardim", "help"):
            print(YARDIM)
            continue
        if ham == "":
            return niyet
        if ham == "geri":
            if niyet:
                print(f"  geri alindi: {niyet.pop()}")
            continue

        parca = ham.split()
        komut = parca[0]
        arg = parca[1] if len(parca) > 1 else ""

        if komut == "gard":
            hedef = DURUS_KISA.get(arg) if arg else None
            if arg and hedef is None:
                print(f"  '{arg}' diye bir durus yok")
                continue
            if hedef and hedef not in rules.adjacent(oyuncu.kamae) and hedef != oyuncu.kamae:
                komsu = ", ".join(sorted(k.value for k in rules.adjacent(oyuncu.kamae)))
                print(f"  {oyuncu.kamae.value} -> {hedef.value} komsu degil. Komsular: {komsu}")
                continue
            yeni = guard(hedef)
        elif komut == "oku":
            if okundu:
                print("  bu tur zaten okudun")
                continue
            yeni = read()
        elif komut in ("agir", "savur", "yalan"):
            hat = hat_coz(arg)
            if hat is None:
                print(f"  '{arg}' diye bir hat yok. Hatlar: sh ke gy yo ts")
                continue
            yeni = {"agir": lambda h: cut(h, heavy=True),
                    "savur": parry, "yalan": feint}[komut](hat)
        else:
            hat = hat_coz(komut)
            if hat is None:
                print(f"  anlamadim: '{ham}'   (? ile yardim)")
                continue
            yeni = cut(hat)

        maliyet = action_cost(oyuncu, yeni, rules)
        if harcanan + maliyet > rules.max_spend:
            print(f"  '{yeni}' {maliyet} Ki ister, tur tavani {rules.max_spend} asilir")
            continue
        if oyuncu.kamae is Kamae.WAKI and len(niyet) >= rules.waki_max_actions:
            print("  gizli durustan turda tek eylem yapilir")
            continue

        niyet.append(yeni)
        print(f"  + {yeni}  ({maliyet} Ki)")
        if yeni.type is ActionType.READ:
            okundu = True


def oyna(dusman_key: str, seed: int) -> None:
    rules = load_rules()
    rng = random.Random(seed)
    beyin = make_brain(dusman_key, rules)

    oyuncu = Fighter("Sen", kamae=Kamae.CHUDAN, ki=rules.ki_start, ki_max=rules.ki_max)
    dusman = beyin.make_fighter(beyin.label, Kamae.CHUDAN)

    print(f"\n  {'*' * 60}")
    print(f"   Karsinda: {beyin.label}")
    print(f"   3 yara olduruyor. Nefesin tur basi +{rules.ki_regen} yenilenir,")
    print(f"   ama bir turda en fazla {rules.max_spend} harcayabilirsin.")
    print(f"   Yani her tur saldiramazsin - nefes almak zorundasin.")
    print(f"  {'*' * 60}")
    print(YARDIM)

    tur = 0
    while oyuncu.alive and dusman.alive:
        tur += 1
        print(f"\n\n########  TUR {tur}  ########")

        dusman_niyet = beyin.choose(dusman, oyuncu, rng)
        gosterilen = beyin.telegraph(dusman_niyet, dusman, rng)
        durum_paneli(rules, oyuncu, dusman, gosterilen, False)

        niyet = niyet_al(rules, oyuncu, beyin, dusman, rng)
        if niyet is None:
            print("\n  Duellodan cekildin.\n")
            return

        # OKU eylemi gercek niyeti acar ve oyuncuya yeniden karar hakki verir
        if any(a.type is ActionType.READ for a in niyet) and gosterilen != dusman_niyet:
            print("\n  >>> OKUDUN: telegraf yalandi! Gercek niyet asagida.")
            durum_paneli(rules, oyuncu, dusman, dusman_niyet, True)
            print("  Niyetini yeniden kurabilirsin (oku eylemi 1 Ki olarak sayilacak).")
            yeni = niyet_al(rules, oyuncu, beyin, dusman, rng)
            if yeni is None:
                print("\n  Duellodan cekildin.\n")
                return
            niyet = [read()] + [a for a in yeni if a.type is not ActionType.READ]

        sonuc = resolve_beat(oyuncu, niyet, dusman, dusman_niyet, rules)
        print("\n  --- COZUMLEME ---")
        for e in sonuc.events:
            print(f"    {e}")
        if not sonuc.events:
            print("    iki taraf da birbirini yokladi, kilic degmedi")
        oyuncu, dusman = sonuc.a, sonuc.b

    print("\n" + "=" * 64)
    if oyuncu.alive and not dusman.alive:
        print(f"  {beyin.label} dustu. {tur} turda bitti.")
    elif dusman.alive and not oyuncu.alive:
        print(f"  Dustun. {beyin.label} {tur} turda bitirdi.")
    else:
        print(f"  Ai-uchi - karsilikli olum. Ikiniz de {tur}. turda dustunuz.")
    print("=" * 64 + "\n")


def main() -> None:
    ap = argparse.ArgumentParser(description="Samuray duellosu (terminal)")
    ap.add_argument("--dusman", default="RONIN", choices=sorted(ARKETIPLER))
    ap.add_argument("--seed", type=int, default=None)
    args = ap.parse_args()
    oyna(args.dusman, args.seed if args.seed is not None else random.randrange(1 << 30))


if __name__ == "__main__":
    main()
