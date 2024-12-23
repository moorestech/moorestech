using System.Collections.Generic;
using System.Linq;

namespace Game.Train.RailGraph
{
    public class RailPosition
    {
        // ノードリスト: 現在のルートを構成するノードリスト
        private List<RailNode> _nodePath;

        // 先頭ノードの次の点までの距離
        private int _distanceToNextNode;

        // 列車の長さ
        private int _trainLength;

        public RailPosition(List<RailNode> initialPath, int initialDistance, int trainLength)
        {
            _nodePath = initialPath;
            _distanceToNextNode = initialDistance;
            _trainLength = trainLength;
        }

        // 現在進行している先のRailNodeを取得
        public RailNode GetCurrentNode()
        {
            return _nodePath.FirstOrDefault();
        }

        // 現在進行している次のRailNodeを取得
        public RailNode GetNextNode()
        {
            return _nodePath.Skip(1).FirstOrDefault();
        }

        // 移動処理: 指定距離だけ進む
        public int Move(int distance)
        {
            _distanceToNextNode -= distance;

            while (_distanceToNextNode <= 0 && _nodePath.Count > 1)
            {
                // 次のノードに移動
                _nodePath.RemoveAt(0);
                _distanceToNextNode += _nodePath.First().ConnectedNodes.First(x => x.Item1 == GetNextNode()).Item2;
            }

            return _distanceToNextNode;
        }

        /*
        // 列車の方向を反転
        public void Reverse()
        {
            _nodePath.Reverse();
            _distanceToNextNode = _trainLength - _distanceToNextNode;
        }
        */

        // 進行予定のノードリストを更新
        public void UpdatePath(List<RailNode> newPath)
        {
            _nodePath = newPath;
        }

        // 列車の全長を取得
        public int GetTrainLength()
        {
            return _trainLength;
        }
    }
}
