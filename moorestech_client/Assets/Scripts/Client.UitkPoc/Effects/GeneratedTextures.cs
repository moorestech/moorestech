using UnityEngine;

namespace Client.UitkPoc.Effects
{
    // CSSのグラデーションや影がUSSに無いため、実行時にテクスチャを焼いて背景画像として使う
    // USS lacks CSS gradients and shadows, so textures are baked at runtime and used as backgrounds
    public static class GeneratedTextures
    {
        public static Texture2D LinearGradient(Color from, Color to, bool horizontal)
        {
            // 256x1の帯を引き伸ばせば任意サイズの線形グラデーションになる
            // A 256x1 strip stretched to any size acts as a linear gradient
            var texture = horizontal ? new Texture2D(256, 1) : new Texture2D(1, 256);
            texture.wrapMode = TextureWrapMode.Clamp;
            for (var i = 0; i < 256; i++)
            {
                var color = Color.Lerp(from, to, i / 255f);
                if (horizontal) texture.SetPixel(i, 0, color);
                else texture.SetPixel(0, 255 - i, color);
            }
            texture.Apply();
            return texture;
        }

        public static Texture2D RadialGlow(Color color, int size)
        {
            var texture = new Texture2D(size, size) { wrapMode = TextureWrapMode.Clamp };
            var center = (size - 1) / 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                    var alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.2f);
                    texture.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
                }
            }
            texture.Apply();
            return texture;
        }

        // 角丸矩形の符号付き距離から、ぼけた影を解析的に作る。9スライスで任意サイズに伸ばす前提
        // Analytic blurred shadow from a rounded-rect signed distance; meant to be 9-sliced to any size
        public static Texture2D SoftShadow(int size, float radius, float blur, Color color)
        {
            var texture = new Texture2D(size, size) { wrapMode = TextureWrapMode.Clamp };
            var half = size / 2f;
            var boxHalf = half - blur;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2(Mathf.Abs(x + 0.5f - half), Mathf.Abs(y + 0.5f - half));
                    var q = p - new Vector2(boxHalf - radius, boxHalf - radius);
                    var distance = new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - radius;
                    var alpha = 1f - Mathf.SmoothStep(-blur * 0.5f, blur, distance);
                    texture.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * alpha));
                }
            }
            texture.Apply();
            return texture;
        }
    }
}
