using System;
using System.Collections.Generic;
using Core.Master;
using Game.Fluid;
using Game.Fluid.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    /// <summary>
    ///     FluidSimulationStepper単体の数値検証。ワールドやDIを介さず、リープフロッグ2フェーズの保存則・収束・クランプ挙動を直接確認する。
    ///     Pure numerical verification of FluidSimulationStepper: conservation, convergence and clamping of the two-phase leapfrog, without world or DI.
    /// </summary>
    public class FluidSimulationStepperTest
    {
        private static readonly FluidId TestFluidId = new(1);

        [Test]
        // 2ノードの水位が均等化され、総量が厳密に保存されることを確認
        // Two nodes equalize their levels and the total amount is conserved exactly
        public void TwoNodeEqualizationTest()
        {
            var network = Chain(2, 0.5, true, true, (0, 30, TestFluidId));

            // Damping=0.002では静定に約2000step要する
            // With Damping=0.002 settling takes about 2000 steps
            Run(network, 2500);

            Assert.AreEqual(15d, network.NodeAmounts[0], 0.5d);
            Assert.AreEqual(15d, network.NodeAmounts[1], 0.5d);
            Assert.AreEqual(30d, network.NodeAmounts[0] + network.NodeAmounts[1], 1e-9);
            Assert.AreEqual(TestFluidId, network.NodeFluidIds[1]);
        }

        [Test]
        // チェーン全体で総量が保存され、全ノードが0以上容量以下に収まることを確認
        // The total is conserved across a chain and every node stays within [0, capacity]
        public void ChainConservationTest()
        {
            var network = Chain(5, 0.5, true, true, (0, 100, TestFluidId), (3, 40, TestFluidId));
            Run(network, 500);

            var total = 0d;
            for (var i = 0; i < network.NodeCount; i++)
            {
                total += network.NodeAmounts[i];
                Assert.GreaterOrEqual(network.NodeAmounts[i], 0);
                Assert.LessOrEqual(network.NodeAmounts[i], network.NodeCapacities[i] + 1e-9);
            }
            Assert.AreEqual(140d, total, 1e-9);

            // 最終的に全ノードが均等（各28）に収束する
            // Eventually every node converges to the same level (28 each)
            for (var i = 0; i < network.NodeCount; i++) Assert.AreEqual(28d, network.NodeAmounts[i], 1d);
        }

        [Test]
        // 一方向面は許可方向にのみ流れることを確認
        // A one-way face only flows in its allowed direction
        public void OneWayFaceTest()
        {
            // A→Bのみ許可。Bに液体があるがAへは逆流しない
            // Only A→B allowed; fluid in B must not flow back into A
            var network = Chain(2, 0.5, true, false, (1, 50, TestFluidId));
            Run(network, 100);

            Assert.AreEqual(0d, network.NodeAmounts[0], 1e-9);
            Assert.AreEqual(50d, network.NodeAmounts[1], 1e-9);
        }

        [Test]
        // 容量が異なるノード同士は充填率（水位）で釣り合うことを確認
        // Nodes with different capacities balance by fill rate (water level)
        public void FillRateBalanceAcrossCapacitiesTest()
        {
            var network = Build(new[] { (new Vector3Int(0, 0, 0), 1000d, 1000d, TestFluidId), (new Vector3Int(1, 0, 0), 100d, 0d, FluidMaster.EmptyFluidId) },
                new[] { (0, 1, 0.5, true, true) }, Array.Empty<IFluidBoundaryPort>());
            Run(network, 2000);

            // 充填率が一致する（tank≒909, pipe≒91）
            // Fill rates match (tank ≈ 909, pipe ≈ 91)
            Assert.AreEqual(network.FillRate(0), network.FillRate(1), 0.02d);
            Assert.AreEqual(1000d, network.NodeAmounts[0] + network.NodeAmounts[1], 1e-9);
        }

        [Test]
        // 境界ポートはノードを排水し、速度は面上限を超えず、拒否時は速度が殺されることを確認
        // A boundary port drains its node, velocity never exceeds the cap, and rejection kills the velocity
        public void BoundaryPortDrainAndFeedbackTest()
        {
            var acceptingPort = new TestBoundaryPort(0, 0.5, acceptAll: true);
            var network = Build(new[] { (new Vector3Int(0, 0, 0), 100d, 20d, TestFluidId) }, Array.Empty<(int, int, double, bool, bool)>(), new IFluidBoundaryPort[] { acceptingPort });
            var scratch = new FluidSimulationScratch(network);
            for (var i = 0; i < 1000; i++)
            {
                FluidSimulationStepper.Step(network, scratch);
                Assert.LessOrEqual(acceptingPort.Velocity, 0.5 + 1e-9);
            }

            // 排水は残量に比例して漸減するため指数テールを持つ。1000tickでほぼ0まで排水される
            // Draining decays proportionally to the remaining amount (exponential tail); after 1000 ticks it is effectively empty
            Assert.AreEqual(0d, network.NodeAmounts[0], 1e-6);
            Assert.AreEqual(20d, acceptingPort.DeliveredTotal, 1e-6);

            // 全拒否する境界では、ノード残量が変わらず速度も0に収束する
            // A rejecting boundary leaves the node untouched and the velocity converges to zero
            var rejectingPort = new TestBoundaryPort(0, 0.5, acceptAll: false);
            var network2 = Build(new[] { (new Vector3Int(0, 0, 0), 100d, 20d, TestFluidId) }, Array.Empty<(int, int, double, bool, bool)>(), new IFluidBoundaryPort[] { rejectingPort });
            Run(network2, 100);

            Assert.AreEqual(20d, network2.NodeAmounts[0], 1e-9);
            Assert.AreEqual(0d, rejectingPort.Velocity, 1e-9);
        }

        [Test]
        // 異種流体が向かい合う面は閉面となり、双方が変化しないことを確認
        // A face between mismatched fluids closes and neither side changes
        public void MismatchedFluidClosedFaceTest()
        {
            var network = Chain(2, 0.5, true, true, (0, 30, new FluidId(1)), (1, 10, new FluidId(2)));
            Run(network, 100);

            Assert.AreEqual(30d, network.NodeAmounts[0], 1e-9);
            Assert.AreEqual(10d, network.NodeAmounts[1], 1e-9);
        }

        [Test]
        // Step が走査のついでに残すハッシュは、全走査で求めた値と一致する
        // The hash the step folds in during its own walk equals the one from a full walk
        public void StepHashMatchesFullRecompute()
        {
            var network = Chain(5, 0.5, true, true, (0, 100, TestFluidId), (3, 40, TestFluidId));
            Run(network, 50);
            var stepped = network.LastStateHash;
            network.RecomputeStateHash();
            Assert.AreEqual(stepped, network.LastStateHash);
        }

        private static void Run(FluidSimNetwork network, int ticks)
        {
            var scratch = new FluidSimulationScratch(network);
            for (var i = 0; i < ticks; i++) FluidSimulationStepper.Step(network, scratch);
        }

        // x軸に並ぶ容量100のノード列を同じ面上限・向き許可で繋ぎ、指定ノードへ初期量を入れる
        // A row of capacity-100 nodes along x, joined by faces with one cap and direction permission, with initial amounts on the given nodes
        private static FluidSimNetwork Chain(int nodeCount, double faceCap, bool allowAToB, bool allowBToA, params (int node, double amount, FluidId fluidId)[] contents)
        {
            var nodes = new (Vector3Int, double, double, FluidId)[nodeCount];
            for (var i = 0; i < nodeCount; i++) nodes[i] = (new Vector3Int(i, 0, 0), 100d, 0d, FluidMaster.EmptyFluidId);
            foreach (var (node, amount, fluidId) in contents) nodes[node] = (nodes[node].Item1, 100d, amount, fluidId);
            var faces = new (int, int, double, bool, bool)[nodeCount - 1];
            for (var i = 0; i < faces.Length; i++) faces[i] = (i, i + 1, faceCap, allowAToB, allowBToA);
            return Build(nodes, faces, Array.Empty<IFluidBoundaryPort>());
        }

        private static FluidSimNetwork Build((Vector3Int position, double capacity, double amount, FluidId fluidId)[] nodes, (int a, int b, double cap, bool allowAToB, bool allowBToA)[] faces, IFluidBoundaryPort[] ports)
        {
            var positions = new Vector3Int[nodes.Length];
            var capacities = new double[nodes.Length];
            var amounts = new double[nodes.Length];
            var fluidIds = new FluidId[nodes.Length];
            for (var i = 0; i < nodes.Length; i++) (positions[i], capacities[i], amounts[i], fluidIds[i]) = nodes[i];
            var faceA = new int[faces.Length];
            var faceB = new int[faces.Length];
            var caps = new double[faces.Length];
            var allowAToB = new bool[faces.Length];
            var allowBToA = new bool[faces.Length];
            for (var i = 0; i < faces.Length; i++) (faceA[i], faceB[i], caps[i], allowAToB[i], allowBToA[i]) = faces[i];
            return new FluidSimNetwork(positions, capacities, amounts, fluidIds, faceA, faceB, caps, new double[faces.Length], allowAToB, allowBToA, ports);
        }

        // テスト用の境界ポート。acceptAll=falseなら全量拒否する
        // Test boundary port; rejects everything when acceptAll is false
        private class TestBoundaryPort : IFluidBoundaryPort
        {
            public int NodeIndex { get; }
            public double FlowCapacityPerTick { get; }
            public double Velocity { get; set; }
            public double DeliveredTotal { get; private set; }

            private readonly bool _acceptAll;

            public TestBoundaryPort(int nodeIndex, double flowCapacityPerTick, bool acceptAll)
            {
                NodeIndex = nodeIndex;
                FlowCapacityPerTick = flowCapacityPerTick;
                _acceptAll = acceptAll;
            }

            public FluidStack Deliver(FluidStack fluidStack)
            {
                if (!_acceptAll) return fluidStack;
                DeliveredTotal += fluidStack.Amount;
                return new FluidStack(0, fluidStack.FluidId);
            }
        }
    }
}
