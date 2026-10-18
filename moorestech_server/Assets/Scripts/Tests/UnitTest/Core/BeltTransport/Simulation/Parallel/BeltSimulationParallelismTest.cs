using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Core.BeltTransport;
using NUnit.Framework;
using Tests.UnitTest.Core.BeltTransport.Simulation.RandomNetwork;

namespace Tests.UnitTest.Core.BeltTransport.Simulation.Parallel
{
    // 並列可能性の前提条件(ADR 0084 §3)。「並列に動いた」ことでなく、段数一定・段階内の順序独立・並列実行一致・アロケーション0を測る
    // Preconditions of parallelizability (ADR 0084 §3): not "it ran in parallel" but a constant stage count, order independence within a stage, parallel equality and zero allocation
    // 下流から順に更新する実装は、段数が直列長に比例して増えるか(段数一定で落ちる)、1段の中に順序依存を隠す(順序入れ替えで落ちる)
    // An implementation that updates downstream first either grows its stage count with the chain (fails the constant count) or hides an order dependence inside a stage (fails the reorder)
    public class BeltSimulationParallelismTest
    {
        private const int ExpectedStageCount = 7;
        private const int TickCount = 200;
        private static readonly int[] Seeds = { 1, 2, 3, 5, 8, 13, 21, 34, 55, 89 };

        [Test]
        public void 段数は網の大きさによらず一定()
        {
            foreach (var seed in Seeds)
            {
                var network = new RandomBeltNetwork(seed);
                Assert.AreEqual(ExpectedStageCount, new BeltSimulation(network.Segments).Stages.Count, $"seed {seed} with {network.Segments.Count} segments");
            }
            Assert.AreEqual(ExpectedStageCount, new BeltSimulation(Array.Empty<BeltConveyorSegment>()).Stages.Count, "empty network");
        }

        // 各段階の要素を逆順・乱数順で実行しても、逐次と毎tick同じ状態になる
        // Running each stage's elements in reverse or shuffled order leaves the same state every tick as the sequential run
        [Test]
        public void 段階内の要素順を入れ替えても結果が同じ()
        {
            foreach (var seed in Seeds)
            {
                AssertSameAsSequential(seed, "reverse", (stage, random) =>
                {
                    for (var i = stage.Count - 1; 0 <= i; i--) stage.Run(i);
                });
                AssertSameAsSequential(seed, "shuffled", (stage, random) =>
                {
                    var order = new int[stage.Count];
                    for (var i = 0; i < order.Length; i++) order[i] = i;
                    for (var i = order.Length - 1; 0 < i; i--)
                    {
                        var j = random.Next(i + 1);
                        (order[i], order[j]) = (order[j], order[i]);
                    }
                    foreach (var i in order) stage.Run(i);
                });
            }
        }

        // 各段階を Parallel.For で回しても逐次と同じ状態になる(データ競合の検出は確率的なので順序入れ替えと組で使う)
        // Running each stage with Parallel.For leaves the same state as the sequential run (race detection is probabilistic, so this pairs with the reorder test)
        [Test]
        public void 段階を並列実行しても結果が同じ()
        {
            foreach (var seed in Seeds)
            {
                AssertSameAsSequential(seed, "parallel", (stage, random) =>
                {
                    System.Threading.Tasks.Parallel.For(0, stage.Count, stage.Run);
                });
            }
        }

        // 定常状態の tick はアロケーションしない。LINQ や List 生成の混入を止める
        // A steady-state tick allocates nothing; keeps LINQ and list creation out of the hot path
        // Unity の Mono では GC.GetAllocatedBytesForCurrentThread が常に 0 を返すので、Profiler の Mono 使用量を計器にする。計器が生きていることを配列確保で先に確かめる
        // Unity's Mono always returns 0 from GC.GetAllocatedBytesForCurrentThread, so the Profiler's Mono used size is the meter; an array allocation first proves the meter is alive
        [Test]
        public void 定常状態のtickはアロケーションしない()
        {
            var network = new RandomBeltNetwork(Seeds[0]);
            var simulation = new BeltSimulation(network.Segments);
            for (var tick = 0; tick < 50; tick++)
            {
                network.PrepareTick();
                simulation.Tick();
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            var probeBefore = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
            var probe = new int[4096];
            Assert.Less(probeBefore, UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong(), $"allocation meter is alive ({probe.Length})");

            for (var tick = 0; tick < 50; tick++)
            {
                network.PrepareTick();
                var before = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                simulation.Tick();
                Assert.AreEqual(0, UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() - before, $"tick {tick} allocated");
            }
        }

        // 同じシードの網を2つ作り、片方は Tick()、もう片方は段階ごとに runner で回して、毎 tick の状態文字列を比べる
        // Build two networks from the same seed, run one with Tick() and the other stage by stage through the runner, and compare the state string every tick
        private static void AssertSameAsSequential(int seed, string label, StageRunner runner)
        {
            var sequentialNetwork = new RandomBeltNetwork(seed);
            var sequential = new BeltSimulation(sequentialNetwork.Segments);
            var candidateNetwork = new RandomBeltNetwork(seed);
            var candidate = new BeltSimulation(candidateNetwork.Segments);
            var random = new Random(seed);
            for (var tick = 1; tick <= TickCount; tick++)
            {
                sequentialNetwork.PrepareTick();
                candidateNetwork.PrepareTick();
                sequential.Tick();
                foreach (var stage in candidate.Stages) runner(stage, random);
                Assert.AreEqual(
                    RandomBeltNetworkInspector.AssertInvariants(sequentialNetwork, $"seed {seed} sequential tick {tick}"),
                    RandomBeltNetworkInspector.AssertInvariants(candidateNetwork, $"seed {seed} {label} tick {tick}"),
                    $"seed {seed} {label} diverged at tick {tick}");
            }
        }

        private delegate void StageRunner(IBeltTickStage stage, Random random);
    }
}
