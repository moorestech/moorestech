using System.Linq;
using MessagePack;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace MainGame.Network.Send
{
    public class SendRequestBlockInventoryProtocol
    {
        private readonly ISocketSender _socketSender;

        public SendRequestBlockInventoryProtocol(ISocketSender socketSender)
        {
            _socketSender = socketSender;
        }

        public void Send(Vector2Int pos)
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new RequestBlockInventoryRequestProtocolMessagePack(pos.x, pos.y)).ToList());
        }
    }
}