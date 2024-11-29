using System;
using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class DebugBlockInfoRequestProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:blockDebug";
        
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        
        public DebugBlockInfoRequestProtocol(ServiceProvider serviceProvider)
        {
            _worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestDebugBlockInfoRequestProtocolMessagePack>(payload.ToArray());
            
            var block = _worldBlockDatastore.GetBlock(data.BlockPos);
            if (block == null)
            {
                return new ResponseDebugBlockInfoRequestProtocolMessagePack(null);
            }
            
            var blockDebugInfo = new List<BlockDebugInfo>();
            var debugInfos = block.ComponentManager.GetComponents<IBlockDebugInfo>();
            foreach (var debug in debugInfos)
            {
                blockDebugInfo.Add(debug.GetDebugInfo());
            }
            
            return new ResponseDebugBlockInfoRequestProtocolMessagePack(blockDebugInfo);
        }
        
        
        [MessagePackObject]
        public class RequestDebugBlockInfoRequestProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3IntMessagePack BlockPos { get; set; }
            
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestDebugBlockInfoRequestProtocolMessagePack() { }
            
            public RequestDebugBlockInfoRequestProtocolMessagePack(Vector3Int pos)
            {
                Tag = BlockInventoryRequestProtocol.ProtocolTag;
                BlockPos = new Vector3IntMessagePack(pos);
            }
        }
        
        [MessagePackObject]
        public class ResponseDebugBlockInfoRequestProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public List<BlockDebugInfo> BlockDebugInfos { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseDebugBlockInfoRequestProtocolMessagePack() { }
            public ResponseDebugBlockInfoRequestProtocolMessagePack(List<BlockDebugInfo> blockDebugInfo)
            {
                Tag = BlockInventoryRequestProtocol.ProtocolTag;
                BlockDebugInfos = blockDebugInfo;
            }
        }
    }
}