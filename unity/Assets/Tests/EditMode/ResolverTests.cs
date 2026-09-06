using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Samuray.Core;

namespace Samuray.Tests
{
    /// <summary>Cozumleme kurallarinin testleri - sim/tests/test_resolver.py'nin portu.
    ///
    /// Resolve() saf oldugu icin her kural tek bir turla izole edilebiliyor.
    /// Denge degisirse bu testler degismemeli: burada sayilar degil KURALLAR
    /// test edilir, o yuzden acik/korunan hatlar tablodan TURETILIR.
    /// </summary>
    public class ResolverTests
    {
        Rules R;

        [SetUp] public void Setup() { R = TestRules.Load(); }

        (Fighter, Fighter) Dovusculer(Kamae ka = Kamae.CHUDAN, Kamae kb = Kamae.CHUDAN, int ki = 4)
            => (new Fighter("A") { Kamae = ka, Ki = ki }, new Fighter("B") { Kamae = kb, Ki = ki });

        /// <summary>Bu durusun savunMAdigi bir hat. Tabloyu sabitlemez, kuraldan turetir.</summary>
        CutLine AcikHat(Kamae k, HashSet<CutLine> haric = null)
        {
            var g = R.GuardsOf(k);
            return R.AllLines.Where(l => !g.Contains(l) && (haric == null || !haric.Contains(l)))
                             .OrderBy(l => l.ToString(), System.StringComparer.Ordinal).First();
        }
        CutLine KorunanHat(Kamae k)
            => R.GuardsOf(k).OrderBy(l => l.ToString(), System.StringComparer.Ordinal).First();

        static List<Action> I(params Action[] a) => new List<Action>(a);
        static string Joined(BeatResult r) => string.Join(" ", r.Events);

        // --- gard ve kesim ---------------------------------------------------

        [Test]
        public void GardsizHatYaraAlir()
        {
            var (a, b) = Dovusculer(Kamae.JODAN, Kamae.CHUDAN);
            var r = BeatResolver.Resolve(a, I(Action.Cut(AcikHat(Kamae.CHUDAN))), b, I(Action.Guard()), R);
            Assert.AreEqual(R.Dmg("fast_cut"), r.B.Wounds);
            Assert.AreEqual(1, r.Hits.Count);
        }

        [Test]
        public void GardliHatBlokeEdilir()
        {
            var (a, b) = Dovusculer();
            var r = BeatResolver.Resolve(a, I(Action.Cut(KorunanHat(Kamae.CHUDAN))), b, I(Action.Guard()), R);
            Assert.AreEqual(0, r.B.Wounds);
            Assert.IsEmpty(r.Hits);
        }

        [Test]
        public void AgirKesimGardiKirarVeSizdirir()
        {
            var (a, b) = Dovusculer();
            var r = BeatResolver.Resolve(a, I(Action.Cut(KorunanHat(Kamae.CHUDAN), true)), b, I(Action.Guard()), R);
            Assert.AreEqual(R.Dmg("guard_break_chip"), r.B.Wounds);
            Assert.IsTrue(r.Hits[0].GuardBroken);
            Assert.AreEqual(Kamae.GEDAN, r.B.Kamae, "gardi kirilan GEDAN'a itilir");
        }

        [Test]
        public void BlokeEdilenHizliKesimKiYakar()
        {
            var (a, b) = Dovusculer();
            var r = BeatResolver.Resolve(a, I(Action.Cut(KorunanHat(Kamae.CHUDAN))), b, I(Action.Guard()), R);
            Assert.LessOrEqual(r.A.Ki, R.KiMax);
            StringAssert.Contains("karsiladi", Joined(r));
        }

        // --- parry -----------------------------------------------------------

