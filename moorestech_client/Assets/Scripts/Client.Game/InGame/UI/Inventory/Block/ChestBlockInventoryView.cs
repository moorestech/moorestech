using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Element;
using Core.Item.Interface;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class ChestBlockInventoryView : CommonBlockInventoryViewBase
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform chestSlotsParent;
        
        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            
            // アイテムリストを初期化
            // Initialize item list
            var itemList = new List<IItemStack>();
            
            var param = (IChestParam)blockGameObject.BlockMasterElement.BlockParam;
            for (var i = 0; i < param.ItemSlotCount; i++)
            {
                var slotObject = Instantiate(itemSlotObjectPrefab, chestSlotsParent);
                _blockItemSlotObjects.Add(slotObject);
                itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
            }
            
            UpdateItemList(itemList);
        }
    }
}