using UnityEngine;

namespace Samuray.Game
{
    /// <summary>Tamamen prosedürel ses - hicbir dosya yok.
    ///
    /// Ornekler kodla uretilip AudioClip'e yaziliyor. Web prototipinde bu
    /// tasarim denendi ve vurus hissindeki farki buyuktu; buradaki portu.
    /// </summary>
    public sealed class DuelAudio : MonoBehaviour
    {
        const int Rate = 44100;

        [SerializeField] float masterVolume = 0.85f;
        [SerializeField] bool muted;

        AudioSource _oneShot, _loop;
        AudioClip _whoosh, _whooshHeavy, _thud, _thudHeavy, _ring, _tick, _toll, _breath;

        public bool Muted { get => muted; set { muted = value; if (value) StopBreath(); } }

        void Awake()
        {
            _oneShot = gameObject.AddComponent<AudioSource>();
            _oneShot.playOnAwake = false;
            _loop = gameObject.AddComponent<AudioSource>();
            _loop.playOnAwake = false; _loop.loop = true;

            _whoosh      = Whoosh("whoosh", 0.30f, 1900f, 320f, 0.16f);
            _whooshHeavy = Whoosh("whooshHeavy", 0.38f, 1400f, 190f, 0.22f);
            _thud        = Thud("thud", 125f, 42f, 0.30f, 0.45f);
            _thudHeavy   = Thud("thudHeavy", 105f, 34f, 0.40f, 0.60f);
            _ring        = Ring("ring");
            _tick        = Tick("tick");
            _toll        = Toll("toll");
            _breath      = Breath("breath");
        }

        // ---------------- oynatma ----------------

        void Shot(AudioClip c, float vol = 1f)
        {
            if (muted || c == null) return;
            _oneShot.PlayOneShot(c, Mathf.Clamp01(vol * masterVolume));
        }

        public void PlaySwing(bool heavy) => Shot(heavy ? _whooshHeavy : _whoosh);
        public void PlayHit(bool heavy, float power) => Shot(heavy ? _thudHeavy : _thud, Mathf.Clamp(power, 0.5f, 1.4f));
        public void PlaySteel() => Shot(_ring);
        public void PlayTick() => Shot(_tick, 0.7f);
        public void PlayDeath() => Shot(_toll);

        public void StartBreath()
        {
            if (muted || _breath == null) return;
            if (_loop.isPlaying) return;
            _loop.clip = _breath;
            _loop.volume = 0.22f * masterVolume;
            _loop.Play();
        }
        public void StopBreath() { if (_loop != null && _loop.isPlaying) _loop.Stop(); }

        // ---------------- sentez ----------------

        static AudioClip Make(string name, float seconds, System.Func<int, float, float> sample)
        {
            int n = Mathf.Max(1, Mathf.RoundToInt(Rate * seconds));
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sample(i, i / (float)Rate), -1f, 1f);
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Kilic hisirtisi: alcalan kesim frekansli gurultu.
        /// Tek kutuplu alcak geciren filtre yeterince ikna edici.</summary>
        static AudioClip Whoosh(string name, float dur, float f0, float f1, float peak)
        {
            float lp = 0f;
            return Make(name, dur, (i, t) =>
            {
                float p = t / dur;
                float cut = Mathf.Lerp(f0, f1, p);
                float a = Mathf.Clamp01(1f - Mathf.Exp(-p * 14f)) * Mathf.Exp(-p * 4.2f);
                float k = 1f - Mathf.Exp(-2f * Mathf.PI * cut / Rate);
                lp += k * (Random.value * 2f - 1f - lp);
                return lp * a * peak * 3.2f;
            });
        }

        /// <summary>Isabet: alcak sinus dususu (taiko) + kisa tokat.</summary>
        static AudioClip Thud(string name, float f0, float f1, float dur, float peak)
        {
            float phase = 0f;
            return Make(name, dur, (i, t) =>
            {
                float p = t / dur;
                float f = Mathf.Lerp(f0, f1, p * p);
                phase += 2f * Mathf.PI * f / Rate;
                float body = Mathf.Sin(phase) * Mathf.Exp(-p * 5.5f);
                float slap = (Random.value * 2f - 1f) * Mathf.Exp(-p * 55f) * 0.5f;
                return (body + slap) * peak;
            });
        }

        /// <summary>Celik: parry ve catisma. Uc uyumsuz tepe metalik tini verir.</summary>
        static AudioClip Ring(string name)
        {
            float[] f = { 2380f, 3140f, 4210f };
            var ph = new float[3];
            return Make(name, 0.45f, (i, t) =>
            {
                float p = t / 0.45f;
                float v = 0f;
                for (int k = 0; k < 3; k++)
                {
                    ph[k] += 2f * Mathf.PI * f[k] / Rate;
                    v += Mathf.Sin(ph[k]) * Mathf.Exp(-p * (7f + k * 3f)) / (k + 1.4f);
                }
                v += (Random.value * 2f - 1f) * Mathf.Exp(-p * 70f) * 0.35f;
                return v * 0.34f;
            });
        }

        /// <summary>Murekkep tiki: jest kabul edildi.</summary>
        static AudioClip Tick(string name)
        {
            float lp = 0f;
            return Make(name, 0.06f, (i, t) =>
            {
                float p = t / 0.06f;
                lp += 0.35f * ((Random.value * 2f - 1f) - lp);
                return lp * Mathf.Exp(-p * 26f) * 0.55f;
            });
        }

        /// <summary>Olum: uzun alcak can.</summary>
        static AudioClip Toll(string name)
        {
            float p1 = 0f, p2 = 0f;
            return Make(name, 2.0f, (i, t) =>
            {
                float p = t / 2.0f;
                p1 += 2f * Mathf.PI * 68f / Rate;
                p2 += 2f * Mathf.PI * 101f / Rate;
                return (Mathf.Sin(p1) * 0.6f + Mathf.Sin(p2) * 0.3f) * Mathf.Exp(-p * 3.2f) * 0.5f;
            });
        }

        /// <summary>Planlama fazinin nefesi: yavas dalgalanan alcak gurultu.
        /// Dongu icin bas ve son yumusatiliyor ki ek yerinde tik olmasin.</summary>
        static AudioClip Breath(string name)
        {
            const float dur = 4.0f;
            float lp = 0f;
            return Make(name, dur, (i, t) =>
            {
                float p = t / dur;
                lp += 0.02f * ((Random.value * 2f - 1f) - lp);
                float lfo = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * 0.28f * t);
                float edge = Mathf.Min(1f, Mathf.Min(p, 1f - p) / 0.05f);
                return lp * lfo * edge * 5.5f;
            });
        }

        public static DuelAudio Create(Transform parent)
        {
            var go = new GameObject("DuelAudio");
            go.transform.SetParent(parent, false);
            return go.AddComponent<DuelAudio>();
        }
    }
}
