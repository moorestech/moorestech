using Client.Ui.Components.Panels;
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
    /// 影は円と同じ大きさのガウスぼかし。円の縁から外向きの距離 d での濃さは半平面の被覆率 Φ(-d/σ)（σ=blur/2）で近似する。円の内側は白い点が覆うので描かない。α は GamePanelFaceOpacityRamp で Linear 合成用に補正する
    /// The shadow is a Gaussian blur of a circle the same size; the shade at outward distance d from the edge approximates the half-plane coverage Phi(-d/sigma) with sigma = blur/2; the white dot covers the inside, so nothing is drawn there; alpha is corrected for Linear blending with GamePanelFaceOpacityRamp
    /// </remarks>
    internal sealed class CrosshairShadowRing : VisualElement
    {
        // σ 単位の距離と Φ(-d) の表（標準正規分布の上側確率）
        // Distance in sigma and the Phi(-d) table (upper tail of the standard normal)
        private static readonly float[] DistancesInSigma = { 0f, 0.5f, 1f, 1.5f, 2f, 2.5f, 3f };
        private static readonly float[] Coverages = { 0.5f, 0.3085f, 0.1587f, 0.0668f, 0.0228f, 0.0062f, 0f };
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

        // σ を単位にした縁からの距離の被覆率。表の間は線形、3σ より外は 0
        // Coverage at a distance from the edge in sigma units; linear between rows and zero beyond 3 sigma
        internal static float CoverageAt(float distance, float sigma)
        {
            var d = distance / sigma;
            for (var i = 1; i < DistancesInSigma.Length; i++)
            {
                if (d > DistancesInSigma[i]) continue;
                var t = (d - DistancesInSigma[i - 1]) / (DistancesInSigma[i] - DistancesInSigma[i - 1]);
                return Mathf.Lerp(Coverages[i - 1], Coverages[i], t);
            }
            return 0f;
        }

        private void OnCustomStyleResolved()
        {
            var hasBlur = UssCustomStyleReader.TryReadPixels(customStyle, "--crosshair-hud-shadow-blur", out _blurRadius);
            var hasColor = UssCustomStyleReader.TryReadColor(customStyle, "--crosshair-hud-shadow-color", out _shadowColor);
            // silent-ok: blur 0px は Web でも影なし / トークンを変えた人が並べ比べで気づく
            _hasTokens = hasBlur && hasColor && _blurRadius > 0f;
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
                    tint.a = GamePanelFaceOpacityRamp.OpacityAt(_shadowColor.a, CoverageAt(distance, sigma));
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
