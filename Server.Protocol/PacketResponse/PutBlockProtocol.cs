using System;
using System.Collections.Generic;
using Core.Block.BlockFactory;
using Core.Block.RecipeConfig;
using Core.Item;
using Game.World.Interface.DataStore;
using Game.World.Interface.Util;
using Microsoft.Extensions.DependencyInjection;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class PutBlockProtocol : IPacketResponse
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly BlockFactory blockFactory;
        private readonly IMachineRecipeConfig _recipeConfig;
        private readonly ItemStackFactory _itemStackFactory;

        public PutBlockProtocol(ServiceProvider serviceProvider)
        {
            _worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            blockFactory = serviceProvider.GetService<BlockFactory>();
            _recipeConfig = serviceProvider.GetService<IMachineRecipeConfig>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            //パケットのパース、接続元、接続先のインスタンス取得
            var byteListEnumerator = new ByteListEnumerator(payload);
            byteListEnumerator.MoveNextToGetShort();
            int blockId = byteListEnumerator.MoveNextToGetInt();
            byteListEnumerator.MoveNextToGetShort();
            int x = byteListEnumerator.MoveNextToGetInt();
            int y = byteListEnumerator.MoveNextToGetInt();
            Console.WriteLine("Place Block blockID:" + blockId + " x:" + x + " y:" + y);

            var block = blockFactory.Create(blockId, EntityId.NewEntityId());

            _worldBlockDatastore.AddBlock(block, x, y, BlockDirection.North);
            //返すものはない
            return new List<byte[]>();
        }
    }
}