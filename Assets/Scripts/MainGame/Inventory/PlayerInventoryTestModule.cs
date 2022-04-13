

using System.Collections.Generic;
using System.Reflection;
using Core.Item;
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
            var single = new SinglePlayInterface(ServerConst.ServerConfigDirectory);
            var itemStackFactory = single.ItemStackFactory;
            var inventory = new PlayerInventoryModel(itemStackFactory,single.ItemConfig);
            var inventoryList = (List<IItemStack>) typeof(PlayerInventoryModel)
                .GetField("_mainInventory", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(inventory);
            
            inventoryList[0] = itemStackFactory.Create(1,100);
            inventoryList[1] = itemStackFactory.Create(1,100);
            inventoryList[2] = itemStackFactory.Create(2,100);
            inventoryList[3] = itemStackFactory.Create(2,100);
            
            playerInventorySlotsInputControl.Construct(inventory);
            playerInventoryView.Construct(inventory,itemImages);
        }
    }
}