using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameConst;
using MainGame.Basic;
using MainGame.UnityView.UI.Inventory.Element;
using SinglePlay;
using UnityEngine;

namespace MainGame.Inventory
{
    public class PlayerInventoryTestModule : MonoBehaviour
    {
        [SerializeField] private ItemImages itemImages;
        
        [SerializeField] private PlayerInventorySlotsInputControl playerInventorySlotsInputControl;
        [SerializeField] private PlayerInventoryView playerInventoryView;
        
        private void Start()
        {
            var inventory = new PlayerInventoryModel(new SinglePlayInterface(ServerConst.ServerConfigDirectory));
            var inventoryList = (List<ItemStack>) typeof(PlayerInventoryModel)
                .GetField("_mainInventory", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(inventory);
            
            inventoryList[0] = new ItemStack(1,100);
            
            playerInventorySlotsInputControl.Construct(inventory);
            playerInventoryView.Construct(inventory,itemImages);
        }
    }
}