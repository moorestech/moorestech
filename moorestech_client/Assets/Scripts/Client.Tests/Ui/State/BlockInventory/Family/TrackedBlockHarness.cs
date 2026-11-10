using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Tests.Common;
using Client.Ui.State.BlockDetail;
using Game.Block.Interface;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using Server.Event.EventReceive;
using UniRx;
using UnityEngine;

namespace Client.Tests.Ui.State.BlockInventory.Family
{
    /// <summary>
    /// 追跡ブロックの試験用の組み立て。本物の追跡（UI 状態・データストア）を使わず、追跡中のブロックと 2 つの通知を手で動かす
    /// Test rig for the tracked block: moves the tracked block and its two notifications by hand without the real tracking (UI state, datastore)
    /// </summary>
    internal sealed class TrackedBlockHarness
    {
        // 追跡の依存は使わない（Initialize を呼ばないので触れられない）
        // The tracker's dependencies are never touched since Initialize is not called
        public readonly OpenBlockDetailTracker Tracker = new(null, null, null, null);
        private readonly List<GameObject> _roots = new();

        // 状態詳細（鍵とシリアライズ済みの値）を持つブロックを作る
        // Creates a block carrying state details (key and serialized value)
        public BlockGameObject CreateBlock(Vector3Int position, BlockMasterElement master, params (string key, object detail)[] details)
        {
            var root = new GameObject("tracked-block");
            _roots.Add(root);
            var block = root.AddComponent<BlockGameObject>();
            var states = new Dictionary<string, byte[]>();
            foreach (var (key, detail) in details) states[key] = MessagePackSerializer.Serialize(detail.GetType(), detail);
            TestReflection.SetField(block, "<BlockPosInfo>k__BackingField", new BlockPositionInfo(position, BlockDirection.North, Vector3Int.one));
            TestReflection.SetField(block, "<BlockMasterElement>k__BackingField", master);
            SetStates(block, states);
            return block;
        }

        public void ReplaceStates(BlockGameObject block, params (string key, object detail)[] details)
        {
            var states = new Dictionary<string, byte[]>();
            foreach (var (key, detail) in details) states[key] = MessagePackSerializer.Serialize(detail.GetType(), detail);
            SetStates(block, states);
        }

        // 追跡替え（null は閉）
        // Track a different block (null means closed)
        public void Track(BlockGameObject block)
        {
            TestReflection.SetField(Tracker, "<TrackedBlock>k__BackingField", block);
            TestReflection.GetField<Subject<BlockGameObject>>(Tracker, "_onTrackedBlockChanged").OnNext(block);
        }

        public void NotifyStateChanged()
        {
            TestReflection.GetField<Subject<Unit>>(Tracker, "_onTrackedBlockStateChanged").OnNext(Unit.Default);
        }

        public void Dispose()
        {
            foreach (var root in _roots) Object.DestroyImmediate(root);
            _roots.Clear();
        }

        private static void SetStates(BlockGameObject block, Dictionary<string, byte[]> states)
        {
#pragma warning disable CS0618
            TestReflection.SetField(block, "_blockStateMessagePack", new BlockStateMessagePack { CurrentStateDetail = states });
#pragma warning restore CS0618
        }
    }
}
