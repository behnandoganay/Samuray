using UnityEngine;

namespace Samuray.Game
{
    /// <summary>Yandan profil savasci: prosedürel uzuvlar ve iki kemikli IK.
    ///
    /// Neden prosedürel? Sanat varligi yok ve olmadan da duruslarin okunmasi
    /// gerekiyor. Her uzuv iki nokta arasina cizilen bir kapsül; eklem konumlari
    /// her karede duruş verisinden hesaplaniyor.
    ///
    /// Kollar IK ile kilici TAKIP EDER. Yani her durus icin on iki eklem acisi
    /// yazmiyoruz - sadece kilicin kabza/uc konumunu veriyoruz, kollar oraya
    /// uzaniyor. Yazilacak veri ucte birine dusuyor ve kollar hicbir zaman
    /// kilictan kopuk gorunmuyor.
    ///
    /// Yerel uzay: Y YUKARI, ayaklar y=0, figur +X yonune bakar. Ters bakan
    /// savasci icin X aynalanir (facing = -1).
    /// </summary>
    public sealed class FighterRig : MonoBehaviour
    {
        [Header("Oranlar (yerel birim)")]
        [SerializeField] float shoulderHeight = 0.76f;   // kalcadan omuza
        [SerializeField] float headRadius = 0.20f;
        [SerializeField] float upperArm = 0.42f;
        [SerializeField] float foreArm = 0.40f;
        [SerializeField] float thigh = 0.54f;
        [SerializeField] float shin = 0.52f;

        [Header("Kalinliklar")]
        [SerializeField] float torsoWidth = 0.34f;
        [SerializeField] float armWidth = 0.13f;
        [SerializeField] float legWidth = 0.17f;
        [SerializeField] float bladeWidth = 0.055f;

        [Header("Kavrama")]
        [Tooltip("Arka elin kabzadan ne kadar geride tuttugu - iki elli kavrama")]
        [SerializeField] float backHandOffset = 0.22f;

        SpriteRenderer _torso, _head, _blade, _guard;
        SpriteRenderer _armBackUpper, _armBackFore, _armFrontUpper, _armFrontFore;
        SpriteRenderer _legBackThigh, _legBackShin, _legFrontThigh, _legFrontShin;
        SpriteRenderer[] _wounds;

        int _facing = 1;
        float _scale = 1f;

        /// <summary>Su anki poz - BeatAnimator ve overlay'ler buradan okur.</summary>
        public Vector2 HiltLocal { get; private set; }
        public Vector2 TipLocal { get; private set; }
        public float ShoulderY => shoulderHeight;

        public int Facing => _facing;
        public float RigScale => _scale;

        /// <summary>Yerel noktayi dunya uzayina cevirir (aynalamayi hesaba katar).</summary>
        public Vector3 ToWorld(Vector2 local)
            => transform.TransformPoint(new Vector3(local.x * _facing, local.y, 0f));

        public void Configure(int facing, float scale)
        {
            _facing = facing < 0 ? -1 : 1;
            _scale = scale;
        }

        /// <summary>Poz uygula. Butun eklem konumlari buradan hesaplanir.</summary>
        public void Apply(Vector2 hilt, Vector2 tip, float torsoLean, float stanceWidth, float hipHeight)
        {
            HiltLocal = hilt; TipLocal = tip;

            // --- govde ---
            var hip = new Vector2(0f, hipHeight);
            float leanRad = -torsoLean * Mathf.Deg2Rad;      // + deger one egilme
            var up = new Vector2(Mathf.Sin(leanRad), Mathf.Cos(leanRad));
            var shoulder = hip + up * shoulderHeight;
            var head = shoulder + up * (headRadius + 0.16f);

            Bone(_torso, hip, shoulder, torsoWidth);
            Dot(_head, head, headRadius * 2f);

            // --- bacaklar: ayaklar sabit, dizler IK ile ---
            var footFront = new Vector2(stanceWidth * 0.55f, 0f);
            var footBack = new Vector2(-stanceWidth * 0.45f, 0f);
            var kneeFront = TwoBone(hip, footFront, thigh, shin, +1f);
            var kneeBack = TwoBone(hip, footBack, thigh, shin, +1f);
            Bone(_legFrontThigh, hip, kneeFront, legWidth);
            Bone(_legFrontShin, kneeFront, footFront, legWidth * 0.85f);
            Bone(_legBackThigh, hip, kneeBack, legWidth);
            Bone(_legBackShin, kneeBack, footBack, legWidth * 0.85f);

            // --- kilic ---
            Bone(_blade, hilt, tip, bladeWidth);
            Dot(_guard, hilt, 0.13f);

            // --- kollar: iki elli kavrama, IK ile kabzayi tutar ---
            var bladeDir = (tip - hilt).normalized;
            var handFront = hilt;
            var handBack = hilt - bladeDir * backHandOffset;

            var elbowFront = TwoBone(shoulder, handFront, upperArm, foreArm, -1f);
            var elbowBack = TwoBone(shoulder, handBack, upperArm, foreArm, -1f);
            Bone(_armFrontUpper, shoulder, elbowFront, armWidth);
            Bone(_armFrontFore, elbowFront, handFront, armWidth * 0.88f);
            Bone(_armBackUpper, shoulder, elbowBack, armWidth);
            Bone(_armBackFore, elbowBack, handBack, armWidth * 0.88f);
        }

