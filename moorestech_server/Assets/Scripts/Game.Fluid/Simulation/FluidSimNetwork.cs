using System;
using System.Collections.Generic;
using Core.Master;
using Game.Fluid.Sync.State;
using UnityEngine;

namespace Game.Fluid.Simulation
{
    /// <summary>
    ///     流体網の状態を列ごとの配列で持つ(SoA)。ノード・面は番号で指し、クラス参照を持たない。サーバーの網もクライアントの複製もこの1つの型。
    ///     配列は構築時に確定し、再構築のたびに新しい網へ置き換える。tick で変わるのは量・流体ID・面とポートの速度だけ。
    ///     The fluid network state as per-column arrays (SoA): nodes and faces are addressed by index with no object references; the server network and the client replica share this one type.
    ///     Arrays are fixed at construction and a rebuild swaps in a new network; only amounts, fluid ids and face/port velocities change per tick.
    /// </summary>
    public sealed class FluidSimNetwork
    {
        // ノード列。座標の辞書順に並び、クライアントの複製も同じ並びを写す
        // Node columns, sorted by position so the client replica copies the same order
        public readonly Vector3Int[] NodePositions;
        public readonly double[] NodeCapacities;
        public readonly double[] NodeAmounts;
        public readonly FluidId[] NodeFluidIds;

        // 面列。NodeA は座標の辞書順で小さい側(正準側)。速度は NodeA→NodeB が正
        // Face columns; NodeA is the lexicographically smaller (canonical) side and velocity is positive from NodeA to NodeB
        public readonly int[] FaceNodeA;
        public readonly int[] FaceNodeB;
        public readonly double[] FaceFlowCapacities;
        public readonly double[] FaceVelocities;
        public readonly bool[] FaceAllowAToB;
        public readonly bool[] FaceAllowBToA;

        // 境界ポートは数が少なく相手(機械か複製)で振る舞いが違うので、オブジェクトのまま持つ
        // Boundary ports stay objects: they are few and behave differently by counterpart (machine or replica)
        public readonly IFluidBoundaryPort[] Ports;

        // 現在の状態のハッシュ。Stepper が最終パスで全要素を足し直し、境界からの流入は要素の差分で更新し、構築直後は全走査で求める。加算で畳むので要素単位の差し替えが O(1) で済む
        // Hash of the current state: the stepper re-sums every element in its final pass, boundary inflows update it by element delta, and construction computes it with a full walk; the additive fold makes a per-element replacement O(1)
        public uint LastStateHash { get; private set; }

        private readonly Dictionary<Vector3Int, int> _nodeIndexByPosition;

        public int NodeCount => NodePositions.Length;
        public int FaceCount => FaceNodeA.Length;

        public FluidSimNetwork(Vector3Int[] nodePositions, double[] nodeCapacities, double[] nodeAmounts, FluidId[] nodeFluidIds,
            int[] faceNodeA, int[] faceNodeB, double[] faceFlowCapacities, double[] faceVelocities, bool[] faceAllowAToB, bool[] faceAllowBToA,
            IFluidBoundaryPort[] ports)
        {
            NodePositions = nodePositions;
            NodeCapacities = nodeCapacities;
            NodeAmounts = nodeAmounts;
            NodeFluidIds = nodeFluidIds;
            FaceNodeA = faceNodeA;
            FaceNodeB = faceNodeB;
            FaceFlowCapacities = faceFlowCapacities;
            FaceVelocities = faceVelocities;
            FaceAllowAToB = faceAllowAToB;
            FaceAllowBToA = faceAllowBToA;
            Ports = ports;
            _nodeIndexByPosition = new Dictionary<Vector3Int, int>(nodePositions.Length);
            for (var i = 0; i < nodePositions.Length; i++) _nodeIndexByPosition.Add(nodePositions[i], i);
            RecomputeStateHash();
        }

        public static FluidSimNetwork CreateEmpty()
        {
            return new FluidSimNetwork(Array.Empty<Vector3Int>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<FluidId>(),
                Array.Empty<int>(), Array.Empty<int>(), Array.Empty<double>(), Array.Empty<double>(), Array.Empty<bool>(), Array.Empty<bool>(),
                Array.Empty<IFluidBoundaryPort>());
        }

