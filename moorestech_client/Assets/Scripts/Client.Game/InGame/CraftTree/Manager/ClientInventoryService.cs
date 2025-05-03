using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Core.Item;
using UnityEngine;

namespace Client.Game.InGame.CraftTree.Manager
{
    /// <summary>
    /// クライアント側のインベントリ情報を提供するサービス
    /// </summary>
    public class ClientInventoryService
    {
        private readonly PlayerInventoryModel _inventoryModel;
        
        /// <summary>
        /// インベントリ変更時のイベント
        /// </summary>
        public event Action OnInventoryChanged;
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="inventoryModel">インベントリモデル</param>
        public ClientInventoryService(PlayerInventoryModel inventoryModel)
        {
            _inventoryModel = inventoryModel ?? throw new ArgumentNullException(nameof(inventoryModel));
            
            // インベントリ更新イベントを購読
            _inventoryModel.OnInventoryItemsChanged += HandleInventoryChanged;
        }
        
        /// <summary>
        /// インベントリ変更イベントハンドラ
        /// </summary>
        private void HandleInventoryChanged()
        {
            // 登録済みリスナーに通知
            OnInventoryChanged?.Invoke();
        }
        
        /// <summary>
        /// すべてのインベントリアイテムを取得
        /// </summary>
        /// <returns>アイテムIDと所持数のディクショナリ</returns>
        public Dictionary<ItemId, int> GetInventoryItems()
        {
            var result = new Dictionary<ItemId, int>();
            
            try
            {
                // メインインベントリのアイテムを集計
                foreach (var slot in _inventoryModel.MainItems)
                {
                    if (slot != null && !slot.IsEmpty())
                    {
                        var itemId = slot.ItemId;
                        if (result.TryGetValue(itemId, out int count))
                        {
                            result[itemId] = count + slot.Count;
                        }
                        else
                        {
                            result[itemId] = slot.Count;
                        }
                    }
                }
                
                // グラブインベントリのアイテムも含める
                var grabItem = _inventoryModel.GrabItem;
                if (grabItem != null && !grabItem.IsEmpty())
                {
                    var itemId = grabItem.ItemId;
                    if (result.TryGetValue(itemId, out int count))
                    {
                        result[itemId] = count + grabItem.Count;
                    }
                    else
                    {
                        result[itemId] = grabItem.Count;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error getting inventory items: {ex.Message}");
            }
            
            return result;
        }
        
        /// <summary>
        /// 特定のアイテムの所持数を取得
        /// </summary>
        /// <param name="itemId">アイテムID</param>
        /// <returns>所持数</returns>
        public int GetItemCount(ItemId itemId)
        {
            var inventory = GetInventoryItems();
            return inventory.TryGetValue(itemId, out int count) ? count : 0;
        }
        
        /// <summary>
        /// レシピの材料が十分にあるか確認
        /// </summary>
        /// <param name="ingredients">材料リスト</param>
        /// <returns>十分な材料がある場合はtrue</returns>
        public bool HasEnoughItems(List<Game.CraftTree.Data.RecipeIngredient> ingredients)
        {
            if (ingredients == null || ingredients.Count == 0)
                return true;
                
            var inventory = GetInventoryItems();
            
            foreach (var ingredient in ingredients)
            {
                if (!inventory.TryGetValue(ingredient.itemId, out int count) || count < ingredient.count)
                {
                    return false;
                }
            }
            
            return true;
        }
    }
}