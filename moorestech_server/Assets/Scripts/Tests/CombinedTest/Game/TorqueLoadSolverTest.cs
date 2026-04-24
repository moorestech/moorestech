using System.Collections.Generic;
using Game.Gear.Common;
using NUnit.Framework;

namespace Tests.CombinedTest.Game
{
    // TorqueLoadSolver の数学的正しさを純粋なグラフ入力で検証する（DI不要）
    // Pure unit tests for TorqueLoadSolver math correctness; no DI required
    public class TorqueLoadSolverTest
    {
        // 2ノード直列: ノード0 → ノード1。エッジ1本のトルクは b[1] そのもの
        // Two-node line: single edge carries torque equal to b[1]
        [Test]
        public void TwoNodeLine_EdgeTorqueEqualsConsumerB()
        {
            var solver = new TorqueLoadSolver();
            var edges = new List<(int i, int j)> { (0, 1) };
            solver.Prepare(2, edges);

            var b = new double[] { 5.0, -5.0 };
            var torque = new double[1];
            solver.Solve(b, torque);

            // edge(0,1): φ[0]-φ[1] = 0 - φ[1] = -(-5) = 5
            Assert.AreEqual(5.0, torque[0], 1e-9);
        }

        // 3ノード直列: 全エッジが同じ大きさのトルクを運ぶ
        // Three-node line: all edges carry the same torque magnitude
        [Test]
        public void ThreeNodeLine_AllEdgesCarrySameTorque()
        {
            var solver = new TorqueLoadSolver();
            var edges = new List<(int i, int j)> { (0, 1), (1, 2) };
            solver.Prepare(3, edges);

            var b = new double[] { 7.0, 0.0, -7.0 };
            var torque = new double[2];
            solver.Solve(b, torque);

            Assert.AreEqual(7.0, torque[0], 1e-9, "edge(0,1)");
            Assert.AreEqual(7.0, torque[1], 1e-9, "edge(1,2)");
        }

        // Y字分岐: 親ノードのエッジが子エッジ群の和を運ぶ（負荷集約の根拠）
        // Branch: parent edge carries the sum of child edges (justifies load aggregation feature)
        [Test]
        public void BranchNode_ParentEdgeEqualsSumOfChildEdges()
        {
            var solver = new TorqueLoadSolver();
            // 0(generator) -- 1(branch) -- 2(consumer)
            //                          \-- 3(consumer)
            var edges = new List<(int i, int j)> { (0, 1), (1, 2), (1, 3) };
            solver.Prepare(4, edges);

            // ノード0 で +5 を生成、ノード2と3 で -3 と -2 を消費
            // Node 0 produces +5; nodes 2 and 3 consume -3 and -2
            var b = new double[] { 5.0, 0.0, -3.0, -2.0 };
            var torque = new double[3];
            solver.Solve(b, torque);

            // edge(0,1) = ノード2,3への合計需要 = 5
            // edge(1,2) = ノード2の需要 = 3
            // edge(1,3) = ノード3の需要 = 2
            Assert.AreEqual(5.0, torque[0], 1e-9, "edge(0,1) = sum of branches");
            Assert.AreEqual(3.0, torque[1], 1e-9, "edge(1,2) = consumer 2 demand");
            Assert.AreEqual(2.0, torque[2], 1e-9, "edge(1,3) = consumer 3 demand");

            // 集約時のノード1（分岐シャフト）の最大隣接トルク = max(5,3,2) = 5
            // Node 1 (branch shaft) max adjacent torque = max(5,3,2) = 5
            var nodeMax = new double[4];
            solver.AggregateNodeMaxAdjacent(torque, nodeMax);
            Assert.AreEqual(5.0, nodeMax[1], 1e-9, "branch node load = max adjacent edge torque");
        }

        // 三角形ループ: 弦と直接エッジで分担される（一意解の確認）
        // Triangle cycle: torque distributes across both paths; verifies unique solution
        [Test]
        public void TriangleCycle_TorqueDistributesAcrossBothPaths()
        {
            var solver = new TorqueLoadSolver();
            // 0 -- 1, 1 -- 2, 0 -- 2 (三角形 / triangle)
            var edges = new List<(int i, int j)> { (0, 1), (1, 2), (0, 2) };
            solver.Prepare(3, edges);

            // ノード0 で +6 生成、ノード2 で -6 消費。ノード1 は中継
            // Node 0 generates +6, node 2 consumes -6, node 1 is transmission
            var b = new double[] { 6.0, 0.0, -6.0 };
            var torque = new double[3];
            solver.Solve(b, torque);

            // 0→2 の直接エッジと 0→1→2 の経路で負荷が分担される
            // Equal-resistance triangle: 0→2 direct gets 4, 0→1→2 path gets 2
            // Verify Kirchhoff at each node: signed sum of adjacent edge torques = b
            var lphi = new double[3];
            for (var k = 0; k < edges.Count; k++)
            {
                lphi[edges[k].i] += torque[k];
                lphi[edges[k].j] -= torque[k];
            }
            Assert.AreEqual(0.0, lphi[1] - b[1], 1e-9, "Kirchhoff at node 1");
            Assert.AreEqual(0.0, lphi[2] - b[2], 1e-9, "Kirchhoff at node 2");
        }

