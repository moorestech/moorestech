using System;

namespace Client.Ui.State.Hotbar.Logic
{
    /// <summary>
    /// ホットバー D&D のドラッグ元。枠の外から掴むことは無いので「枠外」は持たない
    /// A hotbar drag source; nothing is grabbed from outside a slot, so "outside" is not one of these
    /// </summary>
    public abstract class HotbarDragSource
    {
        private protected HotbarDragSource()
        {
        }
    }

    // 割当を生む唯一の経路はビルドメニューのエントリのドラッグ
    // The only path that creates an assignment is dragging a build-menu entry
    public sealed class BuildMenuEntryDragSource : HotbarDragSource
    {
        public Guid TargetId { get; }

        public BuildMenuEntryDragSource(Guid targetId)
        {
            TargetId = targetId;
        }
    }

    public sealed class HotbarSlotDragSource : HotbarDragSource
    {
        public int SlotIndex { get; }

        public HotbarSlotDragSource(int slotIndex)
        {
            SlotIndex = slotIndex;
        }
    }
}
