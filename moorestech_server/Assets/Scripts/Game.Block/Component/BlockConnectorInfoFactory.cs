using System.Collections.Generic;
using Game.Block.Interface.Component;

namespace Game.Block.Component
{
    public static class BlockConnectorInfoFactory
    {
        public static IReadOnlyList<BlockConnectorInfo> FromConnectors<TConnector>(IReadOnlyList<TConnector> connectors)
        {
            if (connectors == null) return null;

            // コネクタのメタ情報を生成する
            // Build connector metadata
            var result = new List<BlockConnectorInfo>(connectors.Count);
            foreach (var connector in connectors)
            {
                if (connector == null) continue;
                dynamic value = connector;
                result.Add(new BlockConnectorInfo(value.ConnectorGuid, value.Offset, value.Directions, value.Option));
            }

            return result;
        }
    }
}
