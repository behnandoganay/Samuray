using System;
using System.Collections.Generic;
using System.Linq;

namespace Samuray.Core
{
    /// <summary>Dusman arketipleri.
    ///
    /// Her arketip oyuncuyu farkli bir aliskanligindan vazgecmeye zorlar. Beyinler
    /// karar verirken rastgelelik kullanabilir, ama BeatResolver saf kalir -
    /// rastgelelik hep burada, cozumlemede degil.
    ///
    /// Telegraph() oyuncunun planlama fazinda GORDUGU niyeti dondurur. Durust
    /// arketiplerde bu gercek niyettir; blofcude yalan olabilir.
    /// </summary>
    public abstract class Brain
    {
        protected readonly Rules R;
        public string Key { get; }
        public string Label { get; }
        public int KiMax { get; }
        public bool Lies { get; }
        public bool SeesThroughFeints { get; }
        public double Aggression { get; }

        protected Brain(string key, Rules rules)
        {
            Key = key; R = rules;
            var cfg = rules.GetBrainConfig(key);
            Label = cfg.Label; KiMax = cfg.KiMax; Lies = cfg.Lies;
            SeesThroughFeints = cfg.SeesThroughFeints; Aggression = cfg.Aggression;
        }

        // --- yardimcilar ------------------------------------------------------

        protected static T Pick<T>(IList<T> items, Random rng) => items[rng.Next(items.Count)];

        protected List<CutLine> SortedLines(IEnumerable<CutLine> src)
            => src.OrderBy(l => l.ToString(), StringComparer.Ordinal).ToList();

        /// <summary>Dusmanin su an savunmadigi hatlar.</summary>
        protected List<CutLine> OpenLines(Fighter foe)
        {
            var g = R.GuardsOf(foe.Kamae);
            return R.AllLines.Where(l => !g.Contains(l)).ToList();
        }

        /// <summary>Butcenin yettigi butun kesimler.</summary>
        protected List<Action> AffordableCuts(Fighter me, int budget)
        {
            var outp = new List<Action>();
            foreach (var l in R.AllLines)
                foreach (var heavy in new[] { false, true })
                {
                    if (heavy && me.Injuries.Contains(Injury.ARM)) continue;
                    if (R.CutCost(me.Kamae, l, heavy) <= budget) outp.Add(Action.Cut(l, heavy));
                }
            return outp;
        }

        /// <summary>Butcedeki en iyi kesim.
        ///
        /// Skor skalerdir cunku maliyet gercekten tartilmali. Sirali bir demet
        /// kullanmak ham hasari maliyetin onune koyuyordu ve gard kirma hicbir
        /// zaman secilemiyordu: acik hatta agir kesim (2 yara) her zaman gardli
        /// hatta agir kesimi (1 yara) yener. Oysa gard kirma tam olarak acik
        /// hatlarin ZORLANAN, gardli hattin DOGAL oldugu durumda karlidir.
        /// </summary>
        protected Action? BestCut(Fighter me, Fighter foe, int budget)
        {
            var open = OpenLines(foe);
            var cands = AffordableCuts(me, budget);
            if (cands.Count == 0) return null;

            (double score, int wound, bool setup) Evaluate(Action c)
            {
                var line = c.Line.Value;
                bool hedefAcik = open.Contains(line);
                int yara = hedefAcik
                    ? (c.Heavy ? R.Dmg("heavy_cut") : R.Dmg("fast_cut"))
                    : (c.Heavy ? R.Dmg("guard_break_chip") : 0);
                bool kurulum = c.Heavy && !hedefAcik && R.GuardBreakOpens;
                int maliyet = R.CutCost(me.Kamae, line, c.Heavy);

                double skor = yara;
                if (foe.Wounds + yara >= R.WoundsToDie) skor += 100.0;  // bitirici her seyin onunde
                if (kurulum)
                {
                    // Gard kirmanin asil degeri hasar degil KONUM: rakibi zorla
                    // GEDAN'a iter, yani gelecek tur nerede duracagini sen secersin.
                    double ek = R.Dmg("fast_cut");
                    if (R.Dmg("guard_break_ki_drain") >= foe.Ki) ek += R.Dmg("fast_cut");
                    int simdi = R.NaturalLines(me.Kamae).Count(l => R.GuardsOf(foe.Kamae).Contains(l));
                    int sonra = R.NaturalLines(me.Kamae).Count(l => R.GuardsOf(Kamae.GEDAN).Contains(l));
                    ek += Math.Max(0, simdi - sonra);
                    skor += ek * 0.6;
                }
                return (skor - 0.5 * maliyet, yara, kurulum);
            }

            var best = cands[0];
            var bs = Evaluate(best);
            foreach (var c in cands)
            {
                var e = Evaluate(c);
                if (e.score > bs.score) { best = c; bs = e; }
            }
            return (bs.wound > 0 || bs.setup) ? best : (Action?)null;
        }

