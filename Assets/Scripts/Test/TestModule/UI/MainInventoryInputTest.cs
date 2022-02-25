using System.Collections;
using MainGame.Control.UI.Inventory;
using MainGame.Control.UI.Inventory.ItemMove;
using MainGame.GameLogic;
using MainGame.GameLogic.Inventory;
using MainGame.Network;
using MainGame.Network.Event;
using MainGame.Network.Send;
using MainGame.UnityView;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;

namespace Test.TestModule.UI
{
    public class MainInventoryInputTest : MonoBehaviour
    {
        [SerializeField] private PlayerInventoryInput playerInventoryInput;
        [SerializeField] private PlayerInventoryEquippedItemImageSet playerInventoryEquippedItemImageSet;

        [SerializeField] private MainInventoryItemView mainInventoryItem;
        [SerializeField] private HotBarItemView hotBarItem;
        [SerializeField] private CraftingInventoryItemView craftingInventoryItem;

        private void Start()
        {
            var mainInventoryDataCache = GetComponent<InventoryViewTestModule>().MainInventoryDataCache;
            var itemMove = new PlayerInventoryMainInventoryItemMoveService(
                mainInventoryDataCache,
                new SendMainInventoryMoveItemProtocol(new TestSocketModule()));
            var craftingInventory = new CraftingInventoryDataCache(new CraftingInventoryUpdateEvent(),craftingInventoryItem);
            
            playerInventoryEquippedItemImageSet.Construct(mainInventoryItem,craftingInventoryItem,new MainInventoryUpdateEvent(),new CraftingInventoryUpdateEvent());
            playerInventoryInput.Construct(playerInventoryEquippedItemImageSet,itemMove,
            mainInventoryItem,mainInventoryDataCache,craftingInventoryItem, craftingInventory);
        }
    }
}