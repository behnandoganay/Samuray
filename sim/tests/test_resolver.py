"""Cozumleme kurallarinin testleri.

resolve_beat saf oldugu icin her kural tek bir turla izole edilebiliyor.
Denge degisirse bu testler degismemeli - burada sayilar degil KURALLAR test edilir.
"""

import unittest

from samuray.model import Fighter, Injury, Kamae, Line, cut, feint, guard, parry, read
from samuray.resolver import intent_cost, legalize, resolve_beat
from samuray.rules import load_rules

R = load_rules()


def dovusculer(ka=Kamae.CHUDAN, kb=Kamae.CHUDAN, ki=4):
    return (Fighter("A", kamae=ka, ki=ki), Fighter("B", kamae=kb, ki=ki))


def acik_hat(kamae: Kamae, haric: set | None = None) -> Line:
    """Bu durusun savunMAdigi bir hat. Tabloyu sabitlemez, kuraldan turetir."""
    aday = set(R.all_lines) - R.guards_of(kamae) - (haric or set())
    return sorted(aday, key=lambda x: x.value)[0]


def korunan_hat(kamae: Kamae) -> Line:
    """Bu durusun savundugu bir hat."""
    return sorted(R.guards_of(kamae), key=lambda x: x.value)[0]


class TestGardVeKesim(unittest.TestCase):
    def test_gardsiz_hat_yara_alir(self):
        a, b = dovusculer(ka=Kamae.JODAN, kb=Kamae.CHUDAN)
        r = resolve_beat(a, [cut(acik_hat(Kamae.CHUDAN))], b, [guard()], R)
        self.assertEqual(r.b.wounds, R.dmg("fast_cut"))
        self.assertEqual(len(r.hits), 1)

    def test_gardli_hat_bloke_edilir(self):
        a, b = dovusculer(ka=Kamae.CHUDAN, kb=Kamae.CHUDAN)
        r = resolve_beat(a, [cut(korunan_hat(Kamae.CHUDAN))], b, [guard()], R)
        self.assertEqual(r.b.wounds, 0)
        self.assertEqual(r.hits, [])

    def test_agir_kesim_gardi_kirar_ve_sizdirir(self):
        a, b = dovusculer(ka=Kamae.CHUDAN, kb=Kamae.CHUDAN)
        r = resolve_beat(a, [cut(korunan_hat(Kamae.CHUDAN), heavy=True)], b, [guard()], R)
        self.assertEqual(r.b.wounds, R.dmg("guard_break_chip"))
        self.assertTrue(r.hits[0].guard_broken)
        self.assertIs(r.b.kamae, Kamae.GEDAN, "gardi kirilan GEDAN'a itilir")

    def test_bloke_edilen_hizli_kesim_ki_yakar(self):
        a, b = dovusculer(ka=Kamae.CHUDAN, kb=Kamae.CHUDAN, ki=4)
        r = resolve_beat(a, [cut(korunan_hat(Kamae.CHUDAN))], b, [guard()], R)
        # 4 - 1 (kesim) - 1 (bloke cezasi) + 2 (yenilenme) = 4, tavanda kalir
        self.assertLessEqual(r.a.ki, 4)
        self.assertIn("karsiladi", " ".join(r.events))


class TestParry(unittest.TestCase):
    def test_dogru_parry_sersemletir_ve_riposte_verir(self):
        a, b = dovusculer(ka=Kamae.CHUDAN, kb=Kamae.JODAN)
        hat = acik_hat(Kamae.CHUDAN)  # A'nin gardi degil, parry'si test ediliyor
        r = resolve_beat(a, [parry(hat)], b, [cut(hat)], R)
        self.assertEqual(r.a.wounds, 0, "savurulan kesim yara yapmaz")
        self.assertTrue(r.a.riposte)
        self.assertIs(r.b.kamae, Kamae.GEDAN, "sersemleyen GEDAN'a duser")

    def test_yanlis_parry_bosa_gider(self):
        a, b = dovusculer(ka=Kamae.CHUDAN, kb=Kamae.JODAN)
        gelen = acik_hat(Kamae.CHUDAN)
        r = resolve_beat(a, [parry(acik_hat(Kamae.CHUDAN, haric={gelen}))],
                         b, [cut(gelen)], R)
        self.assertEqual(r.a.wounds, R.dmg("fast_cut"))
        self.assertIn("bosuna", " ".join(r.events))

    def test_riposte_kesimi_gard_da_parry_de_durduramaz(self):
        a, b = dovusculer(ka=Kamae.CHUDAN, kb=Kamae.CHUDAN)
        a.riposte = True
        # Korunan bir hat ustelik parry ediliyor; riposte ikisini de delip gecer
        hat = korunan_hat(Kamae.CHUDAN)
        r = resolve_beat(a, [cut(hat)], b, [parry(hat)], R)
        self.assertEqual(r.b.wounds, R.dmg("fast_cut"))
        self.assertFalse(r.a.riposte, "riposte tek kullanimliktir")


