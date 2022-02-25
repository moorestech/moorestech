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
            //プロトコル送信クラスのインスタンスを作成
            var sendMain = new SendMainInventoryMoveItemProtocol(new TestSocketModule(), new PlayerConnectionSetting(0));
            var sendCraft = new SendCraftingInventoryMoveItemProtocol();
            var sendCraftMain = new SendCraftingInventoryMainInventoryMoveItemProtocol();
            
            //インベントリデータキャッシュの取得
            var mainInventoryDataCache = GetComponent<InventoryViewTestModule>().MainInventoryDataCache;
            var craftingInventoryDataCache = GetComponent<InventoryViewTestModule>().CraftingInventoryDataCache;

            var itemMove = new PlayerInventoryMainInventoryItemMoveService(
                mainInventoryDataCache,craftingInventoryDataCache,
                sendMain,sendCraft,sendCraftMain);
            var craftingInventory = new CraftingInventoryDataCache(new CraftingInventoryUpdateEvent(),craftingInventoryItem);
            
            playerInventoryEquippedItemImageSet.Construct(mainInventoryItem,craftingInventoryItem,new MainInventoryUpdateEvent(),new CraftingInventoryUpdateEvent());
            playerInventoryInput.Construct(playerInventoryEquippedItemImageSet,itemMove,
            mainInventoryItem,mainInventoryDataCache,craftingInventoryItem, craftingInventory);
        }
    }
}