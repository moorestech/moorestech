using Game.Train.RailCalc;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Server.Util.MessagePack;
using System;
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Train.Diagram;

namespace Client.Game.InGame.Train.Unit
{
    // クライアント上で扱う最小限の列車データ
    // Minimal client-side representation of a train
    public sealed class ClientTrainUnit
    {
        private readonly IRailGraphProvider _railGraphProvider;
        public Guid TrainId { get; }
        public double CurrentSpeed { get; set; }
        public double AccumulatedDistance { get; set; }
        public int MasconLevel { get; set; }
        public bool IsAutoRun { get; set; }
        public bool IsDocked { get; set; }
        IReadOnlyList<TrainCarSnapshot> cars { get; set; }
        // 車両スナップショットを外部に公開する
        // Expose car snapshots for render/update systems
        public IReadOnlyList<TrainCarSnapshot> Cars => cars ?? Array.Empty<TrainCarSnapshot>();

        public ClientTrainDiagram Diagram { get; }
        public RailPosition RailPosition { get; private set; }
        public long LastUpdatedTick { get; private set; }
        public int RemainingDistance { get; private set; } = int.MaxValue;
        public ClientTrainUnit(Guid trainId, IRailGraphProvider railGraphProvider)
        {
            // レールグラフプロバイダを保持する
            // Keep the rail graph provider reference
            _railGraphProvider = railGraphProvider;
            TrainId = trainId;
            IsDocked = false;
            Diagram = new ClientTrainDiagram(new TrainDiagramSnapshot(-1, Array.Empty<TrainDiagramEntrySnapshot>()), _railGraphProvider);
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
            IsDocked = simulation.IsDocked;
            Diagram.UpdateSnapshot(diagram);
            RailPosition = RailPositionFactory.Restore(railPosition, _railGraphProvider);
            cars = simulation.Cars ?? Array.Empty<TrainCarSnapshot>();
            LastUpdatedTick = tick;
            RecalculateRemainingDistance();
        }

        public void ApplyDiagramEvent(TrainDiagramEventMessagePack message)
        {
            if (message == null || message.TrainId != TrainId)
            {
                return;
            }

            var wasDocked = IsDocked;

            // ドッキング/発車イベントをクライアント状態へ反映
            // Apply dock/depart events to client-side state
            if (message.EventType == TrainDiagramEventType.Docked)
            {
                IsDocked = true;
                CurrentSpeed = 0;
                AccumulatedDistance = 0;

                // ドッキング遷移時のみ待機条件を初期値へ戻す
                // Reset departure wait conditions only on dock transition.
                if (!wasDocked)
                {
                    Diagram.ResetCurrentEntryDepartureConditions();
                }
            }
            else if (message.EventType == TrainDiagramEventType.Departed)
            {
                IsDocked = false;
                Diagram.UpdateIndexByEntryId(message.EntryId);
            }
            RecalculateRemainingDistance();
        }

