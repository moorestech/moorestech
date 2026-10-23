using System;
using Game.Block.Interface.Component;
using Game.Context;
using Game.SpaceElevator;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    // 接続の判定と消費はサービスへ委譲する
    // Delegate connection checks and cable consumption to the service
    public class ConnectSpaceElevatorProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:connectSpaceElevator";

        private readonly SpaceElevatorConnectionService _connectionService;

        public ConnectSpaceElevatorProtocol(ServiceProvider serviceProvider)
        {
            _connectionService = serviceProvider.GetRequiredService<SpaceElevatorConnectionService>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            var request = MessagePackSerializer.Deserialize<ConnectSpaceElevatorRequest>(payload);
            var block = ServerContext.WorldBlockDatastore.GetBlock(request.Position.Vector3Int);
            var result = block == null ? SpaceElevatorConnectResult.NotSpaceElevatorBase : _connectionService.Connect(block);

            // 拒否理由と操作元を開発者ログへ残す
            // Record the refusal reason and requester for developers
            if (result != SpaceElevatorConnectResult.Connected)
            {
                Debug.Log($"[ConnectSpaceElevator] connection refused: {result} pos:{request.Position.Vector3Int} player:{requesterPlayerId}");
            }
            return new ConnectSpaceElevatorResponse(result);
        }
    }

    [MessagePackObject]
    public class ConnectSpaceElevatorRequest : ProtocolMessagePackBase
    {
        [Key(2)] public Vector3IntMessagePack Position;

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ConnectSpaceElevatorRequest()
        {
        }

        public ConnectSpaceElevatorRequest(Vector3Int position)
        {
            Tag = ConnectSpaceElevatorProtocol.ProtocolTag;
            Position = new Vector3IntMessagePack(position);
        }
    }

    [MessagePackObject]
    public class ConnectSpaceElevatorResponse : ProtocolMessagePackBase
    {
        [Key(2)] public SpaceElevatorConnectResult Result;

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ConnectSpaceElevatorResponse()
        {
        }

        public ConnectSpaceElevatorResponse(SpaceElevatorConnectResult result)
        {
            Tag = ConnectSpaceElevatorProtocol.ProtocolTag;
            Result = result;
        }
    }
}
