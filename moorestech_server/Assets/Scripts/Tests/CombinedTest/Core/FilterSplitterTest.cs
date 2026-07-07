using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Blocks.FilterSplitter;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.InventoryConnectsModule;
using NUnit.Framework;
using Server.Boot;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;
using Game.Block.Interface.Component.ConnectJudge;

namespace Tests.CombinedTest.Core
{
    /// <summary>
    /// FilterSplitter ブロックのルーティング・フィルター・永続化を検証する。
    /// Verifies routing, filtering, and persistence of the FilterSplitter block.
    /// </summary>
    public class FilterSplitterTest
    {
        [Test]
        public void DefaultModeCatchAllTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (_, component, dummies) = CreateSplitterWithDummies(new BlockInstanceId(1));

            // 初期値は Whitelist のため、全方向を明示的に Default に設定する
            // Initial mode is Whitelist, so explicitly set every direction to Default
            for (var d = 0; d < component.DirectionCount; d++) component.SetMode(d, FilterSplitterMode.Default);

            // 全方向 Default で 3 アイテム挿入 → 各方向に 1 個ずつラウンドロビン
            // All directions Default; 3 inserts should round-robin one item each
            for (var i = 0; i < 3; i++)
            {
                var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
                var remain = component.InsertItem(stack, InsertItemContext.Empty);
                Assert.AreEqual(0, remain.Count);
            }
            component.Update();

            foreach (var dummy in dummies)
            {
                Assert.AreEqual(1, TotalCount(dummy));
            }
        }

        [Test]
        public void WhitelistPriorityOverDefaultTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (_, component, dummies) = CreateSplitterWithDummies(new BlockInstanceId(2));

            // dir0 を Whitelist[ItemId1]、dir1/dir2 は Default
            // dir0 = Whitelist[ItemId1], dir1/dir2 = Default
            component.SetMode(0, FilterSplitterMode.Whitelist);
            component.SetFilterItem(0, 0, ForUnitTestItemId.ItemId1);

            // ItemId1 を 10 個挿入 → 全て dir0 へ集中（Default は fallback）
            // Insert ItemId1 ten times; all should land in dir0 (Default is only fallback)
            for (var i = 0; i < 10; i++)
            {
                var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
                component.InsertItem(stack, InsertItemContext.Empty);
                component.Update();
            }

