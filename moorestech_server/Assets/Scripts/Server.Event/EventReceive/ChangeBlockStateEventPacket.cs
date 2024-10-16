using System;
using System.Collections.Generic;
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
        
        private void ChangeState((BlockState state, WorldBlockData blockData) state)
        {
            var messagePack = new ChangeBlockStateMessagePack(state.state, state.blockData.BlockPositionInfo.OriginalPos);
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
    
    [MessagePackObject]
    public class ChangeBlockStateMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ChangeBlockStateMessagePack()
        {
        }
        
        public ChangeBlockStateMessagePack(BlockState state, Vector3Int pos)
        {
            CurrentState = state.CurrentState;
            PreviousState = state.PreviousState;
            
            CurrentStateDetail = state.CurrentStateDetail;
            Position = new Vector3IntMessagePack(pos);
        }
        
        [Key(0)] public string CurrentState { get; set; }
        
        [Key(1)] public string PreviousState { get; set; }
        
        [Key(2)] public Dictionary<string,byte[]> CurrentStateDetail { get; set; }
        
        [Key(3)] public Vector3IntMessagePack Position { get; set; }
        
        public TBlockState GetStateDetail<TBlockState>(string stateKey)
        {
            return MessagePackSerializer.Deserialize<TBlockState>(CurrentStateDetail[stateKey]);
        }
    }
}