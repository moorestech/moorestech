using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.BaseCamp;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;
using UnityEngine.UI;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class BaseCampBlockInventoryView : CommonBlockInventoryViewBase
    {
        [SerializeField] private RectTransform slotsParent;
        [SerializeField] private RectTransform requiredItemsParent;
        
        [SerializeField] private Slider progressSlider;
        
        [SerializeField] private Button completeButton;
        
        private BlockGameObject _blockGameObject;
        private Dictionary<ItemId, int> _requiredItems;
        
        private void Awake()
        {
            completeButton.onClick.AddListener(() =>
            {
                ClientContext.VanillaApi.SendOnly.CompleteBaseCamp(_blockGameObject.BlockPosInfo.OriginalPos);
            });
        }
        
        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            _blockGameObject = blockGameObject;
            
            // アイテムリストを初期化
            // Initialize item list
            var itemList = new List<IItemStack>();
            
            if (blockGameObject.BlockMasterElement.BlockParam is not BaseCampBlockParam)
            {
                var blockName = blockGameObject.BlockMasterElement.Name;
                var guid = blockGameObject.BlockMasterElement.BlockGuid;
                // TODO ログ基盤にいれる
                Debug.LogError($"ブロック名:{blockName} guid:{guid} はBaseCampBlockParamを持っていません。指定しているUIを見直すか、スキーマを見直してください。");
                return;
            }
            
            var param = (BaseCampBlockParam)blockGameObject.BlockMasterElement.BlockParam;
            
            // 必要アイテムをDictionaryに変換
            // Convert required items to Dictionary
            _requiredItems = new Dictionary<ItemId, int>();
            foreach (var item in param.RequiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(item.ItemGuid);
                var itemView = ClientContext.ItemImageContainer.GetItemView(itemId);
                
                var slotObject = Instantiate(ItemSlotView.Prefab, requiredItemsParent);
                slotObject.SetItem(itemView, item.Amount);
                _requiredItems[itemId] = item.Amount;
            }
            
            for (var i = 0; i < param.InventorySlot; i++)
            {
                var slotObject = Instantiate(ItemSlotView.Prefab, slotsParent);
                SubInventorySlotObjectsInternal.Add(slotObject);
                itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
            }
            
            UpdateItemList(itemList);
        }
        
        private void Update()
        {
            if (_requiredItems == null || SubInventory == null) return;
            
            // SubInventoryのアイテムリストを使って進捗を計算
            // Calculate progress using SubInventory item list
            var isComplete = BaseCampCalculator.CalculateIsCompleted(SubInventory, _requiredItems);
            completeButton.interactable = isComplete;
            
            var progress = BaseCampCalculator.CalculateProgress(SubInventory, _requiredItems);
            progressSlider.value = progress;
        }
    }
}