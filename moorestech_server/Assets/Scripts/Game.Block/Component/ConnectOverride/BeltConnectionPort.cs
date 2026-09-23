using Game.Block.Blocks.BeltConveyor;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Component.ConnectOverride
{
    // 2倍座標の境界と外向き法線を持つ、World登録に依存しない面。
    // A face in doubled coordinates, independent of World registration.
    internal readonly struct BeltConnectionPort
    {
        internal readonly Vector3Int OwnerCell;
        internal readonly BeltConveyorSlopeType Slope;
        internal readonly IBlockConnector Connector;
        internal readonly Vector3Int Boundary;
        internal readonly Vector3Int OutwardNormal;

        internal BeltConnectionPort(Vector3Int ownerCell, BeltConveyorSlopeType slope,
            IBlockConnector connector, Vector3Int boundary, Vector3Int outwardNormal)
        {
            OwnerCell = ownerCell;
            Slope = slope;
            Connector = connector;
            Boundary = boundary;
            OutwardNormal = outwardNormal;
        }
    }
}
