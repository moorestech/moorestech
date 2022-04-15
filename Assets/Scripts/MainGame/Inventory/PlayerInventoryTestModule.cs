

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Core.Item;
using Game.PlayerInventory.Interface;
using GameConst;
using MainGame.Basic;
using MainGame.Inventory.SubInventory;
using MainGame.UnityView.UI.Inventory.Element;
using SinglePlay;
using UnityEngine;

namespace MainGame.Inventory
{
    public class PlayerInventoryTestModule : MonoBehaviour
    {
        [SerializeField] private ItemImages itemImages;

        [SerializeField] private PlayerInventorySlots playerInventorySlots;
        [SerializeField] private PlayerInventorySlotsInputControl playerInventorySlotsInputControl;
        [SerializeField] private PlayerInventoryView playerInventoryView;
        
        private void Start()
        {
            var single = new SinglePlayInterface(ServerConst.ServerConfigDirectory);
            var itemStackFactory = single.ItemStackFactory;
            var inventoryModel = new PlayerInventoryModel(itemStackFactory);
            var inventoryController = new PlayerInventoryModelController(itemStackFactory,single.ItemConfig,inventoryModel);

            playerInventorySlotsInputControl.Construct(inventoryController);
            playerInventoryView.Construct(inventoryController,itemImages,inventoryModel);
            
            var oneSlots = new List<OneSlot>() {new(172,272,0)};
            var arraySlots = new List<ArraySlot>() {new(-172,272,10,3,3)};
            var subInventoryData = new SubInventoryViewData(oneSlots, arraySlots);
            
            playerInventorySlots.SetSubSlots(subInventoryData);
            
            
            var mainInventory = new ItemStack[PlayerInventoryConst.MainInventorySize];
            mainInventory[0] = new (1,100);
            mainInventory[1] = new (1,100);
            mainInventory[2] = new (2,100);
            mainInventory[3] = new (2,100);
            
            inventoryModel.SetMainInventory(mainInventory.ToList());
            
            
            var subInventory = new ItemStack[PlayerInventoryConst.CraftingInventorySize];
            subInventory[0] = new (1,100);
            subInventory[1] = new (2,100);
            
            
            inventoryModel.SetSubInventory(subInventory.ToList());
        }
    }
}