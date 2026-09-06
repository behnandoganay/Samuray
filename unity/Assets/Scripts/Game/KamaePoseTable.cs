using System;
using UnityEngine;
using Samuray.Core;

namespace Samuray.Game
{
    /// <summary>Durus -> vucut ve kilic konumu.
    ///
    /// Oyunun yarisi bu tabloda: durus okunan bir kelime degil, gorulen bir
    /// kilic acisi. ScriptableObject olmasinin sebebi Inspector'dan
    /// ayarlanabilmesi.
    ///
    /// Koordinatlar savascinin yerel uzayinda, Y YUKARI, ayaklar y=0'da.
    /// Figur +X yonune (dusmana) bakiyor kabul edilir; ters bakan savasci icin
    /// X aynalanir. Referans yukseklikler: kalca ~1.0, omuz ~1.75, bas ~2.15.
    /// </summary>
    [CreateAssetMenu(fileName = "KamaePoseTable", menuName = "Samuray/Kamae Poz Tablosu")]
    public sealed class KamaePoseTable : ScriptableObject
    {
        [Serializable]
        public sealed class Pose
        {
            public Kamae kamae;
            [Header("Kilic")]
            [Tooltip("Kabza - on el burayi tutar")] public Vector2 hilt;
            [Tooltip("Kilicin ucu")] public Vector2 tip;
            [Header("Vucut")]
            [Tooltip("Govde one egimi, derece (+ ileri)")] public float torsoLean;
            [Tooltip("Ayaklarin acikligi")] public float stanceWidth = 0.62f;
            [Tooltip("Kalca yuksekligi - dusuk deger = daha cokuk durus")]
            public float hipHeight = 1.0f;
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
        /// Gercek kenjutsu duruslarina gore elle ayarlandi.</summary>
        public static KamaePoseTable CreateDefault()
        {
            var t = CreateInstance<KamaePoseTable>();
            t.poses = new[]
            {
                // Kilic tepede, arkaya yatik - indirmeye hazir
                new Pose { kamae = Kamae.JODAN,  hilt = new Vector2( 0.16f, 2.18f), tip = new Vector2(-0.58f, 2.88f),
                           torsoLean =  4f, stanceWidth = 0.66f, hipHeight = 1.00f },
                // Uc rakibin bogazinda - en dengeli gard
                new Pose { kamae = Kamae.CHUDAN, hilt = new Vector2( 0.45f, 1.46f), tip = new Vector2( 1.56f, 1.76f),
                           torsoLean =  6f, stanceWidth = 0.62f, hipHeight = 0.98f },
                // Uc asagida, yukari kesmeye hazir
                new Pose { kamae = Kamae.GEDAN,  hilt = new Vector2( 0.42f, 1.18f), tip = new Vector2( 1.48f, 0.72f),
                           torsoLean =  8f, stanceWidth = 0.72f, hipHeight = 0.90f },
                // Kilic omuz yaninda dik
                new Pose { kamae = Kamae.HASSO,  hilt = new Vector2( 0.22f, 1.92f), tip = new Vector2( 0.12f, 3.02f),
                           torsoLean =  2f, stanceWidth = 0.60f, hipHeight = 1.02f },
                // Kilic arkada gizli - rakip boyunu goremez
                new Pose { kamae = Kamae.WAKI,   hilt = new Vector2(-0.12f, 1.26f), tip = new Vector2(-1.22f, 0.86f),
                           torsoLean = 12f, stanceWidth = 0.70f, hipHeight = 0.94f },
            };
            return t;
        }
    }
}
