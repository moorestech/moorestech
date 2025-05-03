using System.Collections.Generic;
using Game.CraftTree.Data;

namespace Game.CraftTree.Network
{
    /// <summary>
    /// サーバーからクライアントへのクラフトツリー更新情報を表すクラス
    /// </summary>
    public class CraftTreeUpdateData
    {
        /// <summary>
        /// 目標アイテムリスト（HUDに表示される項目）
        /// </summary>
        public List<GoalItem> goalItems { get; set; }
        
        /// <summary>
        /// ノード更新データリスト（差分更新用）
        /// </summary>
        public List<NodeUpdateData> updatedNodes { get; set; }
        
        /// <summary>
        /// 完全なツリーデータ（初回同期や大規模更新時に使用）
        /// </summary>
        public CraftTreeData fullTreeData { get; set; }
        
        /// <summary>
        /// デフォルトコンストラクタ
        /// </summary>
        public CraftTreeUpdateData()
        {
            goalItems = new List<GoalItem>();
            updatedNodes = new List<NodeUpdateData>();
        }
        
        /// <summary>
        /// ノード更新データを追加
        /// </summary>
        /// <param name="node">更新対象のノード</param>
        public void AddNodeUpdate(CraftTreeNode node)
        {
            if (node == null)
                return;
                
            updatedNodes.Add(new NodeUpdateData(
                node.itemId,
                node.state,
                node.currentCount
            ));
        }
        
        /// <summary>
        /// 更新データが空かどうか
        /// </summary>
        /// <returns>更新データが空の場合はtrue</returns>
        public bool IsEmpty()
        {
            return (goalItems == null || goalItems.Count == 0) &&
                   (updatedNodes == null || updatedNodes.Count == 0) &&
                   fullTreeData == null;
        }
    }
}