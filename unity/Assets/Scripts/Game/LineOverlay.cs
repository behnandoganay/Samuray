using System.Collections.Generic;
using UnityEngine;
using Samuray.Core;

namespace Samuray.Game
{
    /// <summary>Kesim hatlarini bir savascinin GOVDESI uzerine cizer.
    ///
    /// Iki ayri gorevde kullanilir:
    ///   Gard modu     - dusmanin uzerinde: savundugu hatlar vermilyon, digerleri soluk
    ///   Telegraf modu - senin uzerinde: gelen darbenin hayalet izi, nabiz gibi atar
    ///
    /// Metin telegrafinin yerini alan sey bu. Dusmanin niyetini cumleden degil,
    /// kendi govdende yanip sonen hattan okuyorsun.
    /// </summary>
    public sealed class LineOverlay : MonoBehaviour
    {
        [SerializeField] FighterRig rig;
        [Tooltip("Telegraf modu: sadece verilen hatlar cizilir ve nabiz atar")]
        [SerializeField] bool telegraphMode;
        [SerializeField] float guardedWidth = 0.085f;
        [SerializeField] float openWidth = 0.028f;
        [SerializeField] float telegraphWidth = 0.11f;
        [SerializeField] float pulseHz = 1.9f;

        readonly Dictionary<CutLine, LineRenderer> _lines = new Dictionary<CutLine, LineRenderer>();
        readonly HashSet<CutLine> _marked = new HashSet<CutLine>();
        bool _anyMarked;

        void LateUpdate()
        {
            if (rig == null) return;
            float pulse = telegraphMode
                ? 0.45f + 0.55f * Mathf.Abs(Mathf.Sin(Time.time * Mathf.PI * pulseHz))
                : 1f;

            foreach (var kv in _lines)
            {
                var lr = kv.Value;
                bool on = _marked.Contains(kv.Key);

                if (telegraphMode && !on) { lr.enabled = false; continue; }
                lr.enabled = true;

                // Hatlar rig ile birlikte hareket ettigi icin her karede tazeleniyor
                var (a, b) = BodyLines.World(rig, kv.Key);
                lr.SetPosition(0, a);
                lr.SetPosition(1, b);

                float w = telegraphMode ? telegraphWidth : (on ? guardedWidth : openWidth);
                lr.startWidth = w;
                lr.endWidth = w * (telegraphMode ? 0.35f : (on ? 0.45f : 0.6f));

                var col = telegraphMode ? Art.Shu : (on ? Art.Shu : Art.Faint);
                col.a = telegraphMode ? pulse : (on ? 0.95f : 0.30f);
                lr.startColor = lr.endColor = col;
            }
        }

        public void Show(IEnumerable<CutLine> lines)
        {
            _marked.Clear();
            _anyMarked = false;
            if (lines != null)
                foreach (var l in lines) { _marked.Add(l); _anyMarked = true; }
        }

        public void Clear() => Show(null);
        public bool HasAny => _anyMarked;

        public static LineOverlay Create(Transform parent, string name, FighterRig rig,
                                         bool telegraph, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<LineOverlay>();
            v.rig = rig;
            v.telegraphMode = telegraph;

            foreach (CutLine line in System.Enum.GetValues(typeof(CutLine)))
            {
                var child = new GameObject(line.ToString());
                child.transform.SetParent(go.transform, false);
                var lr = child.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.numCapVertices = 4;
                lr.material = Art.LineMaterial();
                lr.sortingOrder = sortingOrder;
                lr.enabled = false;
                v._lines[line] = lr;
            }
            return v;
        }
    }
}
