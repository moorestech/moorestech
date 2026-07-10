using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Block
{
    public class MachineRecipeConfigTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void GUIDからレシピを取得できる()
        {
            var recipe = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.MachineIdRecipe);

            Assert.IsNotNull(recipe);
            Assert.AreEqual(ForUnitTestMachineRecipeId.MachineIdRecipe, recipe.MachineRecipeGuid);
        }

        [Test]
        public void 同じ投入物の複数レシピをGUIDで区別できる()
        {
            var first = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.MachineIdRecipe);
            var alternative = MasterHolder.MachineRecipesMaster.GetRecipeElement(ForUnitTestMachineRecipeId.AlternativeMachineIdRecipe);

            Assert.AreEqual(first.BlockGuid, alternative.BlockGuid);
            Assert.AreEqual(first.InputItems[0].ItemGuid, alternative.InputItems[0].ItemGuid);
            Assert.AreNotEqual(first.OutputItems[0].ItemGuid, alternative.OutputItems[0].ItemGuid);
        }

        [Test]
        public void 存在しないGUIDはnullを返す()
        {
            Assert.IsNull(MasterHolder.MachineRecipesMaster.GetRecipeElement(System.Guid.NewGuid()));
        }
    }
}
