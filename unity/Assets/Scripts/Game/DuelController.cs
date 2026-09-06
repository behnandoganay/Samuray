using System.Collections.Generic;
using UnityEngine;
using Samuray.Core;

namespace Samuray.Game
{
    /// <summary>Duellonun beyni: faz makinesi, tur dongusu, sayac.
    ///
    /// View'lar aptaldir - Core durumunu okuyup gosterirler, hicbir kural
    /// bilmezler. Tur cozumlemesi yalnizca buradan gecer.
    ///
    /// Metin telegrafi kaldirildi: dusmanin niyeti artik SENIN govdende yanip
    /// sonen hat olarak gosteriliyor, cumle olarak degil.
    /// </summary>
    public sealed class DuelController : MonoBehaviour
    {
        enum Phase { Plan, Resolve, Over }

        [Header("Sahne referanslari")]
        [SerializeField] FighterView meView;
        [SerializeField] FighterView foeView;
        [SerializeField] LineOverlay guardOverlay;      // dusmanin uzerinde
        [SerializeField] LineOverlay telegraphOverlay;  // senin uzerinde
        [SerializeField] HudView hud;
        [SerializeField] StrokeInput input;
        [SerializeField] BeatAnimator animator;
        [SerializeField] DuelStage stage;
        [SerializeField] DuelAudio duelAudio;

        [Header("Ayarlar")]
        [SerializeField] string startingFoe = "RONIN";
        [Tooltip("Planlama suresi. 0 = sayac kapali, suresiz dusun.")]
        [SerializeField] float turnSeconds = 5f;

        Rules _r;
        Brain _brain;
        Fighter _me, _foe;
        System.Random _rng;

        readonly List<Gesture> _strokes = new List<Gesture>();
        Action? _pendingGuard;
        bool _didRead;

        List<Action> _foeIntent = new List<Action>();
        List<Action> _foeShown;      // null = gizli durustan okunamiyor
        Phase _phase = Phase.Plan;
        float _timeLeft;
        BeatResult _pending;
        int _beat;

        void Start()
        {
            _r = RulesProvider.Get();
            if (_r == null) { enabled = false; return; }
            _rng = new System.Random();

            if (hud != null)
            {
                hud.UndoPressed += Undo;
                hud.ReadPressed += ReadTelegraph;
                hud.CommitPressed += CommitOrRestart;
                hud.KamaePressed += SelectKamae;
                hud.FoePressed += NewDuel;
            }
            if (input != null)
            {
                input.GestureCompleted += OnGesture;
                input.GesturePreview += OnPreview;
            }
            NewDuel(startingFoe);
        }

        void Update()
        {
            if (_phase != Phase.Plan) return;

            if (turnSeconds > 0f)
            {
                _timeLeft -= Time.deltaTime;
                float left = Mathf.Clamp01(_timeLeft / turnSeconds);
                hud?.SetTimer(left);
                stage?.SetTension(1f - left);
                if (_timeLeft <= 0f)
                {
                    // Sure dolarsa kilicin oldugu yerde gard alirsin - ceza yok.
                    if (CurrentIntent().Count == 0) _pendingGuard = Action.Guard();
                    hud?.SetReadout("süre doldu — gard aldın");
                    Commit();
                }
            }
        }

        // ---------------- duello akisi ----------------

        public void NewDuel(string brainKey)
        {
            _brain = Brain.Make(brainKey, _r);
            _me = new Fighter("Sen") { Kamae = Kamae.CHUDAN, Ki = _r.KiStart, KiMax = _r.KiMax };
            _foe = _brain.MakeFighter(_brain.Label);
            _strokes.Clear(); _pendingGuard = null; _didRead = false;
            _beat = 0; _pending = null;

            meView?.SetKamae(_me.Kamae, true);
            foeView?.SetKamae(_foe.Kamae, true);
            meView?.SetWounds(0); foeView?.SetWounds(0);
            input?.ClearTrail();
            animator?.ClearEffects();
            hud?.Flash("");
            hud?.SetReadout("hat boyunca çiz");

            PlanFoe();
            StartPlanning();
        }

        void PlanFoe()
        {
            _foeIntent = _brain.Choose(_foe, _me, _rng);
            _foeShown = _brain.Telegraph(_foeIntent, _foe, _rng);
        }

        void StartPlanning()
        {
            if (_phase == Phase.Over) return;
            _phase = Phase.Plan;
            _timeLeft = turnSeconds;
            if (input != null) input.Enabled = true;
            hud?.SetTimer(1f);
            stage?.SetTension(0f);
            duelAudio?.StartBreath();
            Refresh();
        }

        List<Action> CurrentIntent()
        {
            var acts = GestureClassifier.AssembleIntent(_strokes);
            if (_pendingGuard.HasValue) acts.Add(_pendingGuard.Value);
            return acts;
        }

