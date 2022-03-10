using Core.Block.RecipeConfig;
using Core.Block.RecipeConfig.Data;
using Core.ConfigJson;
using Core.Item;
using Core.Item.Config;
using NUnit.Framework;
using Test.Module.TestConfig;

namespace Test.UnitTest.Core.Other
{
    public class NullRecipeDataTest
    {
        [Test]
        public void NullTest()
        {
            var config = new ConfigPath(TestModuleConfigPath.FolderPath);
            IMachineRecipeData recipeData = new MachineRecipeConfig(new ItemStackFactory(new ItemConfig(config)),config)
                .GetNullRecipeData();
            Assert.AreEqual(0, recipeData.ItemInputs.Count);
            Assert.AreEqual(0, recipeData.ItemOutputs.Count);
            Assert.AreEqual(0, recipeData.BlockId);
            Assert.AreEqual(0, recipeData.Time);
        }
    }
}