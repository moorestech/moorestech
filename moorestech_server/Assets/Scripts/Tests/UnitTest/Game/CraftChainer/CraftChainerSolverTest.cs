using System.Collections.Generic;
using System.Linq;
using Core.Master;
using NUnit.Framework;

namespace Tests.UnitTest.Game.CraftChainer
{
    /// <summary>
    /// 凡例 legend
    /// 
    /// recipesStr
    /// レシピ番号:アウトプットアイテム名 数量, アウトプットアイテム名 数量 ← インプットアイテム名 数量, インプットアイテム名 数量
    /// RecipeId:OutputItemName Quantity, OutputItemName Quantity ← InputItemName Quantity, InputItemName Quantity
    ///
    /// initialInventoryStr
    /// アイテム名 数量
    /// ItemName Quantity
    ///
    /// targetItemStr
    /// アイテム名 数量
    /// ItemName Quantity
    ///
    /// expectedStr
    /// レシピ番号:使用レシピ回数
    /// RecipeId:UseRecipeCount
    /// 
    /// </summary>
    public class CraftChainerSolverTest
    {
        [Test]
        public void TestCase01()
        {
            var recipesStr = @"
1:A 1 ← B 1, C 2
2:B 1 ← C 2";
            var initialInventoryStr = @"
B 1";
            var targetItem = "A 1";
            var expectedStr = @""; // 解が存在しない場合、期待される結果は空文字列
            ExecuteTest(recipesStr, initialInventoryStr, targetItem, expectedStr);
        }
        
        [Test]
        public void TestCase02()
        {
            var recipesStr = @"
1:A 1 ← B 1, C 2
2:B 1 ← C 2";
            var initialInventoryStr = @""; // 初期在庫なし
            var targetItem = "A 1";
            var expectedStr = @""; // 解が存在しない
            ExecuteTest(recipesStr, initialInventoryStr, targetItem, expectedStr);
        }
        
        [Test]
        public void TestCase03()
        {
            var recipesStr = @"
1:D 1 ← E 8
2:E 4 ← F 2
3:E 4 ← G 2";
            var initialInventoryStr = @""; // 初期在庫なし
            var targetItem = "D 1";
            var expectedStr = @""; // 解が存在しない
            ExecuteTest(recipesStr, initialInventoryStr, targetItem, expectedStr);
        }
        
        [Test]
        public void TestCase04()
        {
            var recipesStr = @"
1:A 1 ← B 2
2:A 1 ← B 4";
            var initialInventoryStr = @"
B 3";
            var targetItem = "A 1";
            var expectedStr = @"
1:1"; // レシピ1を1回使用
            ExecuteTest(recipesStr, initialInventoryStr, targetItem, expectedStr);
        }
        
        [Test]
        public void TestCase05()
        {
            var recipesStr = @"
1:A 1 ← B 3
2:B 1 ← C 2
3:B 1 ← D 2
4:D 1 ← E 1";
            var initialInventoryStr = @"
C 4
D 2";
            var targetItem = "A 1";
            var expectedStr = @"
1:1
2:2
3:1";
            ExecuteTest(recipesStr, initialInventoryStr, targetItem, expectedStr);
        }
        
        [Test]
        public void TestCase06()
        {
            var recipesStr = @"
1:A 1 ← B 2, C 2
2:C 1 ← D 1
3:B 1, C 1 ← D 3";
            var initialInventoryStr = @"
B 1
D 10";
            var targetItem = "A 1";
            var expectedStr = @"
1:1
3:2";
            ExecuteTest(recipesStr, initialInventoryStr, targetItem, expectedStr);
        }
        
        [Test]
        public void TestCase07()
        {
            var recipesStr = @"
1:X 1 ← Y 1, Z 1
2:Y 1 ← W 5
3:Z 1 ← W 10";
            var initialInventoryStr = @"
W 15";
            var targetItem = "X 1";
            var expectedStr = @"
1:1
2:1
3:1";
            ExecuteTest(recipesStr, initialInventoryStr, targetItem, expectedStr);
        }
        
