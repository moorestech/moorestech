using System;
using System.Runtime.CompilerServices;
using Game.Fluid.Simulation;

namespace Game.Fluid.Sync.State
{
    // 網の決定的なハッシュ(FNV-1a、ベルトのBeltTransportStateHashと同型)。doubleはビット列で混ぜるので1bitの差も不一致になる
    // Deterministic hash of the network (FNV-1a, same shape as the belt's BeltTransportStateHash); doubles are mixed as bit patterns, so a one-bit difference mismatches
    // 混ぜるのは tick で変わる項目(量・流体ID・面とポートの速度)と件数だけ。座標・容量・面の相手・向き・上限は再構築まで変わらず、全量送信で揃うので照合に載せない
    // Only the fields that change per tick (amounts, fluid ids, face and port velocities) and the counts are mixed; positions, capacities, face endpoints, directions and caps stay fixed until a rebuild and are settled by the full-state send
    // 実体の配列を直接なめ(全量の切り出しも割り当ても無し)、掛け算の直列依存を外すため4本の鎖で並走して最後に合流する。Mono 実測で 22,800 ノード 3.9ms→0.49ms(ADR 0088)
    // Walks the object arrays directly (no full-state capture, no allocation) and runs four chains in parallel to break the multiply dependency, joining them at the end; measured on Mono at 22,800 nodes: 3.9ms→0.49ms (ADR 0088)
    public static class FluidNetworkStateHash
    {
        private const uint FnvOffset = 2166136261;
        private const uint FnvPrime = 16777619;

        public static uint Compute(FluidSimNode[] nodes, FluidSimFace[] faces, IFluidBoundaryPort[] ports)
        {
            // 鎖ごとに初期値をずらし、件数を先に混ぜる
            // Offset each chain's seed and mix the counts first
            var h0 = Mix(FnvOffset, nodes.Length);
            var h1 = Mix(FnvOffset ^ 1, faces.Length);
            var h2 = Mix(FnvOffset ^ 2, ports.Length);
            var h3 = FnvOffset ^ 3;

            // ノード: 量と流体IDを4本に振り分ける。端数は鎖0
            // Nodes: amounts and fluid ids spread over four chains; the remainder goes to chain 0
            var i = 0;
            for (; i + 4 <= nodes.Length; i += 4)
            {
                var a = nodes[i];
                var b = nodes[i + 1];
                var c = nodes[i + 2];
                var d = nodes[i + 3];
                h0 = MixDouble(h0, a.Amount);
                h1 = MixDouble(h1, b.Amount);
                h2 = MixDouble(h2, c.Amount);
                h3 = MixDouble(h3, d.Amount);
                h0 = Mix(h0, a.FluidId.AsPrimitive());
                h1 = Mix(h1, b.FluidId.AsPrimitive());
                h2 = Mix(h2, c.FluidId.AsPrimitive());
                h3 = Mix(h3, d.FluidId.AsPrimitive());
            }
            for (; i < nodes.Length; i++)
            {
                h0 = MixDouble(h0, nodes[i].Amount);
                h0 = Mix(h0, nodes[i].FluidId.AsPrimitive());
            }

            // 面: 速度だけ
            // Faces: velocity only
            i = 0;
            for (; i + 4 <= faces.Length; i += 4)
            {
                h0 = MixDouble(h0, faces[i].Velocity);
                h1 = MixDouble(h1, faces[i + 1].Velocity);
                h2 = MixDouble(h2, faces[i + 2].Velocity);
                h3 = MixDouble(h3, faces[i + 3].Velocity);
            }
            for (; i < faces.Length; i++) h0 = MixDouble(h0, faces[i].Velocity);

            // ポートは少ないので鎖0のみ。最後に4本を合流する
            // Ports are few, so chain 0 only; then join the four chains
            for (i = 0; i < ports.Length; i++) h0 = MixDouble(h0, ports[i].Velocity);
            return Mix(Mix(Mix(h0, (int)h1), (int)h2), (int)h3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint MixDouble(uint current, double value)
        {
            unchecked
            {
                var bits = BitConverter.DoubleToInt64Bits(value);
                var hash = Mix(current, (int)bits);
                return Mix(hash, (int)(bits >> 32));
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint current, int value)
        {
            unchecked
            {
                return (current ^ (uint)value) * FnvPrime;
            }
        }
    }
}
