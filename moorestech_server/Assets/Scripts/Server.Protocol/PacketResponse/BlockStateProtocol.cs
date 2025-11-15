using System;
using System.Collections.Generic;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class BlockStateProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:blockState";
        
        public BlockStateProtocol(ServiceProvider serviceProvider)
        {
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestBlockStateProtocolMessagePack>(payload.ToArray());
            
            var block = ServerContext.WorldBlockDatastore.GetBlock(data.Position.Vector3Int);
            if (block == null)
            {
                return new ResponseBlockStateProtocolMessagePack(null);
            }
            
            var blockState = block.GetBlockState();
            
            return new ResponseBlockStateProtocolMessagePack(new BlockStateMessagePack(blockState, data.Position.Vector3Int));
        }
        
        [MessagePackObject]
        public class RequestBlockStateProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack Position { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestBlockStateProtocolMessagePack() { }
            
            public RequestBlockStateProtocolMessagePack(Vector3Int pos)
            {
                Tag = ProtocolTag;
                Position = new Vector3IntMessagePack(pos);
            }
        }
        
        [MessagePackObject]
        public class ResponseBlockStateProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public BlockStateMessagePack State { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseBlockStateProtocolMessagePack() { }
            
            public ResponseBlockStateProtocolMessagePack(BlockStateMessagePack state)
            {
                Tag = ProtocolTag;
                State = state;
            }
        }
    }
}