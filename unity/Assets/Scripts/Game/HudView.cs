using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Samuray.Core;

namespace Samuray.Game
{
    /// <summary>Ekran arayuzu.
    ///
    /// TextMeshPro yerine legacy UI.Text kullaniliyor: TMP ilk kullanimda
    /// "Import TMP Essentials" popup'i istiyor ve kurulum surtunmesini artiriyor.
    /// Prototip HUD'u icin yerlesik font fazlasiyla yeterli.
    ///
    /// Yaralar ve nefes burada metin olarak gosteriliyor; gorsel karsiliklari
    /// zaten savascilarin siluetinde var.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] Text foeLine, telegraphLine, meLine, queueLine, budgetLine, logLine, readoutLine;
        [SerializeField] Image timerFill;
        [SerializeField] Button undoButton, readButton, commitButton;
        [SerializeField] Button[] kamaeButtons;
        [SerializeField] Button[] foeButtons;

        public event System.Action UndoPressed, ReadPressed, CommitPressed;
        public event System.Action<Kamae> KamaePressed;
        public event System.Action<string> FoePressed;

        static readonly string[] FoeKeys = { "RONIN", "OGRENCI", "BLOFCU", "USTA" };

        public void SetTimer(float ratio01)
        {
            if (timerFill != null) timerFill.fillAmount = Mathf.Clamp01(ratio01);
        }
        public void SetReadout(string s) { if (readoutLine != null) readoutLine.text = s; }
        public void SetLog(string s) { if (logLine != null) logLine.text = s; }

        public void Refresh(Rules r, Fighter me, Fighter foe, string foeName,
                            IReadOnlyList<Action> telegraph, List<Action> intent,
                            bool canUndo, bool canRead, bool busy, bool over)
        {
            var gf = new List<string>();
            foreach (var l in r.GuardsOf(foe.Kamae)) gf.Add(l.ToString());
            gf.Sort();
            foeLine.text = $"{foeName}   {foe.Kamae}   yara {Dots(foe.Wounds, r.WoundsToDie)}\n" +
                           (gf.Count > 0 ? "savunur " + string.Join(" · ", gf) : "hicbir hatti savunmuyor");

            telegraphLine.text = telegraph == null
                ? "niyeti okunamiyor — gizli duruşta"
                : (telegraph.Count == 0 ? "bekliyor, nefes topluyor" : Join(telegraph));

            var gm = new List<string>();
            foreach (var l in r.GuardsOf(me.Kamae)) gm.Add(l.ToString());
            gm.Sort();
            var nat = new List<string>();
            foreach (var l in r.NaturalLines(me.Kamae)) nat.Add(l.ToString());
            nat.Sort();
            string flags = "";
            if (me.Suki) flags += "  ACIK VERDIN";
            else if (me.Exposed) flags += "  GARDIN KIRIK";
            if (me.Riposte) flags += "  RIPOSTE";
            foreach (var inj in me.Injuries) flags += "  " + inj + " yarasi";

            meLine.text = $"SEN   {me.Kamae}   yara {Dots(me.Wounds, r.WoundsToDie)}   " +
                          $"nefes {Bars(me.Ki, me.KiMax)}{flags}\n" +
                          $"savundugun: {(gm.Count > 0 ? string.Join(" · ", gm) : "yok")}   |   " +
                          $"ucuz kesim: {string.Join(" · ", nat)}";

            queueLine.text = intent.Count == 0
                ? "bu tur icin henuz bir niyetin yok"
                : Join(intent);

            int spent = BeatResolver.IntentCost(me, intent, r);
            budgetLine.text = $"harcanan {spent} / nefes {me.Ki}  ·  tur tavani {r.MaxSpend}" +
                              (spent > me.Ki ? "   — acik vereceksin" : "");
            budgetLine.color = spent > me.Ki ? Art.Shu : Art.Ink;

            undoButton.interactable = !busy && !over && canUndo;
            readButton.interactable = !busy && !over && canRead;
            commitButton.interactable = !busy;
            commitButton.GetComponentInChildren<Text>().text = over ? "YENIDEN" : "TURU BASLAT";

            if (kamaeButtons != null)
                for (int i = 0; i < kamaeButtons.Length; i++)
                {
                    var k = (Kamae)i;
                    bool ok = !busy && !over &&
                              (k == me.Kamae || (r.Adjacent(me.Kamae).Contains(k) && !me.Injuries.Contains(Injury.LEG)));
                    kamaeButtons[i].interactable = ok;
                }
        }

        static string Join(IReadOnlyList<Action> acts)
        {
            var parts = new List<string>();
            foreach (var a in acts) parts.Add(a.ToString());
            return string.Join(",  ", parts);
        }
        static string Dots(int filled, int total)
        {
            var s = "";
            for (int i = 0; i < total; i++) s += i < filled ? "●" : "○";
            return s;
        }
        static string Bars(int filled, int total)
        {
            var s = "";
            for (int i = 0; i < total; i++) s += i < filled ? "▮" : "▯";
            return s;
        }

        // ---------------- kurulum ----------------

        static Font _font;
        static Font Fnt()
        {
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _font;
        }