        public void SetWounds(int count)
        {
            if (_wounds == null) return;
            for (int i = 0; i < _wounds.Length; i++)
                if (_wounds[i] != null) _wounds[i].enabled = i < count;
        }

        // ---------------- yardimcilar ----------------

        /// <summary>Iki kemikli IK: koke bagli, hedefe uzanan eklem noktasi.
        /// bend isareti dirsegin/dizin hangi yone kirilacagini secer.</summary>
        static Vector2 TwoBone(Vector2 root, Vector2 target, float l1, float l2, float bend)
        {
            var delta = target - root;
            float d = Mathf.Clamp(delta.magnitude, Mathf.Abs(l1 - l2) + 0.001f, l1 + l2 - 0.001f);
            var dir = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.up;
            float a = (l1 * l1 - l2 * l2 + d * d) / (2f * d);
            float h = Mathf.Sqrt(Mathf.Max(0f, l1 * l1 - a * a));
            var perp = new Vector2(-dir.y, dir.x) * bend;
            return root + dir * a + perp * h;
        }

        void Bone(SpriteRenderer sr, Vector2 a, Vector2 b, float width)
        {
            if (sr == null) return;
            var am = new Vector2(a.x * _facing, a.y);
            var bm = new Vector2(b.x * _facing, b.y);
            var d = bm - am;
            float len = Mathf.Max(0.02f, d.magnitude);
            sr.transform.localPosition = (am + bm) * 0.5f;
            sr.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
            sr.transform.localScale = new Vector3(width, len, 1f);
        }

        void Dot(SpriteRenderer sr, Vector2 p, float diameter)
        {
            if (sr == null) return;
            sr.transform.localPosition = new Vector3(p.x * _facing, p.y, 0f);
            sr.transform.localScale = Vector3.one * diameter;
        }

        SpriteRenderer Limb(string name, int order)
            => Art.Piece(transform, name, Art.Capsule(), Art.Ink, order);

        /// <summary>Uzuvlari olusturur. Hepsi ayni murekkep rengi - siluet tek
        /// parca gibi okunuyor, sumi-e dili zaten bu.</summary>
        public void Build()
        {
            _legBackThigh = Limb("LegBackThigh", 1);
            _legBackShin = Limb("LegBackShin", 1);
            _armBackUpper = Limb("ArmBackUpper", 2);
            _armBackFore = Limb("ArmBackFore", 2);
            _torso = Limb("Torso", 3);
            _legFrontThigh = Limb("LegFrontThigh", 4);
            _legFrontShin = Limb("LegFrontShin", 4);
            _head = Art.Piece(transform, "Head", Art.Circle(), Art.Ink, 5);
            _blade = Limb("Blade", 6);
            _guard = Art.Piece(transform, "Tsuba", Art.Circle(), Art.Ink, 7);
            _armFrontUpper = Limb("ArmFrontUpper", 8);
            _armFrontFore = Limb("ArmFrontFore", 8);

            _wounds = new SpriteRenderer[3];
            for (int i = 0; i < 3; i++)
            {
                _wounds[i] = Art.Piece(transform, "Wound" + (i + 1), Art.Circle(), Art.Shu, 9);
                _wounds[i].transform.localPosition =
                    new Vector3((-0.10f + i * 0.11f) * _facing, 1.62f - i * 0.30f, 0f);
                _wounds[i].transform.localScale = Vector3.one * (0.24f + i * 0.04f);
                _wounds[i].enabled = false;
            }
            transform.localScale = Vector3.one * _scale;
        }
    }
}
