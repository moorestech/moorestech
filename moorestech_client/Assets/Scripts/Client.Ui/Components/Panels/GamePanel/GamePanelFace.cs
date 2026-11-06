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
    /// 色とフェード幅は持ち主の USS が --game-panel-face・--game-panel-edge-fade（左右）・--game-panel-top-fade・--game-panel-bottom-fade で渡す。0px の辺はフェードしない
    /// The owner USS passes color and spans as --game-panel-face, --game-panel-edge-fade (sides), --game-panel-top-fade and --game-panel-bottom-fade; a 0px side does not fade
    /// </remarks>
    public sealed class GamePanelFace : VisualElement
    {
        private const int GridLineCount = 4;

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

            // 格子の線を置き、16 頂点に色、3×3 セルに三角 2 枚ずつを張る
            // Place the grid lines, color 16 vertices and span two triangles per 3x3 cell
            var width = contentRect.width;
            var height = contentRect.height;
            SetGridLines();
            var mesh = context.Allocate(GridLineCount * GridLineCount, (GridLineCount - 1) * (GridLineCount - 1) * 6);
            WriteVertices();
            WriteIndices();

            #region Internal

            void SetGridLines()
            {
                var side = Mathf.Min(_sideFade, width / 2f);
                _gridXs[0] = 0f;
                _gridXs[1] = side;
                _gridXs[2] = width - side;
                _gridXs[3] = width;
                _gridYs[0] = 0f;
                _gridYs[1] = Mathf.Min(_topFade, height);
                _gridYs[2] = Mathf.Max(height - _bottomFade, _gridYs[1]);
                _gridYs[3] = height;
            }

            // 4 辺の重みの積を α に掛ける（Web の mask-composite: intersect と同じく積を取る）
            // Multiply alpha by the product of the four edge weights (the same product as the Web mask-composite: intersect)
            void WriteVertices()
            {
                for (var row = 0; row < GridLineCount; row++)
                for (var column = 0; column < GridLineCount; column++)
                {
                    var x = _gridXs[column];
                    var y = _gridYs[row];
                    var tint = _faceColor;
                    tint.a *= Ramp(y, _topFade) * Ramp(height - y, _bottomFade) * Ramp(x, _sideFade) * Ramp(width - x, _sideFade);
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

            // 縁からの距離をフェード幅で割った重み。幅 0 の辺は常に 1（mask を掛けない辺）
            // Distance from the edge over the fade span; a zero-span side is always 1 (an unmasked side)
            float Ramp(float distance, float fade)
            {
                return fade <= 0f ? 1f : Mathf.Clamp01(distance / fade);
            }

            #endregion
        }
    }
}
