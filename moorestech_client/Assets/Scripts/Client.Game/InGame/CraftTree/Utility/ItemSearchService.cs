using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Master;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.Utility
{
    /// <summary>
    /// アイテム検索機能を提供するサービスクラス
    /// </summary>
    public class ItemSearchService
    {
        private readonly ItemMaster _itemMaster;
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="itemMaster">アイテムマスター</param>
        public ItemSearchService(ItemMaster itemMaster)
        {
            _itemMaster = itemMaster ?? throw new ArgumentNullException(nameof(itemMaster));
        }
        
        /// <summary>
        /// キーワードによるアイテム検索
        /// </summary>
        /// <param name="query">検索クエリ</param>
        /// <returns>検索結果のアイテムIDリスト</returns>
        public List<ItemId> SearchItems(string query)
        {
            if (string.IsNullOrEmpty(query))
                return new List<ItemId>();
                
            var result = new List<ItemId>();
            var searchTerms = query.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            try
            {
                // すべてのアイテムから検索
                foreach (var itemData in _itemMaster.GetAllItems())
                {
                    // アイテム名で検索
                    var itemName = itemData.Value.DisplayName.ToLowerInvariant();
                    
                    // すべての検索ワードに一致するかどうか
                    bool allTermsMatch = true;
                    foreach (var term in searchTerms)
                    {
                        if (!itemName.Contains(term))
                        {
                            allTermsMatch = false;
                            break;
                        }
                    }
                    
                    if (allTermsMatch)
                    {
                        result.Add(itemData.Key);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error searching items: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// カテゴリ別にアイテムを取得
        /// </summary>
        /// <returns>カテゴリ別アイテムリスト</returns>
        public Dictionary<ItemCategory, List<ItemId>> GetCategorizedItems()
        {
            var result = new Dictionary<ItemCategory, List<ItemId>>();
            
            try
            {
                foreach (var itemData in _itemMaster.GetAllItems())
                {
                    var category = GetItemCategory(itemData.Value);
                    
                    if (!result.TryGetValue(category, out var itemList))
                    {
                        itemList = new List<ItemId>();
                        result[category] = itemList;
                    }
                    
                    itemList.Add(itemData.Key);
                }
                
                // 各カテゴリ内でのソート
                foreach (var categoryList in result.Values)
                {
                    categoryList.Sort((a, b) => 
                    {
                        try
                        {
                            var nameA = _itemMaster.LookupItemInfo(a).DisplayName;
                            var nameB = _itemMaster.LookupItemInfo(b).DisplayName;
                            return string.Compare(nameA, nameB, StringComparison.OrdinalIgnoreCase);
                        }
                        catch
                        {
                            return 0;
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error getting categorized items: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// アイテムのカテゴリを判定
        /// </summary>
        /// <param name="itemInfo">アイテム情報</param>
        /// <returns>アイテムカテゴリ</returns>
        private ItemCategory GetItemCategory(ItemInfo itemInfo)
        {
            // アイテムの種類に応じてカテゴリを判定
            // これは実際のゲームロジックに合わせて調整が必要
            
            if (itemInfo.DisplayName.Contains("原材料") || 
                itemInfo.DisplayName.Contains("鉱石") || 
                itemInfo.DisplayName.Contains("素材"))
            {
                return ItemCategory.Materials;
            }
            else if (itemInfo.DisplayName.Contains("ツール") || 
                     itemInfo.DisplayName.Contains("道具"))
            {
                return ItemCategory.Tools;
            }
            else if (itemInfo.DisplayName.Contains("マシン") || 
                     itemInfo.DisplayName.Contains("機械"))
            {
                return ItemCategory.Machines;
            }
            else
            {
                return ItemCategory.Misc;
            }
        }
    }
    
    /// <summary>
    /// アイテムカテゴリ
    /// </summary>
    public enum ItemCategory
    {
        /// <summary>
        /// 素材・原材料
        /// </summary>
        Materials,
        
        /// <summary>
        /// ツール・道具
        /// </summary>
        Tools,
        
        /// <summary>
        /// 機械・装置
        /// </summary>
        Machines,
        
        /// <summary>
        /// その他
        /// </summary>
        Misc
    }
}