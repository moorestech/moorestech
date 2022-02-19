using Core.Ore;
using Core.Ore.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Test.UnitTest.Core.Other
{
    /// <summary>
    /// 鉱石のコンフィグのテスト
    /// コンフィグが変わったら都度変える
    /// </summary>
    [TestClass]
    public class OreConfigTest
    {
        [TestMethod]
        public void OreIdToItemIdTest()
        {
            IOreConfig oreConfig = new OreConfig();
            Assert.AreEqual(3, oreConfig.OreIdToItemId(1));
            Assert.AreEqual(4, oreConfig.OreIdToItemId(2));
        }
    }
}