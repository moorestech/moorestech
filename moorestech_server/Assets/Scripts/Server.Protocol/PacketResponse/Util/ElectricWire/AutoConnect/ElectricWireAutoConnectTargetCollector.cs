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
    /// ワールド全ブロックから候補構築、選定はSelectorへ委譲
    /// Builds candidates from world blocks; delegates selection to Selector
    /// </summary>
    public static class ElectricWireAutoConnectTargetCollector
    {
        public static List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectTargets(BlockMasterElement blockMaster, BlockPositionInfo ownInfo)
        {
            var (candidates, connectors) = BuildWorldCandidates();
            return ToConnectorResults(ElectricWireAutoConnectSelector.SelectPlacementTargets(blockMaster.BlockParam, ownInfo, candidates), connectors);
        }

        // 全ブロックから候補とConnector逆引き表を構築
        // Build candidates and a connector lookup from world blocks
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

        // 選定結果をConnector付きタプルへ復元
        // Restore selected ids into connector-bearing tuples
        private static List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> ToConnectorResults(List<(BlockInstanceId TargetId, float Distance)> selected, Dictionary<BlockInstanceId, IElectricWireConnector> connectors)
        {
            return selected.Select(s => (s.TargetId, connectors[s.TargetId], s.Distance)).ToList();
        }
    }
}