        void CommitOrRestart()
        {
            if (_phase == Phase.Over) { NewDuel(_brain != null ? _brain.Key : startingFoe); return; }
            if (_phase == Phase.Plan) Commit();
        }

        void Commit()
        {
            var intent = CurrentIntent();
            if (_didRead) intent.Insert(0, Action.Read());

            // Cozumleme hemen yapilir ama sonuc animasyon bitene kadar
            // uygulanmaz: once darbeyi gor, sonra sonucunu.
            _pending = BeatResolver.Resolve(_me, intent, _foe, _foeIntent, _r);

            _strokes.Clear(); _pendingGuard = null; _didRead = false;
            input?.ClearTrail();
            if (input != null) input.Enabled = false;
            _phase = Phase.Resolve;
            duelAudio?.StopBreath();
            hud?.SetTimer(0f);
            stage?.SetTension(0f);
            Refresh();

            if (animator != null)
                animator.Play(BuildSpec(intent, _foeIntent, _pending), OnBeatContact, OnBeatComplete);
            else { OnBeatContact(); OnBeatComplete(); }
        }

        BeatAnimator.Spec BuildSpec(List<Action> mine, List<Action> theirs, BeatResult res)
        {
            var s = new BeatAnimator.Spec();
            foreach (var a in mine)
                if (a.Type == ActionType.CUT) s.Cuts.Add((a.Line.Value, a.Heavy, true));
            foreach (var a in theirs)
                if (a.Type == ActionType.CUT) s.Cuts.Add((a.Line.Value, a.Heavy, false));

            foreach (var e in res.Events)
            {
                if (e.Contains("catisti")) s.Clash = true;
                if (e.Contains("savurdu!")) s.Parried = true;
            }
            foreach (var h in res.Hits)
            {
                if (h.Defender == _foe.Name) s.DamageToFoe += h.Damage;
                else s.DamageToMe += h.Damage;
            }
            s.Lethal = res.A.Wounds >= _r.WoundsToDie || res.B.Wounds >= _r.WoundsToDie;
            s.MyEndKamae = res.A.Kamae;
            s.FoeEndKamae = res.B.Kamae;
            return s;
        }

        /// <summary>Temas ani: yaralar tam burada gorunur, hit-stop'la ayni karede.</summary>
        void OnBeatContact()
        {
            if (_pending == null) return;
            meView?.SetWounds(_pending.A.Wounds);
            foeView?.SetWounds(_pending.B.Wounds);
            hud?.Flash(Summarize(_pending));
        }

        void OnBeatComplete()
        {
            if (_pending == null) return;
            var res = _pending;
            _pending = null;

            _me = res.A; _foe = res.B;
            _beat++;

            meView?.SetKamae(_me.Kamae);
            foeView?.SetKamae(_foe.Kamae);
            meView?.SetWounds(_me.Wounds);
            foeView?.SetWounds(_foe.Wounds);

            if (!_me.Alive(_r) || !_foe.Alive(_r)) { Finish(); return; }

            PlanFoe();
            StartPlanning();
        }

        /// <summary>Turun tek satirlik ozeti - ekranda kisa sure parlar sonra soner.</summary>
        string Summarize(BeatResult res)
        {
            foreach (var e in res.Events) if (e.Contains("savurdu!")) return "savurdun";
            foreach (var e in res.Events) if (e.Contains("catisti")) return "kılıçlar çatıştı";

            int toFoe = 0, toMe = 0;
            foreach (var h in res.Hits)
            {
                if (h.Defender == _foe.Name) toFoe += h.Damage;
                else toMe += h.Damage;
            }
            if (toFoe > 0 && toMe > 0) return "ikiniz de yara aldınız";
            if (toFoe > 0) return toFoe == 1 ? "vurdun" : "derin kesik";
            if (toMe > 0) return toMe == 1 ? "yara aldın" : "ağır yara aldın";

            foreach (var e in res.Events) if (e.Contains("karsiladi")) return "gardına takıldı";
            foreach (var e in res.Events) if (e.Contains("gardini kirdi")) return "gardı kırıldı";
            return "";
        }

        void Finish()
        {
            _phase = Phase.Over;
            duelAudio?.StopBreath();
            duelAudio?.PlayDeath();
            telegraphOverlay?.Clear();

            string head;
            if (_me.Alive(_r) && !_foe.Alive(_r)) head = _foe.Name + " düştü";
            else if (_foe.Alive(_r) && !_me.Alive(_r)) head = "düştün";
            else head = "karşılıklı ölüm";
            hud?.Flash(head);
            hud?.SetReadout(_beat + " turda bitti");
            hud?.SetTimer(0f);
            stage?.SetTension(0f);
            Refresh();
        }

        // ---------------- girdi ----------------

