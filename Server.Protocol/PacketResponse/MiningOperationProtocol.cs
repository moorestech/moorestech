using System.Collections.Generic;
using Core.Item;
using Core.Item.Config;
using Core.Ore;
using Core.Ore.Config;
using Game.PlayerInventory.Interface;
using Game.WorldMap;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class MiningOperationProtocol : IPacketResponse
    {
        public const string Tag = "va:miningOre";
        
        private readonly VeinGenerator _veinGenerator;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IOreConfig _oreConfig;
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
            var byteListEnumerator = new ByteListEnumerator(payload);
            byteListEnumerator.MoveNextToGetShort();
            int x = byteListEnumerator.MoveNextToGetInt();
            int y = byteListEnumerator.MoveNextToGetInt();
            int playerId = byteListEnumerator.MoveNextToGetInt();
            
            
            //プレイヤーインベントリーの取得
            var playerMainInventory =
                _playerInventoryDataStore.GetInventoryData(playerId).MainOpenableInventory;
            
            //鉱石IDを取得
            var oreId = _veinGenerator.GetOreId(x, y);
            //鉱石のアイテムID
            var oreItemId = _oreConfig.OreIdToItemId(oreId);
            //プレイヤーインベントリーに鉱石を挿入
            playerMainInventory.InsertItem(_itemStackFactory.Create(oreItemId,1));
            
            
            return new List<List<byte>>();
        }
    }
    
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class MiningOperationProtocolMessagePack : ProtocolMessagePackBase
    {
        public int PlayerId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsOpen { get; set; }
    }
}