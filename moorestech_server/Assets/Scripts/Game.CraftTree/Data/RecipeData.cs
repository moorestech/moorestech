using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Master;

namespace Game.CraftTree.Data
{
    /// <summary>
    /// レシピデータを表すクラス
    /// </summary>
    public class RecipeData
    {
        /// <summary>
        /// レシピの材料リスト
        /// </summary>
        public List<RecipeIngredient> inputs { get; private set; }
        
        /// <summary>
        /// レシピの出力結果
        /// </summary>
        public RecipeResult output { get; private set; }
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="inputs">レシピの材料リスト</param>
        /// <param name="output">レシピの出力結果</param>
        public RecipeData(List<RecipeIngredient> inputs, RecipeResult output)
        {
            if (inputs == null || !inputs.Any())
                throw new ArgumentException("Recipe must have at least one ingredient", nameof(inputs));
                
            if (output == null)
                throw new ArgumentNullException(nameof(output), "Recipe must have an output");
                
            this.inputs = inputs;
            this.output = output;
        }
        
        /// <summary>
        /// レシピが製作可能かをインベントリと照らし合わせて判定する
        /// </summary>
        /// <param name="inventory">インベントリ（アイテムIDと所持数のディクショナリ）</param>
        /// <returns>製作可能な場合はtrue、そうでない場合はfalse</returns>
        public bool CanCraft(Dictionary<ItemId, int> inventory)
        {
            if (inventory == null)
                return false;
                
            // 各材料ごとにインベントリに十分な数量があるか確認
            foreach (var ingredient in inputs)
            {
                if (!inventory.TryGetValue(ingredient.itemId, out int count) || count < ingredient.count)
                {
                    return false;
                }
            }
            
            return true;
        }
    }
}