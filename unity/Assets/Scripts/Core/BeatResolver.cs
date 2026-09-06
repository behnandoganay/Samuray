using System.Collections.Generic;
using System.Linq;

namespace Samuray.Core
{
    /// <summary>Tur cozumlemesi - motorun kalbi.
    ///
    /// Resolve() saf bir fonksiyondur: rastgelelik yok, dosya yok, UnityEngine yok.
    /// Girdi iki savasci durumu ve iki niyet; cikti yeni durumlar ve olay listesi.
    /// Bu saflik testi duz is haline getiriyor.
    ///
    /// Cozumleme sirasi:
    ///   0. Niyetleri yasallastir (yaralar, WAKI limiti, harcama tavani)
    ///   1. Ki harca; acigi olan SUKI'ye duser
    ///   2. Gard kumelerini belirle
    ///   3. Feint'ler gardi kaydirir
    ///   4. Parry'ler kesimleri iptal eder, saldirani sersemletir
    ///   5. Ayni hatta karsilikli kesim = catisma
    ///   6. Kalan kesimler cozulur (bloke / gard kirma / temiz isabet)
    ///   7. Duruslar guncellenir
    ///   8. Ki yenilenir, bayraklar bir sonraki tura tasinir
    /// </summary>
    public static class BeatResolver
    {
        // ---------------------------------------------------------------
        // Niyet yasallastirma
        // ---------------------------------------------------------------

        public static int ActionCost(Fighter f, Action act, Rules r)
        {
            switch (act.Type)
            {
                case ActionType.CUT:   return r.CutCost(f.Kamae, act.Line.Value, act.Heavy);
                case ActionType.PARRY: return r.Cost("parry");
                case ActionType.FEINT: return r.Cost("feint");
                case ActionType.READ:  return r.Cost("read");
                default:               return r.Cost("guard");
            }
        }

        public static int IntentCost(Fighter f, IEnumerable<Action> intent, Rules r)
        {
            int t = 0;
            foreach (var a in intent) t += ActionCost(f, a, r);
            return t;
        }

        /// <summary>Yaralarin ve WAKI kuralinin izin vermedigi eylemleri ayikla, tavani uygula.</summary>
        public static List<Action> Legalize(Fighter f, List<Action> intent, Rules r, List<string> events)
        {
            var outp = new List<Action>();
            foreach (var original in intent)
            {
                var act = original;
                if (act.Type == ActionType.CUT && act.Heavy && f.Injuries.Contains(Injury.ARM))
                {
                    events.Add($"{f.Name}: kol yarasi agir kesime izin vermiyor, hafifledi");
                    act = Action.Cut(act.Line.Value, false);
                }
                if (act.Type == ActionType.GUARD && act.ToKamae.HasValue)
                {
                    if (f.Injuries.Contains(Injury.LEG))
                    {
                        events.Add($"{f.Name}: bacak yarasi durus degistirmeye izin vermiyor");
                        act = Action.Guard(null);
                    }
                    else if (!r.Adjacent(f.Kamae).Contains(act.ToKamae.Value) && act.ToKamae.Value != f.Kamae)
                    {
                        events.Add($"{f.Name}: {f.Kamae} -> {act.ToKamae.Value} komsu degil, durus korundu");
                        act = Action.Guard(null);
                    }
                }
                outp.Add(act);
            }

            if (f.Kamae == Kamae.WAKI && outp.Count > r.WakiMaxActions)
            {
                events.Add($"{f.Name}: WAKI'den turda tek eylem yapilir, fazlasi dustu");
                outp = outp.GetRange(0, r.WakiMaxActions);
            }

            while (outp.Count > 1 && IntentCost(f, outp, r) > r.MaxSpend)
            {
                var dusen = outp[outp.Count - 1];
                outp.RemoveAt(outp.Count - 1);
                events.Add($"{f.Name}: '{dusen}' nefes yetmedi, dustu");
            }
            return outp;
        }

