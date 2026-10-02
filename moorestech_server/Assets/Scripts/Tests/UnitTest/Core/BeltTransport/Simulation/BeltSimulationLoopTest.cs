using Core.BeltTransport;
using NUnit.Framework;
using static Tests.UnitTest.Core.BeltTransport.BeltConveyorSegmentTestUtil;
using static Tests.UnitTest.Core.BeltTransport.Simulation.BeltSimulationTestUtil;

namespace Tests.UnitTest.Core.BeltTransport.Simulation
{
    // 通常segmentの輪をTickだけで検証する。期待値は手計算
    // Loops of normal segments, verified through Tick only. Expected values are hand-computed
    public class BeltSimulationLoopTest
    {
        [Test]
        public void 自己接続した1本の通常segmentはアイテムを周回させる()
        {
            // 周長4W=1024、速度64。tick1: 空き1024-(512+W)=256に進入距離64で入り1024-64=960、#2は512-64=448
            // Circumference 4W=1024, speed 64. Tick1: entry 64 fits the offer 1024-(512+W)=256, landing at 960; #2 at 448
            var loop = CreateNormal(4, 64);
            loop.ConnectTo(loop, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            Restore(loop, (1, 0), (2, 2 * W));
            var simulation = new BeltSimulation(new[] { loop });

            simulation.Tick();
            AssertItems(loop, (2, 448), (1, 960));

            // tick2: 空きは1024-(960+W)=-192だが出口を越えるアイテムはないので両方64進む
            // Tick2: the offer is 1024-(960+W)=-192, but nothing crosses the exit, so both move 64
            simulation.Tick();
            AssertItems(loop, (2, 384), (1, 896));

            // 1周1024/64=16tickで元の配置へ戻る
            // One lap takes 1024/64=16 ticks and returns to the initial layout
            TickTimes(simulation, 14);
            AssertItems(loop, (1, 0), (2, 2 * W));
        }

        [Test]
        public void 完全に満杯の輪は空き0で動かない()
        {
            var loop = CreateNormal(2, 64);
            loop.ConnectTo(loop, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            RestorePacked(loop, 1, 2);
            var simulation = new BeltSimulation(new[] { loop });

            TickTimes(simulation, 5);
            AssertItems(loop, (1, 0), (2, W));
        }

        [Test]
        public void 速度の違う2本の通常segmentの輪は境界ごとに搬入先の速度へ切り替わる()
        {
            // A(2マス,速度64)→B(2マス,速度32)→A。tick1: 100→36。tick2: 進入距離64-36=28でBの512-28=484へ
            // A(2 cells, speed 64) → B(2 cells, speed 32) → A. Tick1: 100→36. Tick2: entry 64-36=28 lands at 512-28=484 in B
            var a = CreateNormal(2, 64);
            var b = CreateNormal(2, 32);
            a.ConnectTo(b, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            b.ConnectTo(a, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            Restore(a, (1, 100));
            var simulation = new BeltSimulation(new[] { a, b });

            simulation.Tick();
            AssertItems(a, (1, 36));
            simulation.Tick();
            AssertItems(a);
            AssertItems(b, (1, 484));

            // Bでは速度32。tick17で484-15*32=4、tick18で進入距離32-4=28でAの484へ、tick19はAの速度64で420
            // In B the speed is 32. Tick17: 484-15*32=4; tick18: entry 32-4=28 lands at 484 in A; tick19: A's speed 64 gives 420
            simulation.Tick();
            AssertItems(b, (1, 452));
            TickTimes(simulation, 14);
            AssertItems(b, (1, 4));
            simulation.Tick();
            AssertItems(b);
            AssertItems(a, (1, 484));
            simulation.Tick();
            AssertItems(a, (1, 420));
        }
    }
}
