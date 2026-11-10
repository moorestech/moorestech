using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.CommonHud
{
    /// <summary>
    /// クロスヘアの円の外側に box-shadow 0 0 blur color 相当のぼかした影を輪の頂点カラーで描く
    /// Draws the blurred shadow of box-shadow 0 0 blur color outside the crosshair circle as a vertex-colored ring
    /// </summary>
    /// <remarks>
    /// 影は円と同じ大きさのガウスぼかし。円の縁から外向きの距離 d での濃さは半平面の被覆率 Φ(-d/σ)（σ=blur/2。Foundation の GaussianEdgeCoverage）で近似する。円の内側は白い点が覆うので描かない。α は色の α に被覆率を掛ける
    /// The shadow is a Gaussian blur of a circle the same size; the shade at outward distance d from the edge approximates the half-plane coverage Phi(-d/sigma) with sigma = blur/2 (Foundation GaussianEdgeCoverage); the white dot covers the inside, so nothing is drawn there; alpha is the colour alpha times the coverage
    /// </remarks>
    internal sealed class CrosshairShadowRing : VisualElement
    {
        // 輪を置く縁からの距離（σ 単位）。3σ より外の被覆率は 0.0013 で 8 bit の α に出ない
        // Ring distances from the edge in sigma; coverage beyond 3 sigma is 0.0013 and does not show in an 8-bit alpha
        private static readonly float[] DistancesInSigma = { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f };
        private const int Segments = 32;

        private float _blurRadius;
        private Color _shadowColor;
        private bool _hasTokens;

        public CrosshairShadowRing()
        {
            AddToClassList("crosshair-hud__shadow");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawRing;
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
        }

        private void OnCustomStyleResolved()
        {
            var hasBlur = UssCustomStyleReader.TryReadPixels(customStyle, "--crosshair-hud-shadow-blur", out _blurRadius);
            var hasColor = UssCustomStyleReader.TryReadColor(customStyle, "--crosshair-hud-shadow-color", out _shadowColor);
            // silent-ok: blur 0px は Web でも影なし / トークンを変えた人が並べ比べで気づく
            _hasTokens = hasBlur && hasColor && 0f < _blurRadius;
            MarkDirtyRepaint();
        }

        private void DrawRing(MeshGenerationContext context)
        {
            // silent-ok: トークンが読めない間は描かない / 読めなかった名前は UssCustomStyleReader が LogError で出す
            if (!_hasTokens) return;
            var center = contentRect.center;
            var radius = Mathf.Min(contentRect.width, contentRect.height) / 2f;
            // silent-ok: 寸法 0 はレイアウト前の一瞬だけ / 寸法が決まると UITK が描き直す
            if (radius <= 0f) return;

            var sigma = _blurRadius / 2f;
            var rings = DistancesInSigma.Length;
            var mesh = context.Allocate(Segments * rings, Segments * (rings - 1) * 6);
            WriteVertices();
            WriteIndices();

            #region Internal

            // 縁から外へ表の距離ごとに 1 周ずつ、被覆率の α で置く
            // One loop per table distance outward from the edge, with the coverage as alpha
            void WriteVertices()
            {
                for (var ring = 0; ring < rings; ring++)
                {
                    var distance = DistancesInSigma[ring] * sigma;
                    var tint = _shadowColor;
                    tint.a = _shadowColor.a * GaussianEdgeCoverage.At(distance, sigma);
                    for (var segment = 0; segment < Segments; segment++)
                    {
                        var angle = segment * Mathf.PI * 2f / Segments;
                        var position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius + distance);
                        mesh.SetNextVertex(new Vertex { position = new Vector3(position.x, position.y, Vertex.nearZ), tint = tint });
                    }
                }
            }

            // GamePanelFace と同じく画面座標（y 下向き）で時計回りに張る。角度 0 で外周は右・次の角度は下
            // Wind clockwise in screen space (y down) like GamePanelFace; at angle 0 outer is right and next is down
            void WriteIndices()
            {
                for (var ring = 0; ring < rings - 1; ring++)
                for (var segment = 0; segment < Segments; segment++)
                {
                    var next = (segment + 1) % Segments;
                    var inner = (ushort)(ring * Segments + segment);
                    var innerNext = (ushort)(ring * Segments + next);
                    var outer = (ushort)((ring + 1) * Segments + segment);
                    var outerNext = (ushort)((ring + 1) * Segments + next);
                    mesh.SetNextIndex(inner);
                    mesh.SetNextIndex(outerNext);
                    mesh.SetNextIndex(innerNext);
                    mesh.SetNextIndex(inner);
                    mesh.SetNextIndex(outer);
                    mesh.SetNextIndex(outerNext);
                }
            }

            #endregion
        }
    }
}
