using Core.Ore;
using Game.WorldMap;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class VeinGeneratorTest
    {
        [Test]
        public void GenerateTest()
        {
            //直接生成してテストできないので、500x500の範囲で生成して100個以上鉱石があればOKとする
            var (_, serviceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
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