using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.CraftTree.Data.Transfer
{
    /// <summary>
    /// クラフトツリー全体のシリアライズ用データクラス
    /// </summary>
    public class CraftTreeData
    {
        /// <summary>
        /// ツリーの一意識別子
        /// </summary>
        public Guid treeId { get; set; }
        
        /// <summary>
        /// ルートノードのアイテムID
        /// </summary>
        public ItemId rootItemId { get; set; }
        
        /// <summary>
        /// ツリー内のすべてのノードデータのリスト
        /// </summary>
        public List<CraftTreeNodeData> nodes { get; set; }
        
        /// <summary>
        /// デフォルトコンストラクタ（シリアライズ用）
        /// </summary>
        public CraftTreeData()
        {
            nodes = new List<CraftTreeNodeData>();
        }
        
        /// <summary>
        /// CraftTreeからCraftTreeDataを作成
        /// </summary>
        /// <param name="craftTree">変換元のクラフトツリー</param>
        /// <returns>シリアライズ用データ</returns>
        public static CraftTreeData FromTree(Data.CraftTree tree)
        {
            if (tree == null || tree.rootNode == null)
                return null;
                
            var data = new CraftTreeData
            {
                treeId = tree.treeId,
                rootItemId = tree.rootNode.itemId,
                nodes = new List<CraftTreeNodeData>()
            };
            
            // ツリー内のすべてのノードを探索して追加
            CollectNodes(tree.rootNode, data.nodes);
            
            return data;
        }
        
        /// <summary>
        /// ノードとその子孫を再帰的に収集
        /// </summary>
        /// <param name="node">収集開始ノード</param>
        /// <param name="result">結果を格納するリスト</param>
        private static void CollectNodes(CraftTreeNode node, List<CraftTreeNodeData> result)
        {
            // ノードをデータに変換して追加
            result.Add(CraftTreeNodeData.FromNode(node));
            
            // 子ノードも再帰的に処理
            foreach (var child in node.children)
            {
                CollectNodes(child, result);
            }
        }
    }
}