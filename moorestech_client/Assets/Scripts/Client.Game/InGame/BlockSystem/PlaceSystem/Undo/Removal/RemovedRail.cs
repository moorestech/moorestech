using Server.Protocol.PacketResponse;
using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.RailGraph;
using Core.Master;
using Game.Train.SaveLoad;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo.Removal
{
    /// <summary>
    ///     撤去したレール1本。端点はノードIDでなくConnectionDestinationで持つ（再設置でノードGuid/IDが変わるため）
    ///     One removed rail; endpoints are ConnectionDestinations, not node ids, since re-placement regenerates node Guids/ids
    /// </summary>
    public class RemovedRail : IRemovedObject
    {
        private readonly ConnectionDestination _from;
        private readonly ConnectionDestination _to;
        private readonly Guid _railTypeGuid;

        private RemovedRail(ConnectionDestination from, ConnectionDestination to, Guid railTypeGuid)
        {
            _from = from;
            _to = to;
            _railTypeGuid = railTypeGuid;
        }

        // canonical化済みの区間から作る。記録しない理由（未同期・駅内部・種類Emptyの無償区間）を結果で区別する
        // Build from a canonical edge; the result distinguishes why it is not recorded (unsynced, station-internal, Empty-typed free segment)
        public static RemovedRailCreateResult Create(RailGraphClientCache cache, int canonicalFrom, int canonicalTo)
        {
            if (!cache.TryGetNode(canonicalFrom, out var fromNode) || !cache.TryGetNode(canonicalTo, out var toNode)) return RemovedRailCreateResult.NotCreated(RemovedRailCreateOutcome.NodeNotSynced);
            if (RailEdgeClassifier.IsStationInternalEdge(fromNode, toNode)) return RemovedRailCreateResult.NotCreated(RemovedRailCreateOutcome.StationInternal);

            // 種類Emptyは駅隣接の自動レール等の無償区間。駅の再設置で自動的に戻るので記録しない
            // Empty-typed edges are free segments such as station-adjacent auto rails; station re-placement restores them, so they are not recorded
            if (!cache.TryGetRailType(canonicalFrom, canonicalTo, out var railTypeGuid) || railTypeGuid == Guid.Empty) return RemovedRailCreateResult.NotCreated(RemovedRailCreateOutcome.FreeSegment);

            return RemovedRailCreateResult.Created(new RemovedRail(fromNode.ConnectionDestination, toNode.ConnectionDestination, railTypeGuid));
        }

        public object RestoreKey => (_from, _to);

        public BlockRestoreOutcome AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy)
        {
            return BlockRestoreOutcome.NotABlock;
        }

        public void SendConnectionRestore(IRemovalRestoreSender sender)
        {
            sender.ConnectRail(_from, _to, _railTypeGuid);
        }
    }
}