        [Test]
        public void DogruParrySersemletirVeRiposteVerir()
        {
            var (a, b) = Dovusculer(Kamae.CHUDAN, Kamae.JODAN);
            var hat = AcikHat(Kamae.CHUDAN);   // A'nin gardi degil, parry'si test ediliyor
            var r = BeatResolver.Resolve(a, I(Action.Parry(hat)), b, I(Action.Cut(hat)), R);
            Assert.AreEqual(0, r.A.Wounds, "savurulan kesim yara yapmaz");
            Assert.IsTrue(r.A.Riposte);
            Assert.AreEqual(Kamae.GEDAN, r.B.Kamae, "sersemleyen GEDAN'a duser");
        }

        [Test]
        public void YanlisParryBosaGider()
        {
            var (a, b) = Dovusculer(Kamae.CHUDAN, Kamae.JODAN);
            var gelen = AcikHat(Kamae.CHUDAN);
            var yanlis = AcikHat(Kamae.CHUDAN, new HashSet<CutLine> { gelen });
            var r = BeatResolver.Resolve(a, I(Action.Parry(yanlis)), b, I(Action.Cut(gelen)), R);
            Assert.AreEqual(R.Dmg("fast_cut"), r.A.Wounds);
            StringAssert.Contains("bosuna", Joined(r));
        }

        [Test]
        public void RiposteKesimiGardDaParryDeDurduramaz()
        {
            var (a, b) = Dovusculer();
            a.Riposte = true;
            var hat = KorunanHat(Kamae.CHUDAN);
            var r = BeatResolver.Resolve(a, I(Action.Cut(hat)), b, I(Action.Parry(hat)), R);
            Assert.AreEqual(R.Dmg("fast_cut"), r.B.Wounds);
            Assert.IsFalse(r.A.Riposte, "riposte tek kullanimliktir");
        }

        // --- feint -----------------------------------------------------------

        [Test]
        public void FeintGardiKaydirir()
        {
            var (a, b) = Dovusculer();
            var yalan = AcikHat(Kamae.CHUDAN);
            var gercek = KorunanHat(Kamae.CHUDAN);
            var r = BeatResolver.Resolve(a, I(Action.Feint(yalan), Action.Cut(gercek)), b, I(Action.Guard()), R);
            Assert.AreEqual(R.Dmg("fast_cut"), r.B.Wounds);
            StringAssert.Contains("kandi", Joined(r));
        }

        [Test]
        public void ZatenKorunanHattaFeintIseYaramaz()
        {
            var (a, b) = Dovusculer();
            var korunan = R.GuardsOf(Kamae.CHUDAN)
                           .OrderBy(l => l.ToString(), System.StringComparer.Ordinal).ToList();
            var r = BeatResolver.Resolve(a, I(Action.Feint(korunan[0]), Action.Cut(korunan[1])),
                                         b, I(Action.Guard(Kamae.CHUDAN)), R);
            StringAssert.Contains("zaten o hatti koruyordu", Joined(r));
            Assert.AreEqual(0, r.B.Wounds);
        }

        [Test]
        public void UstaFeintGormez()
        {
            var (a, b) = Dovusculer();
            b.SeesThroughFeints = true;
            var r = BeatResolver.Resolve(a, I(Action.Feint(AcikHat(Kamae.CHUDAN)),
                                              Action.Cut(KorunanHat(Kamae.CHUDAN))),
                                         b, I(Action.Guard()), R);
            Assert.AreEqual(0, r.B.Wounds, "gard kaymadi, gercek hat hala korunuyor");
        }

        // --- catisma ve suki -------------------------------------------------

        [Test]
        public void AyniHattaKesimlerCatisir()
        {
            var (a, b) = Dovusculer(Kamae.JODAN, Kamae.JODAN);
            var r = BeatResolver.Resolve(a, I(Action.Cut(CutLine.SHOMEN)), b, I(Action.Cut(CutLine.SHOMEN)), R);
            Assert.AreEqual(0, r.A.Wounds);
            Assert.AreEqual(0, r.B.Wounds);
            Assert.AreEqual(Kamae.GEDAN, r.A.Kamae);
            Assert.AreEqual(Kamae.GEDAN, r.B.Kamae);
        }

