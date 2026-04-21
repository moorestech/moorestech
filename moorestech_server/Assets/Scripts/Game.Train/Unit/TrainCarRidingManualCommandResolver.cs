namespace Game.Train.Unit
{
    public sealed class TrainCarRidingManualCommandResolver
    {
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        private readonly TrainCarRidingInputBuffer _inputBuffer;

        public TrainCarRidingManualCommandResolver(ITrainUnitLookupDatastore trainUnitLookupDatastore, TrainCarRidingInputBuffer inputBuffer)
        {
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
            _inputBuffer = inputBuffer;
        }

        public TrainUnitManualCommand Resolve(TrainUnit trainUnit)
        {
            var hasResolvedInput = false;
            var latestReceivedTick = 0u;
            var resolvedCommand = TrainUnitManualCommand.Default;

            foreach (var inputState in _inputBuffer.GetLatestInputs())
            {
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
                return new TrainUnitManualCommand(false, 1);
            }

            if (trainUnit.CurrentSpeed > 0)
            {
                return new TrainUnitManualCommand(false, -1);
            }

            return new TrainUnitManualCommand(true, 1);
        }
    }
}
