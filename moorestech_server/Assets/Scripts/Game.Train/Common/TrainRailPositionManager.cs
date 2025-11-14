using System.Collections.Generic;
using Game.Train.RailGraph;

namespace Game.Train.Common
{
    public class TrainRailPositionManager
    {
        private static TrainRailPositionManager _instance;
        public static TrainRailPositionManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new TrainRailPositionManager();
                return _instance;
            }
        }

        private List<RailPosition> _list;

        public TrainRailPositionManager()
        {
            InitializeDataStore();
            _instance = this;
        }

        private void InitializeDataStore()
        {
            _list = new List<RailPosition>();
        }

        private void ResetInternalState()
        {
            _list.Clear();
            InitializeDataStore();
        }

        public static void ResetInstance()
        {
            if (_instance == null)
            {
                _instance = new TrainRailPositionManager();
                return;
            }
            _instance.ResetInternalState();
        }

        public void RegisterRailPosition(RailPosition position)
        {
            if (!_list.Contains(position))
            {
                _list.Add(position);
            }
        }

        public void UnregisterRailPosition(RailPosition position)
        {
            if (_list.Contains(position))
            {
                _list.Remove(position);
            }
        }

        // 全てのrailpositionに対してノード削除を通知  
        public void NotifyNodeRemoval(RailNode removedNode)
        {
            foreach (var position in _list)
            {
                position.RemoveNode(removedNode);
            }
        }
        // 全てのrailpositionに対して削除したいノードが1つもなかったらtrue
        public bool CanRemoveNode(RailNode nodeToRemove) 
        {
            bool found = false;
            foreach (var position in _list)
            {
                if (position.ContainsNode(nodeToRemove))
                {
                    found = true;
                    break;
                }
            }
            return found;
        }

    }
}
