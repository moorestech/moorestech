using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.UI.Inventory.Main;
using Game.PlayerInventory.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail
{
    public class TrainRailPlaceSystem : IPlaceSystem
    {
        private readonly TrainRailPlaceSystemService _trainRailPlaceSystemService;
        public TrainRailPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, ILocalPlayerInventory localPlayerInventory)
        {
            _trainRailPlaceSystemService = new TrainRailPlaceSystemService(mainCamera, previewBlockController, localPlayerInventory);
        }
        
        public void Enable()
        {
            _trainRailPlaceSystemService.Enable();
        }
        
        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            var slotIndex = context.CurrentSelectHotbarSlotIndex;
            var inventorySlot = PlayerInventoryConst.HotBarSlotToInventorySlot(slotIndex);
            _trainRailPlaceSystemService.ManualUpdate(inventorySlot);
        }
        
        public void Disable()
        {
            _trainRailPlaceSystemService.Disable();
        }
    }
}