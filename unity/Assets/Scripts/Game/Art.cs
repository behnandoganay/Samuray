using UnityEngine;

namespace Samuray.Game
{
    /// <summary>Calisma aninda uretilen sprite ve materyaller.
    ///
    /// Bilerek hicbir sanat varligi kullanmiyoruz: sumi-e zaten duz siyah
    /// sekiller, dolayisiyla dikdortgen ve daireden olusan siluetler mekanigi
    /// degerlendirmek icin yeterli. Gercek sanat sonra gelir, o zaman bu sinif
    /// yerini asset'lere birakir.
    /// </summary>
    public static class Art
    {
        static Sprite _rect, _circle, _capsule;
        static Material _lineMat;

        public static readonly Color Ink   = new Color(0.106f, 0.098f, 0.086f);
        public static readonly Color Paper = new Color(0.894f, 0.882f, 0.847f);
        public static readonly Color Shu   = new Color(0.722f, 0.196f, 0.184f);
        public static readonly Color Faint = new Color(0.612f, 0.592f, 0.549f);

        public static Sprite Rect()
        {
            if (_rect != null) return _rect;
            var t = new Texture2D(2, 2) { name = "SamurayRect" };
            for (int y = 0; y < 2; y++) for (int x = 0; x < 2; x++) t.SetPixel(x, y, Color.white);
            t.Apply();
            _rect = Sprite.Create(t, new UnityEngine.Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 2f);
            _rect.name = "SamurayRect";
            return _rect;
        }

        public static Sprite Circle()
        {
            if (_circle != null) return _circle;
            const int n = 64;
            var t = new Texture2D(n, n) { name = "SamurayCircle" };
            float r = n * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                    // kenarda yumusak gecis - keskin piksel merdiveni olmasin
                    float a = Mathf.Clamp01((r - d) / 1.5f);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply();
            _circle = Sprite.Create(t, new UnityEngine.Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            _circle.name = "SamurayCircle";
            return _circle;
        }

        /// <summary>Yuvarlatilmis dikdortgen: uzuvlar cubuk gibi degil, firca
        /// darbesi gibi gorunsun diye. Uclari yumusak.</summary>
        public static Sprite Capsule()
        {
            if (_capsule != null) return _capsule;
            const int w = 16, h = 64;
            var t = new Texture2D(w, h) { name = "SamurayCapsule" };
            float r = w * 0.5f;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float dx = Mathf.Abs(x + 0.5f - r);
                    float dy = 0f;
                    if (y < r) dy = r - (y + 0.5f);
                    else if (y > h - r) dy = (y + 0.5f) - (h - r);
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    t.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01((r - d) / 1.2f)));
                }
            t.Apply();
            _capsule = Sprite.Create(t, new UnityEngine.Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), h);
            _capsule.name = "SamurayCapsule";
            return _capsule;
        }

        /// <summary>LineRenderer icin materyal. SpriteRenderer'in aksine LineRenderer
        /// varsayilan materyal atamaz; URP'de yanlis shader sessizce pembe verir.</summary>
        public static Material LineMaterial()
        {
            if (_lineMat != null) return _lineMat;
            var sh = Shader.Find("Sprites/Default")
                  ?? Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default")
                  ?? Shader.Find("Universal Render Pipeline/Unlit");
            if (sh == null)
            {
                Debug.LogError("Samuray: cizgi shader'i bulunamadi. 'Sprites/Default' veya URP " +
                               "sprite shader'i projede yok - cizgiler gorunmeyecek.");
                return null;
            }
            _lineMat = new Material(sh) { name = "SamurayLine" };
            return _lineMat;
        }

        /// <summary>Verilen ebeveynin altina bir SpriteRenderer'li cocuk olusturur.</summary>
        public static SpriteRenderer Piece(Transform parent, string name, Sprite sprite,
                                           Color color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
