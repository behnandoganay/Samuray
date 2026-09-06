using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Samuray.Core;

namespace Samuray.Game
{
    /// <summary>Minimum arayuz.
    ///
    /// Onceki surumde her sey metindi - durum dokumleri, gard listeleri, dusmanin
    /// niyeti cumle olarak. Hepsi kalkti: dusmanin niyetini govdendeki telegraf
    /// izinden, yaralari siluetlerdeki murekkep lekesinden, durusu kilicin
    /// acisindan okuyorsun.
    ///
    /// Ekranda kalan: nefes noktalari, sayac, cizerken tek satirlik okuma,
    /// bir de olay parlamasi (isabet/savurma) - o da soluyor.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] Image timerFill;
        [SerializeField] Image[] kiPips;
        [SerializeField] Text readoutLine, flashLine;
        [SerializeField] Button undoButton, readButton, commitButton;
        [SerializeField] Button[] kamaeButtons;
        [SerializeField] Button[] foeButtons;
        [SerializeField] float flashSeconds = 2.2f;

        public event System.Action UndoPressed, ReadPressed, CommitPressed;
        public event System.Action<Kamae> KamaePressed;
        public event System.Action<string> FoePressed;

        static readonly string[] FoeKeys = { "RONIN", "OGRENCI", "BLOFCU", "USTA" };
        // Japonca terim yerine ne yaptigini soyleyen Turkce etiket: oyuncunun
        // kenjutsu sozlugu ezberlemesi gerekmiyor, kilicin nerede oldugunu okuyor.
        static readonly string[] KamaeShort = { "TEPE", "ORTA", "ALÇAK", "OMUZ", "GİZLİ" };

        float _flashLeft;

        void Update()
        {
            if (_flashLeft <= 0f || flashLine == null) return;
            _flashLeft -= Time.deltaTime;
            var c = flashLine.color;
            c.a = Mathf.Clamp01(_flashLeft / flashSeconds);
            flashLine.color = c;
            if (_flashLeft <= 0f) flashLine.text = "";
        }

        public void SetTimer(float ratio01)
        {
            if (timerFill != null) timerFill.fillAmount = Mathf.Clamp01(ratio01);
        }

        public void SetReadout(string s) { if (readoutLine != null) readoutLine.text = s; }

        /// <summary>Tek satirlik olay parlamasi - sonra soluyor.</summary>
        public void Flash(string s)
        {
            if (flashLine == null) return;
            flashLine.text = s;
            flashLine.color = Art.Shu;
            _flashLeft = flashSeconds;
        }

        public void Refresh(Rules r, Fighter me, bool canUndo, bool canRead, bool busy, bool over)
        {
            if (kiPips != null)
                for (int i = 0; i < kiPips.Length; i++)
                {
                    bool on = i < me.Ki;
                    kiPips[i].enabled = i < me.KiMax;
                    kiPips[i].color = on ? Art.Ink : new Color(Art.Ink.r, Art.Ink.g, Art.Ink.b, 0.18f);
                }

            if (undoButton != null) undoButton.interactable = !busy && !over && canUndo;
            if (readButton != null) readButton.interactable = !busy && !over && canRead;
            if (commitButton != null)
            {
                commitButton.interactable = !busy;
                var t = commitButton.GetComponentInChildren<Text>();
                if (t != null) t.text = over ? "YENİDEN" : "VUR";
            }

            if (kamaeButtons != null)
                for (int i = 0; i < kamaeButtons.Length; i++)
                {
                    var k = (Kamae)i;
                    bool ok = !busy && !over &&
                              (k == me.Kamae || (r.Adjacent(me.Kamae).Contains(k) && !me.Injuries.Contains(Injury.LEG)));
                    kamaeButtons[i].interactable = ok;
                    var img = kamaeButtons[i].targetGraphic as Image;
                    if (img != null)
                        img.color = k == me.Kamae
                            ? new Color(Art.Ink.r, Art.Ink.g, Art.Ink.b, 0.22f)
                            : new Color(Art.Ink.r, Art.Ink.g, Art.Ink.b, 0.06f);
                }
        }

        // ---------------- kurulum ----------------

        static Font _font;
        static Font Fnt()
        {
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _font;
        }

        static Text MakeText(Transform parent, string name, Vector2 aMin, Vector2 aMax,
                             Vector2 oMin, Vector2 oMax, int size, TextAnchor align, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = Fnt(); t.fontSize = size; t.alignment = align; t.color = col;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
            return t;
        }

