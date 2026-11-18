using Client.Ui.Foundation.Layout;
using Client.Ui.Screens.BlockInventory.Fluid;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.Train.Inventory;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Train
{
    /// <summary>
    /// 持ち物タブ。液体貨車は液体の中身、それ以外は貨車スロット（Web TrainInventoryBody と同じ分岐）
    /// Inventory tab: fluid cars show fluid contents, others show car slots (same branch as Web TrainInventoryBody)
    /// </summary>
    public sealed class TrainInventoryContentsView : VisualElement
    {
        private readonly BlockItemGrid _grid;
        private readonly FluidInventoryContents _fluids;

        public TrainInventoryContentsView(BlockItemGrid grid, FluidInventoryContents fluids)
        {
            name = "train-inventory-contents";
            _grid = grid;
            _fluids = fluids;
            Add(_grid);
            Add(_fluids);
        }

        public void Render(TrainInventoryBodyViewModel model)
        {
            var hasFluids = 0 < model.Fluids.Count;
            UiDisplay.SetShown(_grid, !hasFluids);
            UiDisplay.SetShown(_fluids, hasFluids);
            if (hasFluids) _fluids.Render(model.Fluids);
            else _grid.Render(model.ItemSlots, BlockDetailMath.AllSlotIndices(model.ItemSlots.Count));
        }
    }
}
