using System;
using System.Threading;
using Core.Master;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using Server.Boot.Loop.PacketProcessing;
using UnityEngine;

namespace Client.Tests.EditModeInPlayingTest
{
    // 実サーバーのtick末尾で世界を変更し、各段階の接続を値として持ち帰る。
    // Mutate the live world at tick end and carry each connection snapshot back as values.
    internal sealed class BeltConnectionWorldTestEntry : ITickEndPacketEntry
    {
        private static readonly Vector3Int LowerSource = Vector3Int.zero;
        private static readonly Vector3Int UpperSource = Vector3Int.up;
        private static readonly Vector3Int LowerTarget = Vector3Int.forward;
        private static readonly Vector3Int UpperTarget = Vector3Int.up + Vector3Int.forward;

        private readonly BlockId _flatId;
        private readonly BlockId _upId;
        private readonly BlockId _downId;
        private int _completed;

        internal const int UpperToUpper = 1;
        internal const int UpperToLower = 2;
        internal const int LowerToUpper = 4;
        internal const int LowerToLower = 8;
        internal const int AllBlocks = 15;
        internal const int WithoutUpperSource = 14;

        internal string Failure { get; private set; }
        internal int InitiallyConnected { get; private set; }
        internal int AfterRemovalConnected { get; private set; }
        internal int AfterReplacementConnected { get; private set; }
        internal int InitiallyPresent { get; private set; }
        internal int AfterRemovalPresent { get; private set; }
        internal int AfterReplacementPresent { get; private set; }
        internal bool IsCompleted => Volatile.Read(ref _completed) != 0;
        public bool IsActive => true;

        internal BeltConnectionWorldTestEntry(BlockId flatId, BlockId upId, BlockId downId)
        {
            _flatId = flatId;
            _upId = upId;
            _downId = downId;
        }

        public void Process()
        {
            var world = ServerContext.WorldBlockDatastore;

            // 下段を先に置き、上段の候補が接続を奪う順序を通す。
            // Place the lower row first so the upper candidates displace its connection.
            if (!Place(_upId, LowerSource, "lower Up source")) return;
            if (!Place(_downId, LowerTarget, "lower Down target")) return;
            if (!Place(_flatId, UpperSource, "upper Flat source")) return;
            if (!Place(_flatId, UpperTarget, "upper Flat target")) return;
            InitiallyPresent = Presence();
            InitiallyConnected = Connections();

            // 下段→上段の再選択を記録。
            // Record lower-to-upper reselection after removal.
            if (!world.RemoveBlock(UpperSource, BlockRemoveReason.ManualRemove))
            {
                FinishWithFailure("remove upper Flat source");
                return;
            }
            AfterRemovalPresent = Presence();
            AfterRemovalConnected = Connections();

            // 上段再設置後の復帰を記録。
            // Record restoration after replacing the upper source.
            if (!Place(_flatId, UpperSource, "replace upper Flat source")) return;
            AfterReplacementPresent = Presence();
            AfterReplacementConnected = Connections();
            Volatile.Write(ref _completed, 1);

            #region Internal

            bool Place(BlockId id, Vector3Int position, string label)
            {
                if (world.TryAddBlock(id, position, BlockDirection.North,
                        Array.Empty<BlockCreateParam>(), out _)) return true;
                FinishWithFailure("place " + label);
                return false;
            }

            void FinishWithFailure(string operation)
            {
                Failure = operation;
                Volatile.Write(ref _completed, 1);
            }

            int Presence()
            {
                var mask = 0;
                if (world.Exists(UpperSource)) mask |= 1;
                if (world.Exists(LowerSource)) mask |= 2;
                if (world.Exists(UpperTarget)) mask |= 4;
                if (world.Exists(LowerTarget)) mask |= 8;
                return mask;
            }

            int Connections()
            {
                var upperSource = world.GetBlock(UpperSource);
                var lowerSource = world.GetBlock(LowerSource);
                var upperTarget = world.GetBlock(UpperTarget);
                var lowerTarget = world.GetBlock(LowerTarget);
                var mask = 0;

                // 実World上のsourceだけを照会し、撤去済みオブジェクトは判定から外す。
                // Query only sources in the live world, excluding removed block objects.
                if (upperSource != null)
                {
                    var targets = upperSource.GetComponent<BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>>()
                        .ConnectedTargets;
                    if (upperTarget != null && targets.ContainsKey(upperTarget.GetComponent<SegmentBeltComponent>()))
                        mask |= UpperToUpper;
                    if (lowerTarget != null && targets.ContainsKey(lowerTarget.GetComponent<SegmentBeltComponent>()))
                        mask |= UpperToLower;
                }
                if (lowerSource != null)
                {
                    var targets = lowerSource.GetComponent<BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>>()
                        .ConnectedTargets;
                    if (upperTarget != null && targets.ContainsKey(upperTarget.GetComponent<SegmentBeltComponent>()))
                        mask |= LowerToUpper;
                    if (lowerTarget != null && targets.ContainsKey(lowerTarget.GetComponent<SegmentBeltComponent>()))
                        mask |= LowerToLower;
                }
                return mask;
            }

            #endregion
        }
    }
}