        // 現在の状態からスナップショットバンドルを生成する
        // Build a snapshot bundle from the current client state
        public bool TryCreateSnapshotBundle(out TrainUnitSnapshotBundle bundle)
        {
            if (RailPosition == null)
            {
                bundle = default;
                return false;
            }

            var simulation = CreateSimulationSnapshot();
            var diagram = Diagram.Snapshot;
            var railPosition = RailPosition.CreateSaveSnapshot();
            bundle = new TrainUnitSnapshotBundle(simulation, diagram, railPosition);
            return true;
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

            var path = _railGraphProvider.FindShortestPath(approaching, destinationNode);
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


        private TrainSimulationSnapshot CreateSimulationSnapshot()
        {
            // クライアントの移動状態をスナップショットへ変換する
            // Convert client-side motion state into a simulation snapshot
            var carSnapshots = cars ?? Array.Empty<TrainCarSnapshot>();
            return new TrainSimulationSnapshot(
                TrainId,
                CurrentSpeed,
                AccumulatedDistance,
                MasconLevel,
                IsAutoRun,
                IsDocked,
                carSnapshots);
        }

        private IRailNode ResolveCurrentDestinationNode()
        {
        // 現在の目的地ノードを解決する
            // Resolve the current destination node
            return Diagram.TryResolveCurrentDestinationNode(out var destinationNode) ? destinationNode : null;
        }

        // 1tickごとに呼ばれる。進んだ距離を返す
        // Called every tick and returns moved distance
        public int Update()
        {
            // 自動運転が有効で目的地がある場合のみ進める
            // Only advance when auto-run is active and a destination exists
            if (!IsAutoRun || !Diagram.TryResolveCurrentDestinationNode(out _))
            {
                // TODO 未実装
                return 0;
            }

            // 自動運転中はドッキング中なら進まない、ドッキング中じゃないなら目的地に向かって加速
            // During auto-run, stop while docked and accelerate only when undocked.
            if (IsDocked)
            {
                // ドッキング中は移動計算に入らず停止状態を維持する
                // Keep the train stopped and skip movement simulation while docked.
                CurrentSpeed = 0;
                Diagram.TickDockedDepartureConditions(IsAutoRun);
                return 0;
            }

            // ドッキング中でなければ目的地に向かって進む
            // When not docked, proceed toward the destination.
            UpdateMasconLevel();
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
                // 自動運転で目的地に到着したら次の処理へ進める
                // On auto-run arrival, proceed to the next handling
                if (IsArrivedDestination() && IsAutoRun)
                {
                    CurrentSpeed = 0;
                    AccumulatedDistance = 0;
                    // 駅でなければ次のエントリーへ進める
                    // Advance to the next entry when not at a station
                    var destinationNode = ResolveCurrentDestinationNode();
                    if (destinationNode?.StationRef?.HasStation != true)
                    {
                        Diagram.AdvanceToNextEntry();
                        RecalculateRemainingDistance();
                    }
                    break;
                }
                if (distanceToMove == 0) break;
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
            var localCars = cars ?? Array.Empty<TrainCarSnapshot>();
            if (localCars.Count == 0) return 0;
            int totalWeight = 0;
            int totalTraction = 0;
            foreach (var car in localCars)
            {
                var (weight, traction) = GetWeightAndTraction(car);// car.GetWeightAndTraction();//forceに応じて燃料が消費される:未実装
                totalWeight += weight;
                totalTraction += traction;
            }
            if (totalWeight == 0) return 0;
            return (double)totalTraction / totalWeight * masconLevel / TrainMotionParameters.MasconLevelMaximum;
            #region internal
            (int, int) GetWeightAndTraction(TrainCarSnapshot trainCarSnapshot)
            {
                return (TrainMotionParameters.DEFAULT_WEIGHT + trainCarSnapshot.InventorySlotsCount * TrainMotionParameters.WEIGHT_PER_SLOT, trainCarSnapshot.IsFacingForward ? trainCarSnapshot.TractionForce * TrainMotionParameters.DEFAULT_TRACTION : 0);
            }
            #endregion
        }

        //diagramのindexが見ている目的地にちょうど0距離で到達したか
        private bool IsArrivedDestination()
        {
            var node = RailPosition.GetNodeApproaching();
            if (node == null || !Diagram.TryResolveCurrentDestinationNode(out var destinationNode))
            {
                return false;
            }
            if ((node == destinationNode) && (RailPosition.GetDistanceToNextNode() == 0))
            {
                return true;
            }
            return false;
        }

        // ダイアグラム順に到達可能な経路を探索する
        // Walk diagram entries to find a reachable path
        public (bool, List<IRailNode>) CheckAllDiagramPath(IRailNode approaching)
        {
            if (approaching == null)
            {
                return (false, null);
            }

            var found = Diagram.TryFindPathFrom(approaching, out var newPath);
            return (found, newPath);
        }
    }
}
