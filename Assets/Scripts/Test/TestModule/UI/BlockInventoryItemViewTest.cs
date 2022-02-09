using System;
using System.Collections.Generic;
using MainGame.Control.UI.Inventory;
using MainGame.GameLogic.Event;
using MainGame.UnityView.UI.Inventory.Element;
using MainGame.UnityView.UI.Inventory.View;
using UnityEngine;

namespace Test.TestModule.UI
{
    public class BlockInventoryItemViewTest : MonoBehaviour
    {
        [SerializeField] private BlockInventoryItemView blockInventoryItemView;
        [SerializeField] private ItemImages itemImages;
        [SerializeField] private MouseBlockInventoryInput mouseBlockInventoryInput;

        private void Start()
        {
            var playerInventory = new PlayerInventoryViewUpdateEvent();
            var blockInventory = new BlockInventoryUpdateEvent();
            blockInventoryItemView.Construct(playerInventory,itemImages,blockInventory);
            
            mouseBlockInventoryInput.Construct(blockInventoryItemView,new BlockInventoryItemMoveTest());
            
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
                playerInventory.OnOnInventoryUpdate(item.Item1,item.Item2,item.Item3);
            }
            
            
            //blockInventoryを開く
            blockInventory.OnOpenInventoryInvoke("",3,1);
            //BlockInventoryのアイテム設定
            blockInventory.OnInventoryUpdateInvoke(0,2,5);
            blockInventory.OnInventoryUpdateInvoke(3,1,5);
        }
    }
}