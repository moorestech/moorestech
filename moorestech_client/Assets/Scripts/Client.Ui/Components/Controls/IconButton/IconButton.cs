using System;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// 面を持たない小さいアイコンボタン（Web の shared/ui/IconButton）。中の図形は呼び出し側が渡し、閉じるには IconButtonCloseGlyph を渡す
    /// Small faceless icon button (Web shared/ui/IconButton); callers pass the glyph, and IconButtonCloseGlyph for a close button
    /// </summary>
    /// <remarks>
    /// 拡張点: 閉じる以外の図形は画面が VisualElement で作って渡す（Web が children に SVG を渡すのと同じ）。図形は CustomStyleResolvedEvent で GlyphColorProperty と GlyphStrokeProperty を読んで描く
    /// Extension: screens build non-close glyphs as VisualElements and pass them in (as the Web passes an SVG child); a glyph reads GlyphColorProperty and GlyphStrokeProperty in CustomStyleResolvedEvent and draws with them
    /// </remarks>
    public sealed class IconButton : Button
    {
        public const string UssClassName = "icon-button";
        public const string GlyphUssClassName = "icon-button__glyph";
        public const string GlyphColorProperty = "--icon-button-glyph-color";
        public const string GlyphStrokeProperty = "--icon-button-glyph-stroke";

        private readonly Subject<Unit> _clicked = new();

        public IObservable<Unit> OnClicked => _clicked;

        public IconButton(string testid, VisualElement glyph)
        {
            name = testid;
            AddToClassList(UssClassName);

            // GameUi は入力欄以外にフォーカスを持たせない
            // GameUi never lets anything but text fields hold focus
            focusable = false;

            // 図形は押下を横取りしないようポインタを受けず、色と線幅は図形のクラスで受け取る
            // The glyph takes no pointer so it never steals the press, and gets colour and stroke via its class
            glyph.pickingMode = PickingMode.Ignore;
            glyph.AddToClassList(GlyphUssClassName);
            Add(glyph);

            // 押下は UniRx で外へ出す
            // Presses leave through UniRx
            clicked += () => _clicked.OnNext(Unit.Default);
        }
    }
}
