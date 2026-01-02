using System;
using Mooresmaster.Model.GearModule;
using UnityEngine;

namespace Game.Gear.Common
{
    /// <summary>
    /// GearConnectsItemをIGearConnectorに変換するアダプタ
    /// Adapter to convert GearConnectsItem to IGearConnector
    /// </summary>
    public class GearConnectorAdapter : IGearConnector
    {
        public Guid ConnectorGuid { get; }
        public Vector3Int Offset { get; }
        public Vector3Int[] Directions { get; }
        public IGearConnectOption Option { get; }

        public GearConnectorAdapter(GearConnectsItem item)
        {
            ConnectorGuid = item.ConnectorGuid;
            Offset = item.Offset;
            Directions = item.Directions;
            Option = new GearConnectOptionAdapter(item.Option);
        }
    }
}
