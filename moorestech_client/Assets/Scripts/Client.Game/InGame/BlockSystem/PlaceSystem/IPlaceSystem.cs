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
        
        public PlaceSystemUpdateContext(ItemId holdingItemId, bool isSelectSlotChanged, int previousSelectHotbarSlotIndex, int currentSelectHotbarSlotIndex)
        {
            HoldingItemId = holdingItemId;
            IsSelectSlotChanged = isSelectSlotChanged;
            PreviousSelectHotbarSlotIndex = previousSelectHotbarSlotIndex;
            CurrentSelectHotbarSlotIndex = currentSelectHotbarSlotIndex;
        }
    }
}