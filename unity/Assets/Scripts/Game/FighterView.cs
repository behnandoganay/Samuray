using UnityEngine;
using Samuray.Core;

namespace Samuray.Game
{
    /// <summary>Bir savascinin gorunusu: siluet + kilic.
    ///
    /// Tek isi Core'daki Fighter durumunu gostermek; hicbir kural bilgisi tutmaz.
    /// Kilicin acisi KamaePoseTable'dan gelir ve duruslar arasinda yumusak gecer.
    /// </summary>
    public sealed class FighterView : MonoBehaviour
    {
        [SerializeField] KamaePoseTable poseTable;
        [Tooltip("Oyuncu +1 (yukari bakar), dusman -1 (asagi bakar)")]
        [SerializeField] int facing = 1;
        [SerializeField] float bodyScale = 1.5f;
        [SerializeField] float poseLerpSpeed = 9f;

        [SerializeField] SpriteRenderer body, head, blade, hilt;
        [SerializeField] SpriteRenderer[] woundMarks;

        Kamae _target = Kamae.CHUDAN;
        Vector2 _hilt, _tip;      // su anki (yumusatilmis) kilic konumu
        bool _snapped;

        public Kamae CurrentKamae => _target;
        public int Facing => facing;
        public float Scale => bodyScale;

        void LateUpdate()
        {
            var p = poseTable != null ? poseTable.For(_target) : null;
            if (p == null) return;
            float t = _snapped ? 1f : 1f - Mathf.Exp(-poseLerpSpeed * Time.deltaTime);
            _hilt = Vector2.Lerp(_hilt, p.hilt, t);
            _tip  = Vector2.Lerp(_tip,  p.tip,  t);
            _snapped = false;
            ApplyBlade();
        }

        /// <summary>Durusu degistir. instant=true ilk kurulumda kullanilir.</summary>
        public void SetKamae(Kamae k, bool instant = false)
        {
            _target = k;
            if (!instant) return;
            var p = poseTable != null ? poseTable.For(k) : null;
            if (p == null) return;
            _hilt = p.hilt; _tip = p.tip; _snapped = true;
            ApplyBlade();
        }

        public void SetWounds(int wounds)
        {
            if (woundMarks == null) return;
            for (int i = 0; i < woundMarks.Length; i++)
                if (woundMarks[i] != null) woundMarks[i].enabled = i < wounds;
        }

        /// <summary>Kilicin dunya uzerindeki ucu - animasyonun hedef aldigi nokta.</summary>
        public Vector3 BladeTipWorld()
            => transform.TransformPoint(new Vector3(_tip.x, _tip.y * facing, 0f) * bodyScale);

        void ApplyBlade()
        {
            if (blade == null) return;
            Vector2 h = new Vector2(_hilt.x, _hilt.y * facing) * bodyScale;
            Vector2 t = new Vector2(_tip.x,  _tip.y  * facing) * bodyScale;
            Vector2 d = t - h;
            float len = d.magnitude;
            blade.transform.localPosition = (h + t) * 0.5f;
            blade.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
            blade.transform.localScale = new Vector3(len, 0.075f * bodyScale, 1f);
            if (hilt != null)
            {
                hilt.transform.localPosition = h;
                hilt.transform.localScale = Vector3.one * (0.20f * bodyScale);
            }
        }

        /// <summary>Sahne kurucusunun cagirdigi fabrika. Govde parcalarini olusturur.</summary>
        public static FighterView Create(Transform parent, string name, Vector3 pos,
                                         int facing, float scale, KamaePoseTable table)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var v = go.AddComponent<FighterView>();
            v.facing = facing;
            v.bodyScale = scale;
            v.poseTable = table;

            v.body = Art.Piece(go.transform, "Body", Art.Rect(), Art.Ink, 5);
            v.body.transform.localPosition = new Vector3(0f, -0.05f * scale, 0f);
            v.body.transform.localScale = new Vector3(0.62f * scale, 2.15f * scale, 1f);

            v.head = Art.Piece(go.transform, "Head", Art.Circle(), Art.Ink, 6);
            v.head.transform.localPosition = new Vector3(0f, 1.30f * scale, 0f);
            v.head.transform.localScale = Vector3.one * (0.55f * scale);

            v.blade = Art.Piece(go.transform, "Blade", Art.Rect(), Art.Ink, 8);
            v.hilt  = Art.Piece(go.transform, "Hilt",  Art.Circle(), Art.Ink, 9);

            var marks = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                marks[i] = Art.Piece(go.transform, "Wound" + (i + 1), Art.Circle(), Art.Shu, 7);
                marks[i].transform.localPosition =
                    new Vector3((-0.16f + i * 0.18f) * scale, (0.55f - i * 0.45f) * scale, 0f);
                marks[i].transform.localScale = Vector3.one * (0.30f * scale);
                marks[i].enabled = false;
            }
            v.woundMarks = marks;

            v.SetKamae(Kamae.CHUDAN, true);
            return v;
        }
    }
}
