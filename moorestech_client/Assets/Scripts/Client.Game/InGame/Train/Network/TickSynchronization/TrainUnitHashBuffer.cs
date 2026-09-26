using Client.Game.InGame.Train.Unit;
using Core.Update.TickSynchronization;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Client.Game.InGame.Train.Network.TickSynchronization
{
    // - train/rail hashを保持
    // - Buffer train/rail hashes.
    // - stream状態で既適用IDを除外
    // - Reject already applied IDs using stream state.
    internal sealed class TrainUnitHashBuffer
    {
        public const uint DummyHash = uint.MaxValue;
        private bool isGetFirstHash = false;
        private readonly TrainUnitTickState _tickState;
        private readonly SortedDictionary<ulong, (uint unitsHash, uint railGraphHash, uint serverTick, uint tickSequenceId)> _futureHashStates = new();

        public TrainUnitHashBuffer(TrainUnitTickState tickState)
        {
            _tickState = tickState;
        }

        // ハッシュイベントをtick基準でキューへ積む。
        // Queue hash states by tick for tick-aligned verification.
        public void EnqueueHash(uint unitsHash, uint railGraphHash, uint serverTick, uint tickSequenceId)
        {
            if (isGetFirstHash == false)
            {
                Debug.Log($"1stHash: serverTick={serverTick}, tickSequenceId={tickSequenceId}, ");
                isGetFirstHash = true;
            }

            var messageTickUnifiedId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(serverTick, tickSequenceId);
            if (!_tickState.TryAcceptReceivedTickUnifiedId(messageTickUnifiedId))
            {
                // 適用済みの統合順序以下は捨てる。
                // Drop hash states already covered.
                return;
            }
            _futureHashStates[messageTickUnifiedId] = (unitsHash, railGraphHash, serverTick, tickSequenceId);
        }

        public bool TryDequeueHashAtTickSequenceId(ulong tickUnifiedId, out (uint unitsHash, uint railGraphHash, uint serverTick, uint tickSequenceId) message)
        {
            return _futureHashStates.TryGetValue(tickUnifiedId, out message);
        }

        // 対象tickより古いhashは検証対象外として破棄する。
        // Discard hashes older than the requested tick.
        public void DiscardHashesOlderThan(ulong tickUnifiedId)
        {
            while (true)
            {
                if (TryGetFirstHashTickUnifiedId(out var firstTickUnifiedId))
                {
                    if (firstTickUnifiedId < tickUnifiedId)
                    {
                        _futureHashStates.Remove(firstTickUnifiedId);
                        continue;
                    }
                }
                break;
            }
        }

        public bool TryGetFirstHashTickUnifiedId(out ulong tickUnifiedId)
        {
            tickUnifiedId = UInt64.MaxValue;
            if (0 < _futureHashStates.Count)
            {
                tickUnifiedId = _futureHashStates.First().Key;
                return true;
            }
            return false;
        }

    }
}
