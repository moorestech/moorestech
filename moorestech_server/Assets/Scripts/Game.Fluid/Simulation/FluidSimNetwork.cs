using System;
using System.Collections.Generic;
using Core.Master;
using Game.Fluid.Sync.State;
using UnityEngine;

namespace Game.Fluid.Simulation
{
    /// <summary>
    ///     流体網の状態を列ごとの配列で持つ(SoA)。ノード・面は番号で指し、クラス参照を持たない。サーバーの網もクライアントの複製もこの1つの型。
    ///     量・容量・速度・上限は FluidUnits の整数単位(1/1000)の long(ADR 0090)。整数なので加算の順序で結果が変わらず、サーバーとクライアントがビット単位で一致する。
    ///     配列は構築時に確定し、再構築のたびに新しい網へ置き換える。tick で変わるのは量・流体ID・面とポートの速度だけ。
    ///     ノード→接続面・ノード→ポートの一覧(CSR)も構築時に作り、Stepper がノードごとに自分の面を集めるために使う。
    ///     The fluid network state as per-column arrays (SoA): nodes and faces are addressed by index with no object references; the server network and the client replica share this one type.
    ///     Amounts, capacities, velocities and caps are FluidUnits integers (1/1000) held as longs (ADR 0090), so addition order never changes the result and server and client match bit for bit.
    ///     Arrays are fixed at construction and a rebuild swaps in a new network; only amounts, fluid ids and face/port velocities change per tick.
    ///     Node-to-face and node-to-port adjacency (CSR) is also built at construction so the stepper can gather each node's own faces.
    /// </summary>
    public sealed class FluidSimNetwork
    {
        // ノード列。座標の辞書順に並び、クライアントの複製も同じ並びを写す
        // Node columns, sorted by position so the client replica copies the same order
        public readonly Vector3Int[] NodePositions;
        public readonly long[] NodeCapacities;
        public readonly long[] NodeAmounts;
        public readonly FluidId[] NodeFluidIds;

        // 面列。NodeA は座標の辞書順で小さい側(正準側)。速度は NodeA→NodeB が正
        // Face columns; NodeA is the lexicographically smaller (canonical) side and velocity is positive from NodeA to NodeB
        public readonly int[] FaceNodeA;
        public readonly int[] FaceNodeB;
        public readonly long[] FaceFlowCapacities;
        public readonly long[] FaceVelocities;
        public readonly bool[] FaceAllowAToB;
        public readonly bool[] FaceAllowBToA;

        // 境界ポートは数が少なく相手(機械か複製)で振る舞いが違うので、オブジェクトのまま持つ
        // Boundary ports stay objects: they are few and behave differently by counterpart (machine or replica)
        public readonly IFluidBoundaryPort[] Ports;

        // ノード i に接する面は NodeFaces[NodeFaceStart[i] .. NodeFaceStart[i+1]) で、面番号の昇順。ポートも同じ形
        // The faces touching node i are NodeFaces[NodeFaceStart[i] .. NodeFaceStart[i+1]) in ascending face index; ports use the same shape
        public readonly int[] NodeFaceStart;
        public readonly int[] NodeFaces;
        public readonly int[] NodePortStart;
        public readonly int[] NodePorts;

        // 現在の状態のハッシュ。Stepper が最終パスで全要素を足し直し、境界からの流入は要素の差分で更新し、構築直後は全走査で求める。加算で畳むので要素単位の差し替えが O(1) で済む
        // Hash of the current state: the stepper re-sums every element in its final pass, boundary inflows update it by element delta, and construction computes it with a full walk; the additive fold makes a per-element replacement O(1)
        public uint LastStateHash { get; private set; }

        private readonly Dictionary<Vector3Int, int> _nodeIndexByPosition;

        public int NodeCount => NodePositions.Length;
        public int FaceCount => FaceNodeA.Length;

        public FluidSimNetwork(Vector3Int[] nodePositions, long[] nodeCapacities, long[] nodeAmounts, FluidId[] nodeFluidIds,
            int[] faceNodeA, int[] faceNodeB, long[] faceFlowCapacities, long[] faceVelocities, bool[] faceAllowAToB, bool[] faceAllowBToA,
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

            BuildAdjacency(out NodeFaceStart, out NodeFaces, out NodePortStart, out NodePorts);
            RecomputeStateHash();
        }

        public static FluidSimNetwork CreateEmpty()
        {
            return new FluidSimNetwork(Array.Empty<Vector3Int>(), Array.Empty<long>(), Array.Empty<long>(), Array.Empty<FluidId>(),
                Array.Empty<int>(), Array.Empty<int>(), Array.Empty<long>(), Array.Empty<long>(), Array.Empty<bool>(), Array.Empty<bool>(),
                Array.Empty<IFluidBoundaryPort>());
        }

        // パイプの座標からノード番号を引く。パイプでない座標は false
        // Looks up the node index at a pipe position; false for a position with no pipe
        public bool TryGetNodeIndex(Vector3Int position, out int nodeIndex)
        {
            return _nodeIndexByPosition.TryGetValue(position, out nodeIndex);
        }

