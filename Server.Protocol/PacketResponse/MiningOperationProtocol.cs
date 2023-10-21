using System;
using System.Collections.Generic;
using Core.Item;
using Core.Ore;
using Game.PlayerInventory.Interface;
using Game.WorldMap;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    public class MiningOperationProtocol : IPacketResponse
    {
        public const string Tag = "va:miningOre";
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IOreConfig _oreConfig;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        private readonly VeinGenerator _veinGenerator;
        private Seed _seed;

        public MiningOperationProtocol(ServiceProvider serviceProvider)
        {
            _veinGenerator = serviceProvider.GetService<VeinGenerator>();
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            _oreConfig = serviceProvider.GetService<IOreConfig>();
            _seed = serviceProvider.GetService<Seed>();
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<MiningOperationProtocolMessagePack>(payload.ToArray());


            
            var playerMainInventory =
                _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;

            //ID
            var oreId = _veinGenerator.GetOreId(data.X, data.Y);
            //ID
            var oreItemId = _oreConfig.OreIdToItemId(oreId);
            
            playerMainInventory.InsertItem(_itemStackFactory.Create(oreItemId, 1));


            return new List<List<byte>>();
        }
    }


    [MessagePackObject(true)]
    public class MiningOperationProtocolMessagePack : ProtocolMessagePackBase
    {
        [Obsolete("。。")]
        public MiningOperationProtocolMessagePack()
        {
        }

        public MiningOperationProtocolMessagePack(int playerId, int x, int y)
        {
            Tag = MiningOperationProtocol.Tag;
            PlayerId = playerId;
            X = x;
            Y = y;
        }

        public int PlayerId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }
}