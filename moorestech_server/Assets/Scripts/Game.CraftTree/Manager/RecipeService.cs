using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Master;
using Game.CraftTree.Data;

namespace Game.CraftTree.Manager
{
    /// <summary>
    /// レシピ情報を提供するサービスクラス
    /// </summary>
    public class RecipeService
    {
        private readonly CraftRecipeMaster _craftRecipeMaster;
        
        /// <summary>
        /// コンストラクタ
        /// </summary>
        /// <param name="craftRecipeMaster">クラフトレシピマスター</param>
        public RecipeService(CraftRecipeMaster craftRecipeMaster)
        {
            _craftRecipeMaster = craftRecipeMaster ?? throw new ArgumentNullException(nameof(craftRecipeMaster));
        }
        
        /// <summary>
        /// アイテムのレシピリストを取得
        /// </summary>
        /// <param name="itemId">対象アイテムID</param>
        /// <returns>レシピデータのリスト</returns>
        public List<RecipeData> GetRecipesForItem(ItemId itemId)
        {
            var result = new List<RecipeData>();
            
            // マスターからレシピを取得
            var recipes = _craftRecipeMaster.GetRecipesFromResult(itemId);
            if (recipes == null)
                return result;
                
            foreach (var recipe in recipes)
            {
                // アイテムが一致するレシピを変換して追加
                if (recipe.Result.itemId.Equals(itemId))
                {
                    // 材料リストを作成
                    var inputs = recipe.Materials.Select(m => 
                        new RecipeIngredient(m.itemId, m.count)).ToList();
                        
                    // 結果を作成
                    var output = new RecipeResult(recipe.Result.itemId, recipe.Result.count);
                    
                    // レシピデータを作成して追加
                    result.Add(new RecipeData(inputs, output));
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// 特定のレシピを取得
        /// </summary>
        /// <param name="resultItemId">結果アイテムID</param>
        /// <param name="inputs">材料リスト</param>
        /// <returns>マッチするレシピ、または存在しない場合はnull</returns>
        public RecipeData GetRecipe(ItemId resultItemId, List<RecipeIngredient> inputs)
        {
            // 引数チェック
            if (inputs == null || !inputs.Any())
                return null;
                
            // 材料から検索条件を作成
            var materials = inputs.Select(i => new
            {
                ItemId = i.itemId,
                Count = i.count
            }).ToList();
            
            // マスターからレシピを検索
            var recipes = _craftRecipeMaster.GetRecipesFromResult(resultItemId);
            if (recipes == null)
                return null;
                
            // 材料がマッチするレシピを探す
            foreach (var recipe in recipes)
            {
                if (recipe.Result.itemId.Equals(resultItemId) && 
                    recipe.Materials.Count == materials.Count)
                {
                    bool match = true;
                    // すべての材料が一致するか確認
                    foreach (var material in materials)
                    {
                        if (!recipe.Materials.Any(m => 
                            m.itemId.Equals(material.ItemId) && m.count == material.Count))
                        {
                            match = false;
                            break;
                        }
                    }
                    
                    if (match)
                    {
                        // 一致するレシピを見つけた
                        var recipeInputs = recipe.Materials.Select(m => 
                            new RecipeIngredient(m.itemId, m.count)).ToList();
                        var output = new RecipeResult(recipe.Result.itemId, recipe.Result.count);
                        return new RecipeData(recipeInputs, output);
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// レシピがアンロックされているかどうか確認
        /// </summary>
        /// <param name="playerId">プレイヤーID</param>
        /// <param name="resultItemId">結果アイテムID</param>
        /// <param name="inputs">材料リスト</param>
        /// <returns>アンロックされている場合はtrue</returns>
        public bool IsRecipeUnlocked(PlayerId playerId, ItemId resultItemId, List<RecipeIngredient> inputs)
        {
            // 現在は全てのレシピが最初からアンロックされていると仮定
            // 実際のゲームシステムに合わせて、アンロック状態を確認するロジックに変更する必要がある
            return true;
        }
    }
}