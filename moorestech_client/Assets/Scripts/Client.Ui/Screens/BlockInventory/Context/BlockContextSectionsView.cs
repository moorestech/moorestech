using Client.Ui.Content;
using Client.Ui.Foundation.Layout;
using Client.Ui.State.BlockInventory.Context;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 文脈の 3 欄（部屋 → 抽選 → 搬入停止）を Stack gap=sm で並べる。無い欄は SetItemShown で隠す
    /// Lays out the three context sections (room → draw → stall) in Stack gap=sm, hiding absent ones via SetItemShown
    /// </summary>
    public sealed class BlockContextSectionsView : VisualElement
    {
        private readonly UiStack _stack = new(UiGap.Sm);
        private readonly CleanRoomSectionView _cleanRoom = new();
        private readonly CleanRoomYieldSectionView _cleanRoomYield = new();
        private readonly MachineInputStallSectionView _stall;

        public BlockContextSectionsView(GameContentPresentation content)
        {
            name = "block-context-sections";
            _stall = new MachineInputStallSectionView(content);
            _stack.AddItem(_cleanRoom);
            _stack.AddItem(_cleanRoomYield);
            _stack.AddItem(_stall);
            Add(_stack);
        }

        public void Render(BlockContextViewModel viewModel)
        {
            // 無い欄は隠し、ある欄だけ描く（Absent の判定は ContextEntry.IsAbsent の 1 本）
            // Hide absent sections and draw the rest (absence is judged once by ContextEntry.IsAbsent)
            _stack.SetItemShown(_cleanRoom, !viewModel.CleanRoom.IsAbsent);
            if (!viewModel.CleanRoom.IsAbsent) _cleanRoom.Render(viewModel.CleanRoom);
            _stack.SetItemShown(_cleanRoomYield, !viewModel.CleanRoomYield.IsAbsent);
            if (!viewModel.CleanRoomYield.IsAbsent) _cleanRoomYield.Render(viewModel.CleanRoomYield);
            _stack.SetItemShown(_stall, !viewModel.MachineInputStall.IsAbsent);
            if (!viewModel.MachineInputStall.IsAbsent) _stall.Render(viewModel.MachineInputStall);
        }
    }
}
