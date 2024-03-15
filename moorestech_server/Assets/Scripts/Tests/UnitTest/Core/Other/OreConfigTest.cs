using Core.Ore;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Other
{
    /// <summary>
    ///     鉱石のコンフィグのテスト
    ///     コンフィグが変わったら都度変える
    /// </summary>
    public class OreConfigTest
    {
        [Test]
        public void OreIdToItemIdTest()
        {
            var (packetResponse, serviceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var oreConfig = serviceProvider.GetService<IOreConfig>();
            Assert.AreEqual(3, oreConfig.OreIdToItemId(1));
            Assert.AreEqual(4, oreConfig.OreIdToItemId(2));
        }

        [Test]
        public void ModItToOreIdsTest()
        {
            var (packetResponse, serviceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var oreConfig = serviceProvider.GetService<IOreConfig>();

            Assert.AreEqual(2, oreConfig.GetOreIds("Test Author:forUniTest").Count);
        }
    }
}