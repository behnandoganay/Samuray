using UnityEngine;

namespace Samuray.Game
{
    /// <summary>Kamera sarsintisi. Siddet ustel olarak soner.
    ///
    /// Kameranin taban konumunu Awake'te sakliyor ve her karede ofset uyguluyor;
    /// boylece sarsinti kameranin gercek konumunu kalici olarak bozmuyor.
    /// </summary>
    public sealed class CameraShake : MonoBehaviour
    {
        [Tooltip("Saniyede kalan siddet orani - kucuk deger = hizli sonum")]
        [SerializeField] float decayPerSecond = 0.0025f;
        [SerializeField] float cutoff = 0.004f;

        Vector3 _base;
        float _magnitude;

        void Awake() => _base = transform.localPosition;

        public void Shake(float magnitude) => _magnitude = Mathf.Max(_magnitude, magnitude);

        void LateUpdate()
        {
            if (_magnitude <= 0f) return;
            _magnitude *= Mathf.Pow(decayPerSecond, Time.deltaTime);
            if (_magnitude < cutoff) { _magnitude = 0f; transform.localPosition = _base; return; }
            transform.localPosition = _base + (Vector3)(Random.insideUnitCircle * _magnitude);
        }
    }
}
