// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。ただし本クラスは外部（Web UIブリッジ等）から参照中のため削除前に整理が必要（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained, but this class is still referenced externally (e.g. Web UI bridge); untangle before deletion (docs/webui/ugui-retirement-plan.md)
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

