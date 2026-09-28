using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.Unit;
using UnityEngine;

namespace Client.Game.InGame.Train.Network.Diagnostics
{
    public sealed class TrainSynchronizationDiagnostics
    {
        // ADR 0071のhash不一致の診断窓。欠番確定は即保存し、履歴は固定量に保つ。
        // Hash-mismatch diagnostic window from ADR 0071; save confirmed gaps immediately and bound history.
        private const uint HashMismatchTickGapThreshold = 200;
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
            BeginWaiting(expectedId, WaitingObservation.MissingOrderedMessage, null);
            Evaluate(WaitingObservation.MissingOrderedMessage);
        }

        internal void RecordHashMismatch(ulong expectedId, uint localTrain, uint serverTrain, uint localRail, uint serverRail)
        {
            var comparison = new TrainSynchronizationHashComparison(localTrain, serverTrain, localRail, serverRail);
            BeginWaiting(expectedId, WaitingObservation.HashMismatch, comparison);
            // 後着hashが不一致だった場合も、欠落から始まった同一待機に値を残す。
            // Retain a late mismatching hash within the same episode that began as a missing message.
            if (_waiting != null && _waiting.HashComparison == null) _waiting.HashComparison = comparison;
            Evaluate(WaitingObservation.HashMismatch);
        }

        private void BeginWaiting(ulong expectedId, WaitingObservation observation, TrainSynchronizationHashComparison comparison)
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
                WaitingReason = observation.ToString(),
                HashComparison = comparison,
                OnsetHistory = _history.ToArray(),
            };
        }

        private void Evaluate(WaitingObservation observation)
        {
            if (_waiting == null || _writeAttempted) return;
            var hasLaterArrival = _latestReceivedId > _waiting.ExpectedId;
            if (observation == WaitingObservation.HashMismatch || hasLaterArrival) WarnOnce(observation);
            var gap = GetTickGap();
            // 開始理由ではなく現在のgate観測で、欠番とhash不一致の保存条件を選ぶ。
            // Select the persistence policy using the current gate observation instead of the onset reason.
            var captureReason = observation switch
            {
                WaitingObservation.MissingOrderedMessage when hasLaterArrival => "ConfirmedOrderedGap",
                WaitingObservation.HashMismatch when gap >= HashMismatchTickGapThreshold => "ReceivedTickGap",
                _ => null,
            };
            if (captureReason == null) return;

            // 実受信の乖離だけを保存し、失敗しても同一待機の再書込を抑止する。
            // Save only observed receive lag and suppress repeated writes even after a disk failure.
            _writeAttempted = true;
            _waiting.CapturedAtUtc = DateTime.UtcNow;
            _waiting.AppliedIdAtCapture = _tickState.GetAppliedTickUnifiedId();
            _waiting.LatestReceivedIdAtCapture = _latestReceivedId;
            _waiting.TickGapAtCapture = gap;
            _waiting.WaitingReasonAtCapture = observation.ToString();
            _waiting.CaptureReason = captureReason;
            _waiting.RecentHistory = _history.ToArray();
            LastWriteResult = _writer.Write(_waiting);
        }

        private uint GetTickGap()
        {
            var receivedTick = (uint)(_latestReceivedId >> 32);
            var appliedTick = _tickState.GetTick();
            return receivedTick > appliedTick ? receivedTick - appliedTick : 0;
        }

        private void WarnOnce(WaitingObservation observation)
        {
            if (_waiting == null || _warned) return;
            _warned = true;
            Debug.LogWarning($"[TrainSynchronization] Waiting: {observation}, expected={_waiting.ExpectedId >> 32}_{(uint)_waiting.ExpectedId}, applied={_waiting.AppliedIdAtOnset}, latestReceived={_latestReceivedId}");
        }

        private enum WaitingObservation
        {
            MissingOrderedMessage,
            HashMismatch,
        }
    }
}
