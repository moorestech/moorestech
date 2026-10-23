using ClassLibrary;
using Game.Fluid.Simulation;

namespace Game.Fluid.Sync.State
{
    // 網の決定的なハッシュ。要素(ノード・面・ポート)ごとに独立したハッシュを作り、順序に依存しない加算で畳む(ElementHash。列車・ベルトと同じ部品)
    // Deterministic hash of the network: each element (node, face, port) gets an independent hash folded by order-independent addition (ElementHash, shared with train and belt)
    // 混ぜるのは tick で変わる項目(量・流体ID・面とポートの速度)と件数だけ。座標・容量・面の相手・向き・上限は再構築まで変わらず、全量送信で揃うので照合に載せない
    // Only the fields that change per tick (amounts, fluid ids, face and port velocities) and the counts are mixed; positions, capacities, face endpoints, directions and caps stay fixed until a rebuild and are settled by the full-state send
    // 実体の配列を直接なめ、全量の切り出しも割り当ても無い(ADR 0088)
    // Walks the object arrays directly with no full-state capture and no allocation (ADR 0088)
    public static class FluidNetworkStateHash
    {
        private const uint NodeSeed = 0x6E6F6465;
        private const uint FaceSeed = 0x66616365;
        private const uint PortSeed = 0x706F7274;

        public static uint Compute(FluidSimNode[] nodes, FluidSimFace[] faces, IFluidBoundaryPort[] ports)
        {
            unchecked
            {
                var sum = ElementHash.Count(NodeSeed, nodes.Length) + ElementHash.Count(FaceSeed, faces.Length) + ElementHash.Count(PortSeed, ports.Length);
                for (var i = 0; i < nodes.Length; i++)
                {
                    var hash = ElementHash.Begin(NodeSeed, i);
                    hash = ElementHash.Add(hash, nodes[i].Amount);
                    hash = ElementHash.Add(hash, nodes[i].FluidId.AsPrimitive());
                    sum += ElementHash.Finish(hash);
                }
                for (var i = 0; i < faces.Length; i++) sum += ElementHash.Finish(ElementHash.Add(ElementHash.Begin(FaceSeed, i), faces[i].Velocity));
                for (var i = 0; i < ports.Length; i++) sum += ElementHash.Finish(ElementHash.Add(ElementHash.Begin(PortSeed, i), ports[i].Velocity));
                return sum;
            }
        }
    }
}
