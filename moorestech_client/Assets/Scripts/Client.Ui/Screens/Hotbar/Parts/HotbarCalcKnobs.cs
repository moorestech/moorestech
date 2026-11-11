using Client.Ui.Foundation.Theme;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.Hotbar.Parts
{
    /// <summary>
    /// Web で calc()・transform を通る 2 つのつまみを、列のスタイル解決の通知で読んで当てる（設計書 A-47）
    /// Reads the two knobs that go through calc() and transform on the Web when the row's style resolves, and applies them (design A-47)
    /// </summary>
    /// <remarks>
    /// customStyle は CustomStyleResolvedEvent の後でしか読めない（UssCustomStyleReader の前提。未解決で読むと LogError）。読めた値を持ち、解決の前に作った子は解決の通知で当てる
    /// customStyle is readable only after CustomStyleResolvedEvent (the UssCustomStyleReader premise; reading early logs an error); keep the read values and apply them to children built before resolution when the notification arrives
    /// </remarks>
    public sealed class HotbarCalcKnobs
    {
        public const string RowOffsetProperty = "--hotbar-hud-row-offset";
        public const string TabOverhangProperty = "--hotbar-hud-number-tab-overhang";

        private readonly VisualElement _row;
        private bool _overhangResolved;
        private float _overhang;

        public HotbarCalcKnobs(VisualElement row)
        {
            _row = row;
            row.RegisterCallback<CustomStyleResolvedEvent>(_ => ReadAndApply());
        }

        // 子（番号タブ）を作り直した後に呼ぶ。解決の前なら、解決の通知の ReadAndApply が当てる
        // Call after rebuilding the children (number tabs); before resolution, ReadAndApply on the notification applies it
        public void ApplyToRebuiltTabs()
        {
            // silent-ok: スタイル解決の前に作ったタブは解決の通知で当たる / HotbarHudViewTest（SetUp が解決の前に Render する。未解決で読めば LogError で Unity のテストが落ちる）のタブの -16 の主張が見る
            if (!_overhangResolved) return;
            ApplyOverhang();
        }

        // 列の横ずれ（Web .hotbarFrame の translateX）と、タブの上へのはみ出し（Web .num の top: calc(overhang * -1)）
        // The row's horizontal shift (Web .hotbarFrame translateX) and the tab's upward overhang (Web .num top: calc(overhang * -1))
        private void ReadAndApply()
        {
            // 読めないつまみは UssCustomStyleReader が LogError を出す
            // An unreadable knob is logged as an error by UssCustomStyleReader
            if (UssCustomStyleReader.TryReadPixels(_row.customStyle, RowOffsetProperty, out var offset))
            {
                _row.style.translate = new Translate(new Length(offset), new Length(0f));
            }
            _overhangResolved = UssCustomStyleReader.TryReadPixels(_row.customStyle, TabOverhangProperty, out _overhang);
            if (_overhangResolved) ApplyOverhang();
        }

        private void ApplyOverhang()
        {
            _row.Query(className: HotbarNumberTab.ClassName).ForEach(tab => tab.style.top = -_overhang);
        }
    }
}
