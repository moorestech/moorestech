using System.Collections.Generic;
using Core.Block.BlockFactory;
using Core.Block.Config.Service;
using Core.Util;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class SendPlaceHotBarBlockProtocol : IPacketResponse
    {
        private ItemIdToBlockId _itemIdToBlockId;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;
        private IWorldBlockDatastore _worldBlockDatastore;
        private BlockFactory _blockFactory;

        public SendPlaceHotBarBlockProtocol(
            ItemIdToBlockId itemIdToBlockId, IPlayerInventoryDataStore playerInventoryDataStore, 
            IWorldBlockDatastore worldBlockDatastore, BlockFactory blockFactory)
        {
            _itemIdToBlockId = itemIdToBlockId;
            _playerInventoryDataStore = playerInventoryDataStore;
            _worldBlockDatastore = worldBlockDatastore;
            _blockFactory = blockFactory;
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            var packet = new ByteArrayEnumerator(payload);
            packet.MoveNextToGetShort();
            var slot = packet.MoveNextToGetShort();
            var x = packet.MoveNextToGetInt();
            var y = packet.MoveNextToGetInt();
            var playerId = packet.MoveNextToGetInt();

            var inventorySlot = PlayerInventoryConst.HotBarSlotToInventorySlot(slot);
            
            var item = _playerInventoryDataStore.GetInventoryData(playerId).GetItem(inventorySlot);
            //アイテムIDがブロックIDに変換できない場合はそもまま処理を終了
            if (!_itemIdToBlockId.CanConvert(item.Id)) return new List<byte[]>();
            
            //ブロックの作成
            var block = _blockFactory.Create(_itemIdToBlockId.Convert(item.Id), IntId.NewIntId());
            //ブロックの設置
            _worldBlockDatastore.AddBlock(block, x, y,BlockDirection.North);
            
            //アイテムを減らし、セットする
            item = item.SubItem(1);
            _playerInventoryDataStore.GetInventoryData(playerId).SetItem(inventorySlot, item);
            
            
            return new List<byte[]>();
        }
    }
}