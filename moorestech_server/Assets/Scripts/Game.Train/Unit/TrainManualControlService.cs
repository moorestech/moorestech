using System;
using System.Collections.Generic;

namespace Game.Train.Unit
{
    [Flags]
    public enum TrainManualInputFlags
    {
        None = 0,
        Forward = 1 << 0,
        Left = 1 << 1,
        Backward = 1 << 2,
        Right = 1 << 3
    }

    public readonly struct TrainManualOperation
    {
        public TrainManualOperation(int masconLevel, int branchPreference)
        {
            MasconLevel = masconLevel;
            BranchPreference = branchPreference;
        }

        public int MasconLevel { get; }
        public int BranchPreference { get; }
    }

    public sealed class TrainManualControlService
    {
        private const uint InputHoldTicks = 3;

        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        private readonly Dictionary<int, PlayerManualControlSession> _sessions = new();

        public TrainManualControlService(ITrainUnitLookupDatastore trainUnitLookupDatastore)
        {
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
        }

        public bool TrySelectCar(int playerId, long rawTrainCarInstanceId)
        {
            var trainCarInstanceId = new TrainCarInstanceId(rawTrainCarInstanceId);
            if (!_trainUnitLookupDatastore.TryGetTrainCar(trainCarInstanceId, out _))
            {
                return false;
            }

            _sessions[playerId] = new PlayerManualControlSession(playerId, trainCarInstanceId);
            return true;
        }

        public void ClearPlayerSelection(int playerId)
        {
            _sessions.Remove(playerId);
        }

        public bool TryUpdateInput(int playerId, long rawTrainCarInstanceId, int rawInputMask, uint currentTick)
        {
            if (!_sessions.TryGetValue(playerId, out var session))
            {
                return false;
            }

            if (!session.IsRiding || session.TrainCarInstanceId.AsPrimitive() != rawTrainCarInstanceId)
            {
                return false;
            }

            if (!_trainUnitLookupDatastore.TryGetTrainUnitByCar(session.TrainCarInstanceId, out _))
            {
                return false;
            }

            session.RawInputMask = (TrainManualInputFlags)rawInputMask;
            session.LastInputTick = currentTick;
            _sessions[playerId] = session;
            return true;
        }

        public void PrepareTrainsForTick(uint currentTick)
        {
            foreach (var trainUnit in _trainUnitLookupDatastore.GetRegisteredTrains())
            {
                trainUnit.ClearManualInput();
            }

            var orderedPlayerIds = new List<int>(_sessions.Keys);
            orderedPlayerIds.Sort();
            var assignedTrainIds = new HashSet<TrainInstanceId>();

            for (var i = 0; i < orderedPlayerIds.Count; i++)
            {
                var playerId = orderedPlayerIds[i];
                var session = _sessions[playerId];
                if (!session.IsRiding)
                {
                    continue;
                }

                if (!_trainUnitLookupDatastore.TryGetTrainUnitByCar(session.TrainCarInstanceId, out var trainUnit))
                {
                    continue;
                }

                if (!assignedTrainIds.Add(trainUnit.TrainInstanceId))
                {
                    continue;
                }

                if (trainUnit.IsAutoRun)
                {
                    trainUnit.TurnOffAutoRun();
                }

                trainUnit.SetManualInput(ToOperation(GetEffectiveInput(session, currentTick)));
            }
        }

        private static TrainManualOperation ToOperation(TrainManualInputFlags inputFlags)
        {
            var maxMasconLevel = Core.Master.MasterHolder.TrainUnitMaster.MasconLevelMaximum;
            var forwardPressed = inputFlags.HasFlag(TrainManualInputFlags.Forward);
            var backwardPressed = inputFlags.HasFlag(TrainManualInputFlags.Backward);
            var leftPressed = inputFlags.HasFlag(TrainManualInputFlags.Left);
            var rightPressed = inputFlags.HasFlag(TrainManualInputFlags.Right);

            var masconLevel = 0;
            if (forwardPressed && !backwardPressed)
            {
                masconLevel = maxMasconLevel;
            }
            else if (backwardPressed && !forwardPressed)
            {
                masconLevel = -maxMasconLevel;
            }

            var branchPreference = 0;
            if (leftPressed && !rightPressed)
            {
                branchPreference = -1;
            }
            else if (rightPressed && !leftPressed)
            {
                branchPreference = 1;
            }

            return new TrainManualOperation(masconLevel, branchPreference);
        }

        private static TrainManualInputFlags GetEffectiveInput(PlayerManualControlSession session, uint currentTick)
        {
            if (currentTick - session.LastInputTick > InputHoldTicks)
            {
                return TrainManualInputFlags.None;
            }

            return session.RawInputMask;
        }

        private struct PlayerManualControlSession
        {
            public PlayerManualControlSession(int playerId, TrainCarInstanceId trainCarInstanceId)
            {
                PlayerId = playerId;
                TrainCarInstanceId = trainCarInstanceId;
                IsRiding = true;
                RawInputMask = TrainManualInputFlags.None;
                LastInputTick = 0;
            }

            public int PlayerId { get; }
            public TrainCarInstanceId TrainCarInstanceId { get; }
            public bool IsRiding { get; }
            public TrainManualInputFlags RawInputMask { get; set; }
            public uint LastInputTick { get; set; }
        }
    }
}
