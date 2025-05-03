using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Master;
using Game.CraftTree.Network;

namespace Game.CraftTree.Data
{
    /// <summary>
    /// クラフトツリー全体を管理するクラス
    /// </summary>
    public class CraftTree
    {
        /// <summary>
        /// ルートノード
        /// </summary>
        public CraftTreeNode rootNode { get; private set; }
        
        /// <summary>
        /// ItemIdをキーにしたノードの辞書（同じアイテムIDのノードが複数ある可能性あり）
        /// </summary>
        public Dictionary<ItemId, List<CraftTreeNode>> nodesByItemId { get; private set; }
        
        /// <summary>
        /// ツリーの一意識別子
        /// </summary>
        public Guid treeId { get; private set; }
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="rootNode">ルートノード</param>
        public CraftTree(CraftTreeNode rootNode)
        {
            if (rootNode == null)
                throw new ArgumentNullException(nameof(rootNode), "Root node cannot be null");
                
            this.rootNode = rootNode;
            this.treeId = Guid.NewGuid();
            this.nodesByItemId = new Dictionary<ItemId, List<CraftTreeNode>>();
            
            // ルートノードを登録
            RegisterNode(rootNode);
        }
        
        /// <summary>
        /// ノードをItemIdベースの辞書に登録
        /// </summary>
        /// <param name="node">登録するノード</param>
        private void RegisterNode(CraftTreeNode node)
        {
            // ItemIdをキーにノードを登録
            if (!nodesByItemId.TryGetValue(node.itemId, out var nodes))
            {
                nodes = new List<CraftTreeNode>();
                nodesByItemId[node.itemId] = nodes;
            }
            
            nodes.Add(node);
            
            // 子ノードも再帰的に登録
            foreach (var child in node.children)
            {
                RegisterNode(child);
            }
        }
        
        /// <summary>
        /// インベントリ情報に基づいてツリー全体の状態を更新
        /// </summary>
        /// <param name="inventory">インベントリ（アイテムIDと所持数のディクショナリ）</param>
        public void UpdateTreeState(Dictionary<ItemId, int> inventory)
        {
            if (inventory == null)
                throw new ArgumentNullException(nameof(inventory), "Inventory cannot be null");
                
            // ルートから順に更新
            UpdateNodeState(rootNode, inventory);
        }
        
        /// <summary>
        /// ノード状態を再帰的に更新
        /// </summary>
        /// <param name="node">更新対象ノード</param>
        /// <param name="inventory">インベントリ</param>
        private void UpdateNodeState(CraftTreeNode node, Dictionary<ItemId, int> inventory)
        {
            // 現在のアイテム数を取得
            int currentCount = inventory.GetValueOrDefault(node.itemId, 0);
            node.UpdateState(currentCount);
            
            // 子ノードを更新
            foreach (var child in node.children)
            {
                UpdateNodeState(child, inventory);
            }
        }
        
        /// <summary>
        /// 深さ優先探索で「直近取得すべき」ノードを抽出
        /// </summary>
        /// <returns>目標アイテムのリスト</returns>
        public List<GoalItem> ExtractGoalItems()
        {
            var result = new List<GoalItem>();
            var visited = new HashSet<CraftTreeNode>();
            
            void DfsVisit(CraftTreeNode node)
            {
                if (visited.Contains(node)) return;
                visited.Add(node);
                
                // 親が完了で自身が未完了のノードを「直近取得すべき」と定義
                bool isParentCompleted = node.parent == null || node.parent.state == NodeState.Completed;
                
                if (isParentCompleted && node.state == NodeState.Incomplete)
                {
                    result.Add(new GoalItem(node.itemId, node.requiredCount, node.currentCount));
                }
                
                // 子ノードを探索
                foreach (var child in node.children)
                {
                    DfsVisit(child);
                }
            }
            
            DfsVisit(rootNode);
            return result;
        }
        
        /// <summary>
        /// 現在インベントリでクラフト可能なアイテム抽出
        /// </summary>
        /// <param name="inventory">インベントリ</param>
        /// <returns>クラフト可能アイテムのリスト</returns>
        public List<GoalItem> ExtractCraftableItems(Dictionary<ItemId, int> inventory)
        {
            var result = new List<GoalItem>();
            
            // 各ノードについて、全ての子ノードが揃っていればクラフト可能
            foreach (var nodeList in nodesByItemId.Values)
            {
                foreach (var node in nodeList)
                {
                    if (node.state == NodeState.Incomplete && node.selectedRecipe != null)
                    {
                        bool canCraft = true;
                        foreach (var child in node.children)
                        {
                            int availableCount = inventory.GetValueOrDefault(child.itemId, 0);
                            if (availableCount < child.requiredCount)
                            {
                                canCraft = false;
                                break;
                            }
                        }
                        
                        if (canCraft)
                        {
                            result.Add(new GoalItem(node.itemId, node.requiredCount, node.currentCount));
                        }
                    }
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// サーバーからの更新データを適用
        /// </summary>
        /// <param name="updateData">更新データ</param>
        public void ApplyServerUpdate(CraftTreeUpdateData updateData)
        {
            if (updateData == null)
                throw new ArgumentNullException(nameof(updateData), "Update data cannot be null");
                
            // 実装は省略（CraftTreeUpdateDataクラスは後ほど定義）
        }
    }
}