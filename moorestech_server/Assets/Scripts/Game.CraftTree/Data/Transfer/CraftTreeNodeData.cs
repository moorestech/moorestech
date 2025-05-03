using System.Collections.Generic;
using Core.Master;

namespace Game.CraftTree.Data.Transfer
{
    /// <summary>
    /// クラフトツリーノードのシリアライズ用データクラス
    /// </summary>
    public class CraftTreeNodeData
    {
        /// <summary>
        /// アイテムID
        /// </summary>
        public ItemId itemId { get; set; }
        
        /// <summary>
        /// 必要数量
        /// </summary>
        public int requiredCount { get; set; }
        
        /// <summary>
        /// 現在の所持数
        /// </summary>
        public int currentCount { get; set; }
        
        /// <summary>
        /// ノード状態
        /// </summary>
        public NodeState state { get; set; }
        
        /// <summary>
        /// 子ノードのアイテムIDリスト
        /// </summary>
        public List<ItemId> childrenIds { get; set; }
        
        /// <summary>
        /// 選択されたレシピ
        /// </summary>
        public RecipeData selectedRecipe { get; set; }
        
        /// <summary>
        /// デフォルトコンストラクタ（シリアライズ用）
        /// </summary>
        public CraftTreeNodeData()
        {
            childrenIds = new List<ItemId>();
        }
        
        /// <summary>
        /// CraftTreeNodeからCraftTreeNodeDataを作成
        /// </summary>
        /// <param name="node">変換元のノード</param>
        /// <returns>シリアライズ用データ</returns>
        public static CraftTreeNodeData FromNode(CraftTreeNode node)
        {
            if (node == null)
                return null;
                
            var data = new CraftTreeNodeData
            {
                itemId = node.itemId,
                requiredCount = node.requiredCount,
                currentCount = node.currentCount,
                state = node.state,
                selectedRecipe = node.selectedRecipe,
                childrenIds = new List<ItemId>()
            };
            
            // 子ノードのアイテムIDを収集
            foreach (var child in node.children)
            {
                data.childrenIds.Add(child.itemId);
            }
            
            return data;
        }
    }
}