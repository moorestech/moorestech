using System;
using Core.Master;
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
            
            var messagePack = new PlaceBlockEventMessagePack(pos, blockId, direction);
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
    
    
    [MessagePackObject]
    public class PlaceBlockEventMessagePack
    {
        [Key(0)] public BlockDataMessagePack BlockData { get; set; }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public PlaceBlockEventMessagePack()
        {
        }
        
        public PlaceBlockEventMessagePack(Vector3Int blockPos, BlockId blockId, BlockDirection direction)
        {
            BlockData = new BlockDataMessagePack(blockId, blockPos, direction);
        }
    }
    
    
    [MessagePackObject]
    public class BlockDataMessagePack
    {
        [Key(0)] public int BlockId { get; set; }
        [Key(1)] public Vector3IntMessagePack BlockPos { get; set; }
        [Key(2)] public int Direction { get; set; }
        
        
        [IgnoreMember] public BlockDirection BlockDirection => (BlockDirection)Direction;
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlockDataMessagePack() { }
        
        public BlockDataMessagePack(BlockId blockId, Vector3Int blockPos, BlockDirection blockDirection)
        {
            BlockId = (int)blockId;
            BlockPos = new Vector3IntMessagePack(blockPos);
            Direction = (int)blockDirection;
        }
    }
}