        static Button MakeButton(Transform parent, string label, Vector2 aMin, Vector2 aMax,
                                 Vector2 oMin, Vector2 oMax, int size)
        {
            var go = new GameObject("Btn " + label);
            go.transform.SetParent(parent, false);
            var img = go.AddComponent<Image>();
            img.sprite = Art.Rect();
            img.color = new Color(Art.Ink.r, Art.Ink.g, Art.Ink.b, 0.08f);
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            var rt = img.rectTransform;
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.offsetMin = oMin; rt.offsetMax = oMax;
            var t = MakeText(go.transform, "Label", Vector2.zero, Vector2.one,
                             Vector2.zero, Vector2.zero, size, TextAnchor.MiddleCenter, Art.Ink);
            t.text = label;
            return b;
        }

        public static HudView Create(Transform parent)
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 1f;
            go.AddComponent<GraphicRaycaster>();

            var v = go.AddComponent<HudView>();
            var T = go.transform;

            // sayac: en ustte ince bir cizgi
            var bar = new GameObject("TimerBar");
            bar.transform.SetParent(T, false);
            var barImg = bar.AddComponent<Image>();
            barImg.sprite = Art.Rect();
            barImg.color = Art.Shu;
            barImg.type = Image.Type.Filled;
            barImg.fillMethod = Image.FillMethod.Horizontal;
            barImg.raycastTarget = false;
            var brt = barImg.rectTransform;
            brt.anchorMin = new Vector2(0f, 1f); brt.anchorMax = new Vector2(1f, 1f);
            brt.offsetMin = new Vector2(0f, -7f); brt.offsetMax = Vector2.zero;
            v.timerFill = barImg;

            // olay parlamasi - ekranin ortasinin biraz ustu
            v.flashLine = MakeText(T, "Flash", new Vector2(0, 0.5f), new Vector2(1, 0.5f),
                new Vector2(30, 200), new Vector2(-30, 270), 40, TextAnchor.MiddleCenter, Art.Shu);
            v.flashLine.text = "";

            // cizerken okuma
            v.readoutLine = MakeText(T, "Readout", new Vector2(0, 0), new Vector2(1, 0),
                new Vector2(30, 352), new Vector2(-30, 400), 26, TextAnchor.LowerLeft, Art.Faint);

            // nefes noktalari - sol altta, oyuncunun yaninda
            var pips = new List<Image>();
            for (int i = 0; i < 4; i++)
            {
                var pgo = new GameObject("Ki" + i);
                pgo.transform.SetParent(T, false);
                var img = pgo.AddComponent<Image>();
                img.sprite = Art.Circle();
                img.raycastTarget = false;
                var rt = img.rectTransform;
                rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 0);
                rt.offsetMin = new Vector2(32 + i * 40, 300);
                rt.offsetMax = new Vector2(32 + i * 40 + 26, 326);
                pips.Add(img);
            }
            v.kiPips = pips.ToArray();

            // durus dugmeleri: kanji, kompakt
            var kamae = new List<Button>();
            for (int i = 0; i < 5; i++)
            {
                float x0 = 0.02f + i * 0.116f;
                var b = MakeButton(T, KamaeShort[i], new Vector2(x0, 0), new Vector2(x0 + 0.106f, 0),
                    Vector2.zero, Vector2.zero, 26);
                var rt = (RectTransform)b.transform;
                rt.offsetMin = new Vector2(0, 176); rt.offsetMax = new Vector2(0, 278);
                var k = (Kamae)i;
                b.onClick.AddListener(() => v.KamaePressed?.Invoke(k));
                kamae.Add(b);
            }
            v.kamaeButtons = kamae.ToArray();

            v.undoButton = MakeButton(T, "GERİ", new Vector2(0.60f, 0), new Vector2(0.79f, 0),
                new Vector2(4, 176), new Vector2(-4, 278), 24);
            v.readButton = MakeButton(T, "OKU", new Vector2(0.79f, 0), new Vector2(0.98f, 0),
                new Vector2(4, 176), new Vector2(-4, 278), 24);
            v.commitButton = MakeButton(T, "VUR", new Vector2(0.02f, 0), new Vector2(0.98f, 0),
                new Vector2(0, 56), new Vector2(0, 164), 38);
            v.undoButton.onClick.AddListener(() => v.UndoPressed?.Invoke());
            v.readButton.onClick.AddListener(() => v.ReadPressed?.Invoke());
            v.commitButton.onClick.AddListener(() => v.CommitPressed?.Invoke());

            // rakip secimi - gelistirme icin, kucuk ve solgun
            string[] fl = { "Ronin", "Öğrenci", "Blöfçü", "Usta" };
            var foes = new List<Button>();
            for (int i = 0; i < 4; i++)
            {
                float x0 = 0.02f + i * 0.245f;
                var b = MakeButton(T, fl[i], new Vector2(x0, 0), new Vector2(x0 + 0.235f, 0),
                    new Vector2(0, 8), new Vector2(0, 48), 20);
                string key = FoeKeys[i];
                b.onClick.AddListener(() => v.FoePressed?.Invoke(key));
                foes.Add(b);
            }
            v.foeButtons = foes.ToArray();

            return v;
        }
    }
}
