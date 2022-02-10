using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.Network.Interface;
using MainGame.Network.Interface.Receive;
using MainGame.Network.Util;

namespace MainGame.Network.Receive
{
    /// <summary>
    /// Analysis player inventory data 
    /// </summary>
    public class ReceivePlayerInventoryProtocol : IAnalysisPacket
    {
        private readonly PlayerInventoryUpdateEvent _playerInventoryUpdateEvent;

        public ReceivePlayerInventoryProtocol(IPlayerInventoryUpdateEvent playerInventoryUpdateEvent)
        {
            _playerInventoryUpdateEvent = playerInventoryUpdateEvent as PlayerInventoryUpdateEvent;
        }

        public void Analysis(List<byte> data)
        {
            var bytes = new ByteArrayEnumerator(data);
            //packet id
            bytes.MoveNextToGetShort();
            //player id
            var playerId = bytes.MoveNextToGetInt();
            //padding
            bytes.MoveNextToGetShort();
            var items = new List<ItemStack>();
            for (int i = 0; i < PlayerInventoryConstant.MainInventorySize; i++)
            {
                var id = bytes.MoveNextToGetInt();
                var count = bytes.MoveNextToGetInt();
                items.Add(new ItemStack(id, count));
            }
            
            _playerInventoryUpdateEvent.OnOnPlayerInventoryUpdateEvent(
                new OnPlayerInventoryUpdateProperties(
                    playerId,
                    items));
        }
    }
}