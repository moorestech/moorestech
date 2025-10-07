using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Utility;
using System;
using System.Collections.Generic;
using System.Linq;
using static UnityEditor.PlayerSettings;

namespace Game.Train.Train
{
    public class TrainUnit
    {
        public string SaveKey { get; } = typeof(TrainUnit).FullName;
        
        public RailPosition _railPosition;
        // _destinationNodeまでの距離
        private readonly Guid _trainId;
        public Guid TrainId => _trainId;

        private int _remainingDistance;// 自動減速用
        private bool _isAutoRun;
        public bool IsAutoRun
        {
            get { return _isAutoRun; }
        }


        private double _currentSpeed;   // m/s など適宜
        public double CurrentSpeed => _currentSpeed;
        private double _accumulatedDistance; // 累積距離、距離の小数点以下を保持するために使用
        //摩擦係数、空気抵抗係数などはここに追加する
        const double FRICTION = 0.0002f;
        const double AIR_RESISTANCE = 0.00002f;

        public List<TrainCar> _cars;
        public TrainUnitStationDocking trainUnitStationDocking; // 列車の駅ドッキング用のクラス
        public TrainDiagram trainDiagram; // 列車のダイアグラム
        //キー関連
        //マスコンレベル 0がニュートラル、1が前進1段階、-1が後退1段階.キー入力やテスト、外部から直接制御できる。min maxは±16777216とする(暫定)
        public int masconLevel = 0;

        public TrainUnit(
            RailPosition initialPosition,
            List<TrainCar> cars
        )
        {
            _railPosition = initialPosition;
            _trainId = Guid.NewGuid();
            _cars = cars;  // 追加
            _currentSpeed = 0.0; // 仮の初期速度
            _isAutoRun = false;
            trainUnitStationDocking = new TrainUnitStationDocking(this);
            trainDiagram = new TrainDiagram(this);

            TrainDiagramManager.Instance.RegisterDiagram(this, trainDiagram);
            TrainUpdateService.Instance.RegisterTrain(this);
        }


        //1tickごとに呼ばれる.進んだ距離を返す?
        public int Update() 
        {
            if (IsAutoRun)
            {
                // 自動運転中はドッキング中なら進まない、ドッキング中じゃないなら目的地に向かって加速
                if (trainUnitStationDocking.IsDocked)
                {
                    _currentSpeed = 0;
                    //diagramを手動でいじって、現在ドッキング中の駅をエントリーから削除したとき。その場合は安全にドッキング解除しtrainDiagram.MoveToNextEntry();はしない
                    if ((trainDiagram.GetNextDestination() == null)  || (trainDiagram.GetNextDestination() != trainUnitStationDocking.DockedNode))
                    {
                        trainUnitStationDocking.UndockFromStation();
                        return 0;
                    }
                    // もしtrainDiagramの出発条件を満たしていたら、trainDiagramは次の目的地をセットして、ドッキングを解除する
                    if (trainDiagram.CheckEntries())
                    {
                        // 次の目的地をセット
                        trainDiagram.MoveToNextEntry();
                        // ドッキングを解除
                        trainUnitStationDocking.UndockFromStation();
                        return 0;
                    }
                    trainUnitStationDocking.TickDockedStations();
                    return 0;
                }
                else
                {
                    // 自動運転中に手動でダイアグラムをいじって目的地がnullになった場合は自動運転を解除する
                    if (trainDiagram.GetNextDestination() == null)
                    {
                        UnityEngine.
                        Debug.Log("自動運転中に手動でダイアグラムをいじって目的地がnullになった場合は自動運転を解除");
                        _isAutoRun = false;
                        _currentSpeed = 0;
                        return 0;
                    }
                    // ドッキング中でなければ目的地に向かって進む
                    UpdateMasconLevel();
                }
            }
            else 
            {
                // TODO 手動運転中はFキーとかでドッキングできる(satisfactoryを参考に)
                // 未実装
                // もしドッキング中なら
                if (trainUnitStationDocking.IsDocked)
                {
                    // ドッキング中は進まない
                    _currentSpeed = 0;
                    // 強制ドッキング解除
                    trainUnitStationDocking.UndockFromStation();
                    return 0;
                }
                else
                {
                    // ドッキング中でなければキー操作で目的地 or nullに向かって進む
                    KeyInput();
                    //_destinationNode = trainDiagram.GetNextDestination();
                }
            }

            //マスコンレベルから燃料を消費しつつ速度を計算する
            UpdateTrainSpeed();
            //距離計算 進むor後進する
            double floatDistance = _currentSpeed * 0.008;
            _accumulatedDistance += floatDistance;
            int distance = (int)Math.Truncate(_accumulatedDistance);
            _accumulatedDistance -= distance;
            return UpdateTrainByDistance(distance);
        }

