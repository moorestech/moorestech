using System;
using Game.SaveLoad.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    // バグ報告用の即時スナップショットを要求する。書き出しはtick末尾のリングが行い、完了はイベントで通知する
    // Requests an immediate snapshot for a bug report; the tick-end ring writes it and completion arrives as an event
    public class BugReportCaptureProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:bugReportCapture";

        private readonly ISnapshotCaptureRequest _snapshotCaptureRequest;

        public BugReportCaptureProtocol(ServiceProvider serviceProvider)
        {
            _snapshotCaptureRequest = serviceProvider.GetRequiredService<ISnapshotCaptureRequest>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            MessagePackSerializer.Deserialize<BugReportCaptureRequest>(payload);
            var result = _snapshotCaptureRequest.RequestImmediateSnapshot();
            return new BugReportCaptureResponse(result.Accepted, result.RequestId, result.RejectedReason);
        }

        #region MessagePack

        [MessagePackObject]
        public class BugReportCaptureRequest : ProtocolMessagePackBase
        {
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public BugReportCaptureRequest() { }

            public static BugReportCaptureRequest CreateCaptureNowRequest()
            {
                return new BugReportCaptureRequest { Tag = ProtocolTag };
            }
        }

        [MessagePackObject]
        public class BugReportCaptureResponse : ProtocolMessagePackBase
        {
            // 受理されたかを型で返す。拒否のときは要求IDが無いので、要求元は完了イベントを待たずに理由を出す
            // Acceptance is carried explicitly; a rejection has no request id, so the requester surfaces the reason instead of waiting for the completion event
            [Key(2)] public bool Accepted { get; set; }
            [Key(3)] public long RequestedCaptureId { get; set; }
            [Key(4)] public string RejectedReason { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public BugReportCaptureResponse() { }

            public BugReportCaptureResponse(bool accepted, long requestedCaptureId, string rejectedReason)
            {
                Tag = ProtocolTag;
                Accepted = accepted;
                RequestedCaptureId = requestedCaptureId;
                RejectedReason = rejectedReason;
            }
        }

        #endregion
    }
}
