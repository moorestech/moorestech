namespace Client.Ui.State.Hotbar.Logic
{
    /// <summary>
    /// ホットバー D&D の落とし先。ビルドメニューのエントリへは落とせない
    /// A hotbar drop target; a build-menu entry is never a place to drop onto
    /// </summary>
    public abstract class HotbarDropTarget
    {
        private protected HotbarDropTarget()
        {
        }
    }

    public sealed class HotbarSlotDropTarget : HotbarDropTarget
    {
        public int SlotIndex { get; }

        public HotbarSlotDropTarget(int slotIndex)
        {
            SlotIndex = slotIndex;
        }
    }

    public sealed class OutsideHotbarDropTarget : HotbarDropTarget
    {
        public static readonly OutsideHotbarDropTarget Instance = new();

        private OutsideHotbarDropTarget()
        {
        }
    }
}
