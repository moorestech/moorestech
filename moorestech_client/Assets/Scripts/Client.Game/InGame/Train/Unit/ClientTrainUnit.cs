using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using System;
using System.Collections.Generic;

namespace Client.Game.InGame.Train.Unit
{
    // クライアント上で扱う最小限の列車データ
    // Minimal client-side representation of a train
    public sealed class ClientTrainUnit
    {
        private readonly IRailGraphProvider _railGraphProvider;
        private readonly ClientTrainUnitMotion _motion;

        public TrainUnitInstanceId TrainUnitInstanceId { get; }
        public double CurrentSpeed { get; set; }
        public double AccumulatedDistance { get; set; }
        public int MasconLevel { get; set; }
        private int _manualBranchSelectionIndex;

        private IReadOnlyList<TrainCarSnapshot> _cars;
        // 車両スナップショットを外部に公開する
        // Expose car snapshots to consumers
        public IReadOnlyList<TrainCarSnapshot> Cars => _cars ?? Array.Empty<TrainCarSnapshot>();
        public RailPosition RailPosition { get; private set; }

        public ClientTrainUnit(TrainUnitInstanceId trainUnitInstanceId, IRailGraphProvider railGraphProvider)
        {
            // レールグラフプロバイダを保持する
            // Keep the rail graph provider reference
            _railGraphProvider = railGraphProvider;
            _motion = new ClientTrainUnitMotion(railGraphProvider);
            TrainUnitInstanceId = trainUnitInstanceId;
        }

        public int GetManualBranchSelectionIndex()
        {
            return _manualBranchSelectionIndex;
        }

        // スナップショットの内容で内部状態を更新
        // Update internal state by the received snapshot
        public void SnapshotUpdate(TrainSimulationSnapshot simulation, RailPositionSaveData railPosition)
        {
            CurrentSpeed = simulation.CurrentSpeed;
            AccumulatedDistance = simulation.AccumulatedDistance;
            MasconLevel = simulation.MasconLevel;
            _manualBranchSelectionIndex = simulation.ManualBranchSelectionIndex;
            RailPosition = RailPositionFactory.Restore(railPosition, _railGraphProvider);
            _cars = simulation.Cars ?? Array.Empty<TrainCarSnapshot>();
            _motion.ResetTarget(RailPosition);
        }

        // pre sim差分イベントを反映する
        // Apply pre-simulation diff values from the server.
        public bool ApplyPreSimulationDiff(int masconLevelDiff, bool isNowDockingSpeedZero, int approachingNodeId, bool isReversedThisTick, int manualBranchSelectionIndexDiff)
        {
            // reverse は同 tick の速度・距離シミュレーション前に反映する
            // Apply reverse before the same-tick velocity and distance simulation
            if (isReversedThisTick)
            {
                ApplyReverseDiff();
            }

            // マスコンと分岐目標をサーバー通知値に合わせる
            // Align mascon and branch target with server-notified values
            MasconLevel += masconLevelDiff;
            _manualBranchSelectionIndex += manualBranchSelectionIndexDiff;
            if (approachingNodeId != -1)
            {
                _motion.SetApproachingNode(approachingNodeId);
            }

            // ドッキング停止はこの tick の移動処理内で消化する
            // Consume docking stop inside this tick's movement step
            if (isNowDockingSpeedZero)
            {
                _motion.QueueDockingStop();
            }
            return isReversedThisTick;
        }

        // 指定したTrainCarを現在の列車スナップショットから削除する
        // Remove the specified train car from the current snapshot state.
        public bool RemoveCar(TrainCarInstanceId trainCarInstanceId)
        {
            if (!ClientTrainCarSnapshots.TryRemove(_cars, trainCarInstanceId, out var remaining))
            {
                return false;
            }
            _cars = remaining;
            return true;
        }

        // 現在の状態からスナップショットバンドルを生成する
        public bool TryCreateSnapshotBundle(out TrainUnitSnapshotBundle bundle)
        {
            if (RailPosition == null)
            {
                bundle = default;
                return false;
            }

            var simulation = CreateSimulationSnapshot();
            var railPosition = RailPosition.CreateSaveSnapshot();
            bundle = new TrainUnitSnapshotBundle(simulation, railPosition);
            return true;

            #region Internal

            TrainSimulationSnapshot CreateSimulationSnapshot()
            {
                // クライアントの移動状態をスナップショットへ変換する
                // Convert client-side motion state into a simulation snapshot
                var carSnapshots = _cars ?? Array.Empty<TrainCarSnapshot>();
                return new TrainSimulationSnapshot(
                    TrainUnitInstanceId,
                    CurrentSpeed,
                    AccumulatedDistance,
                    MasconLevel,
                    _manualBranchSelectionIndex,
                    carSnapshots);
            }

            #endregion
        }

        // 1tickごとに呼ばれる。進んだ距離を返す
        // Called every tick and returns moved distance
        public int Update()
        {
            // サーバー通知済みMasconLevelで速度シミュレーションを進める
            // Simulate movement using the server-synchronized mascon level.
            var step = _motion.SimulateStep(CurrentSpeed, AccumulatedDistance, MasconLevel, Cars);
            CurrentSpeed = step.NewSpeed;
            AccumulatedDistance = step.NewAccumulatedDistance;
            return UpdateTrainByDistance(step.DistanceToMove);
        }

        // Updateの距離int版
        // Integer-distance variant of Update
        public int UpdateTrainByDistance(int distanceToMove)
        {
            var speed = CurrentSpeed;
            var accumulated = AccumulatedDistance;
            var moved = _motion.UpdateTrainByDistance(RailPosition, distanceToMove, ref speed, ref accumulated);
            CurrentSpeed = speed;
            AccumulatedDistance = accumulated;
            return moved;
        }
        
        // 現在の目標ノードに到達する経路を探索する
        // Find path toward the current target node
        public (bool, List<IRailNode>) TryFindPathToSimulationTarget(IRailNode approaching)
        {
            return _motion.TryFindPathToSimulationTarget(approaching);
        }

        private void ApplyReverseDiff()
        {
            // RailPosition の向きをサーバーの TrainUnit.Reverse と同じように反転する
            // Reverse RailPosition the same way as server-side TrainUnit.Reverse
            RailPosition?.Reverse();
            _motion.ResetTarget(RailPosition);

            // 車両順と各車両の向きを同時に反転し、見た目の向きが打ち消される状態を再現する
            // Reverse car order and per-car facing together to reproduce the visual-canceling state
            _cars = ClientTrainCarSnapshots.Reverse(_cars);
        }
    }
}
