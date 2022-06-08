using System.Collections.Generic;
using System.Linq;
using MainGame.Basic;
using MainGame.Network.Settings;
using MainGame.Network.Util;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Send
{
    public class SendPlaceHotBarBlockProtocol
    {
        private const short ProtocolId = 8;
        
        private readonly ISocket _socket;
        private readonly int _playerId;

        public SendPlaceHotBarBlockProtocol(ISocket socket,PlayerConnectionSetting playerConnectionSetting)
        {
            _socket = socket;
            _playerId = playerConnectionSetting.PlayerId;
        }

        public void Send(int x, int y, short hotBarSlot,BlockDirection blockDirection)
        {
            _socket.Send(MessagePackSerializer.Serialize(new SendPlaceHotBarBlockProtocolMessagePack(
                _playerId,(int)blockDirection,hotBarSlot,x,y)).ToList());
        }

        private byte GetBlockDirectionId(BlockDirection blockDirection)
        {
            return blockDirection switch
            {
                BlockDirection.North => 0,
                BlockDirection.East => 1,
                BlockDirection.South => 2,
                BlockDirection.West => 3,
                _ => 0
            };
        }
    }
}