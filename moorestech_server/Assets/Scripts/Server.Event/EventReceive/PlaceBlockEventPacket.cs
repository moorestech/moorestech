using System;
using System.Linq;
using Game.Block.Interface;
using Game.World.Interface.DataStore;
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
            var c = blockPlaceEventProperties.Pos;
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

        public PlaceBlockEventMessagePack(Vector3Int blockPos, int blockId, int direction)
        {
            BlockData = new BlockDataMessagePack(blockId, blockPos, (BlockDirection)direction);
        }
        
        [Key(0)]
        public BlockDataMessagePack BlockData { get; set; }
    }
    
    

    [MessagePackObject]
    public class BlockDataMessagePack
    {
        [Key(0)]
        public int BlockId { get; set; }
        [Key(1)]
        public Vector3IntMessagePack BlockPos { get; set; }
        [Key(2)]
        public int Direction { get; set; }
        
        [IgnoreMember]
        public BlockDirection BlockDirection => (BlockDirection)Direction;
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlockDataMessagePack() { }
        
        public BlockDataMessagePack(int blockId, Vector3Int blockPos, BlockDirection blockDirection)
        {
            BlockId = blockId;
            BlockPos = new Vector3IntMessagePack(blockPos);
            Direction = (int)blockDirection;
        }
    }
}