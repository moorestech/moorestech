using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.Inventory.Main;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
using Client.Game.InGame.UnlockState;
using Common.Debug;
using Core.Master;
using Game.UnlockState;
using Mooresmaster.Model.ItemsModule;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.Inventory.Sub
{
    public class ItemListView : MonoBehaviour
    {
        public const string ItemRecipeListHighlightKey = "itemRecipeList:{0}";
        
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        [SerializeField] private RectTransform itemListParent;
        
        [Inject] private ILocalPlayerInventory _localPlayerInventory;
        [Inject] private ItemRecipeViewerDataContainer _itemRecipeViewerDataContainer;
        [Inject] private IGameUnlockStateData _gameUnlockStateData;
        private readonly List<ItemSlotObject> _itemListObjects = new();
        
        public IObservable<RecipeViewerItemRecipes> OnClickItem => _onClickItem;
        private readonly Subject<RecipeViewerItemRecipes> _onClickItem = new();
        
        
        [Inject]
        public void Construct()
        {
            _localPlayerInventory.OnItemChange.Subscribe(OnInventoryItemChange);
        }
        
        /// <summary>
        /// OnEnableじゃなくて、ちゃんと制御されたところから呼び出したいとは思っている
        /// </summary>
        private void OnEnable()
        {
            // アイテムリストの削除
            // Delete the item list
            foreach (var item in _itemListObjects)
            {
                Destroy(item.gameObject);
            }
            _itemListObjects.Clear();
            
            // アイテムリストの設定
            // Set the item list
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
                if (!IsShow(itemMaster)) continue;
                
                
                var itemViewData = ClientContext.ItemImageContainer.GetItemView(itemId);
                
                // アイテムリストを設定
                // Set the item list
                var itemSlotObject = Instantiate(itemSlotObjectPrefab, itemListParent);
                var toolTipText = CraftInventoryView.GetMaterialTolTip(itemViewData);
                itemSlotObject.SetItem(itemViewData, 0, toolTipText);
                itemSlotObject.OnLeftClickUp.Subscribe(OnClickItemList);
                
                // ハイライトオブジェクトを設定
                // Set the highlight object
                var target = itemSlotObject.gameObject.AddComponent<UIHighlightTutorialTargetObject>();
                target.Initialize(string.Format(ItemRecipeListHighlightKey, itemMaster.ItemGuid));
                
                // リストに追加（1回だけ）
                _itemListObjects.Add(itemSlotObject);
            }
            
            // 最初のインベントリ状態に基づいてグレーアウト処理を実行
            OnInventoryItemChange(0);
            
            #region Internal
            
            bool IsShow(ItemMasterElement itemMaster)
            {
                if (DebugParameters.GetValueOrDefaultBool(DebugConst.IsItemListViewForceShowKey))
                {
                    return true;
                }
                
                var itemId = MasterHolder.ItemMaster.GetItemId(itemMaster.ItemGuid);
                
                if (itemMaster.RecipeViewType is ItemMasterElement.RecipeViewTypeConst.Default)
                {
                    // デフォルトはアンロックされていてレシピがあれば表示する
                    // Default is to display if unlocked and has a recipe
                    var state = _gameUnlockStateData.ItemUnlockStateInfos[itemId];
                    var isItemUnlocked = state.IsUnlocked;
                    
                    var itemRecipes = _itemRecipeViewerDataContainer.GetItem(itemId);
                    var unlockRecipes = itemRecipes.UnlockedCraftRecipes();
                    var isRecipeItemUnlocked = unlockRecipes.Count != 0;
                    
                    return isItemUnlocked && isRecipeItemUnlocked;
                }

                if (itemMaster.RecipeViewType is ItemMasterElement.RecipeViewTypeConst.IsUnlocked)
                {
                    // アンロックされていれば表示する
                    // Display if unlocked
                    var state = _gameUnlockStateData.ItemUnlockStateInfos[itemId];
                    return state.IsUnlocked;
                }
                if (itemMaster.RecipeViewType is ItemMasterElement.RecipeViewTypeConst.IsCraftRecipeExist)
                {
                    var itemRecipes = _itemRecipeViewerDataContainer.GetItem(itemId);
                    var unlockRecipes = itemRecipes.UnlockedCraftRecipes();
                    if (unlockRecipes.Count == 0)
                    {
                        // クラフトレシピがない場合は表示しない
                        // Do not display if there is no craft recipe
                        return false;
                    }
                    return true;
                }
                if (itemMaster.RecipeViewType is ItemMasterElement.RecipeViewTypeConst.ForceHide)
                {
                    return false;
                }
                if (itemMaster.RecipeViewType is ItemMasterElement.RecipeViewTypeConst.ForceShow)
                {
                    // 強制表示の場合は表示する
                    // If it is a forced display, display it
                    return true;
                }
                
                throw new Exception($"{itemMaster.RecipeViewType}タイプの判定の実装が足りません");
            }
            
            #endregion
        }
        
        private void OnClickItemList(ItemSlotObject slot)
        {
            var itemId = slot.ItemViewData.ItemId;
            var itemRecipes = _itemRecipeViewerDataContainer.GetItem(itemId);
            _onClickItem.OnNext(itemRecipes);
        }
        
        private void OnInventoryItemChange(int slot)
        {
            var enableItem = CheckAllItemCraftable();
            foreach (var itemUI in _itemListObjects)
            {
                var isGrayOut = !enableItem.Contains(itemUI.ItemViewData.ItemId);
                itemUI.SetGrayOut(isGrayOut);
            }
            
            #region Internal
            
            HashSet<ItemId> CheckAllItemCraftable()
            {
                var itemPerCount = new Dictionary<ItemId, int>();
                foreach (var item in _localPlayerInventory)
                {
                    if (item.Id == ItemMaster.EmptyItemId) continue;
                    if (itemPerCount.ContainsKey(item.Id))
                        itemPerCount[item.Id] += item.Count;
                    else
                        itemPerCount.Add(item.Id, item.Count);
                }
                
                var result = new HashSet<ItemId>();
                
                foreach (var craftMaster in MasterHolder.CraftRecipeMaster.GetAllCraftRecipes())
                {
                    var resultItemId = MasterHolder.ItemMaster.GetItemId(craftMaster.CraftResultItemGuid);
                    if (result.Contains(resultItemId)) continue; //すでにクラフト可能なアイテムならスキップ
                    var isCraftable = true;
                    foreach (var requiredItem in craftMaster.RequiredItems)
                    {
                        var requiredItemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                        if (!itemPerCount.ContainsKey(requiredItemId) || itemPerCount[requiredItemId] < requiredItem.Count)
                        {
                            isCraftable = false;
                            break;
                        }
                    }
                    
                    if (isCraftable) result.Add(resultItemId);
                }
                
                return result;
            }
            
            #endregion
        }
    }
}