using System;
using System.Collections.Generic;
using Core.Block.Config;
using Core.Block.Machine.util;
using Core.Block.RecipeConfig;
using Core.Item;
using Core.Util;
using Game.World.Interface;
using Microsoft.Extensions.DependencyInjection;
using Server.PacketHandle.PacketResponse;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class PutBlockProtocol : IPacketResponse
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IBlockConfig _blockConfig;
        private readonly IMachineRecipeConfig _recipeConfig;
        private readonly ItemStackFactory _itemStackFactory;

        public PutBlockProtocol(ServiceProvider serviceProvider)
        {
            _worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            _blockConfig = serviceProvider.GetService<IBlockConfig>();
            _recipeConfig = serviceProvider.GetService<IMachineRecipeConfig>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            //パケットのパース、接続元、接続先のインスタンス取得
            var payloadData = new ByteArrayEnumerator(payload);
            payloadData.MoveNextToGetShort();
            int blockId = payloadData.MoveNextToGetInt();
            payloadData.MoveNextToGetShort();
            int x = payloadData.MoveNextToGetInt();
            int y = payloadData.MoveNextToGetInt();
            Console.WriteLine("Place Block blockID:" + blockId + " x:" + x + " y:" + y);
            
            var inputBlock = _worldBlockDatastore.GetBlockInventory(payloadData.MoveNextToGetInt());

            //TODO 機械の設置処理なので、ブロックの設置処理に書き直す
            var block = NormalMachineFactory.Create(blockId, IntId.NewIntId(), _worldBlockDatastore.GetBlockInventory(payloadData.MoveNextToGetInt()),_blockConfig,_recipeConfig,_itemStackFactory);
            inputBlock.ChangeConnector(block);
            
            _worldBlockDatastore.AddBlock(block, x, y,block);
            //返すものはない
            return new List<byte[]>();
        }
    }
}