        void OnPreview(Gesture g)
        {
            if (_phase != Phase.Plan || g.Action == null) return;
            hud?.SetReadout(Describe(g.Action.Value, g.Tier));
        }

        /// <summary>Jestin okunabilir ozeti - maliyeti ve BITIS DURUSU dahil.
        ///
        /// Bitis durusunu gostermek onemli: "kilicin bittigi yer bir sonraki
        /// turun basladigi yerdir" oyunun temel kurali, ama oyuncuya bunu hicbir
        /// sey soylemezse kilicin nereye gittigi keyfi gorunuyor.
        /// </summary>
        string Describe(Action a, Tier? tier)
        {
            if (a.Type == ActionType.PARRY)
                return "savurma  " + a.Line.Value + "   " + _r.Cost("parry") + " Ki";

            if (a.Type != ActionType.CUT)
                return a.ToString();

            string kademe = tier == Tier.FEINT ? "yarım — yalan olur"
                          : tier == Tier.HEAVY ? "ağır" : "hızlı";
            int maliyet = _r.CutCost(_me.Kamae, a.Line.Value, a.Heavy);
            string son = KamaeAdi(_r.EndsAt(a.Line.Value));
            return $"{a.Line.Value}  ·  {kademe}  ·  {maliyet} Ki   →   bitiş: {son}";
        }

        void OnGesture(Gesture g)
        {
            if (_phase != Phase.Plan) return;
            if (g.Action == null) { hud?.SetReadout(g.Reason); return; }

            if (_me.Kamae == Kamae.WAKI && _strokes.Count >= _r.WakiMaxActions)
            { hud?.SetReadout("gizli duruştan turda tek eylem"); return; }

            var deneme = new List<Gesture>(_strokes) { g };
            var acts = GestureClassifier.AssembleIntent(deneme);
            if (_pendingGuard.HasValue) acts.Add(_pendingGuard.Value);
            if (BeatResolver.IntentCost(_me, acts, _r) > _r.MaxSpend)
            { hud?.SetReadout("tur tavanı " + _r.MaxSpend + " Ki — sığmadı"); return; }

            _strokes.Add(g);
            duelAudio?.PlayTick();
            hud?.SetReadout(Describe(g.Action.Value, g.Tier));
            Refresh();
        }

        void Undo()
        {
            if (_phase != Phase.Plan) return;
            if (_pendingGuard.HasValue) _pendingGuard = null;
            else if (_strokes.Count > 0) _strokes.RemoveAt(_strokes.Count - 1);
            else if (_didRead) _didRead = false;
            hud?.SetReadout("hat boyunca çiz");
            Refresh();
        }

        void ReadTelegraph()
        {
            if (_phase != Phase.Plan || _didRead) return;
            bool yalandi = _foeShown != _foeIntent;
            _didRead = true;
            _foeShown = _foeIntent;
            duelAudio?.PlayTick();
            hud?.SetReadout(yalandi ? "telegraf yalandı" : "telegraf doğruymuş");
            Refresh();
        }

        void SelectKamae(Kamae k)
        {
            if (_phase != Phase.Plan) return;
            if (_pendingGuard.HasValue && _pendingGuard.Value.ToKamae == k) _pendingGuard = null;
            else _pendingGuard = k == _me.Kamae ? Action.Guard() : Action.Guard(k);
            duelAudio?.PlayTick();
            hud?.SetReadout(_pendingGuard.HasValue
                ? (_pendingGuard.Value.ToKamae.HasValue ? "duruş → " + KamaeAdi(k) : "gard (+1 nefes)")
                : "hat boyunca çiz");
            Refresh();
        }

        static string KamaeAdi(Kamae k)
        {
            switch (k)
            {
                case Kamae.JODAN: return "tepe";
                case Kamae.CHUDAN: return "orta";
                case Kamae.GEDAN: return "alçak";
                case Kamae.HASSO: return "omuz";
                default: return "gizli";
            }
        }

        void Refresh()
        {
            // Dusmanin savundugu hatlar KENDI govdesinde
            guardOverlay?.Show(_r.GuardsOf(_foe.Kamae));

            // Gelen darbe SENIN govdende - metin telegrafinin yerini alan sey bu
            if (_phase == Phase.Plan && _foeShown != null)
            {
                var lines = new List<CutLine>();
                foreach (var a in _foeShown)
                    if (a.Type == ActionType.CUT) lines.Add(a.Line.Value);
                telegraphOverlay?.Show(lines);
            }
            else telegraphOverlay?.Clear();

            bool busy = _phase != Phase.Plan;
            bool over = _phase == Phase.Over;
            hud?.Refresh(_r, _me,
                         _strokes.Count > 0 || _pendingGuard.HasValue || _didRead,
                         !_didRead && _me.Ki >= _r.Cost("read") && _foeShown != null,
                         busy, over);
        }
    }
}
