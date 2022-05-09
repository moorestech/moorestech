using System;
using System.Collections.Generic;
using MainGame.Basic;
using MainGame.UnityView.UI.Inventory.View;
using MainGame.UnityView.UI.Inventory.View.SubInventory;
using MainGame.UnityView.UI.Inventory.View.SubInventory.Element;
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
            var craftSubInventoryData = new SubInventoryViewBluePrint
            {
                OneSlots = new List<OneSlot>() {resultSlot},
                ArraySlots = craftSlot
            };
            
            //結果スロットは収集から除外するオプションの設定
            var withoutSlot = PlayerInventoryConstant.CraftingSlotSize + PlayerInventoryConstant.MainInventorySize;
            var subInventoryOption = new SubInventoryOptions(){WithoutCollectSlots = new List<int> {withoutSlot}};
            
            playerInventorySlots.SetSubSlots(craftSubInventoryData,subInventoryOption);
        }
    }
}