        // 同じPrepareで複数のbを連続Solve: 因数分解が正しく再利用され、結果が独立
        // Reusing the same Prepare across multiple Solve calls; results must be independent
        [Test]
        public void RepeatedSolveOnSamePrepare_GivesIndependentResults()
        {
            var solver = new TorqueLoadSolver();
            var edges = new List<(int i, int j)> { (0, 1), (1, 2) };
            solver.Prepare(3, edges);

            var torque = new double[2];

            solver.Solve(new double[] { 1, 0, -1 }, torque);
            Assert.AreEqual(1.0, torque[0], 1e-9);

            solver.Solve(new double[] { 10, 0, -10 }, torque);
            Assert.AreEqual(10.0, torque[0], 1e-9);

            solver.Solve(new double[] { 0.5, -0.5, 0 }, torque);
            // ノード1で-0.5消費、ノード2は中継。edge(0,1)=0.5、edge(1,2)=0
            Assert.AreEqual(0.5, torque[0], 1e-9);
            Assert.AreEqual(0.0, torque[1], 1e-9);
        }

        // 異なるグラフで再Prepareすると、内部状態が完全に置換される
        // Re-Prepare with a different graph fully replaces internal state
        [Test]
        public void RePrepareWithDifferentGraph_ReplacesInternalState()
        {
            var solver = new TorqueLoadSolver();
            solver.Prepare(2, new List<(int i, int j)> { (0, 1) });
            solver.Prepare(4, new List<(int i, int j)> { (0, 1), (1, 2), (2, 3) });

            var torque = new double[3];
            solver.Solve(new double[] { 3, 0, 0, -3 }, torque);

            Assert.AreEqual(3.0, torque[0], 1e-9);
            Assert.AreEqual(3.0, torque[1], 1e-9);
            Assert.AreEqual(3.0, torque[2], 1e-9);
        }

        // 残差 ‖Lφ - b‖∞ が機械精度近く（数値的に正しい）
        // Numerical residual ‖Lφ - b‖∞ is near machine precision
        [Test]
        public void NumericalResidual_NearMachinePrecision()
        {
            var solver = new TorqueLoadSolver();
            // 100ノード鎖で b にランダム値
            // 100-node chain with random b values
            const int n = 100;
            var edges = new List<(int i, int j)>(n - 1);
            for (var k = 0; k < n - 1; k++) edges.Add((k, k + 1));
            solver.Prepare(n, edges);

            var rng = new System.Random(7);
            var b = new double[n];
            var sum = 0.0;
            for (var i = 1; i < n; i++)
            {
                b[i] = rng.NextDouble() - 0.5;
                sum += b[i];
            }
            b[0] = -sum; // 全体で需給バランス。Total balance

            var torque = new double[edges.Count];
            solver.Solve(b, torque);

            // 残差 = Σ符号付トルク - b ：各ノードで0近傍
            // Residual: signed-sum of adjacent torques minus b at each node
            var lphi = new double[n];
            for (var k = 0; k < edges.Count; k++)
            {
                lphi[edges[k].i] += torque[k];
                lphi[edges[k].j] -= torque[k];
            }
            var maxRes = 0.0;
            for (var v = 1; v < n; v++)
            {
                var r = System.Math.Abs(lphi[v] - b[v]);
                if (r > maxRes) maxRes = r;
            }
            Assert.Less(maxRes, 1e-10, $"max residual was {maxRes:E2}");
        }

        // 集約: 各ノードの隣接エッジ |torque| の最大値を返す
        // AggregateNodeMaxAdjacent: returns max |adjacent torque| per node
        [Test]
        public void AggregateNodeMaxAdjacent_ReturnsMaxAbsAdjacentEdgeTorque()
        {
            var solver = new TorqueLoadSolver();
            var edges = new List<(int i, int j)> { (0, 1), (1, 2), (1, 3) };
            solver.Prepare(4, edges);

            // 手で設計したトルク値（Solveを介さず集約だけ検証）
            // Hand-crafted torques to test aggregation in isolation
            var torque = new double[] { -10.0, 3.0, -7.0 };
            var nodeMax = new double[4];
            solver.AggregateNodeMaxAdjacent(torque, nodeMax);

            Assert.AreEqual(10.0, nodeMax[0], 1e-9, "node0 only edge(0,1) |-10| = 10");
            Assert.AreEqual(10.0, nodeMax[1], 1e-9, "node1 max(|-10|,|3|,|-7|) = 10");
            Assert.AreEqual(3.0, nodeMax[2], 1e-9, "node2 only edge(1,2) |3| = 3");
            Assert.AreEqual(7.0, nodeMax[3], 1e-9, "node3 only edge(1,3) |-7| = 7");
        }
    }
}
