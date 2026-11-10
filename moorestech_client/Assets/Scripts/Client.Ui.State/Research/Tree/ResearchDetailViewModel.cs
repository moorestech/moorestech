using System;
using System.Collections.Generic;
using Client.Ui.State.Research.Logic;
using Core.Master;

namespace Client.Ui.State.Research.Tree
{
    /// <summary>
    /// 選択ノードの詳細（研究ボタンの状態・消費アイテムの割当・解放物）
    /// Details of the selected node (research button state, consume allocation, unlocks)
    /// </summary>
    public sealed class ResearchDetailViewModel
    {
        public Guid Guid { get; }
        public ResearchButtonState Button { get; }
        public IReadOnlyList<ResearchConsumeSlotViewModel> ConsumeSlots { get; }
        public IReadOnlyList<UnlockEntry> Unlocks { get; }

        public ResearchDetailViewModel(Guid guid, ResearchButtonState button, IReadOnlyList<ResearchConsumeSlotViewModel> consumeSlots, IReadOnlyList<UnlockEntry> unlocks)
        {
            Guid = guid;
            Button = button;
            ConsumeSlots = consumeSlots;
            Unlocks = unlocks;
        }
    }

    /// <summary>
    /// 消費アイテム 1 枠（必要数・割り当てた所持数・不足）。UITK には所持数の未受信が無いので割当は常にある（A-13）
    /// One consume slot (required, allocated owned count, shortage); UITK has no unreceived inventory, so the allocation always exists (A-13)
    /// </summary>
    public sealed class ResearchConsumeSlotViewModel
    {
        public ItemId ItemId { get; }
        public int Required { get; }
        public int Allocated { get; }
        public bool Lacking { get; }

        public ResearchConsumeSlotViewModel(ItemId itemId, int required, int allocated, bool lacking)
        {
            ItemId = itemId;
            Required = required;
            Allocated = allocated;
            Lacking = lacking;
        }
    }
}
