using UnityEngine;
using UnityEngine.UIElements;

namespace Client.UitkPoc.Effects
{
    // border-style: dashed の代替。Painter2Dの破線で角丸の枠を描く
    // Stand-in for border-style: dashed, drawn with Painter2D dashes
    public class DashedBorderElement : VisualElement
    {
        public DashedBorderElement()
        {
            generateVisualContent += context =>
            {
                var painter = context.painter2D;
                var rect = contentRect;
                painter.lineWidth = 2f;
                painter.strokeColor = new Color(0.95f, 0.65f, 0.25f);
                painter.SetDashPattern(8f, 6f);
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin + 10, rect.yMin + 1));
                painter.ArcTo(new Vector2(rect.xMax - 1, rect.yMin + 1), new Vector2(rect.xMax - 1, rect.yMax - 1), 10);
                painter.ArcTo(new Vector2(rect.xMax - 1, rect.yMax - 1), new Vector2(rect.xMin + 1, rect.yMax - 1), 10);
                painter.ArcTo(new Vector2(rect.xMin + 1, rect.yMax - 1), new Vector2(rect.xMin + 1, rect.yMin + 1), 10);
                painter.ArcTo(new Vector2(rect.xMin + 1, rect.yMin + 1), new Vector2(rect.xMax - 1, rect.yMin + 1), 10);
                painter.ClosePath();
                painter.Stroke();
            };
        }
    }

    // clip-path: polygon() の代替。六角形をグラデーションで塗る
    // Stand-in for clip-path: polygon(); a hexagon filled with a gradient
    public class HexagonElement : VisualElement
    {
        public HexagonElement(Gradient gradient)
        {
            generateVisualContent += context =>
            {
                var painter = context.painter2D;
                var center = contentRect.center;
                var radius = Mathf.Min(contentRect.width, contentRect.height) / 2f - 2f;
                // 6.3のFillGradientで、左上から右下への線形グラデーションを塗る
                // Fill a top-left to bottom-right linear gradient with 6.3 FillGradient
                painter.fillGradient = FillGradient.MakeLinearGradient(gradient, contentRect.min, contentRect.max, AddressMode.Clamp);
                painter.BeginPath();
                for (var i = 0; i < 6; i++)
                {
                    var angle = Mathf.Deg2Rad * (60f * i - 90f);
                    var point = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                    if (i == 0) painter.MoveTo(point);
                    else painter.LineTo(point);
                }
                painter.ClosePath();
                painter.Fill();
            };
        }
    }

    // radial-gradient の代替。角丸矩形のパスを放射グラデーションで塗る
    // Stand-in for radial-gradient; a rounded-rect path filled radially
    public class RadialPanelElement : VisualElement
    {
        public RadialPanelElement(Color inner, Color outer)
        {
            generateVisualContent += context =>
            {
                var painter = context.painter2D;
                var rect = contentRect;
                painter.fillGradient = FillGradient.MakeRadialGradient(inner, outer, rect.center, rect.width * 0.6f, rect.center, AddressMode.Clamp);
                painter.BeginPath();
                painter.MoveTo(new Vector2(rect.xMin + 16, rect.yMin));
                painter.ArcTo(new Vector2(rect.xMax, rect.yMin), new Vector2(rect.xMax, rect.yMax), 16);
                painter.ArcTo(new Vector2(rect.xMax, rect.yMax), new Vector2(rect.xMin, rect.yMax), 16);
                painter.ArcTo(new Vector2(rect.xMin, rect.yMax), new Vector2(rect.xMin, rect.yMin), 16);
                painter.ArcTo(new Vector2(rect.xMin, rect.yMin), new Vector2(rect.xMax, rect.yMin), 16);
                painter.ClosePath();
                painter.Fill();
            };
        }
    }

    // conic-gradient の円形進捗の代替。Arcで弧を描き、値は外から押し込む
    // Stand-in for a conic-gradient progress ring; arcs drawn with Arc, value pushed from outside
    public class ProgressRingElement : VisualElement
    {
        private float _progress;

        public ProgressRingElement()
        {
            generateVisualContent += Draw;
        }

        public void SetProgress(float progress)
        {
            _progress = Mathf.Repeat(progress, 1f);
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            var painter = context.painter2D;
            var center = contentRect.center;
            var radius = Mathf.Min(contentRect.width, contentRect.height) / 2f - 8f;
            painter.lineWidth = 10f;
            painter.lineCap = LineCap.Butt;
            painter.strokeColor = new Color(1f, 1f, 1f, 0.1f);
            painter.BeginPath();
            painter.Arc(center, radius, 0f, 360f);
            painter.Stroke();

            // 進捗の弧は色を時計回りに変える（strokeGradient）
            // The progress arc shifts color clockwise (strokeGradient)
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(0.3f, 0.65f, 1f), 0f), new GradientColorKey(new Color(0.95f, 0.4f, 0.8f), 1f) }, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            painter.strokeGradient = gradient;
            painter.lineCap = LineCap.Round;
            painter.BeginPath();
            painter.Arc(center, radius, -90f, -90f + 360f * _progress);
            painter.Stroke();
        }
    }
}