        /// <summary>Gizli durusa girmeye deger mi?
        ///
        /// WAKI bir tur tamamen savunmasiz kalmak demek, ama sonrasinda her kesim
        /// ucuz, +1 hasarli ve okunamaz. Yani ancak geride kalindiginda ve rakip
        /// o turu cezalandiracak nefese sahip degilken mantikli bir kumar.
        /// </summary>
        protected bool WakiGambit(Fighter me, Fighter foe)
        {
            if (me.Kamae == Kamae.WAKI || !R.Adjacent(me.Kamae).Contains(Kamae.WAKI)) return false;
            if (me.Injuries.Contains(Injury.LEG)) return false;
            return me.Wounds > foe.Wounds && foe.Ki <= 1;
        }

        public Fighter MakeFighter(string name, Kamae kamae = Kamae.CHUDAN) => new Fighter(name)
        {
            Kamae = kamae,
            Ki = Math.Min(R.KiStart, KiMax),
            KiMax = KiMax,
            SeesThroughFeints = SeesThroughFeints
        };

        // --- arayuz -----------------------------------------------------------

        public abstract List<Action> Choose(Fighter me, Fighter foe, Random rng);

        /// <summary>Oyuncuya gosterilen niyet. Yalan soylemeyen beyinlerde gercegin aynisi.
        /// Gizli durustan niyet okunmaz (null doner).</summary>
        public virtual List<Action> Telegraph(List<Action> intent, Fighter me, Random rng)
            => me.Kamae == Kamae.WAKI ? null : intent;

        public static Brain Make(string key, Rules rules)
        {
            switch (key.ToUpperInvariant())
            {
                case "RONIN":   return new Ronin(rules);
                case "OGRENCI": return new Ogrenci(rules);
                case "BLOFCU":  return new Blofcu(rules);
                case "USTA":    return new Usta(rules);
                default: throw new ArgumentException("bilinmeyen arketip: " + key);
            }
        }
    }

    /// <summary>Durust telegraf, dar butce, basit akis. Okuma ve akisi ogretir.</summary>
    public sealed class Ronin : Brain
    {
        public Ronin(Rules r) : base("RONIN", r) { }

        public override List<Action> Choose(Fighter me, Fighter foe, Random rng)
        {
            var open = OpenLines(foe);
            var opts = AffordableCuts(me, me.Ki).Where(c => open.Contains(c.Line.Value)).ToList();
            if (opts.Count == 0 || rng.NextDouble() > Aggression)
            {
                if (me.Ki <= 1)
                {
                    var komsu = R.Adjacent(me.Kamae).OrderBy(k => k.ToString(), StringComparer.Ordinal).ToList();
                    return new List<Action> { Action.Guard(Pick(komsu, rng)) };
                }
                return new List<Action> { Action.Guard() };
            }
            // En ucuz acik kesimi tercih eder - nefesini idareli kullanir
            opts = opts.OrderBy(c => R.CutCost(me.Kamae, c.Line.Value, c.Heavy)).ToList();
            return new List<Action> { opts[0] };
        }
    }

    /// <summary>Surekli CHUDAN'a doner, asiriya kacmayi cezalandirir. Ki disiplini ogretir.</summary>
    public sealed class Ogrenci : Brain
    {
        public Ogrenci(Rules r) : base("OGRENCI", r) { }

        public override List<Action> Choose(Fighter me, Fighter foe, Random rng)
        {
            if (foe.Suki)
            {
                var v = BestCut(me, foe, me.Ki);
                if (v.HasValue) return new List<Action> { v.Value };
            }
            if (WakiGambit(me, foe)) return new List<Action> { Action.Guard(Kamae.WAKI) };

            // Rakip nefes topluyorsa savurma bosa gider; bunun yerine pozisyon al
            if (foe.Ki <= 1)
            {
                if (me.Kamae != Kamae.CHUDAN && R.Adjacent(me.Kamae).Contains(Kamae.CHUDAN))
                    return new List<Action> { Action.Guard(Kamae.CHUDAN) };
                return new List<Action> { Action.Guard() };
            }

            if (me.Ki >= 2 && rng.NextDouble() < Aggression)
            {
                var v = BestCut(me, foe, me.Ki - R.Cost("parry"));
                if (v.HasValue)
                {
                    // Bir Ki'yi savurmaya ayirir - hem vurur hem korunur
                    var tahmin = Pick(SortedLines(R.NaturalLines(foe.Kamae)), rng);
                    return new List<Action> { v.Value, Action.Parry(tahmin) };
                }
            }
            if (me.Ki >= 1)
                return new List<Action> { Action.Parry(Pick(SortedLines(R.NaturalLines(foe.Kamae)), rng)) };
            return new List<Action> { Action.Guard() };
        }
    }

