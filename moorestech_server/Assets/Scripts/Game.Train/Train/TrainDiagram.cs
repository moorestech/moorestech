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
            currentIndex = -1;//-1は未選択,手動運転のようなもの
            _trainUnit = trainUnit;
            _entries = new List<DiagramEntry>();
        }

        //出発条件チェック
        //true出発可能
        public bool CheckEntries()
        {
            bool ret = true;
            //train car全部でIsInventoryFull()をチェックする
            //ドッキングしている車両がすべてアイテム満杯なら出発可能
            foreach (var car in _trainUnit._cars)
            {
                if (car.IsDocked)
                {
                    if (car.IsInventoryFull() == false)
                        ret = false;
                }
            }
            return ret;
        }

        //GetNextDestination
        public RailNode GetNextDestination() 
        {
            if (_entries.Count == 0) return null; // エントリがない場合はnullを返す
            // 現在のインデックスが有効な範囲内であることを確認
            if (currentIndex < 0 || currentIndex >= _entries.Count)
            {
                return null; // インデックスが無効な場合はnullを返す
            }
            return _entries[currentIndex].Node; // 現在のエントリのノードを返す
        }

        //基本ループする
        public void MoveToNextEntry()
        {
            if (_entries.Count == 0) return; // エントリがない場合は何もしない
            currentIndex = (currentIndex + 1) % _entries.Count; // 次のエントリに移動
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

            if (currentIndex >= _entries.Count)
            {
                currentIndex = -1; // インデックスをリセット
            }
        }

    }

}