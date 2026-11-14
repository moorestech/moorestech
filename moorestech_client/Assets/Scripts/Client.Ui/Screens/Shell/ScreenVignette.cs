using Client.Ui.Foundation.Theme;
using Client.Ui.Foundation.Theme.Mesh;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Shell
{
    /// <summary>
    /// 実画面の四辺を沈める常時のヴィネット。楕円のグラデを頂点カラーの格子で描く（Web の .viewport の radial-gradient）
    /// Persistent vignette that darkens every viewport edge, drawn as a vertex-colored grid of the elliptical gradient (Web .viewport radial-gradient)
    /// </summary>
    /// <remarks>
    /// 形と色は Shell.uss が --screen-shell-vignette-* で受け直したトークンから読む。頂点の α は縁の色の α に重みを掛ける
    /// Shape and color come from tokens Shell.uss re-declares as --screen-shell-vignette-*; vertex alpha is the edge colour alpha times the weight
    /// </remarks>
    internal sealed class ScreenVignette : VisualElement
    {
        // 格子の分割。2560×1080 でもセルは 80px 角で、双線形の補間と式の差は 1 段未満
        // Grid divisions; cells stay 80px square even at 2560x1080, keeping bilinear interpolation within one shade of the formula
        private const int Columns = 32;
        private const int Rows = 18;
        private const int Stride = Columns + 1;

        private ScreenVignetteShape _shape;
        private Color _edgeColor;
        private bool _hasTokens;

        public ScreenVignette()
        {
            name = "screen-vignette";
            AddToClassList("screen-shell__vignette");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawVignette;
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
        }

        private void OnCustomStyleResolved()
        {
            // 読めない値は UssCustomStyleReader が LogError で知らせ、そのときは描かない
            // UssCustomStyleReader reports unreadable values with LogError, and then nothing is drawn
            var hasWidth = UssCustomStyleReader.TryReadPercent(customStyle, "--screen-shell-vignette-width", out var widthRatio);
            var hasHeight = UssCustomStyleReader.TryReadPercent(customStyle, "--screen-shell-vignette-height", out var heightRatio);
            var hasCenterX = UssCustomStyleReader.TryReadPercent(customStyle, "--screen-shell-vignette-center-x", out var centerX);
            var hasCenterY = UssCustomStyleReader.TryReadPercent(customStyle, "--screen-shell-vignette-center-y", out var centerY);
            var hasClearStop = UssCustomStyleReader.TryReadPercent(customStyle, "--screen-shell-vignette-clear-stop", out var clearStop);
            var hasEdge = UssCustomStyleReader.TryReadColor(customStyle, "--screen-shell-vignette-edge", out _edgeColor);
            _shape = new ScreenVignetteShape(widthRatio, heightRatio, centerX, centerY, clearStop);
            _hasTokens = hasWidth && hasHeight && hasCenterX && hasCenterY && hasClearStop && hasEdge;
            MarkDirtyRepaint();
        }

        private void DrawVignette(MeshGenerationContext context)
        {
            // silent-ok: トークンが読めない間は描かない / 読めなかった名前は UssCustomStyleReader（TryReadPercent・TryReadColor）が LogError で出す
            if (!_hasTokens) return;
            var width = contentRect.width;
            var height = contentRect.height;
            // silent-ok: 寸法 0 はレイアウト前の一瞬だけ / 寸法が決まると UITK が描き直す
            if (width <= 0f || height <= 0f) return;

            var mesh = context.Allocate(Stride * (Rows + 1), Columns * Rows * 6);
            WriteVertices();
            WriteIndices();

            #region Internal

            // 縁の重みは Web の式で α に掛ける
            // Edge weight from the Web formula multiplied into alpha
            void WriteVertices()
            {
                for (var row = 0; row <= Rows; row++)
                for (var column = 0; column <= Columns; column++)
                {
                    var x = width * column / Columns;
                    var y = height * row / Rows;
                    var tint = _edgeColor;
                    tint.a = _edgeColor.a * _shape.EdgeWeightAt(x, y, width, height);
                    mesh.SetNextVertex(new Vertex { position = new Vector3(x, y, Vertex.nearZ), tint = tint });
                }
            }

            // 画面座標（y 下向き）で時計回りに張る（GamePanelFace と同じ向き）
            // Wind clockwise in screen space (y down), the same as GamePanelFace
            void WriteIndices()
            {
                GridMeshIndexWriter.WriteQuads(mesh, Rows, Columns, Stride);
            }

            #endregion
        }
    }
}