    /// <summary>Telegrafinin yarisi yalan. Oyuncuyu okuma eylemine zorlar.</summary>
    public sealed class Blofcu : Brain
    {
        public Blofcu(Rules r) : base("BLOFCU", r) { }

        public override List<Action> Choose(Fighter me, Fighter foe, Random rng)
        {
            // Feint'in dogru kullanimi: ACIK bir hatta yalan soyle ki gard oraya
            // kaysin, sonra bosalan KORUNAN hattan gercek kesimi indir. Tersi
            // (korunan hatta yalan) hicbir zaman ise yaramaz.
            var open = OpenLines(foe);
            var korunan = R.GuardsOf(foe.Kamae);

            if (korunan.Count > 0 && rng.NextDouble() < Aggression)
            {
                int butce = me.Ki - R.Cost("feint");
                var hedefler = AffordableCuts(me, butce).Where(c => korunan.Contains(c.Line.Value)).ToList();
                var yalanlar = SortedLines(open);
                if (hedefler.Count > 0 && yalanlar.Count > 0)
                {
                    var gercek = hedefler.OrderBy(c => R.CutCost(me.Kamae, c.Line.Value, c.Heavy)).First();
                    return new List<Action> { Action.Feint(Pick(yalanlar, rng)), gercek };
                }
            }

            var v = BestCut(me, foe, me.Ki);
            if (v.HasValue && rng.NextDouble() < Aggression) return new List<Action> { v.Value };
            if (WakiGambit(me, foe)) return new List<Action> { Action.Guard(Kamae.WAKI) };
            if (me.Ki <= 1) return new List<Action> { Action.Guard() };
            return new List<Action> { Action.Parry(Pick(SortedLines(R.NaturalLines(foe.Kamae)), rng)) };
        }

        public override List<Action> Telegraph(List<Action> intent, Fighter me, Random rng)
        {
            if (me.Kamae == Kamae.WAKI) return null;
            if (!Lies || intent.Count == 0 || rng.NextDouble() > 0.5) return intent;
            // Gercek kesimi baska bir hatta gosterir
            return intent.Select(a => a.Type == ActionType.CUT
                ? Action.Cut(Pick(R.AllLines.Where(l => l != a.Line.Value).ToList(), rng), a.Heavy)
                : a).ToList();
        }
    }

    /// <summary>Boss. Feint gormez ve oyuncunun akis grafigini okur: bir durus
    /// dizisini tekrarlarsan o durustan gelen dogal kesimleri onceden savurur.
    /// Oyuncunun ezberini ona karsi silaha cevirir.</summary>
    public sealed class Usta : Brain
    {
        readonly List<Kamae> _gecmis = new List<Kamae>();
        public Usta(Rules r) : base("USTA", r) { }

        public override List<Action> Choose(Fighter me, Fighter foe, Random rng)
        {
            _gecmis.Add(foe.Kamae);
            bool tekrar = _gecmis.Count(k => k == foe.Kamae) >= 3;

            if (foe.Suki || foe.Staggered)
            {
                var v0 = BestCut(me, foe, me.Ki);
                if (v0.HasValue) return new List<Action> { v0.Value };
            }
            if (WakiGambit(me, foe)) return new List<Action> { Action.Guard(Kamae.WAKI) };

            // Oyuncu ayni durusa saplanmissa oradan gelecek dogal kesimi savurur
            if (tekrar && me.Ki >= R.Cost("parry") + 1)
            {
                var dogal = SortedLines(R.NaturalLines(foe.Kamae));
                var karsilik = BestCut(me, foe, me.Ki - R.Cost("parry"));
                if (dogal.Count > 0 && karsilik.HasValue)
                    return new List<Action> { Action.Parry(dogal[0]), karsilik.Value };
            }

            var v = BestCut(me, foe, me.Ki);
            if (v.HasValue && rng.NextDouble() < Aggression) return new List<Action> { v.Value };
            if (me.Ki <= 1) return new List<Action> { Action.Guard() };
            return new List<Action> { Action.Parry(Pick(SortedLines(R.NaturalLines(foe.Kamae)), rng)) };
        }

        public override List<Action> Telegraph(List<Action> intent, Fighter me, Random rng)
        {
            if (me.Kamae == Kamae.WAKI) return null;
            if (rng.NextDouble() > 0.5) return intent;
            return intent.Select(a => a.Type == ActionType.CUT
                ? Action.Cut(Pick(R.AllLines.ToList(), rng), a.Heavy) : a).ToList();
        }
    }
}
