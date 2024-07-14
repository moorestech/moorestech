using System;
using Game.Block.Interface;
using Game.Context;
using Game.World.Interface.DataStore;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

namespace Server.Event.EventReceive
{
    public class PlaceBlockEventPacket
    {
        public const string EventTag = "va:event:blockPlace";
        private readonly EventProtocolProvider _eventProtocolProvider;
        
        public PlaceBlockEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnPlaceBlock);
        }
        
        private void OnPlaceBlock(BlockUpdateProperties updateProperties)
        {
            var pos = updateProperties.Pos;
            var direction = updateProperties.BlockData.BlockPositionInfo.BlockDirection;
            var blockId = updateProperties.BlockData.Block.BlockId;
            
            var messagePack = new PlaceBlockEventMessagePack(pos, blockId, (int)direction);
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
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
        
        [Key(0)] public BlockDataMessagePack BlockData { get; set; }
    }
    
    
    [MessagePackObject]
    public class BlockDataMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlockDataMessagePack()
        {
        }
        
        public BlockDataMessagePack(int blockId, Vector3Int blockPos, BlockDirection blockDirection)
        {
            BlockId = blockId;
            BlockPos = new Vector3IntMessagePack(blockPos);
            Direction = (int)blockDirection;
        }
        
        [Key(0)] public int BlockId { get; set; }
        
        [Key(1)] public Vector3IntMessagePack BlockPos { get; set; }
        
        [Key(2)] public int Direction { get; set; }
        
        [IgnoreMember] public BlockDirection BlockDirection => (BlockDirection)Direction;
    }
}