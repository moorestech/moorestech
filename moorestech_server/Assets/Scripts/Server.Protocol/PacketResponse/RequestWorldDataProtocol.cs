using System;
using System.Collections.Generic;
using Game.Context;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class RequestWorldDataProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getWorldData";


        public RequestWorldDataProtocol()
        {
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            // リクエストからPlayerIdを取得
            // Get PlayerId from request
            MessagePackSerializer.Deserialize<RequestWorldDataMessagePack>(payload);

            // ブロック収集（既存処理）
            // Collect blocks (existing logic)
            var blockMasterDictionary = ServerContext.WorldBlockDatastore.BlockMasterDictionary;
            var blockResult = new List<BlockDataMessagePack>();
            foreach (var blockMaster in blockMasterDictionary)
            {
                var block = blockMaster.Value.Block;
                var pos = blockMaster.Value.BlockPositionInfo.OriginalPos;
                var blockDirection = blockMaster.Value.BlockPositionInfo.BlockDirection;
                blockResult.Add(new BlockDataMessagePack(block.BlockId, pos, blockDirection, block.BlockInstanceId));
            }

            // ベルト走行品は専用の確定tickストリームで同期する。
            // Running belt items synchronize through their dedicated completed-tick stream.
            return new ResponseWorldDataMessagePack(blockResult.ToArray(), Array.Empty<EntityMessagePack>());
        }


        [MessagePackObject]
        public class RequestWorldDataMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }

            public RequestWorldDataMessagePack(int playerId)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
            }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestWorldDataMessagePack() { }
        }
        
        [MessagePackObject]
        public class ResponseWorldDataMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public BlockDataMessagePack[] Blocks { get; set; }
            [Key(3)] public EntityMessagePack[] Entities { get; set; }
            
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseWorldDataMessagePack() { }
            public ResponseWorldDataMessagePack(BlockDataMessagePack[] Block, EntityMessagePack[] entities)
            {
                Tag = ProtocolTag;
                Blocks = Block;
                Entities = entities;
            }
        }
    }
}
