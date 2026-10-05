using System;
using System.Collections.Generic;
using Server.Event.Notification;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.GearChain;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class GearChainConnectionEditProtocol : IPacketResponse
    {
        public const string Tag = "va:gearChainConnectionEdit";

        private readonly NotificationService _notificationService;

        public GearChainConnectionEditProtocol(ServiceProvider serviceProvider)
        {
            _notificationService = serviceProvider.GetService<NotificationService>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            // 要求データをデシリアライズする
            // Deserialize request payload
            var request = MessagePackSerializer.Deserialize<GearChainConnectionEditRequest>(payload);

            // 編集処理を実行し、結果データを構築する
            // Execute edit operation and build response data
            return ExecuteEdit(request);

            #region Internal

            ProtocolMessagePackBase ExecuteEdit(GearChainConnectionEditRequest data)
            {
                // モードに応じて接続または切断を実行する
                // Execute connect or disconnect depending on mode
                bool success;
                string error;

                switch (data.Mode)
                {
                    case ChainEditMode.Connect:
                        success = GearChainSystemUtil.TryConnect(data.PosAVector, data.PosBVector, requesterPlayerId, data.ConnectToolGuid, out var connectFailure);
                        error = success ? string.Empty : connectFailure.ToString();
                        // 応答を待たない接続元へ拒否を通知する
                        // Notify send-only connection callers of refusals
                        if (!success)
                        {
                            Debug.LogWarning($"[GearChainConnectionEdit] connect denied: {connectFailure} posA={data.PosAVector} posB={data.PosBVector} player={requesterPlayerId}");
                            _notificationService.Notify(requesterPlayerId, NotificationMessagePack.CreateOperationDenied($"denied.gearChainConnect.{connectFailure}", Array.Empty<string>()));
                        }
                        break;

                    case ChainEditMode.Disconnect:
                        success = GearChainSystemUtil.TryDisconnect(data.PosAVector, data.PosBVector, requesterPlayerId, out var disconnectFailure);
                        error = success ? string.Empty : disconnectFailure.ToString();
                        // 返却不能などの拒否を要求者へ通知する
                        // Notify the requester of refusals such as an unfitting refund
                        if (!success)
                        {
                            Debug.LogWarning($"[GearChainConnectionEdit] disconnect denied: {disconnectFailure} posA={data.PosAVector} posB={data.PosBVector} player={requesterPlayerId}");
                            _notificationService.Notify(requesterPlayerId, NotificationMessagePack.CreateOperationDenied($"denied.gearChainDisconnect.{disconnectFailure}", Array.Empty<string>()));
                        }
                        break;

                    default:
                        Debug.LogWarning($"[GearChainConnectionEdit] Invalid mode: {data.Mode}");
                        return new GearChainConnectionEditResponse(false, "Invalid mode");
                }

                return new GearChainConnectionEditResponse(success, error);
            }

            #endregion
        }

        [MessagePackObject]
        public class GearChainConnectionEditRequest : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack PosA { get; set; }
            [Key(3)] public Vector3IntMessagePack PosB { get; set; }
            [Key(4)] public ChainEditMode Mode { get; set; }
            [Key(6)] public Guid ConnectToolGuid { get; set; }

            [IgnoreMember] public Vector3Int PosAVector => PosA;
            [IgnoreMember] public Vector3Int PosBVector => PosB;

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public GearChainConnectionEditRequest() { Tag = GearChainConnectionEditProtocol.Tag; }

            public static GearChainConnectionEditRequest CreateConnectRequest(Vector3Int posA, Vector3Int posB, Guid connectToolGuid)
            {
                return new GearChainConnectionEditRequest
                {
                    Tag = GearChainConnectionEditProtocol.Tag,
                    PosA = new Vector3IntMessagePack(posA),
                    PosB = new Vector3IntMessagePack(posB),
                    Mode = ChainEditMode.Connect,
                    ConnectToolGuid = connectToolGuid,
                };
            }

            public static GearChainConnectionEditRequest CreateDisconnectRequest(Vector3Int posA, Vector3Int posB)
            {
                return new GearChainConnectionEditRequest
                {
                    Tag = GearChainConnectionEditProtocol.Tag,
                    PosA = new Vector3IntMessagePack(posA),
                    PosB = new Vector3IntMessagePack(posB),
                    Mode = ChainEditMode.Disconnect,
                    ConnectToolGuid = Guid.Empty,
                };
            }
        }

        [MessagePackObject]
        public class GearChainConnectionEditResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool IsSuccess { get; set; }
            [Key(3)] public string Error { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public GearChainConnectionEditResponse() { }

            public GearChainConnectionEditResponse(bool isSuccess, string error)
            {
                IsSuccess = isSuccess;
                Error = error ?? string.Empty;
            }
        }

        public enum ChainEditMode
        {
            Connect,
            Disconnect,
        }
    }
}