        //キー操作系
        public void KeyInput() 
        {
            masconLevel = 0;
            //wキーでmasconLevel=16777216
            //sキーでmasconLevel=-16777216
        }

        // AutoRun時に目的地に向かうためのマスコンレベル更新
        public void UpdateMasconLevel()
        {
            masconLevel = 0;
            //目的地に近ければ減速したい。自動運行での最大速度を決めておく
            double maxspeed = Math.Sqrt(((double)_remainingDistance) * 10000.0) + 10.0;//10.0は距離が近すぎても進めるよう
            //全力加速する必要がある。マスコンレベルmax
            if (maxspeed > _currentSpeed)
            {
                masconLevel = 16777216;
            }
            //
            if (maxspeed < _currentSpeed * 0.98)//0.02ぶんはバッファ
            {
                var subspeed = maxspeed - _currentSpeed * 0.98;
                masconLevel = Math.Max((int)subspeed, -16777216);
            }
        }

        // 速度更新、自動時、手動時両方
        // 進むべき距離を返す
        public void UpdateTrainSpeed() 
        {
            double force = 0.0;
            int sign, sign2;
            //マスコン操作での加減速
            if (masconLevel > 0)
            {
                force = UpdateTractionForce(masconLevel);
                _currentSpeed += force * 0.008;
            }
            else 
            {
                //currentspeedがマイナスも考慮
                sign = Math.Sign(_currentSpeed);
                _currentSpeed += sign * masconLevel * 1.0; // 0.0005は調整用定数
                sign2 = Math.Sign(_currentSpeed);
                if (sign != sign2) _currentSpeed = 0; // 逆方向に行かないようにする
            }

            //どちらにしても速度は摩擦で減少する。摩擦は速度の1乗、空気抵抗は速度の2乗に比例するとする
            force = Math.Abs(_currentSpeed) * 0.008 * FRICTION + _currentSpeed * _currentSpeed * 0.008 * AIR_RESISTANCE;
            sign = Math.Sign(_currentSpeed);
            _currentSpeed -= sign * force;
            sign2 = Math.Sign(_currentSpeed);
            if (sign != sign2) _currentSpeed = 0; // 逆方向に行かないようにする
            return;
        }


