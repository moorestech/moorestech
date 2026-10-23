using Game.Fluid.Simulation;
using Game.Fluid.Sync.Diff;
using UnityEngine;

namespace Game.Fluid.Sync.Replica
{
    // 全量から組み立てた、ワールドを持たない流体網の複製。クライアントはこれを毎tick進め、境界の出入りは差分で与える
    // A replica of the fluid network assembled from the full state without a world; the client ticks it and feeds boundary exchanges from diffs
    public sealed class FluidNetworkReplica
    {
        // 網はサーバーと同じ型。ポートだけ複製用の実装で、同じ配列を網の Ports としても持つ(読み出しは Network.Ports 経由)
        // The network is the same type as the server's; only the ports are replica implementations, held also as the network's Ports (read through Network.Ports)
        public readonly FluidSimNetwork Network;
        private readonly FluidReplicaBoundaryPort[] _ports;
        private readonly FluidSimulationScratch _scratch;

        public FluidNetworkReplica(FluidSimNetwork network, FluidReplicaBoundaryPort[] ports)
        {
            Network = network;
            _ports = ports;
            _scratch = new FluidSimulationScratch(network);
        }

        public bool TryGetNodeIndex(Vector3Int position, out int nodeIndex)
        {
            return Network.TryGetNodeIndex(position, out nodeIndex);
        }

        // サーバーと同じtickを再生する。渡した量を予告してから1tick進め、その後の流入を同じ順で足す
        // Replays the same tick as the server: announce the deliveries, advance one tick, then add the inflows in the same order
        // 範囲外の番号・試みの無い(または予告より少ない)予告・空でないノードへの異種流体は複製がサーバーと食い違った印。falseを返し、呼び出し側が停止を決める
        // An out-of-range index, an announcement never attempted or larger than the attempt, or a different fluid into a non-empty node marks the replica as diverged; returns false and the caller decides to stop
        public bool Tick(FluidTickDiff diff)
        {
            var consistent = true;
            foreach (var delivery in diff.Deliveries)
            {
                if (0 <= delivery.PortIndex && delivery.PortIndex < _ports.Length)
                {
                    _ports[delivery.PortIndex].AnnounceDelivery(delivery.DeliveredAmount);
                    continue;
                }
                Debug.LogError($"[FluidNetwork] replica got a delivery for port {delivery.PortIndex} (ports: {_ports.Length}); the replica has diverged from the server.");
                consistent = false;
            }

            FluidSimulationStepper.Step(Network, _scratch);

            for (var i = 0; i < _ports.Length; i++)
            {
                if (!_ports[i].ClearAnnouncement()) continue;
                Debug.LogError($"[FluidNetwork] replica never attempted, or attempted less than, the delivery the server made at port {i}; the replica has diverged from the server.");
                consistent = false;
            }

            foreach (var inflow in diff.Inflows)
            {
                if (TryApplyInflow(inflow)) continue;
                consistent = false;
            }
            return consistent;

            #region Internal

            bool TryApplyInflow(FluidNodeInflowRecord inflow)
            {
                if (inflow.NodeIndex < 0 || Network.NodeCount <= inflow.NodeIndex)
                {
                    Debug.LogError($"[FluidNetwork] replica got an inflow for node {inflow.NodeIndex} (nodes: {Network.NodeCount}); the replica has diverged from the server.");
                    return false;
                }

                // サーバーが受け入れた量なので容量確認はしない。中身のある異種流体だけはサーバーが受け入れない組み合わせなので食い違いとして報告する
                // The amount was accepted by the server, so no capacity check; only a different fluid into a non-empty node is a combination the server never accepts, hence a divergence
                var hasFluid = FluidSimulationConstants.AmountEpsilon < Network.NodeAmounts[inflow.NodeIndex];
                if (hasFluid && Network.NodeFluidIds[inflow.NodeIndex] != inflow.FluidId)
                {
                    Debug.LogError($"[FluidNetwork] replica node {inflow.NodeIndex} holds fluid {Network.NodeFluidIds[inflow.NodeIndex]} but the server accepted {inflow.FluidId}; the replica has diverged from the server.");
                    return false;
                }
                Network.AddAccepted(inflow.NodeIndex, inflow.FluidId, inflow.AcceptedAmount);
                return true;
            }

            #endregion
        }
    }
}
