# AIコミット diff (base->ai) — レビュー対象（FilterSplitterのテスト＋実装）
```diff
commit b67b262b3b346701c8a7aead7033f8c73c7d45dc
Author: sakastudio <sakastudio100@gmail.com>
Date:   Tue May 19 20:52:50 2026 +0900

    FilterSplitter: 1プロトコル1メソッド原則、Request再設計、テスト追加
    
    - VanillaApi: GetFilterSplitterState/Set* 3 メソッドを SendFilterSplitterStateRequest 1 メソッドに統合
    - FilterSplitterStateRequest: enum を int に変換せず直接保持、ItemId 型を直接渡す
    - public ctor を private 化、static factory (CreateGetRequest 等) を Operation ごとに分割
    - ItemMaster.ExistItemId(ItemId) を追加し protocol で master 未存在を InvalidItem 応答
    - DirectionStatePack.FilterItemIds を List<ItemId> 化 (Guid 文字列廃止)
    - Component の Update バッファ flush 完了判定を Count<=0 も含む形に修正 (本番バグ修正)
    - 出力方向ごと 1 スロット内部バッファのラウンドロビン振り分け
      Whitelist/Blacklist 明示マッチ優先、Default は fallback、未接続方向はスキップ
    - save/load を ConnectorGuid ベース化、master 並べ替えに耐性
    - FilterSplitter テスト用ブロック/アイテムを ForUnitTest master に追加
    - CombinedTest 7件 + PacketTest 10件、関連回帰 126件全パス
    - creating-server-protocol SKILL に「1プロトコル1メソッド」「enum はそのまま」
      「ItemId はそのまま」「Operation 別 static factory」原則を追記
    
    Co-Authored-By: Claude Opus 4.7 (1M context) <noreply@anthropic.com>

diff --git a/moorestech_server/Assets/Scripts/Game.Block/Blocks/FilterSplitter/VanillaFilterSplitterComponent.cs b/moorestech_server/Assets/Scripts/Game.Block/Blocks/FilterSplitter/VanillaFilterSplitterComponent.cs
index 89952e6b5..ced3428a5 100644
--- a/moorestech_server/Assets/Scripts/Game.Block/Blocks/FilterSplitter/VanillaFilterSplitterComponent.cs
+++ b/moorestech_server/Assets/Scripts/Game.Block/Blocks/FilterSplitter/VanillaFilterSplitterComponent.cs
@@ -161,7 +161,9 @@ namespace Game.Block.Blocks.FilterSplitter
 
                 var context = new InsertItemContext(_blockInstanceId, target.Value.Info.SelfConnector, target.Value.Info.TargetConnector);
                 var result = target.Value.Inventory.InsertItem(dir.BufferedItem, context);
-                dir.BufferedItem = result.Id == ItemMaster.EmptyItemId ? null : result;
+                // 送信完了は「空 ID」または「Count <= 0」で判定（IItemStack 実装によって戻り値が異なるため両方見る）
+                // Treat both "empty id" and "count <= 0" as fully delivered (IItemStack implementations differ)
+                dir.BufferedItem = (result.Id == ItemMaster.EmptyItemId || result.Count <= 0) ? null : result;
             }
         }
 
diff --git a/moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/FilterSplitterTest.cs b/moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/FilterSplitterTest.cs
new file mode 100644
index 000000000..0c6402874
--- /dev/null
+++ b/moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/FilterSplitterTest.cs
@@ -0,0 +1,243 @@
+using System;
+using System.Collections.Generic;
+using System.Linq;
+using Core.Item.Interface;
+using Core.Master;
+using Game.Block.Blocks.FilterSplitter;
+using Game.Block.Component;
+using Game.Block.Interface;
+using Game.Block.Interface.Component;
+using Game.Block.Interface.Extension;
+using Game.Context;
+using Mooresmaster.Model.BlockConnectInfoModule;
+using Mooresmaster.Model.BlocksModule;
+using NUnit.Framework;
+using Server.Boot;
+using Tests.Module;
+using Tests.Module.TestMod;
+using UnityEngine;
+
+namespace Tests.CombinedTest.Core
+{
+    /// <summary>
+    /// FilterSplitter ブロックのルーティング・フィルター・永続化を検証する。
+    /// Verifies routing, filtering, and persistence of the FilterSplitter block.
+    /// </summary>
+    public class FilterSplitterTest
+    {
+        [Test]
+        public void DefaultModeCatchAllTest()
+        {
+            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+            var (splitter, component, dummies) = CreateSplitterWithDummies(new BlockInstanceId(1));
+
+            // 全方向 Default のまま 3 アイテム挿入 → 各方向に 1 個ずつラウンドロビン
+            // All directions stay Default; 3 inserts should round-robin one item each
+            for (var i = 0; i < 3; i++)
+            {
+                var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
+                var remain = component.InsertItem(stack, InsertItemContext.Empty);
+                Assert.AreEqual(0, remain.Count);
+            }
+            component.Update();
+
+            foreach (var dummy in dummies)
+            {
+                Assert.AreEqual(1, TotalCount(dummy));
+            }
+        }
+
+        [Test]
+        public void WhitelistPriorityOverDefaultTest()
+        {
+            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+            var (splitter, component, dummies) = CreateSplitterWithDummies(new BlockInstanceId(2));
+
+            // dir0 を Whitelist[ItemId1]、dir1/dir2 は Default
+            // dir0 = Whitelist[ItemId1], dir1/dir2 = Default
+            component.SetMode(0, FilterSplitterMode.Whitelist);
+            component.SetFilterItem(0, 0, ForUnitTestItemId.ItemId1);
+
+            // ItemId1 を 10 個挿入 → 全て dir0 へ集中（Default は fallback）
+            // Insert ItemId1 ten times; all should land in dir0 (Default is only fallback)
+            for (var i = 0; i < 10; i++)
+            {
+                var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
+                component.InsertItem(stack, InsertItemContext.Empty);
+                component.Update();
+            }
+
+            Assert.AreEqual(10, TotalCount(dummies[0]));
+            Assert.AreEqual(0, TotalCount(dummies[1]));
+            Assert.AreEqual(0, TotalCount(dummies[2]));
+        }
+
+        [Test]
+        public void BlacklistRoutingTest()
+        {
+            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+            var (splitter, component, dummies) = CreateSplitterWithDummies(new BlockInstanceId(3));
+
+            // dir0 を Blacklist[ItemId1]、dir1/dir2 は Default
+            // dir0 = Blacklist[ItemId1], dir1/dir2 = Default
+            component.SetMode(0, FilterSplitterMode.Blacklist);
+            component.SetFilterItem(0, 0, ForUnitTestItemId.ItemId1);
+
+            // ItemId1 は dir0 では拒否され Default 方向 (dir1/dir2) にラウンドロビンで流れる
+            // ItemId1 is rejected by dir0; it round-robins through Default (dir1/dir2)
+            for (var i = 0; i < 4; i++)
+            {
+                var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
+                component.InsertItem(stack, InsertItemContext.Empty);
+                component.Update();
+            }
+
+            Assert.AreEqual(0, TotalCount(dummies[0]));
+            Assert.AreEqual(4, TotalCount(dummies[1]) + TotalCount(dummies[2]));
+
+            // ItemId2 (Blacklist に無い) は dir0 にも Whitelist マッチ扱い (Blacklist 非マッチ = 明示許可) で入れる
+            // ItemId2 (not in Blacklist) is explicitly allowed at dir0; explicit-match priority kicks in
+            for (var i = 0; i < 3; i++)
+            {
+                var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, 1);
+                component.InsertItem(stack, InsertItemContext.Empty);
+                component.Update();
+            }
+            Assert.IsTrue(0 < TotalCount(dummies[0]), "Blacklist 非マッチアイテムが dir0 に届いていない");
+        }
+
+        [Test]
+        public void UnconnectedDirectionSkippedTest()
+        {
+            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+            var (splitter, component, dummies) = CreateSplitterWithDummies(new BlockInstanceId(4));
+
+            // dir1 の接続を外す
+            // Disconnect dir1 from the splitter
+            var connectedTargets = (Dictionary<IBlockInventory, ConnectedInfo>)splitter.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
+            connectedTargets.Remove(dummies[1]);
+
+            // 6 個挿入 → dir0 と dir2 のみに分配される (dir1 はスキップされ詰まらない)
+            // 6 inserts should distribute to dir0 and dir2 only; dir1 must never block insertion
+            for (var i = 0; i < 6; i++)
+            {
+                var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
+                var remain = component.InsertItem(stack, InsertItemContext.Empty);
+                Assert.AreEqual(0, remain.Count);
+                component.Update();
+            }
+
+            Assert.AreEqual(0, TotalCount(dummies[1]));
+            Assert.AreEqual(6, TotalCount(dummies[0]) + TotalCount(dummies[2]));
+        }
+
+        [Test]
+        public void DuplicateFilterSlotItemsHandledCorrectlyTest()
+        {
+            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+            var (splitter, component, dummies) = CreateSplitterWithDummies(new BlockInstanceId(5));
+
+            // dir0 の slot0/slot1 両方に ItemId1、その後 slot0 を ItemId2 に変更
+            // Put ItemId1 in dir0 slot0 and slot1, then change slot0 to ItemId2
+            component.SetMode(0, FilterSplitterMode.Whitelist);
+            component.SetFilterItem(0, 0, ForUnitTestItemId.ItemId1);
+            component.SetFilterItem(0, 1, ForUnitTestItemId.ItemId1);
+            component.SetFilterItem(0, 0, ForUnitTestItemId.ItemId2);
+
+            // slot1 にまだ ItemId1 があるので、ItemId1 は dir0 にマッチしなければならない
+            // slot1 still holds ItemId1, so ItemId1 must still be accepted by dir0
+            var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
+            component.InsertItem(stack, InsertItemContext.Empty);
+            component.Update();
+
+            Assert.AreEqual(1, TotalCount(dummies[0]));
+        }
+
+        [Test]
+        public void SaveLoadPreservesFilterConfigTest()
+        {
+            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+
+            // 1個目: フィルター設定を行い save state を取得
+            // First splitter: configure filters and capture save state
+            var (splitter1, component1, _) = CreateSplitterWithDummies(new BlockInstanceId(6));
+            component1.SetMode(0, FilterSplitterMode.Whitelist);
+            component1.SetFilterItem(0, 0, ForUnitTestItemId.ItemId1);
+            component1.SetMode(1, FilterSplitterMode.Blacklist);
+            component1.SetFilterItem(1, 0, ForUnitTestItemId.ItemId2);
+            var savedJson = component1.GetSaveState();
+
+            // 2個目: save state を渡してロード → 設定が復元されている
+            // Second splitter: load with save state; settings must match
+            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.FilterSplitter).BlockGuid;
+            var loaded = ServerContext.BlockFactory.Load(
+                blockGuid,
+                new BlockInstanceId(7),
+                new Dictionary<string, string> { { component1.SaveKey, savedJson } },
+                new BlockPositionInfo(new Vector3Int(10, 0, 0), BlockDirection.North, Vector3Int.one));
+            var component2 = loaded.GetComponent<VanillaFilterSplitterComponent>();
+
+            Assert.AreEqual(FilterSplitterMode.Whitelist, component2.GetMode(0));
+            Assert.AreEqual(ForUnitTestItemId.ItemId1, component2.GetFilterItems(0)[0]);
+            Assert.AreEqual(FilterSplitterMode.Blacklist, component2.GetMode(1));
+            Assert.AreEqual(ForUnitTestItemId.ItemId2, component2.GetFilterItems(1)[0]);
+        }
+
+        [Test]
+        public void SetItemNormalizesInvalidInputTest()
+        {
+            var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+            var (splitter, component, _) = CreateSplitterWithDummies(new BlockInstanceId(8));
+
+            // count >= 2 を SetItem → 1 個に丸めて格納
+            // SetItem with count >= 2 must be clamped to 1
+            var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 5);
+            component.SetItem(0, stack);
+            Assert.AreEqual(1, component.GetItem(0).Count);
+
+            // empty を SetItem → null 化されて empty が返る
+            // SetItem with empty must null out the buffer and return empty
+            component.SetItem(0, ServerContext.ItemStackFactory.CreatEmpty());
+            Assert.AreEqual(ItemMaster.EmptyItemId, component.GetItem(0).Id);
+        }
+
+        #region Helpers
+
+        // DummyBlockInventory は同 ID をスタックして 1 スロットにまとめるため、個数比較は Count 合計で行う
+        // DummyBlockInventory stacks same-ID items into a single slot, so compare totals via Count sum
+        private static int TotalCount(DummyBlockInventory dummy)
+        {
+            return dummy.InsertedItems.Sum(stack => stack.Count);
+        }
+
+        private static (IBlock Block, VanillaFilterSplitterComponent Component, DummyBlockInventory[] Dummies) CreateSplitterWithDummies(BlockInstanceId blockInstanceId)
+        {
+            // FilterSplitter ブロックを生成し、3 つの出力方向に DummyBlockInventory を接続する
+            // Create a FilterSplitter and wire up 3 DummyBlockInventory targets to its outputs
+            var splitter = ServerContext.BlockFactory.Create(
+                ForUnitTestModBlockId.FilterSplitter,
+                blockInstanceId,
+                new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one));
+            var component = splitter.GetComponent<VanillaFilterSplitterComponent>();
+
+            var param = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.FilterSplitter).BlockParam as FilterSplitterBlockParam;
+            var outputs = param.InventoryConnectors.OutputConnects.items;
+
+            var connectedTargets = (Dictionary<IBlockInventory, ConnectedInfo>)splitter.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
+            connectedTargets.Clear();
+
+            var dummies = new DummyBlockInventory[outputs.Length];
+            for (var i = 0; i < outputs.Length; i++)
+            {
+                var selfConnector = new BlockConnectInfoElement(i, "Inventory", outputs[i].ConnectorGuid, Vector3Int.zero, Array.Empty<Vector3Int>(), null);
+                var targetConnector = new BlockConnectInfoElement(i + 100, "Inventory", Guid.NewGuid(), Vector3Int.zero, Array.Empty<Vector3Int>(), null);
+                dummies[i] = new DummyBlockInventory();
+                connectedTargets.Add(dummies[i], new ConnectedInfo(selfConnector, targetConnector, null));
+            }
+
+            return (splitter, component, dummies);
+        }
+
+        #endregion
+    }
+}
diff --git a/moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/FilterSplitterStateProtocolTest.cs b/moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/FilterSplitterStateProtocolTest.cs
new file mode 100644
index 000000000..a47a5dbf6
--- /dev/null
+++ b/moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/FilterSplitterStateProtocolTest.cs
@@ -0,0 +1,184 @@
+using System;
+using Core.Master;
+using Game.Block.Blocks.FilterSplitter;
+using Game.Block.Interface;
+using Game.Block.Interface.Extension;
+using Game.Context;
+using MessagePack;
+using NUnit.Framework;
+using Server.Boot;
+using Server.Protocol;
+using Server.Protocol.PacketResponse;
+using Tests.Module.TestMod;
+using UnityEngine;
+
+namespace Tests.CombinedTest.Server.PacketTest
+{
+    /// <summary>
+    /// FilterSplitterStateProtocol の Get/SetMode/SetFilterItem 各 Operation を検証する。
+    /// Verifies the Get/SetMode/SetFilterItem operations of FilterSplitterStateProtocol.
+    /// </summary>
+    public class FilterSplitterStateProtocolTest
+    {
+        private static readonly Vector3Int SplitterPos = new(20, 0, 20);
+
+        [Test]
+        public void GetReturnsCurrentSnapshotTest()
+        {
+            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+            PlaceFilterSplitter();
+
+            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateGetRequest(SplitterPos));
+
+            Assert.IsTrue(response.Success);
+            Assert.AreEqual(3, response.DirectionCount);
+            Assert.AreEqual(4, response.FilterSlotCountPerDirection);
+            // 初期状態は全方向 Default
+            // Initial state is Default for all directions
+            for (var d = 0; d < response.DirectionCount; d++)
+            {
+                Assert.AreEqual(FilterSplitterMode.Default, response.Directions[d].Mode);
+            }
+        }
+
+        [Test]
+        public void GetForNonexistentBlockReturnsBlockNotFoundTest()
+        {
+            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+
+            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateGetRequest(new Vector3Int(999, 0, 999)));
+
+            Assert.IsFalse(response.Success);
+            Assert.AreEqual(FilterSplitterStateProtocol.FilterSplitterStateFailureReason.BlockNotFound, response.FailureReason);
+        }
+
+        [Test]
+        public void GetForWrongBlockTypeReturnsNotFilterSplitterTest()
+        {
+            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+            // Chest を置く（FilterSplitter ではない）
+            // Place a Chest (not a FilterSplitter)
+            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.ChestId, SplitterPos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
+
+            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateGetRequest(SplitterPos));
+
+            Assert.IsFalse(response.Success);
+            Assert.AreEqual(FilterSplitterStateProtocol.FilterSplitterStateFailureReason.NotFilterSplitter, response.FailureReason);
+        }
+
+        [Test]
+        public void SetModeUpdatesModeAndReturnsSnapshotTest()
+        {
+            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+            PlaceFilterSplitter();
+
+            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetModeRequest(SplitterPos, 1, FilterSplitterMode.Whitelist));
+
+            Assert.IsTrue(response.Success);
+            Assert.AreEqual(FilterSplitterMode.Whitelist, response.Directions[1].Mode);
+            Assert.AreEqual(FilterSplitterMode.Default, response.Directions[0].Mode);
+        }
+
+        [Test]
+        public void SetModeWithInvalidDirectionReturnsInvalidDirectionTest()
+        {
+            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+            PlaceFilterSplitter();
+
+            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetModeRequest(SplitterPos, 99, FilterSplitterMode.Whitelist));
+
+            Assert.IsFalse(response.Success);
+            Assert.AreEqual(FilterSplitterStateProtocol.FilterSplitterStateFailureReason.InvalidDirection, response.FailureReason);
+        }
+
+        [Test]
+        public void SetModeWithInvalidModeReturnsInvalidModeTest()
+        {
+            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+            PlaceFilterSplitter();
+
+            // 未定義 enum 値 (例: 999) を投入
+            // Inject an undefined enum value (e.g. 999)
+            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetModeRequest(SplitterPos, 0, (FilterSplitterMode)999));
+
+            Assert.IsFalse(response.Success);
+            Assert.AreEqual(FilterSplitterStateProtocol.FilterSplitterStateFailureReason.InvalidMode, response.FailureReason);
+        }
+
+        [Test]
+        public void SetFilterItemValidIdUpdatesSlotTest()
+        {
+            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+            PlaceFilterSplitter();
+
+            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetFilterItemRequest(SplitterPos, 0, 0, ForUnitTestItemId.ItemId1));
+
+            Assert.IsTrue(response.Success);
+            Assert.AreEqual(ForUnitTestItemId.ItemId1, response.Directions[0].FilterItemIds[0]);
+        }
+
+        [Test]
+        public void SetFilterItemEmptyClearsSlotTest()
+        {
+            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+            PlaceFilterSplitter();
+            // 一旦アイテムを入れてからクリアする
+            // Set then clear
+            Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetFilterItemRequest(SplitterPos, 0, 0, ForUnitTestItemId.ItemId1));
+            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetFilterItemRequest(SplitterPos, 0, 0, ItemMaster.EmptyItemId));
+
+            Assert.IsTrue(response.Success);
+            Assert.AreEqual(ItemMaster.EmptyItemId, response.Directions[0].FilterItemIds[0]);
+        }
+
+        [Test]
+        public void SetFilterItemUnknownIdReturnsInvalidItemTest()
+        {
+            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+            PlaceFilterSplitter();
+
+            // master に存在しない ItemId を投入
+            // Inject an ItemId that does not exist in the master
+            var unknownItemId = new ItemId(int.MaxValue);
+            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetFilterItemRequest(SplitterPos, 0, 0, unknownItemId));
+
+            Assert.IsFalse(response.Success);
+            Assert.AreEqual(FilterSplitterStateProtocol.FilterSplitterStateFailureReason.InvalidItem, response.FailureReason);
+        }
+
+        [Test]
+        public void SetFilterItemInvalidSlotReturnsInvalidSlotTest()
+        {
+            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
+            PlaceFilterSplitter();
+
+            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetFilterItemRequest(SplitterPos, 0, 99, ForUnitTestItemId.ItemId1));
+
+            Assert.IsFalse(response.Success);
+            Assert.AreEqual(FilterSplitterStateProtocol.FilterSplitterStateFailureReason.InvalidSlot, response.FailureReason);
+        }
+
+        #region Helpers
+
+        private static void PlaceFilterSplitter()
+        {
+            ServerContext.WorldBlockDatastore.TryAddBlock(
+                ForUnitTestModBlockId.FilterSplitter,
+                SplitterPos,
+                BlockDirection.North,
+                Array.Empty<BlockCreateParam>(),
+                out _);
+        }
+
+        private static FilterSplitterStateProtocol.FilterSplitterStateResponse Send(
+            PacketResponseCreator packet,
+            FilterSplitterStateProtocol.FilterSplitterStateRequest request)
+        {
+            var payload = MessagePackSerializer.Serialize(request);
+            var responseBytes = packet.GetPacketResponse(payload)[0];
+            return MessagePackSerializer.Deserialize<FilterSplitterStateProtocol.FilterSplitterStateResponse>(responseBytes);
+        }
+
+        #endregion
+    }
+}
```
