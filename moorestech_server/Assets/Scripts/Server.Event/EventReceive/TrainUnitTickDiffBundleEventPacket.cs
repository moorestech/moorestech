using System.Collections.Generic;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Blocks.Fluid;
using Game.Context;
using Game.Train.Unit;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

namespace Server.Event.EventReceive
{
    // hash(n-1)とdiff(n)を1イベントで送る統合パケット
    // Unified packet that sends hash(n-1) and diff(n) in one event.
    // hash(n)はtick nの境界(操作の適用と網の再構築の後・セーブの前)で列車・レール・ベルト・流体を一度に計算して覚え、tick n+1の先頭でdiff(n+1)ができた瞬間に束にして送る
    // hash(n) is computed for train, rail, belt and fluid together at tick n's boundary (after input is applied and networks are rebuilt, before save) and kept until diff(n+1) exists at the head of tick n+1, when both go out as one bundle
    public sealed class TrainUnitTickDiffBundleEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:trainUnitTickDiffBundle";
        // 最初のtickには対になる前tickのハッシュが無い。最初のtickが終わる前に接続できる者は居ないので送らなくてよい
        // The first tick has no previous-tick hash to pair with; nobody can be connected before the first tick ends, so nothing is sent for it
        private const uint FirstTick = 1;

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly TrainUpdateService _trainUpdateService;
        private readonly IBeltTransportLookup _beltTransportLookup;
        private readonly IFluidNetworkLookup _fluidNetworkLookup;
        private readonly Dictionary<uint, HashTickState> _hashStatesByTick = new();

        public TrainUnitTickDiffBundleEventPacket(
            EventProtocolProvider eventProtocolProvider,
            TrainUpdateService trainUpdateService,
            IBeltTransportLookup beltTransportLookup,
            IFluidNetworkLookup fluidNetworkLookup)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _trainUpdateService = trainUpdateService;
            _beltTransportLookup = beltTransportLookup;
            _fluidNetworkLookup = fluidNetworkLookup;
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
            // 間引きtickはベルト・流体もダミー。本物のtickだけ今の組と網をその場でハッシュする
            // A skipped tick is a dummy for the belt and fluid too; only a real tick hashes the current assembly and network on the spot
            var isRealHashTick = TrainUpdateService.IsHashBroadcastTick(hashStateEventData.Tick);
            var beltTransportHash = isRealHashTick ? _beltTransportLookup.ComputeStateHash() : TrainUnitHashStateMessagePack.DummyHash;
            var fluidNetworkHash = isRealHashTick ? _fluidNetworkLookup.CurrentStateHash : TrainUnitHashStateMessagePack.DummyHash;
            _hashStatesByTick[hashStateEventData.Tick] = new HashTickState(
                hashStateEventData.UnitsHash,
                hashStateEventData.RailGraphHash,
                beltTransportHash,
                fluidNetworkHash,
                hashTickSequenceId);
        }

        private void OnPreSimulationDiff(uint diffTick, IReadOnlyList<TrainUpdateService.TrainTickDiffData> diffs)
        {
            if (diffTick == FirstTick) return;
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
                hashState.FluidNetworkHash,
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
            public uint FluidNetworkHash { get; }
            public uint HashTickSequenceId { get; }

            public HashTickState(uint unitsHash, uint railGraphHash, uint beltTransportHash, uint fluidNetworkHash, uint hashTickSequenceId)
            {
                UnitsHash = unitsHash;
                RailGraphHash = railGraphHash;
                BeltTransportHash = beltTransportHash;
                FluidNetworkHash = fluidNetworkHash;
                HashTickSequenceId = hashTickSequenceId;
            }
        }

        #endregion
    }
}
