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
