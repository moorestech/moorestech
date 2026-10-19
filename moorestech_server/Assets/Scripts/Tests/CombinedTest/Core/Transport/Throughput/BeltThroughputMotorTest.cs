using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Tests.Module.TestMod;
using Tests.Util;
using Tests.Util.EnergySystem;
using Tests.Util.Throughput;
using UnityEngine;
using static Tests.Util.Throughput.BeltThroughputLayout;

namespace Tests.CombinedTest.Core.Transport.Throughput
{
    // A19: 電気→歯車モーター1台だけで回す歯車網(ADR 0084 裁定 F3 追記)
    // A19: a gear network driven by a single electric-to-gear motor (ADR 0084, F3 ruling addendum)
    // モーターは歯車 tick の末尾で 1 tick 分のバッテリーを使い切り出力 0 に戻る。消費側が原点を生で読むとブロック更新・搬送は毎 tick 0 を見て何も動かない
    // The motor empties its one-tick battery at the end of the gear tick and drops to zero output; consumers reading the origin live saw zero at every block update and transport, so nothing moved
    [Category("CiShardServerBeltThroughput")]
    public class BeltThroughputMotorTest
    {
        // モード0 は 60 RPM。木の歯車ベルト(基準 10 RPM、速度 18)は floor(18×60/10)=108 で回り、360 幅のアイテムは 4 tick に 1 個出る
        // Mode 0 is 60 RPM; a wood gear belt (base 10 RPM, speed 18) runs at floor(18×60/10)=108, so a 360-wide item leaves every 4 ticks
        private const int MotorPeriod = 4;
        private const int Length = 10;
        private static readonly Vector3Int Motor = new(1, 0, 0);
        private static readonly Vector3Int Pole = new(4, 0, -3);
        private static readonly System.Guid PassThroughRecipeGuid = new("7b3e1a10-0006-4000-8000-000000000001");

        // A19a: モーター → 木の歯車ベルト10マス。供給も監視 segment も 100%
        // A19a: motor -> ten wood gear belts; both the source and the watched segment run at 100%
        [Test]
        public void A19a_MotorAloneDrivesGearBelt([Values] BeltPlacementOrder order)
        {
            var scenario = new BeltThroughputScenario().Source(new Vector3Int(0, 0, -1));
            Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 0, 0), BlockDirection.North, Length);
            scenario.Sink(new Vector3Int(0, 0, Length)).Watch(new Vector3Int(0, 0, 5));
            scenario.Block(ForUnitTestModBlockId.TestElectricToGearGenerator, Motor, BlockDirection.North);
            var session = scenario.Start(order);
            PowerMotor();

            session.TickUntilSteady();
            var run = session.Measure(session.DefaultWindow);
            BeltThroughputAssert.Throughput(run.Window / MotorPeriod, run, $"A19a motor-driven belt order={order}");
            BeltThroughputAssert.WatchedSegmentsAtFullThroughput(run, MotorPeriod, $"A19a motor-driven belt order={order}");
        }

        // A19b: モーター → ベルト3 → 素通し機 → ベルト3。歯車機械もモーターの回転数で加工する
        // A19b: motor -> three belts -> pass-through machine -> three belts; the gear machine also processes at the motor's RPM
        [Test]
        public void A19b_MotorAloneDrivesGearMachine([Values] BeltPlacementOrder order)
        {
            var scenario = new BeltThroughputScenario().Source(new Vector3Int(0, 0, -1));
            Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 0, 0), BlockDirection.North, 3);
            scenario.Block(ForUnitTestModBlockId.GearMachine, new Vector3Int(0, 0, 3), BlockDirection.North);
            var exit = Line(scenario, ForUnitTestModBlockId.WoodGearBelt, new Vector3Int(0, 0, 4), BlockDirection.North, 3);
            scenario.Sink(exit).Watch(new Vector3Int(0, 0, 1)).Watch(new Vector3Int(0, 0, 5));
            scenario.Block(ForUnitTestModBlockId.TestElectricToGearGenerator, Motor, BlockDirection.North);
            var session = scenario.Start(order);
            PowerMotor();

            // 機械はレシピを選ぶまで何も受け付けない(ADR 0042)
            // A machine accepts nothing until a recipe is selected (ADR 0042)
            var machine = ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(0, 0, 3));
            MachineRecipeSelectTestUtil.SelectRecipe(machine, MasterHolder.MachineRecipesMaster.GetRecipeElement(PassThroughRecipeGuid));
            session.TickUntilSteady();

            // 機械のスロットに居るアイテムはベルト上に数えないので、A9 と同じく供給数と機械前後の segment で見る
            // Items in the machine's slots are not counted on the belts, so as in A9 the source count and the segments around the machine are checked
            var run = session.Measure(session.DefaultWindow);
            Assert.AreEqual(BeltThroughputWindow.ExpectedTotal(run.Window, MotorPeriod), run.SourceTotal, $"A19b source emissions order={order}; sources={string.Join(",", run.SourceCounts)} sinks={string.Join(",", run.SinkCounts)}");
            BeltThroughputAssert.WatchedSegmentsAtFullThroughput(run, MotorPeriod, $"A19b before and after the machine order={order}");
        }

        // モーターのモード0の要求電力ぶんを毎 tick 供給する。満充電が毎 tick 成立し、歯車 tick ごとに定格で回る
        // Supply exactly mode 0's required power every tick so the battery fills every tick and each gear tick runs at the rated values
        private static void PowerMotor()
        {
            var param = (ElectricToGearGeneratorBlockParam)MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestElectricToGearGenerator).BlockParam;
            ElectricWireTestUtil.WirePower(Motor, Pole, param.OutputModes[0].RequiredPower);
        }
    }
}
