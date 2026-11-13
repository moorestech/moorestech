using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Fluid;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の行（Web の FluidSlotRow）
    /// Fluid row (Web FluidSlotRow in a section): fluid cells and an optional progress arrow
    /// </summary>
    public sealed class FluidRowView : VisualElement, IDetailSectionView<BlockFluidSlotsViewModel>
    {
        private readonly FluidSlotRow _row;
        private readonly bool _showProgress;
        private readonly GameContentPresentation _content;

        public VisualElement Root => this;

        public FluidRowView(string testid, bool showProgress, GameContentPresentation content)
        {
            _showProgress = showProgress;
            _content = content;
            _row = new FluidSlotRow(testid);
            Add(_row);
        }

        public void Render(BlockFluidSlotsViewModel viewModel)
        {
            var contents = new FluidSlotContent[viewModel.Slots.Count];
            for (var i = 0; i < contents.Length; i++) contents[i] = FluidSlotContentMapper.Map(viewModel.Slots[i], _content);
            _row.SetFluids(contents);

            // 進捗は設定の型で値があるときだけ
            // Progress only for configured types with a value
            if (_showProgress && viewModel.HasProgress) _row.SetProgress(viewModel.Progress);
            else _row.ClearProgress();
        }
    }
}
