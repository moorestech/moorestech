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
    // 座標別の最新状態をtick末尾に束ねて送信
    // Keeps the latest block state per position and broadcasts them as one bundled event at tick end (ADR 0088)
    public class ChangeBlockStateEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:blockStatesTickBundle";

        private readonly EventProtocolProvider _eventProtocolProvider;

        // 位置ごとの最後に積んだペイロード（差分検知用）
        // Last queued payload per position, for diff detection
        private readonly Dictionary<Vector3Int, byte[]> _lastQueuedPayloads = new();

        // tick末尾の送信まで溜める、座標ごとの最新状態
        // Latest state per position, held until the tick-end broadcast
        private readonly Dictionary<Vector3Int, BlockStateMessagePack> _pendingStates = new();

        public ChangeBlockStateEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
        }

        public void Load()
        {
            ServerContext.WorldBlockDatastore.OnBlockStateChange.Subscribe(QueueChangedState);
            ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(OnBlockRemove);
            ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnBlockPlace);
        }

        // 購読経路用。前回と同一ペイロードなら溜めない
        // Subscription path: skip when the payload matches the last queued
        public void QueueChangedState((BlockState state, WorldBlockData blockData) state)
        {
            var (pos, messagePack, payload) = SerializeState(state);

            if (_lastQueuedPayloads.TryGetValue(pos, out var lastPayload) && payload.AsSpan().SequenceEqual(lastPayload)) return;

            QueueState(pos, messagePack, payload);
        }

        // 初期pull経路専用。差分スキップ禁止
        // Initial-pull path only: diff-skip is forbidden
        public void QueueStateForInitialPull((BlockState state, WorldBlockData blockData) state)
        {
            var (pos, messagePack, payload) = SerializeState(state);
            QueueState(pos, messagePack, payload);
        }

        // tick末尾に1回呼ぶ。変化0件なら送らない
        // Called once at tick end by MasterTickUpdater; nothing is sent for a tick without changes
        public void BroadcastPendingStates()
        {
            if (_pendingStates.Count == 0) return;

            var states = new BlockStateMessagePack[_pendingStates.Count];
            _pendingStates.Values.CopyTo(states, 0);
            _pendingStates.Clear();

            var payload = MessagePackSerializer.Serialize(new BlockStatesTickBundleMessagePack(states));
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }

        // ブロック削除時に差分記録と溜めた状態を掃除する。放置すると同座標に再設置された別ブロックへ旧状態が届きうる
        // Clean up the diff record and the pending state on removal; otherwise a block re-placed at the same position could receive stale state
        private void OnBlockRemove(BlockRemoveProperties properties)
        {
            ForgetPosition(properties.BlockData.BlockPositionInfo.OriginalPos);
        }

        // Destroy中発火でstale再登録された場合に備え、設置時にも同座標の記録を掃除する
        // Also clear on placement in case Destroy() re-registers a stale entry at this position
        private void OnBlockPlace(BlockPlaceProperties properties)
        {
            ForgetPosition(properties.BlockData.BlockPositionInfo.OriginalPos);
        }

        // 座標・送る形・差分判定用のバイト列を1か所で作る
        // Builds the position, the message and the diff bytes in one place
        private static (Vector3Int pos, BlockStateMessagePack messagePack, byte[] payload) SerializeState((BlockState state, WorldBlockData blockData) state)
        {
            var pos = state.blockData.BlockPositionInfo.OriginalPos;
            var messagePack = new BlockStateMessagePack(state.state, pos);
            return (pos, messagePack, MessagePackSerializer.Serialize(messagePack));
        }

        // 差分記録と溜めた状態は必ず一緒に更新する
        // The diff record and the pending state are always updated together
        private void QueueState(Vector3Int pos, BlockStateMessagePack messagePack, byte[] payload)
        {
            _lastQueuedPayloads[pos] = payload;
            _pendingStates[pos] = messagePack;
        }

        private void ForgetPosition(Vector3Int pos)
        {
            _lastQueuedPayloads.Remove(pos);
            _pendingStates.Remove(pos);
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

    // 1tick分の状態の束。座標ごとに最新1件のみ
    // One tick's bundle of block states; holds only the latest entry per position
    [MessagePackObject]
    public class BlockStatesTickBundleMessagePack
    {
        [Key(0)] public BlockStateMessagePack[] States { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public BlockStatesTickBundleMessagePack()
        {
        }

        public BlockStatesTickBundleMessagePack(BlockStateMessagePack[] states)
        {
            States = states;
        }
    }
}
