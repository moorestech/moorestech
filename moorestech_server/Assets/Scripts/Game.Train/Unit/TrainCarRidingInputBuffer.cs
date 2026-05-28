using System;
using System.Collections.Generic;
using Game.PlayerRiding.Interface;
using UniRx;

namespace Game.Train.Unit
{
    public sealed class TrainCarRidingInputBuffer : IDisposable
    {
        private readonly Dictionary<int, TrainCarRidingMoveInputState> _latestMoveInputsByPlayerId = new();
        private readonly List<TrainCarRidingBranchSelectionInput> _branchSelectionInputs = new();
        private readonly IDisposable _ridingStateSubscription;

        public TrainCarRidingInputBuffer()
        {
        }

        public TrainCarRidingInputBuffer(IPlayerRidingDatastore playerRidingDatastore)
        {
            _ridingStateSubscription = playerRidingDatastore.OnRidingStateChanged.Subscribe(OnRidingStateChanged);
        }

        public void Dispose()
        {
            _ridingStateSubscription?.Dispose();
        }

        public void SetLatestInput(TrainCarRidingInputState inputState)
        {
            _latestMoveInputsByPlayerId[inputState.PlayerId] = new TrainCarRidingMoveInputState(inputState.PlayerId, inputState.ReceivedTick, inputState.MoveForward, inputState.MoveBackward);
            EnqueueBranchSelectionInput(inputState);
        }

        public IReadOnlyCollection<TrainCarRidingMoveInputState> GetLatestMoveInputs()
        {
            return _latestMoveInputsByPlayerId.Values;
        }

        public IReadOnlyList<TrainCarRidingBranchSelectionInput> ConsumeBranchSelectionInputs()
        {
            var result = new List<TrainCarRidingBranchSelectionInput>(_branchSelectionInputs);
            _branchSelectionInputs.Clear();
            return result;
        }

        public void ClearPlayerInput(int playerId)
        {
            _latestMoveInputsByPlayerId.Remove(playerId);
            _branchSelectionInputs.RemoveAll(input => input.PlayerId == playerId);
        }

        public void ClearPlayerMoveInput(int playerId)
        {
            _latestMoveInputsByPlayerId.Remove(playerId);
        }

        private void EnqueueBranchSelectionInput(TrainCarRidingInputState inputState)
        {
            if (inputState.SelectPreviousBranch == inputState.SelectNextBranch)
            {
                return;
            }

            var branchSelectionIndexDelta = inputState.SelectPreviousBranch ? 1 : -1;
            _branchSelectionInputs.Add(new TrainCarRidingBranchSelectionInput(inputState.PlayerId, branchSelectionIndexDelta));
        }

        private void OnRidingStateChanged(RidingStateChange change)
        {
            ClearPlayerInput(change.PlayerId);
        }

        public readonly struct TrainCarRidingMoveInputState
        {
            public int PlayerId { get; }
            public uint ReceivedTick { get; }
            public bool MoveForward { get; }
            public bool MoveBackward { get; }

            public TrainCarRidingMoveInputState(int playerId, uint receivedTick, bool moveForward, bool moveBackward)
            {
                PlayerId = playerId;
                ReceivedTick = receivedTick;
                MoveForward = moveForward;
                MoveBackward = moveBackward;
            }
        }

        public readonly struct TrainCarRidingInputState
        {
            public int PlayerId { get; }
            public uint ReceivedTick { get; }
            public bool MoveForward { get; }
            public bool SelectPreviousBranch { get; }
            public bool MoveBackward { get; }
            public bool SelectNextBranch { get; }

            public TrainCarRidingInputState(int playerId, uint receivedTick, bool moveForward, bool selectPreviousBranch, bool moveBackward, bool selectNextBranch)
            {
                PlayerId = playerId;
                ReceivedTick = receivedTick;
                MoveForward = moveForward;
                SelectPreviousBranch = selectPreviousBranch;
                MoveBackward = moveBackward;
                SelectNextBranch = selectNextBranch;
            }
        }

        public readonly struct TrainCarRidingBranchSelectionInput
        {
            public int PlayerId { get; }
            public int BranchSelectionIndexDelta { get; }

            public TrainCarRidingBranchSelectionInput(int playerId, int branchSelectionIndexDelta)
            {
                PlayerId = playerId;
                BranchSelectionIndexDelta = branchSelectionIndexDelta;
            }
        }
    }
}
