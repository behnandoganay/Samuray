#!/usr/bin/env python3
"""Denge laboratuvari: bot-bot duellolari kosturup mekanigin saglik raporunu cikarir.

Bakilacaklar:
  - Hicbir durusun kullanim orani ezici olmamali (tek dogru durus varsa oyun sig)
  - WAKI kumari karli ama garanti olmamali
  - Ortalama duello 5-12 tur surmeli. Alt sinir bilerek dusuk: tasarim olumcul
    (3 yara oldurur, agir kesim 2 yara), yani kisa duello hatanin degil niyetin
    sonucudur. 12'nin ustu pat demektir - savunma saldiridan karli olmus.
  - Ilk oyuncu avantaji belirgin olmamali (simetrik eslesmede ~%50)

Sapmalar data/rules.json uzerinden ayarlanir, koda dokunulmaz.

Kullanim:  python3 balance.py --runs 2000
"""

from __future__ import annotations

import argparse
import itertools
import random
import sys
from collections import Counter
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from samuray.brains import ARKETIPLER, make_brain
from samuray.duel import run_duel
from samuray.rules import load_rules


def yuzde(pay: int, toplam: int) -> str:
    return f"{100.0 * pay / toplam:5.1f}%" if toplam else "  n/a"


def bar(oran: float, genislik: int = 24) -> str:
    dolu = int(round(oran * genislik))
    return "#" * dolu + "." * (genislik - dolu)


def kosu(runs: int, seed: int) -> None:
    rules = load_rules()
    anahtarlar = list(ARKETIPLER)

    kamae_sayaci: Counter[str] = Counter()
    eylem_sayaci: Counter[str] = Counter()
    hat_sayaci: Counter[str] = Counter()
    tur_toplami = 0
    zaman_asimi = 0
    karsilikli_olum = 0
    waki_isabet = 0
    suki_isabet = 0
    gard_kirma = 0
    toplam_isabet = 0

    print(f"\n{'='*66}\n  DENGE RAPORU  ({runs} duello / eslesme, tohum {seed})\n{'='*66}\n")
    print(f"  {'Eslesme':<24} {'A kazanir':>10} {'B kazanir':>10} {'ort. tur':>9}")
    print(f"  {'-'*24} {'-'*10} {'-'*10} {'-'*9}")

    for ka, kb in itertools.product(anahtarlar, repeat=2):
        rng = random.Random(seed + hash((ka, kb)) % 10000)
        a_galip = b_galip = 0
        turlar = 0
        for _ in range(runs):
            ba, bb = make_brain(ka, rules), make_brain(kb, rules)
            log = run_duel(ba, bb, rules, rng)
            turlar += log.beats
            tur_toplami += log.beats
            if log.timeout:
                zaman_asimi += 1
            elif log.winner is None:
                karsilikli_olum += 1
            elif log.winner.endswith(" A"):
                a_galip += 1
            else:
                b_galip += 1
            kamae_sayaci.update(log.kamae_counts)
            eylem_sayaci.update(log.action_counts)
            for h in log.hits:
                toplam_isabet += 1
                hat_sayaci[h.line.value] += 1
                waki_isabet += h.from_waki
                suki_isabet += h.vs_suki
                gard_kirma += h.guard_broken
        etiket = f"{ba.label} vs {bb.label}"
        print(f"  {etiket:<24} {yuzde(a_galip, runs):>10} {yuzde(b_galip, runs):>10}"
              f" {turlar/runs:>9.1f}")

    toplam_duello = runs * len(anahtarlar) ** 2

    print(f"\n  DURUS KULLANIMI (tek durus baskin olmamali)")
    kt = sum(kamae_sayaci.values())
    for k, n in kamae_sayaci.most_common():
        print(f"    {k:<8} {yuzde(n, kt)}  {bar(n / kt)}")

    print(f"\n  EYLEM DAGILIMI")
    et = sum(eylem_sayaci.values())
    for e, n in eylem_sayaci.most_common():
        print(f"    {e:<14} {yuzde(n, et)}  {bar(n / et)}")

    print(f"\n  KESIM HATLARI (bes hat da kullanilmali)")
    ht = sum(hat_sayaci.values())
    for h, n in hat_sayaci.most_common():
        print(f"    {h:<12} {yuzde(n, ht)}  {bar(n / ht)}")

    print(f"\n  OZET")
    print(f"    ortalama tur sayisi     {tur_toplami/toplam_duello:.2f}")
    print(f"    zaman asimi (pat)       {yuzde(zaman_asimi, toplam_duello)}")
    print(f"    karsilikli olum         {yuzde(karsilikli_olum, toplam_duello)}")
    print(f"    WAKI'den gelen isabet   {yuzde(waki_isabet, toplam_isabet)}")
    print(f"    acik (SUKI) cezalanmasi {yuzde(suki_isabet, toplam_isabet)}")
    print(f"    gard kirma              {yuzde(gard_kirma, toplam_isabet)}")
    print("      (gard kirma bir KONUM silahidir: rakibi zorla GEDAN'a iter. Tek")
    print("       hamle ileri bakan botlar konumu eksik degerlendirir, dolayisiyla")
    print("       buradaki oran gercek degerin alt siniridir.)")

    print(f"\n  SAGLIK KONTROLU")
    uyari = []
    ort = tur_toplami / toplam_duello
    if not 5 <= ort <= 12:
        uyari.append(f"ortalama tur {ort:.1f} - hedef 5-12 araligi disinda")
    if kt and max(kamae_sayaci.values()) / kt > 0.45:
        baskin = kamae_sayaci.most_common(1)[0][0]
        uyari.append(f"{baskin} durusu baskin ({yuzde(kamae_sayaci[baskin], kt).strip()})")
    if len(hat_sayaci) < 5:
        eksik = {l.value for l in rules.all_lines} - set(hat_sayaci)
        uyari.append(f"hic kullanilmayan hat: {', '.join(sorted(eksik))}")
    if zaman_asimi / toplam_duello > 0.05:
        uyari.append("pat orani yuksek - savunma saldiridan karli olabilir")
    if uyari:
        for u in uyari:
            print(f"    [!] {u}")
    else:
        print("    tum gostergeler hedef araliklarda")
    print()


def main() -> None:
    ap = argparse.ArgumentParser(description="Samuray denge laboratuvari")
    ap.add_argument("--runs", type=int, default=500, help="eslesme basina duello sayisi")
    ap.add_argument("--seed", type=int, default=7, help="rastgelelik tohumu")
    args = ap.parse_args()
    kosu(args.runs, args.seed)


if __name__ == "__main__":
    main()
