using Core.Inventory;
using Core.Item;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor.Transport.Drive;
using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using Tests.Util.Throughput;
using UnityEngine;
using static Tests.Util.Throughput.BeltThroughputLayout;

namespace Tests.CombinedTest.Core.Transport.Throughput
{
    // 歯車の動力に左右される配置(ADR 0084、シナリオ案 §2 A11・A12)
    // Layouts governed by gear power (ADR 0084, scenario §2 A11, A12)
    public class BeltThroughputGearTest
    {
        private const int WoodPeriod = 20;
        private const int WoodSpeed = 18;
        private const float BaseRpm = 10f;
        private const int Length = 10;
        private static readonly ItemId FuelItem = new(1);

        // 装填済み燃料(3秒=60tick)は負荷率0.33で約180tick持ち、その後60tickで減速する。余裕を見て600tick
        // A loaded fuel item (3 s = 60 ticks) lasts about 180 ticks at load rate 0.33, then 60 ticks of deceleration; 600 ticks leaves margin
        private const int DrySettleTicks = 600;
        private const int RampMarginTicks = 5;
        private const int StopDeadlineTicks = 1500;
        private const int StoppedTicksToConfirm = 30;
        private const int StoppedTicks = 600;
        private static readonly Vector3Int WatchedCell = new(0, 0, 5);
        private static readonly Vector3Int[] Windmills = { new(1, 0, 0), new(1, 0, 4), new(1, 0, 8) };

        // A11: 燃料式風車3台(約300%)。2台の燃料が尽きても100%、3台とも尽きると回転が落ちて止まり、入れ直すと戻る
        // A11: three fuel windmills (about 300%); 100% survives two running dry, all three running dry slows then stops the belt, refuelling restores it
        [Test]
        public void A11_FuelWindmillsRunningDry([Values] BeltPlacementOrder order)
        {
            var scenario = new BeltThroughputScenario().Source(new Vector3Int(0, 0, -1));
            Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 0, 0), BlockDirection.North, Length);
            scenario.Sink(new Vector3Int(0, 0, Length)).Watch(WatchedCell);
            foreach (var windmill in Windmills) scenario.Block(ForUnitTestModBlockId.ThroughputFuelWindmill, windmill, BlockDirection.North);
            var session = scenario.Start(order);

            // ① 300%: 100%
            foreach (var windmill in Windmills) Refuel(windmill);
            session.TickMany(WindmillRampTicks() + RampMarginTicks);
            session.TickUntilSteady();
            var full = session.Measure(session.DefaultWindow);
            BeltThroughputAssert.Throughput(full.Window / WoodPeriod, full, "A11 (1) 300%");
            BeltThroughputAssert.WatchedSegmentsAtFullThroughput(full, WoodPeriod, "A11 (1) 300%");

            // ② 2台が尽きて約100%: 網の回転数は最速の風車のものなので、残る1台が回す限り100%のまま
            // ② Two run dry, about 100%: the network turns at the fastest windmill, so one fuelled windmill keeps 100%
            // 燃料を抜いても装填済みの1個を負荷率ぶんゆっくり燃やし切ってから減速する。減速に入る tick も含め、残る1台が回す限りベルトは 18 のまま(F3 の回帰: 原点 RPM は RunTick の snapshot)
            // After the fuel is removed the loaded item still burns, slowly at the load rate, before deceleration; the belt stays at 18 throughout, including the tick the others start slowing (F3 regression: origin RPM is the RunTick snapshot)
            RunDry(Windmills[1]);
            RunDry(Windmills[2]);
            for (var tick = 0; tick < DrySettleTicks; tick++)
            {
                session.TickOnce();
                Assert.AreEqual(WoodSpeed, BeltWorldTestUtil.SegmentAt(WatchedCell).Speed, $"A11 (2) speed must not dip while two windmills run dry (tick {session.Tick})");
            }
            var single = session.Measure(session.DefaultWindow);
            BeltThroughputAssert.Throughput(single.Window / WoodPeriod, single, "A11 (2) 100%");
            BeltThroughputAssert.WatchedSegmentsAtFullThroughput(single, WoodPeriod, "A11 (2) 100%");