            Assert.AreEqual(10, TotalCount(dummies[0]));
            Assert.AreEqual(0, TotalCount(dummies[1]));
            Assert.AreEqual(0, TotalCount(dummies[2]));
        }

        [Test]
        public void BlacklistRoutingTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (_, component, dummies) = CreateSplitterWithDummies(new BlockInstanceId(3));

            // dir0 を Blacklist[ItemId1]、dir1/dir2 は Default（初期値 Whitelist から明示変更）
            // dir0 = Blacklist[ItemId1], dir1/dir2 = Default (explicitly changed from initial Whitelist)
            component.SetMode(0, FilterSplitterMode.Blacklist);
            component.SetFilterItem(0, 0, ForUnitTestItemId.ItemId1);
            component.SetMode(1, FilterSplitterMode.Default);
            component.SetMode(2, FilterSplitterMode.Default);

            // ItemId1 は dir0 では拒否され Default 方向 (dir1/dir2) にラウンドロビンで流れる
            // ItemId1 is rejected by dir0; it round-robins through Default (dir1/dir2)
            for (var i = 0; i < 4; i++)
            {
                var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
                component.InsertItem(stack, InsertItemContext.Empty);
                component.Update();
            }

            Assert.AreEqual(0, TotalCount(dummies[0]));
            Assert.AreEqual(4, TotalCount(dummies[1]) + TotalCount(dummies[2]));

            // ItemId2 (Blacklist に無い) は dir0 にも Whitelist マッチ扱い (Blacklist 非マッチ = 明示許可) で入れる
            // ItemId2 (not in Blacklist) is explicitly allowed at dir0; explicit-match priority kicks in
            for (var i = 0; i < 3; i++)
            {
                var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1);
                component.InsertItem(stack, InsertItemContext.Empty);
                component.Update();
            }
            Assert.IsTrue(0 < TotalCount(dummies[0]), "Blacklist 非マッチアイテムが dir0 に届いていない");
        }

        [Test]
        public void UnconnectedDirectionSkippedTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (splitter, component, dummies) = CreateSplitterWithDummies(new BlockInstanceId(4));

            // 初期値は Whitelist のため、全方向を明示的に Default に設定する
            // Initial mode is Whitelist, so explicitly set every direction to Default
            for (var d = 0; d < component.DirectionCount; d++) component.SetMode(d, FilterSplitterMode.Default);

            // dir1 の接続を外す
            // Disconnect dir1 from the splitter
            var connectedTargets = (Dictionary<IBlockInventory, ConnectedInfo>)splitter.GetComponent<BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>>().ConnectedTargets;
            connectedTargets.Remove(dummies[1]);

            // 6 個挿入 → dir0 と dir2 のみに分配される (dir1 はスキップされ詰まらない)
            // 6 inserts should distribute to dir0 and dir2 only; dir1 must never block insertion
            for (var i = 0; i < 6; i++)
            {
                var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
                var remain = component.InsertItem(stack, InsertItemContext.Empty);
                Assert.AreEqual(0, remain.Count);
                component.Update();
            }

            Assert.AreEqual(0, TotalCount(dummies[1]));
            Assert.AreEqual(6, TotalCount(dummies[0]) + TotalCount(dummies[2]));
        }

        [Test]
        public void DuplicateFilterSlotItemsHandledCorrectlyTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (_, component, dummies) = CreateSplitterWithDummies(new BlockInstanceId(5));

            // dir0 の slot0/slot1 両方に ItemId1、その後 slot0 を ItemId2 に変更
            // Put ItemId1 in dir0 slot0 and slot1, then change slot0 to ItemId2
            component.SetMode(0, FilterSplitterMode.Whitelist);
            component.SetFilterItem(0, 0, ForUnitTestItemId.ItemId1);
            component.SetFilterItem(0, 1, ForUnitTestItemId.ItemId1);
            component.SetFilterItem(0, 0, ForUnitTestItemId.ItemId2);

            // slot1 にまだ ItemId1 があるので、ItemId1 は dir0 にマッチしなければならない
            // slot1 still holds ItemId1, so ItemId1 must still be accepted by dir0
            var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
            component.InsertItem(stack, InsertItemContext.Empty);
            component.Update();

            Assert.AreEqual(1, TotalCount(dummies[0]));
        }

        [Test]
        public void SaveLoadPreservesFilterConfigTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // 1個目: フィルター設定を行い save state を取得
            // First splitter: configure filters and capture save state
            var (_, component1, _) = CreateSplitterWithDummies(new BlockInstanceId(6));
            component1.SetMode(0, FilterSplitterMode.Whitelist);
            component1.SetFilterItem(0, 0, ForUnitTestItemId.ItemId1);
            component1.SetMode(1, FilterSplitterMode.Blacklist);
            component1.SetFilterItem(1, 0, ForUnitTestItemId.ItemId2);
            var savedJson = component1.GetSaveState();

            // 2個目: save state を渡してロード → 設定が復元されている
            // Second splitter: load with save state; settings must match
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.FilterSplitter).BlockGuid;
            var loaded = ServerContext.BlockFactory.Load(
                blockGuid,
                new BlockInstanceId(7),
                new Dictionary<string, string> { { component1.SaveKey, savedJson } },
                new BlockPositionInfo(new Vector3Int(10, 0, 0), BlockDirection.North, Vector3Int.one));
            var component2 = loaded.GetComponent<VanillaFilterSplitterComponent>();

            Assert.AreEqual(FilterSplitterMode.Whitelist, component2.GetMode(0));
            Assert.AreEqual(ForUnitTestItemId.ItemId1, component2.GetFilterItems(0)[0]);
            Assert.AreEqual(FilterSplitterMode.Blacklist, component2.GetMode(1));
            Assert.AreEqual(ForUnitTestItemId.ItemId2, component2.GetFilterItems(1)[0]);
        }

        [Test]
        public void SetItemNormalizesInvalidInputTest()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var (_, component, _) = CreateSplitterWithDummies(new BlockInstanceId(8));

            // count >= 2 を SetItem → 1 個に丸めて格納
            // SetItem with count >= 2 must be clamped to 1
            var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 5);
            component.SetItem(0, stack);
            Assert.AreEqual(1, component.GetItem(0).Count);

            // empty を SetItem → null 化されて empty が返る
            // SetItem with empty must null out the buffer and return empty
            component.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
            Assert.AreEqual(ItemMaster.EmptyItemId, component.GetItem(0).Id);
        }

        // DummyBlockInventory は同 ID をスタックして 1 スロットにまとめるため、個数比較は Count 合計で行う
        // DummyBlockInventory stacks same-ID items into a single slot, so compare totals via Count sum
        private static int TotalCount(DummyBlockInventory dummy)
        {
            return dummy.InsertedItems.Sum(stack => stack.Count);
        }

        private static (IBlock Block, VanillaFilterSplitterComponent Component, DummyBlockInventory[] Dummies) CreateSplitterWithDummies(BlockInstanceId blockInstanceId)
        {
            // FilterSplitter ブロックを生成し、3 つの出力方向に DummyBlockInventory を接続する
            // Create a FilterSplitter and wire up 3 DummyBlockInventory targets to its outputs
            var splitter = ServerContext.BlockFactory.Create(
                ForUnitTestModBlockId.FilterSplitter,
                blockInstanceId,
                new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
            var component = splitter.GetComponent<VanillaFilterSplitterComponent>();

            var param = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.FilterSplitter).BlockParam as FilterSplitterBlockParam;
            var outputs = param.InventoryConnectors.OutputConnects;

            var connectedTargets = (Dictionary<IBlockInventory, ConnectedInfo>)splitter.GetComponent<BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>>().ConnectedTargets;
            connectedTargets.Clear();

            var dummies = new DummyBlockInventory[outputs.Length];
            for (var i = 0; i < outputs.Length; i++)
            {
                var selfConnector = new OutputConnectsElement(i, outputs[i].ConnectorGuid, null, Vector3Int.zero, Array.Empty<Vector3Int>());
                var targetConnector = new OutputConnectsElement(i + 100, Guid.NewGuid(), null, Vector3Int.zero, Array.Empty<Vector3Int>());
                dummies[i] = new DummyBlockInventory();
                connectedTargets.Add(dummies[i], new ConnectedInfo(selfConnector, targetConnector, null));
            }

            return (splitter, component, dummies);
        }
    }
}
