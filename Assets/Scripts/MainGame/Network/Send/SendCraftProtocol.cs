using System.Collections.Generic;
using System.Linq;
using MainGame.Network.Settings;
using MainGame.Network.Util;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Send
{
    public class SendCraftProtocol
    {
        private const short ProtocolId = 14;
        private readonly ISocket _socket;
        private readonly int _playerId;

        public SendCraftProtocol(ISocket socket,PlayerConnectionSetting playerConnection)
        {
            _playerId = playerConnection.PlayerId;
            _socket = socket;
        }
        
        public void SendOneCraft()
        {
            //craft type id is 0
            _socket.Send(MessagePackSerializer.Serialize(new CraftProtocolMessagePack(
                _playerId,0)).ToList());
        }
        public void SendAllCraft()
        {
            //craft type id is 1
            _socket.Send(MessagePackSerializer.Serialize(new CraftProtocolMessagePack(
                _playerId,1)).ToList());
        }
        public void SendOneStackCraft()
        {
            //craft type id is 2
            _socket.Send(MessagePackSerializer.Serialize(new CraftProtocolMessagePack(
                _playerId,2)).ToList());
        }
    }
}