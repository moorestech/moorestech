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
            // 周長4W、速度W/4。tick1: 空き4W-(2W+W)=Wに進入距離W/4で入り4W-W/4、#2は2W-W/4
            // Circumference 4W, speed W/4. Tick1: entry W/4 fits the offer 4W-(2W+W)=W, landing at 4W-W/4; #2 at 2W-W/4
            var loop = CreateNormal(4, W / 4);
            loop.ConnectTo(loop, BeltDirection.Front, BeltEntryDirections.Level(BeltDirection.Back));
            Restore(loop, (1, 0), (2, 2 * W));
            var simulation = new BeltSimulation(new[] { loop });

            simulation.Tick();
            AssertItems(loop, (2, 2 * W - W / 4), (1, 4 * W - W / 4));

            // tick2: 空きは4W-(4W-W/4+W)=-3W/4だが出口を越えるアイテムはないので両方W/4進む
            // Tick2: the offer is 4W-(4W-W/4+W)=-3W/4, but nothing crosses the exit, so both move W/4
            simulation.Tick();
            AssertItems(loop, (2, 2 * W - W / 2), (1, 4 * W - W / 2));

            // 1周4W/(W/4)=16tickで元の配置へ戻る
            // One lap takes 4W/(W/4)=16 ticks and returns to the initial layout
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
            // A(2マス,速度64)→B(2マス,速度32)→A。tick1: 100→36。tick2: 進入距離64-36=28でBの2W-28へ
            // A(2 cells, speed 64) → B(2 cells, speed 32) → A. Tick1: 100→36. Tick2: entry 64-36=28 lands at 2W-28 in B
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
            AssertItems(b, (1, 2 * W - 28));

            // Bでは速度32。tick23で2W-28-21*32=20、tick24で進入距離32-20=12でAの2W-12へ、tick25はAの速度64で2W-76
            // In B the speed is 32. Tick23: 2W-28-21*32=20; tick24: entry 32-20=12 lands at 2W-12 in A; tick25: A's speed 64 gives 2W-76
            simulation.Tick();
            AssertItems(b, (1, 2 * W - 28 - 32));
            TickTimes(simulation, 20);
            AssertItems(b, (1, 2 * W - 28 - 21 * 32));
            simulation.Tick();
            AssertItems(b);
            AssertItems(a, (1, 2 * W - 12));
            simulation.Tick();
            AssertItems(a, (1, 2 * W - 76));
        }
    }
}
