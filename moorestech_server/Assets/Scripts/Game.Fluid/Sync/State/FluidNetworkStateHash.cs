using System;
using System.Runtime.CompilerServices;
using Game.Fluid.Simulation;

namespace Game.Fluid.Sync.State
{
    // 網の決定的なハッシュ。要素ごとに独立したハッシュ(種別・番号・値を乗算と右シフトで撹拌)を作り、順序に依存しない加算で畳む
    // Deterministic hash of the network: each element gets an independent hash (kind, index and value scrambled by multiply and shift), folded with order-independent addition
    // 要素間に依存が無いので、どの順でも、何分割しても、別スレッドで分けて足しても同じ値になる。列車・ベルトの FNV-1a(前の値に次を掛ける直列の鎖)とはここが違う
    // Elements do not depend on each other, so any order, any partitioning and any threaded split-and-add yields the same value; this is where it differs from the train and belt FNV-1a (a serial chain multiplying the previous value)
    // 混ぜるのは tick で変わる項目(量・流体ID・面とポートの速度)と件数だけ。座標・容量・面の相手・向き・上限は再構築まで変わらず、全量送信で揃うので照合に載せない
    // Only the fields that change per tick (amounts, fluid ids, face and port velocities) and the counts are mixed; positions, capacities, face endpoints, directions and caps stay fixed until a rebuild and are settled by the full-state send
    // doubleはビット列で混ぜるので1bitの差も不一致になる。実体の配列を直接なめ、全量の切り出しも割り当ても無い(ADR 0088)
    // Doubles are mixed as bit patterns, so a one-bit difference mismatches; walks the object arrays directly with no full-state capture and no allocation (ADR 0088)
    public static class FluidNetworkStateHash
    {
        private const uint NodeSeed = 0x6E6F6465;
        private const uint FaceSeed = 0x66616365;
        private const uint PortSeed = 0x706F7274;

        public static uint Compute(FluidSimNode[] nodes, FluidSimFace[] faces, IFluidBoundaryPort[] ports)
        {
            unchecked
            {
                var sum = Finish(NodeSeed ^ (uint)nodes.Length) + Finish(FaceSeed ^ (uint)faces.Length) + Finish(PortSeed ^ (uint)ports.Length);
                for (var i = 0; i < nodes.Length; i++) sum += NodeElement(i, nodes[i].Amount, nodes[i].FluidId.AsPrimitive());
                for (var i = 0; i < faces.Length; i++) sum += DoubleElement(FaceSeed, i, faces[i].Velocity);
                for (var i = 0; i < ports.Length; i++) sum += DoubleElement(PortSeed, i, ports[i].Velocity);
                return sum;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint NodeElement(int index, double amount, int fluidId)
        {
            unchecked
            {
                var bits = BitConverter.DoubleToInt64Bits(amount);
                var h = Start(NodeSeed, index);
                h = Scramble(h, (uint)bits);
                h = Scramble(h, (uint)(bits >> 32));
                h = Scramble(h, (uint)fluidId);
                return Finish(h);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint DoubleElement(uint seed, int index, double value)
        {
            unchecked
            {
                var bits = BitConverter.DoubleToInt64Bits(value);
                var h = Start(seed, index);
                h = Scramble(h, (uint)bits);
                h = Scramble(h, (uint)(bits >> 32));
                return Finish(h);
            }
        }

        // 種別と番号から要素の初期値を作る。番号を混ぜるので並びの入れ替えは別の値になる
        // Seeds an element from its kind and index; mixing the index makes a reordering a different value
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Start(uint seed, int index)
        {
            unchecked { return seed ^ ((uint)index * 0x9E3779B1u); }
        }

        // 32bit を1つ取り込む。XOR→乗算→上位bitの折り返しで、入力1bitの差を広げる
        // Takes in one 32-bit word: XOR, multiply, then fold the high bits down so a one-bit input difference spreads
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Scramble(uint h, uint k)
        {
            unchecked
            {
                h ^= k;
                h *= 0x85EBCA6Bu;
                return h ^ (h >> 15);
            }
        }

        // 最終撹拌。加算で畳んでも要素同士が打ち消し合いにくいよう、全bitへ広げてから足す
        // Final scramble; spreads over every bit before the additive fold so elements rarely cancel each other
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Finish(uint h)
        {
            unchecked
            {
                h *= 0xC2B2AE35u;
                return h ^ (h >> 16);
            }
        }
    }
}
