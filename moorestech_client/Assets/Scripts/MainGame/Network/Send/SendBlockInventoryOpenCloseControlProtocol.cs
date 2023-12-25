using System.Linq;
using MainGame.Network.Settings;
using MessagePack;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace MainGame.Network.Send
{
    public class SendBlockInventoryOpenCloseControlProtocol
    {
        private readonly int _playerId;
        private readonly ISocketSender _socketSender;


        public SendBlockInventoryOpenCloseControlProtocol(PlayerConnectionSetting playerConnectionSetting, ISocketSender socketSender)
        {
            _socketSender = socketSender;
            _playerId = playerConnectionSetting.PlayerId;
        }

        public void Send(Vector2Int pos, bool isOpen)
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new BlockInventoryOpenCloseProtocolMessagePack(
                _playerId, pos.x, pos.y, isOpen)).ToList());
        }
    }
}