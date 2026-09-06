using UnityEngine;

namespace Samuray.Game
{
    /// <summary>Sahnenin kagit ve murekkep dokusu.
    ///
    /// Ust bosluk asili bir kakemono parsomeni gibi bos birakiliyor - sumi-e'de
    /// bosluk (ma) kompozisyonun parcasi, doldurulacak bir eksik degil.
    /// Zemin tek bir firca darbesi. Sayac daralirken kenarlar kapaniyor.
    /// </summary>
    public sealed class DuelStage : MonoBehaviour
    {
        [SerializeField] SpriteRenderer paper, grain, ground, seal, vignette;
        [SerializeField] float groundY = -3.2f;

        static Sprite _grainSprite, _vignetteSprite;

        /// <param name="tension">0 = sakin, 1 = sure doldu. Kenarlarin kapanmasi.</param>
        public void SetTension(float tension)
        {
            if (vignette == null) return;
            float k = Mathf.Clamp01((tension - 0.35f) / 0.65f);
            var c = Color.black; c.a = k * 0.45f;
            vignette.color = c;
            vignette.enabled = k > 0.01f;
        }

        static Sprite Grain()
        {
            if (_grainSprite != null) return _grainSprite;
            const int n = 128;
            var t = new Texture2D(n, n) { name = "SamurayGrain", wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float v = Random.value;
                    t.SetPixel(x, y, new Color(0.1f, 0.09f, 0.08f, v * v * v * 0.16f));
                }
            t.Apply();
            _grainSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return _grainSprite;
        }

        static Sprite Vignette()
        {
            if (_vignetteSprite != null) return _vignetteSprite;
            const int n = 128;
            var t = new Texture2D(n, n) { name = "SamurayVignette" };
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), new Vector2(r, r)) / r;
                    float a = Mathf.Clamp01((d - 0.45f) / 0.75f);
                    t.SetPixel(x, y, new Color(1, 1, 1, a * a));
                }
            t.Apply();
            _vignetteSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return _vignetteSprite;
        }

        public static DuelStage Create(Transform parent, float width, float height, float groundY)
        {
            var go = new GameObject("Stage");
            go.transform.SetParent(parent, false);
            var s = go.AddComponent<DuelStage>();
            s.groundY = groundY;

            s.paper = Art.Piece(go.transform, "Paper", Art.Rect(), Art.Paper, -100);
            s.paper.transform.localScale = new Vector3(width * 1.2f, height * 1.2f, 1f);

            s.grain = Art.Piece(go.transform, "Grain", Grain(), Color.white, -99);
            s.grain.transform.localScale = new Vector3(width * 1.2f, height * 1.2f, 1f);

            // zemin: tek firca darbesi, uclari inceliyor
            s.ground = Art.Piece(go.transform, "Ground", Art.Capsule(), Art.Ink, -90);
            s.ground.color = new Color(Art.Ink.r, Art.Ink.g, Art.Ink.b, 0.55f);
            s.ground.transform.localPosition = new Vector3(0f, groundY, 0f);
            s.ground.transform.localRotation = Quaternion.Euler(0, 0, 90f);
            s.ground.transform.localScale = new Vector3(0.10f, width * 0.92f, 1f);

            // hanko muhru - kosede kirmizi imza
            s.seal = Art.Piece(go.transform, "Seal", Art.Rect(), Art.Shu, -88);
            s.seal.color = new Color(Art.Shu.r, Art.Shu.g, Art.Shu.b, 0.75f);
            s.seal.transform.localPosition = new Vector3(width * 0.36f, height * 0.40f, 0f);
            s.seal.transform.localScale = new Vector3(0.42f, 0.52f, 1f);

            s.vignette = Art.Piece(go.transform, "Vignette", Vignette(), new Color(0, 0, 0, 0f), 50);
            s.vignette.transform.localScale = new Vector3(width * 1.6f, height * 1.4f, 1f);
            s.vignette.enabled = false;

            return s;
        }
    }
}
