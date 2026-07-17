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
    public class ChangeBlockStateEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:changeBlockState";

        private readonly EventProtocolProvider _eventProtocolProvider;

        // 位置ごとの最終送信ペイロード（差分検知用）
        // Last sent payload per position, for diff detection
        private readonly Dictionary<Vector3Int, byte[]> _lastBroadcastPayloads = new();

        public ChangeBlockStateEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
        }

        public void Load()
        {
            ServerContext.WorldBlockDatastore.OnBlockStateChange.Subscribe(ChangeState);
            ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(OnBlockRemove);
            ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlace);
        }

        // 購読経路用。前回と同一ペイロードなら送信スキップ
        // Subscription path: skip when the payload matches the last sent
        public void ChangeState((BlockState state, WorldBlockData blockData) state)
        {
            var pos = state.blockData.BlockPositionInfo.OriginalPos;
            var payload = SerializePayload(state.state, pos);

            if (_lastBroadcastPayloads.TryGetValue(pos, out var lastPayload) && payload.AsSpan().SequenceEqual(lastPayload)) return;

            _lastBroadcastPayloads[pos] = payload;
            Broadcast(state.blockData.BlockPositionInfo, payload);
        }

        // 初期pull経路専用。差分スキップ禁止
        // Initial-pull path only: diff-skip is forbidden
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

        // Destroy中発火でstale再登録された場合に備え、設置時にも同座標の記録を掃除する
        // Also clear on placement in case Destroy() re-registers a stale entry at this position
        private void OnBlockPlace(BlockPlaceProperties properties)
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