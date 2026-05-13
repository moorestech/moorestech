namespace Game.Train.Unit
{
    public sealed class TrainCarRidingManualCommandResolver
    {
        // 乗車入力は受信tickから20tick未満だけ有効とし、通信断や降車漏れで入力が残り続けるのを防ぐ。
        // Riding input is valid for fewer than 20 server ticks to prevent stale controls after disconnects or missed dismounts.
        private const uint ManualInputTimeToLiveTicks = 20;

        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        private readonly TrainCarRidingInputBuffer _inputBuffer;

        public TrainCarRidingManualCommandResolver(ITrainUnitLookupDatastore trainUnitLookupDatastore, TrainCarRidingInputBuffer inputBuffer)
        {
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
            _inputBuffer = inputBuffer;
        }

        public TrainUnitManualCommand Resolve(TrainUnit trainUnit, uint currentTick)
        {
            var trainDirectionVote = 0;

            foreach (var inputState in _inputBuffer.GetLatestInputs())
            {
                // 期限切れ入力は多数決から除外する。
                // Exclude expired inputs from the vote.
                if (IsExpired(inputState, currentTick))
                {
                    continue;
                }

                // 対象外の train に乗っている入力は、この train の票にしない。
                // Do not count inputs riding a different train.
                if (!_trainUnitLookupDatastore.TryGetTrainUnitByCar(inputState.RidingTrainCarInstanceId, out var ridingTrainUnit))
                {
                    continue;
                }

                if (!ReferenceEquals(ridingTrainUnit, trainUnit))
                {
                    continue;
                }

                if (!_trainUnitLookupDatastore.TryGetTrainCar(inputState.RidingTrainCarInstanceId, out var ridingTrainCar))
                {
                    continue;
                }

                // 車両向きを考慮して train 前後方向の票へ変換し、全員分を合算する。
                // Convert each input with car facing into a train direction vote and sum all riders.
                trainDirectionVote += ResolveTrainDirectionVote(ridingTrainCar, inputState);
            }

            return ResolveManualCommand(trainUnit, trainDirectionVote);
        }

        private static bool IsExpired(TrainCarRidingInputBuffer.TrainCarRidingInputState inputState, uint currentTick)
        {
            if (currentTick < inputState.ReceivedTick)
            {
                return false;
            }

            return currentTick - inputState.ReceivedTick >= ManualInputTimeToLiveTicks;
        }

        private static int ResolveTrainDirectionVote(TrainCar ridingTrainCar, TrainCarRidingInputBuffer.TrainCarRidingInputState inputState)
        {
            if (inputState.W == inputState.S)
            {
                return 0;
            }

            var wantsTrainForward = inputState.W
                ? ridingTrainCar.IsFacingForward
                : !ridingTrainCar.IsFacingForward;
            return wantsTrainForward ? 1 : -1;
        }

        private static TrainUnitManualCommand ResolveManualCommand(TrainUnit trainUnit, int trainDirectionVote)
        {
            if (trainDirectionVote == 0)
            {
                return TrainUnitManualCommand.Default;
            }

            if (trainDirectionVote > 0)
            {
                return new TrainUnitManualCommand(false, TrainUnitMasconCommand.Accelerate);
            }

            if (trainUnit.CurrentSpeed > 0)
            {
                return new TrainUnitManualCommand(false, TrainUnitMasconCommand.Brake);
            }

            return new TrainUnitManualCommand(true, TrainUnitMasconCommand.Accelerate);
        }
    }
}
