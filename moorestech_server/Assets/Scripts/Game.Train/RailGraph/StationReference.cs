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
            // 駅参照を Block インスタンスから設定する
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

        // 同じ駅で、Entry/Exit が対になっていれば true を返す
        // Returns true if both references point to the same station and form an Entry/Exit pair.
        public bool IsPairWith(StationReference other)
        {
            if (other == null) return false;
            if (!HasStation || !other.HasStation) return false;
            if (!StationBlockInstanceId.Equals(other.StationBlockInstanceId)) return false;
            if (NodeSide != other.NodeSide) return false;

            // Entry/Exit の役割が異なるものをペアとして扱う
            // Treat different roles (Entry vs Exit) as a valid pair.
            return NodeRole != other.NodeRole;
        }

        // 駅座標を取得する
        // Returns the station world position.
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
