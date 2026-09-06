using System;
using System.Collections.Generic;
using NUnit.Framework;
using Samuray.Core;

namespace Samuray.Tests
{
    /// <summary>Jest siniflandiricinin testleri - sim/tests/test_gestures.py'nin portu.
    ///
    /// Bu testler PORTUN SOZLESMESIDIR: Python ve C# ayni girdilere ayni ciktiyi
    /// vermelidir. Biri degisirse digeri de degismeli.
    /// </summary>
    public class GestureTests
    {
        Rules R;
        const float W = 1080f, H = 1920f;

        [SetUp] public void Setup() { R = TestRules.Load(); }

        static List<Pt> Cizgi((float x, float y) a, (float x, float y) b, int n = 24)
        {
            var p = new List<Pt>();
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                p.Add(new Pt(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t));
            }
            return p;
        }

        static List<Pt> Halka(float cx, float cy, float r = 120f, int n = 20)
        {
            var p = new List<Pt>();
            for (int t = 0; t < n; t++)
            {
                double ang = t / (double)(n - 2) * 2 * Math.PI;
                p.Add(new Pt(cx + (float)(r * Math.Cos(ang)), cy + (float)(r * Math.Sin(ang))));
            }
            return p;
        }

        // --- geometri --------------------------------------------------------

        [Test]
        public void ResampleIstenenSayidaNoktaVerir()
            => Assert.AreEqual(32, GestureClassifier.Resample(Cizgi((0, 0), (100, 0)), 32).Count);

        [Test]
        public void DuzCizgideDonusSifiraYakin()
            => Assert.Less(GestureClassifier.TotalTurning(Cizgi((0, 0), (500, 500))), 1.0f);

        [Test]
        public void HalkadaDonusBirTamTur()
            => Assert.Greater(GestureClassifier.TotalTurning(Halka(500, 500)), 300f);

        [Test]
        public void AciHatEslemesi()
        {
            Assert.AreEqual(CutLine.YOKO, GestureClassifier.LineFromAngle(0));
            Assert.AreEqual(CutLine.YOKO, GestureClassifier.LineFromAngle(180));
            Assert.AreEqual(CutLine.SHOMEN, GestureClassifier.LineFromAngle(90));    // asagi
            Assert.AreEqual(CutLine.SHOMEN, GestureClassifier.LineFromAngle(-90));   // yukari
            Assert.AreEqual(CutLine.KESA, GestureClassifier.LineFromAngle(45));      // inen capraz
            Assert.AreEqual(CutLine.KESA, GestureClassifier.LineFromAngle(135));     // inen capraz
            Assert.AreEqual(CutLine.GYAKU_KESA, GestureClassifier.LineFromAngle(-45));   // yukselen
            Assert.AreEqual(CutLine.GYAKU_KESA, GestureClassifier.LineFromAngle(-135));  // yukselen
        }

        // --- siniflandirma ---------------------------------------------------

        [Test]
        public void BesHatDaTaninir()
        {
            var beklenen = new Dictionary<CutLine, List<Pt>>
            {
                { CutLine.SHOMEN,     Cizgi((540, 200), (540, 1050)) },
                { CutLine.KESA,       Cizgi((950, 180), (150, 1000)) },
                { CutLine.GYAKU_KESA, Cizgi((150, 1050), (950, 220)) },
                { CutLine.YOKO,       Cizgi((80, 640), (1000, 640)) },
                { CutLine.TSUKI,      Cizgi((540, 900), (540, 730)) },
            };
            foreach (var kv in beklenen)
            {
                var g = GestureClassifier.Classify(kv.Value, W, H, R);
                Assert.IsTrue(g.Action.HasValue, kv.Key + " taninmadi");
                Assert.AreEqual(ActionType.CUT, g.Action.Value.Type);
                Assert.AreEqual(kv.Key, g.Action.Value.Line.Value, kv.Key + " yanlis taniniyor");
            }
        }

