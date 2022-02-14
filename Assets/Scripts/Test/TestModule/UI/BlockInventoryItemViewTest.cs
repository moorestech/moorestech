using System.Collections.Generic;
using MainGame.Control.UI.Inventory;
using MainGame.GameLogic;
using MainGame.GameLogic.Inventory;
using MainGame.Network;
using MainGame.Network.Event;
using MainGame.Network.Send;
using MainGame.UnityView;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;
using UnityEngine.Serialization;

namespace Test.TestModule.UI
{
    public class BlockInventoryItemViewTest : MonoBehaviour
    {
        [SerializeField] private BlockInventoryItemView blockInventoryItemView;
        [SerializeField] private BlockInventoryEquippedItemImageSet blockInventoryEquippedItemImageSet;
        [SerializeField] private ItemImages itemImages;
        [SerializeField] private BlockInventoryInput blockInventoryInput;

        private void Start()
        {
            blockInventoryItemView.Construct(itemImages);
            var queue = new GameObject().AddComponent<QueueInsertionMainThreadByExecution>();
            var blockInventory = new BlockInventoryDataCache(new BlockInventoryUpdateEvent(),blockInventoryItemView);
            var playerInventoryItemView = new GameObject().AddComponent<PlayerInventoryItemView>();
            var itemMove = new InventoryItemMoveService(
                new PlayerConnectionSetting(0),
                blockInventory,
                GetComponent<InventoryViewTestModule>().PlayerInventoryDataCache,
                new SendBlockInventoryMoveItemProtocol(new TestSocketModule()),
                new SendBlockInventoryPlayerInventoryMoveItemProtocol(new TestSocketModule()),
                new SendPlayerInventoryMoveItemProtocol(new TestSocketModule()));

            blockInventoryInput.Construct(blockInventoryItemView,itemMove,blockInventory,blockInventoryEquippedItemImageSet);
            blockInventoryEquippedItemImageSet.Construct(blockInventoryItemView,new PlayerInventoryUpdateEvent(),new BlockInventoryUpdateEvent());
            blockInventoryInput.PostStart();
            
            //プレイヤーインベントリのアイテム設定
            
            //slot id count
            List<(int,int,int)> items = new List<(int,int,int)>();

            //アイテムの設定
            items.Add((0,1,5));
            items.Add((5,2,10));
            items.Add((10,2,1));
            items.Add((40,2,1));
            items.Add((44,2,1));

            //イベントを発火
            foreach (var item in items)
            {
                //playerInventory.OnOnInventoryUpdate(item.Item1,item.Item2,item.Item3);
            }
            
            
            //blockInventoryを開く
            //blockInventory.OnSettingInventoryInvoke("",3,1);
            //BlockInventoryのアイテム設定
            //blockInventory.OnInventoryUpdateInvoke(0,2,6);
            //blockInventory.OnInventoryUpdateInvoke(3,1,7);
        }
    }
}