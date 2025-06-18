using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class ChestBlockInventoryView : CommonBlockInventoryViewBase
    {
        [SerializeField] private RectTransform chestSlotsParent;
        
        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            
            // アイテムリストを初期化
            // Initialize item list
            var itemList = new List<IItemStack>();
            
            if (blockGameObject.BlockMasterElement.BlockParam is not IChestParam)
            {
                var blockName = blockGameObject.BlockMasterElement.Name;
                var guid = blockGameObject.BlockMasterElement.BlockGuid;
                // TODO ログ基盤にいれる
                Debug.LogError($"ブロック名:{blockName} guid:{guid} はIChestParamを持っていません。指定しているUIを見直すか、スキーマを見直してください。");
                return;
            }
            
            var param = (IChestParam)blockGameObject.BlockMasterElement.BlockParam;
            for (var i = 0; i < param.ItemSlotCount; i++)
            {
                var slotObject = Instantiate(itemSlotObjectPrefab, chestSlotsParent);
                SubInventorySlotObjectsInternal.Add(slotObject);
                itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
            }
            
            UpdateItemList(itemList);
        }
    }
}