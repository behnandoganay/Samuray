using System;
using UnityEngine;
using Samuray.Core;

namespace Samuray.Game
{
    /// <summary>Durus -> kilic konumu tablosu.
    ///
    /// Kod icinde sabit dizi yerine ScriptableObject: kilic acilarini Inspector'dan
    /// oynayarak ayarlayabilmek icin. Oyunun yarisi bu tabloda - durus artik
    /// okunan bir kelime degil, gorulen bir kilic acisi.
    ///
    /// Koordinatlar savascinin olcegi cinsinden, Y YUKARI dogru. Oyuncu dusmana
    /// (yukari) bakar; dusman icin Y aynalanir.
    /// </summary>
    [CreateAssetMenu(fileName = "KamaePoseTable", menuName = "Samuray/Kamae Poz Tablosu")]
    public sealed class KamaePoseTable : ScriptableObject
    {
        [Serializable]
        public sealed class Pose
        {
            public Kamae kamae;
            [Tooltip("Kilicin tutuldugu nokta")] public Vector2 hilt;
            [Tooltip("Kilicin ucu")] public Vector2 tip;
        }

        [SerializeField] Pose[] poses;

        public Pose For(Kamae k)
        {
            if (poses != null)
                foreach (var p in poses)
                    if (p.kamae == k) return p;
            return null;
        }

        /// <summary>Sahne kurucusunun kullandigi varsayilan tablo.
        /// Degerler web prototipinden alindi, orada elde denenmisti.</summary>
        public static KamaePoseTable CreateDefault()
        {
            var t = CreateInstance<KamaePoseTable>();
            t.poses = new[]
            {
                new Pose { kamae = Kamae.JODAN,  hilt = new Vector2( 0.30f,  0.52f), tip = new Vector2(-0.38f, 1.88f) },
                new Pose { kamae = Kamae.CHUDAN, hilt = new Vector2( 0.32f, -0.08f), tip = new Vector2( 0.05f, 1.30f) },
                new Pose { kamae = Kamae.GEDAN,  hilt = new Vector2( 0.32f, -0.34f), tip = new Vector2( 0.12f, 0.46f) },
                new Pose { kamae = Kamae.HASSO,  hilt = new Vector2( 0.58f,  0.28f), tip = new Vector2( 0.80f, 1.72f) },
                new Pose { kamae = Kamae.WAKI,   hilt = new Vector2( 0.46f, -0.30f), tip = new Vector2( 1.12f,-1.02f) },
            };
            return t;
        }
    }
}
