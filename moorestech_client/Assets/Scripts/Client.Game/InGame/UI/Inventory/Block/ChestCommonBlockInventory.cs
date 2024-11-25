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
    public class ChestCommonBlockInventory : CommonBlockInventoryBase
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform chestSlotsParent;
        
        public override void Initialize(BlockGameObject blockGameObject)
        {
            // Set ItemMoveInventoryInfo
            var pos = blockGameObject.BlockPosInfo.OriginalPos;
            ItemMoveInventoryInfo = new ItemMoveInventoryInfo(ItemMoveInventoryType.BlockInventory, pos);
            
            // アイテムリストを初期化
            // Initialize item list
            var itemList = new List<IItemStack>();
            var chestParam = (ChestBlockParam)blockGameObject.BlockMasterElement.BlockParam;
            for (var i = 0; i < chestParam.ChestItemSlotCount; i++)
            {
                var slotObject = Instantiate(itemSlotObjectPrefab, chestSlotsParent);
                _blockItemSlotObjects.Add(slotObject);
                itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
            }
            
            UpdateItemList(itemList);
        }
    }
}