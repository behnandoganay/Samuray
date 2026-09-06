using System.Collections.Generic;

namespace Samuray.Core
{
    /// <summary>Bes kesim hatti.</summary>
    /// <remarks>
    /// Enum isimleri rules.json'daki yazimla BIREBIR ayni tutuldu (alt cizgi dahil).
    /// Boylece Enum.Parse dogrudan calisiyor ve JSON ile kod arasinda esleme
    /// tablosu tasimak gerekmiyor.
    /// </remarks>
    public enum CutLine
    {
        SHOMEN,      // dikey, yukaridan asagi
        KESA,        // inen capraz
        GYAKU_KESA,  // yukselen capraz (kiriage)
        YOKO,        // yatay
        TSUKI        // saplama
    }

    /// <summary>Bes durus. Neyi savundugunu, hangi kesimin ucuz oldugunu ve
    /// kesimden sonra nereye dusuleceğini birlikte belirler.</summary>
    public enum Kamae
    {
        JODAN,   // kilic tepede
        CHUDAN,  // kilic ortada, uc bogazda
        GEDAN,   // kilic asagida
        HASSO,   // kilic sag omuzda
        WAKI     // kilic gizli - hicbir sey savunmaz, her kesim surpriz
    }

    public enum ActionType { CUT, PARRY, FEINT, GUARD, READ }

    public enum Injury
    {
        ARM,   // agir kesim yapilamaz
        LEG,   // durus degistirilemez
        LUNG   // Ki yenilenmesi -1
    }

    /// <summary>Tek bir jestin karsiligi. Oyuncu bunlari cizerek olusturur.</summary>
    public readonly struct Action
    {
        public readonly ActionType Type;
        public readonly CutLine? Line;
        public readonly bool Heavy;
        public readonly Kamae? ToKamae;   // sadece GUARD icin

        public Action(ActionType type, CutLine? line = null, bool heavy = false, Kamae? toKamae = null)
        {
            Type = type; Line = line; Heavy = heavy; ToKamae = toKamae;
        }

        public static Action Cut(CutLine line, bool heavy = false) => new Action(ActionType.CUT, line, heavy);
        public static Action Parry(CutLine line) => new Action(ActionType.PARRY, line);
        public static Action Feint(CutLine line) => new Action(ActionType.FEINT, line);
        public static Action Guard(Kamae? toKamae = null) => new Action(ActionType.GUARD, toKamae: toKamae);
        public static Action Read() => new Action(ActionType.READ);

        public override string ToString()
        {
            switch (Type)
            {
                case ActionType.CUT:
                    return (Heavy ? "AGIR" : "HIZLI") + " kesim: " + Line;
                case ActionType.GUARD:
                    return "Gard" + (ToKamae.HasValue ? " -> " + ToKamae.Value : " (yerinde)");
                default:
                    return Line.HasValue ? Type + ": " + Line.Value : Type.ToString();
            }
        }
    }

    /// <summary>Bir savascinin tam durumu. BeatResolver bunu kopyalayip yenisini dondurur.</summary>
    public sealed class Fighter
    {
        public string Name;
        public Kamae Kamae = Kamae.CHUDAN;
        public int Ki = 3;
        public int KiMax = 4;
        public int Wounds;
        public HashSet<Injury> Injuries = new HashSet<Injury>();

        // Tur arasi tasinan bayraklar
        public bool Suki;      // kendi hatan: gard alamaz VE gelen hasar iki kat
        public bool Exposed;   // gardin kirildi: gard alamaz, ama hasar katlanmaz
        public bool Riposte;   // basarili parry odulu: ilk kesim engellenemez
        public bool Staggered; // bu turda parry yedi, Ki'si sifirlandi

        public bool SeesThroughFeints;

        public Fighter(string name) { Name = name; }

        public bool Alive(Rules rules) => Wounds < rules.WoundsToDie;

        public Fighter Clone() => new Fighter(Name)
        {
            Kamae = Kamae, Ki = Ki, KiMax = KiMax, Wounds = Wounds,
            Injuries = new HashSet<Injury>(Injuries),
            Suki = Suki, Exposed = Exposed, Riposte = Riposte, Staggered = Staggered,
            SeesThroughFeints = SeesThroughFeints
        };

        public string Summary()
        {
            var yara = new string('x', Wounds) + new string('.', System.Math.Max(0, 3 - Wounds));
            var parts = new List<string> { Kamae.ToString(), "Ki " + Ki, "yara [" + yara + "]" };
            if (Injuries.Count > 0)
            {
                var list = new List<string>();
                foreach (var i in Injuries) list.Add(i.ToString());
                list.Sort();
                parts.Add(string.Join("+", list));
            }
            if (Suki) parts.Add("SUKI");
            if (Exposed) parts.Add("ACIK");
            if (Riposte) parts.Add("RIPOSTE");
            return Name + ": " + string.Join(" | ", parts);
        }
    }

    /// <summary>Denge istatistikleri icin tek bir isabet kaydi.</summary>
    public sealed class Hit
    {
        public string Attacker;
        public string Defender;
        public CutLine Line;
        public bool Heavy;
        public int Damage;
        public bool GuardBroken;
        public bool FromWaki;
        public bool VsSuki;
    }

    public sealed class BeatResult
    {
        public Fighter A;
        public Fighter B;
        public List<string> Events = new List<string>();
        public List<Hit> Hits = new List<Hit>();
    }
}
