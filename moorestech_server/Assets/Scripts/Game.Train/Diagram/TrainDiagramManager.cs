using System.Collections.Generic;
using Game.Train.RailGraph;

namespace Game.Train.Diagram
{
    public class TrainDiagramManager : IRailGraphNodeRemovalListener
    {
        private readonly List<TrainDiagram> _diagrams;

        public TrainDiagramManager()
        {
            _diagrams = new List<TrainDiagram>();
        }

        public void Reset()
        {
            _diagrams.Clear();
        }

        public void RegisterDiagram(TrainDiagram diagram)
        {
             if (!_diagrams.Contains(diagram))
            {
                _diagrams.Add(diagram);
            }
        }

        public void UnregisterDiagram(TrainDiagram diagram)
        {
            if (_diagrams.Contains(diagram))
            {
                _diagrams.Remove(diagram);
            }
        }

        // 全てのダイアグラムに対してノード削除を通知  
        public void NotifyNodeRemoval(IRailNode removedNode)
        {
            foreach (var diagram in _diagrams)
            {
                diagram.HandleNodeRemoval(removedNode);
            }
        }

    }
}
