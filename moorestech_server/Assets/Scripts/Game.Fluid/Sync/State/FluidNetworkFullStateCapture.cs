using Game.Fluid.Simulation;

namespace Game.Fluid.Sync.State
{
    // tick境界で、網の配列から全量(送受信する形)を切り出す。途中参加と再構築tickの全量送信だけが使い、ハッシュは切り出さずに網が持つ
    // Cuts the full state (the wire form) out of the network arrays at a tick boundary; only the join-time and rebuilt-tick full-state sends use it, while the hash lives on the network without a capture
    public static class FluidNetworkFullStateCapture
    {
        public static FluidNetworkFullState Capture(FluidSimNetwork network)
        {
            var nodeStates = new FluidNodeState[network.NodeCount];
            for (var i = 0; i < nodeStates.Length; i++)
            {
                nodeStates[i] = new FluidNodeState(network.NodePositions[i], network.NodeCapacities[i], network.NodeAmounts[i], network.NodeFluidIds[i]);
            }

            var faceStates = new FluidFaceState[network.FaceCount];
            for (var i = 0; i < faceStates.Length; i++)
            {
                faceStates[i] = new FluidFaceState(network.FaceNodeA[i], network.FaceNodeB[i], network.FaceFlowCapacities[i], network.FaceVelocities[i], network.FaceAllowAToB[i], network.FaceAllowBToA[i]);
            }

            var portStates = new FluidPortState[network.Ports.Length];
            for (var i = 0; i < portStates.Length; i++)
            {
                var port = network.Ports[i];
                portStates[i] = new FluidPortState(port.NodeIndex, port.FlowCapacityPerTick, port.Velocity);
            }

            return new FluidNetworkFullState(nodeStates, faceStates, portStates);
        }
    }
}
