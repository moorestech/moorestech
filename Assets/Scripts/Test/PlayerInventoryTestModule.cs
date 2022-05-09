using System.Collections.Generic;
using System.Linq;
using Game.PlayerInventory.Interface;
using GameConst;
using MainGame.Basic;
using MainGame.UnityView.UI.Inventory.Control;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using MainGame.UnityView.UI.Inventory.View.SubInventory;
using MainGame.UnityView.UI.Inventory.View.SubInventory.Element;
using SinglePlay;
using UnityEngine;

namespace Test
{
    public class PlayerInventoryTestModule : MonoBehaviour
    {
        [SerializeField] private ItemImages itemImages;

        [SerializeField] private PlayerInventorySlots playerInventorySlots;
        [SerializeField] private PlayerInventorySlotsInputControl playerInventorySlotsInputControl;
        [SerializeField] private PlayerInventoryPresenter playerInventoryPresenter;
        
        private void Start()
        {
            var single = new SinglePlayInterface(ServerConst.ServerConfigDirectory);
            var inventoryModel = new PlayerInventoryViewModel(single);
            var inventoryController = new PlayerInventoryViewModelController(single,inventoryModel,playerInventorySlots);

            inventoryController.OnItemSlotGrabbed += (slot, count) =>  Debug.Log($"grab {slot} {count}");
            inventoryController.OnItemSlotAdded += (slot, count) => Debug.Log($"put {slot} {count}");
            inventoryController.OnItemSlotCollect += (slot, count) => Debug.Log($"collect {slot} {count}");

            playerInventorySlotsInputControl.Construct(inventoryController);
            playerInventoryPresenter.Construct(inventoryController,itemImages,inventoryModel);
            
            var oneSlots = new List<OneSlot>() {new(172,272,0,new InventorySlotElementOptions())};
            var arraySlots = new List<ArraySlot>() {new(-172,272,10,3,3)};
            var subInventoryData = new SubInventoryViewBluePrint(oneSlots, arraySlots);
            
            playerInventorySlots.SetSubSlots(subInventoryData,new SubInventoryOptions());
            
            
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