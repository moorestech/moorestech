using NUnit.Framework;
using Game.Block.Interface.Component;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core.Block.SpaceElevator
{
    // ケーブルプールへ足す積荷の判定（サーバーとクライアントが共有する口）
    // The shared check for payloads that feed the cable pool
    public class SpaceElevatorCableItemsTest
    {
        [Test]
        public void 拠点のケーブル品だけがプールへ足される()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Assert.IsTrue(SpaceElevatorCableItems.Contains(ForUnitTestItemId.TestElevatorCable));
            Assert.IsFalse(SpaceElevatorCableItems.Contains(ForUnitTestItemId.TestElevatorSatellite));
            Assert.IsFalse(SpaceElevatorCableItems.Contains(ForUnitTestItemId.CablePayloadId));
        }
    }
}
