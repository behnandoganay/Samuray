"""Tur cozumlemesi - motorun kalbi.

resolve_beat() saf bir fonksiyondur: rastgelelik yok, dosya yok, Unity yok.
Girdi iki savasci durumu ve iki niyet; cikti yeni durumlar ve olay listesi.
Bu saflik hem testi hem de C# portunu duz is haline getiriyor.

Cozumleme sirasi:
  0. Niyetleri yasallastir (yaralar, WAKI limiti, harcama tavani)
  1. Ki harca; acigi olan SUKI'ye duser
  2. Gard kumelerini belirle
  3. Feint'ler gardi kaydirir
  4. Parry'ler kesimleri iptal eder, saldirani sersemletir
  5. Ayni hatta karsilikli kesim = catisma
  6. Kalan kesimler cozulur (bloke / gard kirma / temiz isabet)
  7. Duruslar guncellenir
  8. Ki yenilenir, bayraklar bir sonraki tura tasinir
"""

from __future__ import annotations

from dataclasses import dataclass, field

from .model import Action, ActionType, Fighter, Injury, Intent, Kamae, Line
from .rules import Rules


@dataclass
class Hit:
    """Denge istatistikleri icin tek bir isabet kaydi."""

    attacker: str
    defender: str
    line: Line
    heavy: bool
    damage: int
    guard_broken: bool = False
    from_waki: bool = False
    vs_suki: bool = False


@dataclass
class BeatResult:
    a: Fighter
    b: Fighter
    events: list[str] = field(default_factory=list)
    hits: list[Hit] = field(default_factory=list)

    @property
    def finished(self) -> bool:
        return not self.a.alive or not self.b.alive


# ---------------------------------------------------------------------------
# Niyet yasallastirma
# ---------------------------------------------------------------------------

def legalize(f: Fighter, intent: Intent, rules: Rules, events: list[str]) -> Intent:
    """Yaralarin ve WAKI kuralinin izin vermedigi eylemleri ayikla, tavani uygula."""
    out: Intent = []
    for act in intent:
        if act.type is ActionType.CUT and act.heavy and Injury.ARM in f.injuries:
            events.append(f"{f.name}: kol yarasi agir kesime izin vermiyor, hafifledi")
            act = Action(ActionType.CUT, line=act.line, heavy=False)
        if act.type is ActionType.GUARD and act.to_kamae is not None:
            if Injury.LEG in f.injuries:
                events.append(f"{f.name}: bacak yarasi durus degistirmeye izin vermiyor")
                act = Action(ActionType.GUARD, to_kamae=None)
            elif act.to_kamae not in rules.adjacent(f.kamae) and act.to_kamae != f.kamae:
                events.append(
                    f"{f.name}: {f.kamae.value} -> {act.to_kamae.value} komsu degil, durus korundu"
                )
                act = Action(ActionType.GUARD, to_kamae=None)
        out.append(act)

    if f.kamae is Kamae.WAKI and len(out) > rules.waki_max_actions:
        events.append(f"{f.name}: WAKI'den turda tek eylem yapilir, fazlasi dustu")
        out = out[: rules.waki_max_actions]

    # Harcama tavanini asan sondaki eylemleri dusur
    while len(out) > 1 and intent_cost(f, out, rules) > rules.max_spend:
        dusen = out.pop()
        events.append(f"{f.name}: '{dusen}' nefes yetmedi, dustu")
    return out


def action_cost(f: Fighter, act: Action, rules: Rules) -> int:
    if act.type is ActionType.CUT:
        return rules.cut_cost(f.kamae, act.line, act.heavy)
    if act.type is ActionType.PARRY:
        return rules.cost("parry")
    if act.type is ActionType.FEINT:
        return rules.cost("feint")
    if act.type is ActionType.READ:
        return rules.cost("read")
    return rules.cost("guard")


def intent_cost(f: Fighter, intent: Intent, rules: Rules) -> int:
    return sum(action_cost(f, a, rules) for a in intent)


# ---------------------------------------------------------------------------
# Cozumleme
# ---------------------------------------------------------------------------

def _guard_set(f: Fighter, intent: Intent, savunmasiz: bool, rules: Rules) -> set[Line]:
    """Bu turda fiilen savunulan hatlar. SUKI veya gardi kirikken hicbiri."""
    if savunmasiz:
        return set()
    g = next((a for a in intent if a.type is ActionType.GUARD), None)
    kamae = g.to_kamae if (g is not None and g.to_kamae is not None) else f.kamae
    return rules.guards_of(kamae)


