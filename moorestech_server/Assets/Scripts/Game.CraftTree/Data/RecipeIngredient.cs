using System;
using Core.Item;
using Core.Master;

namespace Game.CraftTree.Data
{
    /// <summary>
    /// レシピで必要な材料アイテムを表すクラス
    /// </summary>
    public class RecipeIngredient
    {
        /// <summary>
        /// アイテムID
        /// </summary>
        public ItemId itemId { get; private set; }
        
        /// <summary>
        /// 必要数量
        /// </summary>
        public int count { get; private set; }
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="itemId">アイテムID</param>
        /// <param name="count">必要数量</param>
        public RecipeIngredient(ItemId itemId, int count)
        {
            if (count <= 0)
                throw new ArgumentException("Recipe ingredient count must be positive", nameof(count));
                
            this.itemId = itemId;
            this.count = count;
        }
    }
}