using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState.State;
using Game.Train.RailGraph;
using UnityEngine;

namespace Client.Game.InGame.Train.RailGraph
{
    public class DeleteTargetRail : MonoBehaviour, IDeleteTarget
    {       
        public BezierRailChain RailChain { get; private set; }
        private RailObjectIdCarrier _railObjectIdCarrier;
        private RailGraphClientCache _railGraphClientCache;
        
        public RailObjectIdCarrier RailObjectIdCarrier
        {
            get
            {
                if (_railObjectIdCarrier) return _railObjectIdCarrier;
                return _railObjectIdCarrier = GetComponent<RailObjectIdCarrier>();
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
                DeleteDeniedReason.Removed => null,
                _ => throw new ArgumentOutOfRangeException(),
            };
            return canDelete == DeleteDeniedReason.None;
        }
        
        public void Delete()
        {
            var carrier = RailObjectIdCarrier;
            var railObjectId = carrier.GetRailObjectId();
            var fromId = unchecked((int)(uint)railObjectId);
            var toId = unchecked((int)(uint)(railObjectId >> 32));
            
            if (!_railGraphClientCache.TryGetNode(fromId, out var fromNode)) return;
            if (!_railGraphClientCache.TryGetNode(toId, out var toNode)) return;
            
            ClientContext.VanillaApi.SendOnly.DisconnectRail(fromNode.NodeId, fromNode.NodeGuid, toNode.NodeId, toNode.NodeGuid);
        }
        
        private DeleteDeniedReason CanDelete()
        {
            if (RailChain.IsRemoving) return DeleteDeniedReason.Removed;
            
            var railObjectId = RailObjectIdCarrier.GetRailObjectId();
            var fromId = unchecked((int)(uint)railObjectId);
            var toId = unchecked((int)(uint)(railObjectId >> 32));
            
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
        
        public enum DeleteDeniedReason
        {
            None,
            StationInternalEdge,
            NodeInUseByTrain,
            Removed,
            UnknownError,
        }
    }
}