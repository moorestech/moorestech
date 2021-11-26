using System.Collections.Generic;
using Core.Item;
using PlayerInventory;
using Server.Util;
using World;

namespace Server.PacketHandle.PacketResponse
{
    public class BlockInventoryPlayerInventoryMoveItemProtocol : IPacketResponse
    {
        private WorldBlockDatastore _worldBlockDatastore;
        private PlayerInventoryDataStore _playerInventoryDataStore;

        public BlockInventoryPlayerInventoryMoveItemProtocol(WorldBlockDatastore worldBlockDatastore, PlayerInventoryDataStore playerInventoryDataStore)
        {
            _worldBlockDatastore = worldBlockDatastore;
            _playerInventoryDataStore = playerInventoryDataStore;
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            var payloadData = new ByteArrayEnumerator(payload);
            payloadData.MoveNextToGetShort();
            var flag = payloadData.MoveNextToGetShort();
            var playerId = payloadData.MoveNextToGetInt();
            var playerInventorySlot = payloadData.MoveNextToGetInt();
            var blockX = payloadData.MoveNextToGetInt();
            var blockY = payloadData.MoveNextToGetInt();
            var blockInventoryIndex = payloadData.MoveNextToGetInt();
            var moveItemAmount = payloadData.MoveNextToGetInt();
            
            var blockInventory = _worldBlockDatastore.GetBlockInventory(blockX, blockY);
            var playerInventory = _playerInventoryDataStore.GetInventoryData(playerId);

            //フラグが0の時はプレイヤーインベントリからブロックインベントリにアイテムを移す
            if (flag == 0)
            {
                //プレイヤーインベントリからアイテムを取得
                var originItem = playerInventory.GetItem(playerInventorySlot);
                if (originItem.Amount < moveItemAmount)
                {
                    moveItemAmount = originItem.Amount;
                }
                //実際に移動するアイテムを取得
                var moveItem = ItemStackFactory.Create(originItem.Id,moveItemAmount);
                //ブロックインベントリにアイテムを移動
                var replaceItem = blockInventory.ReplaceItem(blockInventoryIndex,moveItem);
                
                //プレイヤーインベントリに残るアイテムを計算
                var remainItem = replaceItem.AddItem(ItemStackFactory.Create(originItem.Id,originItem.Amount - moveItemAmount)).ProcessResultItemStack;
                
                //プレイヤーインベントリに残りのアイテムを追加
                playerInventory.SetItem(playerInventorySlot,remainItem);
                
            }
            //1の時はブロックからプレイヤーインベントリにアイテムを移す
            else if (flag == 1)
            {
                var originItem = blockInventory.GetItem(blockInventoryIndex);
                if (originItem.Amount < moveItemAmount)
                {
                    moveItemAmount = originItem.Amount;
                }
                var moveItem = ItemStackFactory.Create(originItem.Id,moveItemAmount);
                var replaceItem = playerInventory.ReplaceItem(playerInventorySlot,moveItem);
                var remainItem = replaceItem.AddItem(ItemStackFactory.Create(originItem.Id,originItem.Amount - moveItemAmount)).ProcessResultItemStack;
                blockInventory.SetItem(blockInventoryIndex,remainItem);
            }

            return new List<byte[]>();
        }
    }
}