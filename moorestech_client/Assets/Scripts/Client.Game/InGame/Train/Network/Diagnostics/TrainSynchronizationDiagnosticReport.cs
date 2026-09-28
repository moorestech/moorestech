using System;

namespace Client.Game.InGame.Train.Network.Diagnostics
{
    // 待機開始と保存時点を分け、実測の受信位置を残す。
    // Separate wait onset from capture and retain observed receive positions.
    internal sealed class TrainSynchronizationDiagnosticReport
    {
        public DateTime WaitingSinceUtc;
        public DateTime CapturedAtUtc;
        public ulong ExpectedId;
        public uint ExpectedTick => (uint)(ExpectedId >> 32);
        public uint ExpectedSequenceId => (uint)ExpectedId;
        public ulong AppliedIdAtOnset;
        public ulong AppliedIdAtCapture;
        public ulong LatestReceivedIdAtOnset;
        public ulong LatestReceivedIdAtCapture;
        public uint TickGapAtOnset;
        public uint TickGapAtCapture;
        public string WaitingReason;
        public string CaptureReason;
        public TrainSynchronizationHashComparison HashComparison;
        public TrainSynchronizationReceiveRecord[] OnsetHistory;
        public TrainSynchronizationReceiveRecord[] RecentHistory;
    }

    internal sealed class TrainSynchronizationHashComparison
    {
        public readonly uint LocalTrainHash;
        public readonly uint ServerTrainHash;
        public readonly uint LocalRailHash;
        public readonly uint ServerRailHash;

        internal TrainSynchronizationHashComparison(uint localTrain, uint serverTrain, uint localRail, uint serverRail)
        {
            LocalTrainHash = localTrain;
            ServerTrainHash = serverTrain;
            LocalRailHash = localRail;
            ServerRailHash = serverRail;
        }
    }

    internal sealed class TrainSynchronizationReceiveRecord
    {
        public readonly DateTime ReceivedAtUtc;
        public readonly string Kind;
        public readonly uint Tick;
        public readonly uint SequenceId;

        internal TrainSynchronizationReceiveRecord(string kind, uint tick, uint sequenceId)
        {
            ReceivedAtUtc = DateTime.UtcNow;
            Kind = kind;
            Tick = tick;
            SequenceId = sequenceId;
        }
    }
}
