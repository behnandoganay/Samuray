"""Duello dongusu surucusu. Iki beyni (veya bir oyuncuyu) tur tur karsilastirir."""

from __future__ import annotations

import random
from dataclasses import dataclass, field

from .brains import Brain
from .model import Action, Fighter, Kamae
from .resolver import BeatResult, resolve_beat
from .rules import Rules

MAX_BEATS = 40  # sonsuz cekismeyi kesmek icin emniyet supabi


@dataclass
class DuelLog:
    winner: str | None
    beats: int
    events: list[str] = field(default_factory=list)
    hits: list = field(default_factory=list)
    kamae_counts: dict[str, int] = field(default_factory=dict)
    action_counts: dict[str, int] = field(default_factory=dict)
    timeout: bool = False


def run_duel(
    brain_a: Brain, brain_b: Brain, rules: Rules,
    rng: random.Random | None = None, verbose: bool = False,
) -> DuelLog:
    rng = rng or random.Random()
    a = brain_a.make_fighter(brain_a.label + " A", Kamae.CHUDAN)
    b = brain_b.make_fighter(brain_b.label + " B", Kamae.CHUDAN)

    log = DuelLog(winner=None, beats=0)

    for beat in range(1, MAX_BEATS + 1):
        log.beats = beat
        ia = brain_a.choose(a, b, rng)
        ib = brain_b.choose(b, a, rng)

        for f, intent in ((a, ia), (b, ib)):
            log.kamae_counts[f.kamae.value] = log.kamae_counts.get(f.kamae.value, 0) + 1
            for act in intent:
                etiket = act.type.value + ("/AGIR" if getattr(act, "heavy", False) else "")
                log.action_counts[etiket] = log.action_counts.get(etiket, 0) + 1

        res = resolve_beat(a, ia, b, ib, rules)
        a, b = res.a, res.b
        log.events.extend(f"[{beat}] {e}" for e in res.events)
        log.hits.extend(res.hits)
        if verbose:
            print(f"--- Tur {beat} ---")
            for e in res.events:
                print("   ", e)
            print("   ", a.summary())
            print("   ", b.summary())

        if not a.alive or not b.alive:
            if a.alive:
                log.winner = a.name
            elif b.alive:
                log.winner = b.name
            else:
                log.winner = None  # karsilikli olum - ai-uchi
            return log

    log.timeout = True
    return log
