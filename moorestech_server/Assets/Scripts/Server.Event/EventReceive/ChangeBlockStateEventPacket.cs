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
            var messagePack = new BlockStateMessagePack(state.state, state.blockData.BlockPositionInfo.OriginalPos);
            var payload = MessagePackSerializer.Serialize(messagePack);
            
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
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
            if (!CurrentStateDetail.TryGetValue(stateKey, out var bytes))
            {
                return default;
            }
            return MessagePackSerializer.Deserialize<TBlockState>(bytes);
        }
        
        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
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