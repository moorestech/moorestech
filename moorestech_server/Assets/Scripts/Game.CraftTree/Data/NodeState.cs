using System;

namespace Game.CraftTree.Data
{
    /// <summary>
    /// クラフトツリーノードの状態を表す列挙型
    /// </summary>
    public enum NodeState
    {
        /// <summary>
        /// 未完了状態（必要なアイテム数に達していない）
        /// </summary>
        Incomplete,
        
        /// <summary>
        /// 完了状態（必要なアイテム数を達成した）
        /// </summary>
        Completed
    }
}