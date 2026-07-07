using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    public interface IPlaceSystem
    {
        public void Enable();
        
        public void ManualUpdate(PlaceSystemUpdateContext context);
        
        public void Disable();
    }
    
    public struct PlaceSystemUpdateContext
    {
        public readonly ItemId HoldingItemId;
        
        public readonly bool IsSelectSlotChanged;
        public readonly int PreviousSelectHotbarSlotIndex;
        public readonly int CurrentSelectHotbarSlotIndex;

        // ビルドメニューで選択中のブロック（未選択はnull）
        // The block selected in the build menu (null when nothing is selected)
        public readonly BlockId? SelectedBlockId;

        public PlaceSystemUpdateContext(ItemId holdingItemId, bool isSelectSlotChanged, int previousSelectHotbarSlotIndex, int currentSelectHotbarSlotIndex, BlockId? selectedBlockId)
        {
            HoldingItemId = holdingItemId;
            IsSelectSlotChanged = isSelectSlotChanged;
            PreviousSelectHotbarSlotIndex = previousSelectHotbarSlotIndex;
            CurrentSelectHotbarSlotIndex = currentSelectHotbarSlotIndex;
            SelectedBlockId = selectedBlockId;
        }
    }
}