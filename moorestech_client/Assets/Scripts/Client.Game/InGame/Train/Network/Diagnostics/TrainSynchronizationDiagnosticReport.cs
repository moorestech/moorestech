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
        public uint TickGapAtOnset => (uint)Math.Max(0L, (long)(LatestReceivedIdAtOnset >> 32) - (long)(AppliedIdAtOnset >> 32));
        public uint TickGapAtCapture => (uint)Math.Max(0L, (long)(LatestReceivedIdAtCapture >> 32) - (long)(AppliedIdAtCapture >> 32));
        public string WaitingReason;
        public string WaitingReasonAtCapture;
        public string CaptureReason;
        public TrainSynchronizationHashComparison HashComparison;
        public TrainSynchronizationReceiveRecord[] OnsetHistory;
        public TrainSynchronizationReceiveRecord[] RecentHistory;
    }

    // 同じtickのローカルとサーバーのハッシュの対。検証器が作り、不一致の診断にそのまま残す
    // The local and server hash pairs of one tick; built by the verifier and kept as is in the mismatch diagnosis
    public sealed class TrainSynchronizationHashComparison
    {
        public readonly uint LocalTrainHash;
        public readonly uint ServerTrainHash;
        public readonly uint LocalRailHash;
        public readonly uint ServerRailHash;
        public readonly uint LocalBeltHash;
        public readonly uint ServerBeltHash;
        public readonly uint LocalFluidHash;
        public readonly uint ServerFluidHash;

        public bool HasMismatch =>
            LocalTrainHash != ServerTrainHash || LocalRailHash != ServerRailHash || LocalBeltHash != ServerBeltHash || LocalFluidHash != ServerFluidHash;

        public TrainSynchronizationHashComparison(uint localTrain, uint serverTrain, uint localRail, uint serverRail, uint localBelt, uint serverBelt, uint localFluid, uint serverFluid)
        {
            LocalTrainHash = localTrain;
            ServerTrainHash = serverTrain;
            LocalRailHash = localRail;
            ServerRailHash = serverRail;
            LocalBeltHash = localBelt;
            ServerBeltHash = serverBelt;
            LocalFluidHash = localFluid;
            ServerFluidHash = serverFluid;
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
