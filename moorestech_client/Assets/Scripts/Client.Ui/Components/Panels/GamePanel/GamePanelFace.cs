using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Panels
{
    /// <summary>
    /// パネルの半透明の面。上下左右の縁を透明へ落とすフェードを頂点カラーの格子で描く（Web の ::before とその mask）
    /// Translucent panel face; draws the fades of all four edges into clear as a vertex-colored grid (the Web ::before and its mask)
    /// </summary>
    /// <remarks>
    /// 色とフェード幅は持ち主の USS が --game-panel-face・--game-panel-edge-fade（左右）・--game-panel-top-fade・--game-panel-bottom-fade で渡す。0px の辺はフェードしない。
    /// フェードの帯は細かく刻み、各頂点の α を GamePanelFaceOpacityRamp で決める（頂点間の線形補間で Web の mask と同じ濃さの並びにするため）。
    /// The owner USS passes color and spans as --game-panel-face, --game-panel-edge-fade (sides), --game-panel-top-fade and --game-panel-bottom-fade; a 0px side does not fade.
    /// Fade bands are finely subdivided and each vertex alpha comes from GamePanelFaceOpacityRamp, so linear interpolation between vertices keeps the Web mask's run of shades.
    /// </remarks>
    internal sealed class GamePanelFace : VisualElement
    {
        // フェード 1 本を刻む数。隅の 2 辺の積の曲面を 8 段で 1 段未満に近似する
        // Segments per fade span; 8 keeps the corner product of two sides within one shade
        private const int SegmentsPerFade = 8;
        private const int GridLineCount = (SegmentsPerFade + 1) * 2;

        private readonly float[] _gridXs = new float[GridLineCount];
        private readonly float[] _gridYs = new float[GridLineCount];
        private Color _faceColor;
        private float _sideFade;
        private float _topFade;
        private float _bottomFade;
        private bool _hasTokens;

        public GamePanelFace(string ownerClassName)
        {
            AddToClassList("game-panel-face");
            AddToClassList(ownerClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawFace;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            var hasColor = UssCustomStyleReader.TryReadColor(customStyle, "--game-panel-face", out _faceColor);
            var hasSide = UssCustomStyleReader.TryReadPixels(customStyle, "--game-panel-edge-fade", out _sideFade);
            var hasTop = UssCustomStyleReader.TryReadPixels(customStyle, "--game-panel-top-fade", out _topFade);
            var hasBottom = UssCustomStyleReader.TryReadPixels(customStyle, "--game-panel-bottom-fade", out _bottomFade);
            _hasTokens = hasColor && hasSide && hasTop && hasBottom;
            MarkDirtyRepaint();
        }

        private void DrawFace(MeshGenerationContext context)
        {
            // silent-ok: トークンが読めない間は描かない / UssCustomStyleReader が読めなかったトークン名を LogError で出す
            if (!_hasTokens) return;

            // 格子線・頂点色・セル三角を張る
            // Place grid lines, vertex colors and cell triangles
            var width = contentRect.width;
            var height = contentRect.height;
            SetGridLines();
            var mesh = context.Allocate(GridLineCount * GridLineCount, (GridLineCount - 1) * (GridLineCount - 1) * 6);
            WriteVertices();
            WriteIndices();

            #region Internal

            // 前半はフェードの始まり側の帯、後半は終わり側の帯を等分する。幅 0 の帯は同じ位置に重なり面積 0 のセルになる
            // The first half splits the leading band and the second half the trailing band; a zero-span band collapses into zero-area cells
            void SetGridLines()
            {
                var side = Mathf.Min(_sideFade, width / 2f);
                var top = Mathf.Min(_topFade, height);
                var bottomStart = Mathf.Max(height - _bottomFade, top);
                for (var i = 0; i <= SegmentsPerFade; i++)
                {
                    var step = (float)i / SegmentsPerFade;
                    _gridXs[i] = side * step;
                    _gridXs[SegmentsPerFade + 1 + i] = Mathf.Lerp(width - side, width, step);
                    _gridYs[i] = top * step;
                    _gridYs[SegmentsPerFade + 1 + i] = Mathf.Lerp(bottomStart, height, step);
                }
            }

            // 頂点 α は縁の mask を GamePanelFaceOpacityRamp で掛けて決める
            // GamePanelFaceOpacityRamp decides vertex alpha by applying the edge mask
            void WriteVertices()
            {
                for (var row = 0; row < GridLineCount; row++)
                for (var column = 0; column < GridLineCount; column++)
                {
                    var x = _gridXs[column];
                    var y = _gridYs[row];
                    var tint = _faceColor;
                    tint.a = GamePanelFaceOpacityRamp.VertexAlpha(_faceColor.a, x, y, width, height, _sideFade, _topFade, _bottomFade);
                    mesh.SetNextVertex(new Vertex { position = new Vector3(x, y, Vertex.nearZ), tint = tint });
                }
            }

            // 画面座標（y 下向き）で時計回りに張る
            // Wind clockwise in screen space (y down)
            void WriteIndices()
            {
                for (var row = 0; row < GridLineCount - 1; row++)
                for (var column = 0; column < GridLineCount - 1; column++)
                {
                    var topLeft = (ushort)(row * GridLineCount + column);
                    var topRight = (ushort)(topLeft + 1);
                    var bottomLeft = (ushort)(topLeft + GridLineCount);
                    var bottomRight = (ushort)(bottomLeft + 1);
                    mesh.SetNextIndex(topLeft);
                    mesh.SetNextIndex(topRight);
                    mesh.SetNextIndex(bottomRight);
                    mesh.SetNextIndex(topLeft);
                    mesh.SetNextIndex(bottomRight);
                    mesh.SetNextIndex(bottomLeft);
                }
            }

            #endregion
        }
    }
}
