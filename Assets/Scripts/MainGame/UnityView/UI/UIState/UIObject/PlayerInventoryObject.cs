using System.Collections.Generic;
using MainGame.UnityView.UI.Inventory.View;
using MainGame.UnityView.UI.Inventory.View.SubInventory;
using UnityEngine;

namespace MainGame.UnityView.UI.UIState.UIObject
{
    public class PlayerInventoryObject : MonoBehaviour
    {
        [SerializeField] private PlayerInventorySlots playerInventorySlots;
        public void SetCraftInventory()
        {
            //todo クラフト画面を動的に構築する
            var oneSlots = new List<OneSlot>() {new(172,272,0)};
            var arraySlots = new List<ArraySlot>() {new(-172,272,10,3,3)};
            var craftSubInventoryData = new SubInventoryViewData(oneSlots, arraySlots);
            
            
            playerInventorySlots.SetSubSlots(craftSubInventoryData);
        }
    }
}