using Core.BeltTransport;
using NUnit.Framework;
using Tests.UnitTest.Core.BeltTransport.Connection;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Simulation.BeltSimulationTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Simulation.EntryDirection.BeltEntryDirectionTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Simulation.EntryDirection
{
    // 通常segmentから外へ渡るときだけ接続の進入方向が書かれ、segment内では変わらないことをTickで検証する
    // MakeItemの進入方向はFromBack。接続には高さ違いの値を使い、水平方向から導出していないことを示す
    // Verifies through Tick that a normal segment writes the connection's entry direction only when an item leaves it
    // MakeItem uses FromBack. Connections use non-level values to show the value is not derived from the horizontal direction
    public class BeltEntryDirectionNormalTest
    {
        [Test]
        public void 通常segment間を渡ったアイテムだけが接続の進入方向になる()
        {
            // #1は出口まで2で搬入先の512-2=510へ。#2は1000から毎tick4ずつ進むだけでa内に留まる
            // #1 is 2 from the exit and lands at 512-2=510 in b. #2 only moves 4 per tick from 1000 and stays in a
            var a = CreateNormal(8, 4);
            var b = CreateNormal(2, 4);
            a.ConnectTo(b, BeltDirection.Front, BeltEntryDirections.FromBelow(BeltDirection.Back));
            a.RestoreItems(new[]
            {
                new BeltItemState(MakeItem(1), 2),
                new BeltItemState(MakeItemFrom(2, BeltEntryDirection.FromRightAbove), 1000)
            });
            var simulation = new BeltSimulation(new[] { a, b });

            simulation.Tick();
            AssertItems(b, (1, 510));
            AssertEntries(b, (1, BeltEntryDirection.FromBackBelow));
            AssertItems(a, (2, 996));
            AssertEntries(a, (2, BeltEntryDirection.FromRightAbove));

            // 100tick進めても、どちらもsegment内を進むだけで進入方向は変わらない
            // After 100 more ticks both only move inside their segments and keep their entry directions
            TickTimes(simulation, 100);
            AssertItems(a, (2, 596));
            AssertEntries(a, (2, BeltEntryDirection.FromRightAbove));
            AssertItems(b, (1, 110));
            AssertEntries(b, (1, BeltEntryDirection.FromBackBelow));
        }

        [Test]
        public void 通常segmentから機械へ渡すアイテムは接続の進入方向を持つ()
        {
            // 拒否された試行でも渡す値は書き換え済みだが、segmentに残るアイテムは元のまま
            // Even a refused attempt passes the rewritten value, while the item left in the segment keeps its original one
            var belt = CreateNormal(2, 64);
            var machine = new FakeBeltReceiver(W, false);
            belt.ConnectTo(machine, BeltDirection.Front, BeltEntryDirections.FromAbove(BeltDirection.Back));
            Restore(belt, (1, 0));
            var simulation = new BeltSimulation(new[] { belt });

            simulation.Tick();
            Assert.AreEqual(BeltEntryDirection.FromBackAbove, machine.ReceiveAttempts[0].Item.EntryDirection);
            AssertEntries(belt, (1, BeltEntryDirection.FromBack));

            machine.SetAccepts(true);
            simulation.Tick();
            Assert.AreEqual(1, Serial(machine.ReceivedItems[0]));
            Assert.AreEqual(BeltEntryDirection.FromBackAbove, machine.ReceivedItems[0].EntryDirection);
            AssertItems(belt);
        }

        [Test]
        public void 機械がtick境界で搬入したアイテムは渡された進入方向を保つ()
        {
            var segment = CreateNormal(2, 64);
            Assert.IsTrue(segment.TryReceive(BeltDirection.Back, 64, MakeItemFrom(1, BeltEntryDirection.FromRightBelow)));
            AssertItems(segment, (1, 448));
            AssertEntries(segment, (1, BeltEntryDirection.FromRightBelow));

            var simulation = new BeltSimulation(new[] { segment });
            simulation.Tick();
            AssertItems(segment, (1, 384));
            AssertEntries(segment, (1, BeltEntryDirection.FromRightBelow));
        }

        [Test]
        public void 自己接続の輪は継ぎ目を越えるたびに進入方向を書く()
        {
            // 周長1024・速度64。tick1で#1だけが継ぎ目を越え、16tickで1周すると#2も越えている
            // Circumference 1024, speed 64. Only #1 crosses the seam on tick 1; after a 16-tick lap #2 has crossed too
            var loop = CreateNormal(4, 64);
            loop.ConnectTo(loop, BeltDirection.Front, BeltEntryDirections.FromAbove(BeltDirection.Back));
            Restore(loop, (1, 0), (2, 2 * W));
            var simulation = new BeltSimulation(new[] { loop });

            simulation.Tick();
            AssertItems(loop, (2, 448), (1, 960));
            AssertEntries(loop, (2, BeltEntryDirection.FromBack), (1, BeltEntryDirection.FromBackAbove));
            TickTimes(simulation, 15);
            AssertItems(loop, (1, 0), (2, 2 * W));
            AssertEntries(loop, (1, BeltEntryDirection.FromBackAbove), (2, BeltEntryDirection.FromBackAbove));

            // tick境界で進入方向だけ変えて繋ぎ直すと、次に越えた#1だけが新しい値になる
            // Rewiring at a tick boundary with another entry direction gives the new value only to #1, which crosses next
            loop.ConnectTo(loop, BeltDirection.Front, BeltEntryDirections.FromBelow(BeltDirection.Back));
            simulation = new BeltSimulation(new[] { loop });
            simulation.Tick();
            AssertItems(loop, (2, 448), (1, 960));
            AssertEntries(loop, (2, BeltEntryDirection.FromBackAbove), (1, BeltEntryDirection.FromBackBelow));
        }
    }
}
