using System.Collections.Generic;
using System.Linq;
using Core.Item;
using Core.Master;
using Core.Update;
using Game.Block.Blocks.BeltConveyor.Sync.Message;
using Game.Block.Blocks.BeltConveyor.Sync.State;
using Game.Block.Blocks.BeltConveyor.Transport;
using Game.Block.Interface;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event;
using Server.Event.EventReceive;
using Server.Event.EventReceive.BeltTransportSync;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.Util.BeltWorldTestUtil;

namespace Tests.CombinedTest.Server.PacketTest.Event.BeltTransportSync
{
    // ベルト同期パケットのテスト用に、機械が押し込むワールドの構築とイベントの仕分け・連番の検査をまとめる
    // Builds a machine-fed belt world for the belt sync packet tests and classifies events and checks sequence ids
    internal static class BeltSyncPacketTestUtil
    {
        internal const int WarmUpTicks = 8;
        private static readonly ItemId ItemA = new(1);

        internal static ServiceProvider CreateServer()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            return serviceProvider;
        }

        // チェスト→4マスの直線→チェストと、チェスト→ベルコン→分配器(前はベルコン→チェスト・左はチェスト・右は行き止まり)を置き、組を作って流し始める
        // Place chest -> four-cell line -> chest and chest -> belt -> splitter (front belt -> chest, left chest, right dead end), then tick so the assembly is built and items flow
        internal static void BuildMachineFedWorldAndWarmUp()
        {
            var maxStack = ItemStackLevelDataStore.Instance.GetMaxStack(ItemA);
            var sourceLine = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, -1), BlockDirection.North));
            for (var z = 0; z <= 3; z++) Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(0, 0, z), BlockDirection.North);
            Place(ForUnitTestModBlockId.ChestId, new Vector3Int(0, 0, 4), BlockDirection.North);

            var sourceBranch = Inventory(Place(ForUnitTestModBlockId.ChestId, new Vector3Int(20, 0, -2), BlockDirection.North));
            Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(20, 0, -1), BlockDirection.North);
            Place(ForUnitTestModBlockId.GearBeltConveyorSplitter, new Vector3Int(20, 0, 0), BlockDirection.North);
            Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(20, 0, 1), BlockDirection.North);
            Place(ForUnitTestModBlockId.ChestId, new Vector3Int(20, 0, 2), BlockDirection.North);
            Place(ForUnitTestModBlockId.ChestId, new Vector3Int(19, 0, 0), BlockDirection.North);
            Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(21, 0, 0), BlockDirection.East);

            sourceLine.SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, maxStack));
            sourceBranch.SetItem(0, ServerContext.ItemStackFactory.Create(ItemA, maxStack));
            for (var i = 0; i < WarmUpTicks; i++) GameUpdater.UpdateOneTick();
        }

        // 分配器の右の行き止まりをx方向へ1マス延ばす。設置で組が汚れ、次のtick先頭で作り直される
        // Extend the splitter's right dead end by one cell along x; the placement dirties the assembly, rebuilt at the next tick head
        internal static void ExtendDeadEnd(int x)
        {
            Assert.IsNotNull(Place(ForUnitTestModBlockId.BeltConveyorId, new Vector3Int(x, 0, 0), BlockDirection.East), $"placed belt at x={x}");
        }

        internal static BeltTransportFullState CaptureServer()
        {
            return BeltTransportFullStateCapture.Capture(ServerContext.GetService<BeltTransportDatastore>().Assembly);
        }

        internal static bool IsBeltTickEvent(EventMessagePack eventMessagePack)
        {
            return eventMessagePack.Tag == BeltTransportTickEventPacket.TickDiffEventTag || eventMessagePack.Tag == BeltTransportTickEventPacket.RebuiltFullStateEventTag;
        }

        // 連番を消費したイベントから(tick, 連番)を集める。列車の束はhash(n-1)とdiff(n)の2つ、ベルトは1つ
        // Collect (tick, sequence id) from sequence-consuming events; a train bundle has two (hash n-1, diff n), a belt event one
        internal static void CollectSequenceIds(IEnumerable<EventMessagePack> events, Dictionary<uint, List<uint>> sequenceIdsByTick)
        {
            foreach (var eventMessagePack in events)
            {
                if (eventMessagePack.Tag == TrainUnitTickDiffBundleEventPacket.EventTag)
                {
                    var bundle = MessagePackSerializer.Deserialize<TrainUnitTickDiffBundleMessagePack>(eventMessagePack.Payload);
                    Add(bundle.ServerTick - 1, bundle.HashTickSequenceId);
                    Add(bundle.ServerTick, bundle.DiffTickSequenceId);
                }
                else if (eventMessagePack.Tag == BeltTransportTickEventPacket.TickDiffEventTag)
                {
                    var diff = MessagePackSerializer.Deserialize<BeltTransportTickDiffMessagePack>(eventMessagePack.Payload);
                    Add(diff.ServerTick, diff.TickSequenceId);
                }
                else if (eventMessagePack.Tag == BeltTransportTickEventPacket.RebuiltFullStateEventTag)
                {
                    var full = MessagePackSerializer.Deserialize<BeltTransportFullStateMessagePack>(eventMessagePack.Payload);
                    Add(full.ServerTick, full.TickSequenceId);
                }
            }

            #region Internal

            void Add(uint tick, uint sequenceId)
            {
                if (!sequenceIdsByTick.TryGetValue(tick, out var ids)) sequenceIdsByTick[tick] = ids = new List<uint>();
                ids.Add(sequenceId);
            }

            #endregion
        }

        // 最初と最後のtickは一部しか観測していないので除き、残りの各tickの連番が欠番・重複なく1..maxを覆うか確かめる。返り値は検査したtick数
        // Skip the partially observed first and last ticks and check every other tick's ids cover 1..max with no gap or duplicate; returns the number of ticks checked
        internal static int AssertNoSequenceGaps(Dictionary<uint, List<uint>> sequenceIdsByTick, int expectedIdsPerTick)
        {
            var ticks = sequenceIdsByTick.Keys.OrderBy(t => t).ToList();
            for (var i = 1; i < ticks.Count - 1; i++)
            {
                var ids = sequenceIdsByTick[ticks[i]].OrderBy(id => id).ToList();
                var expected = Enumerable.Range(1, ids.Count).Select(id => (uint)id).ToList();
                CollectionAssert.AreEqual(expected, ids, $"sequence ids of tick {ticks[i]}: {string.Join(",", ids)}");
                Assert.AreEqual(expectedIdsPerTick, ids.Count, $"sequence ids per tick at tick {ticks[i]}");
            }
            for (var i = 1; i < ticks.Count; i++) Assert.AreEqual(ticks[i - 1] + 1, ticks[i], "observed ticks are consecutive");
            return ticks.Count - 2;
        }
    }
}
