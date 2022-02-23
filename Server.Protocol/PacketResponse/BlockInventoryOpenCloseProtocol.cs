using System.Collections.Generic;
using Game.PlayerInventory.Interface;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
using Server.Util;

namespace Server.Protocol.PacketResponse
{
    public class BlockInventoryOpenCloseProtocol : IPacketResponse
    {
        private readonly IBlockInventoryOpenStateDataStore _inventoryOpenState;
        private const byte IsOpenFlag = 1;

        public BlockInventoryOpenCloseProtocol(ServiceProvider serviceProvider)
        {
            _inventoryOpenState = serviceProvider.GetService<IBlockInventoryOpenStateDataStore>();
        }

        public List<byte[]> GetResponse(List<byte> payload)
        {
            var byteListEnumerator = new ByteListEnumerator(payload);
            byteListEnumerator.MoveNextToGetShort(); //packet id
            var x = byteListEnumerator.MoveNextToGetInt();
            var y = byteListEnumerator.MoveNextToGetInt();
            var playerId = byteListEnumerator.MoveNextToGetInt();
            var isOpen = byteListEnumerator.MoveNextToGetByte() == IsOpenFlag;

            //開く、閉じるのセット
            if (isOpen)
            {
                _inventoryOpenState.Open(playerId,x,y);
            }
            else
            {
                _inventoryOpenState.Close(playerId);
            }



            return new List<byte[]>();
        }
    }
}