def _apply_feints(
    attacker: Fighter, intent: Intent, defender: Fighter,
    guards: set[Line], events: list[str],
) -> set[Line]:
    """Feint, dusmanin gardini yalan hatta kaydirir.

    Iki dogal bagisiklik var: Usta feint gormez, ve zaten korunan bir hatta
    feint atmak kimseyi kandirmaz (var olan garda yalan soyleyemezsin).
    """
    for act in intent:
        if act.type is not ActionType.FEINT:
            continue
        if defender.sees_through_feints:
            events.append(f"{defender.name}, {attacker.name} yalan soyluyor diye okudu ({act.line.value})")
            continue
        if act.line in guards:
            events.append(
                f"{attacker.name} {act.line.value} yalani ise yaramadi, "
                f"{defender.name} zaten o hatti koruyordu"
            )
            continue
        events.append(f"{attacker.name} {act.line.value} yalanina {defender.name} kandi")
        guards = {act.line}
    return guards


def resolve_beat(
    a: Fighter, intent_a: Intent, b: Fighter, intent_b: Intent, rules: Rules
) -> BeatResult:
    a, b = a.clone(), b.clone()
    events: list[str] = []
    hits: list[Hit] = []

    # Gecen turdan tasinan bayraklari al ve sifirla
    suki_in = {"a": a.suki, "b": b.suki}
    exposed_in = {"a": a.exposed, "b": b.exposed}
    riposte_in = {"a": a.riposte, "b": b.riposte}
    for f in (a, b):
        f.suki = f.exposed = f.riposte = f.staggered = False

    intent_a = legalize(a, intent_a, rules, events)
    intent_b = legalize(b, intent_b, rules, events)

    # 1. Ki harcamasi. Elindekinden fazlasini harcamak serbest - bedeli SUKI.
    new_suki = {"a": False, "b": False}
    new_exposed = {"a": False, "b": False}
    spend = {}
    for key, f, intent in (("a", a, intent_a), ("b", b, intent_b)):
        cost = intent_cost(f, intent, rules)
        spend[key] = cost
        f.ki -= cost
        if f.ki < 0:
            f.ki = 0
            new_suki[key] = True
            events.append(f"{f.name} nefesinin otesine gecti - acik verdi (SUKI)")

    # 2. Gard kumeleri
    guards = {
        "a": _guard_set(a, intent_a, suki_in["a"] or exposed_in["a"], rules),
        "b": _guard_set(b, intent_b, suki_in["b"] or exposed_in["b"], rules),
    }
    for key, f in (("a", a), ("b", b)):
        if suki_in[key]:
            events.append(f"{f.name} kendi acigini verdi - gard yok, gelen hasar iki kat")
        elif exposed_in[key]:
            events.append(f"{f.name} gardi kirik - bu tur gard alamaz")

    # 3. Feint'ler
    guards["b"] = _apply_feints(a, intent_a, b, guards["b"], events)
    guards["a"] = _apply_feints(b, intent_b, a, guards["a"], events)

    # Kesimleri topla. Riposte sahibinin ilk kesimi engellenemez.
    def collect(key: str, intent: Intent) -> list[tuple[Action, bool]]:
        cuts = [act for act in intent if act.type is ActionType.CUT]
        return [(act, riposte_in[key] and i == 0) for i, act in enumerate(cuts)]

    cuts = {"a": collect("a", intent_a), "b": collect("b", intent_b)}
    for key, f in (("a", a), ("b", b)):
        if riposte_in[key] and cuts[key]:
            events.append(f"{f.name} riposte penceresinde - ilk kesimi durdurulamaz")

    parries = {
        "a": {act.line for act in intent_a if act.type is ActionType.PARRY},
        "b": {act.line for act in intent_b if act.type is ActionType.PARRY},
    }

    # 4. Parry'ler
    negated: set[int] = set()
    for defkey, atkkey in (("a", "b"), ("b", "a")):
        defender = a if defkey == "a" else b
        attacker = a if atkkey == "a" else b
        for idx, (act, unstoppable) in enumerate(cuts[atkkey]):
            if unstoppable or act.line not in parries[defkey]:
                continue
            negated.add((atkkey, idx))
            attacker.staggered = True
            attacker.ki = 0
            defender.riposte = True
            events.append(
                f"{defender.name} {act.line.value} kesimini savurdu! "
                f"{attacker.name} sersemledi, nefesi tukendi"
            )
    for defkey, atkkey in (("a", "b"), ("b", "a")):
        defender = a if defkey == "a" else b
        yakalanabilir = {act.line for act, uns in cuts[atkkey] if not uns}
        for line in sorted(parries[defkey] - yakalanabilir, key=lambda x: x.value):
            events.append(f"{defender.name} bosuna {line.value} savurdu")

    live = {
        key: [(act, uns) for i, (act, uns) in enumerate(cuts[key]) if (key, i) not in negated]
        for key in ("a", "b")
    }

    # 5. Catisma: ayni hatta karsilikli kesim
    clash_lines = {act.line for act, _ in live["a"]} & {act.line for act, _ in live["b"]}
    clashed = False
    for line in clash_lines:
        clashed = True
        events.append(f"{line.value} hattinda kiliclar catisti - ikisi de savruldu")
    if clashed:
        live = {
            key: [(act, uns) for act, uns in live[key] if act.line not in clash_lines]
            for key in ("a", "b")
        }
        if spend["a"] < spend["b"]:
            new_suki["a"] = True
            events.append(f"{a.name} daha az yuklendi, dengesini kaybetti (SUKI)")
        elif spend["b"] < spend["a"]:
            new_suki["b"] = True
            events.append(f"{b.name} daha az yuklendi, dengesini kaybetti (SUKI)")

    # 6. Kalan kesimler
    forced_gedan = {"a": clashed, "b": clashed}
    for atkkey, defkey in (("a", "b"), ("b", "a")):
        attacker = a if atkkey == "a" else b
        defender = a if defkey == "a" else b
        for act, unstoppable in live[atkkey]:
            blocked = (not unstoppable) and act.line in guards[defkey]
            if blocked and not act.heavy:
                attacker.ki = max(0, attacker.ki - rules.dmg("blocked_ki_penalty"))
                events.append(f"{defender.name} {act.line.value} kesimini karsiladi")
                continue

            if blocked and act.heavy:
                dmg = rules.dmg("guard_break_chip")
                forced_gedan[defkey] = True
                guard_broken = True
                events.append(
                    f"{attacker.name} agir {act.line.value} ile {defender.name} gardini kirdi"
                )
                if rules.guard_break_opens:
                    # Az yara verir ama rakibin bir sonraki turunu satin alir:
                    # nefesi tukenir, acikta kalir. Bitirici degil, tempo silahi.
                    new_exposed[defkey] = True
                    defender.ki = max(0, defender.ki - rules.dmg("guard_break_ki_drain"))
                    events.append(
                        f"{defender.name} agir darbeyi karsiladi ama nefesi bosaldi "
                        f"- gelecek tur acikta"
                    )
            else:
                dmg = rules.dmg("heavy_cut") if act.heavy else rules.dmg("fast_cut")
                guard_broken = False
                if attacker.kamae is Kamae.WAKI:
                    dmg += rules.dmg("waki_bonus")
                    events.append(f"{attacker.name} gizli durustan cikti - kesim daha derin")

            if suki_in[defkey]:
                dmg *= rules.dmg("suki_multiplier")

            defender.wounds += dmg
            defender.injuries.add(rules.injury_for(act.line))
            hits.append(Hit(
                attacker=attacker.name, defender=defender.name, line=act.line,
                heavy=act.heavy, damage=dmg, guard_broken=guard_broken,
                from_waki=attacker.kamae is Kamae.WAKI, vs_suki=suki_in[defkey],
            ))
            events.append(
                f"*** {attacker.name} -> {defender.name}: {act.line.value} "
                f"({dmg} yara, {rules.injury_for(act.line).value})"
            )

    # 7. Duruslar. Kesim yaptiysan kilicin hattin bittigi yere duser.
    for key, f, intent in (("a", a, intent_a), ("b", b, intent_b)):
        son_kesim = next(
            (act for act in reversed(intent) if act.type is ActionType.CUT), None
        )
        if f.staggered or forced_gedan[key]:
            f.kamae = Kamae.GEDAN
        elif son_kesim is not None:
            f.kamae = rules.ends_at(son_kesim.line)
        else:
            g = next((act for act in intent if act.type is ActionType.GUARD), None)
            if g is not None and g.to_kamae is not None:
                f.kamae = g.to_kamae

    # 8. Ki yenilenmesi ve bayraklarin bir sonraki tura tasinmasi
    for key, f, intent in (("a", a, intent_a), ("b", b, intent_b)):
        regen = rules.ki_regen
        if any(act.type is ActionType.GUARD for act in intent):
            regen += rules.guard_bonus_regen
        if Injury.LUNG in f.injuries:
            regen -= 1
        f.ki = max(0, min(f.ki_max, f.ki + max(0, regen)))
        f.suki = new_suki[key]
        f.exposed = new_exposed[key] and not new_suki[key]

    return BeatResult(a=a, b=b, events=events, hits=hits)
