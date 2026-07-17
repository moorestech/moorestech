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
            var nodeA = new FluidSimNode(new Vector3Int(0, 0, 0), 100) { Amount = 30, FluidId = TestFluidId };
            var nodeB = new FluidSimNode(new Vector3Int(1, 0, 0), 100);
            var face = CreateBidirectionalFace(nodeA, nodeB, 0.5);

            var nodes = new List<FluidSimNode> { nodeA, nodeB };
            var faces = new List<FluidSimFace> { face };
            var ports = new List<IFluidBoundaryPort>();

            for (var i = 0; i < 200; i++)
            {
                FluidSimulationStepper.Step(nodes, faces, ports);
            }

            Assert.AreEqual(15d, nodeA.Amount, 0.5d);
            Assert.AreEqual(15d, nodeB.Amount, 0.5d);
            Assert.AreEqual(30d, nodeA.Amount + nodeB.Amount, 1e-9);
            Assert.AreEqual(TestFluidId, nodeB.FluidId);
        }

        [Test]
        // チェーン全体で総量が保存され、全ノードが0以上容量以下に収まることを確認
        // The total is conserved across a chain and every node stays within [0, capacity]
        public void ChainConservationTest()
        {
            var nodes = new List<FluidSimNode>();
            var faces = new List<FluidSimFace>();
            for (var i = 0; i < 5; i++)
            {
                nodes.Add(new FluidSimNode(new Vector3Int(i, 0, 0), 100));
            }
            nodes[0].Amount = 100;
            nodes[0].FluidId = TestFluidId;
            nodes[3].Amount = 40;
            nodes[3].FluidId = TestFluidId;
            for (var i = 0; i < 4; i++)
            {
                faces.Add(CreateBidirectionalFace(nodes[i], nodes[i + 1], 0.5));
            }

            var ports = new List<IFluidBoundaryPort>();
            for (var i = 0; i < 500; i++)
            {
                FluidSimulationStepper.Step(nodes, faces, ports);
            }

            var total = 0d;
            foreach (var node in nodes)
            {
                total += node.Amount;
                Assert.GreaterOrEqual(node.Amount, 0);
                Assert.LessOrEqual(node.Amount, node.Capacity + 1e-9);
            }
            Assert.AreEqual(140d, total, 1e-9);

            // 最終的に全ノードが均等（各28）に収束する
            // Eventually every node converges to the same level (28 each)
            foreach (var node in nodes)
            {
                Assert.AreEqual(28d, node.Amount, 1d);
            }
        }

        [Test]
        // 一方向面は許可方向にのみ流れることを確認
        // A one-way face only flows in its allowed direction
        public void OneWayFaceTest()
        {
            var nodeA = new FluidSimNode(new Vector3Int(0, 0, 0), 100);
            var nodeB = new FluidSimNode(new Vector3Int(1, 0, 0), 100) { Amount = 50, FluidId = TestFluidId };

            // A→Bのみ許可。Bに液体があるがAへは逆流しない
            // Only A→B allowed; fluid in B must not flow back into A
            var face = new FluidSimFace(nodeA, nodeB, 0.5, 0) { AllowAToB = true };

            var nodes = new List<FluidSimNode> { nodeA, nodeB };
            var faces = new List<FluidSimFace> { face };
            var ports = new List<IFluidBoundaryPort>();

            for (var i = 0; i < 100; i++)
            {
                FluidSimulationStepper.Step(nodes, faces, ports);
            }

            Assert.AreEqual(0d, nodeA.Amount, 1e-9);
            Assert.AreEqual(50d, nodeB.Amount, 1e-9);
        }

        [Test]
        // 容量が異なるノード同士は充填率（水位）で釣り合うことを確認
        // Nodes with different capacities balance by fill rate (water level)
        public void FillRateBalanceAcrossCapacitiesTest()
        {
            var tank = new FluidSimNode(new Vector3Int(0, 0, 0), 1000) { Amount = 1000, FluidId = TestFluidId };
            var pipe = new FluidSimNode(new Vector3Int(1, 0, 0), 100);
            var face = CreateBidirectionalFace(tank, pipe, 0.5);

            var nodes = new List<FluidSimNode> { tank, pipe };
            var faces = new List<FluidSimFace> { face };
            var ports = new List<IFluidBoundaryPort>();

            for (var i = 0; i < 2000; i++)
            {
                FluidSimulationStepper.Step(nodes, faces, ports);
            }

            // 充填率が一致する（tank≒909, pipe≒91）
            // Fill rates match (tank ≈ 909, pipe ≈ 91)
            Assert.AreEqual(tank.FillRate, pipe.FillRate, 0.02d);
            Assert.AreEqual(1000d, tank.Amount + pipe.Amount, 1e-9);
        }

        [Test]
        // 境界ポートはノードを排水し、速度は面上限を超えず、拒否時は速度が殺されることを確認
        // A boundary port drains its node, velocity never exceeds the cap, and rejection kills the velocity
        public void BoundaryPortDrainAndFeedbackTest()
        {
            var node = new FluidSimNode(new Vector3Int(0, 0, 0), 100) { Amount = 20, FluidId = TestFluidId };
            var acceptingPort = new TestBoundaryPort(node, 0.5, acceptAll: true);

            var nodes = new List<FluidSimNode> { node };
            var faces = new List<FluidSimFace>();
            var ports = new List<IFluidBoundaryPort> { acceptingPort };

            for (var i = 0; i < 1000; i++)
            {
                FluidSimulationStepper.Step(nodes, faces, ports);
                Assert.LessOrEqual(acceptingPort.Velocity, 0.5 + 1e-9);
            }

            // 排水は残量に比例して漸減するため指数テールを持つ。1000tickでほぼ0まで排水される
            // Draining decays proportionally to the remaining amount (exponential tail); after 1000 ticks it is effectively empty
            Assert.AreEqual(0d, node.Amount, 1e-6);
            Assert.AreEqual(20d, acceptingPort.DeliveredTotal, 1e-6);

            // 全拒否する境界では、ノード残量が変わらず速度も0に収束する
            // A rejecting boundary leaves the node untouched and the velocity converges to zero
            var node2 = new FluidSimNode(new Vector3Int(0, 0, 0), 100) { Amount = 20, FluidId = TestFluidId };
            var rejectingPort = new TestBoundaryPort(node2, 0.5, acceptAll: false);
            var nodes2 = new List<FluidSimNode> { node2 };
            var ports2 = new List<IFluidBoundaryPort> { rejectingPort };

            for (var i = 0; i < 100; i++)
            {
                FluidSimulationStepper.Step(nodes2, faces, ports2);
            }

            Assert.AreEqual(20d, node2.Amount, 1e-9);
            Assert.AreEqual(0d, rejectingPort.Velocity, 1e-9);
        }

        [Test]
        // 異種流体が向かい合う面は閉面となり、双方が変化しないことを確認
        // A face between mismatched fluids closes and neither side changes
        public void MismatchedFluidClosedFaceTest()
        {
            var nodeA = new FluidSimNode(new Vector3Int(0, 0, 0), 100) { Amount = 30, FluidId = new FluidId(1) };
            var nodeB = new FluidSimNode(new Vector3Int(1, 0, 0), 100) { Amount = 10, FluidId = new FluidId(2) };
            var face = CreateBidirectionalFace(nodeA, nodeB, 0.5);

            var nodes = new List<FluidSimNode> { nodeA, nodeB };
            var faces = new List<FluidSimFace> { face };
            var ports = new List<IFluidBoundaryPort>();

            for (var i = 0; i < 100; i++)
            {
                FluidSimulationStepper.Step(nodes, faces, ports);
            }

            Assert.AreEqual(30d, nodeA.Amount, 1e-9);
            Assert.AreEqual(10d, nodeB.Amount, 1e-9);
        }

        private static FluidSimFace CreateBidirectionalFace(FluidSimNode nodeA, FluidSimNode nodeB, double flowCapacityPerTick)
        {
            return new FluidSimFace(nodeA, nodeB, flowCapacityPerTick, 0)
            {
                AllowAToB = true,
                AllowBToA = true,
            };
        }

        // テスト用の境界ポート。acceptAll=falseなら全量拒否する
        // Test boundary port; rejects everything when acceptAll is false
        private class TestBoundaryPort : IFluidBoundaryPort
        {
            public FluidSimNode PipeNode { get; }
            public double FlowCapacityPerTick { get; }
            public double Velocity { get; set; }
            public double DeliveredTotal { get; private set; }

            private readonly bool _acceptAll;

            public TestBoundaryPort(FluidSimNode pipeNode, double flowCapacityPerTick, bool acceptAll)
            {
                PipeNode = pipeNode;
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
