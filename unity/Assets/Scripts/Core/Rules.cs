using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Samuray.Core
{
    /// <summary>rules.json'un okunabilir arayuzu.
    ///
    /// Butun sayilar JSON'da durur; bu sinif sadece onlara erisim verir. Denge
    /// ayari yaparken koda degil rules.json'a dokunulur.
    ///
    /// Cekirdek UnityEngine'e hic dokunmadigi icin (asmdef'te noEngineReferences)
    /// dosyayi kendisi OKUYAMAZ - JSON metnini disaridan alir. Yukleme isi
    /// sunum katmaninin: Resources.Load&lt;TextAsset&gt;("rules").text
    /// </summary>
    public sealed class Rules
    {
        public sealed class KamaeInfo
        {
            public readonly HashSet<CutLine> Guards = new HashSet<CutLine>();
            public readonly HashSet<CutLine> Natural = new HashSet<CutLine>();
            public readonly HashSet<Kamae> Adjacent = new HashSet<Kamae>();
        }

        public sealed class BrainConfig
        {
            public string Label;
            public int KiMax;
            public bool Lies;
            public bool SeesThroughFeints;
            public double Aggression;
        }

        readonly Dictionary<Kamae, KamaeInfo> _kamae = new Dictionary<Kamae, KamaeInfo>();
        readonly Dictionary<CutLine, Kamae> _endings = new Dictionary<CutLine, Kamae>();
        readonly Dictionary<CutLine, Injury> _injuries = new Dictionary<CutLine, Injury>();
        readonly Dictionary<string, int> _ki = new Dictionary<string, int>();
        readonly Dictionary<string, int> _costs = new Dictionary<string, int>();
        readonly Dictionary<string, int> _damage = new Dictionary<string, int>();
        readonly Dictionary<string, double> _gestures = new Dictionary<string, double>();
        readonly Dictionary<string, BrainConfig> _brains = new Dictionary<string, BrainConfig>();

        public IReadOnlyList<CutLine> AllLines { get; private set; }
        public IReadOnlyList<Kamae> AllKamae { get; private set; }
        public bool GuardBreakOpens { get; private set; }
        public int WakiMaxActions { get; private set; }

        Rules() { }

        /// <summary>rules.json metnini ayristirir.</summary>
        public static Rules FromJson(string json)
        {
            if (string.IsNullOrEmpty(json))
                throw new ArgumentException("rules.json bos geldi", nameof(json));

            var root = JObject.Parse(json);
            var r = new Rules();

            var lines = new List<CutLine>();
            foreach (var t in (JArray)root["lines"]) lines.Add(ParseLine((string)t));
            r.AllLines = lines;

            var kamaeList = new List<Kamae>();
            foreach (var t in (JArray)root["kamae_list"]) kamaeList.Add(ParseKamae((string)t));
            r.AllKamae = kamaeList;

            foreach (var kv in (JObject)root["line_endings"])
            {
                if (IsComment(kv.Key)) continue;
                r._endings[ParseLine(kv.Key)] = ParseKamae((string)kv.Value);
            }

            foreach (var kv in (JObject)root["kamae"])
            {
                if (IsComment(kv.Key)) continue;
                var info = new KamaeInfo();
                var o = (JObject)kv.Value;
                foreach (var t in (JArray)o["guards"]) info.Guards.Add(ParseLine((string)t));
                foreach (var t in (JArray)o["natural"]) info.Natural.Add(ParseLine((string)t));
                foreach (var t in (JArray)o["adjacent"]) info.Adjacent.Add(ParseKamae((string)t));
                r._kamae[ParseKamae(kv.Key)] = info;
            }

            ReadInts((JObject)root["ki"], r._ki);
            ReadInts((JObject)root["costs"], r._costs);
            ReadInts((JObject)root["damage"], r._damage);
            ReadDoubles((JObject)root["gestures"], r._gestures);

            var dmg = (JObject)root["damage"];
            r.GuardBreakOpens = dmg["guard_break_opens"] != null && (bool)dmg["guard_break_opens"];

            foreach (var kv in (JObject)root["injuries"])
            {
                if (IsComment(kv.Key) || kv.Key == "effects") continue;
                r._injuries[ParseLine(kv.Key)] = (Injury)Enum.Parse(typeof(Injury), (string)kv.Value);
            }

            r.WakiMaxActions = (int)root["waki"]["max_actions_per_beat"];

            foreach (var kv in (JObject)root["brains"])
            {
                if (IsComment(kv.Key)) continue;
                var o = (JObject)kv.Value;
                r._brains[kv.Key] = new BrainConfig
                {
                    Label = (string)o["label"],
                    KiMax = (int)o["ki_max"],
                    Lies = (bool)o["lies"],
                    SeesThroughFeints = (bool)o["sees_through_feints"],
                    Aggression = (double)o["aggression"]
                };
            }
            return r;
        }

        static bool IsComment(string key) => key.StartsWith("_");
        static CutLine ParseLine(string s) => (CutLine)Enum.Parse(typeof(CutLine), s);
        static Kamae ParseKamae(string s) => (Kamae)Enum.Parse(typeof(Kamae), s);

        static void ReadInts(JObject o, Dictionary<string, int> into)
        {
            foreach (var kv in o)
            {
                if (IsComment(kv.Key)) continue;
                if (kv.Value.Type == JTokenType.Integer) into[kv.Key] = (int)kv.Value;
            }
        }
        static void ReadDoubles(JObject o, Dictionary<string, double> into)
        {
            foreach (var kv in o)
            {
                if (IsComment(kv.Key)) continue;
                if (kv.Value.Type == JTokenType.Integer || kv.Value.Type == JTokenType.Float)
                    into[kv.Key] = (double)kv.Value;
            }
        }

        // --- durus sorgulari -------------------------------------------------

        public HashSet<CutLine> GuardsOf(Kamae k) => _kamae[k].Guards;
        public HashSet<CutLine> NaturalLines(Kamae k) => _kamae[k].Natural;
        public HashSet<Kamae> Adjacent(Kamae k) => _kamae[k].Adjacent;

        /// <summary>Bir kesimden sonra kilicin kalacagi durus. SADECE hatta baglidir.</summary>
        public Kamae EndsAt(CutLine line) => _endings[line];

        /// <summary>Kesim maliyeti iki bagimsiz eksenin toplamidir.
        ///
        /// Konum ekseni : durustan dogal hat 1 Ki, zorlanan hat 2 Ki.
        /// Baglilik ekseni: uzun cizilen (agir) kesim +1 Ki, karsiliginda +1 yara.
        ///
        /// Dogal+hizli 1 | dogal+agir 2 | zorlanan+hizli 2 | zorlanan+agir 3
        /// </summary>
        public int CutCost(Kamae kamae, CutLine line, bool heavy = false)
        {
            int b = NaturalLines(kamae).Contains(line) ? _costs["cut_natural"] : _costs["cut_forced"];
            return b + (heavy ? _costs["heavy_surcharge"] : 0);
        }

        public Injury InjuryFor(CutLine line) => _injuries[line];

        // --- sayilar ---------------------------------------------------------

        public int KiMax => _ki["max"];
        public int KiStart => _ki["start"];
        public int KiRegen => _ki["regen_per_beat"];
        public int GuardBonusRegen => _ki["guard_bonus_regen"];
        public int MaxSpend => _ki["max_spend_per_beat"];
        public int WoundsToDie => _damage["wounds_to_die"];

        public int Cost(string key) => _costs[key];
        public int Dmg(string key) => _damage[key];
        public double Gesture(string key) => _gestures[key];
        public BrainConfig BrainConfig(string key) => _brains[key];
        public IEnumerable<string> BrainKeys => _brains.Keys;
    }
}
