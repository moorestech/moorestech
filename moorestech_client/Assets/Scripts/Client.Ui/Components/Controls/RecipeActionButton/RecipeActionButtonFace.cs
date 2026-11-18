using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// 3 色の横グラデを描く面
    /// Face drawing the three-color horizontal gradient
    /// </summary>
    internal sealed class RecipeActionButtonFace : VisualElement
    {
        public const string UssClassName = "recipe-action-button__face";

        private readonly Vertex[] _vertices = new Vertex[RecipeActionFaceMesh.VertexCount];
        private readonly ushort[] _indices = new ushort[RecipeActionFaceMesh.IndexCount];
        private Color _start;
        private Color _middle;
        private Color _end;
        private float _borderWidth;
        private bool _hasTokens;

        public RecipeActionButtonFace()
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            generateVisualContent += DrawFace;
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
        }

        private void OnCustomStyleResolved()
        {
            // ホバー・無効で付け替わるたびに解決し直されるので、読み直して描き直す
            // Hover and disabled re-resolve the colors each time, so re-read and redraw
            var hasStart = UssCustomStyleReader.TryReadColor(customStyle, RecipeActionButton.FaceStartProperty, out _start);
            var hasMiddle = UssCustomStyleReader.TryReadColor(customStyle, RecipeActionButton.FaceMiddleProperty, out _middle);
            var hasEnd = UssCustomStyleReader.TryReadColor(customStyle, RecipeActionButton.FaceEndProperty, out _end);
            var hasBorder = UssCustomStyleReader.TryReadPixels(customStyle, RecipeActionButton.FaceBorderWidthProperty, out _borderWidth);
            _hasTokens = hasStart && hasMiddle && hasEnd && hasBorder;
            MarkDirtyRepaint();
        }

        private void DrawFace(MeshGenerationContext context)
        {
            // silent-ok: つまみの解決前は描かない / 解決できなければ UssCustomStyleReader が読めなかった名前を LogError で出す
            // silent-ok: draw nothing until the knobs resolve / UssCustomStyleReader logs the unreadable name if they never do
            if (!_hasTokens) return;
            var size = contentRect.size;
            // silent-ok: 配置前の寸法 0・枠より狭い面は描くものが無い / 寸法は RecipeActionButtonTest がトークンと照合する
            // silent-ok: a zero-size face before layout or one narrower than its borders has nothing to draw / RecipeActionButtonTest checks the size against the tokens
            if (size.x <= _borderWidth * 2f || size.y <= 0f) return;

            // 帯を組んで 1 回で渡す
            // Build the strip and hand it over in one go
            RecipeActionFaceMesh.Build(size.x, size.y, _borderWidth, _start, _middle, _end, _vertices, _indices);
            var mesh = context.Allocate(_vertices.Length, _indices.Length);
            mesh.SetAllVertices(_vertices);
            mesh.SetAllIndices(_indices);
        }
    }
}
