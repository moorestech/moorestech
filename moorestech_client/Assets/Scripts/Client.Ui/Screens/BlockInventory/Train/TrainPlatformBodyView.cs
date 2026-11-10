using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Train.Parts;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.Train.Platform;
using Client.Ui.State.Train.Platform.Edit;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 駅・プラットフォームの本文（Web TrainPlatformInventory の写し）。駅名 → スロット → 液体 → モード。部分は「あれば出す」の 1 規則
    /// Station and platform body (copy of Web TrainPlatformInventory): name, slots, fluids, mode; each part follows "show when present"
    /// </summary>
    public sealed class TrainPlatformBodyView : VisualElement
    {
        private readonly UiStack _stack = new(UiGap.Sm);
        private readonly TrainStationNameRow _nameRow;
        private readonly BlockItemGrid _grid;
        private readonly FluidInventoryContents _fluids;
        private readonly TrainPlatformModeSection _mode;

        public TrainPlatformBodyView(BlockItemGrid grid, FluidInventoryContents fluids, TrainPlatformEditCommands commands)
        {
            name = "train-platform-view";
            _nameRow = new TrainStationNameRow(commands);
            _grid = grid;
            _fluids = fluids;
            _mode = new TrainPlatformModeSection(commands);
            _stack.AddItem(_nameRow);
            _stack.AddItem(_grid);
            _stack.AddItem(_fluids);
            _stack.AddItem(_mode);
            Add(_stack);
        }

        public void Render(TrainPlatformBodyState state, string stationNameInput)
        {
            // 閉じた状態ではどの部分も出さない（エラー行は alpha の外殻が出す）
            // A closed state shows no part (alpha's shell shows the error row)
            if (state is not OpenTrainPlatformBody open)
            {
                _stack.SetItemShown(_nameRow, false);
                _stack.SetItemShown(_grid, false);
                _stack.SetItemShown(_fluids, false);
                _stack.SetItemShown(_mode, false);
                return;
            }

            // 出さない部分は Stack から隠し、間の余白も詰める
            // Hidden parts leave the stack so their gaps collapse too
            _stack.SetItemShown(_nameRow, open.StationName.IsPresent);
            _stack.SetItemShown(_grid, open.ItemSlots.IsPresent);
            _stack.SetItemShown(_fluids, 0 < open.Fluids.Count);
            _stack.SetItemShown(_mode, open.Mode.IsPresent);
            if (open.StationName.IsPresent) _nameRow.Render(stationNameInput);
            if (open.ItemSlots.IsPresent) _grid.Render(open.ItemSlots.Value, BlockDetailMath.AllSlotIndices(open.ItemSlots.Value.Count));
            _fluids.Render(open.Fluids);
            if (open.Mode.IsPresent) _mode.Render(open.Mode.Value);
        }
    }
}
