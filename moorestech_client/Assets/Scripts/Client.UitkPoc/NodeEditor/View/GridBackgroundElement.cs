using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.NodeEditor.View
{
    // 無限キャンバスの方眼。ビュー座標で描き、細かくなりすぎたら5倍刻みに間引く
    // Infinite-canvas grid drawn in viewport space, thinned by 5x when lines get too dense
    public class GridBackgroundElement : VisualElement
    {
        private const float MinPixelSpacing = 10f;
        private static readonly Color MinorColor = new(1f, 1f, 1f, 0.04f);
        private static readonly Color MajorColor = new(1f, 1f, 1f, 0.09f);

        private readonly float _gridSize;
        private Vector2 _translation;
        private float _scale = 1f;

        public GridBackgroundElement(float gridSize)
        {
            _gridSize = gridSize;
            pickingMode = PickingMode.Ignore;
            AddToClassList("ne-grid");
            generateVisualContent += Draw;
        }

        public void SetView(Vector2 translation, float scale)
        {
            _translation = translation;
            _scale = scale;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var spacing = _gridSize * _scale;
            while (spacing < MinPixelSpacing) spacing *= 5f;
            var painter = context.painter2D;
            painter.lineWidth = 1f;
            var size = contentRect.size;
            DrawAxis(painter, spacing, _translation.x, size.x, size.y, true);
            DrawAxis(painter, spacing, _translation.y, size.y, size.x, false);
        }

        private static void DrawAxis(Painter2D painter, float spacing, float offset, float length, float span, bool vertical)
        {
            // 5本に1本を太線にする。原点からの通し番号で決めるのでパンしても太線の位置は動かない
            // Every fifth line is major, counted from the origin so majors stay put while panning
            var first = Mathf.Floor(-offset / spacing);
            for (var i = first; i * spacing + offset <= length; i++)
            {
                var p = i * spacing + offset;
                painter.strokeColor = Mathf.RoundToInt(i) % 5 == 0 ? MajorColor : MinorColor;
                painter.BeginPath();
                painter.MoveTo(vertical ? new Vector2(p, 0) : new Vector2(0, p));
                painter.LineTo(vertical ? new Vector2(p, span) : new Vector2(span, p));
                painter.Stroke();
            }
        }
    }
}