            // ③ 300%へ戻してから3台同時に尽きる: 減速中は毎 tick の速度が回転数どおりで、搬出数は累積移動量から決まる。供給<需要で停止
            // ③ Back to 300%, then all three run dry: while slowing each tick's speed follows the RPM and the exits follow the cumulative advance; supply < demand stops the belt
            Refuel(Windmills[1]);
            Refuel(Windmills[2]);
            session.TickMany(WindmillRampTicks() + RampMarginTicks);
            foreach (var windmill in Windmills) RunDry(windmill);
            AssertSlowdownThenStop(session);

            // ④ 入れ直すと加速して100%へ戻る
            // ④ Refuelling accelerates back to 100%
            foreach (var windmill in Windmills) Refuel(windmill);
            session.TickMany(WindmillRampTicks() + RampMarginTicks);
            session.TickUntilSteady();
            var restored = session.Measure(session.DefaultWindow);
            BeltThroughputAssert.Throughput(restored.Window / WoodPeriod, restored, "A11 (4) restored");
            BeltThroughputAssert.WatchedSegmentsAtFullThroughput(restored, WoodPeriod, "A11 (4) restored");
        }

        // A12: 鉄の歯車ベルト10マス → 電気ベルト1マス → 鉄の歯車ベルト10マス。歯車網は電気1マスで切れ、30 RPM で鉄の速度 120 = 電気の速度。下流の網を止めて戻す
        // A12: ten iron gear belts -> one electric belt -> ten iron gear belts; the electric cell cuts the gear network, and 30 RPM makes iron speed 120 = electric speed. The downstream network stops and restarts
        [Test]
        public void A12_DownstreamSegmentStopAndRestart([Values] BeltPlacementOrder order)
        {
            var upstreamGenerator = new Vector3Int(1, 0, 0);
            var downstreamGenerator = new Vector3Int(1, 0, Length + 1);
            var scenario = new BeltThroughputScenario().Source(new Vector3Int(0, 0, -1));
            Line(scenario, ForUnitTestModBlockId.IronGearBelt, new Vector3Int(0, 0, 0), BlockDirection.North, Length);
            scenario.Block(ForUnitTestModBlockId.ElectricBelt, new Vector3Int(0, 0, Length), BlockDirection.North);
            var exit = Line(scenario, ForUnitTestModBlockId.IronGearBelt, new Vector3Int(0, 0, Length + 1), BlockDirection.North, Length);
            scenario.Sink(exit).Watch(new Vector3Int(0, 0, 5)).Watch(new Vector3Int(0, 0, Length + 6));
            Generator(scenario, upstreamGenerator);
            Generator(scenario, downstreamGenerator);
            var session = scenario.Start(order);
            SetRpm(upstreamGenerator, 3f * BaseRpm);
            SetRpm(downstreamGenerator, 3f * BaseRpm);

            // 等速でも通常segment同士の境界は「同じtickの前進で空いた分は使わず境界で1tick待つ」ので、満杯の下流へは1個ごとに1tick待つ。速度120・周期3は4tickに1個=720になる(ADR 0084 F4、特性値としてピン止め)
            // Even at equal speed a normal-to-normal boundary waits one tick ("space freed this tick is not used until the next"), so into a full downstream each item waits a tick; speed 120 / period 3 yields one per 4 ticks = 720 (ADR 0084 F4, pinned as a characteristic value)
            const int pinnedPerWindow = 720;
            session.TickUntilSteady();
            var running = session.Measure(session.DefaultWindow);
            Assert.AreEqual(pinnedPerWindow, running.SourceTotal, $"A12 running pinned; sources={string.Join(",", running.SourceCounts)} watched={string.Join(",", running.WatchedExits)}");
            Assert.AreEqual(running.SourceTotal - running.SinkTotal, running.OnBeltsDelta, "A12 running conservation");

            // 下流を止める: 上流は詰まるだけで個数は保存され、下流の segment からは何も出ない
            // Stop downstream: upstream merely backs up with conservation, and nothing leaves the downstream segment
            // 満杯の上流は止まった瞬間に供給も止まるので、供給の tick に揃える Measure は使わず素の tick で見る
            // A full upstream stops the source the moment downstream stops, so the stopped phase is observed with plain ticks instead of the emission-aligned Measure
            SetRpm(downstreamGenerator, 0f);
            var sourceBefore = session.SourceCounts()[0];
            var sinkBefore = session.SinkCounts()[0];
            var onBeltsBefore = session.ItemsOnBelts();
            var downstreamExitsBefore = session.WatchedExits()[1];
            session.TickMany(StoppedTicks);
            Assert.AreEqual(sourceBefore - session.SourceCounts()[0] - (session.SinkCounts()[0] - sinkBefore), session.ItemsOnBelts() - onBeltsBefore, "A12 conservation while stopped");
            Assert.AreEqual(downstreamExitsBefore, session.WatchedExits()[1], "A12 downstream emits nothing while stopped");

            SetRpm(downstreamGenerator, 3f * BaseRpm);
            session.TickUntilSteady();
            var restored = session.Measure(session.DefaultWindow);
            Assert.AreEqual(pinnedPerWindow, restored.SourceTotal, $"A12 restored to the pinned rate; sources={string.Join(",", restored.SourceCounts)} watched={string.Join(",", restored.WatchedExits)}");
            Assert.AreEqual(restored.SourceTotal - restored.SinkTotal, restored.OnBeltsDelta, "A12 restored conservation");
        }

        // 減速の間、毎 tick の速度が floor(18×RPM/10) に一致し、密着した列の搬出数が累積移動量から決まる数に一致する。最後は速度0で止まる
        // While slowing, each tick's speed equals floor(18×RPM/10) and the packed run's exits equal the number derived from the cumulative advance; it ends stopped at speed 0
        private static void AssertSlowdownThenStop(BeltThroughputSession session)
        {
            var transformer = ServerContext.WorldBlockDatastore.GetBlock(WatchedCell).GetComponent<IGearEnergyTransformer>();
            var headDistance = BeltWorldTestUtil.SegmentAt(WatchedCell).CaptureItems()[0].DistanceToExit;
            var exitsBefore = session.WatchedExits()[0];
            var travelled = 0;
            var stoppedTicks = 0;
            var deadline = session.Tick + StopDeadlineTicks;
            while (stoppedTicks < StoppedTicksToConfirm)
            {
                session.TickOnce();
                var speed = BeltWorldTestUtil.SegmentAt(WatchedCell).Speed;
                Assert.AreEqual(BeltGearDriveSpeed.Resolve(WoodSpeed, transformer.CurrentRpm.AsPrimitive(), BaseRpm), speed, $"A11 (3) speed follows RPM at tick {session.Tick}");
                travelled += speed;
                stoppedTicks = speed == 0 ? stoppedTicks + 1 : 0;
                if (deadline < session.Tick) Assert.Fail($"A11 (3) the belt did not stop within {StopDeadlineTicks} ticks");
            }

            // 通常segmentの先頭は出口を越えた tick に出る(ちょうど届いただけでは出ない)。密着した列なので k 個目は d+360k を越えたときに出る
            // A normal segment's head leaves on the tick it passes the exit (merely reaching it is not enough); in a packed run the k-th item leaves once past d+360k
            var expectedExits = travelled <= headDistance ? 0 : 1 + (travelled - headDistance - 1) / 360;
            Assert.AreEqual(expectedExits, session.WatchedExits()[0] - exitsBefore, "A11 (3) exits during the slowdown follow the cumulative advance");
        }

        private static int WindmillRampTicks()
        {
            var param = (FuelGearGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.ThroughputFuelWindmill).BlockParam;
            return (int)GameUpdater.SecondsToTicks(param.TimeToMax);
        }

        private static void Refuel(Vector3Int windmill)
        {
            var inventory = (IOpenableInventory)ServerContext.WorldBlockDatastore.GetBlock(windmill).GetComponent<IOpenableBlockInventoryComponent>();
            inventory.InsertItem(FuelItem, ItemStackLevelDataStore.Instance.GetMaxStack(FuelItem));
        }

        private static void RunDry(Vector3Int windmill)
        {
            var inventory = ServerContext.WorldBlockDatastore.GetBlock(windmill).GetComponent<IOpenableBlockInventoryComponent>();
            for (var slot = 0; slot < inventory.GetSlotSize(); slot++) inventory.SetItem(slot, ServerContext.ItemStackFactory.CreatEmpty());
        }

        private static void SetRpm(Vector3Int generator, float rpm)
        {
            ServerContext.WorldBlockDatastore.GetBlock(generator).GetComponent<SimpleGearGeneratorComponent>().SetGenerateRpm(rpm);
        }
    }
}