        // ---------------------------------------------------------------
        // Cozumleme
        // ---------------------------------------------------------------

        /// <summary>Bu turda fiilen savunulan hatlar. SUKI veya gardi kirikken hicbiri.</summary>
        static HashSet<CutLine> GuardSet(Fighter f, List<Action> intent, bool savunmasiz, Rules r)
        {
            if (savunmasiz) return new HashSet<CutLine>();
            Kamae k = f.Kamae;
            foreach (var a in intent)
                if (a.Type == ActionType.GUARD) { if (a.ToKamae.HasValue) k = a.ToKamae.Value; break; }
            return new HashSet<CutLine>(r.GuardsOf(k));
        }

        /// <summary>Feint, dusmanin gardini yalan hatta kaydirir.
        ///
        /// Iki dogal bagisiklik var: Usta feint gormez, ve zaten korunan bir hatta
        /// feint atmak kimseyi kandirmaz (var olan bir garda yalan soyleyemezsin).
        /// </summary>
        static HashSet<CutLine> ApplyFeints(Fighter attacker, List<Action> intent, Fighter defender,
                                            HashSet<CutLine> guards, List<string> events)
        {
            foreach (var act in intent)
            {
                if (act.Type != ActionType.FEINT) continue;
                if (defender.SeesThroughFeints)
                {
                    events.Add($"{defender.Name}, {attacker.Name} yalan soyluyor diye okudu ({act.Line.Value})");
                    continue;
                }
                if (guards.Contains(act.Line.Value))
                {
                    events.Add($"{attacker.Name} {act.Line.Value} yalani ise yaramadi, " +
                               $"{defender.Name} zaten o hatti koruyordu");
                    continue;
                }
                events.Add($"{attacker.Name} {act.Line.Value} yalanina {defender.Name} kandi");
                guards = new HashSet<CutLine> { act.Line.Value };
            }
            return guards;
        }

