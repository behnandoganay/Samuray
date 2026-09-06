using System.Collections.Generic;
using UnityEngine;
using Samuray.Core;

namespace Samuray.Game
{
    /// <summary>Duellonun beyni: faz makinesi, tur dongusu, sayac.
    ///
    /// View'lar aptaldir - Core durumunu okuyup gosterirler, hicbir kural bilmezler.
    /// Tur cozumlemesi yalnizca buradan, tek bir noktadan gecer.
    /// </summary>
    public sealed class DuelController : MonoBehaviour
    {
        enum Phase { Plan, Resolve, Over }

        [Header("Sahne referanslari")]
        [SerializeField] ArenaView arena;
        [SerializeField] FighterView meView;
        [SerializeField] FighterView foeView;
        [SerializeField] HudView hud;
        [SerializeField] StrokeInput input;

        [Header("Ayarlar")]
        [SerializeField] string startingFoe = "RONIN";
        [Tooltip("Planlama suresi. 0 = sayac kapali, suresiz dusun.")]
        [SerializeField] float turnSeconds = 5f;
        [SerializeField] float resolveSettleSeconds = 0.9f;

        Rules _r;
        Brain _brain;
        Fighter _me, _foe;
        System.Random _rng;

        readonly List<Gesture> _strokes = new List<Gesture>();
        Action? _pendingGuard;
        bool _didRead;

        List<Action> _foeIntent = new List<Action>();
        List<Action> _foeShown;          // null = gizli durustan okunamiyor
        Phase _phase = Phase.Plan;
        float _timeLeft, _settle;
        int _beat;
        readonly List<string> _log = new List<string>();

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
            if (_phase == Phase.Plan && turnSeconds > 0f)
            {
                _timeLeft -= Time.deltaTime;
                hud?.SetTimer(_timeLeft / turnSeconds);
                if (_timeLeft <= 0f)
                {
                    // Sure dolarsa kilicin oldugu yerde gard alirsin - cezalandirilmazsin.
                    if (CurrentIntent().Count == 0) _pendingGuard = Action.Guard();
                    hud?.SetReadout("sure doldu — gard aldin");
                    Commit();
                }
            }
            else if (_phase == Phase.Resolve)
            {
                _settle -= Time.deltaTime;
                if (_settle <= 0f) StartPlanning();
            }
        }

        // ---------------- duello akisi ----------------

        public void NewDuel(string brainKey)
        {
            _brain = Brain.Make(brainKey, _r);
            _me = new Fighter("Sen") { Kamae = Kamae.CHUDAN, Ki = _r.KiStart, KiMax = _r.KiMax };
            _foe = _brain.MakeFighter(_brain.Label);
            _strokes.Clear(); _pendingGuard = null; _didRead = false;
            _beat = 0; _log.Clear();
            meView?.SetKamae(_me.Kamae, true);
            foeView?.SetKamae(_foe.Kamae, true);
            meView?.SetWounds(0); foeView?.SetWounds(0);
            input?.ClearTrail();
            PlanFoe();
            StartPlanning();
            hud?.SetLog("");
            hud?.SetReadout("hat boyunca çiz");
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

            var res = BeatResolver.Resolve(_me, intent, _foe, _foeIntent, _r);
            _me = res.A; _foe = res.B;
            _beat++;

            _log.Insert(0, "— Tur " + _beat + " —");
            for (int i = 0; i < res.Events.Count; i++) _log.Insert(1 + i, res.Events[i]);
            while (_log.Count > 8) _log.RemoveAt(_log.Count - 1);
            hud?.SetLog(string.Join("\n", _log));

            _strokes.Clear(); _pendingGuard = null; _didRead = false;
            input?.ClearTrail();
            if (input != null) input.Enabled = false;

            meView?.SetKamae(_me.Kamae);
            foeView?.SetKamae(_foe.Kamae);
            meView?.SetWounds(_me.Wounds);
            foeView?.SetWounds(_foe.Wounds);

            if (!_me.Alive(_r) || !_foe.Alive(_r)) { Finish(); return; }

            PlanFoe();
            _phase = Phase.Resolve;
            _settle = resolveSettleSeconds;
            hud?.SetTimer(0f);
            Refresh();
        }

        void Finish()
        {
            _phase = Phase.Over;
            string head;
            if (_me.Alive(_r) && !_foe.Alive(_r)) head = _foe.Name + " dustu. " + _beat + " turda bitti.";
            else if (_foe.Alive(_r) && !_me.Alive(_r)) head = "Dustun. " + _foe.Name + " " + _beat + " turda bitirdi.";
            else head = "Ai-uchi — karsilikli olum. Ikiniz de " + _beat + ". turda dustunuz.";
            _log.Insert(0, head);
            hud?.SetLog(string.Join("\n", _log));
            hud?.SetTimer(0f);
            Refresh();
        }

        // ---------------- girdi ----------------

        void OnPreview(Gesture g)
        {
            if (_phase != Phase.Plan || g.Action == null) return;
            var a = g.Action.Value;
            string tier = g.Tier == Tier.PARRY ? "halka"
                        : g.Tier == Tier.FEINT ? "yarim — yalan olur"
                        : g.Tier == Tier.HEAVY ? "agir" : "hizli";
            hud?.SetReadout((a.Type == ActionType.PARRY ? "savurma: " : "") +
                            (a.Line.HasValue ? a.Line.Value.ToString() : "") + "   " + tier);
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
            hud?.SetReadout("kabul: " + g.Action.Value);
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
            hud?.SetReadout(yalandi ? "telegraf yalandı — gerçek niyet açıldı" : "telegraf doğruymuş");
            Refresh();
        }

        void SelectKamae(Kamae k)
        {
            if (_phase != Phase.Plan) return;
            if (_pendingGuard.HasValue && _pendingGuard.Value.ToKamae == k) _pendingGuard = null;
            else _pendingGuard = k == _me.Kamae ? Action.Guard() : Action.Guard(k);
            hud?.SetReadout(_pendingGuard.HasValue
                ? (_pendingGuard.Value.ToKamae.HasValue ? "duruş → " + k : "gard al (+1 nefes)")
                : "hat boyunca çiz");
            Refresh();
        }

        void Refresh()
        {
            arena?.SetGuarded(_r.GuardsOf(_foe.Kamae));
            bool busy = _phase != Phase.Plan;
            bool over = _phase == Phase.Over;
            hud?.Refresh(_r, _me, _foe, _foe.Name, _foeShown, CurrentIntent(),
                         _strokes.Count > 0 || _pendingGuard.HasValue || _didRead,
                         !_didRead && _me.Ki >= _r.Cost("read") && _foeShown != null,
                         busy, over);
        }
    }
}
