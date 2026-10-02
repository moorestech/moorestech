using Core.BeltTransport;
using NUnit.Framework;
using Tests.UnitTest.Core.BeltTransport.Connection;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Simulation.BeltSimulationTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Simulation
{
    // 通常segmentの走行・機械への搬出・通常segment間の搬送をTickだけで検証する。期待値は手計算
    // Normal segment travel, output to a machine and normal-to-normal transfer through Tick only. Expected values are hand-computed
    public class BeltSimulationNormalTest
    {
        [Test]
        public void 直線の通常segmentは速度ずつ進み出口を越えた分を進入距離として機械へ渡す()
        {
            // 100→36、次tickは64-36=28だけ出口を越えて機械へ渡る
            // 100→36, next tick it passes the exit by 64-36=28 and goes to the machine
            var belt = CreateNormal(2, 64);
            var machine = new FakeBeltReceiver(W, true);
            belt.ConnectTo(machine, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            Restore(belt, (1, 100));
            var simulation = new BeltSimulation(new[] { belt });

            simulation.Tick();
            AssertItems(belt, (1, 36));
            Assert.AreEqual(0, machine.ReceiveAttempts.Count);

            simulation.Tick();
            AssertItems(belt);
            Assert.AreEqual(1, machine.ReceiveAttempts.Count);
            Assert.AreEqual(28, machine.ReceiveAttempts[0].Length);
            Assert.AreEqual(BeltDirection.Back, machine.ReceiveAttempts[0].Direction);
            CollectionAssert.AreEqual(new long[] { 1 }, Serials(machine.ReceivedItems));
        }

        [Test]
        public void 機械が拒否すると先頭は出口で止まり後続が詰める()
        {
            // 後続の隙間は406-100-W=50。tick1で36,342、tick2で先頭が0に止まり残り28で隙間50→22、tick3で密着
            // Follower spacing is 406-100-W=50. Tick1: 36,342; tick2 the head parks at 0 and 28 left closes 50→22; tick3 packs
            var belt = CreateNormal(3, 64);
            var machine = new FakeBeltReceiver(W, false);
            belt.ConnectTo(machine, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            Restore(belt, (1, 100), (2, 406));
            var simulation = new BeltSimulation(new[] { belt });

            simulation.Tick();
            AssertItems(belt, (1, 36), (2, 342));
            simulation.Tick();
            AssertItems(belt, (1, 0), (2, W + 22));
            Assert.AreEqual(28, machine.ReceiveAttempts[0].Length);
            simulation.Tick();
            AssertItems(belt, (1, 0), (2, W));
            Assert.AreEqual(64, machine.ReceiveAttempts[1].Length);

            // 受け入れ再開で先頭が速度64の進入距離で渡り、後続は速度分進む
            // Once accepted again the head goes with entry length 64 and the follower moves by the speed
            machine.SetAccepts(true);
            simulation.Tick();
            AssertItems(belt, (2, W - 64));
            CollectionAssert.AreEqual(new long[] { 1 }, Serials(machine.ReceivedItems));
        }

        [Test]
        public void 速度の違う通常segmentへは進入距離で置かれ搬入先の速度は次tickから効く()
        {
            // 送り元速度100、先頭50なので進入距離50。搬入先の長さ768から50入った718に置き、次tickは速度30で688
            // Source speed 100, head at 50, so entry 50. It lands at 768-50=718 and moves at speed 30 to 688 next tick
            var source = CreateNormal(2, 100);
            var target = CreateNormal(3, 30);
            source.ConnectTo(target, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            Restore(source, (1, 50));
            var simulation = new BeltSimulation(new[] { source, target });

            simulation.Tick();
            AssertItems(source);
            AssertItems(target, (1, 3 * W - 50));
            simulation.Tick();
            AssertItems(target, (1, 3 * W - 50 - 30));
        }

        [Test]
        public void 同じtickの前進で空いた分は搬送に使わず境界で1tick待つ()
        {
            // 搬入先の空きはW-(5+W)=-5なので送れない。搬入先は同tickで機械へ出て空くが、送るのは次tick（W-100=156）
            // The target's offer W-(5+W)=-5 refuses. It empties into the machine the same tick, but the send waits for next tick (W-100=156)
            var source = CreateNormal(2, 100);
            var target = CreateNormal(1, 10);
            var machine = new FakeBeltReceiver(W, true);
            source.ConnectTo(target, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            target.ConnectTo(machine, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            Restore(source, (1, 0));
            Restore(target, (2, 5));
            var simulation = new BeltSimulation(new[] { source, target });

            simulation.Tick();
            AssertItems(source, (1, 0));
            AssertItems(target);
            CollectionAssert.AreEqual(new long[] { 2 }, Serials(machine.ReceivedItems));

            simulation.Tick();
            AssertItems(source);
            AssertItems(target, (1, W - 100));
        }

        [Test]
        public void 速度0の通常segmentは走行も搬出もしない()
        {
            var belt = CreateNormal(2, 0);
            var machine = new FakeBeltReceiver(W, true);
            belt.ConnectTo(machine, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            Restore(belt, (1, 0), (2, 300));
            var simulation = new BeltSimulation(new[] { belt });

            TickTimes(simulation, 3);
            AssertItems(belt, (1, 0), (2, 300));
            Assert.AreEqual(0, machine.ReceiveAttempts.Count);

            // tick境界での速度変更は次の段階0から効く。先頭は進入距離64で出て、後続は300-64へ進む
            // A speed change at a tick boundary applies from the next stage 0. Head leaves with 64, follower moves to 300-64
            belt.SetSpeed(64);
            simulation.Tick();
            AssertItems(belt, (2, 300 - 64));
            CollectionAssert.AreEqual(new long[] { 1 }, Serials(machine.ReceivedItems));
        }

        [Test]
        public void 速度0のbufferは段階1で回収できるが段階3で搬出しない()
        {
            // 出口ちょうどのアイテムは速度0でも回収される
            // An item exactly at the exit is collected even at speed 0
            var branch = CreateBranch(1, 0, BeltPriority.InitializeFromDirection, BeltDirection.Front);
            var front = new FakeBeltReceiver(W, true);
            var left = new FakeBeltReceiver(W, true);
            branch.Buffer.ConnectTo(front, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            branch.Buffer.ConnectTo(left, BeltDirection.Left, BeltEntryDirections.Level(BeltDirection.Right));
            Restore(branch, (1, 0));
            var simulation = new BeltSimulation(new[] { branch });

            TickTimes(simulation, 3);
            Assert.AreEqual(0, branch.Count);
            Assert.AreEqual(1, BufferSerial(branch.Buffer));
            Assert.AreEqual(0, front.ReceiveAttempts.Count + left.ReceiveAttempts.Count);
        }
    }
}
