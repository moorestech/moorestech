using Core.Block.RecipeConfig;
using Core.Block.RecipeConfig.Data;
using Core.Item;
using Core.Item.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.UnitTest.Core.Other
{
    [TestClass]
    public class NullRecipeDataTest
    {
        [TestMethod]
        public void NullTest()
        {
            IMachineRecipeData recipeData = new TestMachineRecipeConfig(new ItemStackFactory(new TestItemConfig()))
                .GetNullRecipeData();
            Assert.AreEqual(0, recipeData.ItemInputs.Count);
            Assert.AreEqual(0, recipeData.ItemOutputs.Count);
            Assert.AreEqual(0, recipeData.BlockId);
            Assert.AreEqual(0, recipeData.Time);
        }
    }
}