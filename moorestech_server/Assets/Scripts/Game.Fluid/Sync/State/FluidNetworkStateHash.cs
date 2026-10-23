using System;
using System.Collections.Generic;
using Game.Fluid.Simulation;
using UnityEngine;

namespace Game.Fluid.Sync.State
{
    // 網の決定的なハッシュ(FNV-1a、ベルトのBeltTransportStateHashと同型)。番号順に全項目を混ぜる。doubleはビット列で混ぜるので1bitの差も不一致になる
    // Deterministic hash of the network (FNV-1a, same shape as the belt's BeltTransportStateHash); mixes every field in index order. Doubles are mixed as bit patterns, so a one-bit difference mismatches
    // 本番はノード・面・ポートから直接混ぜて割り当てを出さない。面とポートのノードは番号でなく座標で混ぜるので、全量からの計算も同じ値になる
    // Production mixes straight from the nodes, faces and ports with no allocation; faces and ports identify nodes by position rather than index, so the full-state overload yields the same value
    public static class FluidNetworkStateHash
    {
        private const uint FnvOffset = 2166136261;
        private const uint FnvPrime = 16777619;

        public static uint Compute(IReadOnlyList<FluidSimNode> nodes, IReadOnlyList<FluidSimFace> faces, IReadOnlyList<IFluidBoundaryPort> ports)
        {
            var hash = Mix(FnvOffset, nodes.Count);
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                hash = Mix(hash, i);
                hash = MixNode(hash, node.Position, node.Capacity, node.Amount, node.FluidId.AsPrimitive());
            }

            hash = Mix(hash, faces.Count);
            for (var i = 0; i < faces.Count; i++)
            {
                var face = faces[i];
                hash = MixFace(hash, face.NodeA.Position, face.NodeB.Position, face.FlowCapacityPerTick, face.Velocity, face.AllowAToB, face.AllowBToA);
            }

            hash = Mix(hash, ports.Count);
            for (var i = 0; i < ports.Count; i++)
            {
                var port = ports[i];
                hash = MixPort(hash, port.PipeNode.Position, port.FlowCapacityPerTick, port.Velocity);
            }
            return hash;
        }

        // 全量(送受信した形)からの計算。テストが本番側の値と突き合わせるのに使う
        // Computation from the full state (the wire form); tests use it to check against the production value
        public static uint Compute(FluidNetworkFullState fullState)
        {
            var hash = Mix(FnvOffset, fullState.Nodes.Length);
            for (var i = 0; i < fullState.Nodes.Length; i++)
            {
                var node = fullState.Nodes[i];
                hash = Mix(hash, i);
                hash = MixNode(hash, node.Position, node.Capacity, node.Amount, node.FluidId.AsPrimitive());
            }

            hash = Mix(hash, fullState.Faces.Length);
            for (var i = 0; i < fullState.Faces.Length; i++)
            {
                var face = fullState.Faces[i];
                hash = MixFace(hash, fullState.Nodes[face.NodeAIndex].Position, fullState.Nodes[face.NodeBIndex].Position, face.FlowCapacityPerTick, face.Velocity, face.AllowAToB, face.AllowBToA);
            }

            hash = Mix(hash, fullState.Ports.Length);
            for (var i = 0; i < fullState.Ports.Length; i++)
            {
                var port = fullState.Ports[i];
                hash = MixPort(hash, fullState.Nodes[port.NodeIndex].Position, port.FlowCapacityPerTick, port.Velocity);
            }
            return hash;
        }

        private static uint MixNode(uint current, Vector3Int position, double capacity, double amount, int fluidId)
        {
            var hash = MixPosition(current, position);
            hash = MixDouble(hash, capacity);
            hash = MixDouble(hash, amount);
            return Mix(hash, fluidId);
        }

        private static uint MixFace(uint current, Vector3Int nodeA, Vector3Int nodeB, double flowCapacityPerTick, double velocity, bool allowAToB, bool allowBToA)
        {
            var hash = MixPosition(current, nodeA);
            hash = MixPosition(hash, nodeB);
            hash = MixDouble(hash, flowCapacityPerTick);
            hash = MixDouble(hash, velocity);
            hash = Mix(hash, allowAToB ? 1 : 0);
            return Mix(hash, allowBToA ? 1 : 0);
        }

        private static uint MixPort(uint current, Vector3Int pipeNode, double flowCapacityPerTick, double velocity)
        {
            var hash = MixPosition(current, pipeNode);
            hash = MixDouble(hash, flowCapacityPerTick);
            return MixDouble(hash, velocity);
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
