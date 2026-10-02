using Core.BeltTransport;
using NUnit.Framework;
using Tests.UnitTest.Core.BeltTransport.Connection;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Simulation.BeltSimulationTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Simulation.EntryDirection.BeltEntryDirectionTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Simulation.EntryDirection
{
    // 合流・分岐への搬入、bufferへの回収、bufferからの搬出で進入方向がいつ書かれるかをTickで検証する
    // Verifies through Tick when the entry direction is written on merge/branch input, buffer collection and buffer output
    public class BeltEntryDirectionBufferTest
    {
        [Test]
        public void 合流へ入ると接続の値になりbuffer回収では変わらずbuffer搬出で次の接続の値になる()
        {
            // 搬出先outは出口に#99が詰まり空き0。tick3で#10がbufferへ回収されたまま留まる
            // The output belt is blocked by #99 at its exit with zero offer, so #10 stays in the buffer after tick 3
            var merge = CreateMerge(64, BeltDirection.Front);
            var left = CreateNormal(4, 128);
            var output = CreateNormal(1, 64);
            var sink = new FakeBeltReceiver(W, false);
            left.ConnectTo(merge, BeltDirection.Right, BeltEntryDirections.FromBelow(BeltDirection.Left));
            merge.Buffer.ConnectTo(output, BeltDirection.Front, BeltEntryDirections.FromAbove(BeltDirection.Back));
            output.ConnectTo(sink, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            Restore(left, (10, 0));
            Restore(output, (99, 0));
            var simulation = new BeltSimulation(new BeltConveyorSegment[] { merge, left, output });

            simulation.Tick();
            AssertItems(merge, (10, 128));
            AssertEntries(merge, (10, BeltEntryDirection.FromLeftBelow));

            TickTimes(simulation, 2);
            AssertItems(merge);
            AssertBufferEntry(merge.Buffer, 10, BeltEntryDirection.FromLeftBelow);

            // 機械が#99を受け取ると、次tickの段階3で#10がoutへ入りbufferの接続の値になる
            // Once the machine takes #99, #10 enters out in the next tick's stage 3 with the buffer connection's value
            sink.SetAccepts(true);
            TickTimes(simulation, 2);
            Assert.AreEqual(-1, BufferSerial(merge.Buffer));
            AssertEntries(output, (10, BeltEntryDirection.FromBackAbove));
            Assert.AreEqual(99, Serial(sink.ReceivedItems[0]));
        }

        [Test]
        public void 分岐bufferは搬出方向ごとの進入方向を書く()
        {
            // 前向き分岐は(Front,Left,Right)の順に1個ずつ回す。分岐への搬入も接続の値になり回収では変わらない
            // A front-facing branch rotates one by one through (Front,Left,Right). Input into it takes the connection's value, kept on collection
            var input = CreateNormal(4, 128);
            var branch = CreateBranch(1, 128, BeltPriority.InitializeFromDirection, BeltDirection.Front);
            var front = new FakeBeltReceiver(W, true);
            var left = new FakeBeltReceiver(W, true);
            var right = new FakeBeltReceiver(W, true);
            input.ConnectTo(branch, BeltDirection.Front, BeltEntryDirections.FromAbove(BeltDirection.Back));
            branch.Buffer.ConnectTo(front, BeltDirection.Front, BeltEntryDirections.FromBelow(BeltDirection.Back));
            branch.Buffer.ConnectTo(left, BeltDirection.Left, BeltEntryDirections.FromAbove(BeltDirection.Right));
            branch.Buffer.ConnectTo(right, BeltDirection.Right, BeltEntryDirections.Level(BeltDirection.Left));
            RestorePacked(input, 1, 3);
            var simulation = new BeltSimulation(new BeltConveyorSegment[] { input, branch });

            simulation.Tick();
            AssertItems(branch, (1, 128));
            AssertEntries(branch, (1, BeltEntryDirection.FromBackAbove));

            // 2tickに1個ずつ前・左・右へ渡る
            // One item per two ticks goes front, left and right in turn
            TickTimes(simulation, 5);
            CollectionAssert.AreEqual(new long[] { 1 }, Serials(front.ReceivedItems));
            CollectionAssert.AreEqual(new long[] { 2 }, Serials(left.ReceivedItems));
            CollectionAssert.AreEqual(new long[] { 3 }, Serials(right.ReceivedItems));
            Assert.AreEqual(BeltEntryDirection.FromBackBelow, front.ReceivedItems[0].EntryDirection);
            Assert.AreEqual(BeltEntryDirection.FromRightAbove, left.ReceivedItems[0].EntryDirection);
            Assert.AreEqual(BeltEntryDirection.FromLeft, right.ReceivedItems[0].EntryDirection);
        }
    }
}
