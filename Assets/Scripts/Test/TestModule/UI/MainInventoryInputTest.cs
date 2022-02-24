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
        [SerializeField] private MainInventoryInput mainInventoryInput;
        [SerializeField] private PlayerInventoryEquippedItemImageSet playerInventoryEquippedItemImageSet;

        [SerializeField] private MainInventoryItemView mainInventoryItem;
        [SerializeField] private BlockInventoryItemView blockInventoryItem;

        private void Start()
        {
            var mainInventoryDataCache = GetComponent<InventoryViewTestModule>().MainInventoryDataCache;
            var itemMove = new BlockInventoryMainInventoryItemMoveService(
                new PlayerConnectionSetting(0),
                new BlockInventoryDataCache(new BlockInventoryUpdateEvent(),blockInventoryItem),
                mainInventoryDataCache,
                new SendBlockInventoryMoveItemProtocol(new TestSocketModule()),
                new SendBlockInventoryMainInventoryMoveItemProtocol(new TestSocketModule()),
                new SendMainInventoryMoveItemProtocol(new TestSocketModule()));
            
            playerInventoryEquippedItemImageSet.Construct(mainInventoryItem,new MainInventoryUpdateEvent());
            mainInventoryInput.Construct(mainInventoryItem,itemMove,mainInventoryDataCache,playerInventoryEquippedItemImageSet);
        }
    }
}