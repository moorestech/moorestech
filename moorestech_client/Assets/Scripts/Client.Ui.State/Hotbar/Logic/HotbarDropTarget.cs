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

    // 列の枠の間の隙間。まだ HUD の中なので枠外（解除）とは別の先（Web の data-hotbar-row）
    // The gap between slots in the row; still inside the HUD, so a different target from outside (Web data-hotbar-row)
    public sealed class HotbarRowGapDropTarget : HotbarDropTarget
    {
        public static readonly HotbarRowGapDropTarget Instance = new();

        private HotbarRowGapDropTarget()
        {
        }
    }
}
