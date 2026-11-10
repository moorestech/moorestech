using System.Collections.Generic;
using System.Globalization;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.Foundation.Text;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.Toast;
using Mooresmaster.Localization.Generated;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械のインベントリ本文（Web MachineInventoryBody）。加工行 → モジュール帯 → 液体の行 → 出力ごとの分間生産数
    /// The machine inventory body (Web MachineInventoryBody): process row → module band → fluid row → per-minute production per output
    /// </summary>
    public sealed class MachineInventoryBody : VisualElement
    {
        private readonly UiStack _stack = new(UiGap.Xs);
        private readonly MachineProcessRow _processRow;
        private readonly MachineModuleBand _moduleBand;
        private readonly FluidSlotRow _fluids = new("machine-fluid-slots");
        private readonly UiStack _perMinuteRows = new(UiGap.Xs);
        private readonly List<Label> _perMinuteNumbers = new();
        private readonly GameContentPresentation _content;

        public MachineInventoryBody(GameContentPresentation content, IBlockSlotCommands commands, UiCommandResultToaster toaster)
        {
            name = "machine-inventory-body";
            pickingMode = PickingMode.Ignore;
            _content = content;
            _processRow = new MachineProcessRow(content, commands, toaster);
            _moduleBand = new MachineModuleBand(content, commands, toaster);
            _stack.pickingMode = PickingMode.Ignore;
            _stack.AddToClassList(UiLayoutClass.StackAlignCenter);
            _perMinuteRows.pickingMode = PickingMode.Ignore;
            _perMinuteRows.AddToClassList(UiLayoutClass.StackAlignCenter);

            // 機械の液体行は矢印を持たない（加工の進捗は入出力の間の矢印が担う）
            // The machine fluid row has no arrow (processing progress lives between the in/out bands)
            _fluids.ClearProgress();
            _stack.AddItem(_processRow);
            _stack.AddItem(_moduleBand);
            _stack.AddItem(_fluids);
            _stack.AddItem(_perMinuteRows);
            _stack.SetItemShown(_moduleBand, false);
            _stack.SetItemShown(_perMinuteRows, false);
            Add(_stack);
        }

        public void Render(MachineSectionViewModel viewModel)
        {
            _processRow.Render(viewModel);
            _stack.SetItemShown(_moduleBand, 0 < viewModel.Bands.Module.Count);
            _moduleBand.Render(viewModel);
            RenderFluids();
            RenderPerMinute();

            #region Internal

            void RenderFluids()
            {
                var fluids = viewModel.SlotView.Fluids;
                var contents = new FluidSlotContent[fluids.Count];
                for (var i = 0; i < contents.Length; i++) contents[i] = FluidSlotContentMapper.MapWithGhost(viewModel.FluidSlots[fluids[i].Index], fluids[i], _content);
                _fluids.SetFluids(contents);
            }

            void RenderPerMinute()
            {
                // レシピ秒が 0 以下の出力は行を出さない（Web は null）
                // Outputs with a non-positive recipe time get no row (null on the Web)
                var rates = new List<double>();
                foreach (var output in viewModel.Detail.RunningRecipeOutputs)
                {
                    if (BlockDetailMath.TryGetItemsPerMinute(output.Count, viewModel.Detail.RunningRecipeTimeSeconds, out var rate)) rates.Add(rate);
                }

                // 行の数が同じ間は行を残して数字だけ差し替える（加工中は毎 tick 届く。D-37）
                // Keep the rows and swap only the numbers while the row count is unchanged (updates arrive every tick; D-37)
                if (rates.Count != _perMinuteNumbers.Count) RebuildRows(rates.Count);
                for (var i = 0; i < rates.Count; i++) _perMinuteNumbers[i].text = rates[i].ToString(CultureInfo.InvariantCulture);
                _stack.SetItemShown(_perMinuteRows, 0 < rates.Count);
            }

            void RebuildRows(int count)
            {
                _perMinuteRows.ClearItems();
                _perMinuteNumbers.Clear();
                for (var i = 0; i < count; i++)
                {
                    var row = new UiGroup(UiGap.Px4) { name = "machine-items-per-minute", pickingMode = PickingMode.Ignore };
                    var label = new LocalizedLabel { pickingMode = PickingMode.Ignore };
                    label.SetKey(LocalizationKeys.Ui.BlockInventory.ProductionPerMinute);
                    var number = new Label { pickingMode = PickingMode.Ignore };
                    row.AddItem(label);
                    row.AddItem(number);
                    _perMinuteRows.AddItem(row);
                    _perMinuteNumbers.Add(number);
                }
            }

            #endregion
        }
    }
}
