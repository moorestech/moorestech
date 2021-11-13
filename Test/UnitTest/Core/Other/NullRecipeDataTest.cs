using Core.Config.Recipe.Data;
using NUnit.Framework;

namespace Test.UnitTest.Core.Other
{
    public class NullRecipeDataTest
    {
        [Test]
        public void NullTest()
        {
            IMachineRecipeData recipeData = new NullMachineRecipeData();
            Assert.AreEqual(0,recipeData.ItemInputs.Count);
            Assert.AreEqual(0,recipeData.ItemOutputs.Count);
            Assert.AreEqual(0,recipeData.BlockId);
            Assert.AreEqual(0,recipeData.Time);
        }
    }
}