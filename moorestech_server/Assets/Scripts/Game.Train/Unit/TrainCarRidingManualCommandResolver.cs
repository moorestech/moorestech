using System.Collections.Generic;
using Core.Update;
using Game.PlayerRiding.Interface;

namespace Game.Train.Unit
{
    public sealed class TrainCarRidingManualCommandResolver
    {
        // 入力heartbeatより長い保険timeoutで、途絶したW/Sをニュートラルへ戻す。
        // Use a timeout longer than the input heartbeat to return stale W/S input to neutral.
        private static readonly uint ManualInputTimeToLiveTicks = (uint)(GameUpdater.TicksPerSecond * 4);

        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        private readonly TrainCarRidingInputBuffer _inputBuffer;
        private readonly IPlayerRidingDatastore _playerRidingDatastore;

        public TrainCarRidingManualCommandResolver(ITrainUnitLookupDatastore trainUnitLookupDatastore, TrainCarRidingInputBuffer inputBuffer, IPlayerRidingDatastore playerRidingDatastore)
        {
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
            _inputBuffer = inputBuffer;
            _playerRidingDatastore = playerRidingDatastore;
        }

        public TrainUnitManualCommand Resolve(TrainUnit trainUnit, uint currentTick)
        {
            var commands = ResolveAll(currentTick);
            return commands.TryGetValue(trainUnit, out var command) ? command : TrainUnitManualCommand.Default;
        }

        public IReadOnlyDictionary<TrainUnit, TrainUnitManualCommand> ResolveAll(uint currentTick)
        {
            var expiredPlayerIds = new List<int>();
            var trainDirectionVotes = new Dictionary<TrainUnit, int>();
            var branchSelectionIndexDeltas = new Dictionary<TrainUnit, int>();

            foreach (var inputState in _inputBuffer.GetLatestMoveInputs())
            {
                // 期限切れ入力は多数決から除外する。
                // Exclude expired inputs from the vote.
                if (IsExpired(inputState, currentTick))
                {
                    expiredPlayerIds.Add(inputState.PlayerId);
                    continue;
                }

                if (!TryResolveRidingTrainCar(inputState.PlayerId, out var ridingTrainUnit, out var ridingTrainCar))
                {
                    expiredPlayerIds.Add(inputState.PlayerId);
                    continue;
                }

                AddVote(trainDirectionVotes, ridingTrainUnit, ResolveTrainDirectionVote(ridingTrainCar, inputState));
            }

            foreach (var playerId in expiredPlayerIds)
            {
                _inputBuffer.ClearPlayerMoveInput(playerId);
            }

            foreach (var input in _inputBuffer.ConsumeBranchSelectionInputs())
            {
                if (!TryResolveRidingTrainCar(input.PlayerId, out var ridingTrainUnit, out _))
                {
                    continue;
                }

                AddVote(branchSelectionIndexDeltas, ridingTrainUnit, input.BranchSelectionIndexDelta);
            }

            return BuildCommands(trainDirectionVotes, branchSelectionIndexDeltas);
        }

        private static bool IsExpired(TrainCarRidingInputBuffer.TrainCarRidingMoveInputState inputState, uint currentTick)
        {
            if (currentTick < inputState.ReceivedTick)
            {
                return true;
            }

            return currentTick - inputState.ReceivedTick >= ManualInputTimeToLiveTicks;
        }

        private static int ResolveTrainDirectionVote(TrainCar ridingTrainCar, TrainCarRidingInputBuffer.TrainCarRidingMoveInputState inputState)
        {
            if (inputState.MoveForward == inputState.MoveBackward)
            {
                return 0;
            }

            var wantsTrainForward = inputState.MoveForward
                ? ridingTrainCar.IsFacingForward
                : !ridingTrainCar.IsFacingForward;
            return wantsTrainForward ? 1 : -1;
        }

        private bool TryResolveRidingTrainCar(int playerId, out TrainUnit ridingTrainUnit, out TrainCar ridingTrainCar)
        {
            ridingTrainUnit = null;
            ridingTrainCar = null;
            if (!_playerRidingDatastore.TryGetRidingState(playerId, out var state))
            {
                return false;
            }

            if (state.Identifier is not TrainCarRidableIdentifier trainCarIdentifier)
            {
                return false;
            }

            var ridingTrainCarInstanceId = new TrainCarInstanceId(trainCarIdentifier.TrainCarInstanceId);
            if (!_trainUnitLookupDatastore.TryGetTrainUnitByCar(ridingTrainCarInstanceId, out ridingTrainUnit))
            {
                return false;
            }

            return _trainUnitLookupDatastore.TryGetTrainCar(ridingTrainCarInstanceId, out ridingTrainCar);
        }

        private static IReadOnlyDictionary<TrainUnit, TrainUnitManualCommand> BuildCommands(IReadOnlyDictionary<TrainUnit, int> trainDirectionVotes, IReadOnlyDictionary<TrainUnit, int> branchSelectionIndexDeltas)
        {
            var commands = new Dictionary<TrainUnit, TrainUnitManualCommand>();
            foreach (var pair in trainDirectionVotes)
            {
                branchSelectionIndexDeltas.TryGetValue(pair.Key, out var branchSelectionIndexDelta);
                commands[pair.Key] = ResolveManualCommand(pair.Key, pair.Value, branchSelectionIndexDelta);
            }

            foreach (var pair in branchSelectionIndexDeltas)
            {
                if (commands.ContainsKey(pair.Key))
                {
                    continue;
                }

                commands[pair.Key] = ResolveManualCommand(pair.Key, 0, pair.Value);
            }

            return commands;
        }

        private static void AddVote(Dictionary<TrainUnit, int> votes, TrainUnit trainUnit, int vote)
        {
            if (votes.TryGetValue(trainUnit, out var current))
            {
                votes[trainUnit] = current + vote;
                return;
            }

            votes[trainUnit] = vote;
        }

        private static TrainUnitManualCommand ResolveManualCommand(TrainUnit trainUnit, int trainDirectionVote, int branchSelectionIndexDelta)
        {
            if (trainDirectionVote == 0)
            {
                return new TrainUnitManualCommand(false, TrainUnitMasconCommand.Neutral, branchSelectionIndexDelta);
            }

            if (trainDirectionVote > 0)
            {
                return new TrainUnitManualCommand(false, TrainUnitMasconCommand.Accelerate, branchSelectionIndexDelta);
            }

            if (trainUnit.CurrentSpeed > 0)
            {
                return new TrainUnitManualCommand(false, TrainUnitMasconCommand.Brake, branchSelectionIndexDelta);
            }

            return new TrainUnitManualCommand(true, TrainUnitMasconCommand.Accelerate, branchSelectionIndexDelta);
        }
    }
}
