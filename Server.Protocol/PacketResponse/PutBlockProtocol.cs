using System;
using System.Collections.Generic;
using Core.Block.BlockFactory;
using Core.Block.RecipeConfig;
using Core.Item;
using Game.World.Interface.DataStore;
using Game.World.Interface.Util;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class PutBlockProtocol : IPacketResponse
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly BlockFactory blockFactory;
        public const string Tag = "va:putBlock";

        public PutBlockProtocol(ServiceProvider serviceProvider)
        {
            _worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            blockFactory = serviceProvider.GetService<BlockFactory>();
            serviceProvider.GetService<IMachineRecipeConfig>();
            serviceProvider.GetService<ItemStackFactory>();
        }
        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<PutBlockProtocolMessagePack>(payload.ToArray());
            
            Console.WriteLine("Place Block blockID:" + data.Id + " x:" + data.X + " y:" + data.Y);
            
            
            var block = blockFactory.Create(data.Id, CreateBlockEntityId.Create());

            _worldBlockDatastore.AddBlock(block, data.X, data.Y, BlockDirection.North);
            return new List<List<byte>>();
        }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class PutBlockProtocolMessagePack : ProtocolMessagePackBase
    {
        public PutBlockProtocolMessagePack(int id, int x, int y)
        {
            Tag = PutBlockProtocol.Tag;
            Id = id;
            X = x;
            Y = y;
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public PutBlockProtocolMessagePack(){}

        public int Id { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        
        
    }
}