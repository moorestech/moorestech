using System.Collections.Generic;
using Client.Game.InGame.Block;
using Core.Item.Interface;

namespace Client.Game.InGame.UI.Inventory.Block
{
    /// <summary>
    /// ブロックインベントリビューのインターフェース
    /// Block inventory view interface
    /// </summary>
    public interface IBlockInventoryView : ISubInventoryView
    {
        /// <summary>
        /// ブロック固有の初期化（型安全版）
        /// Block-specific initialization (type-safe version)
        /// </summary>
        public void Initialize(BlockGameObject blockGameObject);
    }
}