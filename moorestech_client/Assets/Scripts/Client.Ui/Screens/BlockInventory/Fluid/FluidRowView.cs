using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.State.BlockInventory.Fluid;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の行（Web の FluidSlotRow を区画に置いたもの）。液体の枠の横並びと、設定次第の進捗の矢印
    /// Fluid row (the Web FluidSlotRow placed in a section): fluid cells and an optional progress arrow
    /// </summary>
    public sealed class FluidRowView : VisualElement
    {
        private readonly FluidSlotRow _row;
        private readonly GameContentPresentation _content;

        public FluidRowView(string testid, GameContentPresentation content)
        {
            _content = content;
            _row = new FluidSlotRow(testid);
            Add(_row);
        }

        public void Render(BlockFluidSlotsViewModel viewModel, bool showProgress)
        {
            var contents = new FluidSlotContent[viewModel.Slots.Count];
            for (var i = 0; i < contents.Length; i++) contents[i] = FluidSlotContentMapper.Map(viewModel.Slots[i], _content);
            _row.SetFluids(contents);

            // 進捗は設定で出す blockType かつ値があるときだけ
            // Progress only for block types that show it and when a value exists
            if (showProgress && viewModel.HasProgress) _row.SetProgress(viewModel.Progress);
            else _row.ClearProgress();
        }
    }
}
