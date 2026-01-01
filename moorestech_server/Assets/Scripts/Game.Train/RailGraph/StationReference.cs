using Game.Block.Interface;
using UnityEngine;

namespace Game.Train.RailGraph
{
    public class StationReference
    {
        public IBlock StationBlock { get; private set; }
        public bool HasStation => StationBlock != null || _hasStation;
        public BlockInstanceId StationBlockInstanceId => StationBlock != null ? StationBlock.BlockInstanceId : _stationBlockInstanceId;
        public Vector3Int StationPosition => StationBlock != null ? StationBlock.BlockPositionInfo.OriginalPos : _stationPosition;

        public StationNodeRole NodeRole { get; private set; }
        public StationNodeSide NodeSide { get; private set; }

        private BlockInstanceId _stationBlockInstanceId;
        private Vector3Int _stationPosition;
        private bool _hasStation;

        public StationReference()
        {
            ClearStationReference();
        }

        public void SetStationReference(IBlock stationBlock, StationNodeRole role, StationNodeSide side)
        {
            // 駅参照をBlockインスタンスから設定する
            // Set station reference data from a block instance.
            StationBlock = stationBlock;
            NodeRole = role;
            NodeSide = side;
            if (stationBlock == null)
            {
                _stationBlockInstanceId = default;
                _stationPosition = default;
                _hasStation = false;
                return;
            }
            _stationBlockInstanceId = stationBlock.BlockInstanceId;
            _stationPosition = stationBlock.BlockPositionInfo.OriginalPos;
            _hasStation = true;
        }

        public void SetStationReference(BlockInstanceId stationBlockInstanceId, Vector3Int stationPosition, StationNodeRole role, StationNodeSide side)
        {
            // 駅参照を軽量データから設定する
            // Set station reference data from lightweight values.
            StationBlock = null;
            NodeRole = role;
            NodeSide = side;
            _stationBlockInstanceId = stationBlockInstanceId;
            _stationPosition = stationPosition;
            _hasStation = true;
        }

        //蜷後§鬧・°縺､Entry-Exit縺ｮ縺､縺後＞縺ｪ繧液rue繧定ｿ斐☆
        public bool IsPairWith(StationReference other)
        {
            if (other == null) return false;
            if (!HasStation || !other.HasStation) return false;
            if (!StationBlockInstanceId.Equals(other.StationBlockInstanceId)) return false;
            if (NodeSide != other.NodeSide) return false;
            return NodeRole != other.NodeRole; // Entry-Exit縺ｮ邨・∩蜷医ｏ縺帙〒縺ゅｋ縺薙→繧堤｢ｺ隱・
        }

        // 蠢・ｦ√↓蠢懊§縺ｦ蠎ｧ讓吶ｒ蜿門ｾ・ 
        public Vector3Int GetStationPosition() => StationPosition;

        // 駅参照を未設定状態に戻す
        // Reset station reference to the empty state.
        private void ClearStationReference()
        {
            StationBlock = null;
            NodeRole = StationNodeRole.Entry;
            NodeSide = StationNodeSide.Front;
            _stationBlockInstanceId = default;
            _stationPosition = default;
            _hasStation = false;
        }
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
