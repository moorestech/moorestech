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
    public class PlaceBlockEventPacket : IPostLoadEventReceiver
    {
        public const string EventTag = "va:event:blockPlace";
        private readonly EventProtocolProvider _eventProtocolProvider;
        
        // 生成タイミングでOnBlockPlaceEventを購読する。初期ロード完了後に生成されるため、ロード中の設置は配信されない
        // Subscribes on construction; it is created after initial load completes, so load-time placements are not broadcast
        public PlaceBlockEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnPlaceBlock);
        }

        private void OnPlaceBlock(BlockPlaceProperties updateProperties)
        {
            var pos = updateProperties.Pos;
            var direction = updateProperties.BlockData.BlockPositionInfo.BlockDirection;
            var blockId = updateProperties.BlockData.Block.BlockId;
            var blockInstanceId = updateProperties.BlockData.Block.BlockInstanceId;
            
            var messagePack = new PlaceBlockEventMessagePack(pos, blockId, direction, blockInstanceId);
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
        
        public PlaceBlockEventMessagePack(Vector3Int blockPos, BlockId blockId, BlockDirection direction, BlockInstanceId blockInstanceId)
        {
            BlockData = new BlockDataMessagePack(blockId, blockPos, direction, blockInstanceId);
        }
    }
    
    
    [MessagePackObject]
    public class BlockDataMessagePack
    {
        [Key(0)] public int BlockIdInt { get; set; }
        [Key(1)] public Vector3IntMessagePack BlockPos { get; set; }
        [Key(2)] public int Direction { get; set; }
        [Key(3)] public int BlockInstanceIdInt { get; set; }
        
        [IgnoreMember] public BlockDirection BlockDirection => (BlockDirection)Direction;
        [IgnoreMember] public BlockId BlockId => (BlockId)BlockIdInt;
        [IgnoreMember] public BlockInstanceId BlockInstanceId => new(BlockInstanceIdInt);
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlockDataMessagePack() { }
        
        public BlockDataMessagePack(BlockId blockId, Vector3Int blockPos, BlockDirection blockDirection, BlockInstanceId blockInstanceId)
        {
            BlockIdInt = (int)blockId;
            BlockPos = new Vector3IntMessagePack(blockPos);
            Direction = (int)blockDirection;
            BlockInstanceIdInt = blockInstanceId.AsPrimitive();
        }
    }
}