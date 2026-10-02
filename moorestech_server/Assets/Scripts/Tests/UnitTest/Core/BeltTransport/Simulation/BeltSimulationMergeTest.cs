using Core.BeltTransport;
using NUnit.Framework;
using Tests.UnitTest.Core.BeltTransport.Connection;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Simulation.BeltSimulationTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Simulation
{
    // 合流の予約・搬入優先順の回転・buffer搬出をTickだけで検証する
    // 前向き合流の初期搬入順は(Back,Left,Right)。搬入元は速度128で密着して並べ、出口で待つ間は毎tick搬出可能
    // Merge reservation, input order rotation and buffer output, verified through Tick only
    // A front-facing merge starts with (Back,Left,Right). Inputs are packed at speed 128 and stay ready while waiting at the exit
    public class BeltSimulationMergeTest
    {
        [Test]
        public void 二方向合流は空のときだけ予約し成功方向を末尾へ回す()
        {
            // 合流速度64: 進入距離128で隙間128に入り、2tick後の段階1で回収され空になる。よって奇数tickにだけ搬入する
            // Merge speed 64: an item enters with gap 128 and is collected two ticks later in stage 1, so input happens on odd ticks only
            var merge = CreateMerge(64, BeltDirection.Front);
            var back = CreateNormal(4, 128);
            var left = CreateNormal(4, 128);
            var sink = new FakeBeltReceiver(W, true);
            back.ConnectTo(merge, BeltDirection.Front);
            left.ConnectTo(merge, BeltDirection.Right);
            merge.Buffer.ConnectTo(sink, BeltDirection.Front);
            RestorePacked(back, 10, 4);
            RestorePacked(left, 20, 4);
            var simulation = new BeltSimulation(new[] { merge, back, left });

            simulation.Tick();
            AssertItems(merge, (10, 128));
            Assert.AreEqual(Order(BeltDirection.Left, BeltDirection.Right, BeltDirection.Back), merge.PriorityOrder);

            // 合流が空でないtickは予約しない。左は出口で待ち、後は後続が128→0へ進む
            // No reservation while the merge is occupied. Left waits at the exit; back's follower moves 128→0
            simulation.Tick();
            AssertItems(merge, (10, 64));
            AssertItems(left, (20, 0), (21, W), (22, 2 * W), (23, 3 * W));
            AssertItems(back, (11, 0), (12, W), (13, 2 * W));
            Assert.AreEqual(0, sink.ReceiveAttempts.Count);

            // tick3: 回収→Left予約→#10搬出→#20搬入で(Right,Back,Left)。tick5: Rightは未接続で飛ばしBackを予約
            // Tick3: collect, reserve Left, output #10, input #20 giving (Right,Back,Left). Tick5: Right is unconnected so Back is reserved
            simulation.Tick();
            AssertItems(merge, (20, 128));
            Assert.AreEqual(Order(BeltDirection.Right, BeltDirection.Back, BeltDirection.Left), merge.PriorityOrder);
            TickTimes(simulation, 6);
            CollectionAssert.AreEqual(new long[] { 10, 20, 11, 21 }, Serials(sink.ReceivedItems));
            AssertItems(merge, (12, 128));
            Assert.AreEqual(Order(BeltDirection.Right, BeltDirection.Left, BeltDirection.Back), merge.PriorityOrder);

            // 合流bufferの搬出先は1つだけで、常に前の機械へ64ずつ渡す
            // The merge buffer has a single output and always hands over to the front machine with 64
            foreach (var attempt in sink.ReceiveAttempts)
            {
                Assert.AreEqual(BeltDirection.Back, attempt.Direction);
                Assert.AreEqual(64, attempt.Length);
            }
        }

        [Test]
        public void 三方向合流で搬出できない方向は相対順を保ち先頭に残る()
        {
            // 合流速度128なら毎tick段階1で空になり毎tick予約する。Leftは最初は空で搬出できない
            // At merge speed 128 it empties every stage 1 and reserves every tick. Left starts empty and cannot output
            var merge = CreateMerge(128, BeltDirection.Front);
            var back = CreateNormal(4, 128);
            var left = CreateNormal(4, 128);
            var right = CreateNormal(4, 128);
            var sink = new FakeBeltReceiver(W, true);
            back.ConnectTo(merge, BeltDirection.Front);
            left.ConnectTo(merge, BeltDirection.Right);
            right.ConnectTo(merge, BeltDirection.Left);
            merge.Buffer.ConnectTo(sink, BeltDirection.Front);
            RestorePacked(back, 10, 4);
            RestorePacked(right, 20, 4);
            var simulation = new BeltSimulation(new[] { merge, back, left, right });

            // (B,L,R)でBack成功→(L,R,B)。Left不可でRight成功→(L,B,R)。Left不可でBack成功→(L,R,B)
            // (B,L,R) Back succeeds → (L,R,B). Left unavailable, Right succeeds → (L,B,R). Left unavailable, Back succeeds → (L,R,B)
            simulation.Tick();
            AssertItems(merge, (10, 128));
            Assert.AreEqual(Order(BeltDirection.Left, BeltDirection.Right, BeltDirection.Back), merge.PriorityOrder);
            simulation.Tick();
            AssertItems(merge, (20, 128));
            Assert.AreEqual(Order(BeltDirection.Left, BeltDirection.Back, BeltDirection.Right), merge.PriorityOrder);
            simulation.Tick();
            AssertItems(merge, (11, 128));
            Assert.AreEqual(Order(BeltDirection.Left, BeltDirection.Right, BeltDirection.Back), merge.PriorityOrder);

            // tick境界でLeftの出口へアイテムを置くと、Rightも搬出可能だが先頭のLeftが選ばれる
            // Placing an item at Left's exit at a tick boundary: Right is also ready, but Left at the front wins
            Restore(left, (30, 0));
            simulation.Tick();
            AssertItems(merge, (30, 128));
            AssertItems(right, (21, 0), (22, W), (23, 2 * W));
            Assert.AreEqual(Order(BeltDirection.Right, BeltDirection.Back, BeltDirection.Left), merge.PriorityOrder);
            simulation.Tick();
            AssertItems(merge, (21, 128));
            CollectionAssert.AreEqual(new long[] { 10, 20, 11, 30 }, Serials(sink.ReceivedItems));
        }
    }
}
