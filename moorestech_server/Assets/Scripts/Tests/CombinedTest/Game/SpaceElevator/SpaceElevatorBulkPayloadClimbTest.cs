using Game.Block.Interface.State;
using NUnit.Framework;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    // 1便3個の積荷で、昇降機が1便ぶんずつ運ぶことを確かめる
    // Verify the climber carries one launch worth at a time with a three-per-launch payload
    public class SpaceElevatorBulkPayloadClimbTest
    {
        [Test]
        public void 一便に満たない積荷では昇らず入口に残る()
        {
            var elevatorBase = SetupConnected(100f, ForUnitTestItemId.TestElevatorBulkPayload);
            Inventory(elevatorBase).SetItem(0, ForUnitTestItemId.TestElevatorBulkPayload, 2);

            RunSeconds(2.2);

            Assert.AreEqual(2, Inventory(elevatorBase).GetItem(0).Count);
            Assert.AreEqual(0, Inventory(elevatorBase).GetItem(1).Count);
            Assert.AreEqual(SpaceElevatorBaseStatus.WaitingSatellite, ReadDetail(elevatorBase).Status);
        }

        [Test]
        public void 一便ぶんをまとめて運び一便ぶんの成果が出る()
        {
            var elevatorBase = SetupConnected(100f, ForUnitTestItemId.TestElevatorBulkPayload);
            Inventory(elevatorBase).SetItem(0, ForUnitTestItemId.TestElevatorBulkPayload, 3);

            RunSeconds(2.2);

            Assert.AreEqual(0, Inventory(elevatorBase).GetItem(0).Count);
            Assert.AreEqual(ForUnitTestItemId.TestElevatorReward, Inventory(elevatorBase).GetItem(1).Id);
            Assert.AreEqual(7, Inventory(elevatorBase).GetItem(1).Count);
        }

        [Test]
        public void 端数は入口に残り二便ぶんの成果が出る()
        {
            var elevatorBase = SetupConnected(100f, ForUnitTestItemId.TestElevatorBulkPayload);
            Inventory(elevatorBase).SetItem(0, ForUnitTestItemId.TestElevatorBulkPayload, 7);

            RunSeconds(4.6);

            Assert.AreEqual(1, Inventory(elevatorBase).GetItem(0).Count);
            Assert.AreEqual(14, Inventory(elevatorBase).GetItem(1).Count);
        }
    }
}
