using System.Collections.Generic;
using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Panels
{
    /// <summary>
    /// Mantine Modal の影を頂点カラーで描く
    /// Draws the Mantine Modal shadow with vertex colors
    /// </summary>
    /// <remarks>
    /// 層ごとに枠の矩形を y だけ下げ spread だけ広げた形を σ=blur/2 のガウスでぼかす。矩形のぼかしは横と縦の被覆率の積
    /// (Φ((x-x0)/σ)-Φ((x-x1)/σ))×(同じ式の y) で厳密に出るので、縁の前後 3σ を σ/2 刻みにした格子の頂点に置く。
    /// 角丸は捨てる（xl の層 2・3 は spread -7px が半径 4px を消し、層 1 は blur 3px で角の差が 1px に収まる）。
    /// 縁の内側も描く（層 2 は形の下端が枠の下端より 29px 下にあり、外向きの輪だけでは枠と形の間が抜ける）。枠の内側は面が覆う。
    /// α は LinearBlendOpacity で Linear 合成用に補正する（前例 CrosshairShadowRing）
    /// Each layer blurs the frame rect moved down by y and grown by spread with a Gaussian of sigma = blur/2. A blurred rect is
    /// exactly the product of the horizontal and vertical coverages (Phi((x-x0)/sigma)-Phi((x-x1)/sigma)) x (the same in y), so it is
    /// sampled on a grid within 3 sigma of each edge at sigma/2 steps. Corner radii are dropped (in xl, spread -7px removes the 4px
    /// radius of layers 2 and 3, and layer 1's 3px blur keeps the corner difference within 1px). The inside of the edge is drawn too
    /// (layer 2's shape ends 29px below the frame, so an outward-only ring would leave a gap); the surface covers the frame's inside.
    /// Alpha is corrected for Linear blending with LinearBlendOpacity (precedent CrosshairShadowRing)
    /// </remarks>
    internal sealed class DialogFrameShadow : VisualElement
    {
        public const string UssClassName = "dialog-frame__shadow";
        private const int LayerCount = 3;
        private const int StepsPerSide = 6;

        private readonly float[] _offsetY = new float[LayerCount];
        private readonly float[] _blur = new float[LayerCount];
        private readonly float[] _spread = new float[LayerCount];
        private readonly Color[] _color = new Color[LayerCount];
        private bool _hasTokens;

        public DialogFrameShadow()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawLayers;
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
        }

        private void OnCustomStyleResolved()
        {
            _hasTokens = true;
            for (var layer = 0; layer < LayerCount; layer++)
            {
                var prefix = $"--dialog-frame-shadow-{layer + 1}";
                _hasTokens &= UssCustomStyleReader.TryReadPixels(customStyle, $"{prefix}-y", out _offsetY[layer]);
                _hasTokens &= UssCustomStyleReader.TryReadPixels(customStyle, $"{prefix}-blur", out _blur[layer]);
                _hasTokens &= UssCustomStyleReader.TryReadPixels(customStyle, $"{prefix}-spread", out _spread[layer]);
                _hasTokens &= UssCustomStyleReader.TryReadColor(customStyle, $"{prefix}-color", out _color[layer]);
            }
            MarkDirtyRepaint();
        }

        private void DrawLayers(MeshGenerationContext context)
        {
            // silent-ok: トークンが読めない間は描かない / 読めなかった名前は UssCustomStyleReader が LogError で出す
            if (!_hasTokens) return;
            var frame = contentRect;
            // silent-ok: 寸法 0 はレイアウト前の一瞬だけ / 寸法が決まると UITK が描き直す
            if (frame.width <= 0f || frame.height <= 0f) return;
            for (var layer = 0; layer < LayerCount; layer++) DrawLayer(context, frame, layer);
        }

        private void DrawLayer(MeshGenerationContext context, Rect frame, int layer)
        {
            // 形は枠を y だけ下げ spread だけ四方へ広げた矩形。blur 0 でも格子が縁に寄るだけで描ける
            // The shape is the frame moved down by y and grown by spread on all sides; blur 0 only packs the grid at the edges
            var spread = _spread[layer];
            var sigma = Mathf.Max(_blur[layer] / 2f, 0.01f);
            var xs = AxisSamples(frame.xMin - spread, frame.xMax + spread, sigma);
            var ys = AxisSamples(frame.yMin + _offsetY[layer] - spread, frame.yMax + _offsetY[layer] + spread, sigma);
            var mesh = context.Allocate(xs.Length * ys.Length, (xs.Length - 1) * (ys.Length - 1) * 6);
            WriteVertices();
            WriteIndices();

            #region Internal

            // 格子の頂点に横と縦の被覆率の積を α として置く
            // Put the product of the horizontal and vertical coverages at each grid vertex as alpha
            void WriteVertices()
            {
                var x0 = frame.xMin - spread;
                var x1 = frame.xMax + spread;
                var y0 = frame.yMin + _offsetY[layer] - spread;
                var y1 = frame.yMax + _offsetY[layer] + spread;
                foreach (var y in ys)
                {
                    var coverageY = GaussianEdgeCoverage.BandCoverage(y, y0, y1, sigma);
                    foreach (var x in xs)
                    {
                        var coverageX = GaussianEdgeCoverage.BandCoverage(x, x0, x1, sigma);
                        var tint = _color[layer];
                        tint.a = LinearBlendOpacity.OpacityAt(_color[layer].a, coverageX * coverageY);
                        mesh.SetNextVertex(new Vertex { position = new Vector3(x, y, Vertex.nearZ), tint = tint });
                    }
                }
            }

            // 画面座標（y 下向き）で時計回りに張る（右→下）。前例 GamePanelFace・CrosshairShadowRing と同じ向き
            // Wind clockwise in screen space (y down; right then down), the same as GamePanelFace and CrosshairShadowRing
            void WriteIndices()
            {
                for (var row = 0; row < ys.Length - 1; row++)
                for (var column = 0; column < xs.Length - 1; column++)
                {
                    var topLeft = (ushort)(row * xs.Length + column);
                    var topRight = (ushort)(topLeft + 1);
                    var bottomLeft = (ushort)(topLeft + xs.Length);
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

        // 両端の縁の前後 3σ を σ/2 刻みで取り、小さい順に並べる（形が 6σ より狭いと両端の列が交わるので並べ直して重複を除く）
        // Samples within 3 sigma of both edges at sigma/2 steps in ascending order (a shape narrower than 6 sigma interleaves, so sort and drop duplicates)
        private static float[] AxisSamples(float start, float end, float sigma)
        {
            var step = sigma / 2f;
            var samples = new SortedSet<float>();
            for (var k = -StepsPerSide; k <= StepsPerSide; k++)
            {
                samples.Add(start + k * step);
                samples.Add(end + k * step);
            }
            var result = new float[samples.Count];
            samples.CopyTo(result);
            return result;
        }
    }
}
