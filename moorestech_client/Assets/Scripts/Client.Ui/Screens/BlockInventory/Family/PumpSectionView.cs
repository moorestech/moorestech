using System.Collections.Generic;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Parts.Rows;
using Client.Ui.State.BlockInventory.Family;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.Text;
using Core.Master;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Family
{
    /// <summary>
    /// ポンプの区画
    /// The pump section (Web PumpSection): power rows for electric pumps → per-minute rows, else the no-vein warning
    /// </summary>
    public sealed class PumpSectionView : IDetailSectionView<PumpDetail>
    {
        private readonly UiStack _root = new(UiGap.Xs) { name = "pump-section" };
        private readonly UiStack _electricRows = new(UiGap.Xs);
        private readonly MachineStateRow _stateRow = new("pump-state-label", "pump-power-rate");
        private readonly UiGroup _fluids = new(UiGap.Xs) { name = "pump-pumping-fluids" };
        private readonly LackHighlightText _noVein = new("pump-no-vein", LackHighlightTextSize.Sm);
        private readonly List<(FluidId FluidId, PerMinuteRateRow Row)> _rows = new();
        private readonly GameContentPresentation _content;

        public VisualElement Root => _root;

        public PumpSectionView(GameContentPresentation content)
        {
            _content = content;
            _stateRow.MountInto(_electricRows);
            _root.AddItem(_electricRows);
            _root.AddItem(_fluids);
            _root.AddItem(_noVein);

            // ポンプでないブロックでは描かれないまま載るので、文の無いラベルが LogError にならないよう文を先に入れる
            // On non-pump blocks this is mounted without ever being drawn, so seed the texts to keep text-less labels from logging errors
            _stateRow.Render(MachineProcessDisplayState.Idle, 0, 0, false);
            _noVein.Render(new UiText(LocalizationKeys.Ui.BlockInventory.PumpNoVein), true);
        }

        public void Render(PumpDetail detail)
        {
            // 動力行の有無は種別が決める
            // The kind decides whether power rows exist; a gear pump has none
            var electric = detail as ElectricPumpDetail;
            _root.SetItemShown(_electricRows, electric != null);
            if (electric != null) _stateRow.Render(electric.State, electric.CurrentPower, electric.RequestPower, true);

            // 汲み上げ対象の有無で行を分ける
            // Pump targets split fluid rows from the warning
            var hasTargets = 0 < detail.PumpingFluids.Count;
            _root.SetItemShown(_fluids, hasTargets);
            _root.SetItemShown(_noVein, !hasTargets);
            if (!SameFluids()) Rebuild();
            for (var i = 0; i < _rows.Count; i++) _rows[i].Row.Render(detail.PumpingFluids[i].AmountPerMinute);

            #region Internal

            bool SameFluids()
            {
                if (detail.PumpingFluids.Count != _rows.Count) return false;
                for (var i = 0; i < _rows.Count; i++)
                {
                    if (detail.PumpingFluids[i].FluidId != _rows[i].FluidId) return false;
                }
                return true;
            }

            void Rebuild()
            {
                _fluids.ClearItems();
                _rows.Clear();
                foreach (var fluid in detail.PumpingFluids)
                {
                    var icon = new GameIcon();
                    icon.AddToClassList("pump-section__fluid-icon");
                    icon.SetIcon(_content.Fluid(fluid.FluidId), GameIconFallback.None);
                    var row = new PerMinuteRateRow(icon);
                    _fluids.AddItem(row);
                    _rows.Add((fluid.FluidId, row));
                }
            }

            #endregion
        }
    }
}
