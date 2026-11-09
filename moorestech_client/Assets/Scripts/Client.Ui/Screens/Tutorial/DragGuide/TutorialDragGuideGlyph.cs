using Client.Ui.Foundation.Theme;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Tutorial.DragGuide
{
    /// <summary>
    /// ドラッグ誘導のカーソル形（Web DragGuide の svg path。viewBox 24）
    /// The drag guide cursor shape (Web DragGuide svg path; viewBox 24)
    /// </summary>
    public sealed class TutorialDragGuideGlyph : VisualElement
    {
        private const float ViewBoxSize = 24f;
        private const string FillProperty = "--tutorial-drag-guide-glyph-fill";
        private const string ShadowProperty = "--tutorial-drag-guide-glyph-shadow";

        private static readonly Vector2[] CursorPath =
        {
            new(6f, 3f), new(18f, 12f), new(11f, 13.5f), new(13.5f, 20f), new(10.5f, 21f), new(8f, 14.5f), new(3f, 18f),
        };

        private Color _fill;
        private Color _shadow;
        private bool _hasStyle;

        public TutorialDragGuideGlyph()
        {
            AddToClassList("tutorial-drag-guide__glyph");
            pickingMode = PickingMode.Ignore;
            generateVisualContent += Draw;
            RegisterCallback<CustomStyleResolvedEvent>(evt => OnCustomStyleResolved(evt.customStyle));
        }

        private void OnCustomStyleResolved(ICustomStyle style)
        {
            // 塗りと影の色を読み直して描き直す（読めなければ Reader が LogError）
            // Re-read the fill and shadow colours and redraw (the reader logs an error when unreadable)
            var hasFill = UssCustomStyleReader.TryReadColor(style, FillProperty, out _fill);
            var hasShadow = UssCustomStyleReader.TryReadColor(style, ShadowProperty, out _shadow);
            _hasStyle = hasFill && hasShadow;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext context)
        {
            // silent-ok: 色の解決前は描かない / 解決できなければ UssCustomStyleReader が LogError を出す
            if (!_hasStyle) return;

            // viewBox の 24 を要素の幅へ拡大する
            // Scale the 24-unit viewBox to the element width
            var unit = contentRect.width / ViewBoxSize;
            var painter = context.painter2D;
            // 影は blur を持てないので 1px 下へずらした写しで近似する（LX-35）
            // Painter2D has no blur, so approximate the drop shadow with a copy shifted 1px down (LX-35)
            FillPath(painter, unit, new Vector2(0f, 1f), _shadow); // look-exception: LX-35
            FillPath(painter, unit, Vector2.zero, _fill);

            #region Internal

            void FillPath(Painter2D target, float scale, Vector2 offset, Color color)
            {
                target.fillColor = color;
                target.BeginPath();
                target.MoveTo(CursorPath[0] * scale + offset);
                for (var i = 1; i < CursorPath.Length; i++) target.LineTo(CursorPath[i] * scale + offset);
                target.ClosePath();
                target.Fill();
            }

            #endregion
        }
    }
}
