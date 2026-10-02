using Core.BeltTransport;
using NUnit.Framework;
using Tests.UnitTest.Core.BeltTransport.Connection;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Simulation.BeltSimulationTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Simulation
{
    // 分岐bufferの搬出優先順の回転と、segment・機械への搬出をTickだけで検証する
    // 前向き分岐の初期搬出順は(Front,Left,Right)
    // Branch buffer output order rotation and output into segments and machines, verified through Tick only
    // A front-facing branch starts with (Front,Left,Right)
    public class BeltSimulationBranchTest
    {
        [Test]
        public void 三方向分岐は成功方向を末尾へ回し詰まった方向は順序を変えない()
        {
            var branch = CreateBranch(1, 128, BeltPriority.InitializeFromDirection, BeltDirection.Front);
            var front = new FakeBeltReceiver(W, true);
            var left = new FakeBeltReceiver(W, true);
            var right = new FakeBeltReceiver(W, true);
            branch.Buffer.ConnectTo(front, BeltDirection.Front);
            branch.Buffer.ConnectTo(left, BeltDirection.Left);
            branch.Buffer.ConnectTo(right, BeltDirection.Right);
            var simulation = new BeltSimulation(new[] { branch });

            // (F,L,R)でFront→(L,R,F)、Left→(R,F,L)
            // (F,L,R): Front → (L,R,F), Left → (R,F,L)
            TickWithBufferItem(1);
            Assert.AreEqual(Order(BeltDirection.Left, BeltDirection.Right, BeltDirection.Front), branch.PriorityOrder);
            TickWithBufferItem(2);
            Assert.AreEqual(Order(BeltDirection.Right, BeltDirection.Front, BeltDirection.Left), branch.PriorityOrder);

            // Rightは空き0で問い合わせず飛ばし、Leftは受け入れ拒否。Frontが成功し(R,L,F)、末尾成功の次tickは順序不変
            // Right is skipped by a zero offer, Left refuses. Front succeeds giving (R,L,F); a tail success next tick keeps it
            right.SetOffer(0);
            left.SetAccepts(false);
            TickWithBufferItem(3);
            Assert.AreEqual(Order(BeltDirection.Right, BeltDirection.Left, BeltDirection.Front), branch.PriorityOrder);
            TickWithBufferItem(4);
            Assert.AreEqual(Order(BeltDirection.Right, BeltDirection.Left, BeltDirection.Front), branch.PriorityOrder);

            // 解除すると先頭に残ったRightが選ばれる
            // Once released, Right kept at the front is chosen
            right.SetOffer(W);
            left.SetAccepts(true);
            TickWithBufferItem(5);
            Assert.AreEqual(Order(BeltDirection.Left, BeltDirection.Front, BeltDirection.Right), branch.PriorityOrder);

            CollectionAssert.AreEqual(new long[] { 1, 3, 4 }, Serials(front.ReceivedItems));
            CollectionAssert.AreEqual(new long[] { 2 }, Serials(left.ReceivedItems));
            CollectionAssert.AreEqual(new long[] { 5 }, Serials(right.ReceivedItems));
            Assert.AreEqual(2, left.ReceiveAttempts.Count);
            Assert.AreEqual(1, right.ReceiveAttempts.Count);
            Assert.AreEqual(128, front.ReceiveAttempts[0].Length);

            #region Internal

            // tick境界で空のbufferへ1個置いてから1tick進める
            // Put one item into the empty buffer at the tick boundary, then run one tick
            void TickWithBufferItem(long serial)
            {
                branch.Buffer.RestoreItem(MakeItem(serial));
                simulation.Tick();
                Assert.AreEqual(-1, BufferSerial(branch.Buffer));
            }

            #endregion
        }

        [Test]
        public void 二方向分岐は通常segmentと機械へ交互に搬出する()
        {
            // 搬入元は128で密着4個。分岐は隙間128で受け、次tickの段階1で回収・段階3で搬出するので2tickに1個
            // The input has four packed items at 128. The branch receives with gap 128, collects next stage 1 and outputs in stage 3: one per two ticks
            var input = CreateNormal(4, 128);
            var branch = CreateBranch(1, 128, BeltPriority.InitializeFromDirection, BeltDirection.Front);
            var belt = CreateNormal(2, 64);
            var machine = new FakeBeltReceiver(W, true);
            input.ConnectTo(branch, BeltDirection.Front);
            branch.Buffer.ConnectTo(belt, BeltDirection.Front);
            branch.Buffer.ConnectTo(machine, BeltDirection.Right);
            RestorePacked(input, 1, 4);
            var simulation = new BeltSimulation(new[] { input, branch, belt });

            simulation.Tick();
            AssertItems(branch, (1, 128));

            // tick2: #1をFrontへ進入距離128で2W-128=384、同tickに64進み320。順序(L,R,F)
            // Tick2: #1 goes Front with entry 128 to 2W-128=384, then moves 64 to 320 the same tick. Order (L,R,F)
            simulation.Tick();
            AssertItems(belt, (1, 320));
            Assert.AreEqual(Order(BeltDirection.Left, BeltDirection.Right, BeltDirection.Front), branch.PriorityOrder);

            // tick4: Leftは未接続で飛ばしRightの機械へ→(L,F,R)
            // Tick4: Left is unconnected and skipped, so the Right machine gets #2 → (L,F,R)
            TickTimes(simulation, 2);
            AssertItems(belt, (1, 192));
            CollectionAssert.AreEqual(new long[] { 2 }, Serials(machine.ReceivedItems));
            Assert.AreEqual(Order(BeltDirection.Left, BeltDirection.Front, BeltDirection.Right), branch.PriorityOrder);

            // tick6: #3は空き2W-(128+W)=128へ進入距離128で入り384→320、#1は64。tick8: #1は出口で止まり#3が密着
            // Tick6: #3 enters the offer 2W-(128+W)=128 with 128, 384→320, #1 at 64. Tick8: #1 parks at the exit and #3 packs behind
            TickTimes(simulation, 2);
            AssertItems(belt, (1, 64), (3, 320));
            TickTimes(simulation, 2);
            AssertItems(belt, (1, 0), (3, W));
            AssertItems(input);
            AssertItems(branch);
            CollectionAssert.AreEqual(new long[] { 2, 4 }, Serials(machine.ReceivedItems));
        }
    }
}