class TestFeint(unittest.TestCase):
    def test_feint_gardi_kaydirir(self):
        # Acik bir hatta yalan at ki gard oraya kaysin, sonra bosalan KORUNAN
        # hattan gercek kesimi indir.
        a, b = dovusculer(ka=Kamae.CHUDAN, kb=Kamae.CHUDAN)
        yalan, gercek = acik_hat(Kamae.CHUDAN), korunan_hat(Kamae.CHUDAN)
        r = resolve_beat(a, [feint(yalan), cut(gercek)], b, [guard()], R)
        self.assertEqual(r.b.wounds, R.dmg("fast_cut"))
        self.assertIn("kandi", " ".join(r.events))

    def test_zaten_korunan_hatta_feint_ise_yaramaz(self):
        a, b = dovusculer(ka=Kamae.CHUDAN, kb=Kamae.CHUDAN)
        # Zaten korunan bir hatta yalan soylenemez -> gard yerinde kalir
        korunan = sorted(R.guards_of(Kamae.CHUDAN), key=lambda x: x.value)
        r = resolve_beat(a, [feint(korunan[0]), cut(korunan[1])], b, [guard(Kamae.CHUDAN)], R)
        self.assertIn("zaten o hatti koruyordu", " ".join(r.events))
        self.assertEqual(r.b.wounds, 0)

    def test_usta_feint_gormez(self):
        a, b = dovusculer(ka=Kamae.CHUDAN, kb=Kamae.CHUDAN)
        b.sees_through_feints = True
        yalan, gercek = acik_hat(Kamae.CHUDAN), korunan_hat(Kamae.CHUDAN)
        r = resolve_beat(a, [feint(yalan), cut(gercek)], b, [guard()], R)
        self.assertEqual(r.b.wounds, 0, "gard kaymadi, gercek hat hala korunuyor")


class TestCatismaVeSuki(unittest.TestCase):
    def test_ayni_hatta_kesimler_catisir(self):
        a, b = dovusculer(ka=Kamae.JODAN, kb=Kamae.JODAN)
        r = resolve_beat(a, [cut(Line.SHOMEN)], b, [cut(Line.SHOMEN)], R)
        self.assertEqual((r.a.wounds, r.b.wounds), (0, 0))
        self.assertIs(r.a.kamae, Kamae.GEDAN)
        self.assertIs(r.b.kamae, Kamae.GEDAN)

    def test_catismada_az_yuklenen_dengesini_kaybeder(self):
        a, b = dovusculer(ka=Kamae.JODAN, kb=Kamae.JODAN)
        r = resolve_beat(a, [cut(Line.SHOMEN)], b, [cut(Line.SHOMEN, heavy=True)], R)
        self.assertTrue(r.a.suki, "1 Ki harcayan, 2 Ki harcayana karsi savrulur")
        self.assertFalse(r.b.suki)

    def test_nefesin_otesine_gecmek_suki_yapar(self):
        a, b = dovusculer(ka=Kamae.CHUDAN, kb=Kamae.CHUDAN, ki=1)
        r = resolve_beat(a, [cut(Line.SHOMEN, heavy=True)], b, [guard()], R)
        self.assertTrue(r.a.suki)

    def test_suki_gardi_kaldirir_ve_hasari_ikiye_katlar(self):
        a, b = dovusculer(ka=Kamae.CHUDAN, kb=Kamae.CHUDAN)
        b.suki = True
        # Normalde korunan bir hat: SUKI gardi tamamen kaldirir
        r = resolve_beat(a, [cut(korunan_hat(Kamae.CHUDAN))], b, [guard()], R)
        self.assertEqual(r.b.wounds, R.dmg("fast_cut") * R.dmg("suki_multiplier"))


