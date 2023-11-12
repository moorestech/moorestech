using System.Linq;
using Game.World.Interface.DataStore;
using MainGame.Basic;
using MainGame.Network.Settings;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Send
{
    public class SendPlaceHotBarBlockProtocol
    {
        private readonly int _playerId;
        private readonly ISocketSender _socketSender;

        public SendPlaceHotBarBlockProtocol(ISocketSender socketSender, PlayerConnectionSetting playerConnectionSetting)
        {
            _socketSender = socketSender;
            _playerId = playerConnectionSetting.PlayerId;
        }

        public void Send(int x, int y, short hotBarSlot, BlockDirection blockDirection)
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new SendPlaceHotBarBlockProtocolMessagePack(
                _playerId, (int)blockDirection, hotBarSlot, x, y)).ToList());
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