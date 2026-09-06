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

        static Sprite _grainSprite, _vignetteSprite, _sealSprite;

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
                    // Cok seyrek ve cok soluk: kagit dokusu olmali, benek degil.
                    float v = Random.value;
                    float a = v > 0.86f ? (v - 0.86f) / 0.14f * 0.055f : 0f;
                    t.SetPixel(x, y, new Color(0.1f, 0.09f, 0.08f, a));
                }
            t.Apply();
            _grainSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return _grainSprite;
        }

        /// <summary>Hanko muhru: kirmizi cerceve icinde soyut bir glif.
        /// Onceki hali duz bir dikdortgendi ve ekranda anlamsiz pembe bir kutu
        /// olarak duruyordu.</summary>
        static Sprite Seal()
        {
            if (_sealSprite != null) return _sealSprite;
            const int n = 64;
            var t = new Texture2D(n, n) { name = "SamuraySeal" };
            var red = new Color(1f, 1f, 1f, 1f);
            var clear = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    bool border = x < 5 || x >= n - 5 || y < 5 || y >= n - 5;
                    bool outside = x < 2 || x >= n - 2 || y < 2 || y >= n - 2;
                    // ic glif: iki yatay, bir dikey darbe
                    bool glyph = (y > 20 && y < 26 && x > 16 && x < 48)
                              || (y > 38 && y < 44 && x > 16 && x < 48)
                              || (x > 29 && x < 35 && y > 14 && y < 50);
                    t.SetPixel(x, y, (!outside && border) || glyph ? red : clear);
                }
            t.Apply();
            _sealSprite = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return _sealSprite;
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

            // hanko muhru - kosede kucuk kirmizi imza
            s.seal = Art.Piece(go.transform, "Seal", Seal(), Art.Shu, -88);
            s.seal.color = new Color(Art.Shu.r, Art.Shu.g, Art.Shu.b, 0.42f);
            s.seal.transform.localPosition = new Vector3(width * 0.38f, height * 0.41f, 0f);
            s.seal.transform.localScale = Vector3.one * 0.36f;

            s.vignette = Art.Piece(go.transform, "Vignette", Vignette(), new Color(0, 0, 0, 0f), 50);
            s.vignette.transform.localScale = new Vector3(width * 1.6f, height * 1.4f, 1f);
            s.vignette.enabled = false;

            return s;
        }
    }
}