        // Updateの距離int版
        // distanceToMoveの距離絶対進む。進んだ距離を返す
        // 目的地は常にtrainDiagram.GetNextDestination()を見るので最新のtrainDiagramが適応される。もし目的地がnullなら_isAutoRun = false;は上記ループで行われる(なぜなら1フレーム対応が遅れるので)
        public int UpdateTrainByDistance(int distanceToMove) 
        {
            //進行メインループ
            int totalMoved = 0;
            var _destinationNode = trainDiagram.GetNextDestination();
            //何かが原因で無限ループになることがあるので、一定回数で強制終了する
            int loopCount = 0;
            while (true)
            {
                int moveLength = _railPosition.MoveForward(distanceToMove);
                distanceToMove -= moveLength;
                totalMoved += moveLength;
                _remainingDistance -= moveLength;
                //自動運転で目的地に到着してたらドッキング判定を行う必要がある
                if (IsArrivedDestination() && _isAutoRun)
                {
                    _currentSpeed = 0;
                    trainUnitStationDocking.TryDockWhenStopped();
                    break;
                }
                if (distanceToMove == 0) break;
                //----------------------------------------------------------------------------------------
                //この時点でdistanceToMoveが0以外かつ分岐地点または行き止まりについてる状況
                RailNode approaching = _railPosition.GetNodeApproaching();
                if (approaching == null) 
                {
                    _isAutoRun = false;
                    _currentSpeed = 0;
                    throw new InvalidOperationException("列車が進行中に接近しているノードがnullになりました。");
                }


                if (IsAutoRun)
                {
                    //分岐点で必ず最短経路を再度探す。手動でレールが変更されてるかもしれないので
                    //例えば自動運転中に手動でダイアグラムをいじる、または手動で線路接続を変更して目的地までの経路がなくなった場合はダイアグラム上 次の目的地に変更する
                    //最低でも返りlistにはapproaching, _destinationNodeが入っているはず
                    var newPath = RailGraphDatastore.FindShortestPath(approaching, _destinationNode);
                    if (newPath.Count < 2)
                    {
                        //ダイアグラム上、次に目的地に変更していく。全部の経路がなくなった場合は自動運転を解除する
                        bool found = false;
                        while (true) 
                        {
                            trainDiagram.MoveToNextEntry();
                            var nextdestinationNode = trainDiagram.GetNextDestination();
                            if (nextdestinationNode == _destinationNode)
                                break;//全部の経路がなくなった
                            if (nextdestinationNode == null)
                                break;//なにかの例外
                            newPath = RailGraphDatastore.FindShortestPath(approaching, nextdestinationNode);
                            if (newPath.Count >= 2) 
                            {
                                found = true;
                                _destinationNode = nextdestinationNode;
                                break;//見つかったのでループを抜ける
                            }
                        }
                        if (!found)
                        {
                            //全部の経路がなくなった
                            _isAutoRun = false;
                            _currentSpeed = 0;
                            throw new InvalidOperationException("自動運転で目的地までの経路が見つからない");
                            break;
                        }
                        continue;//見つかったので次のループでnewPathを使う
                    }
                    else//見つかったので一番いいルートを自動選択
                    {
                        _railPosition.AddNodeToHead(newPath[1]);//newPath[0]はapproachingがはいってる
                                                                //残りの距離を再更新
                        _remainingDistance = RailNodeCalculate.CalculateTotalDistanceF(newPath);//計算量NlogN(logはnodeからintの辞書アクセス)
                    }
                }
                else
                {
                    //approachingから次のノードをリストの若い順に取得して_railPosition.AddNodeToHead
                    var nextNodelist = approaching.ConnectedNodes.ToList();
                    if (nextNodelist.Count == 0)
                    {
                        _currentSpeed = 0;
                        break;//もう進めない
                    }
                    var nextNode = nextNodelist[0];
                    _railPosition.AddNodeToHead(nextNode);
                }

                //----------------------------------------------------------------------------------------

                loopCount++;
                if (loopCount > 1000000)
                {
                    throw new InvalidOperationException("列車速度が無限に近いか、レール経路の無限ループを検知しました。");
                    break;
                }
            }
            return totalMoved;
        }


        //毎フレーム燃料の在庫を確認しながら加速力を計算する
        public double UpdateTractionForce(int masconLevel)
        {
            int totalWeight = 0;
            int totalTraction = 0;

            foreach (var car in _cars)
            {
                var (weight, traction) = car.GetWeightAndTraction();//forceに応じて燃料が消費される:未実装
                totalWeight += weight;
                totalTraction += traction;
            }
            return (double)totalTraction / totalWeight * masconLevel / 16777216.0;
        }

        //diagramのindexが見ている目的地にちょうど0距離で到達したか
        private bool IsArrivedDestination()
        {
            var node = _railPosition.GetNodeApproaching();
            if ((node == trainDiagram.GetNextDestination()) & (_railPosition.GetDistanceToNextNode() == 0))
            {
                return true;
            }
            return false;
        }

