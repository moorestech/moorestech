using System;

namespace MainGame.Network.Send
{
    public class SendCraftingInventoryMoveItemProtocol
    {
        private const short ProtocolId = 13;
        private readonly ISocket _socket;
        private readonly int _playerId;

        public SendCraftingInventoryMoveItemProtocol(ISocket socket,PlayerConnectionSetting playerConnection)
        {
            _playerId = playerConnection.PlayerId;
            _socket = socket;
        }
        
        public void Send( int fromSlot, int toSlot, int itemCount)
        {
            throw new NotImplementedException();
        }
    }
}