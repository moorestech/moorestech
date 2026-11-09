using Client.Ui.Foundation.Stage.Columns;
using Client.Ui.Foundation.Theme;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory
{
    /// <summary>
    /// 帯の高さと大型の幅を舞台の寸法とトークンから導く（Web の calc() を C# で解く。A-47）
    /// Derives the band height and large width from the stage size and tokens (solves the Web calc() in C#; A-47)
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

            // 舞台が安全帯より狭いと帯が負になる。負の高さは当てず 0 で止め、狭すぎることを Warning で知らせる
            // A stage narrower than the safe areas makes the band negative; clamp to 0 instead of applying it and warn that it is too small
            if (band < 0)
            {
                UnityEngine.Debug.LogWarning($"[BlockInventoryPanelBand] stage height {style.height} is below the safe areas (top {style.paddingTop}, bottom {_menuLower}); band clamped to 0");
                band = 0;
            }
            _shell.style.maxHeight = band;
            _shell.style.height = _large ? new StyleLength(band) : new StyleLength(StyleKeyword.Auto);

            // silent-ok: 幅のトークンが未解決の間は大型の幅を当てない / 同上の再適用と LogError
            if (!_inventoryWidthResolved || !_columnGapResolved) return;
            _shell.style.width = _large ? new StyleLength(style.width - style.paddingLeft - style.paddingRight - _inventoryWidth - _columnGap) : new StyleLength(StyleKeyword.Auto);
        }
    }
}
