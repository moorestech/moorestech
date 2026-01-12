using System.Collections.Generic;
using Game.Train.RailGraph;

namespace Game.Train.Common
{
    public class TrainRailPositionManager
    {
        private List<RailPosition> _list;

        public TrainRailPositionManager()
        {
            InitializeDataStore();
        }

        private void InitializeDataStore() => _list = new List<RailPosition>();

        public void Reset()
        {
            _list.Clear();
            InitializeDataStore();
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
        // 全てのrailpositionに対して削除したい辺が1つもなかったらtrue
        public bool CanRemoveEdge(RailNode from, RailNode to)
        {
            bool found = false;
            foreach (var position in _list)
            {
                if (position.ContainsEdge(from, to))
                {
                    found = true;
                    break;
                }
            }
            return found;
        }
    }
}
