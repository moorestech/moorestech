using System.Collections.Generic;
using Game.Context;
using Game.Train.Unit;
using MessagePack;
using Server.Event.EventReceive.BeltTransportSync;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

namespace Server.Event.EventReceive
{
    // hash(n-1)とdiff(n)を1イベントで送る統合パケット
    // Unified packet that sends hash(n-1) and diff(n) in one event.
    // hash(n-1)には列車・レールに加えてベルト搬送のハッシュも相乗りする。ベルトは搬送tick直後に計算した値を保持しており、ここで読む
    // hash(n-1) also carries the belt transport hash alongside train and rail; the belt holds the value computed right after its transport tick and it is read here
    public sealed class TrainUnitTickDiffBundleEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:trainUnitTickDiffBundle";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly TrainUpdateService _trainUpdateService;
        private readonly BeltTransportTickEventPacket _beltTransportTickEventPacket;
        private readonly Dictionary<uint, HashTickState> _hashStatesByTick = new();

        public TrainUnitTickDiffBundleEventPacket(
            EventProtocolProvider eventProtocolProvider,
            TrainUpdateService trainUpdateService,
            BeltTransportTickEventPacket beltTransportTickEventPacket)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _trainUpdateService = trainUpdateService;
            _beltTransportTickEventPacket = beltTransportTickEventPacket;
        }

        public void Load()
        {
            _trainUpdateService.OnHashEvent.Subscribe(OnHashTick);
            _trainUpdateService.OnPreSimulationDiffEvent.Subscribe(tuple => OnPreSimulationDiff(tuple.Item1, tuple.Item2));
        }

        #region Internal

        private void OnHashTick(TrainUpdateService.HashStateEventData hashStateEventData)
        {
            var hashTickSequenceId = _trainUpdateService.NextTickSequenceId();
            // 間引きtickはベルトもダミー。本物のtickだけ保持値を読む
            // A skipped tick is a dummy for the belt too; only a real tick reads the held value
            var beltTransportHash = TrainUpdateService.IsHashBroadcastTick(hashStateEventData.Tick)
                ? _beltTransportTickEventPacket.StateHashOf(hashStateEventData.Tick)
                : uint.MaxValue;
            _hashStatesByTick[hashStateEventData.Tick] = new HashTickState(
                hashStateEventData.UnitsHash,
                hashStateEventData.RailGraphHash,
                beltTransportHash,
                hashTickSequenceId);
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
                hashState.BeltTransportHash,
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

        private readonly struct HashTickState
        {
            public uint UnitsHash { get; }
            public uint RailGraphHash { get; }
            public uint BeltTransportHash { get; }
            public uint HashTickSequenceId { get; }

            public HashTickState(uint unitsHash, uint railGraphHash, uint beltTransportHash, uint hashTickSequenceId)
            {
                UnitsHash = unitsHash;
                RailGraphHash = railGraphHash;
                BeltTransportHash = beltTransportHash;
                HashTickSequenceId = hashTickSequenceId;
            }
        }

        #endregion
    }
}