        public void TurnOnAutoRun()
        {
            var destinationNode = trainDiagram.GetNextDestination();
            if (destinationNode == null)
            {
                trainDiagram.MoveToNextEntry();
                destinationNode = trainDiagram.GetNextDestination();
            }

            if (destinationNode == null)
            {
                _isAutoRun = false;
                return;
            }

            var approaching = _railPosition.GetNodeApproaching();
            if (approaching == null)
            {
                _isAutoRun = false;
                return;
            }

            if (approaching == destinationNode)
            {
                _remainingDistance = _railPosition.GetDistanceToNextNode();
                _isAutoRun = true;
                return;
            }

            var newPath = RailGraphDatastore.FindShortestPath(approaching, destinationNode);
            if (newPath == null || newPath.Count < 2)
            {
                _remainingDistance = int.MaxValue;
                _isAutoRun = true;//目的地までの経路が見つからない場合はとりあえず自動化onにしてメインループ内でどうにかする
                return;
            }

            _remainingDistance = RailNodeCalculate.CalculateTotalDistanceF(newPath) - _railPosition.GetDistanceToNextNode();
            _isAutoRun = true;
        }

        public void TurnOffAutoRun()
        {
            _isAutoRun = false;
        }



        //列車編成を保存する。ブロックとは違うことに注意
        public string GetSaveState()
        {
            return "";
        }

        //============================================================
        // ▼ ここからが「編成を分割する」ための処理例
        //============================================================
        /// <summary>
        ///  列車を「後ろから numberOfCars 両」切り離して、後ろの部分を新しいTrainUnitとして返す
        ///  新しいTrainUnitのrailpositionは、切り離した車両の長さに応じて調整される
        ///  新しいTrainUnitのtrainDiagramは空になる
        ///  新しいTrainUnitのドッキング状態はcarに情報があるためそのまま保存される
        /// </summary>
        public TrainUnit SplitTrain(int numberOfCarsToDetach)
        {
            // 例：10両 → 5両 + 5両など
            // 後ろから 5両を抜き取るケースを想定
            if (numberOfCarsToDetach <= 0 || numberOfCarsToDetach >= _cars.Count)
            {
                UnityEngine.Debug.LogError("SplitTrain: 指定両数が不正です。");
                return null;
            }
            // 1) 切り離す車両リストを作成
            //    後ろ側から numberOfCarsToDetach 両を取得
            var detachedCars = _cars
                .Skip(_cars.Count - numberOfCarsToDetach)
                .ToList();
            // 3) 新しく後ろのTrainUnitを作る
            var splittedRailPosition = CreateSplittedRailPosition(detachedCars);
            // 2) 既存のTrainUnitからは そのぶん削除
            _cars.RemoveRange(_cars.Count - numberOfCarsToDetach, numberOfCarsToDetach);
            // _carsの両数に応じて、列車長を算出する
            int newTrainLength = 0;
            foreach (var car in _cars)
                newTrainLength += car.Length;
            _railPosition.SetTrainLength(newTrainLength);
            // 4) 新しいTrainUnitを作成
            var splittedUnit = new TrainUnit(
                splittedRailPosition,
                detachedCars
            );
            // 6) 新しいTrainUnitを返す
            return splittedUnit;
        }

        /// <summary>
        /// 後続列車のために、新しいRailPositionを生成し返す。
        /// ここでは単純に列車の先頭からRailNodeの距離を調整するだけ
        /// </summary>
        private RailPosition CreateSplittedRailPosition(List<TrainCar> splittedCars)
        {
            // _railPositionのdeepコピー
            var newNodes = _railPosition.DeepCopy();
            // splittedCarsの両数に応じて、列車長を算出する
            int splittedTrainLength = 0;
            foreach (var car in splittedCars)
                splittedTrainLength += car.Length;
            //newNodesを反転して、新しい列車長を設定
            newNodes.Reverse();
            newNodes.SetTrainLength(splittedTrainLength);
            //また反転すればちゃんと後ろの列車になる
            newNodes.Reverse();
            return newNodes;
        }

        private void OnDestroy()
        {
            // 列車が破棄されるときに、ダイアグラムを解除
            TrainDiagramManager.Instance.UnregisterDiagram(this);
            TrainUpdateService.Instance.UnregisterTrain(this);
            trainUnitStationDocking.UndockFromStation();
            trainUnitStationDocking = null;
            trainDiagram = null;
        }
    }

}
