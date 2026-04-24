using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.Gear.Common
{
    // 歯車ネットワーク上の各ブロックを通過する負荷トルクを Laplacian + 疎コレスキー解で計算する静的ユーティリティ
    // 結果は State.NodeLoadByBlockInstance に格納され、呼び出し側が BlockInstanceId で引いて SupplyPower に渡す
    // Static utility that solves per-edge torque flow via Laplacian + sparse Cholesky
    // Results are stored in State.NodeLoadByBlockInstance; callers look up per BlockInstanceId and feed SupplyPower
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
            public readonly Dictionary<BlockInstanceId, Torque> NodeLoadByBlockInstance = new();

            public Torque GetLoad(BlockInstanceId id)
            {
                return NodeLoadByBlockInstance.TryGetValue(id, out var v) ? v : new Torque(0);
            }
        }

        // ネットワーク全ノードに対して負荷を計算し、State.NodeLoadByBlockInstance に格納する
        // Compute load for every node in the network and store the result in State.NodeLoadByBlockInstance
        public static void ComputeNodeLoads(
            IReadOnlyList<IGearGenerator> generators,
            IReadOnlyList<IGearEnergyTransformer> nonGenerators,
            IReadOnlyDictionary<BlockInstanceId, GearRotationInfo> rotationInfo,
            State state)
        {
            state.NodeLoadByBlockInstance.Clear();

            var nodes = CollectNodes(generators, nonGenerators);
            if (nodes.Count < 2)
            {
                state.OrderedNodes = nodes;
                state.Solver = null;
                state.Version = 0L;
                return;
            }

            var (edges, idToIndex, signature) = BuildGraph(nodes);

            // CSparse は nnz=0 で内部配列を null 初期化するので Cholesky.Create が NRE になる前に弾く
            // Guard up-front: CSparse leaves internal arrays null when nnz=0 which would crash Cholesky.Create
            if (edges.Count == 0)
            {
                state.OrderedNodes = nodes;
                state.Solver = null;
                state.Version = signature;
                return;
            }

            // 過渡状態で非連結なら縮約Laplacianが非SPDになり Cholesky が失敗するので事前に弾く
            // Guard non-connected transient state which would yield a non-SPD reduced Laplacian and fail Cholesky
            if (!IsConnected(nodes.Count, edges))
            {
                state.OrderedNodes = nodes;
                state.Solver = null;
                state.Version = signature;
                return;
            }

            EnsureSolverReady(state, nodes, edges, signature);
            FillBVector(state, rotationInfo);
            state.Solver.Solve(state.B, state.Torque);
            state.Solver.AggregateNodeMaxAdjacent(state.Torque, state.NodeMax);

            for (var i = 0; i < nodes.Count; i++)
            {
                state.NodeLoadByBlockInstance[nodes[i].BlockInstanceId] = new Torque((float)state.NodeMax[i]);
            }
        }

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
            long signature)
        {
            if (state.Version == signature && state.Solver != null)
            {
                state.OrderedNodes = nodes;
                return;
            }

            state.Solver = new TorqueLoadSolver();
            state.Solver.Prepare(nodes.Count, edges);
            state.B = new double[nodes.Count];
            state.Torque = new double[edges.Count];
            state.NodeMax = new double[nodes.Count];
            state.OrderedNodes = nodes;
            state.Version = signature;
        }

        private static void FillBVector(State state, IReadOnlyDictionary<BlockInstanceId, GearRotationInfo> rotationInfo)
        {
            // b[v] = ジェネレータの生成トルク - 当該ノードの要求トルク（当フレームのDFS結果から取得）。中継ブロックは0
            // b[v] = generator output minus required torque (from this frame's DFS result). Pure transmission = 0
            // SupplyPower前に呼ばれるためCurrentRpmはまだ更新されていないので、rotationInfoの計算済みrpm由来のRequiredTorqueを使う
            // Called before SupplyPower so CurrentRpm is stale; use RequiredTorque derived from the freshly computed rpm in rotationInfo
            // 注: ソルバはノード0をφ=0で固定し縮約後にSolveするため、全体の需給差分は暗黙的にノード0で吸収される
            // Note: solver anchors node 0 at φ=0 and solves the reduced system, so Σb imbalance is absorbed at node 0 implicitly
            var b = state.B;
            for (var i = 0; i < state.OrderedNodes.Count; i++)
            {
                var t = state.OrderedNodes[i];
                var required = rotationInfo.TryGetValue(t.BlockInstanceId, out var info) ? info.RequiredTorque.AsPrimitive() : 0.0;
                var generated = t is IGearGenerator gen ? gen.GenerateTorque.AsPrimitive() : 0.0;
                b[i] = generated - required;
            }
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
    }
}
