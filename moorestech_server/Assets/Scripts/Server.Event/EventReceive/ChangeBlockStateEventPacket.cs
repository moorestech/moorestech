using System;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Game.World.Interface.DataStore;
using MessagePack;
using Newtonsoft.Json;
using Server.Util.MessagePack;

namespace Server.Event.EventReceive
{
    public class ChangeBlockStateEventPacket
    {
        public const string EventTag = "va:event:changeBlockState";

        private readonly EventProtocolProvider _eventProtocolProvider;

        public ChangeBlockStateEventPacket(EventProtocolProvider eventProtocolProvider,
            IWorldBlockDatastore worldBlockDatastore)
        {
            _eventProtocolProvider = eventProtocolProvider;
            worldBlockDatastore.OnBlockStateChange += ChangeState;
        }

        private void ChangeState((ChangedBlockState state, IBlock block, int x, int y) state)
        {
            var messagePack = new ChangeBlockStateEventMessagePack(state.state, state.x, state.y);
            var payload = MessagePackSerializer.Serialize(messagePack);

            _eventProtocolProvider.AddBroadcastEvent(EventTag,payload);
        }
    }

    [MessagePackObject]
    public class ChangeBlockStateEventMessagePack
    {
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ChangeBlockStateEventMessagePack()
        {
        }

        public ChangeBlockStateEventMessagePack(ChangedBlockState state, int x, int y)
        {
            CurrentState = state.CurrentState;
            PreviousState = state.PreviousState;

            CurrentStateJsonData = state.CurrentStateJsonData;
            Position = new Vector2IntMessagePack(x, y);
        }

        [Key(0)]
        public string CurrentState { get; set; }
        [Key(1)]
        public string PreviousState { get; set; }
        [Key(2)]
        public string CurrentStateJsonData { get; set; }
        [Key(3)]
        public Vector2IntMessagePack Position { get; set; }

        public TBlockState GetStateDat<TBlockState>()
        {
            return JsonConvert.DeserializeObject<TBlockState>(CurrentStateJsonData);
        }
    }
}