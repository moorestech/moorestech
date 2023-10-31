using System;
using System.Linq;
using Game.World.Interface.Event;
using MessagePack;

namespace Server.Event.EventReceive
{
    public class RemoveBlockToSetEventPacket
    {
        public const string EventTag = "va:event:removeBlock";
        private readonly EventProtocolProvider _eventProtocolProvider;

        public RemoveBlockToSetEventPacket(IBlockRemoveEvent blockRemoveEvent, EventProtocolProvider eventProtocolProvider)
        {
            blockRemoveEvent.Subscribe(ReceivedEvent);
            _eventProtocolProvider = eventProtocolProvider;
        }

        private void ReceivedEvent(BlockRemoveEventProperties blockPlaceEventProperties)
        {
            var c = blockPlaceEventProperties.CoreVector2Int;


            var payload = MessagePackSerializer.Serialize(new RemoveBlockEventMessagePack(
                c.X, c.Y)).ToList();
            ;


            _eventProtocolProvider.AddBroadcastEvent(payload);
        }
    }

    [MessagePackObject(true)]
    public class RemoveBlockEventMessagePack : EventProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RemoveBlockEventMessagePack()
        {
        }

        public RemoveBlockEventMessagePack(int x, int y)
        {
            EventTag = RemoveBlockToSetEventPacket.EventTag;
            X = x;
            Y = y;
        }

        public int X { get; set; }
        public int Y { get; set; }
    }
}