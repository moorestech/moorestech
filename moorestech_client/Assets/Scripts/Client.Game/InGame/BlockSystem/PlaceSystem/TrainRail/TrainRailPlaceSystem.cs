using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Input;
using Game.PlayerInventory.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail
{
    public class TrainRailPlaceSystem : IPlaceSystem
    {
        private readonly TrainRailPlaceSystemService _trainRailPlaceSystemService;
        private readonly ILocalPlayerInventory _localPlayerInventory;
            
        public TrainRailPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, ILocalPlayerInventory localPlayerInventory)
        {
            _trainRailPlaceSystemService = new TrainRailPlaceSystemService(mainCamera, previewBlockController);
            _localPlayerInventory = localPlayerInventory;
        }
        
        public void Enable()
        {
            _trainRailPlaceSystemService.Enable();
        }
        
        public void ManualUpdate(PlaceSystemUpdateContext context)
        {
            var slotIndex = context.CurrentSelectHotbarSlotIndex;
            var inventorySlot = PlayerInventoryConst.HotBarSlotToInventorySlot(slotIndex);
            var itemStack = _localPlayerInventory[inventorySlot];
            var itemId = itemStack.Id;
            var placeInfo = _trainRailPlaceSystemService.ManualUpdate(itemId);
            if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return;
            
            PlaceSystemUtil.SendPlaceProtocol(new List<PlaceInfo> { placeInfo }, context);
        }
        
        public void Disable()
        {
            _trainRailPlaceSystemService.Disable();
        }
    }
}