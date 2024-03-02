using System;
using System.Collections.Generic;
using Core.Const;
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

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<RemoveBlockProtocolMessagePack>(payload.ToArray());


            //プレイヤーインベントリーの取得
            var playerMainInventory =
                _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory;

            var isNotRemainItem = true;

            //インベントリがある時は
            if (_worldBlockDatastore.TryGetBlock<IBlockInventory>(data.X, data.Y, out var blockInventory))
                //プレイヤーインベントリにブロック内のアイテムを挿入
                for (var i = 0; i < blockInventory.GetSlotSize(); i++)
                {
                    //プレイヤーインベントリにアイテムを挿入
                    var remainItem = playerMainInventory.InsertItem(blockInventory.GetItem(i));
                    //余ったアイテムをブロックに戻す
                    //この時、もしプレイヤーインベントリにアイテムを入れれたのなら、空のアイテムをブロックに戻すようになっているs
                    blockInventory.SetItem(i, remainItem);

                    //アイテムが入りきらなかったらブロックを削除しないフラグを立てる
                    if (!remainItem.Equals(_itemStackFactory.CreatEmpty())) isNotRemainItem = false;
                }


            //インベントリに削除するブロックを入れる

            //壊したブロックをインベントリーに挿入
            //ブロックIdの取得
            var blockId = _worldBlockDatastore.GetBlock(data.X, data.Y).BlockId;
            //すでにブロックがなかったら-1
            if (blockId == BlockConst.EmptyBlockId)
            {
                return null;
            }
            
            //ブロックのIDを取得
            var blockItemId = _blockConfig.GetBlockConfig(blockId).ItemId;
            var remainBlockItem = playerMainInventory.InsertItem(_itemStackFactory.Create(blockItemId, 1));


            //ブロック内のアイテムを全てインベントリに入れ、ブロックもインベントリに入れれた時だけブロックを削除する
            if (isNotRemainItem && remainBlockItem.Equals(_itemStackFactory.CreatEmpty()))
                _worldBlockDatastore.RemoveBlock(data.X, data.Y);

            return null;
        }
    }


    [MessagePackObject]
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
        public RemoveBlockProtocolMessagePack()
        {
        }

        [Key(2)]
        public int PlayerId { get; set; }
        [Key(3)]
        public int X { get; set; }
        [Key(4)]
        public int Y { get; set; }
    }
}