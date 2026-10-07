using Server.Protocol.PacketResponse;
using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.RailGraph;
using Core.Master;
using Game.Train.SaveLoad;
using UnityEngine;

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

        // 直接撤去と巻き込みの違いを反映して採取する
        // Capture according to whether the edge is deleted directly or by cascade
        public static void Capture(RailGraphClientCache cache, int canonicalFrom, int canonicalTo, RemovedRailCaptureContext context, RemovedObjectCollector collector)
        {
            if (!cache.TryGetNode(canonicalFrom, out var fromNode) || !cache.TryGetNode(canonicalTo, out var toNode))
            {
                collector.AddUnrecordable((canonicalFrom, canonicalTo), $"rail {canonicalFrom}->{canonicalTo}: node not synced");
                return;
            }
            if (RailEdgeClassifier.IsStationInternalEdge(fromNode, toNode)) return;

            // 種類未同期は復元できず、無償区間は直接切断だけ通知対象にする
            // Unsynced types cannot be restored; only directly deleted free edges count as skipped
            if (!cache.TryGetRailType(canonicalFrom, canonicalTo, out var railTypeGuid))
            {
                collector.AddUnrecordable((canonicalFrom, canonicalTo), $"rail {canonicalFrom}->{canonicalTo}: type not synced");
                return;
            }
            if (railTypeGuid == Guid.Empty)
            {
                if (context == RemovedRailCaptureContext.Direct)
                    collector.AddUnrecordable((canonicalFrom, canonicalTo), $"costless rail {canonicalFrom}->{canonicalTo}: direct restore unavailable");
                return;
            }

            collector.Add(new RemovedRail(fromNode.ConnectionDestination, toNode.ConnectionDestination, railTypeGuid));
        }

        public object RestoreKey => (_from, _to);

        public BlockRestoreOutcome AppendBlockRestore(List<PlaceInfo> placeInfos, IBlockOccupancyQuery occupancy, HashSet<Vector3Int> skippedBlockPositions)
        {
            return BlockRestoreOutcome.NotABlock;
        }

        public bool TrySendConnectionRestore(IRemovalRestoreSender sender, HashSet<Vector3Int> skippedBlockPositions)
        {
            // 跡地に別ブロックがある端点へ引くと意図しない配線と素材消費になる
            // Drawing to an endpoint now occupied by another block would wire it unintentionally and consume materials
            if (skippedBlockPositions.Contains(_from.blockPosition) || skippedBlockPositions.Contains(_to.blockPosition))
            {
                Debug.LogWarning($"[RemovalRestore] skip rail restore: endpoint block not restored {(Vector3Int)_from.blockPosition}-{(Vector3Int)_to.blockPosition}");
                return false;
            }
            sender.ConnectRail(_from, _to, _railTypeGuid);
            return true;
        }
    }

    public enum RemovedRailCaptureContext
    {
        Direct,
        Cascade,
    }
}
