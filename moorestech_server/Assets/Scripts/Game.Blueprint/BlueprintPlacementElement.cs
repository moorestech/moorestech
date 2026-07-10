using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Game.Blueprint
{
    public class BlueprintPlacementElement
    {
        public readonly Vector3Int Position;
        public readonly BlockDirection Direction;
        public readonly BlockId BlockId;
        public readonly Dictionary<string, string> Settings;

        public BlueprintPlacementElement(Vector3Int position, BlockDirection direction, BlockId blockId, Dictionary<string, string> settings)
        {
            Position = position;
            Direction = direction;
            BlockId = blockId;
            Settings = settings;
        }
    }
}
