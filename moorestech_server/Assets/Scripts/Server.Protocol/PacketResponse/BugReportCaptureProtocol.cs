using System;
using Game.SaveLoad.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    // バグ報告用の即時スナップショットを要求する。書き出しはtick末尾のリングが行い、完了はイベントで通知する
    // Requests an immediate snapshot for a bug report; the tick-end ring writes it and completion arrives as an event
    public class BugReportCaptureProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:bugReportCapture";

        private readonly ISnapshotCaptureRequest _snapshotCaptureRequest;
        private readonly BugReportCaptureRequesterRegistry _requesterRegistry;

        public BugReportCaptureProtocol(ServiceProvider serviceProvider)
        {
            _snapshotCaptureRequest = serviceProvider.GetRequiredService<ISnapshotCaptureRequest>();
            _requesterRegistry = serviceProvider.GetRequiredService<BugReportCaptureRequesterRegistry>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            MessagePackSerializer.Deserialize<BugReportCaptureRequest>(payload);

            // 完了は要求元1人へ返すので、プレイヤーが確定していない接続からは受け付けない
            // The completion goes back to the single requester, so a connection with no bound player is refused
            if (!context.PlayerId.HasValue)
            {
                const string reason = "プレイヤーが確定していない接続のため即時スナップショット要求を受け付けられません";
                Debug.LogWarning(reason);
                return new BugReportCaptureResponse(false, 0, reason);
            }

            var result = _snapshotCaptureRequest.RequestImmediateSnapshot();
            if (result.Accepted) _requesterRegistry.Remember(result.RequestId, context.PlayerId.Value);
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
