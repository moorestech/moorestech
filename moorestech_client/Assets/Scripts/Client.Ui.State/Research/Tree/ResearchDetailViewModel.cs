using System;
using System.Collections.Generic;
using Client.Ui.State.Research.Logic;
using Core.Master;

namespace Client.Ui.State.Research.Tree
{
    /// <summary>
    /// 選択ノードの詳細
    /// Details of the selected node
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
    /// 消費アイテム1枠（必要数・割当・不足）
    /// One consume slot (required, allocated, short)
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
