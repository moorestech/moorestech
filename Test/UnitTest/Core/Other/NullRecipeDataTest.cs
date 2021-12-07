using Core.Block.RecipeConfig;
using Core.Block.RecipeConfig.Data;
using Core.Item;
using Core.Item.Config;
using NUnit.Framework;

namespace Test.UnitTest.Core.Other
{
    public class NullRecipeDataTest
    {
        [Test]
        public void NullTest()
        {
            IMachineRecipeData recipeData = new TestMachineRecipeConfig(new ItemStackFactory(new TestItemConfig())).GetNullRecipeData();
            Assert.AreEqual(0,recipeData.ItemInputs.Count);
            Assert.AreEqual(0,recipeData.ItemOutputs.Count);
            Assert.AreEqual(0,recipeData.BlockId);
            Assert.AreEqual(0,recipeData.Time);
        }
    }
}