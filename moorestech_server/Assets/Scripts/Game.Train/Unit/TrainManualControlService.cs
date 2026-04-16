using System.Collections.Generic;
using System.Linq;
using Core.Master;

namespace Game.Train.Unit
{
    public sealed class TrainManualControlService
    {
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        private readonly Dictionary<TrainInstanceId, TrainManualRawInputState> _latestInputsByTrainId = new();
        private readonly Dictionary<int, OperatingTargetState> _operatingTargetByPlayerId = new();

        public TrainManualControlService(TrainUnitDatastore trainUnitDatastore)
        {
            _trainUnitLookupDatastore = trainUnitDatastore;
            trainUnitDatastore.TrainRemoved += Clear;
        }

        public void SetLatestInput(TrainInstanceId trainId, TrainManualRawInputState input)
        {
            // 空の列車IDは保持せず、無効な状態流入を防ぐ
            // Ignore empty train ids to avoid storing invalid state
            if (trainId == TrainInstanceId.Empty)
            {
                return;
            }

            // 同じ列車への入力は常に最新値で上書きする
            // Always overwrite the latest input for the target train
            _latestInputsByTrainId[trainId] = input;
        }

        public bool TryGetLatestInput(TrainInstanceId trainId, out TrainManualRawInputState input)
        {
            // 未登録時も neutral を返して呼び出し側の分岐を単純にする
            // Return neutral on misses so callers can use a simple fallback flow
            if (_latestInputsByTrainId.TryGetValue(trainId, out input))
            {
                return true;
            }

            input = TrainManualRawInputState.Neutral;
            return false;
        }

        public void SetOperatingTarget(int playerId, TrainInstanceId trainId, TrainCarInstanceId trainCarInstanceId)
        {
            // 最小実装では player と train の対応付けを優先して保持する
            // Track player-to-train now and resolve the base car later if needed
            if (playerId <= 0 || trainId == TrainInstanceId.Empty)
            {
                return;
            }

            _operatingTargetByPlayerId[playerId] = new OperatingTargetState(trainId, trainCarInstanceId);
        }

        public bool TryGetOperatingTrain(int playerId, out TrainInstanceId trainId)
        {
            if (_operatingTargetByPlayerId.TryGetValue(playerId, out var operatingTarget))
            {
                trainId = operatingTarget.TrainId;
                return true;
            }

            trainId = default;
            return false;
        }

        public bool TryGetOperatingCar(int playerId, out TrainCarInstanceId trainCarInstanceId)
        {
            if (_operatingTargetByPlayerId.TryGetValue(playerId, out var operatingTarget))
            {
                trainCarInstanceId = operatingTarget.TrainCarInstanceId;
                return true;
            }

            trainCarInstanceId = default;
            return false;
        }

        public void Clear(TrainInstanceId trainId)
        {
            // 列車削除時に raw input と player 対応の両方を掃除する
            // Remove both train input and player mappings tied to the train
            if (trainId == TrainInstanceId.Empty)
            {
                return;
            }

            _latestInputsByTrainId.Remove(trainId);

            var removePlayerIds = new List<int>();
            foreach (var pair in _operatingTargetByPlayerId)
            {
                if (pair.Value.TrainId == trainId)
                {
                    removePlayerIds.Add(pair.Key);
                }
            }

            // 対象列車を見ていた player の関連情報だけを消す
            // Clear only the player mappings that pointed at the removed train
            foreach (var playerId in removePlayerIds)
            {
                _operatingTargetByPlayerId.Remove(playerId);
            }
        }

        public bool TryBuildManualCommand(TrainUnit trainUnit, out TrainManualCommand command)
        {
            // manual command は raw input を直接渡さず列車状態を見て解決する
            // Build a unit-facing command instead of passing raw input through directly
            if (trainUnit == null)
            {
                command = TrainManualCommand.Neutral;
                return false;
            }

            if (!TryGetLatestInput(trainUnit.TrainInstanceId, out var rawInput))
            {
                command = TrainManualCommand.Neutral;
                return false;
            }

            if (!TryResolveOperatingCar(trainUnit, out var operatingCar))
            {
                command = TrainManualCommand.Neutral;
                return false;
            }

            if (!TryResolveDesiredUnitForward(rawInput, operatingCar, out var desiredUnitForward))
            {
                command = TrainManualCommand.Neutral;
                return true;
            }

            var maxMasconLevel = MasterHolder.TrainUnitMaster.MasconLevelMaximum;
            var isStopped = trainUnit.CurrentSpeed <= 0;

            // 停車中の逆方向発進だけ reverse 指示へ変換する
            // Convert only stopped opposite launches into an explicit reverse request
            if (desiredUnitForward)
            {
                command = new TrainManualCommand(maxMasconLevel, false);
                return true;
            }

            command = isStopped
                ? new TrainManualCommand(maxMasconLevel, true)
                : new TrainManualCommand(-maxMasconLevel, false);
            return true;

            #region Internal

            bool TryResolveOperatingCar(TrainUnit targetTrainUnit, out TrainCar trainCar)
            {
                trainCar = null;

                if (!TryGetOperatingPlayerId(targetTrainUnit.TrainInstanceId, out var playerId))
                {
                    return false;
                }

                if (_operatingTargetByPlayerId.TryGetValue(playerId, out var operatingTarget) &&
                    operatingTarget.TrainCarInstanceId != default &&
                    _trainUnitLookupDatastore.TryGetTrainCar(operatingTarget.TrainCarInstanceId, out var lookedUpCar) &&
                    targetTrainUnit.Cars.Contains(lookedUpCar))
                {
                    trainCar = lookedUpCar;
                    return true;
                }

                if (operatingTarget.TrainId != targetTrainUnit.TrainInstanceId)
                {
                    return false;
                }

                if (targetTrainUnit.Cars == null || targetTrainUnit.Cars.Count == 0)
                {
                    return false;
                }

                // car 指定が未整備な段階では先頭車両を基準車両として扱う
                // Use the head car as the temporary operating base until ride flow exists
                trainCar = targetTrainUnit.Cars[0];
                _operatingTargetByPlayerId[playerId] = operatingTarget.WithOperatingCar(trainCar.TrainCarInstanceId);
                return true;
            }

            bool TryGetOperatingPlayerId(TrainInstanceId targetTrainId, out int playerId)
            {
                foreach (var pair in _operatingTargetByPlayerId)
                {
                    if (pair.Value.TrainId == targetTrainId)
                    {
                        playerId = pair.Key;
                        return true;
                    }
                }

                playerId = default;
                return false;
            }

            bool TryResolveDesiredUnitForward(TrainManualRawInputState input, TrainCar trainCar, out bool desiredForward)
            {
                desiredForward = false;

                if (input.Forward == input.Backward)
                {
                    return false;
                }

                // W/S は乗車している車両基準で解釈し、unit 向きへ変換する
                // Interpret W/S from the operating car and then map into unit direction
                desiredForward = trainCar.IsFacingForward
                    ? input.Forward
                    : input.Backward;
                return true;
            }

            #endregion
        }

        private readonly struct OperatingTargetState
        {
            public TrainInstanceId TrainId { get; }
            public TrainCarInstanceId TrainCarInstanceId { get; }

            public OperatingTargetState(TrainInstanceId trainId, TrainCarInstanceId trainCarInstanceId)
            {
                TrainId = trainId;
                TrainCarInstanceId = trainCarInstanceId;
            }

            public OperatingTargetState WithOperatingCar(TrainCarInstanceId trainCarInstanceId)
            {
                return new OperatingTargetState(TrainId, trainCarInstanceId);
            }
        }
    }
}
