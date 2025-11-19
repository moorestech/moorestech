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

        public void ResetInstance()
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
        public void NotifyNodeRemoval(RailNode removedNode)
        {
            foreach (var diagram in _diagrams)
            {
                diagram.HandleNodeRemoval(removedNode);
            }
        }

        //仮実装中 TODO 今後、ダイアグラムをクライアント側で手動で設定できるようにした場合はこの実装をけす
        //デバッグトグルon off切替時のみ全駅のfront exitノードを全ダイアグラムに追加 wait は300tick。旧ダイアグラムは全削除
        public void ResetAndNotifyNodeAddition(List<RailNode> newNodes)
        {
            foreach (var diagram in _diagrams)
            {
                while (diagram.Entries.Count != 0) 
                {
                    var currentNode = diagram.GetCurrentNode();
                    diagram.HandleNodeRemoval(currentNode);
                }
                foreach (var newNode in newNodes)
                {
                    diagram.AddEntry(newNode, TrainDiagram.DepartureConditionType.WaitForTicks, 300);
                }
            }
        }
    }
}
