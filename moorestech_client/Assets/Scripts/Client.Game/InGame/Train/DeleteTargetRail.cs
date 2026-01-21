using Client.Game.InGame.UI.UIState.State;
using Game.Train.RailGraph;
using UnityEngine;

namespace Client.Game.InGame.Train
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
            if (!CanDelete()) return;
            RailChain.SetRemovePreviewing();
        }
        public void ResetMaterial()
        {
            RailChain.ResetMaterial();
        }
        
        private bool CanDelete()
        {
            var railObjectId = RailObjectIdCarrier.GetRailObjectId();
            var fromId = unchecked((int)(uint)railObjectId);
            var toId = unchecked((int)(uint)(railObjectId >> 32));
            
            if (!_railGraphClientCache.TryGetNode(fromId, out var fromNode)) return false;
            if (!_railGraphClientCache.TryGetNode(toId, out var toNode)) return false;
            
            if (IsStationInternalEdge(fromNode, toNode)) return false;
            
            return true;
        }
        
        private bool IsStationInternalEdge(IRailNode from, IRailNode to)
        {
            if (!from.StationRef.HasStation || !to.StationRef.HasStation)
            {
                return false;
            }
            return from.StationRef.StationBlockInstanceId.Equals(to.StationRef.StationBlockInstanceId);
        }
    }
}