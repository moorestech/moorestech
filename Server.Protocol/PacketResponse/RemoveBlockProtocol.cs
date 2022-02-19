using System.Collections.Generic;
using Core.Block.BlockInventory;
using Core.Block.Config;
using Core.Item;
using Core.Item.Config;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class RemoveBlockProtocol : IPacketResponse
    {
        
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private ItemStackFactory _itemStackFactory = new ItemStackFactory(new TestItemConfig());
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly IBlockConfig _blockConfig;
        private readonly IWorldBlockComponentDatastore<IBlockInventory> _worldBlockComponentDatastore;


        public  RemoveBlockProtocol(ServiceProvider serviceProvider)
        {
            _worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _blockConfig = serviceProvider.GetService<IBlockConfig>();
            _worldBlockComponentDatastore = serviceProvider.GetService<IWorldBlockComponentDatastore<IBlockInventory>>();
        }
        
        public List<byte[]> GetResponse(List<byte> payload)
        {
            
            var payloadData = new ByteArrayEnumerator(payload);
            payloadData.MoveNextToGetShort();
            int x = payloadData.MoveNextToGetInt();
            int y = payloadData.MoveNextToGetInt();
            int playerId = payloadData.MoveNextToGetInt();
            
            //ブロックIdの取得
            var Blockid = _worldBlockDatastore.GetBlock(x,y).GetBlockId();
            //ブロック情報の取得
            var blockConfigData = _blockConfig.GetBlockConfig(Blockid);
            
            //プレイヤーインベントリーの設定
            var playerInventoryData =
                _playerInventoryDataStore.GetInventoryData(playerId);
            //壊したブロックをインベントリーに挿入
            playerInventoryData.MainInventory.InsertItem(_itemStackFactory.Create(blockConfigData.ItemId,1));
            
            if (_worldBlockComponentDatastore.ExistsComponentBlock(x, y) == true)
            {
                var BlockInventory = _worldBlockComponentDatastore.GetBlock(x, y);
                for (int i = 0; i < BlockInventory.GetSlotSize(); i++)
                {
                    playerInventoryData.MainInventory.InsertItem(BlockInventory.GetItem(i));
                }
                    
            }
                
            //ブロック削除
            _worldBlockDatastore.RemoveBlock(x, y);

            return new List<byte[]>();
            
            
        }

    }
}