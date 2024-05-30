using System;
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
        
        private void ChangeState((ChangedBlockState state, WorldBlockData blockData) state)
        {
            var messagePack = new ChangeBlockStateEventMessagePack(state.state, state.blockData.BlockPositionInfo.OriginalPos);
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }
    
    [MessagePackObject]
    public class ChangeBlockStateEventMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ChangeBlockStateEventMessagePack()
        {
        }
        
        public ChangeBlockStateEventMessagePack(ChangedBlockState state, Vector3Int pos)
        {
            CurrentState = state.CurrentState;
            PreviousState = state.PreviousState;
            
            CurrentStateData = state.CurrentStateData;
            Position = new Vector3IntMessagePack(pos);
        }
        
        [Key(0)] public string CurrentState { get; set; }
        
        [Key(1)] public string PreviousState { get; set; }
        
        [Key(2)] public byte[] CurrentStateData { get; set; }
        
        [Key(3)] public Vector3IntMessagePack Position { get; set; }
        
        public TBlockState GetStateData<TBlockState>()
        {
            return MessagePackSerializer.Deserialize<TBlockState>(CurrentStateData);
        }
    }
}