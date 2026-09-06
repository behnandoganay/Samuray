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

        /// <summary>Durusun pozu. Asset bos veya bayatsa yerlesik varsayilana duser.
        ///
        /// Bu geri dusus onemli: eski semayla serilestirilmis bir asset'te poses
        /// dizisi bos gelebiliyor ve o zaman figur hic pozlanmiyordu - butun
        /// parcalar varsayilan olcekte ust uste yigiliyordu. Tablo artik yalnizca
        /// AYAR icin; dogruluk ona bagli degil.
        /// </summary>
        public Pose For(Kamae k)
        {
            if (poses != null)
                foreach (var p in poses)
                    if (p != null && p.kamae == k && p.hipHeight > 0.01f) return p;
            return Builtin(k);
        }

        /// <summary>Asset'te veri yoksa kullanilan yerlesik pozlar.</summary>
        public static Pose Builtin(Kamae k)
        {
            if (_builtin == null)
            {
                _builtin = new System.Collections.Generic.Dictionary<Kamae, Pose>();
                foreach (var p in DefaultPoses()) _builtin[p.kamae] = p;
            }
            return _builtin.TryGetValue(k, out var found) ? found : _builtin[Kamae.CHUDAN];
        }
        static System.Collections.Generic.Dictionary<Kamae, Pose> _builtin;

        /// <summary>Asset'i varsayilanlarla doldurur (bos veya bayatsa).</summary>
        public bool EnsurePopulated()
        {
            bool eksik = poses == null || poses.Length == 0;
            if (!eksik)
                foreach (Kamae k in System.Enum.GetValues(typeof(Kamae)))
                {
                    bool var_ = false;
                    foreach (var p in poses) if (p != null && p.kamae == k && p.hipHeight > 0.01f) var_ = true;
                    if (!var_) { eksik = true; break; }
                }
            if (!eksik) return false;
            poses = DefaultPoses();
            return true;
        }

        /// <summary>Sahne kurucusunun kullandigi varsayilan tablo.
        /// Gercek kenjutsu duruslarina gore elle ayarlandi.</summary>
        public static KamaePoseTable CreateDefault()
        {
            var t = CreateInstance<KamaePoseTable>();
            t.poses = DefaultPoses();
            return t;
        }

        static Pose[] DefaultPoses()
        {
            return new[]
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
        }
    }
}
