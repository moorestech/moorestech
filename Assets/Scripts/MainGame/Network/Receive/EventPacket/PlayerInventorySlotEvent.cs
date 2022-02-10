using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Event;
using MainGame.Network.Interface;
using MainGame.Network.Interface.Receive;
using MainGame.Network.Util;

namespace MainGame.Network.Receive.EventPacket
{
    public class PlayerInventorySlotEvent : IAnalysisEventPacket
    {
        private PlayerInventoryUpdateEvent _playerInventoryUpdateEvent;

        public PlayerInventorySlotEvent(IPlayerInventoryUpdateEvent playerInventorySlotEvent)
        {
            _playerInventoryUpdateEvent = playerInventorySlotEvent as PlayerInventoryUpdateEvent;
        }

        public void Analysis(List<byte> packet)
        {
            var bytes = new ByteArrayEnumerator(packet);
            bytes.MoveNextToGetShort();
            bytes.MoveNextToGetShort();
            var slot = bytes.MoveNextToGetInt();
            var id = bytes.MoveNextToGetInt();
            var count = bytes.MoveNextToGetInt();
            
            _playerInventoryUpdateEvent.OnOnPlayerInventorySlotUpdateEvent(
                new OnPlayerInventorySlotUpdateProperties(
                    slot,new ItemStack(id,count)));
        }
    }
}