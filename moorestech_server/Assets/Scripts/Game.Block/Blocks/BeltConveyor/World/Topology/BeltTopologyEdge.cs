using Game.Block.Interface.Extension;
using System;
using Game.BeltSegment;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using UnityEngine;
namespace Game.Block.Blocks.BeltConveyor
{
    internal sealed class BeltTopologyEdge
    {
        internal readonly IBlock Source;
        internal readonly IBlockInventory Target;
        internal readonly ConnectedInfo Connection;
        internal readonly Vector3Int SourceCell, TargetCell;
        internal readonly BeltDirection Direction;
        internal BeltTopologyEdge(IBlock source, IBlockInventory target, ConnectedInfo connection)
        {
            Source = source; Target = target; Connection = connection;
            SourceCell = connection.SelfConnector == null ? source.BlockPositionInfo.OriginalPos
                : source.BlockPositionInfo.ConvertBlockLocalToWorldCell(connection.SelfConnector.Offset);
            TargetCell = connection.TargetConnectorCell;
            Direction = BeltTopologyGeometry.Direction(TargetCell - SourceCell);
        }
        internal InsertItemContext Context => new(Source.BlockInstanceId, Connection.SelfConnector, Connection.TargetConnector);
        internal static int Compare(BeltTopologyEdge a, BeltTopologyEdge b)
        {
            int c = BeltTopologyGeometry.Compare(a.SourceCell, b.SourceCell);
            if (c != 0) return c;
            c = a.Direction.CompareTo(b.Direction); if (c != 0) return c;
            c = BeltTopologyGeometry.Compare(a.TargetCell, b.TargetCell); if (c != 0) return c;
            c = Nullable.Compare(a.Connection.SelfConnector?.ConnectorGuid, b.Connection.SelfConnector?.ConnectorGuid);
            return c != 0 ? c : Nullable.Compare(a.Connection.TargetConnector?.ConnectorGuid, b.Connection.TargetConnector?.ConnectorGuid);
        }
    }
}
