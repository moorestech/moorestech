using System;

namespace MainGame.Network.Send
{
    public class SendCraftingInventoryMainInventoryMoveItemProtocol
    {
        private const short ProtocolId = 12;
        private readonly ISocket _socket;
        private readonly int _playerId;

        public SendCraftingInventoryMainInventoryMoveItemProtocol(ISocket socket,PlayerConnectionSetting playerConnection)
        {
            _playerId = playerConnection.PlayerId;
            _socket = socket;
        }
        
        public void Send(bool toCrafting, int fromSlot, int toSlot, int itemCount)
        {
            throw new NotImplementedException();
        }
    }
}