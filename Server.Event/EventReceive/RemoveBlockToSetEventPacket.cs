using System;
using System.Collections.Generic;
using System.Linq;
using Game.World.Interface.Event;
using MessagePack;
using Server.Util;

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
            var c = blockPlaceEventProperties.Coordinate;
            
            
            _eventProtocolProvider.AddBroadcastEvent(new RemoveBlockEventMessagePack(
                c.X,c.Y));
        }
    }
    [MessagePackObject(keyAsPropertyName :true)]
    public class RemoveBlockEventMessagePack : EventProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RemoveBlockEventMessagePack() { }

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