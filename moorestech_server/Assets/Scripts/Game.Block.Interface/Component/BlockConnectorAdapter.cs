using System;
using Mooresmaster.Model.FluidInventoryConnectsModule;
using Mooresmaster.Model.GearModule;
using Mooresmaster.Model.InventoryConnectsModule;
using UnityEngine;

namespace Game.Block.Interface.Component
{
    /// <summary>
    /// 各種コネクタ型をIBlockConnectorに変換するアダプタ
    /// Adapter to convert various connector types to IBlockConnector
    /// </summary>
    public class BlockConnectorAdapter : IBlockConnector
    {
        public Guid ConnectorGuid { get; }
        public Vector3Int Offset { get; }
        public Vector3Int[] Directions { get; }
        public object ConnectOption { get; }

        // InventoryConnects用
        // For InventoryConnects
        public BlockConnectorAdapter(InputConnectsItem item)
        {
            ConnectorGuid = item.ConnectorGuid;
            Offset = item.Offset;
            Directions = item.Directions;
            ConnectOption = item.Option;
        }

        public BlockConnectorAdapter(OutputConnectsItem item)
        {
            ConnectorGuid = item.ConnectorGuid;
            Offset = item.Offset;
            Directions = item.Directions;
            ConnectOption = item.Option;
        }

        // FluidInventoryConnects用
        // For FluidInventoryConnects
        public BlockConnectorAdapter(InflowConnectsItem item)
        {
            ConnectorGuid = item.ConnectorGuid;
            Offset = item.Offset;
            Directions = item.Directions;
            ConnectOption = item.Option;
        }

        public BlockConnectorAdapter(OutflowConnectsItem item)
        {
            ConnectorGuid = item.ConnectorGuid;
            Offset = item.Offset;
            Directions = item.Directions;
            ConnectOption = item.Option;
        }

        // GearConnects用
        // For GearConnects
        public BlockConnectorAdapter(GearConnectsItem item)
        {
            ConnectorGuid = item.ConnectorGuid;
            Offset = item.Offset;
            Directions = item.Directions;
            ConnectOption = item.Option;
        }
    }
}
