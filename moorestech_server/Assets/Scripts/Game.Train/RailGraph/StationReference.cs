using Game.Block.Interface;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Train.RailGraph
{
    public class StationReference
    {
        public IBlock StationBlock { get; }
        public StationNodeRole NodeRole { get; }

        public StationReference(IBlock stationBlock, StationNodeRole role)
        {
            StationBlock = stationBlock;
            NodeRole = role;
        }

        // 必要に応じて座標を取得  
        public Vector3Int GetStationPosition() => StationBlock.BlockPositionInfo.OriginalPos;
    }

    public enum StationNodeRole
    {
        Entry,
        Exit,
    }
}