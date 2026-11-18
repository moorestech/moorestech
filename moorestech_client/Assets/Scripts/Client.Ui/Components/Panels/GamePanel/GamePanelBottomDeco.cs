using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Panels
{
    /// <summary>
    /// 下部の三角3個。三角の幅と下端のフェードは持ち主の USS が --game-panel-deco-triangle-width・--game-panel-bottom-fade で渡す
    /// Three bottom triangles; the owner USS passes the triangle width and bottom fade as --game-panel-deco-triangle-width and --game-panel-bottom-fade
    /// </summary>
    public class GamePanelBottomDeco : VisualElement
    {
        // Web の .bottomDeco が持つ span の数（構造なので USS でなく C# が持つ）
        // Number of spans in the Web .bottomDeco (structure, so C# holds it rather than USS)
        private const int TriangleCount = 3;

        private readonly Gradient _gradient = new();
        private Color _triangleColor;
        private float _triangleWidth;
        private float _bottomFadeLength;
        private bool _hasTokens;

        public GamePanelBottomDeco()
        {
            AddToClassList("game-panel__bottom-deco");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawTriangles;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            // 3 つとも読めたときだけ描く。読めない名前は UssCustomStyleReader が LogError を出す
            // Draw only when all three resolve; UssCustomStyleReader logs an error for any unreadable name
            var hasColor = UssCustomStyleReader.TryReadColor(customStyle, "--game-panel-deco", out _triangleColor);
            var hasWidth = UssCustomStyleReader.TryReadPixels(customStyle, "--game-panel-deco-triangle-width", out _triangleWidth);
            var hasFade = UssCustomStyleReader.TryReadPixels(customStyle, "--game-panel-bottom-fade", out _bottomFadeLength);
            _hasTokens = hasColor && hasWidth && hasFade;
            MarkDirtyRepaint();
        }

        private void DrawTriangles(MeshGenerationContext context)
        {
            // silent-ok: トークンの解決前は描かない / 解決できなければ UssCustomStyleReader が LogError を出す
            if (!_hasTokens) return;

            // 幅不足なら三角を等分に縮める
            // Shrink triangles evenly when narrow
            var size = contentRect.size;
            var triangleWidth = Mathf.Min(_triangleWidth, size.x / TriangleCount);
            var gap = (size.x - triangleWidth * TriangleCount) / (TriangleCount - 1);
            var painter = context.painter2D;
            painter.fillColor = Color.white;
            painter.fillGradient = MakeBottomFadeGradient(size.y);
            for (var i = 0; i < TriangleCount; i++) FillTriangle(painter, i * (triangleWidth + gap), triangleWidth, size.y);

            #region Internal

            FillGradient MakeBottomFadeGradient(float height)
            {
                // 縦mask: 下端のフェード幅で透明
                // Vertical mask: clear across the bottom fade span
                var opaqueUntil = _bottomFadeLength < height ? (height - _bottomFadeLength) / height : 0f;
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
