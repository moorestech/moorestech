using System;
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

        private readonly Dictionary<TrainUnit, TrainDiagram> _diagrams;

        public TrainDiagramManager()
        {
            _instance = this;
            _diagrams = new Dictionary<TrainUnit, TrainDiagram>();
        }

        public void RegisterDiagram(TrainUnit trainUnit, TrainDiagram diagram)
        {
            _diagrams[trainUnit] = diagram;
        }

        public void UnregisterDiagram(TrainUnit trainUnit)
        {
            _diagrams.Remove(trainUnit);
        }

        // 全てのダイアグラムに対してノード削除を通知  
        public void NotifyNodeRemoval(RailNode removedNode)
        {
            foreach (var diagram in _diagrams.Values)
            {
                diagram.HandleNodeRemoval(removedNode);
            }
        }
    }
}
