using System;
using Core.Master;
using Game.Fluid;
using Game.Fluid.Simulation;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game
{
    /// <summary>
    ///     Stepper が範囲をいくつに分けて回しても同じ結果になること(並列可能な構造)と、整数化で決めた規則(異種流体は番号最小の面・境界の切り捨て)を確認する(ADR 0090)。
    ///     Verifies that the stepper gives the same result however its ranges are split (parallelizable structure) and the rules fixed with the integer conversion (lowest-index face wins among mismatched fluids, boundary flooring) (ADR 0090).
    /// </summary>
    public class FluidSimulationStepperPartitionTest
    {
        private static readonly FluidId Water = new(1);
        private static readonly FluidId Oil = new(2);
        private const long Unit = FluidUnits.UnitsPerAmount;

        [Test]
        // 同じ網を全範囲1回と2分割・3分割・7分割で回しても、量・流体ID・面速度・ポート速度・ハッシュが完全一致する
        // Running the same network whole and in 2, 3 and 7 parts yields identical amounts, fluid ids, face and port velocities and hash
        public void 範囲を分けて回しても結果が完全一致するTest()
        {
            var whole = BuildBranchingNetwork(out var wholePorts);
            Run(whole, 300, 1);

            foreach (var partitionCount in new[] { 2, 3, 7 })
            {
                var split = BuildBranchingNetwork(out var splitPorts);
                Run(split, 300, partitionCount);

                CollectionAssert.AreEqual(whole.NodeAmounts, split.NodeAmounts, $"amounts with {partitionCount} parts");
                CollectionAssert.AreEqual(whole.NodeFluidIds, split.NodeFluidIds, $"fluid ids with {partitionCount} parts");
                CollectionAssert.AreEqual(whole.FaceVelocities, split.FaceVelocities, $"face velocities with {partitionCount} parts");
                for (var p = 0; p < wholePorts.Length; p++)
                {
                    Assert.AreEqual(wholePorts[p].Velocity, splitPorts[p].Velocity, $"port {p} velocity with {partitionCount} parts");
                    Assert.AreEqual(wholePorts[p].DeliveredTotal, splitPorts[p].DeliveredTotal, $"port {p} delivered with {partitionCount} parts");
                }
                Assert.AreEqual(whole.LastStateHash, split.LastStateHash, $"hash with {partitionCount} parts");
            }
        }

        [Test]
        // 空のノードへ同じtickに水と油が入ろうとしたら、番号の小さい面の流体(水)だけが入り、油の面は閉じる
        // When water and oil try to enter an empty node in the same tick, only the fluid of the lower-index face (water) enters and the oil face closes
        public void 空ノードへの異種流体は番号最小の面の流体だけが入るTest()
        {
            // 座標順で面0 = (0,0,0)-(1,0,0) が水、面1 = (1,0,0)-(2,0,0) が油。中央(1,0,0)が空
            // By position order face 0 = (0,0,0)-(1,0,0) carries water and face 1 = (1,0,0)-(2,0,0) oil; the middle node (1,0,0) is empty
            var network = FluidSimulationStepperTest.Build(
                new[]
                {
                    (new Vector3Int(0, 0, 0), 100 * Unit, 50 * Unit, Water),
                    (new Vector3Int(1, 0, 0), 100 * Unit, 0L, FluidMaster.EmptyFluidId),
                    (new Vector3Int(2, 0, 0), 100 * Unit, 50 * Unit, Oil),
                },
                new[] { (0, 1, 500L, true, true), (1, 2, 500L, true, true) },
                Array.Empty<IFluidBoundaryPort>());
            Run(network, 50, 1);

            Assert.AreEqual(Water, network.NodeFluidIds[1]);
            Assert.Greater(network.NodeAmounts[1], 0);
            Assert.AreEqual(50 * Unit, network.NodeAmounts[2], "oil never enters");
            Assert.AreEqual(Oil, network.NodeFluidIds[2]);
            Assert.AreEqual(100 * Unit, network.NodeAmounts[0] + network.NodeAmounts[1] + network.NodeAmounts[2]);
        }

        [Test]
        // 機械からの流入は 1/1000 単位に切り捨てられ、端数は残りとして機械へ返る。受け入れた単位だけが網に足される
        // Inflow from a machine is floored to 1/1000 units and the fraction returns to the machine as remainder; only the accepted units land in the network
        public void 境界からの流入は単位に切り捨てて端数を返すTest()
        {
            var network = FluidSimulationStepperTest.Chain(1, 500, true, true);

            var remainder = network.AddExternal(0, new FluidStack(0.0005, Water), out var accepted);
            Assert.AreEqual(0, accepted);
            Assert.AreEqual(0.0005, remainder.Amount, 1e-15);
            Assert.AreEqual(0, network.NodeAmounts[0]);
            Assert.AreEqual(FluidMaster.EmptyFluidId, network.NodeFluidIds[0]);

            remainder = network.AddExternal(0, new FluidStack(1.2345, Water), out accepted);
            Assert.AreEqual(1234, accepted);
            Assert.AreEqual(1.2345 - 1.234, remainder.Amount, 1e-12);
            Assert.AreEqual(1234, network.NodeAmounts[0]);
            Assert.AreEqual(Water, network.NodeFluidIds[0]);
        }

        private static void Run(FluidSimNetwork network, int ticks, int partitionCount)
        {
            var scratch = new FluidSimulationScratch(network);
            for (var i = 0; i < ticks; i++) FluidSimulationStepper.Step(network, scratch, partitionCount);
        }

        // 分岐と合流のある網: 幹 0-1-2-3、枝 1-4、4-5、2-6。水と油の源、一方向面、ポート3つ(受け入れ・拒否・受け入れ)
        // A network with branches and merges: trunk 0-1-2-3, branches 1-4, 4-5, 2-6; water and oil sources, a one-way face and three ports (accept, reject, accept)
        private static FluidSimNetwork BuildBranchingNetwork(out FluidSimulationStepperTest.TestBoundaryPort[] ports)
        {
            ports = new[]
            {
                new FluidSimulationStepperTest.TestBoundaryPort(3, 300, acceptAll: true),
                new FluidSimulationStepperTest.TestBoundaryPort(3, 200, acceptAll: false),
                new FluidSimulationStepperTest.TestBoundaryPort(5, 150, acceptAll: true),
            };
            return FluidSimulationStepperTest.Build(
                new[]
                {
                    (new Vector3Int(0, 0, 0), 100 * Unit, 100 * Unit, Water),
                    (new Vector3Int(1, 0, 0), 100 * Unit, 0L, FluidMaster.EmptyFluidId),
                    (new Vector3Int(2, 0, 0), 500 * Unit, 70 * Unit, Water),
                    (new Vector3Int(3, 0, 0), 100 * Unit, 0L, FluidMaster.EmptyFluidId),
                    (new Vector3Int(1, 1, 0), 100 * Unit, 0L, FluidMaster.EmptyFluidId),
                    (new Vector3Int(1, 2, 0), 50 * Unit, 0L, FluidMaster.EmptyFluidId),
                    (new Vector3Int(2, 1, 0), 100 * Unit, 60 * Unit, Oil),
                },
                new[]
                {
                    (0, 1, 500L, true, true),
                    (1, 2, 500L, true, true),
                    (2, 3, 400L, true, false),
                    (1, 4, 300L, true, true),
                    (4, 5, 300L, true, true),
                    (2, 6, 500L, true, true),
                },
                ports);
        }
    }
}
