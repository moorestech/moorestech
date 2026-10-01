using System;
using System.Collections.Generic;
using System.Linq;
using Game.Context;
using Game.Entity.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse.Util;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class RequestWorldDataProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getWorldData";
        public RequestWorldDataProtocol(ServiceProvider serviceProvider) { }

        public ProtocolMessagePackBase GetResponse(byte[] payload, int requesterPlayerId)
        {
            // リクエストを読み、接続のプレイヤー位置を使う
            // Read the request and use the position of the bound player
            var request = MessagePackSerializer.Deserialize<RequestWorldDataMessagePack>(payload);

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

            // ベルトの表示状態は初期snapshotとtick差分の専用経路で送る。
            // Belt display state uses its initial snapshot and tick-difference stream.
            return new ResponseWorldDataMessagePack(blockResult.ToArray(), Array.Empty<EntityMessagePack>());
        }


        [MessagePackObject]
        public class RequestWorldDataMessagePack : ProtocolMessagePackBase
        {

            public RequestWorldDataMessagePack()
            {
                Tag = ProtocolTag;
            }

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
