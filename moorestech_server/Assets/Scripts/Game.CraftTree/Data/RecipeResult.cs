using System;
using Core.Item;
using Core.Master;

namespace Game.CraftTree.Data
{
    /// <summary>
    /// レシピの結果（出力アイテム）を表すクラス
    /// </summary>
    public class RecipeResult
    {
        /// <summary>
        /// 出力されるアイテムID
        /// </summary>
        public ItemId itemId { get; private set; }
        
        /// <summary>
        /// 出力アイテム数
        /// </summary>
        public int count { get; private set; }
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="itemId">出力アイテムID</param>
        /// <param name="count">出力数量</param>
        public RecipeResult(ItemId itemId, int count)
        {
            if (count <= 0)
                throw new ArgumentException("Recipe result count must be positive", nameof(count));
                
            this.itemId = itemId;
            this.count = count;
        }
    }
}