using System;
using System.Collections.Generic;
using Client.Game.InGame.Train.Unit;
using UnityEngine;

namespace Client.Game.InGame.Train.Network.Diagnostics
{
    public sealed class TrainSynchronizationDiagnostics
    {
        private const int HistoryCapacity = 256;
        private readonly TrainUnitTickState _tickState;
        private readonly TrainSynchronizationDiagnosticWriter _writer;
        private readonly Queue<TrainSynchronizationReceiveRecord> _history = new();
        private TrainSynchronizationDiagnosticReport _waiting;
        private bool _warned;
        private bool _writeAttempted;
        internal TrainSynchronizationDiagnosticWriteResult LastWriteResult { get; private set; }

        public TrainSynchronizationDiagnostics(TrainUnitTickState tickState, TrainSynchronizationDiagnosticWriter writer)
        {
            _tickState = tickState;
            _writer = writer;
        }

        internal void RecordReceived(string kind, uint tick, uint sequenceId)
        {
            // 古い到着・同一IDの上書きも破棄前に履歴へ残す。
            // Retain stale arrivals and duplicate overwrites before the buffer discards them.
            _history.Enqueue(new TrainSynchronizationReceiveRecord(kind, tick, sequenceId));
            if (HistoryCapacity < _history.Count) _history.Dequeue();
        }

        internal void RecordApplied(ulong appliedId)
        {
            if (_waiting == null || appliedId < _waiting.ExpectedId) return;
            _waiting = null;
            _warned = false;
            _writeAttempted = false;
        }

        internal void RecordMissingOrderedMessage(ulong expectedId, bool confirmedGap)
        {
            BeginWaiting(expectedId, "MissingOrderedMessage", null);
            Capture("MissingOrderedMessage", confirmedGap ? "ConfirmedOrderedGap" : null, confirmedGap);
        }

        internal void RecordHashMismatch(ulong expectedId, uint localTrain, uint serverTrain, uint localRail, uint serverRail, bool permanentWait)
        {
            // 初回の比較値を保ち、再観測で同じ比較値を作り直さない。
            // Preserve the first comparison without allocating another object on each repeated observation.
            var comparison = _waiting?.HashComparison ?? new TrainSynchronizationHashComparison(localTrain, serverTrain, localRail, serverRail);
            BeginWaiting(expectedId, "HashMismatch", comparison);
            if (_waiting != null && _waiting.HashComparison == null) _waiting.HashComparison = comparison;
            Capture("HashMismatch", permanentWait ? "ReceivedTickGap" : null, true);
        }

        private void BeginWaiting(ulong expectedId, string reason, TrainSynchronizationHashComparison comparison)
        {
            if (!_tickState.IsInitialized || _waiting != null) return;
            // 初回の期待位置と直前履歴を以後の到着で上書きしない。
            // Freeze the first expected position and preceding history against later arrivals.
            _waiting = new TrainSynchronizationDiagnosticReport
            {
                WaitingSinceUtc = DateTime.UtcNow,
                ExpectedId = expectedId,
                AppliedIdAtOnset = _tickState.GetAppliedTickUnifiedId(),
                LatestReceivedIdAtOnset = _tickState.LatestReceivedId,
                WaitingReason = reason,
                HashComparison = comparison,
                OnsetHistory = _history.ToArray(),
            };
        }

        private void Capture(string observation, string captureReason, bool shouldWarn)
        {
            if (_waiting == null || _writeAttempted) return;
            if (shouldWarn) WarnOnce();
            if (captureReason == null) return;

            // 停止判定はgateが所有し、確定済みの理由を一度だけ保存する。
            // The gate owns stop decisions; diagnostics save the confirmed reason once.
            _writeAttempted = true;
            _waiting.CapturedAtUtc = DateTime.UtcNow;
            _waiting.AppliedIdAtCapture = _tickState.GetAppliedTickUnifiedId();
            _waiting.LatestReceivedIdAtCapture = _tickState.LatestReceivedId;
            _waiting.WaitingReasonAtCapture = observation;
            _waiting.CaptureReason = captureReason;
            _waiting.RecentHistory = _history.ToArray();
            LastWriteResult = _writer.Write(_waiting);

            #region Internal
            void WarnOnce()
            {
                if (_warned) return;
                _warned = true;
                var hashes = _waiting.HashComparison;
                var comparison = hashes == null ? "" : $", train(client={hashes.LocalTrainHash}, server={hashes.ServerTrainHash}), rail(client={hashes.LocalRailHash}, server={hashes.ServerRailHash})";
                Debug.LogWarning($"[TrainSynchronization] Waiting: {observation}, expected={_waiting.ExpectedId >> 32}_{(uint)_waiting.ExpectedId}, applied={_waiting.AppliedIdAtOnset}, latestReceived={_tickState.LatestReceivedId}{comparison}");
            }
            #endregion
        }
    }
}
