using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Panels
{
    /// <summary>
    /// 下部の三角3個
    /// Three bottom triangles
    /// </summary>
    public class GamePanelBottomDeco : VisualElement
    {
        // Web span幅68pxとmask
        // Web span width 68px and mask
        private const float TriangleWidth = 68f;
        private const float BottomFadeLength = 12f;
        private const int TriangleCount = 3;

        private readonly Gradient _gradient = new();
        private Color _triangleColor;
        private bool _hasColor;

        public GamePanelBottomDeco()
        {
            AddToClassList("game-panel__bottom-deco");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawTriangles;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            _hasColor = UssCustomStyleReader.TryReadColor(customStyle, "--game-panel-deco", out _triangleColor);
            MarkDirtyRepaint();
        }

        private void DrawTriangles(MeshGenerationContext context)
        {
            // silent-ok: 色の解決前は描かない / 解決できなければ UssCustomStyleReader が LogError を出す
            if (!_hasColor) return;

            // 幅不足なら三角を等分に縮める
            // Shrink triangles evenly when narrow
            var size = contentRect.size;
            var triangleWidth = Mathf.Min(TriangleWidth, size.x / TriangleCount);
            var gap = (size.x - triangleWidth * TriangleCount) / (TriangleCount - 1);
            var painter = context.painter2D;
            painter.fillColor = Color.white;
            painter.fillGradient = MakeBottomFadeGradient(size.y);
            for (var i = 0; i < TriangleCount; i++) FillTriangle(painter, i * (triangleWidth + gap), triangleWidth, size.y);

            #region Internal

            FillGradient MakeBottomFadeGradient(float height)
            {
                // 縦mask: 下端12pxで透明
                // Vertical mask: clear at the bottom 12px
                var opaqueUntil = BottomFadeLength < height ? (height - BottomFadeLength) / height : 0f;
                var opaqueColor = new Color(_triangleColor.r, _triangleColor.g, _triangleColor.b, 1f);
                _gradient.SetKeys(
                    new[] { new GradientColorKey(opaqueColor, 0f), new GradientColorKey(opaqueColor, 1f) },
                    new[] { new GradientAlphaKey(_triangleColor.a, 0f), new GradientAlphaKey(_triangleColor.a, opaqueUntil), new GradientAlphaKey(0f, 1f) });
                return FillGradient.MakeLinearGradient(_gradient, Vector2.zero, new Vector2(0f, height), AddressMode.Clamp);
            }

            void FillTriangle(Painter2D painter, float left, float width, float height)
            {
                painter.BeginPath();
                painter.MoveTo(new Vector2(left, 0f));
                painter.LineTo(new Vector2(left + width, 0f));
                painter.LineTo(new Vector2(left + width / 2f, height));
                painter.ClosePath();
                painter.Fill();
            }

            #endregion
        }
    }
}
