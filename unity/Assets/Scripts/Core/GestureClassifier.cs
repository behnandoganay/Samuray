using System;
using System.Collections.Generic;

namespace Samuray.Core
{
    /// <summary>Ekran noktasi. UnityEngine.Vector2 kullanilmiyor cunku cekirdek
    /// motordan bagimsiz; sunum katmani donusturur.</summary>
    public readonly struct Pt
    {
        public readonly float X, Y;   // Y ekrana asagi dogru buyur
        public Pt(float x, float y) { X = x; Y = y; }
    }

    public enum Tier { FEINT, FAST, HEAVY, PARRY }

    public sealed class Gesture
    {
        public Action? Action;
        public Tier? Tier;
        public float Confidence;
        public string Reason = "";
    }

    /// <summary>Jest tanima: nokta listesini bir Action'a cevirir.
    ///
    /// Hazir $1 Unistroke Recognizer yerine kendi hafif siniflandiricimiz.
    /// Jestlerimiz neredeyse hep duz cizgi ve basit sekil oldugu icin bu hem
    /// daha kolay ayarlanir hem de anlasilir kod olur.
    ///
    /// Ekran iki bolgeye ayrilir:
    ///   Ust bolge : kesim alani (cizgiler ve halkalar)
    ///   Alt serit : durus secici (bes yatay yuva)
    /// Bu ayrim en buyuk belirsizligi kaldirir: asagi dogru bir cizgi hem SHOMEN
    /// kesimi hem "kendine dogru gard" olabilirdi; bolge bunu tek basina cozer.
    /// </summary>
    public static class GestureClassifier
    {
        static float Dist(Pt a, Pt b) => (float)Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Y - a.Y) * (b.Y - a.Y));

        static float PathLength(IList<Pt> p)
        {
            float t = 0;
            for (int i = 0; i < p.Count - 1; i++) t += Dist(p[i], p[i + 1]);
            return t;
        }

        /// <summary>Cizgiyi esit araliklarla n noktaya yeniden ornekle.</summary>
        public static List<Pt> Resample(IList<Pt> pts, int n)
        {
            var res = new List<Pt>();
            if (pts.Count < 2) { res.AddRange(pts); return res; }
            float total = PathLength(pts);
            if (total <= 0) { for (int i = 0; i < n; i++) res.Add(pts[0]); return res; }

            float step = total / (n - 1);
            res.Add(pts[0]);
            float acc = 0;
            int idx = 1;
            Pt p = pts[0];
            while (idx < pts.Count && res.Count < n)
            {
                float d = Dist(p, pts[idx]);
                if (acc + d >= step)
                {
                    float t = d > 0 ? (step - acc) / d : 0f;
                    var np = new Pt(p.X + t * (pts[idx].X - p.X), p.Y + t * (pts[idx].Y - p.Y));
                    res.Add(np); p = np; acc = 0;
                }
                else { acc += d; p = pts[idx]; idx++; }
            }
            while (res.Count < n) res.Add(pts[pts.Count - 1]);
            return res;
        }

        /// <summary>Toplam donus acisi (derece). Duz cizgide ~0, kapali halkada &gt;270.</summary>
        public static float TotalTurning(IList<Pt> pts)
        {
            var angs = new List<double>();
            for (int i = 0; i < pts.Count - 1; i++)
            {
                double dx = pts[i + 1].X - pts[i].X, dy = pts[i + 1].Y - pts[i].Y;
                if (dx != 0 || dy != 0) angs.Add(Math.Atan2(dy, dx));
            }
            double total = 0;
            for (int i = 0; i < angs.Count - 1; i++)
            {
                double d = angs[i + 1] - angs[i];
                while (d > Math.PI) d -= 2 * Math.PI;
                while (d < -Math.PI) d += 2 * Math.PI;
                total += Math.Abs(d);
            }
            return (float)(total * 180.0 / Math.PI);
        }

        /// <summary>Uzunluk esiklerinin olculdugu referans.
        ///
        /// Ekran kosegenini kullanmak yanlisti: bas parmak o boyu cizemez, o yuzden
        /// AGIR kesim pratikte ulasilamaz oluyordu. Dogru referans, kesim alaninin
        /// gercekten kat edilebilir olcusu.
        /// </summary>
        public static float ReferenceLength(float width, float height, double selfBand)
            => (float)Math.Min(width, height * (1.0 - selfBand));

        /// <summary>Net vektorun acisini bes hattan birine esle (Y ekrana asagi dogru).
        ///
        /// Inen capraz -> KESA, yukselen capraz -> GYAKU_KESA. Iki koseden de gelse
        /// fark etmez; onemli olan kesimin inip yukselmesi.
        /// </summary>
        public static CutLine LineFromAngle(double deg)
        {
            double d = ((deg + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;
            double a = Math.Abs(d);
            if (a <= 22.5 || a >= 157.5) return CutLine.YOKO;
            if ((d > 67.5 && d < 112.5) || (d > -112.5 && d < -67.5)) return CutLine.SHOMEN;
            return d > 0 ? CutLine.KESA : CutLine.GYAKU_KESA;
        }

        /// <param name="selfBandOverride">
        /// Alt seridin (durus secici) oranini ezer. Cizim yuzeyinin altinda serit
        /// YOKSA - ornegin durus secimi ayri butonlarla yapiliyorsa - 0 gecilir.
        /// Varsayilan null: rules.json'daki deger kullanilir.
        /// </param>
        public static Gesture Classify(IList<Pt> pts, float width, float height, Rules r,
                                       IReadOnlyList<Kamae> kamaeSlots = null,
                                       double? selfBandOverride = null)
        {
            if (pts == null || pts.Count < 2) return new Gesture { Reason = "cizgi cok kisa" };

            double selfBand = selfBandOverride ?? r.Gesture("self_band_ratio");
            float refLen = ReferenceLength(width, height, selfBand);
            int n = (int)r.Gesture("resample_points");
            var s = Resample(pts, n);

            float ndx = s[s.Count - 1].X - s[0].X, ndy = s[s.Count - 1].Y - s[0].Y;
            float net = (float)Math.Sqrt(ndx * ndx + ndy * ndy);
            float len = PathLength(s);
            float ratio = len / refLen;

            float cy = 0; foreach (var p in s) cy += p.Y; cy /= s.Count;

            // 1. Alt serit: durus secici. Uzunluk kontrolunden ONCE bakilir, cunku
            //    serit uzerindeki bir dokunus bilerek kisadir.
            if (cy >= height * (1.0 - selfBand))
            {
                var slots = kamaeSlots ?? r.AllKamae;
                float cx0 = 0; foreach (var p in s) cx0 += p.X; cx0 /= s.Count;
                int i = (int)(cx0 / width * slots.Count);
                i = Math.Max(0, Math.Min(i, slots.Count - 1));
                return new Gesture
                {
                    Action = Action.Guard(slots[i]),
                    Confidence = 0.9f,
                    Reason = "alt seritte cizildi - durus secimi"
                };
            }

            if (ratio < r.Gesture("min_length_ratio"))
                return new Gesture { Reason = "cizgi cok kisa" };

            // 2. Kapali halka: parry
            float turn = TotalTurning(s);
            bool kapali = net <= len * r.Gesture("loop_closure_ratio");
            if (turn >= r.Gesture("loop_turn_degrees") && kapali)
            {
                float cx = 0; foreach (var p in s) cx += p.X; cx /= s.Count;
                double ang = Math.Atan2(cy - height * 0.5, cx - width * 0.5) * 180.0 / Math.PI;
                double radial = Math.Sqrt(Math.Pow(cx - width * 0.5, 2) + Math.Pow(cy - height * 0.5, 2));
                var hat = radial < refLen * 0.12 ? CutLine.TSUKI : LineFromAngle(ang);
                return new Gesture
                {
                    Action = Action.Parry(hat), Tier = Core.Tier.PARRY,
                    Confidence = 0.8f, Reason = "kapali halka"
                };
            }

            // 3. Kisa ve dusmana dogru (yukari): saplama
            double aci = Math.Atan2(ndy, ndx) * 180.0 / Math.PI;
            if (net <= refLen * r.Gesture("tsuki_max_length_ratio") && ndy < 0)
                return new Gesture
                {
                    Action = Action.Cut(CutLine.TSUKI), Tier = Core.Tier.FAST,
                    Confidence = 0.75f, Reason = "kisa ileri durtme"
                };

            // 4. Kesim: hat aciyla, baglilik uzunlukla
            var line = LineFromAngle(aci);
            Tier tier;
            if (ratio < r.Gesture("feint_max_length_ratio")) tier = Core.Tier.FEINT;
            else if (ratio >= r.Gesture("heavy_min_length_ratio")) tier = Core.Tier.HEAVY;
            else tier = Core.Tier.FAST;

            float duzluk = Math.Max(0f, 1f - turn / 180f);
            return new Gesture
            {
                Action = Action.Cut(line, tier == Core.Tier.HEAVY),
                Tier = tier,
                Confidence = 0.4f + 0.6f * duzluk,
                Reason = tier + " kesim"
            };
        }

        /// <summary>Bir turda cizilen jestleri niyete cevir.
        ///
        /// Yarim birakilmis (FEINT kademesi) bir kesim ANCAK arkasindan baska bir
        /// jest geliyorsa yalan sayilir. Tek basina cizilmisse sadece zayif bir
        /// kesimdir. Jestin *tamamlanmamisligi* feint'in kendisidir - ayri bir
        /// buton yok.
        /// </summary>
        public static List<Action> AssembleIntent(IList<Gesture> gestures)
        {
            var gecerli = new List<Gesture>();
            foreach (var g in gestures) if (g.Action.HasValue) gecerli.Add(g);

            var outp = new List<Action>();
            for (int i = 0; i < gecerli.Count; i++)
            {
                var act = gecerli[i].Action.Value;
                bool sonMu = i == gecerli.Count - 1;
                if (act.Type == ActionType.CUT && gecerli[i].Tier == Core.Tier.FEINT && !sonMu)
                    outp.Add(Action.Feint(act.Line.Value));
                else
                    outp.Add(act);
            }
            return outp;
        }
    }
}
