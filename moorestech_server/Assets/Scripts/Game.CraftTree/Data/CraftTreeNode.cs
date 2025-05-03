using System;
using System.Collections.Generic;
using Core.Item;
using Core.Master;

namespace Game.CraftTree.Data
{
    /// <summary>
    /// クラフトツリーの単一ノードを表すクラス
    /// </summary>
    public class CraftTreeNode
    {
        /// <summary>
        /// アイテムID
        /// </summary>
        public ItemId itemId { get; private set; }
        
        /// <summary>
        /// 必要数量
        /// </summary>
        public int requiredCount { get; private set; }
        
        /// <summary>
        /// 子ノードのリスト（材料アイテム）
        /// </summary>
        public List<CraftTreeNode> children { get; private set; }
        
        /// <summary>
        /// ノードの状態（完了/未完了）
        /// </summary>
        public NodeState state { get; set; }
        
        /// <summary>
        /// 選択されたレシピ
        /// </summary>
        public RecipeData selectedRecipe { get; set; }
        
        /// <summary>
        /// 現在の所持数量
        /// </summary>
        public int currentCount { get; private set; }
        
        /// <summary>
        /// 親ノード
        /// </summary>
        public CraftTreeNode parent { get; set; }
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="itemId">アイテムID</param>
        /// <param name="requiredCount">必要数量</param>
        public CraftTreeNode(ItemId itemId, int requiredCount)
        {
            if (requiredCount <= 0)
                throw new ArgumentException("Required count must be positive", nameof(requiredCount));
                
            this.itemId = itemId;
            this.requiredCount = requiredCount;
            this.children = new List<CraftTreeNode>();
            this.state = NodeState.Incomplete;
            this.currentCount = 0;
        }
        
        /// <summary>
        /// 状態を更新する
        /// </summary>
        /// <param name="currentItemCount">現在のアイテム数</param>
        public void UpdateState(int currentItemCount)
        {
            this.currentCount = currentItemCount;
            
            // 必要数に達していれば完了、そうでなければ未完了
            if (currentItemCount >= requiredCount)
            {
                state = NodeState.Completed;
            }
            else
            {
                state = NodeState.Incomplete;
            }
        }
        
        /// <summary>
        /// ノードの状態をリセットする
        /// </summary>
        public void Reset()
        {
            state = NodeState.Incomplete;
            currentCount = 0;
            
            // 子ノードも再帰的にリセット
            foreach (var child in children)
            {
                child.Reset();
            }
        }
        
        /// <summary>
        /// ノードが完了しているかどうか
        /// </summary>
        /// <returns>完了状態の場合はtrue</returns>
        public bool IsCompleted()
        {
            return state == NodeState.Completed;
        }
        
        /// <summary>
        /// 子ノードを追加
        /// </summary>
        /// <param name="node">追加する子ノード</param>
        public void AddChild(CraftTreeNode node)
        {
            if (node == null)
                throw new ArgumentNullException(nameof(node), "Child node cannot be null");
                
            // 親子関係を設定
            node.parent = this;
            children.Add(node);
        }
    }
}