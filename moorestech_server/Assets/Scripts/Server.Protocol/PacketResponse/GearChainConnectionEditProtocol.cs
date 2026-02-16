using System;
using System.Collections.Generic;
using Core.Master;
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

        public GearChainConnectionEditProtocol(ServiceProvider serviceProvider)
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload)
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
                        success = GearChainSystemUtil.TryConnect(data.PosAVector, data.PosBVector, data.PlayerId, new ItemId(data.ItemId), out error);
                        break;

                    case ChainEditMode.Disconnect:
                        success = GearChainSystemUtil.TryDisconnect(data.PosAVector, data.PosBVector, data.PlayerId, out error);
                        break;

                    default:
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
            [Key(5)] public int PlayerId { get; set; }
            [Key(6)] public int ItemId { get; set; }

            [IgnoreMember] public Vector3Int PosAVector => PosA;
            [IgnoreMember] public Vector3Int PosBVector => PosB;

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public GearChainConnectionEditRequest() { Tag = GearChainConnectionEditProtocol.Tag; }

            public static GearChainConnectionEditRequest CreateConnectRequest(Vector3Int posA, Vector3Int posB, int playerId, ItemId itemId)
            {
                return new GearChainConnectionEditRequest
                {
                    Tag = GearChainConnectionEditProtocol.Tag,
                    PosA = new Vector3IntMessagePack(posA),
                    PosB = new Vector3IntMessagePack(posB),
                    Mode = ChainEditMode.Connect,
                    PlayerId = playerId,
                    ItemId = itemId.AsPrimitive(),
                };
            }

            public static GearChainConnectionEditRequest CreateDisconnectRequest(Vector3Int posA, Vector3Int posB, int playerId)
            {
                return new GearChainConnectionEditRequest
                {
                    Tag = GearChainConnectionEditProtocol.Tag,
                    PosA = new Vector3IntMessagePack(posA),
                    PosB = new Vector3IntMessagePack(posB),
                    Mode = ChainEditMode.Disconnect,
                    PlayerId = playerId,
                };
            }
        }

        [MessagePackObject]
        public class GearChainConnectionEditResponse : ProtocolMessagePackBase
        {
            [Key(2)] public bool IsSuccess { get; set; }
            [Key(3)] public string Error { get; set; }

            [IgnoreMember] public bool HasError => !string.IsNullOrEmpty(Error);

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
