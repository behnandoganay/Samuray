using System.Collections.Generic;
using UnityEngine;

namespace Samuray.Game
{
    /// <summary>Yaraya sicrayan murekkep.
    ///
    /// ParticleSystem yerine kucuk bir SpriteRenderer havuzu: murekkep lekesi
    /// gorunumu icin daha dogrudan kontrol veriyor ve sumi-e'nin duz siyah/kirmizi
    /// dilinden cikmiyor.
    /// </summary>
    public sealed class InkSplatter : MonoBehaviour
    {
        [SerializeField] int poolSize = 48;
        [SerializeField] float gravity = 9f;
        [SerializeField] float lifeSeconds = 0.9f;

        sealed class Dab
        {
            public SpriteRenderer sr;
            public Vector2 vel;
            public float life;
        }

        readonly List<Dab> _pool = new List<Dab>();

        void Awake()
        {
            for (int i = 0; i < poolSize; i++)
            {
                var sr = Art.Piece(transform, "Dab" + i, Art.Circle(), Art.Shu, 15);
                sr.enabled = false;
                _pool.Add(new Dab { sr = sr, life = 0f });
            }
        }

        /// <param name="power">1 = tek yara, buyudukce daha genis ve daha hizli sacilir</param>
        public void Burst(Vector3 worldPos, int count, float power)
        {
            int spawned = 0;
            foreach (var d in _pool)
            {
                if (spawned >= count) break;
                if (d.life > 0f) continue;
                float a = Random.value * Mathf.PI * 2f;
                float sp = (1.5f + Random.value * 4.5f) * power;
                d.vel = new Vector2(Mathf.Cos(a) * sp, Mathf.Sin(a) * sp + 1.2f);
                d.life = lifeSeconds * (0.6f + Random.value * 0.6f);
                d.sr.transform.position = worldPos;
                d.sr.transform.localScale = Vector3.one * (0.10f + Random.value * 0.26f * power);
                d.sr.enabled = true;
                spawned++;
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            foreach (var d in _pool)
            {
                if (d.life <= 0f) continue;
                d.life -= dt;
                if (d.life <= 0f) { d.sr.enabled = false; continue; }
                d.vel.y -= gravity * dt;
                d.sr.transform.position += (Vector3)(d.vel * dt);
                var c = Art.Shu;
                c.a = Mathf.Clamp01(d.life / lifeSeconds);
                d.sr.color = c;
            }
        }

        public void ClearAll()
        {
            foreach (var d in _pool) { d.life = 0f; d.sr.enabled = false; }
        }

        public static InkSplatter Create(Transform parent)
        {
            var go = new GameObject("InkSplatter");
            go.transform.SetParent(parent, false);
            return go.AddComponent<InkSplatter>();
        }
    }
}