        static Text MakeText(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                             Vector2 offMin, Vector2 offMax, int size, TextAnchor align, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Fnt(); t.fontSize = size; t.alignment = align; t.color = col;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = t.rectTransform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
            return t;
        }

        static Button MakeButton(Transform parent, string label, Vector2 anchorMin, Vector2 anchorMax,
                                 Vector2 offMin, Vector2 offMax, int size)
        {
            var go = new GameObject("Btn " + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0.10f);
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            var rt = img.rectTransform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offMin; rt.offsetMax = offMax;
            var t = MakeText(go.transform, "Label", Vector2.zero, Vector2.one,
                             Vector2.zero, Vector2.zero, size, TextAnchor.MiddleCenter, Art.Ink);
            t.text = label;
            return b;
        }

        /// <summary>Sahne kurucusunun cagirdigi fabrika: Canvas ve tum ogeleri kurar.</summary>
        public static HudView Create(Transform parent)
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 1f;   // dikey oyun: yuksekligi baz al
            go.AddComponent<GraphicRaycaster>();

            var v = go.AddComponent<HudView>();
            var T = go.transform;

            // sayac cubugu - en ust
            var bar = new GameObject("TimerBar");
            bar.transform.SetParent(T, false);
            var barImg = bar.AddComponent<Image>();
            barImg.sprite = Art.Rect();          // Filled tipi sprite olmadan doldurmaz
            barImg.color = Art.Shu;
            barImg.type = Image.Type.Filled;
            barImg.fillMethod = Image.FillMethod.Horizontal;
            var brt = barImg.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f);
            brt.offsetMin = new Vector2(0f, -8f); brt.offsetMax = new Vector2(0f, 0f);
            v.timerFill = barImg;

            v.foeLine = MakeText(T, "FoeLine", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(24, -110), new Vector2(-24, -16), 30, TextAnchor.UpperLeft, Art.Ink);
            v.telegraphLine = MakeText(T, "Telegraph", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(24, -170), new Vector2(-24, -114), 32, TextAnchor.UpperLeft, Art.Shu);
            v.readoutLine = MakeText(T, "Readout", new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(24, -212), new Vector2(-24, -174), 26, TextAnchor.UpperLeft, Art.Faint);

            v.meLine = MakeText(T, "MeLine", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(24, 470), new Vector2(-24, 570), 28, TextAnchor.LowerLeft, Art.Ink);
            v.queueLine = MakeText(T, "Queue", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(24, 420), new Vector2(-24, 466), 28, TextAnchor.LowerLeft, Art.Ink);
            v.budgetLine = MakeText(T, "Budget", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(24, 380), new Vector2(-24, 418), 26, TextAnchor.LowerLeft, Art.Ink);
            v.logLine = MakeText(T, "Log", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(24, 180), new Vector2(-24, 370), 26, TextAnchor.UpperLeft, Art.Faint);

            // durus butonlari
            var kamae = new List<Button>();
            string[] kn = { "JODAN", "CHUDAN", "GEDAN", "HASSO", "WAKI" };
            for (int i = 0; i < 5; i++)
            {
                float x0 = i / 5f, x1 = (i + 1) / 5f;
                var b = MakeButton(T, kn[i], new Vector2(x0, 0), new Vector2(x1, 0),
                    new Vector2(6 + 24 * (i == 0 ? 1 : 0), 300), new Vector2(-6 - 24 * (i == 4 ? 1 : 0), 366), 22);
                var k = (Kamae)i;
                b.onClick.AddListener(() => v.KamaePressed?.Invoke(k));
                kamae.Add(b);
            }
            v.kamaeButtons = kamae.ToArray();

            // eylem butonlari
            v.undoButton = MakeButton(T, "GERI", new Vector2(0f, 0), new Vector2(0.28f, 0),
                new Vector2(24, 200), new Vector2(-6, 288), 26);
            v.readButton = MakeButton(T, "OKU", new Vector2(0.28f, 0), new Vector2(0.56f, 0),
                new Vector2(6, 200), new Vector2(-6, 288), 26);
            v.commitButton = MakeButton(T, "TURU BASLAT", new Vector2(0.56f, 0), new Vector2(1f, 0),
                new Vector2(6, 200), new Vector2(-24, 288), 26);
            v.undoButton.onClick.AddListener(() => v.UndoPressed?.Invoke());
            v.readButton.onClick.AddListener(() => v.ReadPressed?.Invoke());
            v.commitButton.onClick.AddListener(() => v.CommitPressed?.Invoke());

            // rakip secimi
            string[] fl = { "Ronin", "Ogrenci", "Blofcu", "Usta" };
            var foes = new List<Button>();
            for (int i = 0; i < 4; i++)
            {
                float x0 = i / 4f, x1 = (i + 1) / 4f;
                var b = MakeButton(T, fl[i], new Vector2(x0, 0), new Vector2(x1, 0),
                    new Vector2(6 + 18 * (i == 0 ? 1 : 0), 24), new Vector2(-6 - 18 * (i == 3 ? 1 : 0), 90), 22);
                string key = FoeKeys[i];
                b.onClick.AddListener(() => v.FoePressed?.Invoke(key));
                foes.Add(b);
            }
            v.foeButtons = foes.ToArray();

            return v;
        }
    }
}
