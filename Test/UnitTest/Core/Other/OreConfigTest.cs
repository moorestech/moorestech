using Core.ConfigJson;
using Core.Ore;
using Core.Ore.Config;
using NUnit.Framework;
using Test.Module.TestConfig;

namespace Test.UnitTest.Core.Other
{
    /// <summary>
    /// 鉱石のコンフィグのテスト
    /// コンフィグが変わったら都度変える
    /// </summary>
    public class OreConfigTest
    {
        [Test]
        public void OreIdToItemIdTest()
        {
            IOreConfig oreConfig = new OreConfig(new ConfigPath(TestModuleConfigPath.FolderPath));
            Assert.AreEqual(3, oreConfig.OreIdToItemId(1));
            Assert.AreEqual(4, oreConfig.OreIdToItemId(2));
        }
    }
}