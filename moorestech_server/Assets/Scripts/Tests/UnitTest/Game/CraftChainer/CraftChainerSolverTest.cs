using System.Collections.Generic;
using System.Linq;
using Core.Master;
using NUnit.Framework;

namespace Tests.UnitTest.Game.CraftChainer
{
    public class CraftChainerSolverTest
    {
        [Test]
        public void Case01()
        {
            var recipeSettings = @"
1:A 1 ← B 1, C 2
2:B 1 ← C 2";
            var initialSettings = @"
B 1";
            var targetItem = "A 1";
            var expected = @"";
            
            ExecuteTest(recipeSettings, initialSettings, targetItem, expected);
        }
        
        [Test]
        public void Case05()
        {
            var recipeSettings = @"
1:A 1 ← B 3
2:B 1 ← C 2
3:B 1 ← D 2
4:D 1 ← E 1";
            var initialSettings = @"
C 4
D 2";
            var targetItem = "A 1";
            var expected = @"
1:1
2:2
3:1";
            ExecuteTest(recipeSettings, initialSettings, targetItem, expected);
        }
        
        
        private void ExecuteTest(
            string recipesStr, 
            string initialInventoryStr, 
            string targetItemStr, 
            string expectedStr)
        {
            var (recipes, initialInventory, targetItemId, targetQuantity, expected) = ParseInput(recipesStr, initialInventoryStr, targetItemStr, expectedStr);
            var actual = CraftingSolver.Solve(recipes, initialInventory, targetItemId, targetQuantity);
            
            if (expected == null)
            {
                Assert.IsNull(actual);
                return;
            }
            
            Assert.IsNotNull(actual);
            Assert.AreEqual(expected.Count, actual.Count);
            
            foreach (var kvp in expected)
            {
                Assert.IsTrue(actual.ContainsKey(kvp.Key));
                Assert.AreEqual(kvp.Value, actual[kvp.Key]);
            }
        }
        
        private (List<Recipe> recipes, Dictionary<ItemId, int> initialInventory, ItemId targetItemId, int targetQuantity, Dictionary<RecipeId, int> expected) ParseInput(
            string recipesStr,
            string initialInventoryStr,
            string targetItemStr,
            string expectedStr)
        {
            var recipes = ParseRecipes();
            var initialInventory = ParseInitialInventory();
            var (targetItemId, targetQuantity) = ParseTargetItem();
            var expected = ParseExpected();
            
            return (recipes, initialInventory, targetItemId, targetQuantity, expected);
            
            #region Internal
            
            List<Recipe> ParseRecipes()
            {
                var result = new List<Recipe>();
                
                var recipeLines = recipesStr.Split('\n').Where(x => !string.IsNullOrWhiteSpace(x));
                foreach (var recipeLine in recipeLines)
                {
                    var recipeId = new RecipeId(int.Parse(recipeLine.Split(':')[0]));
                    var inputItemsStr = recipeLine.Split(':')[1].Split('←')[1].Trim();
                    var outputItemStr = recipeLine.Split(':')[1].Split('←')[0].Trim();
                    
                    var inputItems = ParseRecipeItems(inputItemsStr);
                    var outputItems = ParseRecipeItems(outputItemStr);
                    
                    result.Add(new Recipe(recipeId, inputItems, outputItems));
                }
                
                return result;
            }
            
            Dictionary<ItemId,int> ParseInitialInventory()
            {
                var result = new Dictionary<ItemId, int>();
                
                var inventoryLines = initialInventoryStr.Split('\n').Where(x => !string.IsNullOrWhiteSpace(x));
                foreach (var inventoryLine in inventoryLines)
                {
                    var itemName = inventoryLine.Split(' ')[0];
                    var itemId = new ItemId(GetItemId(itemName));
                    var quantity = int.Parse(inventoryLine.Split(' ')[1]);
                    
                    result.Add(itemId, quantity);
                }
                
                return result;
            }
            
            (ItemId targetItemId, int targetQuantity) ParseTargetItem()
            {
                var itemName = targetItemStr.Split(' ')[0];
                var itemId = new ItemId(GetItemId(itemName));
                var quantity = int.Parse(targetItemStr.Split(' ')[1]);
                
                return (itemId, quantity);
            }
            
            Dictionary<RecipeId,int> ParseExpected()
            {
                var result = new Dictionary<RecipeId, int>();
                
                var expectedLines = expectedStr.Split('\n').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                if (expectedLines.Count == 0)
                {
                    return null;
                }
                
                foreach (var expectedLine in expectedLines)
                {
                    var recipeId = new RecipeId(int.Parse(expectedLine.Split(':')[0]));
                    var quantity = int.Parse(expectedLine.Split(':')[1]);
                    
                    result.Add(recipeId, quantity);
                }
                
                return result;
            }
            
            List<RecipeItem> ParseRecipeItems(string itemRecipes)
            {
                var result = new List<RecipeItem>();
                foreach (var item in itemRecipes.Split(','))
                {
                    var trimItem = item.Trim();
                    var itemName = trimItem.Split(' ')[0];
                    var itemId = new ItemId(GetItemId(itemName));
                    var quantity = int.Parse(trimItem.Split(' ')[1]);
                    
                    result.Add(new RecipeItem(itemId, quantity));
                }
                return result;
            }
            
            int GetItemId(string itemName)
            {
                return itemName switch
                {
                    "A" => 1,
                    "B" => 2,
                    "C" => 3,
                    "D" => 4,
                    "E" => 5,
                    _ => throw new System.Exception($"Unknown item name: {itemName}")
                };
            }
            
  #endregion
        }
    }
}