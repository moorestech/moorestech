using Game.Train.Common;
using Game.Train.RailGraph;
using Game.Train.Train;
using Game.Train.Utility;
using Server.Util.MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Client.Game.InGame.Train
{
    // クライアント上で扱う最小限の列車データ
    // Minimal client-side representation of a train
    public sealed class ClientTrainUnit
    {
        
        public Guid TrainId { get; }

        public double CurrentSpeed { get; set; }
        public double AccumulatedDistance { get; set; }
        public int MasconLevel { get; set; }
        public bool IsAutoRun { get; set; }
        public bool IsDocked { get; set; }
        IReadOnlyList<TrainCarSnapshot> cars { get; set; }
        // 暫定の現在diagramで進む先のirailnode
        IRailNode diagramApproachingRailNode { get; set; }

        public TrainDiagramSnapshot Diagram { get; private set; }
        public RailPosition RailPosition { get; private set; }
        public long LastUpdatedTick { get; private set; }
        public int RemainingDistance { get; private set; } = int.MaxValue;
        public ClientTrainUnit(Guid trainId)
        {
            TrainId = trainId;
            IsDocked = false;
        }


        // スナップショットの内容で内部状態を更新
        // これは全更新 TODO carの更新はまだ未実装
        // Update internal state by the received snapshot
        public void SnapshotUpdate(TrainSimulationSnapshot simulation, TrainDiagramSnapshot diagram, RailPositionSaveData railPosition, long tick)
        {
            CurrentSpeed = simulation.CurrentSpeed;
            AccumulatedDistance = simulation.AccumulatedDistance;
            MasconLevel = simulation.MasconLevel;
            IsAutoRun = simulation.IsAutoRun;
            Diagram = diagram;
            RailPosition = RailPositionFactory.Restore(railPosition);
            cars = simulation.Cars;
            LastUpdatedTick = tick;
            RecalculateRemainingDistance();
        }

        public void ApplyDiagramEvent(TrainDiagramEventMessagePack message)
        {
            if (message == null || message.TrainId != TrainId)
            {
                return;
            }

            // ドッキング/発車イベントをクライアント状態へ反映
            // Apply dock/depart events to client-side state
            if (message.EventType == TrainDiagramEventType.Docked)
            {
                IsDocked = true;
                CurrentSpeed = 0;
                AccumulatedDistance = 0;
            }
            else if (message.EventType == TrainDiagramEventType.Departed)
            {
                IsDocked = false;
                UpdateDiagramIndexByEntryId(message.EntryId);
            }
            RecalculateRemainingDistance();
        }

        private void UpdateDiagramIndexByEntryId(Guid entryId)
        {
            if (Diagram.Entries == null || Diagram.Entries.Count == 0)
            {
                return;
            }

            var entries = Diagram.Entries;
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].EntryId == entryId)
                {
                    Diagram = new TrainDiagramSnapshot(i, entries);
                    return;
                }
            }
        }

        private void RecalculateRemainingDistance()
        {
            // 目的地までの概算距離を再計算
            // Recalculate the approximate remaining distance toward destination
            var destinationNode = ResolveCurrentDestinationNode();
            var approaching = RailPosition?.GetNodeApproaching();
            if (destinationNode == null || approaching == null)
            {
                RemainingDistance = int.MaxValue;
                return;
            }

            if (ReferenceEquals(destinationNode, approaching))
            {
                RemainingDistance = RailPosition.GetDistanceToNextNode();
                return;
            }

            var path = RailGraphProvider.Current.FindShortestPath(approaching, destinationNode);
            if (path == null || path.Count < 2)
            {
                RemainingDistance = int.MaxValue;
                return;
            }

            var tailDistance = RailNodeCalculate.CalculateTotalDistanceF(path);
            if (tailDistance < 0)
            {
                RemainingDistance = int.MaxValue;
                return;
            }

            RemainingDistance = RailPosition.GetDistanceToNextNode() + tailDistance;
        }


        private IRailNode ResolveCurrentDestinationNode()
        {
            if (Diagram.Entries == null || Diagram.Entries.Count == 0)
            {
                diagramApproachingRailNode = null;
                return null;
            }

            var index = Diagram.CurrentIndex;
            if (index < 0 || index >= Diagram.Entries.Count)
            {
                diagramApproachingRailNode = null;
                return null;
            }

            var destinationSnapshot = Diagram.Entries[index];
            var destinationNode = RailGraphProvider.Current.ResolveRailNode(destinationSnapshot.Node);
            diagramApproachingRailNode = destinationNode;
            return destinationNode;
        }

        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------

        //1tickごとに呼ばれる.進んだ距離を返す?
        public int Update()
        {
            // 手動運転などを想定
            if ((diagramApproachingRailNode == null)||(!IsAutoRun))
            {
                // TODO 未実装
                return 0;
            }

            // 自動運転中はドッキング中なら進まない、ドッキング中じゃないなら目的地に向かって加速
            if (IsDocked)
            {
            }
            else
            {
                // ドッキング中でなければ目的地に向かって進む
                UpdateMasconLevel();
            }
            

            // マスコンレベルから燃料を消費しつつ速度を計算する
            // マスコン確定後に進む距離を算出
            // Calculate distance to travel after mascon decision
            var distanceToMove = SimulateMotionStep();
            return UpdateTrainByDistance(distanceToMove);
        }

        // 自動運転時のマスコン制御を共通ロジックで更新
        // Update mascon level via shared auto-run calculation
        public void UpdateMasconLevel()
        {
            var input = new AutoRunMasconInput(CurrentSpeed, RemainingDistance);
            MasconLevel = TrainAutoRunMasconCalculator.Calculate(input);
        }

        // 速度と距離のステップ計算
        // Simulate velocity and distance per tick
        private int SimulateMotionStep()
        {
            var tractionForce = MasconLevel > 0 ? UpdateTractionForce(MasconLevel) : 0.0;
            var stepInput = new TrainMotionStepInput(CurrentSpeed, AccumulatedDistance, MasconLevel, tractionForce);
            var stepResult = TrainDistanceSimulator.Step(stepInput);
            CurrentSpeed = stepResult.NewSpeed;
            AccumulatedDistance = stepResult.NewAccumulatedDistance;
            return stepResult.DistanceToMove;
        }


        // Updateの距離int版
        // distanceToMoveの距離絶対進む。進んだ距離を返す
        // 目的地は常にtrainDiagram.GetNextDestination()を見るので最新のtrainDiagramが適応される。もし目的地がnullなら_isAutoRun = false;は上記ループで行われる(なぜなら1フレーム対応が遅れるので)
        public int UpdateTrainByDistance(int distanceToMove)
        {
            //進行メインループ
            int totalMoved = 0;
            //何かが原因で無限ループになることがあるので、一定回数で強制終了する
            int loopCount = 0;
            while (true)
            {
                int moveLength = RailPosition.MoveForward(distanceToMove);
                distanceToMove -= moveLength;
                totalMoved += moveLength;
                RemainingDistance = Math.Max(0, RemainingDistance - moveLength);
                //自動運転で目的地に到着してたらドッキング判定を行う必要がある
                if (IsArrivedDestination() && IsAutoRun)
                {
                    CurrentSpeed = 0;
                    AccumulatedDistance = 0;
                    break;
                }
                if (distanceToMove == 0) break;
                //----------------------------------------------------------------------------------------
                //この時点でdistanceToMoveが0以外かつ分岐地点または行き止まりについてる状況
                var approaching = RailPosition.GetNodeApproaching();
                if (approaching == null)
                {
                    CurrentSpeed = 0;
                    break;
                }

                if (IsAutoRun)
                {
                    var (found, newPath) = CheckAllDiagramPath(approaching);
                    if (!found)//全部の経路がなくなった
                    {
                        break;
                    }
                    //見つかったので一番いいルートを自動選択
                    RailPosition.AddNodeToHead(newPath[1]);//newPath[0]はapproachingがはいってる
                                                            //残りの距離を再更新
                    RemainingDistance = RailNodeCalculate.CalculateTotalDistanceF(newPath);//計算量NlogN(logはnodeからintの辞書アクセス)
                }
                else
                {
                    //approachingから次のノードをリストの若い順に取得して_railPosition.AddNodeToHead
                    var nextNodelist = approaching.ConnectedNodes.ToList();
                    if (nextNodelist.Count == 0)
                    {
                        CurrentSpeed = 0;
                        break;//もう進めない
                    }
                    var nextNode = nextNodelist[0];
                    RailPosition.AddNodeToHead(nextNode);
                }

                //----------------------------------------------------------------------------------------

                loopCount++;
                if (loopCount > 1000000)
                {
                    throw new InvalidOperationException("列車速度が無限に近いか、レール経路の無限ループを検知しました。");
                }
            }
            return totalMoved;
        }


        //毎フレーム燃料の在庫を確認しながら加速力を計算する
        public double UpdateTractionForce(int masconLevel)
        {
            int totalWeight = 0;
            int totalTraction = 0;
            foreach (var car in cars)
            {
                var (weight, traction) = GetWeightAndTraction(car);// car.GetWeightAndTraction();//forceに応じて燃料が消費される:未実装
                totalWeight += weight;
                totalTraction += traction;
            }
            return (double)totalTraction / totalWeight * masconLevel / TrainMotionParameters.MasconLevelMaximum;
            #region internal
            (int, int) GetWeightAndTraction(TrainCarSnapshot trainCarSnapshot)
            {
                return (TrainMotionParameters.DEFAULT_WEIGHT + trainCarSnapshot.InventorySlotsCount * TrainMotionParameters.WHEIGHT_PER_SLOT, trainCarSnapshot.IsFacingForward ? trainCarSnapshot.TractionForce * TrainMotionParameters.DEFAULT_TRACTION : 0);
            }
            #endregion
        }

        //diagramのindexが見ている目的地にちょうど0距離で到達したか
        private bool IsArrivedDestination()
        {
            var node = RailPosition.GetNodeApproaching();
            if ((node == diagramApproachingRailNode) & (RailPosition.GetDistanceToNextNode() == 0))
            {
                return true;
            }
            return false;
        }

        //現在のdiagramのcurrentから順にすべてのエントリーを順番にみていって、approachingからエントリーnodeへpathが繋がっていればtrueを返す
        public (bool, List<IRailNode>) CheckAllDiagramPath(IRailNode approaching)
        {
            if (diagramApproachingRailNode == null)
            {
                return (false, null);
            }
            
            IRailNode destinationNode = null;
            List<IRailNode> newPath = null;
            //ダイアグラム上、次に目的地に変更していく。全部の経路がなくなった場合は自動運転を解除する
            bool found = false;
            for (int i = 0; i < Diagram.Entries.Count; i++)
            {
                destinationNode = ResolveCurrentDestinationNode();
                if (destinationNode == null)
                    break;//なにかの例外
                var path = RailGraphProvider.Current.FindShortestPath(approaching, destinationNode);
                newPath = path?.ToList();
                if (newPath == null || newPath.Count < 2)
                {
                    var tempIndex = Diagram.CurrentIndex;
                    Diagram =new TrainDiagramSnapshot((tempIndex + 1) % Diagram.Entries.Count, Diagram.Entries);
                    continue;
                }
                //見つかったのでループを抜ける
                found = true;
                break;
            }
            return (found, newPath);
            
            /*
            var nlist = RailGraphProvider.Current.FindShortestPath(approaching, diagramApproachingRailNode).ToList();
            if (nlist == null || nlist.Count < 2)
            {
                return (false, null);
            }
            else 
            {
                return (true, nlist);
            }
            */
        }

        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------
    }
}
