using Core.BeltTransport;
using NUnit.Framework;
using Tests.UnitTest.Core.BeltTransport.Connection;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Simulation.BeltSimulationTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Simulation
{
    // README「1 tickの順序」の段階間の規則をTickだけで検証する
    // Rules between stages from README "order of one tick", verified through Tick only
    public class BeltSimulationStageOrderTest
    {
        private const int Init = BeltPriority.InitializeFromDirection;

        [Test]
        public void 段階1で回収したアイテムは同じtickの段階3で搬出される()
        {
            // 先頭10は速度64で出口に止まって回収され、進入距離min(64,W)=64で機械へ出る
            // The head at 10 parks at the exit with speed 64, is collected and leaves with entry min(64,W)=64
            var branch = CreateBranch(1, 64, Init, BeltDirection.Front);
            var front = new FakeBeltReceiver(W, true);
            var left = new FakeBeltReceiver(W, true);
            branch.Buffer.ConnectTo(front, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            branch.Buffer.ConnectTo(left, BeltDirection.Left, BeltEntryDirections.Level(BeltDirection.Right));
            Restore(branch, (1, 10));
            var simulation = new BeltSimulation(new BeltConveyorSegment[] { branch });

            simulation.Tick();
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(-1, BufferSerial(branch.Buffer));
            CollectionAssert.AreEqual(new long[] { 1 }, Serials(front.ReceivedItems));
            Assert.AreEqual(64, front.ReceiveAttempts[0].Length);
        }

        [Test]
        public void 段階1で満杯だったbufferが段階3で空いても次の回収は次tick()
        {
            // 左は拒否するので、両アイテムとも前へ出る
            // Left refuses, so both items go to the front
            var branch = CreateBranch(2, 64, Init, BeltDirection.Front);
            var front = new FakeBeltReceiver(W, true);
            var left = new FakeBeltReceiver(W, false);
            branch.Buffer.ConnectTo(front, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            branch.Buffer.ConnectTo(left, BeltDirection.Left, BeltEntryDirections.Level(BeltDirection.Right));
            branch.Buffer.RestoreItem(MakeItem(1));
            Restore(branch, (2, 0));
            var simulation = new BeltSimulation(new BeltConveyorSegment[] { branch });

            simulation.Tick();
            AssertItems(branch, (2, 0));
            Assert.AreEqual(-1, BufferSerial(branch.Buffer));
            CollectionAssert.AreEqual(new long[] { 1 }, Serials(front.ReceivedItems));

            simulation.Tick();
            AssertItems(branch);
            Assert.AreEqual(-1, BufferSerial(branch.Buffer));
            CollectionAssert.AreEqual(new long[] { 1, 2 }, Serials(front.ReceivedItems));
        }

        [Test]
        public void 段階3でbufferから受け取った通常segmentのアイテムは同じtickの段階4でも進む()
        {
            // 進入距離min(64,2W)=64で2W-64=448に置かれ、段階4で速度50だけ進み398
            // Placed at 2W-64=448 with entry min(64,2W)=64, then moves by speed 50 in stage 4 to 398
            var branch = CreateBranch(1, 64, Init, BeltDirection.Front);
            var normal = CreateNormal(2, 50);
            var left = new FakeBeltReceiver(W, false);
            branch.Buffer.ConnectTo(normal, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            branch.Buffer.ConnectTo(left, BeltDirection.Left, BeltEntryDirections.Level(BeltDirection.Right));
            branch.Buffer.RestoreItem(MakeItem(1));
            var simulation = new BeltSimulation(new BeltConveyorSegment[] { branch, normal });

            simulation.Tick();
            Assert.AreEqual(-1, BufferSerial(branch.Buffer));
            AssertItems(normal, (1, 2 * W - 64 - 50));
        }

        [Test]
        public void 機械がtick境界で入れた最後尾が入口からはみ出す間は空きが負で送れない()
        {
            // 機械が進入距離100で入れると3W-100=668。各tickの段階4開始時の空きは3W-(d+W)で-156→-28→100
            // A machine inserts with entry 100 at 3W-100=668. The offer 3W-(d+W) at each stage-4 start goes -156→-28→100
            var source = CreateNormal(1, 64);
            var target = CreateNormal(3, 128);
            source.ConnectTo(target, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            Restore(source, (1, 0));
            Assert.IsTrue(target.TryReceive(BeltDirection.Back, 100, MakeItem(2)));
            var simulation = new BeltSimulation(new BeltConveyorSegment[] { source, target });

            simulation.Tick();
            AssertItems(source, (1, 0));
            AssertItems(target, (2, 540));
            simulation.Tick();
            AssertItems(source, (1, 0));
            AssertItems(target, (2, 412));

            // 空き100に進入距離64が収まり、前進後の284の後ろ3W-64=704へ置かれる
            // The entry 64 fits the offer 100 and lands at 3W-64=704 behind the advanced 284
            simulation.Tick();
            AssertItems(source);
            AssertItems(target, (2, 284), (1, 3 * W - 64));
        }
    }
}
