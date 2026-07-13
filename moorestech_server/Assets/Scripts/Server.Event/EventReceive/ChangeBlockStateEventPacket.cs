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

        // 位置ごとの最終送信ペイロード（差分検知用）
        // Last sent payload per position, for diff detection
        private readonly Dictionary<Vector3Int, byte[]> _lastBroadcastPayloads = new();

        public ChangeBlockStateEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            ServerContext.WorldBlockDatastore.OnBlockStateChange.Subscribe(ChangeState);
            ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(OnBlockRemove);
        }

        // 購読経路用。前回ブロードキャストしたペイロードとバイト列一致なら送信をスキップする
        // Subscription path: skip the broadcast when the payload byte-matches the last one sent
        public void ChangeState((BlockState state, WorldBlockData blockData) state)
        {
            var pos = state.blockData.BlockPositionInfo.OriginalPos;
            var payload = SerializePayload(state.state, pos);

            if (_lastBroadcastPayloads.TryGetValue(pos, out var lastPayload) && payload.AsSpan().SequenceEqual(lastPayload)) return;

            _lastBroadcastPayloads[pos] = payload;
            Broadcast(state.blockData.BlockPositionInfo, payload);
        }

        // InvokeBlockStateEventProtocol専用。初期状態pull経路のため差分判定なしで必ず送信する
        // For InvokeBlockStateEventProtocol only: always broadcasts without diffing, for the initial-state pull path
        public void ForceChangeState((BlockState state, WorldBlockData blockData) state)
        {
            var pos = state.blockData.BlockPositionInfo.OriginalPos;
            var payload = SerializePayload(state.state, pos);

            _lastBroadcastPayloads[pos] = payload;
            Broadcast(state.blockData.BlockPositionInfo, payload);
        }

        public static string CreateSpecifiedBlockEventTag(BlockPositionInfo posInfo)
        {
            return $"{EventTag}:{posInfo.OriginalPos}";
        }

        // ブロック削除時に差分記録を掃除する。放置すると同座標に再設置された別ブロックが旧ペイロードと誤って比較されうる
        // Clean up the diff record on removal; otherwise a block re-placed at the same position could be wrongly diffed against stale state
        private void OnBlockRemove(BlockRemoveProperties properties)
        {
            _lastBroadcastPayloads.Remove(properties.BlockData.BlockPositionInfo.OriginalPos);
        }

        private static byte[] SerializePayload(BlockState blockState, Vector3Int pos)
        {
            var messagePack = new BlockStateMessagePack(blockState, pos);
            return MessagePackSerializer.Serialize(messagePack);
        }

        private void Broadcast(BlockPositionInfo posInfo, byte[] payload)
        {
            var eventTag = CreateSpecifiedBlockEventTag(posInfo);
            _eventProtocolProvider.AddBroadcastEvent(eventTag, payload);
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