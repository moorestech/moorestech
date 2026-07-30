// [uGUI廃止Phase1] uGUI描画は恒久停止・ビューは未メンテ。ただし本クラスは外部（Web UIブリッジ等）から参照中のため削除前に整理が必要（docs/webui/ugui-retirement-plan.md）
// [uGUI retirement Phase1] uGUI rendering is permanently disabled and the view is unmaintained, but this class is still referenced externally (e.g. Web UI bridge); untangle before deletion (docs/webui/ugui-retirement-plan.md)
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