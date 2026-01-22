using System;
using System.Collections.Generic;
using Game.Train.RailGraph;
using UniRx;

namespace Game.Train.Diagram
{
    public class TrainDiagramManager : IRailGraphNodeRemovalListener
    {
        private readonly List<TrainDiagram> _diagrams;
        private Subject<TrainDiagramEventData> _trainDocked;
        private Subject<TrainDiagramEventData> _trainDeparted;
        public IObservable<TrainDiagramEventData> TrainDocked => _trainDocked;
        public IObservable<TrainDiagramEventData> TrainDeparted => _trainDeparted;

        public TrainDiagramManager()
        {
            _diagrams = new List<TrainDiagram>();
            _trainDocked = new Subject<TrainDiagramEventData>();
            _trainDeparted = new Subject<TrainDiagramEventData>();
        }

        public void Reset()
        {
            _diagrams.Clear();
            _trainDocked.Dispose();
            _trainDeparted.Dispose();
            _trainDocked = new Subject<TrainDiagramEventData>();
            _trainDeparted = new Subject<TrainDiagramEventData>();
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
                    diagram.AddEntry(newNode, TrainDiagram.DepartureConditionType.WaitForTicks, 60);
                }
            }
        }

        internal void NotifyDocked(ITrainDiagramContext context, TrainDiagramEntry entry, long tick, uint diagramHash)
        {
            _trainDocked?.OnNext(new TrainDiagramEventData(context, entry, tick, diagramHash));
        }

        internal void NotifyDeparted(ITrainDiagramContext context, TrainDiagramEntry entry, long tick, uint diagramHash)
        {
            _trainDeparted?.OnNext(new TrainDiagramEventData(context, entry, tick, diagramHash));
        }

        public readonly struct TrainDiagramEventData
        {
            public TrainDiagramEventData(ITrainDiagramContext context, TrainDiagramEntry entry, long tick, uint diagramHash)
            {
                Context = context;
                Entry = entry;
                Tick = tick;
                DiagramHash = diagramHash;
            }

            public ITrainDiagramContext Context { get; }
            public TrainDiagramEntry Entry { get; }
            public long Tick { get; }
            public uint DiagramHash { get; }
        }
    }
}
