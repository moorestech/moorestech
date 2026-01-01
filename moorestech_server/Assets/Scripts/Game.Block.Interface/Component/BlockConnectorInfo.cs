using System;
using UnityEngine;

namespace Game.Block.Interface.Component
{
    public sealed class BlockConnectorInfo
    {
        public Guid ConnectorGuid { get; }
        public Vector3Int Offset { get; }
        public Vector3Int[] Directions { get; }
        public object Option { get; }

        public BlockConnectorInfo(Guid connectorGuid, Vector3Int offset, Vector3Int[] directions, object option)
        {
            ConnectorGuid = connectorGuid;
            Offset = offset;
            Directions = directions;
            Option = option;
        }
    }
}