        public static BeatResult Resolve(Fighter aIn, List<Action> intentA,
                                         Fighter bIn, List<Action> intentB, Rules r)
        {
            var f = new[] { aIn.Clone(), bIn.Clone() };
            var events = new List<string>();
            var hits = new List<Hit>();

            // Gecen turdan tasinan bayraklari al ve sifirla
            var sukiIn = new[] { f[0].Suki, f[1].Suki };
            var expIn = new[] { f[0].Exposed, f[1].Exposed };
            var ripIn = new[] { f[0].Riposte, f[1].Riposte };
            foreach (var x in f) { x.Suki = x.Exposed = x.Riposte = x.Staggered = false; }

            var intents = new[] { Legalize(f[0], intentA, r, events), Legalize(f[1], intentB, r, events) };

            // 1. Ki harcamasi. Elindekinden fazlasini harcamak serbest - bedeli SUKI.
            var newSuki = new[] { false, false };
            var newExposed = new[] { false, false };
            var spend = new int[2];
            for (int i = 0; i < 2; i++)
            {
                int cost = IntentCost(f[i], intents[i], r);
                spend[i] = cost;
                f[i].Ki -= cost;
                if (f[i].Ki < 0)
                {
                    f[i].Ki = 0; newSuki[i] = true;
                    events.Add($"{f[i].Name} nefesinin otesine gecti - acik verdi (SUKI)");
                }
            }

            // 2. Gard kumeleri
            var guards = new HashSet<CutLine>[2];
            for (int i = 0; i < 2; i++)
                guards[i] = GuardSet(f[i], intents[i], sukiIn[i] || expIn[i], r);
            for (int i = 0; i < 2; i++)
            {
                if (sukiIn[i]) events.Add($"{f[i].Name} kendi acigini verdi - gard yok, gelen hasar iki kat");
                else if (expIn[i]) events.Add($"{f[i].Name} gardi kirik - bu tur gard alamaz");
            }

            // 3. Feint'ler
            guards[1] = ApplyFeints(f[0], intents[0], f[1], guards[1], events);
            guards[0] = ApplyFeints(f[1], intents[1], f[0], guards[0], events);

            // Kesimleri topla. Riposte sahibinin ilk kesimi engellenemez.
            var cuts = new List<(Action act, bool uns)>[2];
            for (int i = 0; i < 2; i++)
            {
                cuts[i] = new List<(Action, bool)>();
                int n = 0;
                foreach (var a in intents[i])
                    if (a.Type == ActionType.CUT) { cuts[i].Add((a, ripIn[i] && n == 0)); n++; }
                if (ripIn[i] && cuts[i].Count > 0)
                    events.Add($"{f[i].Name} riposte penceresinde - ilk kesimi durdurulamaz");
            }

            var parries = new HashSet<CutLine>[2];
            for (int i = 0; i < 2; i++)
            {
                parries[i] = new HashSet<CutLine>();
                foreach (var a in intents[i])
                    if (a.Type == ActionType.PARRY) parries[i].Add(a.Line.Value);
            }

            // 4. Parry'ler
            var negated = new HashSet<string>();
            for (int d = 0; d < 2; d++)
            {
                int atk = 1 - d;
                for (int i = 0; i < cuts[atk].Count; i++)
                {
                    var c = cuts[atk][i];
                    if (c.uns || !parries[d].Contains(c.act.Line.Value)) continue;
                    negated.Add(atk + ":" + i);
                    f[atk].Staggered = true;
                    f[atk].Ki = 0;
                    f[d].Riposte = true;
                    events.Add($"{f[d].Name} {c.act.Line.Value} kesimini savurdu! " +
                               $"{f[atk].Name} sersemledi, nefesi tukendi");
                }
            }
            for (int d = 0; d < 2; d++)
            {
                int atk = 1 - d;
                var yakalanabilir = new HashSet<CutLine>(
                    cuts[atk].Where(c => !c.uns).Select(c => c.act.Line.Value));
                foreach (var line in parries[d].Where(l => !yakalanabilir.Contains(l))
                                               .OrderBy(l => l.ToString(), System.StringComparer.Ordinal))
                    events.Add($"{f[d].Name} bosuna {line} savurdu");
            }

            var live = new List<(Action act, bool uns)>[2];
            for (int i = 0; i < 2; i++)
            {
                live[i] = new List<(Action, bool)>();
                for (int j = 0; j < cuts[i].Count; j++)
                    if (!negated.Contains(i + ":" + j)) live[i].Add(cuts[i][j]);
            }

            // 5. Catisma: ayni hatta karsilikli kesim
            var la = new HashSet<CutLine>(live[0].Select(c => c.act.Line.Value));
            var clashLines = new HashSet<CutLine>(
                live[1].Select(c => c.act.Line.Value).Where(l => la.Contains(l)));
            bool clashed = clashLines.Count > 0;
            foreach (var line in clashLines.OrderBy(l => l.ToString(), System.StringComparer.Ordinal))
                events.Add($"{line} hattinda kiliclar catisti - ikisi de savruldu");
            if (clashed)
            {
                for (int i = 0; i < 2; i++)
                    live[i] = live[i].Where(c => !clashLines.Contains(c.act.Line.Value)).ToList();
                if (spend[0] < spend[1])
                {
                    newSuki[0] = true;
                    events.Add($"{f[0].Name} daha az yuklendi, dengesini kaybetti (SUKI)");
                }
                else if (spend[1] < spend[0])
                {
                    newSuki[1] = true;
                    events.Add($"{f[1].Name} daha az yuklendi, dengesini kaybetti (SUKI)");
                }
            }

            // 6. Kalan kesimler
            var forcedGedan = new[] { clashed, clashed };
            for (int atk = 0; atk < 2; atk++)
            {
                int d = 1 - atk;
                var attacker = f[atk];
                var defender = f[d];
                foreach (var (act, uns) in live[atk])
                {
                    var line = act.Line.Value;
                    bool blocked = !uns && guards[d].Contains(line);

                    if (blocked && !act.Heavy)
                    {
                        attacker.Ki = System.Math.Max(0, attacker.Ki - r.Dmg("blocked_ki_penalty"));
                        events.Add($"{defender.Name} {line} kesimini karsiladi");
                        continue;
                    }

                    int dmg;
                    bool guardBroken = false;
                    if (blocked && act.Heavy)
                    {
                        dmg = r.Dmg("guard_break_chip");
                        forcedGedan[d] = true;
                        guardBroken = true;
                        events.Add($"{attacker.Name} agir {line} ile {defender.Name} gardini kirdi");
                        if (r.GuardBreakOpens)
                        {
                            // Az yara verir ama rakibin bir sonraki turunu satin alir:
                            // nefesi tukenir, acikta kalir. Bitirici degil, tempo silahi.
                            newExposed[d] = true;
                            defender.Ki = System.Math.Max(0, defender.Ki - r.Dmg("guard_break_ki_drain"));
                            events.Add($"{defender.Name} agir darbeyi karsiladi ama nefesi bosaldi " +
                                       $"- gelecek tur acikta");
                        }
                    }
                    else
                    {
                        dmg = act.Heavy ? r.Dmg("heavy_cut") : r.Dmg("fast_cut");
                        if (attacker.Kamae == Kamae.WAKI)
                        {
                            dmg += r.Dmg("waki_bonus");
                            events.Add($"{attacker.Name} gizli durustan cikti - kesim daha derin");
                        }
                    }

                    if (sukiIn[d]) dmg *= r.Dmg("suki_multiplier");

                    defender.Wounds += dmg;
                    var inj = r.InjuryFor(line);
                    defender.Injuries.Add(inj);
                    hits.Add(new Hit
                    {
                        Attacker = attacker.Name, Defender = defender.Name, Line = line,
                        Heavy = act.Heavy, Damage = dmg, GuardBroken = guardBroken,
                        FromWaki = attacker.Kamae == Kamae.WAKI, VsSuki = sukiIn[d]
                    });
                    events.Add($"*** {attacker.Name} -> {defender.Name}: {line} ({dmg} yara, {inj})");
                }
            }

            // 7. Duruslar. Kesim yaptiysan kilicin hattin bittigi yere duser.
            for (int i = 0; i < 2; i++)
            {
                Action? sonKesim = null;
                for (int j = intents[i].Count - 1; j >= 0; j--)
                    if (intents[i][j].Type == ActionType.CUT) { sonKesim = intents[i][j]; break; }

                if (f[i].Staggered || forcedGedan[i]) f[i].Kamae = Kamae.GEDAN;
                else if (sonKesim.HasValue) f[i].Kamae = r.EndsAt(sonKesim.Value.Line.Value);
                else
                {
                    foreach (var a in intents[i])
                        if (a.Type == ActionType.GUARD)
                        {
                            if (a.ToKamae.HasValue) f[i].Kamae = a.ToKamae.Value;
                            break;
                        }
                }
            }

            // 8. Ki yenilenmesi ve bayraklarin bir sonraki tura tasinmasi
            for (int i = 0; i < 2; i++)
            {
                int regen = r.KiRegen;
                foreach (var a in intents[i]) if (a.Type == ActionType.GUARD) { regen += r.GuardBonusRegen; break; }
                if (f[i].Injuries.Contains(Injury.LUNG)) regen -= 1;
                f[i].Ki = System.Math.Max(0, System.Math.Min(f[i].KiMax, f[i].Ki + System.Math.Max(0, regen)));
                f[i].Suki = newSuki[i];
                f[i].Exposed = newExposed[i] && !newSuki[i];
            }

            return new BeatResult { A = f[0], B = f[1], Events = events, Hits = hits };
        }
    }
}
