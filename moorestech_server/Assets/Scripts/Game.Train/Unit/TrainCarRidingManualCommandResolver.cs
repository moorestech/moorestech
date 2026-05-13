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
            var hasResolvedInput = false;
            var latestReceivedTick = 0u;
            var resolvedCommand = TrainUnitManualCommand.Default;

            foreach (var inputState in _inputBuffer.GetLatestInputs())
            {
                if (IsExpired(inputState, currentTick))
                {
                    continue;
                }

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

                var manualCommand = ResolveManualCommand(trainUnit, ridingTrainCar, inputState);
                if (!hasResolvedInput || latestReceivedTick <= inputState.ReceivedTick)
                {
                    hasResolvedInput = true;
                    latestReceivedTick = inputState.ReceivedTick;
                    resolvedCommand = manualCommand;
                }
            }

            return resolvedCommand;
        }

        private static bool IsExpired(TrainCarRidingInputBuffer.TrainCarRidingInputState inputState, uint currentTick)
        {
            if (currentTick < inputState.ReceivedTick)
            {
                return false;
            }

            return currentTick - inputState.ReceivedTick >= ManualInputTimeToLiveTicks;
        }

        private static TrainUnitManualCommand ResolveManualCommand(TrainUnit trainUnit, TrainCar ridingTrainCar, TrainCarRidingInputBuffer.TrainCarRidingInputState inputState)
        {
            if (inputState.W == inputState.S)
            {
                return TrainUnitManualCommand.Default;
            }

            var wantsTrainForward = inputState.W
                ? ridingTrainCar.IsFacingForward
                : !ridingTrainCar.IsFacingForward;

            if (wantsTrainForward)
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
