using ClassLibrary;
using Game.Fluid.Simulation;

namespace Game.Fluid.Sync.State
{
    // 網の決定的なハッシュ。要素(ノード・面・ポート)ごとに独立したハッシュを作り、順序に依存しない加算で畳む(ElementHash。列車・ベルトと同じ部品)
    // Deterministic hash of the network: each element (node, face, port) gets an independent hash folded by order-independent addition (ElementHash, shared with train and belt)
    // 混ぜるのは tick で変わる項目(量・流体ID・面とポートの速度)と件数だけ。座標・容量・面の相手・向き・上限は再構築まで変わらず、全量送信で揃うので照合に載せない
    // Only the fields that change per tick (amounts, fluid ids, face and port velocities) and the counts are mixed; positions, capacities, face endpoints, directions and caps stay fixed until a rebuild and are settled by the full-state send
    // 要素関数は Stepper が最終パスで直接呼ぶ(走査を増やさない)。Compute は構築直後など Step を通らない場面の全走査
    // The element functions are called directly by the stepper's final passes (no extra walk); Compute is the full walk for moments outside Step such as right after construction
    public static class FluidNetworkStateHash
    {
        private const uint NodeSeed = 0x6E6F6465;
        private const uint FaceSeed = 0x66616365;
        private const uint PortSeed = 0x706F7274;

        public static uint Compute(FluidSimNetwork network)
        {
            unchecked
            {
                var amounts = network.NodeAmounts;
                var fluidIds = network.NodeFluidIds;
                var velocities = network.FaceVelocities;
                var ports = network.Ports;
                var sum = Counts(amounts.Length, velocities.Length, ports.Length);
                for (var i = 0; i < amounts.Length; i++) sum += NodeElement(i, amounts[i], fluidIds[i].AsPrimitive());
                for (var i = 0; i < velocities.Length; i++) sum += FaceElement(i, velocities[i]);
                for (var i = 0; i < ports.Length; i++) sum += PortElement(i, ports[i].Velocity);
                return sum;
            }
        }

        public static uint Counts(int nodeCount, int faceCount, int portCount)
        {
            unchecked { return ElementHash.Count(NodeSeed, nodeCount) + ElementHash.Count(FaceSeed, faceCount) + ElementHash.Count(PortSeed, portCount); }
        }

        public static uint NodeElement(int index, long amount, int fluidId)
        {
            var hash = ElementHash.Begin(NodeSeed, index);
            hash = ElementHash.Add(hash, amount);
            hash = ElementHash.Add(hash, fluidId);
            return ElementHash.Finish(hash);
        }

        public static uint FaceElement(int index, long velocity)
        {
            return ElementHash.Finish(ElementHash.Add(ElementHash.Begin(FaceSeed, index), velocity));
        }

        public static uint PortElement(int index, long velocity)
        {
            return ElementHash.Finish(ElementHash.Add(ElementHash.Begin(PortSeed, index), velocity));
        }
    }
}
