using System.Collections;
using MainGame.Control.UI.Inventory;
using MainGame.GameLogic;
using MainGame.GameLogic.Inventory;
using MainGame.Network;
using MainGame.Network.Event;
using MainGame.Network.Send;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;

namespace Test.TestModule.UI
{
    public class MouseInventoryInputTest : MonoBehaviour
    {
        [SerializeField] private PlayerInventoryInput playerInventoryInput;
        [SerializeField] private PlayerInventoryEquippedItemImageSet playerInventoryEquippedItemImageSet;

        [SerializeField] private PlayerInventoryItemView playerInventoryItem;
        [SerializeField] private BlockInventoryItemView blockInventoryItem;

        private void Start()
        {
            var playerInventory = new PlayerInventoryDataCache(new PlayerInventoryUpdateEvent(),playerInventoryItem);
            var itemMove = new InventoryItemMoveService(
                new PlayerConnectionSetting(0),
                new BlockInventoryDataCache(new BlockInventoryUpdateEvent(),blockInventoryItem),
                playerInventory,
                new SendBlockInventoryMoveItemProtocol(new TestSocketModule()),
                new SendBlockInventoryPlayerInventoryMoveItemProtocol(new TestSocketModule()),
                new SendPlayerInventoryMoveItemProtocol(new TestSocketModule()));
            
            playerInventoryEquippedItemImageSet.Construct(playerInventoryItem,new PlayerInventoryUpdateEvent());
            playerInventoryInput.Construct(playerInventoryItem,itemMove,playerInventory,playerInventoryEquippedItemImageSet);

            StartCoroutine(PostStart());
        }

        private IEnumerator PostStart()
        {
            yield return new WaitForSeconds(0.1f);
            playerInventoryInput.PostStart();
        }
    }
}