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

namespace Server.Protocol.PacketResponse
{
    public class RequestWorldDataProtocol : IPacketResponse
    {
        public const string Tag = "va:getWorldData";
        private readonly IEntityFactory _entityFactory;
        
        public RequestWorldDataProtocol(ServiceProvider serviceProvider)
        {
            _entityFactory = serviceProvider.GetService<IEntityFactory>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var blockMasterDictionary = ServerContext.WorldBlockDatastore.BlockMasterDictionary;
            var blockResult = new List<BlockDataMessagePack>();
            foreach (var blockMaster in blockMasterDictionary)
            {
                var block = blockMaster.Value.Block;
                var pos = blockMaster.Value.BlockPositionInfo.OriginalPos;
                var blockDirection = blockMaster.Value.BlockPositionInfo.BlockDirection;
                blockResult.Add(new BlockDataMessagePack(block.BlockId, pos, blockDirection));
            }
            
            //TODO 今はベルトコンベアのアイテムをエンティティとして返しているだけ 今後は本当のentityも返す
            var items = CollectBeltConveyorItems.CollectItemFromWorld(_entityFactory);
            var entities = new List<EntityMessagePack>();
            entities.AddRange(items.Select(item => new EntityMessagePack(item)));
            
            
            return new ResponseWorldDataMessagePack(blockResult.ToArray(), entities.ToArray());
        }
    }
    
    [MessagePackObject]
    public class RequestWorldDataMessagePack : ProtocolMessagePackBase
    {
        public RequestWorldDataMessagePack()
        {
            Tag = RequestWorldDataProtocol.Tag;
        }
    }
    
    [MessagePackObject]
    public class ResponseWorldDataMessagePack : ProtocolMessagePackBase
    {
        [Key(2)] public BlockDataMessagePack[] Blocks { get; set; }
        
        [Key(3)] public EntityMessagePack[] Entities { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ResponseWorldDataMessagePack()
        {
        }
        
        public ResponseWorldDataMessagePack(BlockDataMessagePack[] Block, EntityMessagePack[] entities)
        {
            Tag = RequestWorldDataProtocol.Tag;
            Blocks = Block;
            Entities = entities;
        }
    }
}