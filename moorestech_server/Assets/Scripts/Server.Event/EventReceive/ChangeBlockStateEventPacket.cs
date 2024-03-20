using System;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Game.World.Interface.DataStore;
using MessagePack;
using Newtonsoft.Json;
using Server.Util.MessagePack;
using UniRx;
using UnityEngine;

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
            worldBlockDatastore.OnBlockStateChange.Subscribe(ChangeState);
        }

        private void ChangeState((ChangedBlockState state, WorldBlockData blockData) state)
        {
            var messagePack = new ChangeBlockStateEventMessagePack(state.state, state.blockData.OriginalPos);
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

        public ChangeBlockStateEventMessagePack(ChangedBlockState state, Vector3Int pos)
        {
            CurrentState = state.CurrentState;
            PreviousState = state.PreviousState;

            CurrentStateJsonData = state.CurrentStateJsonData;
            Position = new Vector3IntMessagePack(pos);
        }

        [Key(0)]
        public string CurrentState { get; set; }
        [Key(1)]
        public string PreviousState { get; set; }
        [Key(2)]
        public string CurrentStateJsonData { get; set; }
        [Key(3)]
        public Vector3IntMessagePack Position { get; set; }

        public TBlockState GetStateDat<TBlockState>()
        {
            return JsonConvert.DeserializeObject<TBlockState>(CurrentStateJsonData);
        }
    }
}