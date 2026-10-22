using Game.Fluid.Simulation;
using Game.Fluid.Sync.State;

namespace Game.Fluid.Sync.Replica
{
    // 全量からノード・面・ポートを生成し、番号どおりに配線する。サーバーの網と同じ並びになるので、Stepperの走査順も一致する
    // Creates nodes, faces and ports from the full state and wires them by index; the order matches the server network, so the stepper iterates identically
    public static class FluidNetworkReplicaAssembler
    {
        public static FluidNetworkReplica Assemble(FluidNetworkFullState fullState)
        {
            var nodes = new FluidSimNode[fullState.Nodes.Length];
            for (var i = 0; i < nodes.Length; i++)
            {
                var state = fullState.Nodes[i];
                nodes[i] = new FluidSimNode(state.Position, state.Capacity)
                {
                    Amount = state.Amount,
                    FluidId = state.FluidId,
                };
            }

            var faces = new FluidSimFace[fullState.Faces.Length];
            for (var i = 0; i < faces.Length; i++)
            {
                var state = fullState.Faces[i];
                faces[i] = new FluidSimFace(nodes[state.NodeAIndex], nodes[state.NodeBIndex], state.FlowCapacityPerTick, state.Velocity)
                {
                    AllowAToB = state.AllowAToB,
                    AllowBToA = state.AllowBToA,
                };
            }

            var ports = new FluidReplicaBoundaryPort[fullState.Ports.Length];
            for (var i = 0; i < ports.Length; i++)
            {
                var state = fullState.Ports[i];
                ports[i] = new FluidReplicaBoundaryPort(nodes[state.NodeIndex], state.FlowCapacityPerTick, state.Velocity);
            }

            return new FluidNetworkReplica(nodes, faces, ports);
        }
    }
}
