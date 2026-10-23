using System;
using System.Collections.Generic;
using Game.Fluid.Simulation;
using Game.Fluid.Sync.Diff;
using UnityEngine;

namespace Game.Fluid.Sync.Replica
{
    // 全量から組み立てた、ワールドを持たない流体網の複製。クライアントはこれを毎tick進め、境界の出入りは差分で与える
    // A replica of the fluid network assembled from the full state without a world; the client ticks it and feeds boundary exchanges from diffs
    public sealed class FluidNetworkReplica
    {
        // 添字はノード・面・ポートの番号
        // Indices are node, face and port numbers
        public readonly FluidSimNode[] Nodes;
        public readonly FluidSimFace[] Faces;
        public readonly FluidReplicaBoundaryPort[] Ports;
        private readonly Dictionary<Vector3Int, int> _nodeIndexByPosition;

        public FluidNetworkReplica(FluidSimNode[] nodes, FluidSimFace[] faces, FluidReplicaBoundaryPort[] ports)
        {
            Nodes = nodes;
            Faces = faces;
            Ports = ports;
            _nodeIndexByPosition = new Dictionary<Vector3Int, int>(nodes.Length);
            for (var i = 0; i < nodes.Length; i++) _nodeIndexByPosition.Add(nodes[i].Position, i);
        }

        // パイプの座標からノードを引く。パイプでない座標はfalse
        // Looks up the node at a pipe position; false for a position with no pipe
        public bool TryGetNode(Vector3Int position, out FluidSimNode node)
        {
            if (_nodeIndexByPosition.TryGetValue(position, out var index))
            {
                node = Nodes[index];
                return true;
            }
            node = null;
            return false;
        }

        // サーバーと同じtickを再生する。渡した量を予告してから1tick進め、その後の流入を同じ順で足す
        // Replays the same tick as the server: announce the deliveries, advance one tick, then add the inflows in the same order
        // 範囲外の番号・試みの無い予告・空でないノードへの異種流体は複製がサーバーと食い違った印。falseを返し、呼び出し側が停止を決める
        // An out-of-range index, an announcement never attempted, or a different fluid into a non-empty node marks the replica as diverged; returns false and the caller decides to stop
        public bool Tick(FluidTickDiff diff)
        {
            var consistent = true;
            foreach (var delivery in diff.Deliveries)
            {
                if (0 <= delivery.PortIndex && delivery.PortIndex < Ports.Length)
                {
                    Ports[delivery.PortIndex].AnnounceDelivery(delivery.DeliveredAmount);
                    continue;
                }
                Debug.LogError($"[FluidNetwork] replica got a delivery for port {delivery.PortIndex} (ports: {Ports.Length}); the replica has diverged from the server.");
                consistent = false;
            }

            FluidSimulationStepper.Step(Nodes, Faces, Ports);

            for (var i = 0; i < Ports.Length; i++)
            {
                if (!Ports[i].ClearAnnouncement()) continue;
                Debug.LogError($"[FluidNetwork] replica never attempted the delivery the server made at port {i}; the replica has diverged from the server.");
                consistent = false;
            }

            foreach (var inflow in diff.Inflows)
            {
                if (TryApplyInflow(inflow)) continue;
                consistent = false;
            }
            return consistent;
        }

        private bool TryApplyInflow(FluidNodeInflowRecord inflow)
        {
            if (inflow.NodeIndex < 0 || Nodes.Length <= inflow.NodeIndex)
            {
                Debug.LogError($"[FluidNetwork] replica got an inflow for node {inflow.NodeIndex} (nodes: {Nodes.Length}); the replica has diverged from the server.");
                return false;
            }

            // サーバーが受け入れた量なので容量確認はしない。中身のある異種流体だけはサーバーが受け入れない組み合わせなので食い違いとして報告する
            // The amount was accepted by the server, so no capacity check; only a different fluid into a non-empty node is a combination the server never accepts, hence a divergence
            var node = Nodes[inflow.NodeIndex];
            var hasFluid = FluidSimulationConstants.AmountEpsilon < node.Amount;
            if (hasFluid && node.FluidId != inflow.FluidId)
            {
                Debug.LogError($"[FluidNetwork] replica node {inflow.NodeIndex} holds fluid {node.FluidId} but the server accepted {inflow.FluidId}; the replica has diverged from the server.");
                return false;
            }
            if (!hasFluid) node.FluidId = inflow.FluidId;
            node.Amount += inflow.AcceptedAmount;
            return true;
        }
    }
}
