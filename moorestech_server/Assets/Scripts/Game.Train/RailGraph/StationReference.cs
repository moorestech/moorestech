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

        //同じ駅かつEntry-Exitのつがいならtrueを返す
        public bool IsPairWith(StationReference other)
        {
            if (other == null) return false;
            if (StationBlock == null || other.StationBlock == null) return false;
            return StationBlock.BlockInstanceId == other.StationBlock.BlockInstanceId &&
                   NodeRole != other.NodeRole; // Entry-Exitの組み合わせであることを確認
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