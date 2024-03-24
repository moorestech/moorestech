using System;
using Game.World.Interface.Event;
using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

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
            var c = blockPlaceEventProperties.Pos;

            var payload = MessagePackSerializer.Serialize(new RemoveBlockEventMessagePack(c));

            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }

    [MessagePackObject]
    public class RemoveBlockEventMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RemoveBlockEventMessagePack() { }

        public RemoveBlockEventMessagePack(Vector3Int pos)
        {
            Position = new Vector3IntMessagePack(pos);
        }

        [Key(0)]
        public Vector3IntMessagePack Position { get; set; }
    }
}