        [Test]
        public void CatismadaAzYuklenenDengesiniKaybeder()
        {
            var (a, b) = Dovusculer(Kamae.JODAN, Kamae.JODAN);
            var r = BeatResolver.Resolve(a, I(Action.Cut(CutLine.SHOMEN)),
                                         b, I(Action.Cut(CutLine.SHOMEN, true)), R);
            Assert.IsTrue(r.A.Suki, "1 Ki harcayan, 2 Ki harcayana karsi savrulur");
            Assert.IsFalse(r.B.Suki);
        }

        [Test]
        public void NefesinOtesineGecmekSukiYapar()
        {
            var (a, b) = Dovusculer(Kamae.CHUDAN, Kamae.CHUDAN, 1);
            var r = BeatResolver.Resolve(a, I(Action.Cut(CutLine.SHOMEN, true)), b, I(Action.Guard()), R);
            Assert.IsTrue(r.A.Suki);
        }

        [Test]
        public void SukiGardiKaldirirVeHasariIkiyeKatlar()
        {
            var (a, b) = Dovusculer();
            b.Suki = true;
            var r = BeatResolver.Resolve(a, I(Action.Cut(KorunanHat(Kamae.CHUDAN))), b, I(Action.Guard()), R);
            Assert.AreEqual(R.Dmg("fast_cut") * R.Dmg("suki_multiplier"), r.B.Wounds);
        }

        [Test]
        public void GardKirilmasiSukiDegilAcilmaYapar()
        {
            // Kendi hatan (SUKI) ile rakibin seni zorlamasi (ACILMA) bilerek esit degil:
            // ikisi de gardi kaldirir ama sadece SUKI hasari iki katina cikarir.
            var (a, b) = Dovusculer();
            var r = BeatResolver.Resolve(a, I(Action.Cut(KorunanHat(Kamae.CHUDAN), true)), b, I(Action.Guard()), R);
            Assert.IsTrue(r.B.Exposed);
            Assert.IsFalse(r.B.Suki);
        }

        // --- WAKI ------------------------------------------------------------

        [Test]
        public void WakiHicbirHattiSavunmaz()
        {
            foreach (var line in R.AllLines)
            {
                var (a, b) = Dovusculer(Kamae.CHUDAN, Kamae.WAKI);
                var r = BeatResolver.Resolve(a, I(Action.Cut(line)), b, I(Action.Guard()), R);
                Assert.Greater(r.B.Wounds, 0, line + " WAKI'ye gecmeliydi");
            }
        }

        [Test]
        public void WakiKesimiDahaDerin()
        {
            var (a, b) = Dovusculer(Kamae.WAKI, Kamae.JODAN);
            var r = BeatResolver.Resolve(a, I(Action.Cut(CutLine.YOKO)), b, I(Action.Guard()), R);
            Assert.AreEqual(R.Dmg("fast_cut") + R.Dmg("waki_bonus"), r.B.Wounds);
        }

        [Test]
        public void WakiTurdaTekEylem()
        {
            var (a, _) = Dovusculer(Kamae.WAKI, Kamae.JODAN);
            var kalan = BeatResolver.Legalize(a, I(Action.Cut(CutLine.YOKO), Action.Cut(CutLine.TSUKI)),
                                              R, new List<string>());
            Assert.AreEqual(R.WakiMaxActions, kalan.Count);
        }

        [Test]
        public void WakiyeAncakGardAlarakGirilir()
        {
            // Hicbir hat WAKI'de bitmez; oraya girmek bir tur harcamayi gerektirir.
            var varilan = new HashSet<Kamae>(R.AllLines.Select(l => R.EndsAt(l)));
            Assert.IsFalse(varilan.Contains(Kamae.WAKI));
            Assert.IsTrue(R.Adjacent(Kamae.GEDAN).Contains(Kamae.WAKI));
        }

        // --- duruslar ve yaralar ---------------------------------------------

