using System;
using System.Collections.Generic;
using Game.Block.Base;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using Game.World.Interface.Util;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class SendPlaceHotBarBlockProtocol : IPacketResponse
    {
        public const string Tag = "va:palceHotbarBlock";
        private readonly IBlockFactory _blockFactory;

        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IBlockConfig _blockConfig;

        public SendPlaceHotBarBlockProtocol(ServiceProvider serviceProvider)
        {
            _blockConfig = serviceProvider.GetService<IBlockConfig>();
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            _blockFactory = serviceProvider.GetService<IBlockFactory>();
        }

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<SendPlaceHotBarBlockProtocolMessagePack>(payload.ToArray());

            var inventorySlot = PlayerInventoryConst.HotBarSlotToInventorySlot(data.Slot);
            var item = _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory
                .GetItem(inventorySlot);

            //アイテムIDがブロックIDに変換できない場合はそもまま処理を終了
            if (!_blockConfig.IsBlock(item.Id)) return null;
            //すでにブロックがある場合はそもまま処理を終了
            if (_worldBlockDatastore.Exists(data.Pos)) return null;

            //ブロックの作成
            var block = _blockFactory.Create(_blockConfig.ItemIdToBlockId(item.Id), CreateBlockEntityId.Create());
            //ブロックの設置
            _worldBlockDatastore.AddBlock(block, data.Pos, data.BlockDirection);

            //アイテムを減らし、セットする
            item = item.SubItem(1);
            _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory
                .SetItem(inventorySlot, item);


            return null;
        }
    }


    [MessagePackObject]
    public class SendPlaceHotBarBlockProtocolMessagePack : ProtocolMessagePackBase
    {
        public SendPlaceHotBarBlockProtocolMessagePack(int playerId, BlockDirection direction, int slot, Vector3Int pos)
        {
            Tag = SendPlaceHotBarBlockProtocol.Tag;
            PlayerId = playerId;
            Direction = (int)direction;
            Slot = slot;
            Pos = new Vector3IntMessagePack(pos);
        }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public SendPlaceHotBarBlockProtocolMessagePack()
        {
        }

        [Key(2)]
        public int PlayerId { get; set; }
        [Key(3)]
        public int Direction { get; set; }
        
        [IgnoreMember]
        public BlockDirection BlockDirection => (BlockDirection) Direction;
        
        [Key(4)]
        public int Slot { get; set; }

        [Key(5)]
        public Vector3IntMessagePack Pos { get; set; }
    }
}