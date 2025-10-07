using System.Collections.Generic;
using Game.Train.RailGraph;
using Game.Train.Train;

namespace Game.Train.Common
{
    public class TrainDiagramManager
    {
        private static TrainDiagramManager _instance;
        public static TrainDiagramManager Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new TrainDiagramManager();
                return _instance;
            }
        }

        private readonly List<TrainDiagram> _diagrams;

        public TrainDiagramManager()
        {
            _instance = this;
            _diagrams = new List<TrainDiagram>();
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
        public void NotifyNodeRemoval(RailNode removedNode)
        {
            foreach (var diagram in _diagrams)
            {
                diagram.HandleNodeRemoval(removedNode);
            }
        }
    }
}
