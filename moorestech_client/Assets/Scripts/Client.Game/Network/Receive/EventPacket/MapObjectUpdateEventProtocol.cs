using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using MainGame.Network.Event;
using MessagePack;
using Server.Event.EventReceive;

namespace MainGame.Network.Receive.EventPacket
{
    public class MapObjectUpdateEventProtocol : IAnalysisEventPacket
    {
        private readonly ReceiveUpdateMapObjectEvent _mapObjectUpdateEvent;


        public MapObjectUpdateEventProtocol(ReceiveUpdateMapObjectEvent mapObjectUpdateEvent)
        {
            _mapObjectUpdateEvent = mapObjectUpdateEvent;
        }

        public void Analysis(List<byte> packet)
        {
            var data = MessagePackSerializer.Deserialize<MapObjectUpdateEventMessagePack>(packet.ToArray());
            switch (data.EventType)
            {
                case MapObjectUpdateEventMessagePack.DestroyEventType:
                    _mapObjectUpdateEvent.InvokeOnDestroyMapObject(new MapObjectProperties(data.InstanceId, true)).Forget();
                    break;
                default:
                    throw new Exception("MapObjectUpdateEventProtocol: EventTypeが不正か実装されていません");
            }
        }
    }
}