using System;
using System.Collections.Generic;
using Core.Block.BlockInventory;
using Core.Block.Config;
using Core.Item;
using Core.Item.Config;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class RemoveBlockProtocol : IPacketResponse
    {
        public const string Tag = "va:removeBlock";
        
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly ItemStackFactory _itemStackFactory;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly IBlockConfig _blockConfig;
        private readonly IWorldBlockComponentDatastore<IBlockInventory> _worldBlockComponentDatastore;


        public  RemoveBlockProtocol(ServiceProvider serviceProvider)
        {
            _worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _itemStackFactory = serviceProvider.GetService<ItemStackFactory>();
            _blockConfig = serviceProvider.GetService<IBlockConfig>();
            _worldBlockComponentDatastore = serviceProvider.GetService<IWorldBlockComponentDatastore<IBlockInventory>>();
        }
        
        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RemoveBlockProtocolMessagePack>(payload.ToArray());
            
            
            //プレイヤーインベントリーの取得
            var playerMainInventory =
                _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;

            var isNotRemainItem = true;
            
            //インベントリがある時は
            if (_worldBlockComponentDatastore.ExistsComponentBlock(data.X, data.Y) == true)
            {
                //プレイヤーインベントリにブロック内のアイテムを挿入
                var blockInventory = _worldBlockComponentDatastore.GetBlock(data.X, data.Y);
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
            var blockId = _worldBlockDatastore.GetBlock(data.X,data.Y).BlockId;
            //ブロックのIDを取得
            var blockItemId = _blockConfig.GetBlockConfig(blockId).ItemId;
            var remainBlockItem = playerMainInventory.InsertItem(_itemStackFactory.Create(blockItemId,1));
                
            
            //ブロック内のアイテムを全てインベントリに入れ、ブロックもインベントリに入れれた時だけブロックを削除する
            if (isNotRemainItem && remainBlockItem.Equals(_itemStackFactory.CreatEmpty()))
            {
                _worldBlockDatastore.RemoveBlock(data.X, data.Y);
            }

            return new List<List<byte>>();
        }

    }
    
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class RemoveBlockProtocolMessagePack : ProtocolMessagePackBase
    {
        public RemoveBlockProtocolMessagePack(int playerId, int x, int y)
        {
            Tag = RemoveBlockProtocol.Tag;
            PlayerId = playerId;
            X = x;
            Y = y;
        }

        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RemoveBlockProtocolMessagePack() { }

        public int PlayerId { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
    }
}