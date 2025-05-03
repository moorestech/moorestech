using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Game.CraftTree.Data;
using Game.CraftTree.Network;

namespace Game.CraftTree.Utility
{
    /// <summary>
    /// CraftTreeオブジェクトのシリアライズ/デシリアライズを行うユーティリティクラス
    /// </summary>
    public static class CraftTreeSerializer
    {
        /// <summary>
        /// CraftTreeをシリアライズ可能なCraftTreeDataに変換
        /// </summary>
        /// <param name="tree">変換元のツリー</param>
        /// <returns>シリアライズ可能なデータオブジェクト</returns>
        public static CraftTreeData Serialize(Data.CraftTree tree)
        {
            return CraftTreeData.FromTree(tree);
        }
        
        /// <summary>
        /// CraftTreeDataからCraftTreeを復元
        /// </summary>
        /// <param name="treeData">シリアライズされたツリーデータ</param>
        /// <returns>復元されたCraftTree</returns>
        public static Data.CraftTree Deserialize(CraftTreeData treeData)
        {
            if (treeData == null || treeData.nodes == null || treeData.nodes.Count == 0)
                return null;
                
            // ノードデータを辞書形式に変換して検索を容易にする
            var nodeDataDict = treeData.nodes.ToDictionary(n => n.itemId);
            
            // ルートノードを見つける
            if (!nodeDataDict.TryGetValue(treeData.rootItemId, out var rootNodeData))
                return null;
                
            // ルートノードを生成
            var rootNode = new CraftTreeNode(rootNodeData.itemId, rootNodeData.requiredCount)
            {
                state = rootNodeData.state,
                selectedRecipe = rootNodeData.selectedRecipe
            };
            
            // ツリー全体を再構築
            var nodeMap = new Dictionary<ItemId, CraftTreeNode>();
            nodeMap[rootNode.itemId] = rootNode;
            
            // すべてのノードを生成
            foreach (var nodeData in treeData.nodes.Where(n => !n.itemId.Equals(rootNodeData.itemId)))
            {
                var node = new CraftTreeNode(nodeData.itemId, nodeData.requiredCount)
                {
                    state = nodeData.state,
                    selectedRecipe = nodeData.selectedRecipe
                };
                
                nodeMap[node.itemId] = node;
            }
            
            // 親子関係を構築
            foreach (var nodeData in treeData.nodes)
            {
                if (!nodeMap.TryGetValue(nodeData.itemId, out var parentNode))
                    continue;
                    
                foreach (var childId in nodeData.childrenIds)
                {
                    if (nodeMap.TryGetValue(childId, out var childNode))
                    {
                        childNode.parent = parentNode;
                        parentNode.children.Add(childNode);
                    }
                }
            }
            
            // 完全なツリーを作成して返す
            return new Data.CraftTree(rootNode)
            {
                treeId = treeData.treeId
            };
        }
    }
}