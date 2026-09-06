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
        [SerializeField] Rect drawArea = new Rect(0f, 0.30f, 1f, 0.70f);
        [SerializeField] Camera cam;
        [SerializeField] InkTrail trail;
        [SerializeField] float minPointDistance = 4f;   // piksel

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
            float minX = drawArea.xMin * Screen.width;
            float maxY = drawArea.yMax * Screen.height;
            float w = drawArea.width * Screen.width;
            float h = drawArea.height * Screen.height;

            var pts = new List<Pt>(_screen.Count);
            foreach (var s in _screen) pts.Add(new Pt(s.x - minX, maxY - s.y));

            return GestureClassifier.Classify(pts, w, h, RulesProvider.Get(),
                                              null, selfBandOverride: 0.0);
        }

        public void ClearTrail() => trail?.ClearNow();

        public static StrokeInput Create(Transform parent, Camera camera, InkTrail inkTrail, Rect area)
        {
            var go = new GameObject("StrokeInput");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<StrokeInput>();
            s.cam = camera; s.trail = inkTrail; s.drawArea = area;
            return s;
        }
    }
}
