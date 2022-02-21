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
            
            
            //プレイヤーインベントリーの取得
            var playerMainInventory =
                _playerInventoryDataStore.GetInventoryData(playerId).MainInventory;

            var isNotRemainItem = true;
            
            //インベントリがある時は
            if (_worldBlockComponentDatastore.ExistsComponentBlock(x, y) == true)
            {
                //プレイヤーインベントリにブロック内のアイテムを挿入
                var blockInventory = _worldBlockComponentDatastore.GetBlock(x, y);
                for (int i = 0; i < blockInventory.GetSlotSize(); i++)
                {
                    //プレイヤーインベントリにアイテムを挿入
                    var remainItem = playerMainInventory.InsertItem(blockInventory.GetItem(i));
                    //余ったアイテムをブロックに戻す
                    //この時、もしプレイヤーインベントリにアイテムを入れれたのなら、空のアイテムをブロックに戻すようになっているs
                    blockInventory.SetItem(i,remainItem);
                    
                    //アイテムが入りきらなかったらブロックを削除しないフラグを立てる
                    if (!remainItem.Equals(_itemStackFactory.CreatEmpty()))
                    {
                        isNotRemainItem = false;
                    }
                }
            }
            
            
            //インベントリに削除するブロックを入れる
            
            //壊したブロックをインベントリーに挿入
            //ブロックIdの取得
            var blockId = _worldBlockDatastore.GetBlock(x,y).GetBlockId();
            //ブロックのIDを取得
            var blockItemId = _blockConfig.GetBlockConfig(blockId).ItemId;
            var remainBlockItem = playerMainInventory.InsertItem(_itemStackFactory.Create(blockItemId,1));
                
            
            //ブロック内のアイテムを全てインベントリに入れ、ブロックもインベントリに入れれた時だけブロックを削除する
            if (isNotRemainItem && remainBlockItem.Equals(_itemStackFactory.CreatEmpty()))
            {
                _worldBlockDatastore.RemoveBlock(x, y);
            }

            return new List<byte[]>();
        }

    }
}