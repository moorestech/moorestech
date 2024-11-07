using System.Collections.Generic;
using Core.Master;
using NUnit.Framework;

namespace Tests.UnitTest.Game.CraftChainer
{
    /// <summary>
    /// 凡例 Legend
    /// 
    /// レシピ設定 Recipe setting
    /// レシピ番号 アウトプットID 数量 ← インプットID 数量, インプットID 数量
    /// RecipeNumber OutputID Quantity ← InputID Quantity, InputID Quantity
    /// 
    /// 初期インベントリ Initial inventory
    /// アイテムID 数量
    /// ItemID Quantity
    /// 
    /// ターゲットアイテム（必ず1個） Target item (always 1)
    /// アイテムID 数量
    /// ItemID Quantity
    /// 
    /// 期待値 Expected value
    /// レシピ番号 数量
    /// RecipeNumber Quantity
    /// 
    /// </summary>
    public class CraftChainerSolverTest
    {
        /// <summary>
        /// レシピ設定 Recipe setting
        /// 1 A 1 ← B 1, C 2
        /// 2 B 1 ← C 2
        /// 
        /// 初期インベントリ Initial inventory
        /// B 1
        /// 
        /// ターゲットアイテム Target item
        /// A 1
        /// 
        /// 期待値 Expected value
        /// 1 1
        /// </summary>
        [Test]
        public void Case01()
        {
            var itemAId = new ItemId(0);
            var itemBId = new ItemId(1);
            var itemCId = new ItemId(2);
            
            var recipe1Id = new RecipeId(0);
            var recipe2Id = new RecipeId(1);
            
            var recipes = new List<Recipe>
            {
                new(recipe1Id, new List<InputItem> {new(itemAId,1)} , new List<OutputItem> {new(itemBId,1), new(itemCId,2)}),
                new(recipe2Id, new List<InputItem> {new(itemBId,1)} , new List<OutputItem> {new(itemCId,2)}),
            };
            var initialInventory = new Dictionary<ItemId, int>
            {
                {itemBId, 1}
            };
            var targetItemId = itemAId;
            var targetQuantity = 1;
            
            var expected = new Dictionary<RecipeId, int>
            {
                {recipe1Id, 1}
            };
            
            ExecuteTest(recipes, initialInventory, targetItemId, targetQuantity, expected);
        }
        
        
        private void ExecuteTest(List<Recipe> itemsProducedByRecipe, Dictionary<ItemId, int> initialInventory, ItemId targetItemName, int targetQuantity, Dictionary<RecipeId, int> expected)
        {
            var actual = CraftingSolver.Solve(itemsProducedByRecipe, initialInventory, targetItemName, targetQuantity);
            
            foreach (var kvp in expected)
            {
                Assert.IsTrue(actual.ContainsKey(kvp.Key));
                Assert.AreEqual(kvp.Value, actual[kvp.Key]);
            }
        }
    }
}