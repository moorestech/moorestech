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
            
            var payload = MessagePackSerializer.Serialize(new RemoveBlockEventMessagePack(c.x, c.y));
            
            _eventProtocolProvider.AddBroadcastEvent(EventTag,payload);
        }
    }

    [MessagePackObject]
    public class RemoveBlockEventMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RemoveBlockEventMessagePack() { }

        public RemoveBlockEventMessagePack(int x, int y)
        {
            X = x;
            Y = y;
        }

        [Key(0)]
        public int X { get; set; }
        [Key(1)]
        public int Y { get; set; }
    }
}