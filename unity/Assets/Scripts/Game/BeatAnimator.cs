using System.Collections.Generic;
using UnityEngine;
using Samuray.Core;

namespace Samuray.Game
{
    /// <summary>Tur cozumlemesinin animasyonu: savurma, temas, hit-stop, sarsinti.
    ///
    /// Zamanlama degerleri Inspector'da acik - vurus hissi tam olarak burada
    /// ayarlaniyor.
    ///
    /// Hit-stop icin Time.timeScale KULLANILMIYOR: o HUD'u ve sayaci da
    /// dondururdu. Kendi saatimiz var, donma yalnizca bu animasyonu etkiliyor.
    /// </summary>
    public sealed class BeatAnimator : MonoBehaviour
    {
        public sealed class Spec
        {
            public readonly List<(CutLine line, bool heavy, bool mine)> Cuts = new List<(CutLine, bool, bool)>();
            public bool Clash, Parried, Lethal;
            public int DamageToFoe, DamageToMe;
            public Kamae MyEndKamae, FoeEndKamae;
        }

        [Header("Zamanlama (saniye)")]
        [SerializeField] float windup = 0.17f;
        [SerializeField] float strike = 0.34f;
        [Range(0.2f, 0.95f)][SerializeField] float contactAt = 0.68f;
        [SerializeField] float follow = 0.33f;

        [Header("Hit-stop (saniye)")]
        [SerializeField] float hitStopNormal = 0.10f;
        [SerializeField] float hitStopLethal = 0.19f;
        [SerializeField] float hitStopClash = 0.15f;

        [Header("Sarsinti (dunya birimi)")]
        [SerializeField] float shakePerWound = 0.22f;
        [SerializeField] float shakeBase = 0.28f;
        [SerializeField] float shakeClash = 0.55f;

        [Header("Firca izi")]
        [SerializeField] float sweepWidth = 0.14f;
        [SerializeField] float sweepTail = 0.42f;

        [Header("Referanslar")]
        [SerializeField] CameraShake shake;
        [SerializeField] InkSplatter splatter;
        [SerializeField] DuelAudio audioBank;
        [SerializeField] FighterView meView, foeView;

        readonly List<LineRenderer> _sweeps = new List<LineRenderer>();
        Spec _spec;
        System.Action _onContact, _onComplete;
        float _clock, _hitStop;
        bool _contactFired;