        [Test]
        public void TestCase08()
        {
            var recipesStr = @"
1:A 1 ← B 2, C 1
2:A 1 ← B 1, D 2
3:B 1 ← E 3
4:C 1 ← E 2
5:D 1 ← E 1";
            var initialInventoryStr = @"
E 10";
            var targetItem = "A 2";
            var expectedStr = @"
2:2
3:2
5:4";
            ExecuteTest(recipesStr, initialInventoryStr, targetItem, expectedStr);
        }
        
        [Test]
        public void TestCase09()
        {
            var recipesStr = @"
1:A 1 ← B 10
2:B 1 ← C 5
3:C 1 ← D 2
4:D 1 ← E 1";
            var initialInventoryStr = @"
C 2
E 10";
            var targetItem = "A 1";
            var expectedStr = @""; // 解が存在しない
            ExecuteTest(recipesStr, initialInventoryStr, targetItem, expectedStr);
        }
        
        [Test]
        public void TestCase10()
        {
            var recipesStr = @"
1:A 1 ← B 1
2:B 3 ← C 2
3:C 5 ← D 1";
            var initialInventoryStr = @"
D 1";
            var targetItem = "A 1";
            var expectedStr = @"
1:1
2:1
3:1";
            ExecuteTest(recipesStr, initialInventoryStr, targetItem, expectedStr);
        }
        
        [Test]
        public void TestCase11()
        {
            var recipesStr = @"
1:A 1 ← B 1, C 2
2:B 1 ← D 3
3:C 1 ← D 2
4:D 1 ← E 1
5:B 1 ← F 1
6:C 1 ← G 1";
            var initialInventoryStr = @"
D 1
F 1
G 2";
            var targetItem = "A 1";
            var expectedStr = @"
1:1
5:1
6:2";
            ExecuteTest(recipesStr, initialInventoryStr, targetItem, expectedStr);
        }
        
        [Test]
        public void TestCase12()
        {
            var recipesStr = @"
1:A 1 ← B 1, C 1
2:B 1 ← C 2
3:C 1 ← E 1
4:E 1 ← B 1
5:B 1 ← D 1";
            var initialInventoryStr = @"
D 2";
            var targetItem = "A 1";
            var expectedStr = @"
1:1
3:1
4:1
5:2";
            ExecuteTest(recipesStr, initialInventoryStr, targetItem, expectedStr);
        }
        
        [Test]
        public void TestCase13()
        {
            var recipesStr = @"
1:A 1 ← B1 1, B2 1, B3 1, B4 1
2:B1 1 ← C1 1
3:B2 1 ← C2 1
4:B3 1 ← C3 1
5:B4 1 ← C4 1
6:C1 1 ← D 1
7:C2 1 ← D 1
8:C3 1 ← D 1
9:C4 1 ← D 1";
            var initialInventoryStr = @"
D 5";
            var targetItem = "A 1";
            var expectedStr = @"
1:1
2:1
3:1
4:1
5:1
6:1
7:1
8:1
9:1";
            ExecuteTest(recipesStr, initialInventoryStr, targetItem, expectedStr);
        }
        
        [Test]
        public void TestCase14()
        {
            var recipesStr = @"
1:A 1 ← B 2
2:B 1 ← C 2
3:B 1 ← D 1";
            var initialInventoryStr = @"
C 1
D 1";
            var targetItem = "A 1";
            var expectedStr = @""; // 解が存在しない
            ExecuteTest(recipesStr, initialInventoryStr, targetItem, expectedStr);
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
            
            Dictionary<ItemId, int> ParseInitialInventory()
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
            
            Dictionary<RecipeId, int> ParseExpected()
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
            
  #endregion
        }
        
        private int _nextItemId = 1;
        private Dictionary<string, int> _itemNameToId = new();
        
        private int GetItemId(string itemName)
        {
            // アイテム名をユニークなIDにマッピングするメソッド
            // ここでは簡単のために静的な辞書を使用
            if (!_itemNameToId.TryGetValue(itemName, out var itemId))
            {
                itemId = _nextItemId++;
                _itemNameToId[itemName] = itemId;
            }
            return itemId;
        }
    }
}