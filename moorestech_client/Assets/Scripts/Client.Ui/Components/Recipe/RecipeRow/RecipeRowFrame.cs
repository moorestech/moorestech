using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Recipe
{
    /// <summary>
    /// 光彩・輪・影・括弧を描く枠
    /// Frame drawing glow, ring, shadow and brackets
    /// </summary>
    internal sealed class RecipeRowFrame : VisualElement
    {
        public const string UssClassName = "recipe-row__frame";

        private readonly Rect[] _ringRects = new Rect[RecipeRowFrameGeometry.RingRectCount];
        private readonly Rect[] _bracketRects = new Rect[RecipeRowFrameGeometry.BracketRectCount];
        private readonly Vertex[] _glowVertices = new Vertex[RecipeRowFrameShadowMesh.GlowVertexCount];
        private readonly ushort[] _glowIndices = new ushort[RecipeRowFrameShadowMesh.GlowIndexCount];
        private readonly Vertex[] _barShadowVertices = new Vertex[RecipeRowFrameShadowMesh.BarShadowVertexCount];
        private readonly ushort[] _barShadowIndices = new ushort[RecipeRowFrameShadowMesh.BarShadowIndexCount];
        private float _outset;
        private float _borderWidth;
        private float _ringWidth;
        private float _bracketLength;
        private float _bracketThickness;
        private Color _ringColor;
        private Color _bracketColor;
        private float _glowBlur;
        private Color _glowColor;
        private float _bracketShadowBlur;
        private Color _bracketShadowColor;
        private bool _hasStyle;

        public RecipeRowFrame()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawFrame;
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
        }

        private void OnCustomStyleResolved()
        {
            var hasOutset = UssCustomStyleReader.TryReadPixels(customStyle, "--recipe-row-frame-outset", out _outset);
            var hasBorder = UssCustomStyleReader.TryReadPixels(customStyle, "--recipe-row-frame-border", out _borderWidth);
            var hasRingWidth = UssCustomStyleReader.TryReadPixels(customStyle, "--recipe-row-frame-ring-width", out _ringWidth);
            var hasLength = UssCustomStyleReader.TryReadPixels(customStyle, "--recipe-row-frame-bracket-length", out _bracketLength);
            var hasThickness = UssCustomStyleReader.TryReadPixels(customStyle, "--recipe-row-frame-bracket-thickness", out _bracketThickness);
            var hasRing = UssCustomStyleReader.TryReadColor(customStyle, "--recipe-row-frame-ring", out _ringColor);
            var hasBracket = UssCustomStyleReader.TryReadColor(customStyle, "--recipe-row-frame-bracket", out _bracketColor);
            var hasGlowBlur = UssCustomStyleReader.TryReadPixels(customStyle, "--recipe-row-frame-glow-blur", out _glowBlur);
            var hasGlow = UssCustomStyleReader.TryReadColor(customStyle, "--recipe-row-frame-glow", out _glowColor);
            var hasShadowBlur = UssCustomStyleReader.TryReadPixels(customStyle, "--recipe-row-frame-bracket-shadow-blur", out _bracketShadowBlur);
            var hasShadow = UssCustomStyleReader.TryReadColor(customStyle, "--recipe-row-frame-bracket-shadow", out _bracketShadowColor);
            _hasStyle = hasOutset && hasBorder && hasRingWidth && hasLength && hasThickness && hasRing && hasBracket && hasGlowBlur && hasGlow && hasShadowBlur && hasShadow;

            // 枠の箱は Web の擬似要素と同じく padding の箱から outset だけ外（USS に負の calc が無いので C# が当てる）
            // The frame box sits outset outside the padding box like the Web pseudo-elements (USS has no negative calc, so C# applies it)
            if (hasOutset) ApplyOutset();
            MarkDirtyRepaint();

            #region Internal

            void ApplyOutset()
            {
                style.left = -_outset;
                style.top = -_outset;
                style.right = -_outset;
                style.bottom = -_outset;
            }

            #endregion
        }

        private void DrawFrame(MeshGenerationContext context)
        {
            // silent-ok: つまみの解決前は描かない / 解決できなければ UssCustomStyleReader が読めなかった名前を LogError で出す
            // silent-ok: draw nothing until the knobs resolve / UssCustomStyleReader logs the unreadable name if they never do
            if (!_hasStyle) return;

            // 重なりは Web と同じ: 光彩の上に輪、括弧の影の上に括弧（Allocate の順が描く順）
            // Same stacking as the Web: the ring over the glow, brackets over their shadow (Allocate order is draw order)
            var size = contentRect.size;
            var borderInset = _outset - _borderWidth;
            RecipeRowFrameGeometry.WriteRingRects(size, borderInset, _ringWidth, _ringRects);
            RecipeRowFrameGeometry.WriteBracketRects(size, _bracketLength, _bracketThickness, _bracketRects);

            // box-shadow のぼかしは σ の 2 倍、drop-shadow のぼかしは σ そのもの
            // A box-shadow blur is twice σ; a drop-shadow blur is σ itself
            var borderBox = new Rect(borderInset, borderInset, size.x - borderInset * 2f, size.y - borderInset * 2f);
            RecipeRowFrameShadowMesh.BuildGlow(borderBox, _glowBlur * 0.5f, _glowColor, _glowVertices, _glowIndices);
            WriteMesh(_glowVertices, _glowIndices);
            WriteQuads(_ringRects, _ringColor);
            foreach (var bar in _bracketRects)
            {
                RecipeRowFrameShadowMesh.BuildBarShadow(bar, _bracketShadowBlur, _bracketShadowColor, _barShadowVertices, _barShadowIndices);
                WriteMesh(_barShadowVertices, _barShadowIndices);
            }
            WriteQuads(_bracketRects, _bracketColor);

            #region Internal

            void WriteMesh(Vertex[] vertices, ushort[] indices)
            {
                var mesh = context.Allocate(vertices.Length, indices.Length);
                mesh.SetAllVertices(vertices);
                mesh.SetAllIndices(indices);
            }

            // 画面座標（y 下向き）で時計回りの 2 三角
            // Two clockwise triangles in screen space (y down)
            void WriteQuads(Rect[] rects, Color tint)
            {
                var mesh = context.Allocate(rects.Length * 4, rects.Length * 6);
                var written = 0;
                foreach (var rect in rects)
                {
                    var first = (ushort)written;
                    mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMin, Vertex.nearZ), tint = tint });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMin, Vertex.nearZ), tint = tint });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMax, rect.yMax, Vertex.nearZ), tint = tint });
                    mesh.SetNextVertex(new Vertex { position = new Vector3(rect.xMin, rect.yMax, Vertex.nearZ), tint = tint });
                    mesh.SetNextIndex(first);
                    mesh.SetNextIndex((ushort)(first + 1));
                    mesh.SetNextIndex((ushort)(first + 2));
                    mesh.SetNextIndex(first);
                    mesh.SetNextIndex((ushort)(first + 2));
                    mesh.SetNextIndex((ushort)(first + 3));
                    written += 4;
                }
            }

            #endregion
        }
    }
}
