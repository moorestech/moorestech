using System;
using Core.Master;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Protocol.PacketResponse.Util.ElectricWire;
using Server.Util.MessagePack;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Server.Protocol.PacketResponse
{
    public class ElectricWireConnectionEditProtocol : IPacketResponse
    {
        public const string Tag = "va:electricWireConnectionEdit";

        public ElectricWireConnectionEditProtocol(ServiceProvider serviceProvider)
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            // 要求データをデシリアライズする
            // Deserialize request payload
            var request = MessagePackSerializer.Deserialize<ElectricWireConnectionEditRequest>(payload);

            // 編集処理を実行し、結果データを構築する
            // Execute edit operation and build response data
            return ExecuteEdit(request);

            #region Internal

            ProtocolMessagePackBase ExecuteEdit(ElectricWireConnectionEditRequest data)
            {
                // モードに応じて接続または切断を実行する
                // Execute connect or disconnect depending on mode
                bool success;
                ElectricWirePlacementFailureReason failureReason;

                switch (data.Mode)
                {
                    case WireEditMode.Connect:
                        success = ElectricWireSystemUtil.TryConnect(data.PosAVector, data.PosBVector, data.PlayerId, data.ConnectToolGuid, out failureReason);
                        break;

                    case WireEditMode.Disconnect:
                        success = ElectricWireSystemUtil.TryDisconnect(data.PosAVector, data.PosBVector, data.PlayerId, out failureReason);
                        break;

                    default:
                        return new ElectricWireConnectionEditResponse(false, ElectricWirePlacementFailureReason.InvalidMode);
                }

                return new ElectricWireConnectionEditResponse(success, failureReason);
            }

            #endregion
        }

        [MessagePackObject]
        public class ElectricWireConnectionEditRequest : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack PosA { get; set; }
            [Key(3)] public Vector3IntMessagePack PosB { get; set; }
            [Key(4)] public WireEditMode Mode { get; set; }
            [Key(5)] public int PlayerId { get; set; }
            [Key(6)] public Guid ConnectToolGuid { get; set; }

            [IgnoreMember] public Vector3Int PosAVector => PosA;
            [IgnoreMember] public Vector3Int PosBVector => PosB;

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ElectricWireConnectionEditRequest() { Tag = ElectricWireConnectionEditProtocol.Tag; }

            public static ElectricWireConnectionEditRequest CreateConnectRequest(Vector3Int posA, Vector3Int posB, int playerId, Guid connectToolGuid)
            {
                return new ElectricWireConnectionEditRequest
                {
                    Tag = ElectricWireConnectionEditProtocol.Tag,
                    PosA = new Vector3IntMessagePack(posA),
                    PosB = new Vector3IntMessagePack(posB),
                    Mode = WireEditMode.Connect,
                    PlayerId = playerId,
                    ConnectToolGuid = connectToolGuid,
                };
            }

            public static ElectricWireConnectionEditRequest CreateDisconnectRequest(Vector3Int posA, Vector3Int posB, int playerId)
            {
                return new ElectricWireConnectionEditRequest
                {
                    Tag = ElectricWireConnectionEditProtocol.Tag,
                    PosA = new Vector3IntMessagePack(posA),
                    PosB = new Vector3IntMessagePack(posB),
                    Mode = WireEditMode.Disconnect,
                    PlayerId = playerId,
                };
            }
        }

        [MessagePackObject]
        public class ElectricWireConnectionEditResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool IsSuccess { get; set; }
            [Key(3)] public ElectricWirePlacementFailureReason FailureReason { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ElectricWireConnectionEditResponse() { }

            public ElectricWireConnectionEditResponse(bool isSuccess, ElectricWirePlacementFailureReason failureReason)
            {
                IsSuccess = isSuccess;
                FailureReason = failureReason;
            }
        }

        public enum WireEditMode
        {
            Connect,
            Disconnect,
        }
    }
}
