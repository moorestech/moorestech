using System;
using Game.SaveLoad.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using UnityEngine;

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
            var request = MessagePackSerializer.Deserialize<BugReportCaptureRequest>(payload);
            switch (request.Operation)
            {
                case BugReportCaptureOperation.CaptureNow:
                    return new BugReportCaptureResponse(_snapshotCaptureRequest.RequestImmediateSnapshot());
            }

            Debug.LogError($"未知のバグ報告取得操作です operation:{request.Operation}");
            return new BugReportCaptureResponse(0);
        }

        #region MessagePack

        [MessagePackObject]
        public class BugReportCaptureRequest : ProtocolMessagePackBase
        {
            [Key(2)] public BugReportCaptureOperation Operation { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public BugReportCaptureRequest() { }

            private BugReportCaptureRequest(BugReportCaptureOperation operation)
            {
                Tag = ProtocolTag;
                Operation = operation;
            }

            public static BugReportCaptureRequest CreateCaptureNowRequest()
            {
                return new BugReportCaptureRequest(BugReportCaptureOperation.CaptureNow);
            }
        }

        [MessagePackObject]
        public class BugReportCaptureResponse : ProtocolMessagePackBase
        {
            [Key(2)] public long RequestedCaptureId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public BugReportCaptureResponse() { }

            public BugReportCaptureResponse(long requestedCaptureId)
            {
                Tag = ProtocolTag;
                RequestedCaptureId = requestedCaptureId;
            }
        }

        public enum BugReportCaptureOperation
        {
            CaptureNow = 0,
        }

        #endregion
    }
}
