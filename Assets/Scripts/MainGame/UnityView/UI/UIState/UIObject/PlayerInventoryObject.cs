using System;
using System.Collections.Generic;
using MainGame.UnityView.UI.Inventory.View;
using MainGame.UnityView.UI.Inventory.View.SubInventory;
using UnityEngine;

namespace MainGame.UnityView.UI.UIState.UIObject
{
    public class PlayerInventoryObject : MonoBehaviour
    {
        public event Action OnResultSlotClick;
        
        [SerializeField] private PlayerInventorySlots playerInventorySlots;
        public void SetCraftInventory()
        {
            var resultSlotOption = new InventorySlotElementOptions(){IsEnableControllerEvent = false};
            resultSlotOption.OnLeftClickDown += _ => OnResultSlotClick?.Invoke();
            
            var resultSlot = new OneSlot(172, 272, 0,resultSlotOption);
            
            var craftSlot = new List<ArraySlot>() {new(-172,272,10,3,3)};
            var craftSubInventoryData = new SubInventoryViewData(new List<OneSlot>() {resultSlot}, craftSlot);
            
            
            playerInventorySlots.SetSubSlots(craftSubInventoryData);
        }
    }
}