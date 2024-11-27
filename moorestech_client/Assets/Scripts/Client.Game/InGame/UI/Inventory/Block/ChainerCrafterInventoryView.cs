using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Block.ChainerCrafter;
using Client.Game.InGame.UI.Inventory.Element;
using Client.Network.API;
using Core.Item.Interface;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.CraftChainer.BlockComponent.Crafter;
using Game.CraftChainer.CraftChain;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Block
{
    public class ChainerCrafterInventoryView : CommonBlockInventoryViewBase
    {
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        
        [SerializeField] private RectTransform chestSlotsParent;
        [SerializeField] private List<ItemSlotObject> recipeInputItemSlotObjects;
        [SerializeField] private List<ItemSlotObject> recipeOutputItemSlotObjects;
        
        [SerializeField] private ChainerCrafterItemSelectModal itemSelectModal;
        
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
                    var slotObject = Instantiate(itemSlotObjectPrefab, chestSlotsParent);
                    _blockItemSlotObjects.Add(slotObject);
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
            
            SetItemSlot(recipeInputItemSlotObjects, _currentRecipe.Inputs);
            SetItemSlot(recipeOutputItemSlotObjects, _currentRecipe.Outputs);
            
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
            
            void SetItemSlot(List<ItemSlotObject> itemSlots, List<CraftingSolverItem> items)
            {
                for (var i = 0; i < itemSlots.Count; i++)
                {
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