        [Test]
        public void KesimBitisDurusuSadeceHattaBagli()
        {
            foreach (var baslangic in new[] { Kamae.JODAN, Kamae.CHUDAN, Kamae.GEDAN, Kamae.HASSO })
                foreach (var line in R.AllLines)
                {
                    var (a, b) = Dovusculer(baslangic, Kamae.GEDAN);
                    var r = BeatResolver.Resolve(a, I(Action.Cut(line)), b, I(Action.Guard()), R);
                    if (!r.A.Staggered)
                        Assert.AreEqual(R.EndsAt(line), r.A.Kamae, baslangic + "+" + line);
                }
        }

        [Test]
        public void KomsuOlmayanDurusaGecilemez()
        {
            var a = new Fighter("A") { Kamae = Kamae.JODAN };
            var ev = new List<string>();
            var kalan = BeatResolver.Legalize(a, I(Action.Guard(Kamae.WAKI)), R, ev);
            Assert.IsFalse(kalan[0].ToKamae.HasValue);
            StringAssert.Contains("komsu degil", string.Join(" ", ev));
        }

        [Test]
        public void KolYarasiAgirKesimiEngeller()
        {
            var a = new Fighter("A") { Kamae = Kamae.JODAN };
            a.Injuries.Add(Injury.ARM);
            var kalan = BeatResolver.Legalize(a, I(Action.Cut(CutLine.SHOMEN, true)), R, new List<string>());
            Assert.IsFalse(kalan[0].Heavy);
        }

        [Test]
        public void BacakYarasiDurusDegistirmeyiEngeller()
        {
            var a = new Fighter("A") { Kamae = Kamae.CHUDAN };
            a.Injuries.Add(Injury.LEG);
            var kalan = BeatResolver.Legalize(a, I(Action.Guard(Kamae.GEDAN)), R, new List<string>());
            Assert.IsFalse(kalan[0].ToKamae.HasValue);
        }

        [Test]
        public void AkcigerYarasiYenilenmeyiDusurur()
        {
            var (a, b) = Dovusculer(Kamae.CHUDAN, Kamae.CHUDAN, 0);
            a.Injuries.Add(Injury.LUNG);
            var r = BeatResolver.Resolve(a, new List<Action>(), b, new List<Action>(), R);
            Assert.AreEqual(R.KiRegen - 1, r.A.Ki);
            Assert.AreEqual(R.KiRegen, r.B.Ki);
        }

        [Test]
        public void UcYaraOldurur()
        {
            var (a, b) = Dovusculer(Kamae.JODAN, Kamae.CHUDAN);
            b.Wounds = R.WoundsToDie - 1;
            var r = BeatResolver.Resolve(a, I(Action.Cut(AcikHat(Kamae.CHUDAN))), b, I(Action.Guard()), R);
            Assert.IsFalse(r.B.Alive(R));
        }

        // --- durus tasarimi --------------------------------------------------

        [Test]
        public void HicbirDurusBirDigerininKopyasiDegil()
        {
            // Ilk tasarimda JODAN ile HASSO birebir aynıydi (ayni gard, ayni ucuz
            // kesim); denge laboratuvari HASSO'yu %1 kullanimda gosterince yakalandi.
            var savunma = new Dictionary<string, Kamae>();
            foreach (var k in R.AllKamae)
            {
                var g = R.GuardsOf(k);
                if (g.Count == 0) continue;   // WAKI bilerek hicbir sey savunmaz
                var key = string.Join(",", g.Select(x => x.ToString()).OrderBy(x => x, System.StringComparer.Ordinal));
                Assert.IsFalse(savunma.ContainsKey(key), k + " baska bir durusla ayni hatlari savunuyor");
                savunma[key] = k;
            }

            var ucuz = new Dictionary<string, Kamae>();
            foreach (var k in R.AllKamae)
            {
                var n = R.NaturalLines(k);
                if (n.Count == R.AllLines.Count) continue;   // WAKI'de her kesim ucuz
                var key = string.Join(",", n.Select(x => x.ToString()).OrderBy(x => x, System.StringComparer.Ordinal));
                Assert.IsFalse(ucuz.ContainsKey(key), k + " baska bir durusla ayni kesimleri ucuza veriyor");
                ucuz[key] = k;
            }
        }

