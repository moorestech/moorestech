using UnityEngine;

namespace Client.Tests.Ui.Screens.ModeHud
{
    // WCAG のコントラスト比（sRGB）。半透明の面は背景に重ねてから測る
    // WCAG contrast ratio (sRGB); translucent faces are composited over the backdrop first
    public static class ContrastRatio
    {
        public static float Of(Color foreground, Color background)
        {
            var a = Luminance(foreground) + 0.05f;
            var b = Luminance(background) + 0.05f;
            return Mathf.Max(a, b) / Mathf.Min(a, b);
        }

        public static Color Over(Color face, Color backdrop)
        {
            return new Color(Mathf.Lerp(backdrop.r, face.r, face.a), Mathf.Lerp(backdrop.g, face.g, face.a), Mathf.Lerp(backdrop.b, face.b, face.a), 1f);
        }

        private static float Luminance(Color color)
        {
            return 0.2126f * Linear(color.r) + 0.7152f * Linear(color.g) + 0.0722f * Linear(color.b);
        }

        private static float Linear(float channel)
        {
            return channel <= 0.03928f ? channel / 12.92f : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }
    }
}
