using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components
{
    /// <summary>
    /// GamePanel の半透明ネイビーの面。上端の縦フェードと左右・下端のマスクを頂点カラーの格子で描く（Web の ::before）
    /// Translucent navy face of GamePanel; draws the top fade and the side/bottom masks as a vertex-colored grid (the Web ::before)
    /// </summary>
    public class GamePanelFace : VisualElement
    {
        // Web の linear-gradient(180deg, 透明 0, 面 11.742px) と mask の calc(100% - 12px)
        // The Web linear-gradient(180deg, clear 0, face 11.742px) and the mask's calc(100% - 12px)
        private const float TopFadeLength = 11.742f;
        private const float BottomFadeLength = 12f;
        private const int GridLineCount = 4;

        private readonly float[] _gridXs = new float[GridLineCount];
        private readonly float[] _gridYs = new float[GridLineCount];
        private Color _faceColor;
        private float _edgeFade;
        private bool _hasTokens;

        public GamePanelFace()
        {
            AddToClassList("game-panel__face");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawFace;
            RegisterCallback<CustomStyleResolvedEvent>(OnCustomStyleResolved);
        }

        private void OnCustomStyleResolved(CustomStyleResolvedEvent evt)
        {
            var hasColor = UssCustomStyleReader.TryReadColor(customStyle, "--game-panel-face", out _faceColor);
            var hasEdgeFade = UssCustomStyleReader.TryReadPixels(customStyle, "--game-panel-edge-fade", out _edgeFade);
            _hasTokens = hasColor && hasEdgeFade;
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
            SetGridLines(width, height);
            var mesh = context.Allocate(GridLineCount * GridLineCount, (GridLineCount - 1) * (GridLineCount - 1) * 6);
            WriteVertices();
            WriteIndices();

            #region Internal

            void SetGridLines(float w, float h)
            {
                var sideFade = Mathf.Min(_edgeFade, w / 2f);
                _gridXs[0] = 0f;
                _gridXs[1] = sideFade;
                _gridXs[2] = w - sideFade;
                _gridXs[3] = w;
                _gridYs[0] = 0f;
                _gridYs[1] = Mathf.Min(TopFadeLength, h);
                _gridYs[2] = Mathf.Max(h - BottomFadeLength, _gridYs[1]);
                _gridYs[3] = h;
            }

            void WriteVertices()
            {
                for (var row = 0; row < GridLineCount; row++)
                for (var column = 0; column < GridLineCount; column++)
                {
                    var x = _gridXs[column];
                    var y = _gridYs[row];
                    var tint = _faceColor;
                    tint.a *= VerticalWeight(y) * HorizontalWeight(x);
                    mesh.SetNextVertex(new Vertex { position = new Vector3(x, y, Vertex.nearZ), tint = tint });
                }
            }

            void WriteIndices()
            {
                // 画面座標（y 下向き）で時計回りに張る
                // Wind clockwise in screen space (y down)
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

            // 縦は上端の立ち上がりと下端の mask、横は左右の mask。Web の mask-composite: intersect と同じく積を取る
            // Vertical is the top ramp times the bottom mask, horizontal the side masks; multiply like the Web mask-composite: intersect
            float VerticalWeight(float y)
            {
                return Mathf.Clamp01(y / TopFadeLength) * Mathf.Clamp01((height - y) / BottomFadeLength);
            }

            float HorizontalWeight(float x)
            {
                if (_edgeFade <= 0f) return 1f;
                return Mathf.Clamp01(x / _edgeFade) * Mathf.Clamp01((width - x) / _edgeFade);
            }

            #endregion
        }
    }
}