        [Test]
        public void HerHatEnAzBirDurusTarafindanSavunulur()
        {
            var savunulan = new HashSet<CutLine>();
            foreach (var k in R.AllKamae) savunulan.UnionWith(R.GuardsOf(k));
            CollectionAssert.AreEquivalent(R.AllLines.ToList(), savunulan.ToList());
        }

        [Test]
        public void AkisGrafigiHerDurusaUlasabilir()
        {
            var varilabilir = new HashSet<Kamae>(R.AllLines.Select(l => R.EndsAt(l)));
            var beklenen = new HashSet<Kamae>(R.AllKamae.Where(k => k != Kamae.WAKI));
            CollectionAssert.AreEquivalent(beklenen.ToList(), varilabilir.ToList());
        }

        // --- Ki ekonomisi ----------------------------------------------------

        [Test]
        public void MaliyetIkiEksenToplami()
        {
            Assert.AreEqual(1, R.CutCost(Kamae.JODAN, CutLine.SHOMEN, false));
            Assert.AreEqual(2, R.CutCost(Kamae.JODAN, CutLine.SHOMEN, true));
            Assert.AreEqual(2, R.CutCost(Kamae.JODAN, CutLine.TSUKI, false));
            Assert.AreEqual(3, R.CutCost(Kamae.JODAN, CutLine.TSUKI, true));
        }

        [Test]
        public void HarcamaTavaniAsilirsaSonEylemDuser()
        {
            var a = new Fighter("A") { Kamae = Kamae.CHUDAN, Ki = 4 };
            var kalan = BeatResolver.Legalize(a, I(Action.Cut(CutLine.TSUKI), Action.Cut(CutLine.YOKO),
                                                   Action.Cut(CutLine.SHOMEN)), R, new List<string>());
            Assert.LessOrEqual(BeatResolver.IntentCost(a, kalan, R), R.MaxSpend);
        }

        [Test]
        public void GardEkstraNefesVerir()
        {
            var (a, b) = Dovusculer(Kamae.CHUDAN, Kamae.CHUDAN, 0);
            var r = BeatResolver.Resolve(a, I(Action.Guard()), b, new List<Action>(), R);
            Assert.AreEqual(R.KiRegen + R.GuardBonusRegen, r.A.Ki);
            Assert.AreEqual(R.KiRegen, r.B.Ki);
        }

        [Test]
        public void YenilenmeHarcamaTavaninAltinda()
        {
            // Her tur tam guc saldirmayi imkansiz kilan kural: nefes almak zorunlusun
            Assert.Less(R.KiRegen, R.MaxSpend);
        }

        // --- saflik ----------------------------------------------------------

        [Test]
        public void ResolveGirdiyiDegistirmez()
        {
            var (a, b) = Dovusculer(Kamae.JODAN, Kamae.CHUDAN);
            var once = (a.Kamae, a.Ki, a.Wounds, b.Kamae, b.Ki, b.Wounds);
            BeatResolver.Resolve(a, I(Action.Cut(CutLine.SHOMEN, true)), b, I(Action.Cut(CutLine.YOKO)), R);
            Assert.AreEqual(once, (a.Kamae, a.Ki, a.Wounds, b.Kamae, b.Ki, b.Wounds));
        }

        [Test]
        public void AyniGirdiAyniCikti()
        {
            var (a, b) = Dovusculer(Kamae.HASSO, Kamae.GEDAN);
            var ia = I(Action.Feint(CutLine.SHOMEN), Action.Cut(CutLine.KESA));
            var ib = I(Action.Parry(CutLine.KESA));
            var r1 = BeatResolver.Resolve(a, ia, b, ib, R);
            var r2 = BeatResolver.Resolve(a, ia, b, ib, R);
            CollectionAssert.AreEqual(r1.Events, r2.Events);
            Assert.AreEqual(r1.A.Wounds, r2.A.Wounds);
            Assert.AreEqual(r1.B.Wounds, r2.B.Wounds);
        }
    }
}
