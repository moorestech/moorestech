using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.EnergySystem;
using Mooresmaster.Model.BlocksModule;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// ワールド全ブロックから候補を組み立て、選定はElectricWireAutoConnectSelectorに委譲する
    /// Builds candidates from all world blocks and delegates selection to ElectricWireAutoConnectSelector
    /// </summary>
    public static class ElectricWireAutoConnectTargetCollector
    {
        public static List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectPoleTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo)
        {
            var (candidates, connectors) = BuildWorldCandidates();
            return ToConnectorResults(ElectricWireAutoConnectSelector.SelectPoleTargets(ownParam, ownInfo, candidates), connectors);
        }

        public static List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectPoleMachineTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo, int usedCount)
        {
            var (candidates, connectors) = BuildWorldCandidates();
            return ToConnectorResults(ElectricWireAutoConnectSelector.SelectPoleMachineTargets(ownParam, ownInfo, usedCount, candidates), connectors);
        }

        public static List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectMachineTargets(BlockMasterElement blockMaster, BlockPositionInfo ownInfo)
        {
            var (candidates, connectors) = BuildWorldCandidates();
            return ToConnectorResults(ElectricWireAutoConnectSelector.SelectMachineTargets(blockMaster.BlockParam, ownInfo, candidates), connectors);
        }

        // ワールド全ブロックからワイヤー端点候補とConnector逆引き表を組み立てる
        // Build endpoint candidates and a connector lookup from all world blocks
        private static (List<ElectricWireConnectCandidate> Candidates, Dictionary<BlockInstanceId, IElectricWireConnector> Connectors) BuildWorldCandidates()
        {
            var candidates = new List<ElectricWireConnectCandidate>();
            var connectors = new Dictionary<BlockInstanceId, IElectricWireConnector>();

            foreach (var worldBlock in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
            {
                if (!worldBlock.Block.TryGetComponent<IElectricWireConnector>(out var connector)) continue;

                candidates.Add(new ElectricWireConnectCandidate(connector.BlockInstanceId, worldBlock.Block.BlockMasterElement.BlockParam, worldBlock.BlockPositionInfo, connector.WireConnections.Count));
                connectors[connector.BlockInstanceId] = connector;
            }

            return (candidates, connectors);
        }

        // 選定結果のInstanceIdをConnector付きタプルへ復元する
        // Restore selected instance ids into connector-bearing tuples
        private static List<(BlockInstanceId, IElectricWireConnector, float)> ToConnectorResults(List<(BlockInstanceId TargetId, float Distance)> selected, Dictionary<BlockInstanceId, IElectricWireConnector> connectors)
        {
            return selected.Select(s => (s.TargetId, connectors[s.TargetId], s.Distance)).ToList();
        }
    }
}
