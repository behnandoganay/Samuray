using UnityEngine;
using Samuray.Core;

namespace Samuray.Game
{
    /// <summary>Bes kesim hattinin GOVDE UZERINDEKI yerleri.
    ///
    /// Soyut isinlar yerine anatomik konumlar: shomen basin ustunden iner, kesa
    /// omuzdan capraz gecer, yoko belden yatay, tsuki bogaza saplanir. Oyuncu
    /// ne kestigini gorur.
    ///
    /// Koordinatlar savascinin yerel uzayinda (Y yukari, ayaklar y=0, figur +X
    /// yonune bakar). FighterRig.ToWorld ile dunyaya tasinir, boylece hatlar
    /// figurle birlikte olceklenir ve aynalanir.
    /// </summary>
    public static class BodyLines
    {
        public static (Vector2 from, Vector2 to) Local(CutLine line)
        {
            switch (line)
            {
                // basin ustunden dikey asagi, govdeye kadar
                case CutLine.SHOMEN:
                    return (new Vector2(0.06f, 2.78f), new Vector2(0.06f, 1.30f));
                // on omuzdan arka kalcaya inen capraz
                case CutLine.KESA:
                    return (new Vector2(0.52f, 2.18f), new Vector2(-0.38f, 0.98f));
                // arka kalcadan on omuza yukselen capraz
                case CutLine.GYAKU_KESA:
                    return (new Vector2(-0.38f, 0.92f), new Vector2(0.52f, 2.14f));
                // belden yatay
                case CutLine.YOKO:
                    return (new Vector2(-0.52f, 1.34f), new Vector2(0.58f, 1.34f));
                // bogaza dogru kisa saplama - disaridan iceri
                default:
                    return (new Vector2(0.96f, 1.82f), new Vector2(0.20f, 1.80f));
            }
        }

        public static (Vector3 from, Vector3 to) World(FighterRig rig, CutLine line)
        {
            var (a, b) = Local(line);
            return (rig.ToWorld(a), rig.ToWorld(b));
        }
    }
}
