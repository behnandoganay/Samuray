"""data/rules.json yukleyicisi ve kural sorgulari.

Butun sayilar JSON'da durur; bu modul sadece onlara okunabilir bir arayuz verir.
Denge ayari yaparken koda degil rules.json'a dokunulur.
"""

from __future__ import annotations

import json
from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path

from .model import Injury, Kamae, Line

DEFAULT_RULES_PATH = Path(__file__).resolve().parents[2] / "data" / "rules.json"


@dataclass(frozen=True)
class Rules:
    raw: dict

    # --- durus sorgulari -------------------------------------------------

    def guards_of(self, kamae: Kamae) -> set[Line]:
        """Bu durusun dogal olarak savundugu hatlar."""
        return {Line(x) for x in self.raw["kamae"][kamae.value]["guards"]}

    def natural_lines(self, kamae: Kamae) -> set[Line]:
        """Bu durustan ucuza (1 Ki) yapilabilen kesimler."""
        return {Line(x) for x in self.raw["kamae"][kamae.value]["natural"]}

    def adjacent(self, kamae: Kamae) -> set[Kamae]:
        """Tek gard eylemiyle gecilebilecek duruslar."""
        return {Kamae(x) for x in self.raw["kamae"][kamae.value]["adjacent"]}

    def ends_at(self, line: Line) -> Kamae:
        """Bir kesimden sonra kilicin kalacagi durus. Sadece hatta baglidir."""
        return Kamae(self.raw["line_endings"][line.value])

    def cut_cost(self, kamae: Kamae, line: Line, heavy: bool = False) -> int:
        """Kesim maliyeti iki bagimsiz eksenin toplamidir.

        Konum ekseni : durustan dogal hat 1 Ki, zorlanan hat 2 Ki.
        Baglilik ekseni: uzun cizilen (agir) kesim +1 Ki, karsiliginda +1 yara.

        Dogal+hizli 1 | dogal+agir 2 | zorlanan+hizli 2 | zorlanan+agir 3
        """
        natural = self.raw["costs"]["cut_natural"]
        forced = self.raw["costs"]["cut_forced"]
        base = natural if line in self.natural_lines(kamae) else forced
        return base + (self.raw["costs"]["heavy_surcharge"] if heavy else 0)

    def injury_for(self, line: Line) -> Injury:
        return Injury(self.raw["injuries"][line.value])

    # --- sayilar ---------------------------------------------------------

    @property
    def ki_max(self) -> int:
        return self.raw["ki"]["max"]

    @property
    def ki_start(self) -> int:
        return self.raw["ki"]["start"]

    @property
    def ki_regen(self) -> int:
        return self.raw["ki"]["regen_per_beat"]

    @property
    def guard_bonus_regen(self) -> int:
        return self.raw["ki"]["guard_bonus_regen"]

    @property
    def max_spend(self) -> int:
        return self.raw["ki"]["max_spend_per_beat"]

    @property
    def wounds_to_die(self) -> int:
        return self.raw["damage"]["wounds_to_die"]

    @property
    def guard_break_opens(self) -> bool:
        """Agir kesimle kirilan gard, savunani gelecek tur acikta birakir mi?"""
        return bool(self.raw["damage"].get("guard_break_opens"))

    @property
    def waki_max_actions(self) -> int:
        return self.raw["waki"]["max_actions_per_beat"]

    def cost(self, key: str) -> int:
        return self.raw["costs"][key]

    def dmg(self, key: str) -> int:
        return self.raw["damage"][key]

    def gesture(self, key: str):
        return self.raw["gestures"][key]

    def brain_config(self, key: str) -> dict:
        return self.raw["brains"][key]

    @property
    def all_lines(self) -> list[Line]:
        return [Line(x) for x in self.raw["lines"]]

    @property
    def all_kamae(self) -> list[Kamae]:
        return [Kamae(x) for x in self.raw["kamae_list"]]


@lru_cache(maxsize=4)
def load_rules(path: str | Path = DEFAULT_RULES_PATH) -> Rules:
    with open(path, encoding="utf-8") as fh:
        return Rules(json.load(fh))
