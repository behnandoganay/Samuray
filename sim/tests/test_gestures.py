"""Jest siniflandiricinin testleri.

Bu testler Unity portunun sozlesmesidir: C# tarafi ayni girdilere ayni
cikti vermelidir.
"""

import math
import unittest

from samuray.gestures import (Gesture, Tier, assemble_intent, classify,
                              line_from_angle, resample, total_turning)
from samuray.model import ActionType, Kamae, Line
from samuray.rules import load_rules

R = load_rules()
W, H = 1080.0, 1920.0


def cizgi(a, b, n=24):
    return [(a[0] + (b[0] - a[0]) * i / (n - 1),
             a[1] + (b[1] - a[1]) * i / (n - 1)) for i in range(n)]


def halka(cx, cy, r=120.0, n=20):
    return [(cx + r * math.cos(t / (n - 2) * 2 * math.pi),
             cy + r * math.sin(t / (n - 2) * 2 * math.pi)) for t in range(n)]


class TestGeometri(unittest.TestCase):
    def test_resample_istenen_sayida_nokta_verir(self):
        self.assertEqual(len(resample(cizgi((0, 0), (100, 0)), 32)), 32)

    def test_duz_cizgide_donus_sifira_yakin(self):
        self.assertLess(total_turning(cizgi((0, 0), (500, 500))), 1.0)

    def test_halkada_donus_bir_tam_tur(self):
        self.assertGreater(total_turning(halka(500, 500)), 300.0)

    def test_aci_hat_eslemesi(self):
        self.assertIs(line_from_angle(0), Line.YOKO)
        self.assertIs(line_from_angle(180), Line.YOKO)
        self.assertIs(line_from_angle(90), Line.SHOMEN)    # asagi
        self.assertIs(line_from_angle(-90), Line.SHOMEN)   # yukari
        self.assertIs(line_from_angle(45), Line.KESA)          # inen capraz
        self.assertIs(line_from_angle(135), Line.KESA)         # inen capraz
        self.assertIs(line_from_angle(-45), Line.GYAKU_KESA)   # yukselen
        self.assertIs(line_from_angle(-135), Line.GYAKU_KESA)  # yukselen


class TestSiniflandirma(unittest.TestCase):
    def test_bes_hat_da_taninir(self):
        beklenen = {
            Line.SHOMEN: cizgi((540, 200), (540, 1050)),
            Line.KESA: cizgi((950, 180), (150, 1000)),
            Line.GYAKU_KESA: cizgi((150, 1050), (950, 220)),
            Line.YOKO: cizgi((80, 640), (1000, 640)),
            Line.TSUKI: cizgi((540, 900), (540, 730)),
        }
        for hat, pts in beklenen.items():
            g = classify(pts, W, H, R)
            self.assertIsNotNone(g.action, hat.value)
            self.assertIs(g.action.type, ActionType.CUT)
            self.assertIs(g.action.line, hat, f"{hat.value} yanlis taniniyor")

    def test_uzunluk_baglilik_kademesini_belirler(self):
        kisa = classify(cizgi((540, 500), (540, 800)), W, H, R)
        orta = classify(cizgi((540, 380), (540, 1000)), W, H, R)
        uzun = classify(cizgi((540, 150), (540, 1150)), W, H, R)
        self.assertEqual(kisa.tier, Tier.FEINT)
        self.assertEqual(orta.tier, Tier.FAST)
        self.assertEqual(uzun.tier, Tier.HEAVY)
        self.assertTrue(uzun.action.heavy)
        self.assertFalse(orta.action.heavy)

    def test_kapali_halka_parrydir(self):
        g = classify(halka(540, 400), W, H, R)
        self.assertIs(g.action.type, ActionType.PARRY)

    def test_alt_serit_durus_secer(self):
        yuvalar = R.all_kamae
        for i, beklenen in enumerate(yuvalar):
            x = W * (i + 0.5) / len(yuvalar)
            g = classify(cizgi((x, 1750), (x + 20, 1780), 6), W, H, R)
            self.assertIs(g.action.type, ActionType.GUARD)
            self.assertIs(g.action.to_kamae, beklenen)

    def test_alt_seritteki_kisa_dokunus_reddedilmez(self):
        # Serit dokunusu bilerek kisadir; uzunluk esigine takilmamali
        g = classify(cizgi((540, 1800), (548, 1806), 4), W, H, R)
        self.assertIsNotNone(g.action)

    def test_cok_kisa_cizgi_reddedilir(self):
        self.assertIsNone(classify(cizgi((540, 500), (545, 505), 4), W, H, R).action)
        self.assertIsNone(classify([(1.0, 1.0)], W, H, R).action)

    def test_agir_kesim_ulasilabilir_olmali(self):
        # Referans kosegen olsaydi AGIR pratikte cizilemezdi. Ekranin dikey
        # kesim alanini bastan asagi kat eden bir cizgi AGIR sayilmali.
        g = classify(cizgi((540, 60), (540, 1200)), W, H, R)
        self.assertEqual(g.tier, Tier.HEAVY)


class TestNiyetKurulumu(unittest.TestCase):
    def test_yarim_cizgi_ardindan_baskasi_gelirse_feint_olur(self):
        yalan = classify(cizgi((540, 500), (540, 800)), W, H, R)
        gercek = classify(cizgi((80, 640), (1000, 640)), W, H, R)
        niyet = assemble_intent([yalan, gercek])
        self.assertIs(niyet[0].type, ActionType.FEINT)
        self.assertIs(niyet[1].type, ActionType.CUT)

    def test_tek_basina_yarim_cizgi_sadece_zayif_kesimdir(self):
        yalan = classify(cizgi((540, 500), (540, 800)), W, H, R)
        niyet = assemble_intent([yalan])
        self.assertIs(niyet[0].type, ActionType.CUT)
        self.assertFalse(niyet[0].heavy)

    def test_taninmayan_jestler_elenir(self):
        self.assertEqual(assemble_intent([Gesture(None), Gesture(None)]), [])


if __name__ == "__main__":
    unittest.main()
