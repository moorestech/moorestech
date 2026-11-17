using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Foundation.Theme;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 帯の高さと大小パネルの幅を導く（A-47）
    /// Derives the band height and the large/natural panel widths (A-47)
    /// </summary>
    public sealed class BlockInventoryPanelBand
    {
        // :root のトークンは子へ継承されないので、.block-inventory で受け直した名前を読む（BlockInventory.uss）
        // :root tokens do not inherit, so read the names re-declared on .block-inventory (BlockInventory.uss)
        private const string MenuLowerSafeArea = "--block-inventory-menu-lower-safe-area";
        private const string InventoryPanelWidth = "--block-inventory-inventory-panel-width";
        private const string StageColumnGap = "--block-inventory-stage-column-gap";

        private readonly StageColumnsLayout _stage;
        private readonly VisualElement _shell;
        private float _menuLower;
        private float _inventoryWidth;
        private float _columnGap;
        private bool _menuLowerResolved;
        private bool _inventoryWidthResolved;
        private bool _columnGapResolved;
        private bool _large;
        private bool _stageTooSmallReported;

        public BlockInventoryPanelBand(StageColumnsLayout stage, VisualElement shell)
        {
            _stage = stage;
            _shell = shell;
            shell.RegisterCallback<CustomStyleResolvedEvent>(_ => ReadTokens());
            stage.RegisterCallback<GeometryChangedEvent>(_ => Apply());

            #region Internal

            void ReadTokens()
            {
                // 3 つを別々に持つ。欠けは UssCustomStyleReader が LogError で知らせる
                // Keep the three separately; a missing one is reported by UssCustomStyleReader with LogError
                _menuLowerResolved = UssCustomStyleReader.TryReadPixels(_shell.customStyle, MenuLowerSafeArea, out _menuLower);
                _inventoryWidthResolved = UssCustomStyleReader.TryReadPixels(_shell.customStyle, InventoryPanelWidth, out _inventoryWidth);
                _columnGapResolved = UssCustomStyleReader.TryReadPixels(_shell.customStyle, StageColumnGap, out _columnGap);
                Apply();
            }

            #endregion
        }

        public void SetLarge(bool large)
        {
            _large = large;
            Apply();
        }

        private void Apply()
        {
            // silent-ok: 最初のレイアウト前は舞台の寸法もトークンも無い / 解決時の CustomStyleResolvedEvent・GeometryChangedEvent で当て直し、トークン欠けは LogError
            if (!_menuLowerResolved || float.IsNaN(_stage.resolvedStyle.height)) return;

            // 帯 = 舞台の高さ − 上の安全帯（舞台の padding-top）− 下の安全帯
            // Band = stage height − upper safe area (the stage's padding-top) − lower safe area
            var style = _stage.resolvedStyle;
            var band = style.height - style.paddingTop - _menuLower;

            // 帯が負なら 0 で止め入口で警告
            // Clamp negative band to 0; warn once on entry
            if (band < 0)
            {
                if (!_stageTooSmallReported) UnityEngine.Debug.LogWarning($"[BlockInventoryPanelBand] stage height {style.height} is below the safe areas (top {style.paddingTop}, bottom {_menuLower}); band clamped to 0");
                _stageTooSmallReported = true;
                band = 0;
            }
            else
            {
                _stageTooSmallReported = false;
            }
            _shell.style.maxHeight = band;
            _shell.style.height = _large ? new StyleLength(band) : new StyleLength(StyleKeyword.Auto);

            // silent-ok: 幅のトークンが未解決の間は幅を当てない / 同上の再適用と LogError
            if (!_inventoryWidthResolved || !_columnGapResolved) return;

            // 大型は viewer〜items の 2 列ぶん（Web .panelLarge の calc）。小型は viewer の列の残り（空の items の列と列の間 2 つを除く）までの中身の幅で、Web の auto の列と justify-self: start の fit-content と同じく舞台の外へ広がらない
            // Large spans viewer..items (Web .panelLarge calc); natural is content-wide up to the viewer track's remainder (minus the empty items track's two gaps), never spreading past the stage like the Web auto track with justify-self: start fit-content
            var largeWidth = style.width - style.paddingLeft - style.paddingRight - _inventoryWidth - _columnGap;
            _shell.style.width = _large ? new StyleLength(largeWidth) : new StyleLength(StyleKeyword.Auto);
            _shell.style.maxWidth = _large ? new StyleLength(StyleKeyword.None) : new StyleLength(UnityEngine.Mathf.Max(0f, largeWidth - _columnGap));
        }
    }
}
