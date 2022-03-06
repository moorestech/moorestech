using System.Collections.Generic;
using MainGame.Basic;
using MainGame.Network.Util;

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
            var packet = new List<byte>();
            
            packet.AddRange(ToByteList.Convert(ProtocolId));
            packet.AddRange(ToByteList.Convert(hotBarSlot));
            packet.AddRange(ToByteList.Convert(x));
            packet.AddRange(ToByteList.Convert(y));
            packet.AddRange(ToByteList.Convert(_playerId));
            packet.Add(GetBlockDirectionId(blockDirection));

            _socket.Send(packet.ToArray());
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