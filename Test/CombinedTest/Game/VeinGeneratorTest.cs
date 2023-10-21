#if NET6_0
using Core.Ore;
using Game.WorldMap;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Test.Module.TestMod;

namespace Test.CombinedTest.Game
{
    public class VeinGeneratorTest
    {
        [Test]
        public void GenerateTest()
        {
            //500x500100OK
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var veinGenerator = serviceProvider.GetService<VeinGenerator>();

            var count = 0;
            for (var i = 0; i < 500; i++)
            for (var j = 0; j < 500; j++)
            {
                var oreId = veinGenerator.GetOreId(i, j);
                if (oreId != OreConst.NoneOreId) count++;
            }

            Assert.True(100 < count);
        }
    }
}
#endif