        // 表示用の充填率(0〜1)。計算には使わない(計算は Stepper が整数の充填率で行う)
        // Fill rate (0..1) for display; the stepper computes its own integer fill rate
        public double FillRate(int node)
        {
            return NodeAmounts[node] / (double)NodeCapacities[node];
        }

        // 受け入れ規則の唯一の定義。異種流体は拒否(0)、空なら流体IDを引き継ぎ、空き容量でクランプした受け入れ量(単位)を返す。持ち越し中のパイプも同じ規則を使う
        // The single definition of the acceptance rule: reject mismatched fluid (0), an empty holder adopts the incoming id, clamp by free capacity and return the accepted units; unbuilt pipes use the same rule
        public static long ResolveAcceptedUnits(long currentAmount, long capacity, FluidId currentFluidId, FluidId incomingFluidId, long incomingUnits)
        {
            if (incomingUnits <= 0) return 0;
            if (0 < currentAmount && currentFluidId != incomingFluidId) return 0;
            return Math.Max(0, Math.Min(incomingUnits, capacity - currentAmount));
        }

        // 境界(機械のpush等)からの流入。double を切り捨てて単位にし、受け入れ規則でクランプして、実際に足した単位を accepted で返す。差分にはこの値をそのまま記録する。残りは double のまま返す(端数は機械に残る)
        // External inflow from boundaries (machine push etc.): the double is floored to units, clamped by the acceptance rule, and the units actually added are returned in accepted (what the diff records); the remainder stays a double (the fraction stays in the machine)
        public FluidStack AddExternal(int node, FluidStack fluidStack, out long accepted)
        {
            accepted = ResolveAcceptedUnits(NodeAmounts[node], NodeCapacities[node], NodeFluidIds[node], fluidStack.FluidId, FluidUnits.ToUnitsFloor(fluidStack.Amount));
            if (accepted <= 0) return fluidStack;

            // 要素ハッシュの差し替えは流体IDの書き換えより前の値から取る
            // The element-hash swap starts from the value before the fluid id is rewritten
            var before = FluidNetworkStateHash.NodeElement(node, NodeAmounts[node], NodeFluidIds[node].AsPrimitive());
            if (NodeAmounts[node] <= 0) NodeFluidIds[node] = fluidStack.FluidId;
            NodeAmounts[node] += accepted;
            ReplaceNodeElement(node, before);
            return new FluidStack(fluidStack.Amount - FluidUnits.ToAmount(accepted), fluidStack.FluidId);
        }

        // サーバーが受け入れ済みの量をそのまま足す(複製の差分再生)。容量確認はしない。空のノードは流体IDを引き継ぐ
        // Adds units the server already accepted (replica diff replay); no capacity check, and an empty node adopts the fluid id
        public void AddAccepted(int node, FluidId fluidId, long units)
        {
            var before = FluidNetworkStateHash.NodeElement(node, NodeAmounts[node], NodeFluidIds[node].AsPrimitive());
            if (NodeAmounts[node] <= 0) NodeFluidIds[node] = fluidId;
            NodeAmounts[node] += units;
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
            if (0 < NodeAmounts[node]) return;
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

        // ノードごとの接続面・ポートの一覧を CSR で作る。面・ポートを番号順になめて詰めるので、各ノードの一覧は番号の昇順になる
        // Builds per-node face and port adjacency as CSR; faces and ports are walked in index order, so each node's list is ascending
        private void BuildAdjacency(out int[] faceStart, out int[] faces, out int[] portStart, out int[] portIndices)
        {
            var nodeCount = NodePositions.Length;
            faceStart = new int[nodeCount + 1];
            for (var f = 0; f < FaceNodeA.Length; f++)
            {
                faceStart[FaceNodeA[f] + 1]++;
                faceStart[FaceNodeB[f] + 1]++;
            }
            for (var i = 0; i < nodeCount; i++) faceStart[i + 1] += faceStart[i];
            faces = new int[faceStart[nodeCount]];
            var faceCursor = new int[nodeCount];
            for (var f = 0; f < FaceNodeA.Length; f++)
            {
                var a = FaceNodeA[f];
                var b = FaceNodeB[f];
                faces[faceStart[a] + faceCursor[a]++] = f;
                faces[faceStart[b] + faceCursor[b]++] = f;
            }

            portStart = new int[nodeCount + 1];
            for (var p = 0; p < Ports.Length; p++) portStart[Ports[p].NodeIndex + 1]++;
            for (var i = 0; i < nodeCount; i++) portStart[i + 1] += portStart[i];
            portIndices = new int[portStart[nodeCount]];
            var portCursor = new int[nodeCount];
            for (var p = 0; p < Ports.Length; p++)
            {
                var node = Ports[p].NodeIndex;
                portIndices[portStart[node] + portCursor[node]++] = p;
            }
        }
    }
}
