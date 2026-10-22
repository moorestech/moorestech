using UnityEngine;

namespace Client.UitkPoc.VectorEditor.Model
{
    // 新しい図形に順番に割り当てる塗り色と、共通の線の色
    // Fill colors handed to new shapes in turn, plus the shared stroke color
    public static class VectorPalette
    {
        public static readonly Color Stroke = new(0.1f, 0.11f, 0.13f);

        private static readonly Color[] Fills =
        {
            new(0.95f, 0.65f, 0.25f), new(0.31f, 0.64f, 1f), new(0.45f, 0.8f, 0.5f),
            new(0.9f, 0.4f, 0.45f), new(0.7f, 0.5f, 0.95f), new(0.95f, 0.85f, 0.35f),
        };

        private static int _next;

        public static Color NextFill()
        {
            return Fills[_next++ % Fills.Length];
        }

        public static Color FillAt(int index)
        {
            return Fills[index % Fills.Length];
        }
    }
}
