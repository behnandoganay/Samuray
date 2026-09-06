using UnityEngine;
using Samuray.Core;

namespace Samuray.Game
{
    /// <summary>Bir savascinin gorunusu. Rig'i besler, duruslar arasinda yumusatir.
    ///
    /// Hicbir kural bilgisi tutmaz; sadece Core'daki Fighter durumunu gosterir.
    /// </summary>
    public sealed class FighterView : MonoBehaviour
    {
        [SerializeField] KamaePoseTable poseTable;
        [Tooltip("+1 saga bakar (oyuncu), -1 sola bakar (dusman)")]
        [SerializeField] int facing = 1;
        [SerializeField] float bodyScale = 1.0f;
        [SerializeField] float poseLerpSpeed = 9f;
        [SerializeField] FighterRig rig;

        struct P
        {
            public Vector2 hilt, tip;
            public float lean, stance, hip;
            public static P From(KamaePoseTable.Pose p) => new P
            { hilt = p.hilt, tip = p.tip, lean = p.torsoLean, stance = p.stanceWidth, hip = p.hipHeight };
            public static P Lerp(P a, P b, float t) => new P
            {
                hilt = Vector2.Lerp(a.hilt, b.hilt, t),
                tip = Vector2.Lerp(a.tip, b.tip, t),
                lean = Mathf.Lerp(a.lean, b.lean, t),
                stance = Mathf.Lerp(a.stance, b.stance, t),
                hip = Mathf.Lerp(a.hip, b.hip, t)
            };
        }

        Kamae _target = Kamae.CHUDAN;
        P _current;
        bool _ready;

        /// <summary>Animasyonun gecici olarak dayattigi poz (wind-up, savurma).
        /// null ise durus poziyla surulur.</summary>
        public Kamae? OverrideKamae { get; set; }
        public float OverrideBlend { get; set; }

        public FighterRig Rig => rig;
        public Kamae CurrentKamae => _target;
        public int Facing => facing;

        void LateUpdate()
        {
            if (rig == null || poseTable == null) return;
            var basePose = poseTable.For(_target);
            if (basePose == null) return;

            var goal = P.From(basePose);
            if (OverrideKamae.HasValue)
            {
                var o = poseTable.For(OverrideKamae.Value);
                if (o != null) goal = P.Lerp(goal, P.From(o), Mathf.Clamp01(OverrideBlend));
            }

            float t = _ready ? 1f - Mathf.Exp(-poseLerpSpeed * Time.deltaTime) : 1f;
            _current = _ready ? P.Lerp(_current, goal, t) : goal;
            _ready = true;
            rig.Apply(_current.hilt, _current.tip, _current.lean, _current.stance, _current.hip);
        }

        public void SetKamae(Kamae k, bool instant = false)
        {
            _target = k;
            if (instant) _ready = false;
        }

        public void SetWounds(int wounds) => rig?.SetWounds(wounds);

        /// <summary>Kilicin ucu, dunya uzayinda.</summary>
        public Vector3 BladeTipWorld() => rig != null ? rig.ToWorld(rig.TipLocal) : transform.position;

        public static FighterView Create(Transform parent, string name, Vector3 pos,
                                         int facing, float scale, KamaePoseTable table)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            var v = go.AddComponent<FighterView>();
            v.facing = facing; v.bodyScale = scale; v.poseTable = table;

            var rigGo = new GameObject("Rig");
            rigGo.transform.SetParent(go.transform, false);
            var r = rigGo.AddComponent<FighterRig>();
            r.Configure(facing, scale);
            r.Build();
            v.rig = r;

            v.SetKamae(Kamae.CHUDAN, true);
            return v;
        }
    }
}
