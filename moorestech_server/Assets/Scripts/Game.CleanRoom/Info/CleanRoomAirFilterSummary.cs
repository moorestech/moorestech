using Game.Block.Interface.Component;
using UnityEngine;

namespace Game.CleanRoom.Info
{
    /// <summary>
    ///     部屋の中の清浄機1台の位置と稼働状況
    ///     Position and operating state of one purifier in a room
    /// </summary>
    public class CleanRoomAirFilterSummary
    {
        public Vector3Int Position { get; }
        public CleanRoomAirFilterStatus Status { get; }

        public CleanRoomAirFilterSummary(Vector3Int position, CleanRoomAirFilterStatus status)
        {
            Position = position;
            Status = status;
        }
    }
}
