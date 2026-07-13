using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Block
{
    public class MachineRecipeConfigTest
    {
        [Test]
        public void GetRecipeElementByGuidTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 全レシピがGUIDで一意に引けること
            // Every recipe is retrievable by its GUID
            foreach (var recipe in MasterHolder.MachineRecipesMaster.MachineRecipes.Data)
            {
                Assert.AreEqual(recipe, MasterHolder.MachineRecipesMaster.GetRecipeElement(recipe.MachineRecipeGuid));
            }

            // 存在しないGUIDはnull
            // Unknown GUID returns null
            Assert.IsNull(MasterHolder.MachineRecipesMaster.GetRecipeElement(System.Guid.NewGuid()));
        }

        [Test]
        public void DuplicateInputRecipesAreAllowedTest()
        {
            // 同一入力でも例外なくロード
            // Master load must not throw even when two recipes share the same block and inputs
            Assert.DoesNotThrow(() => new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)));
        }
    }
}
