using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Protocol.PacketResponse
{
    public class SendPlaceHotBarBlockProtocol : IPacketResponse
    {
        public const string Tag = "va:palceHotbarBlock";
        
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        
        public SendPlaceHotBarBlockProtocol(ServiceProvider serviceProvider)
        {
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<SendPlaceHotBarBlockProtocolMessagePack>(payload.ToArray());
            
            var inventorySlot = PlayerInventoryConst.HotBarSlotToInventorySlot(data.Slot);
            var item = _playerInventoryDataStore.GetInventoryData(data.PlayerId).MainOpenableInventory.GetItem(inventorySlot);
            
            var blockConfig = ServerContext.BlockConfig;
            //アイテムIDがブロックIDに変換できない場合はそもまま処理を終了
            if (!blockConfig.IsBlock(item.Id)) return null;
            //すでにブロックがある場合はそもまま処理を終了
            if (ServerContext.WorldBlockDatastore.Exists(data.Pos)) return null;
            
            //ブロックの作成
            var blockId = blockConfig.ItemIdToBlockId(item.Id);
            var blockSize = blockConfig.GetBlockConfig(blockId).BlockSize;
            var blockPositionInfo = new BlockPositionInfo(data.Pos, data.BlockDirection, blockSize);
            var block = ServerContext.BlockFactory.Create(blockId, BlockInstanceId.Create(), blockPositionInfo);
            //ブロックの設置
            ServerContext.WorldBlockDatastore.TryAddBlock(block);
            
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
        
        [Key(2)] public int PlayerId { get; set; }
        
        [Key(3)] public int Direction { get; set; }
        
        [IgnoreMember] public BlockDirection BlockDirection => (BlockDirection)Direction;
        
        [Key(4)] public int Slot { get; set; }
        
        [Key(5)] public Vector3IntMessagePack Pos { get; set; }
    }
}