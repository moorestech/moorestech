using Client.Ui.State.BlockDetail;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.Ui.State.BlockDetail
{
    public class BlockNetworkInfoCacheElectricTrackingTest
    {
        [SetUp]
        public void SetUp()
        {
            // MasterHolderの静的初期化だけが目的で戻り値は使わない
            // Only used to initialize the static MasterHolder; the return values are unused
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 電柱は電力網のポーリング対象になる()
        {
            var poleParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ElectricPoleId).BlockParam;

            Assert.IsTrue(BlockNetworkInfoCache.JoinsElectricNetwork(poleParam));
        }

        [Test]
        public void 電線に繋がらないブロックは電力網のポーリング対象にならない()
        {
            var chestParam = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ChestId).BlockParam;

            Assert.IsFalse(BlockNetworkInfoCache.JoinsElectricNetwork(chestParam));
        }
    }
}
