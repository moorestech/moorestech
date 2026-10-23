using System;
using Core.Master;
using Game.Fluid;
using Game.Fluid.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    /// <summary>
    ///     痕跡の規則(ADR 0090 追記)。整数化で「押し出せない微小量」が残っても、別の流体を遮らず入ってきた流体の名に置き換わること、
    ///     境界ポートに繋がるノードは最後の単位まで渡しきること、閾値がゲインの式どおりであることを確認する。
    ///     Trace rule (ADR 0090 addendum): a residue too small to push never blocks another fluid and takes the incoming fluid id,
    ///     a node on a boundary port drains to its last unit, and the threshold follows the gain formula exactly.
    /// </summary>
    public class FluidTraceResidueTest
    {
        private static readonly FluidId Water = new(1);
        private static readonly FluidId Oil = new(2);
        private const long Unit = FluidUnits.UnitsPerAmount;

        [Test]
        // 最小ゲインは「減衰の切り捨てを越えて速度 0 から 1 単位を流す最小値」であることを式で確認
        // MinSustainedGain is the least gain that survives the damping truncation from a zero velocity
        public void MinSustainedGainIsTheSmallestGainThatSurvivesDamping()
        {
            var keep = 1000 - FluidSimulationConstants.DampingPerMille;
            Assert.AreEqual(0, (FluidPressureMath.MinSustainedGain - 1) * keep / 1000);
            Assert.GreaterOrEqual(FluidPressureMath.MinSustainedGain * keep / 1000, 1);
        }

        [Test]
        // 鉄のパイプ(500)どうしなら 17 単位までが痕跡、18 単位から流れになる(ゲイン 2 の境は 16.7 単位だが、充填率の切り捨てで 1 単位ずれる)。タンク(50000)と鉄のパイプの面では 1500 単位が痕跡で 2000 単位は流れになる
        // Between iron pipes (500) 17 units are a trace and 18 become a flow (the gain-2 bound is 16.7 units; the fill-rate truncation shifts it by one); across a tank (50000) to iron-pipe face 1500 units are a trace and 2000 are a flow
        public void TraceThresholdFollowsTheGainFormula()
        {
            Assert.IsTrue(FluidPressureMath.IsTrace(0, 500 * Unit, 500 * Unit));
            Assert.IsTrue(FluidPressureMath.IsTrace(17, 500 * Unit, 500 * Unit));
            Assert.IsFalse(FluidPressureMath.IsTrace(18, 500 * Unit, 500 * Unit));
            Assert.IsTrue(FluidPressureMath.IsTrace(1500, 50000 * Unit, 500 * Unit));
            Assert.IsFalse(FluidPressureMath.IsTrace(2000, 50000 * Unit, 500 * Unit));
        }

        [Test]
        // 排水しきった直列網は、ポートのノードが 0 になり、他のノードの残りは静止の上限(隣との差が痕跡 17 単位以内なので、ポートから i 番目は 17 × i 単位以下)に収まる。総量は保存される
        // A chain drained through an end port leaves the port node at zero; the rest stays within the resting bound (neighbors differ by at most the 17-unit trace, so node i from the port holds at most 17 × i units) and the total is conserved
        public void ChainDrainEmptiesThePortNodeAndLeavesOnlyRestingResidueElsewhere()
        {
            var port = new FluidSimulationStepperTest.TestBoundaryPort(0, 500, acceptAll: true);
            var nodes = new (Vector3Int, long, long, FluidId)[10];
            var faces = new (int, int, long, bool, bool)[9];
            for (var i = 0; i < 10; i++) nodes[i] = (new Vector3Int(i, 0, 0), 10 * Unit, i < 3 ? 5 * Unit : 0L, i < 3 ? Water : FluidMaster.EmptyFluidId);
            for (var i = 0; i < 9; i++) faces[i] = (i, i + 1, 500L, true, true);
            var network = FluidSimulationStepperTest.Build(nodes, faces, new IFluidBoundaryPort[] { port });
            Run(network, 20000, 1);

            Assert.AreEqual(0, network.NodeAmounts[0]);
            Assert.AreEqual(FluidMaster.EmptyFluidId, network.NodeFluidIds[0]);
            long remaining = 0;
            for (var i = 1; i < 10; i++)
            {
                Assert.LessOrEqual(network.NodeAmounts[i], 17 * i, $"ノード {i} に静止の上限を超える {network.NodeAmounts[i]} 単位が残った");
                remaining += network.NodeAmounts[i];
            }
            Assert.AreEqual(15 * Unit, port.DeliveredTotal + remaining);
        }

        [Test]
        // 痕跡の水が残るノードへ油が届くと、面は閉じずに油が入り、ノードの名は油になる。総量(単位)は保存される
        // Oil reaching a node holding a trace of water enters through an open face and the node becomes oil; total units are conserved
        public void TraceResidueAdoptsTheIncomingFluidInsteadOfBlocking()
        {
            var network = FluidSimulationStepperTest.Build(
                new[] { (new Vector3Int(0, 0, 0), 100 * Unit, 10L, Water), (new Vector3Int(1, 0, 0), 100 * Unit, 50 * Unit, Oil) },
                new[] { (0, 1, 500L, true, true) }, Array.Empty<IFluidBoundaryPort>());
            Run(network, 200, 1);

            Assert.AreEqual(Oil, network.NodeFluidIds[0]);
            Assert.Greater(network.NodeAmounts[0], 10);
            Assert.AreEqual(50 * Unit + 10, network.NodeAmounts[0] + network.NodeAmounts[1]);
        }

        [Test]
        // 痕跡を 1 単位でも超える水は今までどおり面を閉じ、どちらも変化しない
        // Water just one unit above the trace threshold still closes the face and neither side changes
        public void ResidueAboveTheThresholdStillClosesTheFace()
        {
            var network = FluidSimulationStepperTest.Build(
                new[] { (new Vector3Int(0, 0, 0), 100 * Unit, 18L, Water), (new Vector3Int(1, 0, 0), 100 * Unit, 50 * Unit, Oil) },
                new[] { (0, 1, 500L, true, true) }, Array.Empty<IFluidBoundaryPort>());
            Run(network, 200, 1);

            Assert.AreEqual(Water, network.NodeFluidIds[0]);
            Assert.AreEqual(18, network.NodeAmounts[0]);
            Assert.AreEqual(50 * Unit, network.NodeAmounts[1]);
        }

        [Test]
        // タンクの痕跡は、繋がる面の小さい方の容量で判定する。1500 単位(1.5)の水は鉄のパイプの油に置き換わり、2000 単位は遮る
        // A tank trace is judged by the smaller capacity of its faces: 1500 units (1.5) of water give way to oil from an iron pipe, 2000 units block it
        public void TankTraceIsJudgedByItsSmallestNeighborFace()
        {
            var replaced = BuildTankAndPipe(1500);
            Run(replaced, 500, 1);
            Assert.AreEqual(Oil, replaced.NodeFluidIds[0]);
            Assert.AreEqual(100 * Unit + 1500, replaced.NodeAmounts[0] + replaced.NodeAmounts[1]);

            var blocked = BuildTankAndPipe(2000);
            Run(blocked, 500, 1);
            Assert.AreEqual(Water, blocked.NodeFluidIds[0]);
            Assert.AreEqual(2000, blocked.NodeAmounts[0]);
        }

        [Test]
        // 境界ポートに繋がるノードは、ゲインが切り捨てで 0 になる量でも最後の単位まで渡しきり、空になる
        // A node on a boundary port drains to its last unit even when the gain truncates to zero, and ends empty
        public void PortNodeDrainsCompletelyBelowTheGainThreshold()
        {
            var port = new FluidSimulationStepperTest.TestBoundaryPort(0, 500, acceptAll: true);
            var network = FluidSimulationStepperTest.Build(new[] { (new Vector3Int(0, 0, 0), 100 * Unit, 10L, Water) }, Array.Empty<(int, int, long, bool, bool)>(), new IFluidBoundaryPort[] { port });
            Run(network, 50, 1);

            Assert.AreEqual(0, network.NodeAmounts[0]);
            Assert.AreEqual(FluidMaster.EmptyFluidId, network.NodeFluidIds[0]);
            Assert.AreEqual(10, port.DeliveredTotal);
        }

        [Test]
        // 機械からの流入も同じ規則。痕跡の水しかないノードへの油は受け入れて名が油になり、閾値を超える水のノードへの油は拒否される
        // Machine inflow follows the same rule: oil into a node with only a trace of water is accepted and relabels it, oil into a node above the threshold is rejected
        public void ExternalInflowReplacesATraceOfAnotherFluid()
        {
            var trace = FluidSimulationStepperTest.Build(new[] { (new Vector3Int(0, 0, 0), 100 * Unit, 10L, Water) }, Array.Empty<(int, int, long, bool, bool)>(), Array.Empty<IFluidBoundaryPort>());
            var remainder = trace.AddExternal(0, new FluidStack(5d, Oil), out var accepted);
            Assert.AreEqual(5 * Unit, accepted);
            Assert.AreEqual(0d, remainder.Amount);
            Assert.AreEqual(Oil, trace.NodeFluidIds[0]);
            Assert.AreEqual(5 * Unit + 10, trace.NodeAmounts[0]);

            var above = FluidSimulationStepperTest.Build(new[] { (new Vector3Int(0, 0, 0), 100 * Unit, 18L, Water) }, Array.Empty<(int, int, long, bool, bool)>(), Array.Empty<IFluidBoundaryPort>());
            above.AddExternal(0, new FluidStack(5d, Oil), out var rejected);
            Assert.AreEqual(0, rejected);
            Assert.AreEqual(Water, above.NodeFluidIds[0]);
            Assert.AreEqual(18, above.NodeAmounts[0]);
        }

        [Test]
        // 置き換えを含む tick も、範囲の分割数に依らず量・名・ハッシュが一致する
        // Ticks that include a replacement still give the same amounts, ids and hash for any partition count
        public void TraceReplacementIsPartitionIndependent()
        {
            var single = BuildTankAndPipe(1500);
            var split = BuildTankAndPipe(1500);
            Run(single, 300, 1);
            Run(split, 300, 3);

            Assert.AreEqual(single.NodeAmounts, split.NodeAmounts);
            Assert.AreEqual(single.NodeFluidIds, split.NodeFluidIds);
            Assert.AreEqual(single.LastStateHash, split.LastStateHash);
        }

        // タンク(容量 50000)に水 waterUnits、隣の鉄のパイプ(容量 500)に油 100 を入れた 2 ノード網
        // A tank (capacity 50000) holding waterUnits of water next to an iron pipe (capacity 500) holding 100 of oil
        private static FluidSimNetwork BuildTankAndPipe(long waterUnits)
        {
            return FluidSimulationStepperTest.Build(
                new[] { (new Vector3Int(0, 0, 0), 50000 * Unit, waterUnits, Water), (new Vector3Int(1, 0, 0), 500 * Unit, 100 * Unit, Oil) },
                new[] { (0, 1, 500L, true, true) }, Array.Empty<IFluidBoundaryPort>());
        }

        private static void Run(FluidSimNetwork network, int ticks, int partitionCount)
        {
            var scratch = new FluidSimulationScratch(network);
            for (var i = 0; i < ticks; i++) FluidSimulationStepper.Step(network, scratch, partitionCount);
        }
    }
}
