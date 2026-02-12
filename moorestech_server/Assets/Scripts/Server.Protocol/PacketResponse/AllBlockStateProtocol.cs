using System;
using System.Collections.Generic;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;

namespace Server.Protocol.PacketResponse
{
    public class AllBlockStateProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:allBockState";
        
        public AllBlockStateProtocol(ServiceProvider serviceProvider)
        {
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var stateList = new List<BlockStateMessagePack>();
            foreach (var block in ServerContext.WorldBlockDatastore.BlockMasterDictionary.Values)
            {
                var pos = block.BlockPositionInfo.OriginalPos;
                var state = block.Block.GetBlockState();
                if (state != null) stateList.Add(new BlockStateMessagePack(state, pos));
            }
            
            return new ResponseAllBlockStateProtocolMessagePack(stateList);
        }
        
        [MessagePackObject]
        public class RequestAllBlockStateProtocolMessagePack : ProtocolMessagePackBase
        {
            public RequestAllBlockStateProtocolMessagePack()
            {
                Tag = ProtocolTag;
            }
        }
        
        [MessagePackObject]
        public class ResponseAllBlockStateProtocolMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public List<BlockStateMessagePack> StateList { get; set; }
            
            [Obsolete("This constructor is for deserialization. Do not use directly.")]
            public ResponseAllBlockStateProtocolMessagePack() { }
            
            public ResponseAllBlockStateProtocolMessagePack(List<BlockStateMessagePack> stateList)
            {
                Tag = ProtocolTag;
                StateList = stateList;
            }
        }
    }
}