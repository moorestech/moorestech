using System;
using System.Linq;
using Game.World.Interface.Event;
using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Event.EventReceive
{
    public class PlaceBlockEventPacket
    {
        public const string EventTag = "va:event:blockPlace";
        private readonly EventProtocolProvider _eventProtocolProvider;

        public PlaceBlockEventPacket(IBlockPlaceEvent blockPlaceEvent, EventProtocolProvider eventProtocolProvider)
        {
            blockPlaceEvent.Subscribe(ReceivedEvent);
            _eventProtocolProvider = eventProtocolProvider;
        }

        private void ReceivedEvent(BlockPlaceEventProperties blockPlaceEventProperties)
        {
            var c = blockPlaceEventProperties.CoreVector2Int;
            var blockId = blockPlaceEventProperties.Block.BlockId;

            var messagePack = new PlaceBlockEventMessagePack(c, blockId, (int)blockPlaceEventProperties.BlockDirection);
            var payload = MessagePackSerializer.Serialize(messagePack);

            _eventProtocolProvider.AddBroadcastEvent(EventTag,payload);
        }
    }


    [MessagePackObject]
    public class PlaceBlockEventMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public PlaceBlockEventMessagePack()
        {
        }

        public PlaceBlockEventMessagePack(Vector2Int blockPos, int blockId, int direction)
        {
            BlockPos = new Vector2IntMessagePack(blockPos);
            BlockId = blockId;
            Direction = direction;
        }

        [Key(0)]
        public Vector2IntMessagePack BlockPos { get; set; }
        [Key(1)]
        public int BlockId { get; set; }
        [Key(2)]
        public int Direction { get; set; }
    }
}