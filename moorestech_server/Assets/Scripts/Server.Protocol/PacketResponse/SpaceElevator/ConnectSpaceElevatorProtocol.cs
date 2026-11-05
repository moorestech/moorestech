using System;
using Game.Block.Interface;
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
            // 欠損と個体の置換を消費前に拒否する
            // Reject missing fields and replaced instances before consuming cables
            if (request == null || request.Position == null || !request.ExpectedInstanceId.HasValue)
                return Respond(SpaceElevatorConnectResult.InvalidRequest);
            var block = ServerContext.WorldBlockDatastore.GetBlock(request.Position.Vector3Int);
            if (block == null) return Respond(SpaceElevatorConnectResult.BlockNotFound);
            if (block.BlockInstanceId.AsPrimitive() != request.ExpectedInstanceId.Value)
                return Respond(SpaceElevatorConnectResult.InstanceMismatch);
            return Respond(_connectionService.Connect(block));

            #region Internal
            ConnectSpaceElevatorResponse Respond(SpaceElevatorConnectResult result)
            {
                // 拒否理由と操作元を記録する
                // Record the refusal reason and requester
                if (result != SpaceElevatorConnectResult.Connected)
                    Debug.Log($"[ConnectSpaceElevator] connection refused: {result} pos:{request?.Position?.Vector3Int} player:{requesterPlayerId}");
                return new ConnectSpaceElevatorResponse(result);
            }
            #endregion
        }
    }

    [MessagePackObject]
    public class ConnectSpaceElevatorRequest : ProtocolMessagePackBase
    {
        [Key(2)] public Vector3IntMessagePack Position;
        [Key(3)] public int? ExpectedInstanceId;

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ConnectSpaceElevatorRequest()
        {
        }

        public ConnectSpaceElevatorRequest(Vector3Int position, BlockInstanceId expectedInstanceId)
        {
            Tag = ConnectSpaceElevatorProtocol.ProtocolTag;
            Position = new Vector3IntMessagePack(position);
            ExpectedInstanceId = expectedInstanceId.AsPrimitive();
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
