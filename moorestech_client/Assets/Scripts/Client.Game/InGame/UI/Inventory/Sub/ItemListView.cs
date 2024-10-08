using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Tutorial.UIHighlight;
using Client.Game.InGame.UI.Inventory.Element;
using Client.Game.InGame.UI.Inventory.Main;
using Core.Master;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory.Sub
{
    public class ItemListView : MonoBehaviour
    {
        public const string ItemRecipeListHighlightKey = "itemRecipeList:{0}";
        
        [SerializeField] private ItemSlotObject itemSlotObjectPrefab;
        [SerializeField] private RectTransform itemListParent;
        
        [SerializeField] private CraftInventoryView craftInventoryView;
        
        private readonly List<ItemSlotObject> _itemListObjects = new();
        private ILocalPlayerInventory _localPlayerInventory;
        
        private void Awake()
        {
            _localPlayerInventory.OnItemChange.Subscribe(OnInventoryItemChange);

            
            foreach (var itemId in MasterHolder.ItemMaster.GetItemAllIds())
            {
                var itemViewData = ClientContext.ItemImageContainer.GetItemView(itemId);
                
                // アイテムリストを設定
                // Set the item list
                var itemSlotObject = Instantiate(itemSlotObjectPrefab, itemListParent);
                itemSlotObject.SetItem(itemViewData, 0);
                itemSlotObject.OnLeftClickUp.Subscribe(craftInventoryView.OnClickItemList);
                _itemListObjects.Add(itemSlotObject);
                
                // ハイライトオブジェクトを設定
                // Set the highlight object
                var target = itemSlotObject.gameObject.AddComponent<UIHighlightTutorialTargetObject>();
                var itemMaster = MasterHolder.ItemMaster.GetItemMaster(itemId);
                target.Initialize(string.Format(ItemRecipeListHighlightKey, itemMaster.Name));
            }
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