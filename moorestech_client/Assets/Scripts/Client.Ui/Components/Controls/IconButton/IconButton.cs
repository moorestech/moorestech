using System;
using Client.Ui.Foundation.Input.OsCursor;
using UniRx;
using UnityEngine.UIElements;

namespace Client.Ui.Components.Controls
{
    /// <summary>
    /// 面を持たない小アイコンボタン。図形は呼び出し側が渡す
    /// Faceless small icon button; callers pass the glyph
    /// </summary>
    /// <remarks>
    /// 拡張点: 閉じる以外の図形は画面が VisualElement で渡す
    /// Extension: screens pass non-close glyphs as VisualElements
    /// </remarks>
    public sealed class IconButton : Button
    {
        public const string UssClassName = "icon-button";
        public const string GlyphUssClassName = "icon-button__glyph";
        public const string GlyphColorProperty = "--icon-button-glyph-color";
        public const string GlyphStrokeProperty = "--icon-button-glyph-stroke";

        private readonly Subject<Unit> _clicked = new();

        public IObservable<Unit> OnClicked => _clicked;

        public IconButton(string testid, VisualElement glyph, IUiOsCursor osCursor)
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

            // Web の cursor: pointer（IconButton/style.module.css:9 ほか）。部品が自分で出す（A-247）
            // Web cursor: pointer (IconButton/style.module.css:9 and others); the part requests it itself (A-247)
            new UiOsCursorHoverRequest(this, osCursor, UiOsCursorShape.Pointer, UiOsCursorLayer.Element);
        }
    }
}
