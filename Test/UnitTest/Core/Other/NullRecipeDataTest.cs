using Core.Block.RecipeConfig;
using Core.Block.RecipeConfig.Data;
using Core.ConfigJson;
using Core.Item;
using Core.Item.Config;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.StartServerSystem;
using Test.Module.TestConfig;
using Test.Module.TestMod;

namespace Test.UnitTest.Core.Other
{
    public class NullRecipeDataTest
    {
        [Test]
        public void NullTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var recipeData = serviceProvider.GetService<IMachineRecipeConfig>().GetNullRecipeData();
            Assert.AreEqual(0, recipeData.ItemInputs.Count);
            Assert.AreEqual(0, recipeData.ItemOutputs.Count);
            Assert.AreEqual(0, recipeData.BlockId);
            Assert.AreEqual(0, recipeData.Time);
        }
    }
}