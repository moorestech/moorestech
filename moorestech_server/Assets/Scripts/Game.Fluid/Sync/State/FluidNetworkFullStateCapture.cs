using System.Collections.Generic;
using Game.Fluid.Simulation;

namespace Game.Fluid.Sync.State
{
    // tick境界で、ノード・面・ポートの一覧から全量を切り出す。サーバーの網からも、クライアントの複製からも同じ手順で切り出せる
    // Cuts the full state out of the node, face and port lists at a tick boundary; the same procedure serves the server network and the client replica
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
