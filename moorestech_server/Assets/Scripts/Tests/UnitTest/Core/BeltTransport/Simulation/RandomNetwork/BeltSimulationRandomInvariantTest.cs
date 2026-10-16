using System.Collections.Generic;
using System.Linq;
using Core.BeltTransport;
using NUnit.Framework;

namespace Tests.UnitTest.Core.BeltTransport.Simulation.RandomNetwork
{
    // 固定シードで生成した網を数百tick回し、毎tickの不変条件と同一シードでの決定論を検証する
    // Run seeded random networks for hundreds of ticks, checking invariants every tick and determinism per seed
    public class BeltSimulationRandomInvariantTest
    {
        private const int TickCount = 300;

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        public void ランダムな網でも保存則と配置の不変条件を毎tick満たし同じシードなら同じ状態になる(int seed)
        {
            var first = Run(seed, out var firstNetwork);
            var second = Run(seed, out _);
            CollectionAssert.AreEqual(first, second, "determinism");

            // 検査が空振りしていないことを確かめる
            // Make sure the check is not vacuous
            Assert.Greater(firstNetwork.InsertedSerials.Count, 0, "inserted");
            Assert.Greater(firstNetwork.Sinks.Sum(s => s.Delivered.Count), 0, "delivered");
            Assert.IsTrue(firstNetwork.Segments.Any(s => s.Kind == BeltSegmentKind.Merge), "has merge");
            Assert.IsTrue(firstNetwork.Segments.Any(s => s.Kind == BeltSegmentKind.Branch), "has branch");
        }

        private static List<string> Run(int seed, out RandomBeltNetwork network)
        {
            network = new RandomBeltNetwork(seed);
            var simulation = new BeltSimulation(network.Segments);
            var snapshots = new List<string> { RandomBeltNetworkInspector.AssertInvariants(network, $"seed {seed} initial") };
            for (var tick = 1; tick <= TickCount; tick++)
            {
                // tick境界の供給後と、tick後の両方で検査する
                // Check both after the boundary supply and after the tick
                network.PrepareTick();
                RandomBeltNetworkInspector.AssertInvariants(network, $"seed {seed} boundary {tick}");
                simulation.Tick();
                snapshots.Add(RandomBeltNetworkInspector.AssertInvariants(network, $"seed {seed} tick {tick}"));
            }
            return snapshots;
        }
    }
}
