using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.Network.Event;
using MessagePack;
using Server.Protocol.PacketResponse;

namespace MainGame.Network.Receive
{
    public class ReceiveMapObjectDestructionInformationProtocol : IAnalysisPacket
    {
        private readonly ReceiveUpdateMapObjectEvent _updateMapObjectEvent;


        public ReceiveMapObjectDestructionInformationProtocol(ReceiveUpdateMapObjectEvent updateMapObjectEvent)
        {
            _updateMapObjectEvent = updateMapObjectEvent;
        }

        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer.Deserialize<ResponseMapObjectsMessagePack>(packet.ToArray());

            var mapObjectEvent = new List<MapObjectProperties>();
            foreach (var mapObject in data.MapObjects) mapObjectEvent.Add(new MapObjectProperties(mapObject.InstanceId, mapObject.IsDestroyed));

            _updateMapObjectEvent.InvokeReceiveMapObjectInformation(mapObjectEvent).Forget();
        }
    }
}