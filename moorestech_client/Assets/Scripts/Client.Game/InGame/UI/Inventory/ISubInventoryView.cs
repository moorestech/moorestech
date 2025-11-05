using System.Collections.Generic;
using Core.Item.Interface;

namespace Client.Game.InGame.UI.Inventory
{
    /// <summary>
    /// インベントリUIの表示と更新を統一的に管理するインターフェース
    /// Interface for unified management of inventory UI display and updates
    /// </summary>
    public interface ISubInventoryView : ISubInventory
    {
        /// <summary>
        /// ビューを初期化（ジェネリック版）
        /// Initialize view (generic version)
        /// </summary>
        void Initialize(object context);
        
        /// <summary>
        /// アイテムリストを一括更新
        /// Batch update item list
        /// </summary>
        void UpdateItemList(List<IItemStack> items);
        
        /// <summary>
        /// 特定スロットのアイテムを更新
        /// Update specific slot item
        /// </summary>
        void UpdateInventorySlot(int slot, IItemStack item);
        
        /// <summary>
        /// UIを破棄
        /// Destroy UI
        /// </summary>
        void DestroyUI();
    }
}

