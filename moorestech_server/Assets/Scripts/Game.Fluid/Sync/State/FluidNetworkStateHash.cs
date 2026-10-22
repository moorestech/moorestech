using System;
using UnityEngine;

namespace Game.Fluid.Sync.State
{
    // 全量の決定的なハッシュ(FNV-1a、ベルトのBeltTransportStateHashと同型)。番号順に全項目を混ぜる。doubleはビット列で混ぜるので1bitの差も不一致になる
    // Deterministic hash of the full state (FNV-1a, same shape as the belt's BeltTransportStateHash); mixes every field in index order. Doubles are mixed as bit patterns, so a one-bit difference mismatches
    public static class FluidNetworkStateHash
    {
        private const uint FnvOffset = 2166136261;
        private const uint FnvPrime = 16777619;

        public static uint Compute(FluidNetworkFullState fullState)
        {
            var hash = Mix(FnvOffset, fullState.Nodes.Length);
            for (var i = 0; i < fullState.Nodes.Length; i++)
            {
                var node = fullState.Nodes[i];
                hash = Mix(hash, i);
                hash = MixPosition(hash, node.Position);
                hash = MixDouble(hash, node.Capacity);
                hash = MixDouble(hash, node.Amount);
                hash = Mix(hash, node.FluidId.AsPrimitive());
            }

            hash = Mix(hash, fullState.Faces.Length);
            for (var i = 0; i < fullState.Faces.Length; i++)
            {
                var face = fullState.Faces[i];
                hash = Mix(hash, face.NodeAIndex);
                hash = Mix(hash, face.NodeBIndex);
                hash = MixDouble(hash, face.FlowCapacityPerTick);
                hash = MixDouble(hash, face.Velocity);
                hash = Mix(hash, face.AllowAToB ? 1 : 0);
                hash = Mix(hash, face.AllowBToA ? 1 : 0);
            }

            hash = Mix(hash, fullState.Ports.Length);
            for (var i = 0; i < fullState.Ports.Length; i++)
            {
                var port = fullState.Ports[i];
                hash = Mix(hash, port.NodeIndex);
                hash = MixDouble(hash, port.FlowCapacityPerTick);
                hash = MixDouble(hash, port.Velocity);
            }
            return hash;
        }

        private static uint MixPosition(uint current, Vector3Int position)
        {
            var hash = Mix(current, position.x);
            hash = Mix(hash, position.y);
            return Mix(hash, position.z);
        }

        private static uint MixDouble(uint current, double value)
        {
            unchecked
            {
                var bits = BitConverter.DoubleToInt64Bits(value);
                var hash = Mix(current, (int)bits);
                return Mix(hash, (int)(bits >> 32));
            }
        }

        private static uint Mix(uint current, int value)
        {
            unchecked
            {
                return (current ^ (uint)value) * FnvPrime;
            }
        }
    }
}
