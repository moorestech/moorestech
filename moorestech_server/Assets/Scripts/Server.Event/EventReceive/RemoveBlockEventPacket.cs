using System;
using Game.Context;
using Game.World.Interface.DataStore;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

namespace Server.Event.EventReceive
{
    public class RemoveBlockToSetEventPacket
    {
        public const string EventTag = "va:event:removeBlock";
        private readonly EventProtocolProvider _eventProtocolProvider;
        
        public RemoveBlockToSetEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(OnBlockRemove);
        }
        
        private void OnBlockRemove(BlockUpdateProperties updateProperties)
        {
            var pos = updateProperties.Pos;
            var payload = MessagePackSerializer.Serialize(new RemoveBlockEventMessagePack(pos));
            
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
    
    [MessagePackObject]
    public class RemoveBlockEventMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RemoveBlockEventMessagePack()
        {
        }
        
        public RemoveBlockEventMessagePack(Vector3Int pos)
        {
            Position = new Vector3IntMessagePack(pos);
        }
        
        [Key(0)] public Vector3IntMessagePack Position { get; set; }
    }
}