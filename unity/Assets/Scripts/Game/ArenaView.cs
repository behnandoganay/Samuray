using System.Collections.Generic;
using UnityEngine;
using Samuray.Core;

namespace Samuray.Game
{
    /// <summary>Bes kesim hattini cizer ve dusmanin savundugu hatlari vermilyona boyar.
    ///
    /// Oyunun strateji katmani tek bakista buradan okunuyor: kirmizi hat "oradan
    /// vurursan karsilar" demek.
    /// </summary>
    public sealed class ArenaView : MonoBehaviour
    {
        [SerializeField] float radius = 3.4f;
        [SerializeField] float openWidth = 0.06f;
        [SerializeField] float guardedWidth = 0.16f;

        readonly Dictionary<CutLine, LineRenderer> _lines = new Dictionary<CutLine, LineRenderer>();

        public float Radius => radius;

        /// <summary>Bir hattin dunya uzerindeki baslangic/bitis noktalari.
        /// BeatAnimator savurma animasyonunu bu segment boyunca oynatir.</summary>
        public (Vector3 from, Vector3 to) Segment(CutLine line)
        {
            Vector3 c = transform.position;
            float r = radius;
            switch (line)
            {
                case CutLine.SHOMEN:     return (c + new Vector3(0f, r * 1.05f), c + new Vector3(0f, -r * 0.55f));
                case CutLine.KESA:       return (c + new Vector3(-r * 0.90f, r * 0.80f), c + new Vector3(r * 0.90f, -r * 0.80f));
                case CutLine.GYAKU_KESA: return (c + new Vector3(-r * 0.90f, -r * 0.80f), c + new Vector3(r * 0.90f, r * 0.80f));
                case CutLine.YOKO:       return (c + new Vector3(-r * 1.02f, -r * 0.12f), c + new Vector3(r * 1.02f, -r * 0.12f));
                default:                 return (c + new Vector3(0f, -r * 1.10f), c + new Vector3(0f, -r * 0.70f));
            }
        }

        /// <summary>Hangi hatlarin savunuldugunu isaretle.</summary>
        public void SetGuarded(IEnumerable<CutLine> guarded)
        {
            var set = new HashSet<CutLine>(guarded);
            foreach (var kv in _lines)
            {
                bool on = set.Contains(kv.Key);
                var lr = kv.Value;
                lr.startColor = lr.endColor = on ? Art.Shu : Art.Faint;
                // Firca hissi: kalindan inceye - ayni zamanda kesimin yonunu soyler
                lr.startWidth = on ? guardedWidth : openWidth;
                lr.endWidth = (on ? guardedWidth : openWidth) * 0.30f;
            }
        }

        void Build()
        {
            foreach (CutLine line in System.Enum.GetValues(typeof(CutLine)))
            {
                var go = new GameObject(line.ToString());
                go.transform.SetParent(transform, false);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.positionCount = 2;
                lr.numCapVertices = 4;
                lr.material = Art.LineMaterial();
                lr.sortingOrder = 2;
                var (a, b) = Segment(line);
                lr.SetPosition(0, a);
                lr.SetPosition(1, b);
                _lines[line] = lr;
            }
        }

        public static ArenaView Create(Transform parent, Vector3 center, float radius)
        {
            var go = new GameObject("Arena");
            go.transform.SetParent(parent, false);
            go.transform.position = center;
            var v = go.AddComponent<ArenaView>();
            v.radius = radius;
            v.Build();
            v.SetGuarded(new CutLine[0]);
            return v;
        }
    }
}
