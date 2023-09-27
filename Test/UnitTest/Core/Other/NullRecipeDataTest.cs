using Game.Block.RecipeConfig;
using Core.ConfigJson;
using Core.Item;
using Core.Item.Config;
using Game.Block.Interface.RecipeConfig;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;


using Test.Module.TestMod;

#if NET6_0
namespace Test.UnitTest.Core.Other
{
    public class NullRecipeDataTest
    {
        [Test]
        public void NullTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var recipeData = serviceProvider.GetService<IMachineRecipeConfig>().GetEmptyRecipeData();
            Assert.AreEqual(0, recipeData.ItemInputs.Count);
            Assert.AreEqual(0, recipeData.ItemOutputs.Count);
            Assert.AreEqual(0, recipeData.BlockId);
            Assert.AreEqual(0, recipeData.Time);
        }
    }
}
#endif