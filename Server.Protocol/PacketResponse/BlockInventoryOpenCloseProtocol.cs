using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class BlockInventoryOpenCloseProtocol : IPacketResponse
    {
        private readonly IBlockInventoryOpenStateDataStore _inventoryOpenState;
        private const byte IsOpenFlag = 1;

        public BlockInventoryOpenCloseProtocol(IBlockInventoryOpenStateDataStore inventoryOpenState)
        {
            _inventoryOpenState = inventoryOpenState;
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            var enumerator = new ByteArrayEnumerator(payload);
            enumerator.MoveNextToGetShort(); //packet id
            var x = enumerator.MoveNextToGetInt();
            var y = enumerator.MoveNextToGetInt();
            var playerId = enumerator.MoveNextToGetInt();
            var isOpen = enumerator.MoveNextToGetByte() == IsOpenFlag;

            //開く、閉じるのセット
            if (isOpen)
            {
                _inventoryOpenState.Open(playerId,new Coordinate(x,y));
            }
            else
            {
                _inventoryOpenState.Close(playerId);
            }



            return new List<byte[]>();
        }
    }
}