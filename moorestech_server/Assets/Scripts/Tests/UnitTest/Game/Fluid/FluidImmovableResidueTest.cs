using System;
using Core.Master;
using Game.Fluid;
using Game.Fluid.Simulation;
using Game.Fluid.Sync.State;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.Fluid
{
    /// <summary>
    ///     動けない量の規則(ADR 0090 追記)。整数の切り捨てで止まる微小量は tick の最後に 0 になって名が外れ、別の流体を遮らないこと、
    ///     閾値が圧力の式どおりで容量比に従うこと、流れている量や閾値以上の流入は消えないこと、分割数に依らないことを確認する。
    ///     Immovable-amount rule (ADR 0090 addendum): a residue stopped by integer truncation is cleared with its id at the end of a tick and never blocks another fluid,
    ///     the threshold follows the pressure formula and the capacity ratio, flowing amounts and inflow above the threshold survive, and the result is partition independent.
    /// </summary>
    public class FluidImmovableResidueTest
    {
        private static readonly FluidId Water = new(1);
        private static readonly FluidId Oil = new(2);
        private const long Unit = FluidUnits.UnitsPerAmount;
        private const long FaceCap = Unit / 2;

        [Test]
        // 上限の圧力差では速度 0 から動き出せず、1 大きい圧力差で 1 単位が流れる
        // The largest immovable pressure difference cannot start a flow from rest, while one more unit moves one unit
        public void MaxImmovablePressureUnitsIsTheLargestDifferenceThatCannotStartAFlow()
        {
            var keep = 1000 - FluidSimulationConstants.DampingPerMille;
            Assert.AreEqual(16, FluidPressureMath.MaxImmovablePressureUnits);
            Assert.AreEqual(0, FluidPressureMath.Gain(FluidPressureMath.MaxImmovablePressureUnits) * keep / 1000);
            Assert.GreaterOrEqual(FluidPressureMath.Gain(FluidPressureMath.MaxImmovablePressureUnits + 1) * keep / 1000, 1);
        }

        [Test]
        // 鉄のパイプ(500)どうしでは 16 単位まで、タンク(50000)と鉄のパイプの面では 1,699 単位(圧力差 16)までが動けない量
        // Between iron pipes (500) up to 16 units are immovable; across a tank (50000) to iron-pipe face up to 1,699 units (pressure difference 16) are
        public void ImmovableThresholdFollowsTheCapacityRatio()
        {
            Assert.IsTrue(FluidPressureMath.IsImmovable(0, 500 * Unit, 500 * Unit));
            Assert.IsTrue(FluidPressureMath.IsImmovable(16, 500 * Unit, 500 * Unit));
            Assert.IsFalse(FluidPressureMath.IsImmovable(17, 500 * Unit, 500 * Unit));
            Assert.IsTrue(FluidPressureMath.IsImmovable(1699, 50000 * Unit, 500 * Unit));
            Assert.IsFalse(FluidPressureMath.IsImmovable(1700, 50000 * Unit, 500 * Unit));
        }

        [Test]
        // 端のポートで排水した直列網は全ノードが 0 と名なしになり、消えた量は各ノード数十単位(10^-8 相当)以内に収まる
        // A chain drained through an end port ends with every node at zero and unnamed, and the vanished amount stays within a few dozen units (~1e-8) per node
        public void ChainDrainClearsEveryNodeAndLosesOnlyImmovableAmounts()
        {
            var port = new FluidSimulationStepperTest.TestBoundaryPort(0, FaceCap, acceptAll: true);
            var nodes = new (Vector3Int, long, long, FluidId)[10];
            var faces = new (int, int, long, bool, bool)[9];
            for (var i = 0; i < 10; i++) nodes[i] = (new Vector3Int(i, 0, 0), 10 * Unit, i < 3 ? 5 * Unit : 0L, i < 3 ? Water : FluidMaster.EmptyFluidId);
            for (var i = 0; i < 9; i++) faces[i] = (i, i + 1, FaceCap, true, true);
            var network = FluidSimulationStepperTest.Build(nodes, faces, new IFluidBoundaryPort[] { port });
            Run(network, 20000, 1);

            for (var i = 0; i < 10; i++)
            {
                Assert.AreEqual(0, network.NodeAmounts[i], $"ノード {i} に残った");
                Assert.AreEqual(FluidMaster.EmptyFluidId, network.NodeFluidIds[i], $"ノード {i} の名が残った");
            }
            Assert.LessOrEqual(port.DeliveredTotal, 15 * Unit);
            Assert.GreaterOrEqual(port.DeliveredTotal, 15 * Unit - 10 * 100);
        }

        [Test]
        // 動けない量の水が残るノードは次の tick に空になり、油が入って名が油になる。油は厳密に保存され、ハッシュは全走査と一致する
        // A node holding an immovable trace of water is cleared next tick, then oil enters and names it; oil is conserved exactly and the hash matches a full walk
        public void ImmovableResidueOfAnotherFluidDoesNotBlockTheNextFluid()
        {
            var network = FluidSimulationStepperTest.Build(
                new[] { (new Vector3Int(0, 0, 0), 100 * Unit, 10L, Water), (new Vector3Int(1, 0, 0), 100 * Unit, 50 * Unit, Oil) },
                new[] { (0, 1, FaceCap, true, true) }, Array.Empty<IFluidBoundaryPort>());
            Run(network, 200, 1);

            Assert.AreEqual(Oil, network.NodeFluidIds[0]);
            Assert.Greater(network.NodeAmounts[0], 0);
            Assert.AreEqual(50 * Unit, network.NodeAmounts[0] + network.NodeAmounts[1]);
            Assert.AreEqual(FluidNetworkStateHash.Compute(network), network.LastStateHash);
        }

        [Test]
        // 動ける量(閾値 + 1)の水は消えず、今までどおり面を閉じて双方が変化しない
        // Water just above the threshold is kept, still closes the face, and neither side changes
        public void MovableResidueStillClosesTheFace()
        {
            var water = FluidPressureMath.MaxImmovablePressureUnits + 1;
            var network = FluidSimulationStepperTest.Build(
                new[] { (new Vector3Int(0, 0, 0), 100 * Unit, water, Water), (new Vector3Int(1, 0, 0), 100 * Unit, 50 * Unit, Oil) },
                new[] { (0, 1, FaceCap, true, true) }, Array.Empty<IFluidBoundaryPort>());
            Run(network, 200, 1);

            Assert.AreEqual(Water, network.NodeFluidIds[0]);
            Assert.AreEqual(water, network.NodeAmounts[0]);
            Assert.AreEqual(50 * Unit, network.NodeAmounts[1]);
        }

        [Test]
        // タンクの動けない量は、繋がる面の小さい方の容量で判定する。1,699 単位の水は消えて油が入り、1,700 単位は残って遮る
        // A tank's immovable amount is judged by the smaller capacity of its faces: 1,699 units of water vanish and oil enters, 1,700 units stay and block
        public void TankResidueIsJudgedByItsSmallestNeighborFace()
        {
            var cleared = BuildTankAndPipe(1699);
            Run(cleared, 500, 1);
            Assert.AreEqual(Oil, cleared.NodeFluidIds[0]);
            Assert.AreEqual(100 * Unit, cleared.NodeAmounts[0] + cleared.NodeAmounts[1]);

            var blocked = BuildTankAndPipe(1700);
            Run(blocked, 500, 1);
            Assert.AreEqual(Water, blocked.NodeFluidIds[0]);
            Assert.AreEqual(1700, blocked.NodeAmounts[0]);
            Assert.AreEqual(100 * Unit, blocked.NodeAmounts[1]);
        }

        [Test]
        // 境界ポートに繋がるノードは 0 と名なしで終わり、機械に渡らず消える量は閾値(16 単位)以内
        // A node on a boundary port ends at zero and unnamed; what vanishes instead of reaching the machine is within the threshold (16 units)
        public void PortNodeEndsEmptyAndLosesAtMostTheThreshold()
        {
            var port = new FluidSimulationStepperTest.TestBoundaryPort(0, FaceCap, acceptAll: true);
            var network = FluidSimulationStepperTest.Build(new[] { (new Vector3Int(0, 0, 0), 100 * Unit, 10 * Unit, Water) }, Array.Empty<(int, int, long, bool, bool)>(), new IFluidBoundaryPort[] { port });
            Run(network, 2000, 1);

            Assert.AreEqual(0, network.NodeAmounts[0]);
            Assert.AreEqual(FluidMaster.EmptyFluidId, network.NodeFluidIds[0]);
            Assert.LessOrEqual(port.DeliveredTotal, 10 * Unit);
            Assert.GreaterOrEqual(port.DeliveredTotal, 10 * Unit - FluidPressureMath.MaxImmovablePressureUnits);
        }

        [Test]
        // 閾値を超える流入(2^-20 ≒ 953 単位/tick)は積み上がり、閾値以下の流入(2^-27 ≒ 7 単位/tick)は毎 tick 消える。後者が規則の限界で、マスタの流量より 5 桁以上小さい
        // Inflow above the threshold (2^-20 ≈ 953 units per tick) accumulates; inflow below it (2^-27 ≈ 7 units per tick) vanishes every tick. The latter is the rule's limit, five-plus orders below any master flow rate
        public void InflowAboveTheThresholdAccumulatesAndInflowBelowItIsClearedEachTick()
        {
            var accumulating = SingleNode();
            var accumulatingScratch = new FluidSimulationScratch(accumulating);
            for (var i = 0; i < 100; i++)
            {
                accumulating.AddExternal(0, new FluidStack(Math.Pow(2, -20), Water), out var accepted);
                Assert.AreEqual(953, accepted);
                FluidSimulationStepper.Step(accumulating, accumulatingScratch);
            }
            Assert.AreEqual(95300, accumulating.NodeAmounts[0]);
            Assert.AreEqual(Water, accumulating.NodeFluidIds[0]);

            var cleared = SingleNode();
            var clearedScratch = new FluidSimulationScratch(cleared);
            for (var i = 0; i < 100; i++)
            {
                cleared.AddExternal(0, new FluidStack(Math.Pow(2, -27), Water), out var accepted);
                Assert.AreEqual(7, accepted);
                FluidSimulationStepper.Step(cleared, clearedScratch);
            }
            Assert.AreEqual(0, cleared.NodeAmounts[0]);
            Assert.AreEqual(FluidMaster.EmptyFluidId, cleared.NodeFluidIds[0]);
        }

        [Test]
        // 0 にする tick を含めても、範囲の分割数に依らず量・名・ハッシュが一致する
        // Ticks that clear a node still give the same amounts, ids and hash for any partition count
        public void ClearingIsPartitionIndependent()
        {
            var single = BuildTankAndPipe(1699);
            var split = BuildTankAndPipe(1699);
            Run(single, 300, 1);
            Run(split, 300, 3);

            Assert.AreEqual(single.NodeAmounts, split.NodeAmounts);
            Assert.AreEqual(single.NodeFluidIds, split.NodeFluidIds);
            Assert.AreEqual(single.LastStateHash, split.LastStateHash);
        }

        private static FluidSimNetwork SingleNode()
        {
            return FluidSimulationStepperTest.Build(new[] { (new Vector3Int(0, 0, 0), 100 * Unit, 0L, FluidMaster.EmptyFluidId) }, Array.Empty<(int, int, long, bool, bool)>(), Array.Empty<IFluidBoundaryPort>());
        }

        // タンク(容量 50000)に水 waterUnits、隣の鉄のパイプ(容量 500)に油 100 を入れた 2 ノード網
        // A tank (capacity 50000) holding waterUnits of water next to an iron pipe (capacity 500) holding 100 of oil
        private static FluidSimNetwork BuildTankAndPipe(long waterUnits)
        {
            return FluidSimulationStepperTest.Build(
                new[] { (new Vector3Int(0, 0, 0), 50000 * Unit, waterUnits, Water), (new Vector3Int(1, 0, 0), 500 * Unit, 100 * Unit, Oil) },
                new[] { (0, 1, FaceCap, true, true) }, Array.Empty<IFluidBoundaryPort>());
        }

        private static void Run(FluidSimNetwork network, int ticks, int partitionCount)
        {
            var scratch = new FluidSimulationScratch(network);
            for (var i = 0; i < ticks; i++) FluidSimulationStepper.Step(network, scratch, partitionCount);
        }
    }
}
