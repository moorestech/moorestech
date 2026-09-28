using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.Unit;
using UnityEngine;

namespace Client.Game.InGame.Train.Network.Diagnostics
{
    public sealed class TrainSynchronizationDiagnostics
    {
        // ADR 0071の診断窓。短い通常待ちは保存せず、受信履歴は固定量に保つ。
        // Diagnostic windows from ADR 0071; avoid saving brief waits and bound receive history.
        private const uint TickGapThreshold = 200;
        private const int HistoryCapacity = 256;
        private readonly TrainUnitTickState _tickState;
        private readonly TrainSynchronizationDiagnosticWriter _writer;
        private readonly Queue<TrainSynchronizationReceiveRecord> _history = new();
        private TrainSynchronizationDiagnosticReport _waiting;
        private ulong _latestReceivedId;
        private bool _initialized;
        private bool _warned;
        private bool _writeAttempted;
        internal bool IsWaiting => _waiting != null;
        internal TrainSynchronizationDiagnosticWriteResult LastWriteResult { get; private set; }

        public TrainSynchronizationDiagnostics(TrainUnitTickState tickState, TrainSynchronizationDiagnosticWriter writer)
        {
            _tickState = tickState;
            _writer = writer;
        }

        internal void Initialize(ulong appliedId)
        {
            // 初回snapshot成功時の受信位置を基準に有効化する。
            // Activate against the received position of the successful initial snapshot.
            _initialized = true;
            _latestReceivedId = Math.Max(_latestReceivedId, appliedId);
            RecordApplied(appliedId);
        }

        internal void RecordReceived(string kind, uint tick, uint sequenceId)
        {
            // 古い到着・同一IDの上書きも、破棄前に到着履歴へ残す。
            // Retain stale arrivals and duplicate overwrites before the buffer discards them.
            _history.Enqueue(new TrainSynchronizationReceiveRecord(kind, tick, sequenceId));
            if (_history.Count > HistoryCapacity) _history.Dequeue();
            _latestReceivedId = Math.Max(_latestReceivedId, TrainTickUnifiedIdUtility.CreateTickUnifiedId(tick, sequenceId));
        }

        internal void RecordApplied(ulong appliedId)
        {
            if (_waiting == null || appliedId < _waiting.ExpectedId) return;
            _waiting = null;
            _warned = false;
            _writeAttempted = false;
        }

        internal void RecordMissingOrderedMessage(ulong expectedId)
        {
            BeginWaiting(expectedId, "MissingOrderedMessage", null);
            Evaluate();
        }

        internal void RecordHashMismatch(ulong expectedId, uint localTrain, uint serverTrain, uint localRail, uint serverRail)
        {
            var comparison = new TrainSynchronizationHashComparison(localTrain, serverTrain, localRail, serverRail);
            BeginWaiting(expectedId, "HashMismatch", comparison);
            // 後着hashが不一致だった場合も、欠落から始まった同一待機に値を残す。
            // Retain a late mismatching hash within the same episode that began as a missing message.
            if (_waiting != null && _waiting.HashComparison == null) _waiting.HashComparison = comparison;
            WarnOnce();
            Evaluate();
        }

        private void BeginWaiting(ulong expectedId, string reason, TrainSynchronizationHashComparison comparison)
        {
            if (!_initialized || _waiting != null) return;
            // 初回の期待位置と直前履歴は、以後の到着で上書きしない。
            // Freeze the first expected position and preceding history against later arrivals.
            _waiting = new TrainSynchronizationDiagnosticReport
            {
                WaitingSinceUtc = DateTime.UtcNow,
                ExpectedId = expectedId,
                AppliedIdAtOnset = _tickState.GetAppliedTickUnifiedId(),
                LatestReceivedIdAtOnset = _latestReceivedId,
                TickGapAtOnset = GetTickGap(),
                WaitingReason = reason,
                HashComparison = comparison,
                OnsetHistory = _history.ToArray(),
            };
        }

        private void Evaluate()
        {
            if (_waiting == null || _writeAttempted) return;
            if (_latestReceivedId > _waiting.ExpectedId) WarnOnce();
            var gap = GetTickGap();
            if (gap < TickGapThreshold) return;

            // 実受信の乖離だけを保存し、失敗しても同一待機の再書込を抑止する。
            // Save only observed receive lag and suppress repeated writes even after a disk failure.
            WarnOnce();
            _writeAttempted = true;
            _waiting.CapturedAtUtc = DateTime.UtcNow;
            _waiting.AppliedIdAtCapture = _tickState.GetAppliedTickUnifiedId();
            _waiting.LatestReceivedIdAtCapture = _latestReceivedId;
            _waiting.TickGapAtCapture = gap;
            _waiting.CaptureReason = "ReceivedTickGap";
            _waiting.RecentHistory = _history.ToArray();
            LastWriteResult = _writer.Write(_waiting);
        }

        private uint GetTickGap()
        {
            var receivedTick = (uint)(_latestReceivedId >> 32);
            var appliedTick = _tickState.GetTick();
            return receivedTick > appliedTick ? receivedTick - appliedTick : 0;
        }

        private void WarnOnce()
        {
            if (_waiting == null || _warned) return;
            _warned = true;
            Debug.LogWarning($"[TrainSynchronization] Waiting: {_waiting.WaitingReason}, expected={_waiting.ExpectedId >> 32}_{(uint)_waiting.ExpectedId}, applied={_waiting.AppliedIdAtOnset}, latestReceived={_latestReceivedId}");
        }
    }
}
