using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Game.Context;
using Game.World.Interface.DataStore;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

namespace Server.Event.EventReceive
{
    public class ChangeBlockStateEventPacket
    {
        public const string EventTag = "va:event:changeBlockState";
        
        private readonly EventProtocolProvider _eventProtocolProvider;
        
        public ChangeBlockStateEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            ServerContext.WorldBlockDatastore.OnBlockStateChange.Subscribe(ChangeState);
        }
        
        public void ChangeState((BlockState state, WorldBlockData blockData) state)
        {
            var messagePack = new BlockStateMessagePack(state.state, state.blockData.BlockPositionInfo.OriginalPos);
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            var eventTag = CreateSpecifiedBlockEventTag(state.blockData.BlockPositionInfo);
            _eventProtocolProvider.AddBroadcastEvent(eventTag, payload);
        }
        
        public static string CreateSpecifiedBlockEventTag(BlockPositionInfo posInfo)
        {
            return $"{EventTag}:{posInfo.OriginalPos}";
        }
    }
    
    [MessagePackObject]
    public class BlockStateMessagePack
    {
        /// <summary>
        /// key Component key, value Component state
        /// </summary>
        [Key(0)] public Dictionary<string,byte[]> CurrentStateDetail { get; set; }
        
        [Key(1)] public Vector3IntMessagePack Position { get; set; } // TODO ここをinstanceIdに変更する？
        
        public TBlockState GetStateDetail<TBlockState>(string stateKey)
        {
            return CurrentStateDetail.GetStateDetail<TBlockState>(stateKey);
        }
        
        [Obsolete("This constructor is for deserialization. Do not use directly.")]
        public BlockStateMessagePack()
        {
        }
        
        public BlockStateMessagePack(BlockState state, Vector3Int pos)
        {
            CurrentStateDetail = state.CurrentStateDetails;
            Position = new Vector3IntMessagePack(pos);
        }

    }
}