using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Game.Train.RailGraph;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    public class DeleteTargetRail : MonoBehaviour, IDeleteTarget
    {       
        public BezierRailChain RailChain { get; private set; }
        private RailSegmentCarrier _railSegmentCarrier;
        private RailGraphClientCache _railGraphClientCache;
        
        public RailSegmentCarrier RailSegmentCarrier
        {
            get
            {
                if (_railSegmentCarrier) return _railSegmentCarrier;
                return _railSegmentCarrier = GetComponent<RailSegmentCarrier>();
            }
        }
        
        public void SetRailGraphCache(RailGraphClientCache cache)
        {
            _railGraphClientCache = cache;
        }
        
        public void SetParentBezierRailChain(BezierRailChain parent)
        {
            RailChain = parent;
        }

        public void SetRemovePreviewing()
        {
            RailChain.SetRemovePreviewing();
        }
        public void ResetMaterial()
        {
            RailChain.ResetMaterial();
        }
        
        public bool IsRemovable(out string reason)
        {
            var canDelete = CanDelete();
            reason = canDelete switch
            {
                DeleteDeniedReason.None => null,
                DeleteDeniedReason.StationInternalEdge => "駅内部のレールは削除できません。",
                DeleteDeniedReason.NodeInUseByTrain => "レール上に車両があります。",
                DeleteDeniedReason.UnknownError => "不明なエラー",
                _ => null,
            };
            return canDelete == DeleteDeniedReason.None;
        }
        
        public void Delete()
        {
            var carrier = RailSegmentCarrier;
            var railSegment = carrier.GetRailSegment();
            var segmentId = railSegment.GetSegmentId();
            var minNodeId = segmentId.GetMinNodeId();
            var maxNodeId = segmentId.GetMaxNodeId();
            ResolveDisconnectDirection(railSegment, minNodeId, maxNodeId, out var fromId, out var toId);
            
            if (!_railGraphClientCache.TryGetNode(fromId, out var fromNode)) return;
            if (!_railGraphClientCache.TryGetNode(toId, out var toNode)) return;
            
            ClientContext.VanillaApi.SendOnly.DisconnectRail(fromNode.NodeId, fromNode.NodeGuid, toNode.NodeId, toNode.NodeGuid);
        }
        
        private DeleteDeniedReason CanDelete()
        {
            var railSegment = RailSegmentCarrier.GetRailSegment();
            var segmentId = railSegment.GetSegmentId();
            var minNodeId = segmentId.GetMinNodeId();
            var maxNodeId = segmentId.GetMaxNodeId();
            ResolveDisconnectDirection(railSegment, minNodeId, maxNodeId, out var fromId, out var toId);
            
            if (!_railGraphClientCache.TryGetNode(fromId, out var fromNode)) return DeleteDeniedReason.UnknownError;
            if (!_railGraphClientCache.TryGetNode(toId, out var toNode)) return DeleteDeniedReason.UnknownError;
            
            if (IsStationInternalEdge(fromNode, toNode)) return DeleteDeniedReason.StationInternalEdge;
            
            return DeleteDeniedReason.None;
        }
        
        private bool IsStationInternalEdge(IRailNode from, IRailNode to)
        {
            if (!from.StationRef.HasStation || !to.StationRef.HasStation)
            {
                return false;
            }
            return from.StationRef.StationBlockInstanceId.Equals(to.StationRef.StationBlockInstanceId);
        }

        // レールセグメントの向きに合わせて切断方向を決める
        // Resolve the disconnect direction based on segment orientation
        private static void ResolveDisconnectDirection(RailSegment railSegment, int minNodeId, int maxNodeId, out int fromId, out int toId)
        {
            if (railSegment.HasMinToMax())
            {
                fromId = minNodeId;
                toId = maxNodeId;
                return;
            }
            if (railSegment.HasMaxToMin())
            {
                fromId = maxNodeId;
                toId = minNodeId;
                return;
            }
            fromId = minNodeId;
            toId = maxNodeId;
        }
        
        public enum DeleteDeniedReason
        {
            None,
            StationInternalEdge,
            NodeInUseByTrain,
            UnknownError,
        }
    }
}
