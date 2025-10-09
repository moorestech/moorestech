using Game.Block.Interface;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Game.Train.RailGraph
{
    public class StationReference
    {
        public IBlock StationBlock { get; private set; }

        public StationNodeRole NodeRole { get; private set; }
        public StationNodeSide NodeSide { get; private set; }

        public StationReference()
        {
            SetStationReference(null, StationNodeRole.Entry, StationNodeSide.Front);
        }
        public void SetStationReference(IBlock stationBlock, StationNodeRole role, StationNodeSide side)
        {
            StationBlock = stationBlock;
            NodeRole = role;
            NodeSide = side;
        }

        //同じ駅かつEntry-Exitのつがいならtrueを返す
        public bool IsPairWith(StationReference other)
        {
            if (other == null) return false;
            if (StationBlock == null || other.StationBlock == null) return false;
            if (StationBlock.BlockInstanceId != other.StationBlock.BlockInstanceId) return false;
            if (NodeSide != other.NodeSide) return false;
            return NodeRole != other.NodeRole; // Entry-Exitの組み合わせであることを確認
        }

        // 必要に応じて座標を取得  
        public Vector3Int GetStationPosition() => StationBlock.BlockPositionInfo.OriginalPos;
    }

    public enum StationNodeRole
    {
        Entry,
        Exit,
    }
    public enum StationNodeSide 
    {
        Front,
        Back
    }


}