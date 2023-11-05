using System.Linq;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Send
{
    public class RequestBlockInventoryProtocol
    {
        private readonly ISocketSender _socketSender;

        public RequestBlockInventoryProtocol(ISocketSender socketSender)
        {
            _socketSender = socketSender;
        }

        public void Send(int x, int y)
        {
            _socketSender.Send(MessagePackSerializer.Serialize(new RequestBlockInventoryRequestProtocolMessagePack(
                x, y)).ToList());
        }
    }
}