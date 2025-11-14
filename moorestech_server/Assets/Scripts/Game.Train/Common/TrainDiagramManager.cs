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

        //仮実装中 TODO 今後、ダイアグラムをクライアント側で手動で設定できるようにした場合はこの実装をけす、呼び出し駅側コードも消すよう
        //駅新規追加時のみ駅のfront exitノードを全ダイアグラムに追加 wait は1200tick
        public void NotifyNodeAddition(RailNode newNode)
        {
            foreach (var diagram in _diagrams)
            {
                diagram.AddEntry(newNode, TrainDiagram.DepartureConditionType.WaitForTicks, 1200);
            }
        }
    }
}
