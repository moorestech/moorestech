using Client.Network.API;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;

namespace Client.Game.InGame.BugReport
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

        private void OnCaptureCompleted(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<BugReportCaptureCompletedEventPacket.BugReportCaptureCompletedMessagePack>(payload);
            _session.OnServerCaptureCompleted(packet.CaptureId, packet.Tick, packet.Success, packet.SnapshotDirectory, packet.SnapshotFileNames, packet.PacketLogFileNames);
        }
    }
}
