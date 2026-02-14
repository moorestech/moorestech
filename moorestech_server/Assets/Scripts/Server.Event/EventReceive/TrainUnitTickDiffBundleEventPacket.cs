using System.Collections.Generic;
using Game.Train.RailGraph;
using Game.Train.Unit;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

namespace Server.Event.EventReceive
{
    // hash(n-1)とdiff(n)を1イベントで送る統合パケット
    // Unified packet that sends hash(n-1) and diff(n) in one event.
    public sealed class TrainUnitTickDiffBundleEventPacket
    {
        public const string EventTag = "va:event:trainUnitTickDiffBundle";

        // hash計算を間引くtick間隔
        // Tick interval for hash computation thinning.
        private const uint HashComputeIntervalTicks = 2;

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly TrainUpdateService _trainUpdateService;
        private readonly Dictionary<uint, HashTickState> _hashStatesByTick = new();

        public TrainUnitTickDiffBundleEventPacket(
            EventProtocolProvider eventProtocolProvider,
            IRailGraphDatastore railGraphDatastore,
            TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _railGraphDatastore = railGraphDatastore;
            _trainUpdateService = trainUpdateService;
            _trainUpdateService.OnHashEvent.Subscribe(OnHashTick);
            _trainUpdateService.OnPreSimulationDiffEvent.Subscribe(tuple => OnPreSimulationDiff(tuple.Item1, tuple.Item2));
        }

        #region Internal

        private void OnHashTick(uint hashTick)
        {
            var hashTickSequenceId = _trainUpdateService.NextTickSequenceId();
            var (unitsHash, railGraphHash) = ComputeOrDummyHash(hashTick);
            _hashStatesByTick[hashTick] = new HashTickState(unitsHash, railGraphHash, hashTickSequenceId);
        }

        private void OnPreSimulationDiff(uint diffTick, IReadOnlyList<TrainUpdateService.TrainTickDiffData> diffs)
        {
            var hashTick = diffTick - 1;
            PruneStaleHashes(hashTick);
            if (!TryGetHashState(hashTick, out var hashState))
            {
                Debug.LogWarning($"[TrainUnitTickDiffBundleEventPacket] Missing hash state for diffTick={diffTick}, hashTick={hashTick}.");
                return;
            }

            var diffTickSequenceId = _trainUpdateService.NextTickSequenceId();
            var messagePack = new TrainUnitTickDiffBundleMessagePack(
                diffTick,
                hashState.HashTickSequenceId,
                diffTickSequenceId,
                hashState.UnitsHash,
                hashState.RailGraphHash,
                diffs);
            var payload = MessagePackSerializer.Serialize(messagePack);
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
            _hashStatesByTick.Remove(hashTick);
            return;

            #region Internal

            void PruneStaleHashes(uint targetHashTick)
            {
                var staleTicks = new List<uint>();
                foreach (var kv in _hashStatesByTick)
                {
                    if (kv.Key < targetHashTick)
                    {
                        staleTicks.Add(kv.Key);
                    }
                }
                for (var i = 0; i < staleTicks.Count; i++)
                {
                    _hashStatesByTick.Remove(staleTicks[i]);
                }
            }

            bool TryGetHashState(uint targetHashTick, out HashTickState state)
            {
                return _hashStatesByTick.TryGetValue(targetHashTick, out state);
            }

            #endregion
        }

        private (uint unitsHash, uint railGraphHash) ComputeOrDummyHash(uint hashTick)
        {
            if (hashTick % HashComputeIntervalTicks != 0)
            {
                return (TrainUnitHashStateMessagePack.DummyHash, TrainUnitHashStateMessagePack.DummyHash);
            }

            var bundles = new List<TrainUnitSnapshotBundle>();
            foreach (var train in _trainUpdateService.GetRegisteredTrains())
            {
                bundles.Add(TrainUnitSnapshotFactory.CreateSnapshot(train));
            }
            var unitsHash = TrainUnitSnapshotHashCalculator.Compute(bundles);
            var railGraphHash = _railGraphDatastore.GetConnectNodesHash();
            return (unitsHash, railGraphHash);
        }

        private readonly struct HashTickState
        {
            public uint UnitsHash { get; }
            public uint RailGraphHash { get; }
            public uint HashTickSequenceId { get; }

            public HashTickState(uint unitsHash, uint railGraphHash, uint hashTickSequenceId)
            {
                UnitsHash = unitsHash;
                RailGraphHash = railGraphHash;
                HashTickSequenceId = hashTickSequenceId;
            }
        }

        #endregion
    }
}
