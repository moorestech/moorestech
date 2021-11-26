using System.Collections.Generic;
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
            var playerInventoryIndex = payloadData.MoveNextToGetInt();
            var blockX = payloadData.MoveNextToGetInt();
            var blockY = payloadData.MoveNextToGetInt();
            var blockInventoryIndex = payloadData.MoveNextToGetInt();
            var moveItemNumber = payloadData.MoveNextToGetInt();


            //フラグが0の時はプレイヤーインベントリからブロックインベントリにアイテムを移す
            if (flag == 0)
            {
                
            }
            //1の時はブロックからプレイヤーインベントリにアイテムを移す
            else if (flag == 1)
            {
                
            }
            else
            {
                
            }

            return new List<byte[]>();
        }
    }
}