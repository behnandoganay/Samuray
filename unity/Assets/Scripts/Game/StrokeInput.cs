using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using Samuray.Core;

namespace Samuray.Game
{
    /// <summary>Cizim yakalama: parmak/fare -> nokta listesi -> Gesture.
    ///
    /// YENI Input System kullaniliyor. Projede InputSystem_Actions asset'i var,
    /// yani Active Input Handling "Input System Package (New)"; bu durumda eski
    /// UnityEngine.Input API'si exception firlatir. Pointer.current fare ile
    /// dokunmayi tek arayuzde birlestiriyor, zaten istedigimiz de bu.
    /// </summary>
    public sealed class StrokeInput : MonoBehaviour
    {
        [Tooltip("Cizime izin verilen ekran bolgesi (viewport orani, 0-1)")]
        [SerializeField] Rect drawArea = new Rect(0f, 0.22f, 1f, 0.78f);
        [SerializeField] Camera cam;
        [SerializeField] InkTrail trail;
        [SerializeField] float minPointDistance = 4f;   // piksel

        [Header("Olcek referansi")]
        [Tooltip("Cizgi uzunlugu BUNUN boyutuna gore olculur - genelde dusman.")]
        [SerializeField] Transform focus;
        [Tooltip("Odagin dunya yuksekligi; kare referans alan bunun 1.15 kati olur.")]
        [SerializeField] float focusWorldHeight = 2.9f;
        [Tooltip("Odagin merkezinin yerel yuksekligi")]
        [SerializeField] float focusCenterY = 1.45f;

        readonly List<Vector2> _screen = new List<Vector2>();
        readonly List<Vector3> _world = new List<Vector3>();
        bool _drawing;

        public bool Enabled { get; set; } = true;

        /// <summary>Cizgi tamamlandi ve siniflandirildi.</summary>
        public event System.Action<Gesture> GestureCompleted;
        /// <summary>Cizerken her karede: o ana kadarki tahmin (HUD'da canli geri bildirim).</summary>
        public event System.Action<Gesture> GesturePreview;

        void Update()
        {
            var p = Pointer.current;
            if (p == null) return;

            bool pressed = p.press.isPressed;
            Vector2 pos = p.position.ReadValue();

            if (!_drawing && pressed && Enabled && InArea(pos) && !OverUI())
            {
                _drawing = true;
                _screen.Clear(); _world.Clear();
                AddPoint(pos);
            }
            else if (_drawing && pressed)
            {
                if (_screen.Count == 0 || Vector2.Distance(_screen[_screen.Count - 1], pos) >= minPointDistance)
                {
                    AddPoint(pos);
                    if (_screen.Count >= 2) GesturePreview?.Invoke(Classify());
                }
            }
            else if (_drawing && !pressed)
            {
                _drawing = false;
                trail?.Release();
                if (_screen.Count >= 2) GestureCompleted?.Invoke(Classify());
                else GestureCompleted?.Invoke(new Gesture { Reason = "cizgi cok kisa" });
            }
        }

        void AddPoint(Vector2 screenPos)
        {
            _screen.Add(screenPos);
            if (cam != null)
            {
                var w = cam.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, -cam.transform.position.z));
                w.z = 0f;
                _world.Add(w);
                trail?.SetPoints(_world);
            }
        }

        bool InArea(Vector2 screenPos)
        {
            float x = screenPos.x / Screen.width, y = screenPos.y / Screen.height;
            return drawArea.Contains(new Vector2(x, y));
        }

        static bool OverUI()
            => EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();

        /// <summary>Ekran noktalarini siniflandiricinin uzayina cevirip Classify cagirir.
        ///
        /// Iki donusum var: (1) Unity'de Y YUKARI, siniflandirici Y ASAGI bekliyor,
        /// (2) koordinatlar cizim dikdortgenine gore yerellestiriliyor. Ayrica alt
        /// serit KAPATILIYOR (selfBandOverride: 0) cunku durus secimi burada HUD
        /// butonlariyla yapiliyor, cizim yuzeyinin altinda serit yok.
        /// </summary>
        Gesture Classify()
        {
            // Referans alan: odagin (dusmanin) etrafinda bir KARE. Boylece
            // "govdeyi bastan asagi kat eden cizgi = AGIR" oluyor - esikler
            // ekran boyutuna degil rakibin boyuna gore anlam kazaniyor.
            float side = FocusScreenSide();
            var center = FocusScreenCenter();
            float minX = center.x - side * 0.5f;
            float maxY = center.y + side * 0.5f;

            var pts = new List<Pt>(_screen.Count);
            foreach (var s in _screen) pts.Add(new Pt(s.x - minX, maxY - s.y));

            // Yandan profil cercevesinde dusman SAGDA: saplama "kisa + saga".
            return GestureClassifier.Classify(pts, side, side, RulesProvider.Get(),
                                              null, selfBandOverride: 0.0,
                                              thrustDirection: new Pt(1f, 0f));
        }

        float FocusScreenSide()
        {
            if (cam == null || focus == null) return Mathf.Min(Screen.width, Screen.height) * 0.5f;
            var a = cam.WorldToScreenPoint(focus.TransformPoint(new Vector3(0f, 0f, 0f)));
            var b = cam.WorldToScreenPoint(focus.TransformPoint(new Vector3(0f, focusWorldHeight, 0f)));
            return Mathf.Max(40f, Mathf.Abs(b.y - a.y) * 1.15f);
        }

        Vector2 FocusScreenCenter()
        {
            if (cam == null || focus == null) return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var c = cam.WorldToScreenPoint(focus.TransformPoint(new Vector3(0f, focusCenterY, 0f)));
            return new Vector2(c.x, c.y);
        }

        public void ClearTrail() => trail?.ClearNow();

        public static StrokeInput Create(Transform parent, Camera camera, InkTrail inkTrail,
                                         Rect area, Transform focusTarget)
        {
            var go = new GameObject("StrokeInput");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<StrokeInput>();
            s.cam = camera; s.trail = inkTrail; s.drawArea = area; s.focus = focusTarget;
            return s;
        }
    }
}
