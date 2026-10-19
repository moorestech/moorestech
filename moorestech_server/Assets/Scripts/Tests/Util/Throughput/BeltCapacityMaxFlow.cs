using System;
using System.Collections.Generic;
using Core.BeltTransport;
using Game.Block.Blocks.BeltConveyor.Transport;

namespace Tests.Util.Throughput
{
    // 複雑系の理論最大を、組(segment と接続)から作った容量グラフの最大流で出す(ADR 0084、シナリオ案 §2 C)
    // The theoretical maximum of a complex layout, as the max flow of a capacity graph built from the assembly (segments and links) (ADR 0084, scenario §2 C)
    // segment は「入口→出口」の辺で容量 speed/360 個/tick、機械からの入力は無限の供給、機械への出力は無限の受け。round robin は詰まった方向を飛ばすので最大流に一致する
    // A segment is an edge from its entrance to its exit with capacity speed/360 items per tick; machine inputs are an unbounded source and machine outputs an unbounded sink. Round robin skips blocked directions, so it matches the max flow
    public static class BeltCapacityMaxFlow
    {
        private const double Unbounded = 1e9;

        public static double ItemsPerTick(BeltTransportAssembly assembly)
        {
            var segmentCount = assembly.Layouts.Length;
            var source = 2 * segmentCount;
            var sink = source + 1;
            var graph = new FlowGraph(sink + 1);
            for (var i = 0; i < segmentCount; i++)
            {
                var layout = assembly.Layouts[i];
                graph.AddEdge(Entrance(i), Exit(i), assembly.Segments[i].Speed / (double)BeltConstants.ItemWidth);
                foreach (var input in layout.Inputs)
                {
                    if (input.IsMachine) graph.AddEdge(source, Entrance(i), Unbounded);
                }
                foreach (var output in layout.Outputs)
                {
                    if (output.IsMachine) graph.AddEdge(Exit(i), sink, Unbounded);
                    else graph.AddEdge(Exit(i), Entrance(output.PartnerSegmentIndex), Unbounded);
                }
            }
            return graph.MaxFlow(source, sink);
        }

        // 窓の間に流れる個数。周期の倍数の窓なら整数になる(丸め誤差だけ吸収する)
        // Items over the window; a whole number when the window is a multiple of the period (only rounding noise is absorbed)
        public static int ExpectedTotal(BeltTransportAssembly assembly, int window)
        {
            return (int)Math.Round(ItemsPerTick(assembly) * window);
        }

        private static int Entrance(int segmentIndex) => 2 * segmentIndex;
        private static int Exit(int segmentIndex) => 2 * segmentIndex + 1;

        // Edmonds–Karp。辺は少数(数十)なので隣接リストの BFS で十分
        // Edmonds–Karp; with a few dozen edges a BFS over adjacency lists is plenty
        private sealed class FlowGraph
        {
            private readonly List<int>[] _adjacent;
            private readonly List<int> _to = new();
            private readonly List<double> _capacity = new();

            public FlowGraph(int nodeCount)
            {
                _adjacent = new List<int>[nodeCount];
                for (var i = 0; i < nodeCount; i++) _adjacent[i] = new List<int>();
            }

            public void AddEdge(int from, int to, double capacity)
            {
                _adjacent[from].Add(_to.Count);
                _to.Add(to);
                _capacity.Add(capacity);
                _adjacent[to].Add(_to.Count);
                _to.Add(from);
                _capacity.Add(0);
            }

            public double MaxFlow(int source, int sink)
            {
                var flow = 0.0;
                var parentEdge = new int[_adjacent.Length];
                while (FindPath(source, sink, parentEdge))
                {
                    var bottleneck = double.MaxValue;
                    for (var node = sink; node != source; node = _to[parentEdge[node] ^ 1]) bottleneck = Math.Min(bottleneck, _capacity[parentEdge[node]]);
                    for (var node = sink; node != source; node = _to[parentEdge[node] ^ 1])
                    {
                        _capacity[parentEdge[node]] -= bottleneck;
                        _capacity[parentEdge[node] ^ 1] += bottleneck;
                    }
                    flow += bottleneck;
                }
                return flow;
            }

            private bool FindPath(int source, int sink, int[] parentEdge)
            {
                for (var i = 0; i < parentEdge.Length; i++) parentEdge[i] = -1;
                var queue = new Queue<int>();
                queue.Enqueue(source);
                parentEdge[source] = -2;
                while (queue.Count > 0)
                {
                    var node = queue.Dequeue();
                    foreach (var edge in _adjacent[node])
                    {
                        var next = _to[edge];
                        if (parentEdge[next] != -1 || _capacity[edge] <= 1e-12) continue;
                        parentEdge[next] = edge;
                        if (next == sink) return true;
                        queue.Enqueue(next);
                    }
                }
                return false;
            }
        }
    }
}
