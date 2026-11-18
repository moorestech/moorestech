using Client.Ui.Foundation.Input;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Foundation.Theme;
using Client.Ui.Screens.KeyHints.Parts;
using Client.Ui.State.KeyHints;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.KeyHints
{
    /// <summary>
    /// 左下に積むキーヒント（Web の KeyHintHud）。受け取ったヒントを行に作り直すだけで、源を知らない
    /// Bottom-left key hints (Web KeyHintHud); it only rebuilds rows from the hints it receives and knows no sources
    /// </summary>
    public sealed class KeyHintHudView : VisualElement
    {
        private readonly UiStack _rows;
        private float _bottomGap;
        private float _bandHeight;
        private float _rowHeight;
        private bool _deleteMode;
        private bool _hasTokens;

        public KeyHintHudView()
        {
            name = "key-hints";
            AddToClassList("key-hint-hud");
            AddToClassList(UiTypographyClass.KeyHintText);
            pickingMode = PickingMode.Ignore;
            _rows = new UiStack(UiGap.Xs);
            _rows.pickingMode = PickingMode.Ignore;
            Add(_rows);
            UiDisplay.SetShown(this, false);
            RegisterCallback<CustomStyleResolvedEvent>(_ => OnCustomStyleResolved());
        }

        public void Render(KeyHintHudViewModel model)
        {
            UiDisplay.SetShown(this, model.Shown);
            _deleteMode = model.DeleteMode;
            if (_hasTokens) ApplyBottom();

            // 行は毎回作り直す（高々十数行で、内容の正は state 側）
            // Rebuild the rows every time (at most a dozen, and the state owns the content)
            _rows.ClearItems();
            foreach (var hint in model.Hints)
            {
                var row = new KeyHintRow(hint);
                if (_hasTokens) row.SetRowHeight(_rowHeight);
                _rows.AddItem(row);
            }
            UiPickingIgnore.ApplyToSubtree(this);
        }

        private void OnCustomStyleResolved()
        {
            // 削除モードの底は Web の calc(帯 + 隙間)。USS に calc が無いので足し算は C#（設計書 A-47）
            // Delete-mode bottom is the Web calc(band + gap); USS has no calc, so C# adds them (design A-47)
            var hasBottomGap = UssCustomStyleReader.TryReadPixels(customStyle, "--key-hint-bottom-gap", out _bottomGap);
            var hasBandHeight = UssCustomStyleReader.TryReadPixels(customStyle, "--key-hint-hud-band-height", out _bandHeight);

            // 行の高さは Web の line-height（字の大きさ × 1.2）。UITK の Label は行高を持たないので固定の高さで写す（D-22）
            // Row height is the Web line-height (font size x 1.2); UITK Labels have no line-height, so a fixed height carries it (D-22)
            var hasFontSize = UssCustomStyleReader.TryReadPixels(customStyle, "--key-hint-hud-font-size", out var fontSize);
            var hasLineHeight = UssCustomStyleReader.TryReadNumber(customStyle, "--key-hint-hud-line-height", out var lineHeight);
            _hasTokens = hasBottomGap && hasBandHeight && hasFontSize && hasLineHeight;

            // silent-ok: トークンが読めない間は配置しない / 読めなかった名前は UssCustomStyleReader が LogError で出す
            if (!_hasTokens) return;
            _rowHeight = fontSize * lineHeight;
            _rows.Query<KeyHintRow>().ForEach(row => row.SetRowHeight(_rowHeight));
            ApplyBottom();
        }

        private void ApplyBottom()
        {
            style.bottom = _deleteMode ? _bandHeight + _bottomGap : _bottomGap;
        }
    }
}
