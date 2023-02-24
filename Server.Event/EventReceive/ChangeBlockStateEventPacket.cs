using System;
using System.Linq;
using Core.Block.Blocks;
using Core.Item;
using Game.World.Interface.DataStore;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Event.EventReceive
{
    public class ChangeBlockStateEventPacket
    {
        public const string EventTag = "va:event:changeBlockState";
        
        private readonly EventProtocolProvider _eventProtocolProvider;

        public ChangeBlockStateEventPacket(EventProtocolProvider eventProtocolProvider, IWorldBlockDatastore worldBlockDatastore)
        {
            _eventProtocolProvider = eventProtocolProvider;
            worldBlockDatastore.OnBlockStateChange += ChangeState;
        }

        private void ChangeState((ChangedBlockState state,IBlock block,int x,int y) state)
        {
            var payload = MessagePackSerializer.Serialize(
                new ChangeBlockStateEventMessagePack(state.state,  state.x, state.y)).ToList();
            
            _eventProtocolProvider.AddBroadcastEvent(payload);
        }
    }
    
    [MessagePackObject(keyAsPropertyName :true)]
    public class ChangeBlockStateEventMessagePack : EventProtocolMessagePackBase
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ChangeBlockStateEventMessagePack() { }
        
        public string CurrentState { get; set; }
        public string PreviousState { get; set; }
        public byte[] CurrentStateData { get; set; }
        public Vector2MessagePack Position { get; set; }
        
        public ChangeBlockStateEventMessagePack(ChangedBlockState state,int x,int y)
        {
            EventTag = ChangeBlockStateEventPacket.EventTag;
            CurrentState = state.CurrentState;
            PreviousState = state.PreviousState;
            CurrentStateData = state.CurrentStateData;
            Position = new Vector2MessagePack(x,y);
        }
    }
}