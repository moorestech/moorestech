using System.Collections.Generic;
using Core.Item;
using Core.Item.Util;
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
            var blockInventorySlot = payloadData.MoveNextToGetInt();
            var moveItemAmount = payloadData.MoveNextToGetInt();
            
            var blockInventory = _worldBlockDatastore.GetBlockInventory(blockX, blockY);
            var playerInventory = _playerInventoryDataStore.GetInventoryData(playerId);

            //フラグが0の時はプレイヤーインベントリからブロックインベントリにアイテムを移す
            if (flag == 0)
            {
                //プレイヤーインベントリからアイテムを取得
                var originItem = playerInventory.GetItem(playerInventorySlot);
                //動かすアイテム数の修正
                if (originItem.Amount < moveItemAmount)
                {
                    moveItemAmount = originItem.Amount;
                }
                //実際に移動するアイテムを取得
                var moveItem = ItemStackFactory.Create(originItem.Id,moveItemAmount);
                
                var blockInventoryItem = blockInventory.GetItem(blockInventorySlot);
                
                //移動先アイテムがnullの時はそのまま入れかえる
                //もしくは、移動先と同じIDの時は移動先スロットに加算し、余ったアイテムをプレイヤーインベントリに入れる
                if (blockInventoryItem.Id == ItemConst.NullItemId || originItem.Id == blockInventoryItem.Id)
                {
                    //ブロックインベントリにアイテムを移動
                    var replaceItem = blockInventory.ReplaceItem(blockInventorySlot,moveItem);
                
                    //プレイヤーインベントリに残るアイテムを計算
                    //ゼロの時は自動でNullItemになる
                    var playerItemAmount = originItem.Amount - moveItemAmount;
                    var remainItem = replaceItem.AddItem(ItemStackFactory.Create(originItem.Id,playerItemAmount)).ProcessResultItemStack;
                    
                    //プレイヤーインベントリに残りのアイテムをセット
                    playerInventory.SetItem(playerInventorySlot,remainItem);
                }
                else
                {
                    //プレイヤーインベントリのアイテムをすべて入れ替える時にのみ入れ替えを実行する
                    //一部入れ替え時は入れ替え作業は実行しない
                    if (moveItemAmount == originItem.Amount)
                    {
                        blockInventory.SetItem(blockInventorySlot,originItem);
                        playerInventory.SetItem(playerInventorySlot,blockInventoryItem);
                    }
                }
            }
            //1の時はブロックからプレイヤーインベントリにアイテムを移す
            //0と逆の事をしているだけで基本的なロジックは同じ
            else if (flag == 1)
            {
                var originItem = blockInventory.GetItem(blockInventorySlot);
                if (originItem.Amount < moveItemAmount)
                {
                    moveItemAmount = originItem.Amount;
                }
                var moveItem = ItemStackFactory.Create(originItem.Id,moveItemAmount);
                var playerInventoryItem = playerInventory.GetItem(playerInventorySlot);
                
                if (playerInventoryItem.Id == ItemConst.NullItemId || originItem.Id == playerInventoryItem.Id)
                {
                    var replaceItem = playerInventory.ReplaceItem(playerInventorySlot,moveItem);
                
                    var blockItemAmount = originItem.Amount - moveItemAmount;
                    var remainItem = replaceItem.AddItem(ItemStackFactory.Create(originItem.Id,blockItemAmount)).ProcessResultItemStack;
                    
                    blockInventory.SetItem(blockInventorySlot,remainItem);
                }
                else
                {
                    if (moveItemAmount == originItem.Amount)
                    {
                        playerInventory.SetItem(playerInventorySlot,originItem);
                        blockInventory.SetItem(blockInventorySlot,playerInventoryItem);
                    }
                }
            }

            return new List<byte[]>();
        }
    }
}