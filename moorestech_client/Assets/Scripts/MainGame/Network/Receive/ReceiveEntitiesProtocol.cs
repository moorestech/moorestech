using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.Network.Event;
using MessagePack;
using Server.Protocol.PacketResponse.MessagePack;

namespace MainGame.Network.Receive
{
    public class ReceiveEntitiesProtocol : IAnalysisPacket
    {
        private readonly ReceiveEntitiesDataEvent _entitiesDataEvent;

        public ReceiveEntitiesProtocol(ReceiveEntitiesDataEvent entitiesDataEvent)
        {
            _entitiesDataEvent = entitiesDataEvent;
        }

        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer.Deserialize<EntitiesResponseMessagePack>(packet.ToArray());
            _entitiesDataEvent.InvokeChunkUpdateEvent(data).Forget();
        }
    }
}