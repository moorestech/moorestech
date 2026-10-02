using Core.BeltTransport;
using NUnit.Framework;
using Tests.UnitTest.Core.BeltTransport.Connection;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Simulation.BeltSimulationTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Simulation
{
    // 分岐bufferが合流の搬入元になる構成で、段階2の予約と段階3の搬出の関係をTickだけで検証する
    // 分岐のFrontは合流のBack搬入、Leftは機械。合流のLeft搬入は通常segment。合流の初期搬入順は(Back,Left,Right)
    // A branch buffer feeding a merge: the relation of stage-2 reservation and stage-3 output, verified through Tick only
    // Branch Front goes into the merge's Back input, Left into a machine. The merge's Left input is a normal segment. Merge order starts (Back,Left,Right)
    public class BeltSimulationBufferMergeTest
    {
        private BeltConveyorSegment _branch;
        private BeltConveyorSegment _merge;
        private BeltConveyorSegment _belt;
        private FakeBeltReceiver _machine;
        private FakeBeltReceiver _sink;
        private BeltSimulation _simulation;

        [Test]
        public void 最優先出力が合流でない分岐bufferは選ばれず合流は次の搬入元を予約する()
        {
            Build(Order(BeltDirection.Left, BeltDirection.Front, BeltDirection.Right), true);

            // 分岐の最優先はLeft(機械)なのでBackは候補外。Left搬入の通常segmentを予約し、分岐は機械へ出す
            // The branch's top is Left (machine), so Back is not offered. The merge reserves the Left belt; the branch outputs to the machine
            _simulation.Tick();
            CollectionAssert.AreEqual(new long[] { 1 }, Serials(_machine.ReceivedItems));
            AssertItems(_merge, (10, 128));
            AssertItems(_belt, (11, 128));
            Assert.AreEqual(Order(BeltDirection.Back, BeltDirection.Right, BeltDirection.Left), _merge.PriorityOrder);
            Assert.AreEqual(Order(BeltDirection.Front, BeltDirection.Right, BeltDirection.Left), _branch.PriorityOrder);

            // tick2: 分岐の最優先が合流になり、そのtickの予約でBackが選ばれて#2は合流へ入る（前tickの予約は使わない）
            // Tick2: the branch's top is now the merge, this tick's reservation picks Back and #2 enters the merge (not last tick's reservation)
            _branch.Buffer.RestoreItem(MakeItem(2));
            _simulation.Tick();
            AssertItems(_merge, (2, 128));
            CollectionAssert.AreEqual(new long[] { 1 }, Serials(_machine.ReceivedItems));
            CollectionAssert.AreEqual(new long[] { 10 }, Serials(_sink.ReceivedItems));
            Assert.AreEqual(Order(BeltDirection.Right, BeltDirection.Left, BeltDirection.Back), _merge.PriorityOrder);
            Assert.AreEqual(Order(BeltDirection.Right, BeltDirection.Left, BeltDirection.Front), _branch.PriorityOrder);
        }

        [Test]
        public void 予約した方向は同じtick内で選び直さず空の合流でも予約外のbufferは搬入できない()
        {
            Build(Order(BeltDirection.Left, BeltDirection.Front, BeltDirection.Right), false);

            // tick1: 合流はLeftを予約。分岐は機械に拒否され、Frontの合流は空いていても予約外なので空き0で残る
            // Tick1: the merge reserves Left. The machine refuses the branch, and the empty merge offers 0 to the unreserved Front
            _simulation.Tick();
            Assert.AreEqual(1, BufferSerial(_branch.Buffer));
            AssertItems(_merge, (10, 128));
            Assert.AreEqual(Order(BeltDirection.Left, BeltDirection.Front, BeltDirection.Right), _branch.PriorityOrder);

            // tick2: 合流は空になるが、通常segmentの後続は128で搬出不可、分岐の最優先は依然Leftなので予約なし
            // Tick2: the merge empties, but the belt's follower at 128 cannot output and the branch's top is still Left, so nothing is reserved
            _simulation.Tick();
            AssertItems(_merge);
            Assert.AreEqual(1, BufferSerial(_branch.Buffer));

            // 機械が拒否し続ける間、分岐の優先方向は繰り下がらず合流へは送られない（README許容の偏り）
            // While the machine keeps refusing, the branch's priority does not fall through and nothing goes to the merge (bias accepted by README)
            TickTimes(_simulation, 4);
            Assert.AreEqual(1, BufferSerial(_branch.Buffer));
            CollectionAssert.AreEqual(new long[] { 10, 11 }, Serials(_sink.ReceivedItems));
            AssertItems(_merge);

            _machine.SetAccepts(true);
            _simulation.Tick();
            CollectionAssert.AreEqual(new long[] { 1 }, Serials(_machine.ReceivedItems));
        }

        [Test]
        public void 最優先出力が合流の分岐bufferを予約するとそのtickは通常segmentを受けない()
        {
            Build(Order(BeltDirection.Front, BeltDirection.Left, BeltDirection.Right), true);

            // 合流はBack(分岐)を予約し、進入距離min(128,W)=128で隙間128へ受ける。通常segmentは空き0で出口に止まる
            // The merge reserves Back (branch) and takes it with entry min(128,W)=128 at gap 128. The belt gets offer 0 and parks at the exit
            _simulation.Tick();
            AssertItems(_merge, (1, 128));
            AssertItems(_belt, (10, 0), (11, W));
            Assert.AreEqual(0, _machine.ReceiveAttempts.Count);
            Assert.AreEqual(Order(BeltDirection.Left, BeltDirection.Right, BeltDirection.Back), _merge.PriorityOrder);
            Assert.AreEqual(Order(BeltDirection.Left, BeltDirection.Right, BeltDirection.Front), _branch.PriorityOrder);
        }

        [Test]
        public void 速度0の分岐bufferは段階2で搬出不可と答え合流は次の搬入元を予約する()
        {
            Build(Order(BeltDirection.Front, BeltDirection.Left, BeltDirection.Right), true);
            _branch.SetSpeed(0);

            // 分岐の最優先は合流だが速度0なので候補外。合流はLeftの通常segmentを予約し、#10が進入距離128で入る
            // The branch's top is the merge, but at speed 0 it is not offered. The merge reserves the Left belt and #10 enters with entry 128
            _simulation.Tick();
            AssertItems(_merge, (10, 128));
            AssertItems(_belt, (11, 128));
            Assert.AreEqual(1, BufferSerial(_branch.Buffer));
            Assert.AreEqual(0, _machine.ReceiveAttempts.Count);
            Assert.AreEqual(Order(BeltDirection.Back, BeltDirection.Right, BeltDirection.Left), _merge.PriorityOrder);
            Assert.AreEqual(Order(BeltDirection.Front, BeltDirection.Left, BeltDirection.Right), _branch.PriorityOrder);
        }

        // 全segmentの速度128。分岐bufferに#1、通常segmentに#10,#11を密着して置く
        // All speeds 128. #1 in the branch buffer, #10 and #11 packed on the belt
        private void Build(int branchOrder, bool machineAccepts)
        {
            _branch = CreateBranch(1, 128, branchOrder, BeltDirection.Front);
            _merge = CreateMerge(128, BeltDirection.Front);
            _belt = CreateNormal(2, 128);
            _machine = new FakeBeltReceiver(W, machineAccepts);
            _sink = new FakeBeltReceiver(W, true);
            _branch.Buffer.ConnectTo(_merge, BeltDirection.Front);
            _branch.Buffer.ConnectTo(_machine, BeltDirection.Left);
            _belt.ConnectTo(_merge, BeltDirection.Right);
            _merge.Buffer.ConnectTo(_sink, BeltDirection.Front);
            _branch.Buffer.RestoreItem(MakeItem(1));
            RestorePacked(_belt, 10, 2);
            _simulation = new BeltSimulation(new[] { _branch, _merge, _belt });
        }
    }
}
