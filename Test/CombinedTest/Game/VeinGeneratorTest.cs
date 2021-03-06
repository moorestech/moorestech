using Core.ConfigJson;
using Core.Ore;
using Core.Ore.Config;
using Game.WorldMap;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;

using Test.Module.TestConfig;
using Test.Module.TestMod;

namespace Test.CombinedTest.Game
{
    public class VeinGeneratorTest
    {
        [Test]
        public void GenerateTest()
        {
            //直接生成してテストできないので、500x500の範囲で生成して100個以上鉱石があればOKとする
            var (_, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var veinGenerator = serviceProvider.GetService<VeinGenerator>();

            var count = 0;
            for (int i = 0; i < 500; i++)
            {
                for (int j = 0; j < 500; j++)
                {
                    var oreId = veinGenerator.GetOreId(i, j);
                    if (oreId != OreConst.NoneOreId)
                    {
                        count++;
                    }
                }
            }
            
            Assert.True(100 < count);
        }
        
    }
}