class TestWaki(unittest.TestCase):
    def test_waki_hicbir_hatti_savunmaz(self):
        a, b = dovusculer(ka=Kamae.CHUDAN, kb=Kamae.WAKI)
        for line in R.all_lines:
            r = resolve_beat(a, [cut(line)], b, [guard()], R)
            self.assertGreater(r.b.wounds, 0, f"{line.value} WAKI'ye gecmeliydi")

    def test_waki_kesimi_daha_derin(self):
        a, b = dovusculer(ka=Kamae.WAKI, kb=Kamae.JODAN)
        r = resolve_beat(a, [cut(Line.YOKO)], b, [guard()], R)
        self.assertEqual(r.b.wounds, R.dmg("fast_cut") + R.dmg("waki_bonus"))

    def test_waki_turda_tek_eylem(self):
        a, b = dovusculer(ka=Kamae.WAKI, kb=Kamae.JODAN)
        ev = []
        kalan = legalize(a, [cut(Line.YOKO), cut(Line.TSUKI)], R, ev)
        self.assertEqual(len(kalan), R.waki_max_actions)

    def test_wakiye_ancak_gard_alarak_girilir(self):
        # Hicbir hat WAKI'de bitmez; oraya girmek bir tur harcamayi gerektirir.
        self.assertNotIn(Kamae.WAKI, {R.ends_at(l) for l in R.all_lines})
        self.assertIn(Kamae.WAKI, R.adjacent(Kamae.GEDAN))


class TestDuruslarVeYaralar(unittest.TestCase):
    def test_kesim_bitis_durusu_sadece_hatta_bagli(self):
        for baslangic in (Kamae.JODAN, Kamae.CHUDAN, Kamae.GEDAN, Kamae.HASSO):
            for line in R.all_lines:
                a, b = dovusculer(ka=baslangic, kb=Kamae.GEDAN)
                r = resolve_beat(a, [cut(line)], b, [guard()], R)
                if not r.a.staggered:
                    self.assertIs(r.a.kamae, R.ends_at(line),
                                  f"{baslangic.value}+{line.value}")

    def test_komsu_olmayan_durusa_gecilemez(self):
        a = Fighter("A", kamae=Kamae.JODAN)
        ev = []
        kalan = legalize(a, [guard(Kamae.WAKI)], R, ev)  # JODAN'in komsusu degil
        self.assertIsNone(kalan[0].to_kamae)
        self.assertIn("komsu degil", " ".join(ev))

    def test_kol_yarasi_agir_kesimi_engeller(self):
        a = Fighter("A", kamae=Kamae.JODAN, injuries={Injury.ARM})
        kalan = legalize(a, [cut(Line.SHOMEN, heavy=True)], R, [])
        self.assertFalse(kalan[0].heavy)

    def test_bacak_yarasi_durus_degistirmeyi_engeller(self):
        a = Fighter("A", kamae=Kamae.CHUDAN, injuries={Injury.LEG})
        kalan = legalize(a, [guard(Kamae.GEDAN)], R, [])
        self.assertIsNone(kalan[0].to_kamae)

    def test_akciger_yarasi_yenilenmeyi_dusurur(self):
        a, b = dovusculer(ki=0)
        a.injuries.add(Injury.LUNG)
        r = resolve_beat(a, [], b, [], R)
        self.assertEqual(r.a.ki, R.ki_regen - 1)
        self.assertEqual(r.b.ki, R.ki_regen)

    def test_uc_yara_oldurur(self):
        a, b = dovusculer(ka=Kamae.JODAN, kb=Kamae.CHUDAN)
        b.wounds = R.wounds_to_die - 1
        r = resolve_beat(a, [cut(acik_hat(Kamae.CHUDAN))], b, [guard()], R)
        self.assertFalse(r.b.alive)
        self.assertTrue(r.finished)


