using System;
using System.Linq;
using Game.World.Interface.Event;
using MessagePack;

namespace Server.Event.EventReceive
{
    public class PlaceBlockToSetEventPacket
    {
        public const string EventTag = "va:event:blockPlace";
        private readonly EventProtocolProvider _eventProtocolProvider;

        public PlaceBlockToSetEventPacket(IBlockPlaceEvent blockPlaceEvent, EventProtocolProvider eventProtocolProvider)
        {
            blockPlaceEvent.Subscribe(ReceivedEvent);
            _eventProtocolProvider = eventProtocolProvider;
        }

        private void ReceivedEvent(BlockPlaceEventProperties blockPlaceEventProperties)
        {
            var c = blockPlaceEventProperties.Coordinate;
            var blockId = blockPlaceEventProperties.Block.BlockId;

            var payload = MessagePackSerializer.Serialize(new PlaceBlockEventMessagePack(
                c.X, c.Y, blockId, (int)blockPlaceEventProperties.BlockDirection
            )).ToList();
            ;

            _eventProtocolProvider.AddBroadcastEvent(payload);
        }
    }


    [MessagePackObject(true)]
    public class PlaceBlockEventMessagePack : EventProtocolMessagePackBase
    {
        [Obsolete("。。")]
        public PlaceBlockEventMessagePack()
        {
        }

        public PlaceBlockEventMessagePack(int x, int y, int blockId, int direction)
        {
            EventTag = PlaceBlockToSetEventPacket.EventTag;
            X = x;
            Y = y;
            BlockId = blockId;
            Direction = direction;
        }

        public int X { get; set; }
        public int Y { get; set; }
        public int BlockId { get; set; }
        public int Direction { get; set; }
    }
}