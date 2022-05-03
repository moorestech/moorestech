using System.Collections.Generic;
using Core.Block.BlockFactory;
using Core.Block.Config.Service;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using Game.World.Interface.Util;
using Microsoft.Extensions.DependencyInjection;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class SendPlaceHotBarBlockProtocol : IPacketResponse
    {
        private ItemIdToBlockId _itemIdToBlockId;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private IWorldBlockDatastore _worldBlockDatastore;
        private BlockFactory _blockFactory;

        public SendPlaceHotBarBlockProtocol(ServiceProvider serviceProvider)
        {
            _itemIdToBlockId = serviceProvider.GetService<ItemIdToBlockId>();
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            _worldBlockDatastore = serviceProvider.GetService<IWorldBlockDatastore>();
            _blockFactory = serviceProvider.GetService<BlockFactory>();
        }

        public List<List<byte>> GetResponse(List<byte> payload)
        {
            var byteListEnumerator = new ByteListEnumerator(payload);
            byteListEnumerator.MoveNextToGetShort();
            var slot = byteListEnumerator.MoveNextToGetShort();
            var x = byteListEnumerator.MoveNextToGetInt();
            var y = byteListEnumerator.MoveNextToGetInt();
            var playerId = byteListEnumerator.MoveNextToGetInt();
            var directionByte = byteListEnumerator.MoveNextToGetByte();

            
            var inventorySlot = PlayerInventoryConst.HotBarSlotToInventorySlot(slot);
            var item = _playerInventoryDataStore.GetInventoryData(playerId).MainOpenableInventory.GetItem(inventorySlot);
            
            
            //アイテムIDがブロックIDに変換できない場合はそもまま処理を終了
            if (!_itemIdToBlockId.CanConvert(item.Id)) return new List<List<byte>>();
            //すでにブロックがある場合はそもまま処理を終了
            if (_worldBlockDatastore.Exists(x,y))  return new List<List<byte>>();

            
            var blockDirection = directionByte switch
            {
                0 => BlockDirection.North,
                1 => BlockDirection.East,
                2 => BlockDirection.South,
                3 => BlockDirection.West,
                _ => BlockDirection.North
            };

            
            //ブロックの作成
            var block = _blockFactory.Create(_itemIdToBlockId.Convert(item.Id), EntityId.NewEntityId());
            //ブロックの設置
            _worldBlockDatastore.AddBlock(block, x, y,blockDirection);
            
            //アイテムを減らし、セットする
            item = item.SubItem(1);
            _playerInventoryDataStore.GetInventoryData(playerId).MainOpenableInventory.SetItem(inventorySlot, item);
            
            
            return new List<List<byte>>();
        }
    }
}