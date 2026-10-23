using Core.Master;
using Game.Fluid.Simulation;
using Game.Fluid.Sync.State;
using UnityEngine;

namespace Game.Fluid.Sync.Replica
{
    // 全量から網の配列と複製ポートを作る。番号どおりに写すのでサーバーの網と同じ並びになり、Stepper の走査順も一致する
    // Builds the network arrays and replica ports from the full state; indices are copied as they are, so the order matches the server network and the stepper iterates identically
    public static class FluidNetworkReplicaAssembler
    {
        public static FluidNetworkReplica Assemble(FluidNetworkFullState fullState)
        {
            var nodes = fullState.Nodes;
            var positions = new Vector3Int[nodes.Length];
            var capacities = new long[nodes.Length];
            var amounts = new long[nodes.Length];
            var fluidIds = new FluidId[nodes.Length];
            for (var i = 0; i < nodes.Length; i++)
            {
                positions[i] = nodes[i].Position;
                capacities[i] = nodes[i].Capacity;
                amounts[i] = nodes[i].Amount;
                fluidIds[i] = nodes[i].FluidId;
            }

            var faces = fullState.Faces;
            var faceA = new int[faces.Length];
            var faceB = new int[faces.Length];
            var faceCaps = new long[faces.Length];
            var velocities = new long[faces.Length];
            var allowAToB = new bool[faces.Length];
            var allowBToA = new bool[faces.Length];
            for (var i = 0; i < faces.Length; i++)
            {
                faceA[i] = faces[i].NodeAIndex;
                faceB[i] = faces[i].NodeBIndex;
                faceCaps[i] = faces[i].FlowCapacityPerTick;
                velocities[i] = faces[i].Velocity;
                allowAToB[i] = faces[i].AllowAToB;
                allowBToA[i] = faces[i].AllowBToA;
            }

            var ports = new FluidReplicaBoundaryPort[fullState.Ports.Length];
            for (var i = 0; i < ports.Length; i++)
            {
                var state = fullState.Ports[i];
                ports[i] = new FluidReplicaBoundaryPort(state.NodeIndex, state.FlowCapacityPerTick, state.Velocity);
            }

            var network = new FluidSimNetwork(positions, capacities, amounts, fluidIds, faceA, faceB, faceCaps, velocities, allowAToB, allowBToA, ports);
            return new FluidNetworkReplica(network, ports);
        }
    }
}
