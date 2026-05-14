using System.Collections.Generic;

namespace Game.Train.Unit
{
    public sealed class TrainCarRidingInputBuffer
    {
        private readonly Dictionary<int, TrainCarRidingInputState> _latestInputsByPlayerId = new();

        public void SetLatestInput(TrainCarRidingInputState inputState)
        {
            if (_latestInputsByPlayerId.TryGetValue(inputState.PlayerId, out var current))
            {
                if (current.ReceivedTick > inputState.ReceivedTick)
                {
                    return;
                }
            }

            _latestInputsByPlayerId[inputState.PlayerId] = inputState;
        }

        public IReadOnlyCollection<TrainCarRidingInputState> GetLatestInputs()
        {
            return _latestInputsByPlayerId.Values;
        }

        public readonly struct TrainCarRidingInputState
        {
            public int PlayerId { get; }
            public TrainCarInstanceId RidingTrainCarInstanceId { get; }
            public uint ReceivedTick { get; }
            public bool W { get; }
            public bool A { get; }
            public bool S { get; }
            public bool D { get; }

            public TrainCarRidingInputState(int playerId, TrainCarInstanceId ridingTrainCarInstanceId, uint receivedTick, bool w, bool a, bool s, bool d)
            {
                PlayerId = playerId;
                RidingTrainCarInstanceId = ridingTrainCarInstanceId;
                ReceivedTick = receivedTick;
                W = w;
                A = a;
                S = s;
                D = d;
            }
        }
    }
}
