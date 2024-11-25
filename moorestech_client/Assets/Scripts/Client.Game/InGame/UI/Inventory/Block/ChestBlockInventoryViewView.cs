using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Element;
using Core.Item.Interface;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class ChestBlockInventoryViewView : CommonBlockInventoryViewBase
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform chestSlotsParent;
        
        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            
            // Set ItemMoveInventoryInfo
            var pos = blockGameObject.BlockPosInfo.OriginalPos;
            ItemMoveInventoryInfo = new ItemMoveInventoryInfo(ItemMoveInventoryType.BlockInventory, pos);
            
            // アイテムリストを初期化
            // Initialize item list
            var itemList = new List<IItemStack>();
            
            var param = blockGameObject.BlockMasterElement.BlockParam;
            var slotSize =  param switch
            {
                ChestBlockParam blockParam => blockParam.ChestItemSlotCount, // TODO master interfaceブロックインベントリの整理箇所
                CraftChainerProviderChestBlockParam blockParam => blockParam.ItemSlotCount,
                CraftChainerMainComputerBlockParam blockParam => blockParam.ItemSlotCount,
                _ => 0
            };
            for (var i = 0; i < slotSize; i++)
            {
                var slotObject = Instantiate(itemSlotObjectPrefab, chestSlotsParent);
                _blockItemSlotObjects.Add(slotObject);
                itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
            }
            
            UpdateItemList(itemList);
        }
    }
}