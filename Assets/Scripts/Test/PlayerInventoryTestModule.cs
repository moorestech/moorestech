using System.Collections.Generic;
using System.Linq;
using Game.PlayerInventory.Interface;
using GameConst;
using MainGame.Basic;
using MainGame.UnityView.UI.Builder.BluePrint;
using MainGame.UnityView.UI.Inventory.Control;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using MainGame.UnityView.UI.Inventory.View.SubInventory;
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
        [SerializeField] private PlayerInventoryItemNamePresenter playerInventoryItemNamePresenter;
        
        private void Start()
        {
            var single = new SinglePlayInterface(ServerConst.ServerModsDirectory);
            var inventoryModel = new PlayerInventoryViewModel(single);
            var inventoryController = new PlayerInventoryViewModelController(single,inventoryModel,playerInventorySlots);

            inventoryController.OnItemSlotGrabbed += (slot, count) =>  Debug.Log($"grab {slot} {count}");
            inventoryController.OnItemSlotAdded += (slot, count) => Debug.Log($"put {slot} {count}");
            inventoryController.OnItemSlotCollect += (slot, count) => Debug.Log($"collect {slot} {count}");

            playerInventorySlotsInputControl.Construct(inventoryController);
            playerInventoryPresenter.Construct(inventoryController,itemImages,inventoryModel);
            
            var oneSlots = new List<OneSlot>() {new(172,272,0,new InventorySlotElementOptions())};
            var arraySlots = new List<ArraySlot>() {new(-172,272,10,3,3)};
            var textElements = new List<TextElement>() {new(0,470,1,"TextText",40)};
            var subInventoryData = new SubInventoryViewBluePrint()
            {
                OneSlots = oneSlots, 
                ArraySlots = arraySlots,
                TextElements = textElements
            };
            
            playerInventorySlots.SetSubSlots(subInventoryData,new SubInventoryOptions());
            
            
            var mainInventory = new ItemStack[PlayerInventoryConst.MainInventorySize];
            mainInventory[0] = new (1,100);
            mainInventory[1] = new (1,100);
            mainInventory[2] = new (2,100);
            mainInventory[3] = new (2,100);
            
            inventoryModel.SetMainInventory(mainInventory.ToList());
            
            
            var subInventory = new ItemStack[PlayerInventoryConst.CraftingSlotSize];
            subInventory[0] = new (1,100);
            subInventory[1] = new (2,100);
            
            
            inventoryModel.SetSubInventory(subInventory.ToList());
            
            
            playerInventoryItemNamePresenter.Construct(inventoryModel,inventoryController,itemImages);
        }
    }
}