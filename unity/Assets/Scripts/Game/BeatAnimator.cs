using System.Collections.Generic;
using UnityEngine;
using Samuray.Core;

namespace Samuray.Game
{
    /// <summary>Tur cozumlemesinin animasyonu: savurma, temas, hit-stop, sarsinti.
    ///
    /// Zamanlama degerleri Inspector'da acik - vurus hissi tam olarak burada
    /// ayarlaniyor. Web prototipinde elde denenmis degerlerle basliyor.
    ///
    /// Hit-stop icin Time.timeScale KULLANILMIYOR: timeScale HUD'u ve sayaci da
    /// dondururdu. Kendi saatimizi tutuyoruz, donma yalnizca bu animasyonu
    /// etkiliyor.
    /// </summary>
    public sealed class BeatAnimator : MonoBehaviour
    {
        public sealed class Spec
        {
            public readonly List<(CutLine line, bool heavy, bool mine)> Cuts = new List<(CutLine, bool, bool)>();
            public bool Clash, Parried, Lethal;
            public int DamageToFoe, DamageToMe;
            public Vector3 FoePos, MePos;
        }

        [Header("Zamanlama (saniye)")]
        [Tooltip("Geri cekilme - darbeden onceki nefes")]
        [SerializeField] float windup = 0.17f;
        [Tooltip("Savurma suresi")]
        [SerializeField] float strike = 0.34f;
        [Tooltip("Savurmanin kacinci aninda temas oluyor (0-1)")]
        [Range(0.2f, 0.95f)][SerializeField] float contactAt = 0.68f;
        [Tooltip("Takip - kiliclarin durulmasi")]
        [SerializeField] float follow = 0.33f;

        [Header("Hit-stop (saniye)")]
        [SerializeField] float hitStopNormal = 0.10f;
        [SerializeField] float hitStopLethal = 0.19f;
        [SerializeField] float hitStopClash = 0.15f;

        [Header("Sarsinti (dunya birimi)")]
        [SerializeField] float shakePerWound = 0.22f;
        [SerializeField] float shakeBase = 0.28f;
        [SerializeField] float shakeClash = 0.55f;

        [Header("Gorunum")]
        [SerializeField] float arenaRadius = 3.4f;
        [SerializeField] float meRadius = 2.2f;
        [SerializeField] float sweepWidth = 0.26f;
        [Tooltip("Firca izinin hattin ne kadarini kaplamasi")]
        [SerializeField] float sweepTail = 0.42f;

        [Header("Referanslar")]
        [SerializeField] CameraShake shake;
        [SerializeField] InkSplatter splatter;

        readonly List<LineRenderer> _sweeps = new List<LineRenderer>();
        Spec _spec;
        System.Action _onContact, _onComplete;
        float _clock, _hitStop;
        bool _contactFired;

        public bool Playing { get; private set; }

        void Awake()
        {
            for (int i = 0; i < 6; i++)
            {
                var go = new GameObject("Sweep" + i);
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.numCapVertices = 4;
                lr.material = Art.LineMaterial();
                lr.sortingOrder = 12;
                lr.enabled = false;
                _sweeps.Add(lr);
            }
        }

        public void Play(Spec spec, System.Action onContact, System.Action onComplete)
        {
            _spec = spec;
            _onContact = onContact;
            _onComplete = onComplete;
            _clock = 0f; _hitStop = 0f; _contactFired = false;
            Playing = true;
            foreach (var lr in _sweeps) lr.enabled = false;
        }

        public float TotalSeconds => windup + strike + follow;

        void Update()
        {
            if (!Playing) return;

            // Hit-stop: saat durur, ekran donar. Sarsinti ve sicrama devam eder.
            if (_hitStop > 0f) { _hitStop -= Time.deltaTime; DrawSweeps(SweepProgress()); return; }

            _clock += Time.deltaTime;
            float p = SweepProgress();
            DrawSweeps(p);

            if (!_contactFired && _clock >= windup + strike * contactAt)
            {
                _contactFired = true;
                FireContact();
            }

            if (_clock >= TotalSeconds)
            {
                Playing = false;
                foreach (var lr in _sweeps) lr.enabled = false;
                _onComplete?.Invoke();
            }
        }

        /// <summary>Savurmanin ilerlemesi 0..1.4 (takip fazinda hattin ucundan tasar).</summary>
        float SweepProgress()
        {
            if (_clock < windup) return 0f;
            if (_clock < windup + strike) return (_clock - windup) / strike;
            return 1f + Mathf.Min(0.45f, (_clock - windup - strike) / Mathf.Max(0.0001f, follow) * 0.45f);
        }

        void DrawSweeps(float p)
        {
            if (_spec == null) return;
            int i = 0;
            foreach (var c in _spec.Cuts)
            {
                if (i >= _sweeps.Count) break;
                var lr = _sweeps[i++];
                if (p <= 0f) { lr.enabled = false; continue; }

                var center = c.mine ? _spec.FoePos : _spec.MePos;
                var radius = c.mine ? arenaRadius : meRadius;
                var (a, b) = ArenaView.SegmentAt(center, radius, c.line);

                float head = Mathf.Clamp01(p);
                float tail = Mathf.Clamp01(p - sweepTail);
                lr.enabled = true;
                lr.SetPosition(0, Vector3.Lerp(a, b, tail));
                lr.SetPosition(1, Vector3.Lerp(a, b, head));
                float w = sweepWidth * (c.heavy ? 1.6f : 1f);
                lr.startWidth = w * 0.35f;
                lr.endWidth = w;
                var col = c.mine ? Art.Ink : Art.Shu;
                col.a = Mathf.Clamp01(1.6f - p * 0.6f);
                lr.startColor = lr.endColor = col;
            }
            for (; i < _sweeps.Count; i++) _sweeps[i].enabled = false;
        }

        void FireContact()
        {
            var s = _spec;

            if (s.Clash || s.Parried)
            {
                shake?.Shake(shakeClash);
                _hitStop = hitStopClash;
            }
            if (s.DamageToFoe > 0)
            {
                splatter?.Burst(s.FoePos, 10 + s.DamageToFoe * 7, 0.8f + s.DamageToFoe * 0.35f);
                shake?.Shake(shakeBase + s.DamageToFoe * shakePerWound);
                _hitStop = Mathf.Max(_hitStop, s.Lethal ? hitStopLethal : hitStopNormal);
            }
            if (s.DamageToMe > 0)
            {
                splatter?.Burst(s.MePos, 10 + s.DamageToMe * 7, 0.8f + s.DamageToMe * 0.35f);
                shake?.Shake(shakeBase + s.DamageToMe * shakePerWound * 1.3f);
                _hitStop = Mathf.Max(_hitStop, s.Lethal ? hitStopLethal : hitStopNormal);
            }
            _onContact?.Invoke();
        }

        public void ClearEffects()
        {
            splatter?.ClearAll();
            foreach (var lr in _sweeps) lr.enabled = false;
            Playing = false;
        }

        public static BeatAnimator Create(Transform parent, CameraShake camShake,
                                          InkSplatter ink, float arenaR, float meR)
        {
            var go = new GameObject("BeatAnimator");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<BeatAnimator>();
            a.shake = camShake; a.splatter = ink;
            a.arenaRadius = arenaR; a.meRadius = meR;
            return a;
        }
    }
}
