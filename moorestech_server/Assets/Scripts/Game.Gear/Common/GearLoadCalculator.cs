using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.Gear.Common
{
    // 歯車ネットワーク上の各ブロックを通過するトルクを Laplacian + 疎コレスキー解で計算し、
    // 結果を各 IGearEnergyTransformer に CurrentLoadTorque として書き戻す静的ユーティリティ
    // Static utility that solves per-edge torque flow via Laplacian + sparse Cholesky and
    // writes the max-adjacent value back to each IGearEnergyTransformer.CurrentLoadTorque
    internal static class GearLoadCalculator
    {
        // ネットワーク単位の計算状態。トポロジ未変更ならソルバを使い回す
        // Per-network cached state. Reuse solver while topology is unchanged
        public sealed class State
        {
            public long Version = -1;
            public TorqueLoadSolver Solver;
            public List<IGearEnergyTransformer> OrderedNodes;
            public double[] B;
            public double[] Torque;
            public double[] NodeMax;
        }

        // ネットワーク全ノードに対して負荷を計算し、CurrentLoadTorque に書き戻す
        // Compute load for every node in the network and push it to each transformer's CurrentLoadTorque
        public static void ComputeAndDistribute(
            IReadOnlyList<IGearGenerator> generators,
            IReadOnlyList<IGearEnergyTransformer> nonGenerators,
            State state)
        {
            var nodes = CollectNodes(generators, nonGenerators);
            if (nodes.Count < 2)
            {
                ResetToZero(state, nodes, signature: 0L);
                return;
            }

            var (edges, idToIndex, signature) = BuildGraph(nodes);

            // CSparse は nnz=0 で内部配列を null 初期化するので Cholesky.Create が NRE になる前に弾く
            // Guard up-front: CSparse leaves internal arrays null when nnz=0 which would crash Cholesky.Create
            if (edges.Count == 0)
            {
                ResetToZero(state, nodes, signature);
                return;
            }

            // 過渡状態で非連結なら縮約Laplacianが非SPDになり Cholesky が失敗するので事前に弾く
            // Guard non-connected transient state which would yield a non-SPD reduced Laplacian and fail Cholesky
            if (!IsConnected(nodes.Count, edges))
            {
                ResetToZero(state, nodes, signature);
                return;
            }

            EnsureSolverReady(state, nodes, edges, idToIndex, signature);
            FillBVector(state);
            state.Solver.Solve(state.B, state.Torque);
            state.Solver.AggregateNodeMaxAdjacent(state.Torque, state.NodeMax);
            Distribute(state);
        }

        #region Internal

        private static List<IGearEnergyTransformer> CollectNodes(
            IReadOnlyList<IGearGenerator> generators,
            IReadOnlyList<IGearEnergyTransformer> nonGenerators)
        {
            var nodes = new List<IGearEnergyTransformer>(generators.Count + nonGenerators.Count);
            foreach (var g in generators) nodes.Add(g);
            foreach (var t in nonGenerators) nodes.Add(t);
            return nodes;
        }

        private static (List<(int i, int j)> edges, Dictionary<BlockInstanceId, int> idToIndex, long signature) BuildGraph(
            IReadOnlyList<IGearEnergyTransformer> nodes)
        {
            var idToIndex = new Dictionary<BlockInstanceId, int>(nodes.Count);
            for (var i = 0; i < nodes.Count; i++) idToIndex[nodes[i].BlockInstanceId] = i;

            var edgeSet = new HashSet<(int, int)>();
            var edges = new List<(int i, int j)>();
            for (var a = 0; a < nodes.Count; a++)
            {
                foreach (var conn in nodes[a].GetGearConnects())
                {
                    if (!idToIndex.TryGetValue(conn.Transformer.BlockInstanceId, out var b)) continue;
                    if (a == b) continue;
                    var key = a < b ? (a, b) : (b, a);
                    if (edgeSet.Add(key)) edges.Add(key);
                }
            }

            // signature: ノード数 + エッジ集合の累積ハッシュ。long のまま比較して衝突を回避する
            // Coarse signature on (node count, edge multiset). Kept as long to avoid 32-bit collision
            long signature = nodes.Count;
            foreach (var e in edges) signature = signature * 1000003L + ((long)e.i * nodes.Count + e.j);
            return (edges, idToIndex, signature);
        }

        private static void EnsureSolverReady(
            State state,
            List<IGearEnergyTransformer> nodes,
            List<(int i, int j)> edges,
            Dictionary<BlockInstanceId, int> idToIndex,
            long signature)
        {
            if (state.Version == signature && state.Solver != null)
            {
                ClearLoadOnLeftNodes(state, idToIndex);
                state.OrderedNodes = nodes;
                return;
            }

            ClearLoadOnLeftNodes(state, idToIndex);
            state.Solver = new TorqueLoadSolver();
            state.Solver.Prepare(nodes.Count, edges);
            state.B = new double[nodes.Count];
            state.Torque = new double[edges.Count];
            state.NodeMax = new double[nodes.Count];
            state.OrderedNodes = nodes;
            state.Version = signature;
        }

        private static void ClearLoadOnLeftNodes(State state, Dictionary<BlockInstanceId, int> newIdToIndex)
        {
            // 前tickに居て今tickに居ないブロックの負荷をゼロクリアする
            // Zero out CurrentLoadTorque on blocks that left this network since the previous tick
            if (state.OrderedNodes == null) return;
            foreach (var old in state.OrderedNodes)
            {
                if (newIdToIndex.ContainsKey(old.BlockInstanceId)) continue;
                old.SetCurrentLoadTorque(new Torque(0));
            }
        }

        private static void FillBVector(State state)
        {
            // b[v] = ジェネレータの生成トルク - 当該RPMでの要求トルク。中継ブロックは要求トルク=0なのでb=0
            // b[v] = generator output minus required torque at current rpm. Pure transmission has zero required torque so b=0
            // 注: ソルバはノード0をφ=0で固定し縮約後にSolveするため、全体の需給差分は暗黙的にノード0で吸収される
            // Note: solver anchors node 0 at φ=0 and solves the reduced system, so Σb imbalance is absorbed at node 0 implicitly
            var b = state.B;
            for (var i = 0; i < state.OrderedNodes.Count; i++)
            {
                var t = state.OrderedNodes[i];
                var rpm = t.CurrentRpm;
                var clockwise = t.IsCurrentClockwise;
                var required = t.GetRequiredTorque(rpm, clockwise).AsPrimitive();
                var generated = t is IGearGenerator gen ? gen.GenerateTorque.AsPrimitive() : 0.0;
                b[i] = generated - required;
            }
        }

        private static void Distribute(State state)
        {
            for (var i = 0; i < state.OrderedNodes.Count; i++)
            {
                state.OrderedNodes[i].SetCurrentLoadTorque(new Torque((float)state.NodeMax[i]));
            }
        }

        private static void ResetToZero(State state, List<IGearEnergyTransformer> nodes, long signature)
        {
            // 旧ノードのうち、新ノード集合に居ないものは CurrentLoadTorque を 0 にリセットする
            // Reset CurrentLoadTorque to 0 for old nodes not present in the new node set
            if (state.OrderedNodes != null)
            {
                var newIds = new HashSet<BlockInstanceId>();
                foreach (var n in nodes) newIds.Add(n.BlockInstanceId);
                foreach (var old in state.OrderedNodes)
                {
                    if (newIds.Contains(old.BlockInstanceId)) continue;
                    old.SetCurrentLoadTorque(new Torque(0));
                }
            }
            foreach (var n in nodes) n.SetCurrentLoadTorque(new Torque(0));
            state.OrderedNodes = nodes;
            state.Solver = null;
            state.Version = signature;
        }

        private static bool IsConnected(int nodeCount, List<(int i, int j)> edges)
        {
            // BFSでノード0から到達可能なノード数を数え、全ノードに到達するかチェック
            // BFS from node 0; check that every node is reachable
            var adjStart = new int[nodeCount + 1];
            foreach (var e in edges) { adjStart[e.i + 1]++; adjStart[e.j + 1]++; }
            for (var k = 1; k <= nodeCount; k++) adjStart[k] += adjStart[k - 1];
            var adj = new int[adjStart[nodeCount]];
            var cursor = new int[nodeCount];
            foreach (var e in edges)
            {
                adj[adjStart[e.i] + cursor[e.i]++] = e.j;
                adj[adjStart[e.j] + cursor[e.j]++] = e.i;
            }
            var visited = new bool[nodeCount];
            var queue = new Queue<int>();
            visited[0] = true;
            queue.Enqueue(0);
            var reached = 1;
            while (queue.Count > 0)
            {
                var u = queue.Dequeue();
                var end = adjStart[u + 1];
                for (var p = adjStart[u]; p < end; p++)
                {
                    var v = adj[p];
                    if (visited[v]) continue;
                    visited[v] = true;
                    reached++;
                    queue.Enqueue(v);
                }
            }
            return reached == nodeCount;
        }

        #endregion

    }
}
