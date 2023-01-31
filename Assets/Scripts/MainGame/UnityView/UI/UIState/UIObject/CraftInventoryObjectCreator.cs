using System;
using System.Collections.Generic;
using MainGame.Basic;
using MainGame.UnityView.UI.Builder.BluePrint;
using MainGame.UnityView.UI.Inventory.View;
using MainGame.UnityView.UI.Inventory.View.SubInventory;
using UnityEngine;

namespace MainGame.UnityView.UI.UIState.UIObject
{
    /// <summary>
    /// UIブループリントシステムを使って、クラフト画面のサブインベントリを構築する
    /// </summary>
    public class CraftInventoryObjectCreator : MonoBehaviour
    {
        public event Action OnResultSlotClick;
        
        [SerializeField] private PlayerInventorySlots playerInventorySlots;
        public void SetCraftInventory()
        {
            var resultSlotOption = new InventorySlotElementOptions(){IsEnableControllerEvent = false};
            resultSlotOption.OnLeftClickDown += _ => OnResultSlotClick?.Invoke();
            
            var resultSlot = new UIBluePrintItemSlot(172, 272, 0,resultSlotOption);
            
            var craftSlot = new List<UIBluePrintItemSlotArray>() {new(-172,272,10,3,3)};
            var craftSubInventoryData = new SubInventoryViewBluePrint
            {
                OneSlots = new List<UIBluePrintItemSlot>() {resultSlot},
                ArraySlots = craftSlot
            };
            
            //クラフト結果スロットは収集から除外するオプションの設定
            var withoutSlot = PlayerInventoryConstant.CraftingSlotSize + PlayerInventoryConstant.MainInventorySize;
            var subInventoryOption = new SubInventoryOptions(){WithoutCollectSlots = new List<int> {withoutSlot}};
            
            playerInventorySlots.SetSubSlots(craftSubInventoryData,subInventoryOption);
        }
    }
}