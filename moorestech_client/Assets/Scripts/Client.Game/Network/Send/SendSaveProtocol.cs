using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Server.Protocol.PacketResponse;
using Server.Util;

namespace MainGame.Network.Send
{
    public class SendSaveProtocol
    {
        private const short ProtocolId = 17;
        private readonly ISocketSender _socketSender;

        public SendSaveProtocol(ISocketSender socketSender)
        {
            _socketSender = socketSender;
        }

        public void Send()
        {
            //todo ここメッセージパック対応
            var packet = new List<byte>();

            packet.AddRange(ToByteList.Convert(ProtocolId));

            _socketSender.Send(MessagePackSerializer.Serialize(new SaveProtocolMessagePack()).ToList());
        }
    }
}