using System;

namespace Client.Ui.State.Hotbar.Logic
{
    /// <summary>
    /// D&D の結果。Web の null（何もしない）は HotbarNoDropAction で表す
    /// The outcome of a drop; the Web's null (do nothing) is HotbarNoDropAction
    /// </summary>
    public abstract class HotbarDropAction
    {
        private protected HotbarDropAction()
        {
        }
    }

    public sealed class HotbarAssignAction : HotbarDropAction
    {
        public int SlotIndex { get; }
        public Guid TargetId { get; }

        public HotbarAssignAction(int slotIndex, Guid targetId)
        {
            SlotIndex = slotIndex;
            TargetId = targetId;
        }
    }

    public sealed class HotbarSwapAction : HotbarDropAction
    {
        public int FromSlotIndex { get; }
        public int ToSlotIndex { get; }

        public HotbarSwapAction(int fromSlotIndex, int toSlotIndex)
        {
            FromSlotIndex = fromSlotIndex;
            ToSlotIndex = toSlotIndex;
        }
    }

    public sealed class HotbarClearAction : HotbarDropAction
    {
        public int SlotIndex { get; }

        public HotbarClearAction(int slotIndex)
        {
            SlotIndex = slotIndex;
        }
    }

    public sealed class HotbarNoDropAction : HotbarDropAction
    {
        public static readonly HotbarNoDropAction Instance = new();

        private HotbarNoDropAction()
        {
        }
    }
}
