using System;
using Core.Master;
using Game.Fluid;
using Game.Fluid.Simulation;
using Game.Fluid.Sync.State;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    /// <summary>
    ///     FluidSimulationStepper単体の数値検証。ワールドやDIを介さず、リープフロッグ2フェーズの保存則・収束・クランプ挙動を直接確認する。
    ///     量は FluidUnits の整数単位(1 = 1/1000)。整数なので総量は許容誤差なしで一致する。
    ///     Pure numerical verification of FluidSimulationStepper: conservation, convergence and clamping of the two-phase leapfrog, without world or DI.
    ///     Amounts are FluidUnits integers (1 = 1/1000), so totals match with no tolerance.
    /// </summary>
    public class FluidSimulationStepperTest
    {
        private static readonly FluidId TestFluidId = new(1);
        private const long Unit = FluidUnits.UnitsPerAmount;

        [Test]
        // 2ノードの水位が均等化され、総量が厳密に保存されることを確認
        // Two nodes equalize their levels and the total amount is conserved exactly
        public void TwoNodeEqualizationTest()
        {
            var network = Chain(2, 500, true, true, (0, 30 * Unit, TestFluidId));

            // Damping=0.002では静定に約2000step要する
            // With Damping=0.002 settling takes about 2000 steps
            Run(network, 2500);

            Assert.AreEqual(15 * Unit, network.NodeAmounts[0], 500);
            Assert.AreEqual(15 * Unit, network.NodeAmounts[1], 500);
            Assert.AreEqual(30 * Unit, network.NodeAmounts[0] + network.NodeAmounts[1]);
            Assert.AreEqual(TestFluidId, network.NodeFluidIds[1]);
        }

        [Test]
        // チェーン全体で総量が保存され、全ノードが0以上容量以下に収まることを確認
        // The total is conserved across a chain and every node stays within [0, capacity]
        public void ChainConservationTest()
        {
            var network = Chain(5, 500, true, true, (0, 100 * Unit, TestFluidId), (3, 40 * Unit, TestFluidId));
            Run(network, 500);

            long total = 0;
            for (var i = 0; i < network.NodeCount; i++)
            {
                total += network.NodeAmounts[i];
                Assert.GreaterOrEqual(network.NodeAmounts[i], 0);
                Assert.LessOrEqual(network.NodeAmounts[i], network.NodeCapacities[i]);
            }
            Assert.AreEqual(140 * Unit, total);

            // 最終的に全ノードが均等（各28）に収束する
            // Eventually every node converges to the same level (28 each)
            for (var i = 0; i < network.NodeCount; i++) Assert.AreEqual(28 * Unit, network.NodeAmounts[i], 1 * Unit);
        }

        [Test]
        // 一方向面は許可方向にのみ流れることを確認
        // A one-way face only flows in its allowed direction
        public void OneWayFaceTest()
        {
            // A→Bのみ許可。Bに液体があるがAへは逆流しない
            // Only A→B allowed; fluid in B must not flow back into A
            var network = Chain(2, 500, true, false, (1, 50 * Unit, TestFluidId));
            Run(network, 100);

            Assert.AreEqual(0, network.NodeAmounts[0]);
            Assert.AreEqual(50 * Unit, network.NodeAmounts[1]);
        }

        [Test]
        // 容量が異なるノード同士は充填率（水位）で釣り合うことを確認
        // Nodes with different capacities balance by fill rate (water level)
        public void FillRateBalanceAcrossCapacitiesTest()
        {
            var network = Build(new[] { (new Vector3Int(0, 0, 0), 1000 * Unit, 1000 * Unit, TestFluidId), (new Vector3Int(1, 0, 0), 100 * Unit, 0L, FluidMaster.EmptyFluidId) },
                new[] { (0, 1, 500L, true, true) }, Array.Empty<IFluidBoundaryPort>());
            Run(network, 2000);

            // 充填率が一致する（tank≒909, pipe≒91）
            // Fill rates match (tank ≈ 909, pipe ≈ 91)
            Assert.AreEqual(network.FillRate(0), network.FillRate(1), 0.02d);
            Assert.AreEqual(1000 * Unit, network.NodeAmounts[0] + network.NodeAmounts[1]);
        }

        [Test]
        // 境界ポートはノードを排水し、速度は面上限を超えず、拒否時は速度が殺されることを確認
        // A boundary port drains its node, velocity never exceeds the cap, and rejection kills the velocity
        public void BoundaryPortDrainAndFeedbackTest()
        {
            var acceptingPort = new TestBoundaryPort(0, 500, acceptAll: true);
            var network = Build(new[] { (new Vector3Int(0, 0, 0), 100 * Unit, 20 * Unit, TestFluidId) }, Array.Empty<(int, int, long, bool, bool)>(), new IFluidBoundaryPort[] { acceptingPort });
            var scratch = new FluidSimulationScratch(network);
            for (var i = 0; i < 1000; i++)
            {
                FluidSimulationStepper.Step(network, scratch);
                Assert.LessOrEqual(acceptingPort.Velocity, 500);
            }

            // 排水は残量に比例して漸減するため指数テールを持つ。整数の切り捨てで圧力が0になる数単位だけが残り、渡した量と残量の和は厳密に保存される
            // Draining decays proportionally to the remaining amount (exponential tail); only the few units whose pressure truncates to zero remain, and delivered plus remaining is conserved exactly
            Assert.LessOrEqual(network.NodeAmounts[0], 8);
            Assert.AreEqual(20 * Unit, acceptingPort.DeliveredTotal + network.NodeAmounts[0]);

            // 全拒否する境界では、ノード残量が変わらず速度も0に収束する
            // A rejecting boundary leaves the node untouched and the velocity converges to zero
            var rejectingPort = new TestBoundaryPort(0, 500, acceptAll: false);
            var network2 = Build(new[] { (new Vector3Int(0, 0, 0), 100 * Unit, 20 * Unit, TestFluidId) }, Array.Empty<(int, int, long, bool, bool)>(), new IFluidBoundaryPort[] { rejectingPort });
            Run(network2, 100);

            Assert.AreEqual(20 * Unit, network2.NodeAmounts[0]);
            Assert.AreEqual(0, rejectingPort.Velocity);
        }

        [Test]
        // 異種流体が向かい合う面は閉面となり、双方が変化しないことを確認
        // A face between mismatched fluids closes and neither side changes
        public void MismatchedFluidClosedFaceTest()
        {
            var network = Chain(2, 500, true, true, (0, 30 * Unit, new FluidId(1)), (1, 10 * Unit, new FluidId(2)));
            Run(network, 100);

            Assert.AreEqual(30 * Unit, network.NodeAmounts[0]);
            Assert.AreEqual(10 * Unit, network.NodeAmounts[1]);
        }

        [Test]
        // Step が走査のついでに残すハッシュは、全走査で求めた値と一致する
        // The hash the step folds in during its own walk equals the one from a full walk
        public void StepHashMatchesFullRecompute()
        {
            var network = Chain(5, 500, true, true, (0, 100 * Unit, TestFluidId), (3, 40 * Unit, TestFluidId));
            Run(network, 50);
            var stepped = network.LastStateHash;
            Assert.AreEqual(stepped, FluidNetworkStateHash.Compute(network));
        }

        private static void Run(FluidSimNetwork network, int ticks)
        {
            var scratch = new FluidSimulationScratch(network);
            for (var i = 0; i < ticks; i++) FluidSimulationStepper.Step(network, scratch);
        }

        // x軸に並ぶ容量100のノード列を同じ面上限・向き許可で繋ぎ、指定ノードへ初期量(単位)を入れる
        // A row of capacity-100 nodes along x, joined by faces with one cap and direction permission, with initial amounts (units) on the given nodes
        internal static FluidSimNetwork Chain(int nodeCount, long faceCapUnits, bool allowAToB, bool allowBToA, params (int node, long units, FluidId fluidId)[] contents)
        {
            var nodes = new (Vector3Int, long, long, FluidId)[nodeCount];
            for (var i = 0; i < nodeCount; i++) nodes[i] = (new Vector3Int(i, 0, 0), 100 * Unit, 0L, FluidMaster.EmptyFluidId);
            foreach (var (node, units, fluidId) in contents) nodes[node] = (nodes[node].Item1, 100 * Unit, units, fluidId);
            var faces = new (int, int, long, bool, bool)[nodeCount - 1];
            for (var i = 0; i < faces.Length; i++) faces[i] = (i, i + 1, faceCapUnits, allowAToB, allowBToA);
            return Build(nodes, faces, Array.Empty<IFluidBoundaryPort>());
        }

        internal static FluidSimNetwork Build((Vector3Int position, long capacity, long amount, FluidId fluidId)[] nodes, (int a, int b, long cap, bool allowAToB, bool allowBToA)[] faces, IFluidBoundaryPort[] ports)
        {
            var positions = new Vector3Int[nodes.Length];
            var capacities = new long[nodes.Length];
            var amounts = new long[nodes.Length];
            var fluidIds = new FluidId[nodes.Length];
            for (var i = 0; i < nodes.Length; i++) (positions[i], capacities[i], amounts[i], fluidIds[i]) = nodes[i];
            var faceA = new int[faces.Length];
            var faceB = new int[faces.Length];
            var caps = new long[faces.Length];
            var allowAToB = new bool[faces.Length];
            var allowBToA = new bool[faces.Length];
            for (var i = 0; i < faces.Length; i++) (faceA[i], faceB[i], caps[i], allowAToB[i], allowBToA[i]) = faces[i];
            return new FluidSimNetwork(positions, capacities, amounts, fluidIds, faceA, faceB, caps, new long[faces.Length], allowAToB, allowBToA, ports);
        }

        // テスト用の境界ポート。acceptAll=falseなら全量拒否する
        // Test boundary port; rejects everything when acceptAll is false
        internal class TestBoundaryPort : IFluidBoundaryPort
        {
            public int NodeIndex { get; }
            public long FlowCapacityPerTick { get; }
            public long Velocity { get; set; }
            public long DeliveredTotal { get; private set; }

            private readonly bool _acceptAll;

            public TestBoundaryPort(int nodeIndex, long flowCapacityPerTick, bool acceptAll)
            {
                NodeIndex = nodeIndex;
                FlowCapacityPerTick = flowCapacityPerTick;
                _acceptAll = acceptAll;
            }

            public long Deliver(long attemptedUnits, FluidId fluidId)
            {
                if (!_acceptAll) return 0;
                DeliveredTotal += attemptedUnits;
                return attemptedUnits;
            }
        }
    }
}
