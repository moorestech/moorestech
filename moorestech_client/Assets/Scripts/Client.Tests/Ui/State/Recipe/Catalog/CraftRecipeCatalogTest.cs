using System.Linq;
using Client.Ui.State.Recipe.Catalog;
using Core.Master;
using Game.UnlockState;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.Recipe.Catalog
{
    /// <summary>
    /// 解放済みのクラフトレシピだけをマスタの順で写す（L0）
    /// Copies only unlocked craft recipes in master order (L0)
    /// </summary>
    public class CraftRecipeCatalogTest
    {
        private IGameUnlockStateDataController _unlock;

        [SetUp]
        public void SetUp()
        {
            // テスト mod のマスタとサーバーの解放状態を作る
            // Build the test-mod master and the server unlock state
            var (_, provider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            _unlock = provider.GetService<IGameUnlockStateDataController>();
        }

        [Test]
        public void 解放済みのレシピだけを結果と素材のIDへ写す()
        {
            // ロック中の 1 件を解放してから写す
            // Unlock one locked recipe, then copy
            var all = MasterHolder.CraftRecipeMaster.GetAllCraftRecipes();
            var locked = all.First(r => !_unlock.CraftRecipeUnlockStateInfos[r.CraftRecipeGuid].IsUnlocked);
            _unlock.UnlockCraftRecipe(locked.CraftRecipeGuid);

            var result = CraftRecipeCatalog.BuildUnlocked(_unlock);

            // 件数・順と、解放した 1 件の中身を見る
            // Check count, order, and the contents of the unlocked recipe
            var expected = all.Where(r => _unlock.CraftRecipeUnlockStateInfos[r.CraftRecipeGuid].IsUnlocked).ToList();
            Assert.Less(expected.Count, all.Length);
            CollectionAssert.AreEqual(expected.Select(r => r.CraftRecipeGuid), result.Select(r => r.RecipeGuid));
            var copied = result.First(r => r.RecipeGuid == locked.CraftRecipeGuid);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(locked.CraftResultItemGuid), copied.ResultItemId);
            Assert.AreEqual(locked.CraftResultCount, copied.ResultCount);
            Assert.AreEqual(locked.CraftTime, copied.CraftTime);
            Assert.AreEqual(locked.RequiredItems.Length, copied.RequiredItems.Count);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(locked.RequiredItems[0].ItemGuid), copied.RequiredItems[0].ItemId);
            Assert.AreEqual(locked.RequiredItems[0].Count, copied.RequiredItems[0].Count);
        }
    }
}
