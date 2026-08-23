using System.Collections.Generic;
using Client.Game.InGame.Construction;
using Core.Master;
using Game.Construction;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.GearChainPoleConnect
{
    /// <summary>
    /// ギアチェーンポールのチェーン素材予約が実消費分になることの契約試験
    /// Contract tests proving the gear chain pole reserves exactly what the cell consumes
    /// </summary>
    public class GearChainPoleReservedCostTest
    {
        [Test]
        public void 財布が賄うセルのチェーン素材予約は空になる()
        {
            CreateServer();

            var mirror = new ClientRemainingPlacementCountDatastore();
            mirror.ApplyAll(new Dictionary<BlockId, int> { { ForUnitTestModBlockId.GearBeltConveyor, 1 } });
            var walletQuery = new ConstructionWalletQuery(mirror);

            // 予約は「そのセルで実際に消費する建設コスト」。財布が賄うなら何も予約しない
            // The reservation is what the cell actually consumes; a wallet-covered cell reserves nothing
            Assert.AreEqual(0, walletQuery.GetItemsToConsume(ForUnitTestModBlockId.GearBeltConveyor).Count);
        }

        [Test]
        public void 財布が空なら建設コスト全額を予約する()
        {
            CreateServer();

            var walletQuery = new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());

            Assert.AreEqual(2, walletQuery.GetItemsToConsume(ForUnitTestModBlockId.GearBeltConveyor).Count);
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
