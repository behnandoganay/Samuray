#!/usr/bin/env python3
"""data/rules.json -> unity/Assets/Resources/rules.json esitleyicisi.

Ayar dosyasi TEK kaynak olmali. Unity Resources klasorunden okumak zorunda
oldugu icin bir kopya tutuyoruz; bu betik kopyanin kaymasini engelliyor.

  python3 tools/sync_rules.py           kopyayi guncelle
  python3 tools/sync_rules.py --check   farkliysa hata ver (CI icin)
"""
import filecmp, shutil, sys
from pathlib import Path

KOK = Path(__file__).resolve().parents[1]
KAYNAK = KOK / "data" / "rules.json"
HEDEF = KOK / "unity" / "Assets" / "Resources" / "rules.json"

def main() -> int:
    kontrol = "--check" in sys.argv
    if not KAYNAK.exists():
        print(f"kaynak yok: {KAYNAK}"); return 2
    ayni = HEDEF.exists() and filecmp.cmp(KAYNAK, HEDEF, shallow=False)
    if kontrol:
        if ayni:
            print("rules.json esit"); return 0
        print("FARKLI: unity kopyasi guncel degil -> python3 tools/sync_rules.py")
        return 1
    if ayni:
        print("zaten esit"); return 0
    HEDEF.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(KAYNAK, HEDEF)
    print(f"kopyalandi -> {HEDEF.relative_to(KOK)}")
    return 0

if __name__ == "__main__":
    raise SystemExit(main())