class TestDurusTasarimi(unittest.TestCase):
    """Duruslarin birbirinden ayrisip ayrismadigini sinar.

    Ilk tasarimda JODAN ile HASSO birebir aynıydi (ayni gard, ayni ucuz kesim);
    denge laboratuvari HASSO'yu %1 kullanimda gosterince yakalandi. Bu test
    o hatanin geri gelmesini engeller.
    """

    def test_hicbir_durus_bir_digerinin_kopyasi_degil(self):
        savunma = {}
        for k in R.all_kamae:
            g = frozenset(R.guards_of(k))
            if not g:
                continue  # WAKI bilerek hicbir sey savunmaz
            self.assertNotIn(g, savunma,
                             f"{k.value} ile {savunma.get(g)} ayni hatlari savunuyor")
            savunma[g] = k.value

        ucuz = {}
        for k in R.all_kamae:
            n = frozenset(R.natural_lines(k))
            if len(n) == len(R.all_lines):
                continue  # WAKI'de her kesim ucuz
            self.assertNotIn(n, ucuz, f"{k.value} ile {ucuz.get(n)} ayni kesimleri ucuza veriyor")
            ucuz[n] = k.value

    def test_her_hat_en_az_bir_durus_tarafindan_savunulur(self):
        savunulan = set()
        for k in R.all_kamae:
            savunulan |= R.guards_of(k)
        self.assertEqual(savunulan, set(R.all_lines))

    def test_akis_grafigi_her_durusa_ulasabilir(self):
        # WAKI disinda her durusa bir kesimin bitisiyle varilabilmeli
        varilabilir = {R.ends_at(l) for l in R.all_lines}
        beklenen = set(R.all_kamae) - {Kamae.WAKI}
        self.assertEqual(varilabilir, beklenen)


class TestKiEkonomisi(unittest.TestCase):
    def test_maliyet_iki_eksenin_toplami(self):
        # JODAN: SHOMEN dogal, TSUKI zorlanan
        self.assertEqual(R.cut_cost(Kamae.JODAN, Line.SHOMEN, False), 1)
        self.assertEqual(R.cut_cost(Kamae.JODAN, Line.SHOMEN, True), 2)
        self.assertEqual(R.cut_cost(Kamae.JODAN, Line.TSUKI, False), 2)
        self.assertEqual(R.cut_cost(Kamae.JODAN, Line.TSUKI, True), 3)

    def test_harcama_tavani_asilirsa_son_eylem_duser(self):
        a = Fighter("A", kamae=Kamae.CHUDAN, ki=4)
        ev = []
        kalan = legalize(a, [cut(Line.TSUKI), cut(Line.YOKO), cut(Line.SHOMEN)], R, ev)
        self.assertLessEqual(intent_cost(a, kalan, R), R.max_spend)

    def test_gard_ekstra_nefes_verir(self):
        a, b = dovusculer(ki=0)
        r = resolve_beat(a, [guard()], b, [], R)
        self.assertEqual(r.a.ki, R.ki_regen + R.guard_bonus_regen)
        self.assertEqual(r.b.ki, R.ki_regen)

    def test_yenilenme_harcama_tavaninin_altinda(self):
        # Her tur tam guc saldirmayi imkansiz kilan kural: nefes almak zorunlusun
        self.assertLess(R.ki_regen, R.max_spend)


class TestSaflik(unittest.TestCase):
    def test_resolve_beat_girdiyi_degistirmez(self):
        a, b = dovusculer(ka=Kamae.JODAN, kb=Kamae.CHUDAN)
        onceki = (a.kamae, a.ki, a.wounds, b.kamae, b.ki, b.wounds)
        resolve_beat(a, [cut(Line.SHOMEN, heavy=True)], b, [cut(Line.YOKO)], R)
        self.assertEqual((a.kamae, a.ki, a.wounds, b.kamae, b.ki, b.wounds), onceki)

    def test_ayni_girdi_ayni_cikti(self):
        a, b = dovusculer(ka=Kamae.HASSO, kb=Kamae.GEDAN)
        ia, ib = [feint(Line.SHOMEN), cut(Line.KESA)], [parry(Line.KESA)]
        r1 = resolve_beat(a, ia, b, ib, R)
        r2 = resolve_beat(a, ia, b, ib, R)
        self.assertEqual(r1.events, r2.events)
        self.assertEqual((r1.a.wounds, r1.b.wounds), (r2.a.wounds, r2.b.wounds))


if __name__ == "__main__":
    unittest.main()
