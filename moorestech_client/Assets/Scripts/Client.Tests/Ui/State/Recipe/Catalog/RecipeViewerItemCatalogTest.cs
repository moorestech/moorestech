using System.Linq;
using Client.Game.InGame.UI.Inventory.RecipeViewer;
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
    /// 一覧のアイテムは表示判定が Show のものだけをマスタの列挙順で返す（L0）
    /// The item list returns only Show items in master enumeration order (L0)
    /// </summary>
    public class RecipeViewerItemCatalogTest
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
        public void 表示判定がShowのアイテムだけをマスタの列挙順で返す()
        {
            var viewer = new ItemRecipeViewerDataContainer(_unlock);
            var result = new RecipeViewerItemCatalog(viewer).BuildVisibleItems();

            // 共有の評価器で Show のものだけが同じ順で並ぶ
            // Only items the shared evaluator marks Show appear, in the same order
            var allIds = MasterHolder.ItemMaster.GetItemAllIds().ToList();
            var expected = allIds.Where(id => viewer.EvaluateVisibility(id, MasterHolder.ItemMaster.GetItemMaster(id)) == RecipeViewerItemVisibility.Show).ToList();
            CollectionAssert.AreEqual(expected, result);
            Assert.IsNotEmpty(result);
            Assert.Less(result.Count, allIds.Count);
        }
    }
}
