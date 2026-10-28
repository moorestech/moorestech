namespace Game.Fluid.Sync.State
{
    // ワールド全体の流体網のtick境界での全量。添字がノード・面・ポートの番号で、差分はこの番号で指す
    // The full state of the world-wide fluid network at a tick boundary; the indices are the node, face and port numbers that diffs refer to
    public sealed class FluidNetworkFullState
    {
        public readonly FluidNodeState[] Nodes;
        public readonly FluidFaceState[] Faces;
        public readonly FluidPortState[] Ports;

        public FluidNetworkFullState(FluidNodeState[] nodes, FluidFaceState[] faces, FluidPortState[] ports)
        {
            Nodes = nodes;
            Faces = faces;
            Ports = ports;
        }
    }
}
