using System.Collections.Generic;
using Client.Network.API;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;

namespace Client.Game.InGame.BugReport.Capture
{
    // サーバーの即時スナップショット完了イベントを購読し、確保セッションへ渡す
    // Subscribes to the server's immediate-snapshot completion event and forwards it to the capture session
    public sealed class BugReportCaptureEventHandler : IInitializable
    {
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly BugReportCaptureSession _session;

        public BugReportCaptureEventHandler(IVanillaApiEvent vanillaApiEvent, BugReportCaptureSession session)
        {
            _vanillaApiEvent = vanillaApiEvent;
            _session = session;
        }

        public void Initialize()
        {
            _vanillaApiEvent.SubscribeEventResponse(BugReportCaptureCompletedEventPacket.EventTag, OnCaptureCompleted);
        }

        // サーバーが申告した縮退も含めて丸ごと渡す。ここで落とすと欠けたパケットに誰も気づけない
        // Everything the server declared, degradation included, is forwarded; dropping it here hides missing packets from everyone
        private void OnCaptureCompleted(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<BugReportCaptureCompletedEventPacket.BugReportCaptureCompletedMessagePack>(payload);
            _session.OnServerCaptureCompleted(new ServerCaptureCompletion(
                packet.CaptureId, packet.Tick, packet.Success, packet.SnapshotDirectory, packet.ServerDataDirectory,
                packet.SnapshotFileNames, packet.PacketLogFileNames, packet.PacketLogDegradeReason, packet.PacketLogDegradedAtTick));
        }
    }

    // サーバーの書き出し完了イベントの中身。要求の結果と、記録が縮退していたかを1つの値で受け取る
    // The payload of the server's write-completed event; the outcome and any degradation arrive as one value
    public readonly struct ServerCaptureCompletion
    {
        public long CaptureId { get; }
        public ulong Tick { get; }
        public bool Success { get; }
        public string SnapshotDirectory { get; }
        public string ServerDataDirectory { get; }
        public IReadOnlyList<string> SnapshotFileNames { get; }
        public IReadOnlyList<string> PacketLogFileNames { get; }

        // サーバーが申告したパケット記録の縮退。空でなければ区間が揃っていてもパケットは欠けている
        // The server's declared packet-log degradation; when it is not empty the packets are missing even with every segment present
        public string PacketLogDegradeReason { get; }
        public ulong PacketLogDegradedAtTick { get; }

        public ServerCaptureCompletion(long captureId, ulong tick, bool success, string snapshotDirectory, string serverDataDirectory,
            IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames, string packetLogDegradeReason, ulong packetLogDegradedAtTick)
        {
            CaptureId = captureId;
            Tick = tick;
            Success = success;
            SnapshotDirectory = snapshotDirectory;
            ServerDataDirectory = serverDataDirectory;
            SnapshotFileNames = snapshotFileNames;
            PacketLogFileNames = packetLogFileNames;
            PacketLogDegradeReason = packetLogDegradeReason;
            PacketLogDegradedAtTick = packetLogDegradedAtTick;
        }
    }
}
