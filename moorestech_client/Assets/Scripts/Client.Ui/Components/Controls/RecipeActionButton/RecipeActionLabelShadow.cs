using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Theme;
using Client.Ui.State.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// 文言の影（字 4 つをずらして重ねる）
    /// Label shadow (four shifted copies stacked)
    /// </summary>
    /// <remarks>
    /// 文字の縁（-unity-text-outline-width）は字の内側にも掛かり細い字の白を塗りつぶすので使わない。影の幅が 0 なら描かない（研究は影なし）。
    /// The text outline also eats into the glyph and covers thin white strokes, so it is not used; a zero shadow width draws nothing (research has no shadow).
    /// </remarks>
    internal sealed class RecipeActionLabelShadow : VisualElement
    {
        public const string UssClassName = "recipe-action-button__label-shadow";
        public const string GlyphUssClassName = "recipe-action-button__label-shadow-glyph";
        public const string OffsetProperty = "--recipe-action-button-text-shadow-offset";

        // Web の text-shadow の並び（パネルの座標は下向きが正）: 右・左・下・上
        // Web text-shadow order in panel space (y grows downward): right, left, down, up
        private static readonly Vector2[] Directions = { new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f) };

        private readonly LocalizedLabel[] _glyphs = new LocalizedLabel[4];

        public RecipeActionLabelShadow(RecipeActionLabelWeight weight)
        {
            AddToClassList(UssClassName);
            pickingMode = PickingMode.Ignore;
            for (var i = 0; i < _glyphs.Length; i++)
            {
                var glyph = new LocalizedLabel { pickingMode = PickingMode.Ignore };
                glyph.AddToClassList(GlyphUssClassName);
                if (weight == RecipeActionLabelWeight.Medium) glyph.AddToClassList(UiTypographyClass.TextMedium);
                _glyphs[i] = glyph;
                Add(glyph);
            }
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
        }

        public void SetText(UiText text)
        {
            foreach (var glyph in _glyphs) glyph.SetText(text);
        }

        private void OnCustomStyleResolved()
        {
            // 読めなければ UssCustomStyleReader が LogError を出すので、影の無い既定の見た目で続ける
            // An unreadable width is reported by UssCustomStyleReader's LogError, so continue without the shadow
            if (!UssCustomStyleReader.TryReadPixels(customStyle, OffsetProperty, out var offset)) offset = 0f;

            // 幅 0 は影なし。重ねたままだと字の縁の画素が影の色で暗むので描かない
            // Zero width means no shadow; stacking the copies would darken the glyph edges, so skip drawing
            style.display = 0f < offset ? DisplayStyle.Flex : DisplayStyle.None;

            for (var i = 0; i < _glyphs.Length; i++)
            {
                _glyphs[i].style.translate = new Translate(Directions[i].x * offset, Directions[i].y * offset);
            }
        }
    }
}