        [Test]
        public void UzunlukBagliligiBelirler()
        {
            var kisa = GestureClassifier.Classify(Cizgi((540, 500), (540, 800)), W, H, R);
            var orta = GestureClassifier.Classify(Cizgi((540, 380), (540, 1000)), W, H, R);
            var uzun = GestureClassifier.Classify(Cizgi((540, 150), (540, 1150)), W, H, R);
            Assert.AreEqual(Tier.FEINT, kisa.Tier);
            Assert.AreEqual(Tier.FAST, orta.Tier);
            Assert.AreEqual(Tier.HEAVY, uzun.Tier);
            Assert.IsTrue(uzun.Action.Value.Heavy);
            Assert.IsFalse(orta.Action.Value.Heavy);
        }

        [Test]
        public void KapaliHalkaParrydir()
        {
            var g = GestureClassifier.Classify(Halka(540, 400), W, H, R);
            Assert.AreEqual(ActionType.PARRY, g.Action.Value.Type);
        }

        [Test]
        public void AltSeritDurusSecer()
        {
            var yuvalar = R.AllKamae;
            for (int i = 0; i < yuvalar.Count; i++)
            {
                float x = W * (i + 0.5f) / yuvalar.Count;
                var g = GestureClassifier.Classify(Cizgi((x, 1750), (x + 20, 1780), 6), W, H, R);
                Assert.AreEqual(ActionType.GUARD, g.Action.Value.Type);
                Assert.AreEqual(yuvalar[i], g.Action.Value.ToKamae.Value);
            }
        }

        [Test]
        public void AltSerittekiKisaDokunusReddedilmez()
        {
            // Serit dokunusu bilerek kisadir; uzunluk esigine takilmamali
            var g = GestureClassifier.Classify(Cizgi((540, 1800), (548, 1806), 4), W, H, R);
            Assert.IsTrue(g.Action.HasValue);
        }

        [Test]
        public void CokKisaCizgiReddedilir()
        {
            Assert.IsFalse(GestureClassifier.Classify(Cizgi((540, 500), (545, 505), 4), W, H, R).Action.HasValue);
            Assert.IsFalse(GestureClassifier.Classify(new List<Pt> { new Pt(1, 1) }, W, H, R).Action.HasValue);
        }

        [Test]
        public void AgirKesimUlasilabilirOlmali()
        {
            // Referans ekran kosegeni olsaydi AGIR pratikte cizilemezdi.
            var g = GestureClassifier.Classify(Cizgi((540, 60), (540, 1200)), W, H, R);
            Assert.AreEqual(Tier.HEAVY, g.Tier);
        }

        // --- niyet kurulumu --------------------------------------------------

        [Test]
        public void YarimCizgiArdindanBaskasiGelirseFeintOlur()
        {
            var yalan = GestureClassifier.Classify(Cizgi((540, 500), (540, 800)), W, H, R);
            var gercek = GestureClassifier.Classify(Cizgi((80, 640), (1000, 640)), W, H, R);
            var niyet = GestureClassifier.AssembleIntent(new[] { yalan, gercek });
            Assert.AreEqual(ActionType.FEINT, niyet[0].Type);
            Assert.AreEqual(ActionType.CUT, niyet[1].Type);
        }

        [Test]
        public void TekBasinaYarimCizgiSadeceZayifKesimdir()
        {
            var yalan = GestureClassifier.Classify(Cizgi((540, 500), (540, 800)), W, H, R);
            var niyet = GestureClassifier.AssembleIntent(new[] { yalan });
            Assert.AreEqual(ActionType.CUT, niyet[0].Type);
            Assert.IsFalse(niyet[0].Heavy);
        }

        [Test]
        public void TaninmayanJestlerElenir()
            => Assert.IsEmpty(GestureClassifier.AssembleIntent(new[] { new Gesture(), new Gesture() }));
    }
}
