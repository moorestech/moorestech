using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MainGame.Basic;
using MainGame.UnityView.UI.Inventory.Element;
using UnityEngine;

namespace MainGame.Inventory
{
    public class PlayerInventoryTestModule : MonoBehaviour
    {
        [SerializeField] private ItemImages itemImages;
        
        [SerializeField] private PlayerInventorySlotsInput playerInventorySlotsInput;
        [SerializeField] private PlayerInventoryView playerInventoryView;
        
        private void Start()
        {
            var inventory = new PlayerInventoryModel();
            var inventoryList = (List<ItemStack>) typeof(PlayerInventoryModel)
                .GetField("_mainInventory", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(inventory);
            
            inventoryList[0] = new ItemStack(1,100);
            
            playerInventorySlotsInput.Construct(inventory);
            playerInventoryView.Construct(inventory,itemImages);
        }
    }
}