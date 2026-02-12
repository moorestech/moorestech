using System.Collections.Generic;
using Core.Update;
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

        //仮実装中 TODO 今後、ダイアグラムをクライアント側で手動で設定できるようにした場合はこの実装をけす
        //デバッグトグルon off切替時のみ全駅のfront exitノードを全ダイアグラムに追加 wait は300tick。旧ダイアグラムは全削除
        public void ResetAndNotifyNodeAddition(IReadOnlyList<IRailNode> newNodes)
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
                    diagram.AddEntry(newNode, TrainDiagram.DepartureConditionType.WaitForTicks, GameUpdater.TicksPerSecond);
                }
            }
        }

    }
}