        public bool Playing { get; private set; }
        public float TotalSeconds => windup + strike + follow;

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
                lr.sortingOrder = 14;
                lr.enabled = false;
                _sweeps.Add(lr);
            }
        }

        public void Play(Spec spec, System.Action onContact, System.Action onComplete)
        {
            _spec = spec; _onContact = onContact; _onComplete = onComplete;
            _clock = 0f; _hitStop = 0f; _contactFired = false;
            Playing = true;
            foreach (var lr in _sweeps) lr.enabled = false;

            bool anyHeavy = false, anyCut = false;
            foreach (var c in spec.Cuts) { anyCut = true; anyHeavy |= c.heavy; }
            if (anyCut) audioBank?.PlaySwing(anyHeavy);
        }

        void Update()
        {
            if (!Playing) return;

            if (_hitStop > 0f) { _hitStop -= Time.deltaTime; Render(); return; }

            _clock += Time.deltaTime;
            Render();

            if (!_contactFired && _clock >= windup + strike * contactAt)
            {
                _contactFired = true;
                FireContact();
            }

            if (_clock >= TotalSeconds)
            {
                Playing = false;
                foreach (var lr in _sweeps) lr.enabled = false;
                ClearPoseOverride();
                _onComplete?.Invoke();
            }
        }

        float SweepProgress()
        {
            if (_clock < windup) return 0f;
            if (_clock < windup + strike) return (_clock - windup) / strike;
            return 1f + Mathf.Min(0.45f, (_clock - windup - strike) / Mathf.Max(0.0001f, follow) * 0.45f);
        }

        void Render()
        {
            if (_spec == null) return;
            float p = SweepProgress();

            // Savascilar savurma boyunca bitis duruslarina dogru kayar - kilic
            // gercekten hareket ediyor gibi gorunsun diye.
            float blend = Mathf.Clamp01(p);
            if (meView != null) { meView.OverrideKamae = _spec.MyEndKamae; meView.OverrideBlend = blend; }
            if (foeView != null) { foeView.OverrideKamae = _spec.FoeEndKamae; foeView.OverrideBlend = blend; }

            int i = 0;
            foreach (var c in _spec.Cuts)
            {
                if (i >= _sweeps.Count) break;
                var lr = _sweeps[i++];
                if (p <= 0f) { lr.enabled = false; continue; }

                // Kesim HEDEFIN govdesi uzerinde oynatilir
                var target = c.mine ? foeView : meView;
                if (target == null || target.Rig == null) { lr.enabled = false; continue; }
                var (a, b) = BodyLines.World(target.Rig, c.line);

                lr.enabled = true;
                lr.SetPosition(0, Vector3.Lerp(a, b, Mathf.Clamp01(p - sweepTail)));
                lr.SetPosition(1, Vector3.Lerp(a, b, Mathf.Clamp01(p)));
                float w = sweepWidth * (c.heavy ? 1.6f : 1f);
                lr.startWidth = w * 0.3f;
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
            bool heavy = false;
            foreach (var c in s.Cuts) heavy |= c.heavy;

            if (s.Clash || s.Parried)
            {
                audioBank?.PlaySteel();
                shake?.Shake(shakeClash);
                _hitStop = hitStopClash;
            }
            if (s.DamageToFoe > 0 && foeView != null)
            {
                var at = foeView.Rig != null ? foeView.Rig.ToWorld(new Vector2(0f, 1.5f)) : foeView.transform.position;
                splatter?.Burst(at, 10 + s.DamageToFoe * 7, 0.8f + s.DamageToFoe * 0.35f);
                shake?.Shake(shakeBase + s.DamageToFoe * shakePerWound);
                audioBank?.PlayHit(heavy, 0.7f + s.DamageToFoe * 0.3f);
                _hitStop = Mathf.Max(_hitStop, s.Lethal ? hitStopLethal : hitStopNormal);
            }
            if (s.DamageToMe > 0 && meView != null)
            {
                var at = meView.Rig != null ? meView.Rig.ToWorld(new Vector2(0f, 1.5f)) : meView.transform.position;
                splatter?.Burst(at, 10 + s.DamageToMe * 7, 0.8f + s.DamageToMe * 0.35f);
                shake?.Shake(shakeBase + s.DamageToMe * shakePerWound * 1.3f);
                audioBank?.PlayHit(heavy, 0.7f + s.DamageToMe * 0.3f);
                _hitStop = Mathf.Max(_hitStop, s.Lethal ? hitStopLethal : hitStopNormal);
            }
            _onContact?.Invoke();
        }

        void ClearPoseOverride()
        {
            if (meView != null) { meView.OverrideKamae = null; meView.OverrideBlend = 0f; }
            if (foeView != null) { foeView.OverrideKamae = null; foeView.OverrideBlend = 0f; }
        }

        public void ClearEffects()
        {
            splatter?.ClearAll();
            foreach (var lr in _sweeps) lr.enabled = false;
            ClearPoseOverride();
            Playing = false;
        }

        public static BeatAnimator Create(Transform parent, CameraShake camShake, InkSplatter ink,
                                          DuelAudio audio, FighterView me, FighterView foe)
        {
            var go = new GameObject("BeatAnimator");
            go.transform.SetParent(parent, false);
            var a = go.AddComponent<BeatAnimator>();
            a.shake = camShake; a.splatter = ink; a.audioBank = audio;
            a.meView = me; a.foeView = foe;
            return a;
        }
    }
}
