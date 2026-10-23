using System.Collections.Generic;
using Game.Fluid.Simulation;

namespace Game.Fluid.Sync.State
{
    // tick境界で、サーバーの網のノード・面・ポートから全量(送受信する形)を切り出す。途中参加と再構築tickの全量送信だけが使い、ハッシュは切り出さずに直接混ぜる
    // Cuts the full state (the wire form) out of the server network's nodes, faces and ports at a tick boundary; only the join-time and rebuilt-tick full-state sends use it, while the hash mixes directly without a capture
    public static class FluidNetworkFullStateCapture
    {
        public static FluidNetworkFullState Capture(IReadOnlyList<FluidSimNode> nodes, IReadOnlyList<FluidSimFace> faces, IReadOnlyList<IFluidBoundaryPort> ports)
        {
            // 面とポートはノードを参照で持つので、番号へ写すための逆引きを作る
            // Faces and ports hold nodes by reference, so build the reverse lookup to express them as indices
            var indexByNode = new Dictionary<FluidSimNode, int>(nodes.Count);
            var nodeStates = new FluidNodeState[nodes.Count];
            for (var i = 0; i < nodes.Count; i++)
            {
                var node = nodes[i];
                indexByNode.Add(node, i);
                nodeStates[i] = new FluidNodeState(node.Position, node.Capacity, node.Amount, node.FluidId);
            }

            var faceStates = new FluidFaceState[faces.Count];
            for (var i = 0; i < faces.Count; i++)
            {
                var face = faces[i];
                faceStates[i] = new FluidFaceState(indexByNode[face.NodeA], indexByNode[face.NodeB], face.FlowCapacityPerTick, face.Velocity, face.AllowAToB, face.AllowBToA);
            }

            var portStates = new FluidPortState[ports.Count];
            for (var i = 0; i < ports.Count; i++)
            {
                var port = ports[i];
                portStates[i] = new FluidPortState(indexByNode[port.PipeNode], port.FlowCapacityPerTick, port.Velocity);
            }

            return new FluidNetworkFullState(nodeStates, faceStates, portStates);
        }
    }
}
