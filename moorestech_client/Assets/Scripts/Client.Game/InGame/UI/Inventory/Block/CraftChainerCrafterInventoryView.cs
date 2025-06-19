using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Block.ChainerCrafter;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Item.Interface;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.CraftChainer.BlockComponent.Crafter;
using Game.CraftChainer.CraftChain;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class CraftChainerCrafterInventoryView : CommonBlockInventoryViewBase
    {
        [SerializeField] private RectTransform chestSlotsParent;
        [SerializeField] private List<ItemSlotObject> recipeInputItemSlotObjects;
        [SerializeField] private List<ItemSlotObject> recipeOutputItemSlotObjects;
        
        [SerializeField] private CraftChainerCrafterItemSelectModal itemSelectModal;
        
        private BlockGameObject _blockGameObject;
        private CancellationToken _gameObjectCancellationToken;
        
        private CraftingSolverRecipe _currentRecipe;
        
        public override void Initialize(BlockGameObject blockGameObject)
        {
            base.Initialize(blockGameObject);
            _blockGameObject = blockGameObject;
            _gameObjectCancellationToken = this.GetCancellationTokenOnDestroy();
            
            // アイテムリストを初期化
            // Initialize item list
            InitializeItemList();
            
            // レシピの情報を取得
            // Get recipe information
            InitializeRecipeSlots().Forget();
            
            itemSelectModal.Initialize();
            
            #region Internal
            
            void InitializeItemList()
            {
                var itemList = new List<IItemStack>();
                var param = (CraftChainerCrafterBlockParam)blockGameObject.BlockMasterElement.BlockParam;
                for (var i = 0; i < param.ItemSlotCount; i++)
                {
                    var slotObject = Instantiate(ItemSlotObject.Prefab, chestSlotsParent);
                    SubInventorySlotObjectsInternal.Add(slotObject);
                    itemList.Add(ServerContext.ItemStackFactory.CreatEmpty());
                }
                UpdateItemList(itemList);
            }
            
  #endregion
        }
        
        private async UniTask InitializeRecipeSlots()
        {
            _currentRecipe = await GetRecipe();
            if (_currentRecipe == null) return;
            
            SetRecipeUI(_currentRecipe);
            
            SetupRecipeSlotEvent();
            
            #region Internal
            
            async UniTask<CraftingSolverRecipe> GetRecipe()
            {
                var pos = _blockGameObject.BlockPosInfo.OriginalPos;
                var blockStates = await ClientContext.VanillaApi.Response.GetBlockState(pos, _gameObjectCancellationToken);
                if (blockStates == null) return null;
                
                var chainerState = blockStates.GetStateDetail<ChainerCrafterComponentSerializeObject>(ChainerCrafterComponentSerializeObject.StateDetailKey);
                if (chainerState == null) return null;
                
                return chainerState.Recipe.ToCraftingSolverRecipe();
            }
            
            void SetupRecipeSlotEvent()
            {
                for (var i = 0; i < recipeInputItemSlotObjects.Count; i++)
                {
                    var input = recipeInputItemSlotObjects[i];
                    var index = i;
                    input.OnLeftClickUp.Subscribe(item => ClickRecipeInputItem(item, index, true).Forget());
                }
                for (var i = 0; i < recipeOutputItemSlotObjects.Count; i++)
                {
                    var output = recipeOutputItemSlotObjects[i];
                    var index = i;
                    output.OnLeftClickUp.Subscribe(item => ClickRecipeInputItem(item, index, false).Forget());
                }
            }
            
            #endregion
        }
        
        private async UniTask ClickRecipeInputItem(ItemSlotObject itemSlotObject, int index, bool isInput)
        {
            // アイテムを選択
            // Select item
            var (resultId, resultCount) = await SelectItem();
            
            // レシピ情報を更新
            // Update recipe information
            UpdateRecipe();
            
            // UIを更新
            // Update UI
            SetRecipeUI(_currentRecipe);
            
            // レシピ情報を送信
            // Send recipe information
            SendRecipeInfo();
            
            #region Internal
            
            async UniTask<(ItemId,int)> SelectItem()
            {
                // モーダルを開いてアイテムを選択
                var currentId = itemSlotObject.ItemViewData?.ItemId ?? ItemMaster.EmptyItemId;
                var currentCount = itemSlotObject.Count;
                
                var (id, count) = await itemSelectModal.GetSelectItem(currentId, currentCount);
                
                return (id, count);
            }
            
            void UpdateRecipe()
            {
                var recipeItems = isInput ? _currentRecipe.Inputs : _currentRecipe.Outputs;
                
                if (index < recipeItems.Count)
                {
                    recipeItems[index] = new CraftingSolverItem(resultId, resultCount);
                }
                else
                {
                    for (var i = recipeItems.Count; i < index; i++)
                    {
                        recipeItems.Add(new CraftingSolverItem(ItemMaster.EmptyItemId, 0));
                    }
                    
                    recipeItems.Add(new CraftingSolverItem(resultId, resultCount));
                }
            }
            
            void SendRecipeInfo()
            {
                // 送る用に適切な形に変換
                var input = _currentRecipe.Inputs.Where(i => i.ItemId != ItemMaster.EmptyItemId).ToList();
                var output = _currentRecipe.Outputs.Where(i => i.ItemId != ItemMaster.EmptyItemId).ToList();
                
                var pos = _blockGameObject.BlockPosInfo.OriginalPos;
                ClientContext.VanillaApi.SendOnly.SetCraftChainerCrafterRecipe(pos, input, output);
            }
            
  #endregion
            
        }
        
        
        private void SetRecipeUI(CraftingSolverRecipe recipe)
        {
            SetItemSlot(recipeInputItemSlotObjects, recipe.Inputs);
            SetItemSlot(recipeOutputItemSlotObjects, recipe.Outputs);
            
            #region Internal
            
            void SetItemSlot(List<ItemSlotObject> itemSlots, List<CraftingSolverItem> items)
            {
                for (var i = 0; i < itemSlots.Count; i++)
                {
                    if (i >= items.Count)
                    {
                        itemSlots[i].SetItem(null, 0);
                        continue;
                    }
                    
                    var item = items[i];
                    var slotObject = itemSlots[i];
                    var itemView = ClientContext.ItemImageContainer.GetItemView(item.ItemId);
                    slotObject.SetItem(itemView, item.Count);
                }
            }
            
  #endregion
        }
    }
}