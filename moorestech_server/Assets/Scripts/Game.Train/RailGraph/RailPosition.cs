using Game.Train.Common;
using Game.Train.Utility;
using MessagePack;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Game.Train.RailGraph
{
    public class RailPosition
    {
        // RailNodeのリスト。インデックスが小さいほうに向かって進む。
        private List<RailNode> _railNodes;

        // 先頭の前輪が次のノードまでどれだけ離れているか
        private int _distanceToNextNode;

        // 列車の長さ
        private int _trainLength;

        public int TrainLength => _trainLength;
        public int DistanceToNextNode => _distanceToNextNode;

        public RailPosition(List<RailNode> railNodes, int trainLength, int initialDistanceToNextNode)
        {
            if (railNodes == null || railNodes.Count < 1)
            {
                throw new ArgumentException("RailNodeリストには1つ以上の要素が必要です。");
            }

            if (trainLength < 0)
            {
                throw new ArgumentException("列車の長さは0以上である必要があります。");
            }

            _railNodes = railNodes;
            _trainLength = trainLength;
            _distanceToNextNode = initialDistanceToNextNode;
            ValidatePosition();
        }

        public void OnDestroy()
        {
            TrainRailPositionManager.Instance.UnregisterRailPosition(this);
            _railNodes.Clear();
            _railNodes = null;
        }



        private void ValidatePosition()
        {
            // 現在のRailNodeリストと距離が列車の長さに収まっているかを確認
            int totalDistance = RailNodeCalculate.CalculateTotalDistance(_railNodes);
            if (totalDistance + _distanceToNextNode < _trainLength)
            {
                throw new InvalidOperationException("RailNodeリストと距離が列車の長さに収まっていない");
            }
        }


        // 距離int進むメソッド。ただし分岐点でとまる
        // マイナスの距離がはいることも考慮する
        // 進んだ距離を整数で返す
        // 進んだときにリストの経路の中でいらない情報を削除する
        public int MoveForward(int distance)
        {
            // 進む距離が負なら反転してfowardで計算しまた反転する
            if (distance < 0) 
            {
                Reverse();
                var result = -MoveForward(-distance);
                Reverse();
                return result;
            }

            // あとは進む距離が正のみを考える
            if (distance <= _distanceToNextNode)
            {
                _distanceToNextNode -= distance;
                RemoveUnnecessaryNodes();
                return distance;
            }
            else 
            {
                var ret = _distanceToNextNode;
                _distanceToNextNode = 0;
                RemoveUnnecessaryNodes();
                return ret;
            }
        }

        // 列車を反転させる
        public void Reverse()
        {
            _railNodes.Reverse();
            for (int i = 0; i < _railNodes.Count; i++)
            {
                _railNodes[i] = _railNodes[i].OppositeNode; // RailNode自体の反転
            }
            //_distanceToNextNode再計算
            _distanceToNextNode = RailNodeCalculate.CalculateTotalDistance(_railNodes) - _distanceToNextNode - _trainLength;
        }

        // 今持っているリストの中でいらない情報を削除する
        // 具体的には列車が含まれる経路の全部のNodeを残したい。また前輪後輪がぴったりのっているNodeは残す
        private void RemoveUnnecessaryNodes()
        {
            if (_railNodes.Count < 2)
            {
                if (_distanceToNextNode != 0)
                {
                    throw new InvalidOperationException("RailPositionの内部状態が不正です。");
                }
                else 
                {
                    return;
                }
            }
            //railposition先頭側のバリデーション
            if (_distanceToNextNode != 0)
            {
                var length01 = _railNodes[1].GetDistanceToNode(_railNodes[0]);
                if (length01 <= _distanceToNextNode)
                {
                    //_railNodes[0]をけす
                    _railNodes.RemoveAt(0);
                    _distanceToNextNode -= length01;
                    RemoveUnnecessaryNodes();
                }
            }
            //railposition最後尾側のバリデーション
            //list[0]から最後尾の距離
            int distanceFromFront = _trainLength + _distanceToNextNode;
            int totalListDistance = 0;
            bool isCoverLength = false;
            for (int i = 0; i < _railNodes.Count - 1; i++)
            {
                isCoverLength = totalListDistance >= distanceFromFront;//長さがたりてるか
                var nodeleng = _railNodes[i + 1].GetDistanceToNode(_railNodes[i]);
                if (nodeleng == 0) continue;
                totalListDistance += nodeleng;
                
                //はじめてtotalListDistanceがdistanceFromFrontを超えたなら
                if (totalListDistance > distanceFromFront) 
                {
                    var overlength = totalListDistance - distanceFromFront;
                    if (isCoverLength) // 今回を含めなくても長さが足りているなら
                    {//それ以降の情報はいらない
                        _railNodes.RemoveRange(i + 1, _railNodes.Count - i - 2);
                        break;
                    }
                    else // 今回を含めないと長さが足りないなら
                    {
                        if (i + 2 == _railNodes.Count) break;
                        //それ以降の情報はいらない
                        _railNodes.RemoveRange(i + 2, _railNodes.Count - i - 2);
                        break;
                    }
                }
            }
            isCoverLength = totalListDistance >= distanceFromFront;//長さがたりてるか
            if (!isCoverLength) 
            {
                throw new InvalidOperationException("RailPositionのrailnode全体長がlengthに足りてません");
            }
            return;
        }

        // railpositionの先頭の位置のrailpositionを返す
        public RailPosition GetHeadRailPosition()
        {
            var copy = DeepCopy();
            copy._trainLength = 0;
            copy.RemoveUnnecessaryNodes();
            return copy;
        }
        // railpositionの最後尾の位置のrailpositionを返す
        public RailPosition GetRearRailPosition()
        {
            var copy = DeepCopy();
            copy._distanceToNextNode += copy._trainLength;
            copy._trainLength = 0;
            copy.RemoveUnnecessaryNodes();
            return copy;
        }
        // 0距離で同じ位置にあるか(node重なりは同じ位置とみなす)。railpositionの先頭位置同士を比較
        public bool IsSamePositionAllowNodeOverlap(RailPosition other)
        {
            RemoveUnnecessaryNodes();
            if (other == null) return false;
            other.RemoveUnnecessaryNodes();
            if (other.GetDistanceToNextNode() != _distanceToNextNode) return false;
            var node1 = other.GetNodeApproaching();
            var node2 = GetNodeApproaching();
            if (node1 == node2)
            {
                if (GetNodeJustPassed() == null) return false;
                return GetNodeJustPassed() == other.GetNodeJustPassed();//nodeと距離が一致 or nodeが異なっている
            }
            // 距離は一致している。nodeが異なっているが0距離で重なっているだけなら一致とみなす
            var length1 = node1.GetDistanceToNode(node2, true);//FindPathを使って距離を調べる
            var length2 = node2.GetDistanceToNode(node1, true);//FindPathを使って距離を調べる
            return length1 == 0 || length2 == 0;
        }
        
        // railpositionの最後尾にrailpositionを追加する。内部距離も追加する
        public void AppendRailPositionAtRear(RailPosition other)
        {
            RemoveUnnecessaryNodes();
            other.RemoveUnnecessaryNodes();
            var othersRailNodes = (List<RailNode>)other.EnumerateRailNodes();
            var assetcount = (_distanceToNextNode != 0) ? 1 : 0;
            Assert.Less(assetcount, _railNodes.Count, "railpositionの内容が不正です");
            Assert.Less(assetcount, othersRailNodes.Count, "railpositionの内容が不正です");
            // railpositionの最後尾とotherの先頭が同じ位置か確認
            UnityEngine.Debug.Assert(
                GetRearRailPosition().IsSamePositionAllowNodeOverlap(other.GetHeadRailPosition())
                , "railpositionの最後尾と追加するrailpositionの先頭があいません"
                );

            if (_distanceToNextNode != 0)
            {
                // railnodeの片方 上記assetで十分
                // var node_0a = _railNodes[_railNodes.Count - 2];
                // var node_0b = othersRailNodes[0];
                // if (node_0a != node_0b) Assert.Fail("railpositionの最後尾と追加するrailpositionの先頭があいません");
                // railnodeの片方 上記assetで十分
                // var node_1a = _railNodes[_railNodes.Count - 1];
                // var node_1b = othersRailNodes[1];
                // if (node_1a != node_1b) Assert.Fail("railpositionの最後尾と追加するrailpositionの先頭があいません");
                for (int i = 2; i < othersRailNodes.Count; i++)
                {
                    _railNodes.Add(othersRailNodes[i]);
                }
                //距離更新
                _trainLength += other.TrainLength;
                return;
            }
            else
            {
                //重なっている状態
                //_railNodesにother._railNodes.First()がふくまれていたら、それが目印
                var otherFirstNode = othersRailNodes.First();
                for (int i = 0; i < _railNodes.Count; i++)
                {
                    if (_railNodes[i] == otherFirstNode) 
                    {
                        _railNodes.RemoveRange(i, _railNodes.Count - i);
                        _railNodes.AddRange(othersRailNodes);//_railNodesにotherを結合ですむ
                        _trainLength += other.TrainLength;
                        return;//breakいらない
                    }
                }
                //重なっている状態でかつ_railNodesにother._railNodes.First()がふくまれていないので経路を探索する必要がある
                var currentLastNode = _railNodes.Last();
                var nodelist = RailGraphDatastore.FindShortestPath(otherFirstNode, currentLastNode);//先頭がotherFirstNode

                if (nodelist == null || nodelist.Count < 2)
                {
                    throw new InvalidOperationException("結合失敗。RailPositionのAppendRailPositionAtRearのアサート漏れの可能性があります。要確認");
                }

                nodelist.Reverse();//先頭がcurrentLastNode
                nodelist.RemoveAt(0);//currentLastNodeを削除
                nodelist.RemoveAt(_railNodes.Count - 1);//otherFirstNode.First()を削除
                //nodelistを追加
                _railNodes.AddRange(nodelist);
                _railNodes.AddRange(othersRailNodes);
                //距離更新
                _trainLength += other.TrainLength;
                return;
            }
        }


        // 現在の先頭のRailNodeを取得
        // これは現在向かっているまたは前輪がちょうど乗っているRailNode
        public RailNode GetNodeApproaching()
        {
            return _railNodes.FirstOrDefault();
        }

        // 次のRailNodeを取得
        public RailNode GetNodeJustPassed()
        {
            return _railNodes.Count > 1 ? _railNodes[1] : null;
        }

        // 現在の距離情報を取得
        public int GetDistanceToNextNode()
        {
            return _distanceToNextNode;
        }

        public IReadOnlyList<ConnectionDestination> CreateSaveSnapshot()
        {
            var snapshot = new List<ConnectionDestination>(_railNodes.Count);
            foreach (var node in _railNodes)
            {
                if (!RailGraphDatastore.TryGetConnectionDestination(node, out var connection))
                {
                    continue;
                }
                var destinationId = new RailComponentID(connection.railComponentID.Position, connection.railComponentID.ID);
                snapshot.Add(new ConnectionDestination(destinationId, connection.IsFront));
            }

            return snapshot;
        }

        public IEnumerable<RailNode> EnumerateRailNodes()
        {
            if (_railNodes == null)
            {
                yield break;
            }

            foreach (var node in _railNodes)
            {
                yield return node;
            }
        }

        //_railNodesのindex 0にrailnodeを追加する
        //それに合わせて_distanceToNextNodeを更新する
        public void AddNodeToHead(RailNode node)
        {
            if (node == null)
            {
                throw new ArgumentNullException("node");
            }
            //_railNodes.countが0の場合は_distanceToNextNodeは0
            if (_railNodes.Count == 0)
            {
                _railNodes.Add(node);
                _distanceToNextNode = 0;
                return;
            }
            int distance = _railNodes[0].GetDistanceToNode(node);
            if (distance != -1)//繋がっていれば
            {
                _railNodes.Insert(0, node);
                _distanceToNextNode += distance;
            }
        }
        public RailPosition DeepCopy()
        {
            var newrailNodes = new List<RailNode>();
            foreach (var node in _railNodes)
            {
                newrailNodes.Add(node);
            }
            return new RailPosition(newrailNodes, _trainLength, _distanceToNextNode);
        }

        //先頭の位置は変えずに長さだけかえる
        //基本的に長さが短くなるときだけ使う。長くなるときはNodeを超える可能性があるので
        public void SetTrainLength(int newLength)
        {
            if (newLength >= _trainLength)
            {
                throw new ArgumentException("列車の長さは短くなる必要があります。");
            }
            if (newLength < 0) 
            {
                throw new ArgumentException("列車の長さは0または正の値である必要があります。");
            }
            _trainLength = newLength;
            RemoveUnnecessaryNodes();
        }

        // intの距離を入力として、railpositionの先頭からその距離さかのぼったところにちょうどあるRailNodeをlistですべて取得する
        // 事実上ドッキング判定のみに使う
        public List<RailNode> GetNodesAtDistance(int distance)
        {
            List<RailNode> nodesAtDistance = new List<RailNode>();
            int totalDistance = _distanceToNextNode + distance;//この地点をみたい
            for (int i = 0; i < _railNodes.Count; i++)
            {
                if (totalDistance == 0) 
                {
                    nodesAtDistance.Add(_railNodes[i]);
                }
                if (i == _railNodes.Count - 1) break; // 最後のノードまで到達したら終了
                int segmentDistance = _railNodes[i + 1].GetDistanceToNode(_railNodes[i]);
                totalDistance -= segmentDistance;
                if (totalDistance < 0) break;
            }
            return nodesAtDistance;
        }

        //nodeがあったら対象を削除
        //基本的にレールの撤去時は全てのrailpositionをみて1つでもあったら、削除はできないのが仕様
        //この関数は強制削除用
        //railpositionの中に同じnodeが複数ある場合も考慮(該当全削除)
        public void RemoveNode(RailNode node)
        {
            if (node == null) return;
            bool removed = false;
            while (_railNodes.Remove(node))
            {
                removed = true;
            }
            if (removed)
            {
                //距離再計算
                _distanceToNextNode = RailNodeCalculate.CalculateTotalDistance(_railNodes) - _trainLength;
                if (_distanceToNextNode < 0) _distanceToNextNode = 0;
            }
        }

        //nodeがあったらtrue
        public bool ContainsNode(RailNode node)
        {
            return _railNodes.Contains(node);
        }

        //テスト用
        //TestGet_railNodes()
        public List<RailNode> TestGet_railNodes()
        {
            return _railNodes;
        }
    }
}
