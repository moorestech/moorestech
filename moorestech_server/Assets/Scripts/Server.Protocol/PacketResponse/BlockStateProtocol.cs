using System;
using System.Collections.Generic;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;

namespace Server.Protocol.PacketResponse
{
    public class BlockStateProtocol : IPacketResponse
    {
        public const string Tag = "va:blockState";
        
        public BlockStateProtocol(ServiceProvider serviceProvider)
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
            
            return new ResponseBlockStateProtocolMessagePack(stateList);
        }
    }
    
    [MessagePackObject]
    public class RequestBlockStateProtocolMessagePack : ProtocolMessagePackBase
    {
        public RequestBlockStateProtocolMessagePack()
        {
            Tag = BlockStateProtocol.Tag;
        }
    }
    
    [MessagePackObject]
    public class ResponseBlockStateProtocolMessagePack : ProtocolMessagePackBase
    {
        [Key(2)] public List<BlockStateMessagePack> StateList { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ResponseBlockStateProtocolMessagePack()
        {
        }
        
        public ResponseBlockStateProtocolMessagePack(List<BlockStateMessagePack> stateList)
        {
            Tag = BlockStateProtocol.Tag;
            StateList = stateList;
        }
    }
}