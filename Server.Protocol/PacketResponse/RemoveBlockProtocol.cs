using System;
using System.Collections.Generic;
using Core.Item;
using Game.Block.BlockInventory;
using Game.Block.Interface.BlockConfig;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class RemoveBlockProtocol : IPacketResponse
    {
        public const string Tag = "va:removeBlock";
        private readonly IBlockConfig _blockConfig;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        private readonly IWorldBlockDatastore _worldBlockDatastore;


        public RemoveBlockProtocol(ServiceProvider serviceProvider)
        {
            _worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            _blockConfig = serviceProvider.GetService<IBlockConfig>();
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RemoveBlockProtocolMessagePack>(payload.ToArray());


            
            var playerMainInventory =
                _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;

            var isNotRemainItem = true;

            
            if (_worldBlockDatastore.TryGetBlock<IBlockInventory>(data.X, data.Y, out var blockInventory))
                
                for (var i = 0; i < blockInventory.GetSlotSize(); i++)
                {
                    
                    var remainItem = playerMainInventory.InsertItem(blockInventory.GetItem(i));
                    
                    //s
                    blockInventory.SetItem(i, remainItem);

                    
                    if (!remainItem.Equals(_itemStackFactory.CreatEmpty())) isNotRemainItem = false;
                }


            

            
            //Id
            var blockId = _worldBlockDatastore.GetBlock(data.X, data.Y).BlockId;
            //ID
            var blockItemId = _blockConfig.GetBlockConfig(blockId).ItemId;
            var remainBlockItem = playerMainInventory.InsertItem(_itemStackFactory.Create(blockItemId, 1));


            
            if (isNotRemainItem && remainBlockItem.Equals(_itemStackFactory.CreatEmpty())) _worldBlockDatastore.RemoveBlock(data.X, data.Y);

            return new List<List<byte>>();
        }
    }


    [MessagePackObject(true)]
    public class RemoveBlockProtocolMessagePack : ProtocolMessagePackBase
    {
        public RemoveBlockProtocolMessagePack(int playerId, int x, int y)
        {
            Tag = RemoveBlockProtocol.Tag;
            PlayerId = playerId;
            X = x;
            Y = y;
        }


        [Obsolete("。。")]
        public RemoveBlockProtocolMessagePack()
        {
        }

        public int PlayerId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }
}