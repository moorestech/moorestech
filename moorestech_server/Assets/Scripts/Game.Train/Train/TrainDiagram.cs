using System.Collections.Generic;
using System.Linq;
using Game.Train.RailGraph;

//1つのtrainunitが1つもつダイアグラム
//登録されているnodeに順に向かう、一番下までみたら上にループ
namespace Game.Train.Train
{
    public class TrainDiagram
    {
        private TrainUnit _trainUnit;
        public List<DiagramEntry> _entries;
        //_entriesの何番目を指しているか
        public int currentIndex;

        public class DiagramEntry
        {
            public RailNode Node { get; set; }
            //ここにfactorioの出発条件などを追加してもいいかもしれない
        }

        public TrainDiagram(TrainUnit trainUnit)
        {
            _trainUnit = trainUnit;
            _entries = new List<DiagramEntry>();
        }

        //出発条件チェック
        //true出発可能
        public bool CheckEntries()
        {
            /*
            if (!_trainUnit._isUseDestination) return;

            var currentNode = _trainUnit._railPosition.GetNodeApproaching();
            var distance = _trainUnit._railPosition.GetDistanceToNextNode();

            // ノードに到着した場合  
            if (distance == 0 && currentNode != null)
            {
                var lastEntry = _entries.LastOrDefault();
                if (lastEntry == null || lastEntry.Node != currentNode || !lastEntry.IsArrival)
                {
                    _entries.Add(new DiagramEntry
                    {
                        Node = currentNode,
                        Timestamp = currentTime,
                        IsArrival = true
                    });
                }
            }
            */
            return true;
        
        }

        //RailGraphDatabaseからTrainDiagramManager経由で実行される
        public void HandleNodeRemoval(RailNode removedNode)
        {
            // 削除されたノードを含むエントリにマークを付ける  
            foreach (var entry in _entries.Where(e => e.Node == removedNode))
            {
                entry.Node = null; // または特別な「削除済み」マーカー  
            }

            // 必要に応じて、削除されたノードのエントリを無効化  
            var RemoveIndex = _entries.FindIndex(e => e.Node == removedNode);
            if (RemoveIndex >= 0)
            {
                // 削除されたノードを無効化  
                _entries.RemoveAt(RemoveIndex);
            }
        }

    }

}