        // パイプの座標からノード番号を引く。パイプでない座標は false
        // Looks up the node index at a pipe position; false for a position with no pipe
        public bool TryGetNodeIndex(Vector3Int position, out int nodeIndex)
        {
            return _nodeIndexByPosition.TryGetValue(position, out nodeIndex);
        }

        public double FillRate(int node)
        {
            return NodeAmounts[node] / NodeCapacities[node];
        }

        // 受け入れ規則の唯一の定義。異種流体は拒否(0)、空なら流体IDを引き継ぎ、空き容量でクランプした受け入れ量を返す。持ち越し中のパイプも同じ規則を使う
        // The single definition of the acceptance rule: reject mismatched fluid (0), an empty holder adopts the incoming id, clamp by free capacity and return the accepted amount; unbuilt pipes use the same rule
        public static double ResolveAcceptedAmount(double currentAmount, double capacity, FluidId currentFluidId, FluidStack incoming)
        {
            if (incoming.Amount <= 0) return 0;
            var hasFluid = FluidSimulationConstants.AmountEpsilon < currentAmount;
            if (hasFluid && currentFluidId != incoming.FluidId) return 0;
            return Math.Max(0, Math.Min(incoming.Amount, capacity - currentAmount));
        }

        // 境界(機械のpush等)からの流入。受け入れ規則でクランプし、実際に足した量をacceptedで返す。差分にはこの値をそのまま記録する
        // External inflow from boundaries (machine push etc.); clamped by the acceptance rule, with the amount actually added returned in accepted, which is what the diff must record
        public FluidStack AddExternal(int node, FluidStack fluidStack, out double accepted)
        {
            accepted = ResolveAcceptedAmount(NodeAmounts[node], NodeCapacities[node], NodeFluidIds[node], fluidStack);
            if (accepted <= 0) return fluidStack;

            // 要素ハッシュの差し替えは流体IDの書き換えより前の値から取る
            // The element-hash swap starts from the value before the fluid id is rewritten
            var before = FluidNetworkStateHash.NodeElement(node, NodeAmounts[node], NodeFluidIds[node].AsPrimitive());
            if (!(FluidSimulationConstants.AmountEpsilon < NodeAmounts[node])) NodeFluidIds[node] = fluidStack.FluidId;
            NodeAmounts[node] += accepted;
            ReplaceNodeElement(node, before);
            return new FluidStack(fluidStack.Amount - accepted, fluidStack.FluidId);
        }

        // サーバーが受け入れ済みの量をそのまま足す(複製の差分再生)。容量確認はしない。空のノードは流体IDを引き継ぐ
        // Adds an amount the server already accepted (replica diff replay); no capacity check, and an empty node adopts the fluid id
        public void AddAccepted(int node, FluidId fluidId, double amount)
        {
            var before = FluidNetworkStateHash.NodeElement(node, NodeAmounts[node], NodeFluidIds[node].AsPrimitive());
            if (!(FluidSimulationConstants.AmountEpsilon < NodeAmounts[node])) NodeFluidIds[node] = fluidId;
            NodeAmounts[node] += amount;
            ReplaceNodeElement(node, before);
        }

        // ノード1個の要素ハッシュを古い値から新しい値へ差し替える。Step の外で状態を変えた後も網のハッシュを現在の状態に保つ
        // Swaps one node's element hash from its old to its new value, keeping the network hash current after a change made outside Step
        private void ReplaceNodeElement(int node, uint before)
        {
            unchecked { LastStateHash += FluidNetworkStateHash.NodeElement(node, NodeAmounts[node], NodeFluidIds[node].AsPrimitive()) - before; }
        }

        // 空になったら流体IDを解除する
        // Release the fluid id once the node is empty
        internal void CleanupIfEmpty(int node)
        {
            if (FluidSimulationConstants.AmountEpsilon < NodeAmounts[node]) return;
            NodeAmounts[node] = 0;
            NodeFluidIds[node] = FluidMaster.EmptyFluidId;
        }

        internal void SetLastStateHash(uint hash)
        {
            LastStateHash = hash;
        }

        // 全走査でハッシュを求め直す。構築直後に使う
        // Recompute the hash with a full walk; used right after construction
        private void RecomputeStateHash()
        {
            LastStateHash = FluidNetworkStateHash.Compute(this);
        }
    }
}
