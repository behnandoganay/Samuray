using System.Collections.Generic;
using UnityEngine;

namespace Samuray.Game
{
    /// <summary>Cizerken parmagi takip eden murekkep izi.
    ///
    /// Firca hissi icin uc inceliyor. Parmak kalktiginda iz solup kayboluyor -
    /// jest kabul edildiyse hayaleti kaliyor demek degil, o ayri.
    /// </summary>
    public sealed class InkTrail : MonoBehaviour
    {
        [SerializeField] float width = 0.16f;
        [SerializeField] float fadeSpeed = 3.5f;

        LineRenderer _lr;
        float _alpha = 1f;
        bool _fading;

        void Awake()
        {
            _lr = GetComponent<LineRenderer>();
            if (_lr == null) _lr = gameObject.AddComponent<LineRenderer>();
            _lr.useWorldSpace = true;
            _lr.numCapVertices = 6;
            _lr.material = Art.LineMaterial();
            _lr.sortingOrder = 20;
            _lr.startWidth = width;
            _lr.endWidth = width * 0.35f;
            _lr.positionCount = 0;
        }

        void Update()
        {
            if (!_fading) return;
            _alpha -= fadeSpeed * Time.deltaTime;
            if (_alpha <= 0f) { _lr.positionCount = 0; _fading = false; _alpha = 1f; return; }
            var c = Art.Ink; c.a = _alpha;
            _lr.startColor = _lr.endColor = c;
        }

        public void SetPoints(IReadOnlyList<Vector3> pts)
        {
            _fading = false; _alpha = 1f;
            _lr.startColor = _lr.endColor = Art.Ink;
            _lr.positionCount = pts.Count;
            for (int i = 0; i < pts.Count; i++) _lr.SetPosition(i, pts[i]);
        }

        public void Release() => _fading = _lr.positionCount > 0;

        public void ClearNow() { _lr.positionCount = 0; _fading = false; _alpha = 1f; }

        public static InkTrail Create(Transform parent)
        {
            var go = new GameObject("InkTrail");
            go.transform.SetParent(parent, false);
            return go.AddComponent<InkTrail>();
        }
    }
}
