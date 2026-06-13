# GOLDEN: 人間が直した内容（捕捉できれば合格）
観点ID: T（テストが実装の実初期状態と不一致）

AIのテストは『全方向 Default のまま3アイテム挿入→ラウンドロビン』『初期状態は全方向Default』と、FilterSplitter の初期モードが Default である前提で書かれている。実際の初期モードは Whitelist。
人間の実際の修正(最小): (1) 各テスト冒頭で全方向を明示的に SetMode(Default) してから挿入を検証する。 (2) 初期状態を確認するテストの期待値を Default→Whitelist に修正。

合格条件: (1)『テストが初期モードを Default と仮定しているが実装と一致しない（初期値を設定/検証していない）』を検知し、(2)修正方針が『初期モードを明示設定する／期待値を実初期値に合わせる』方向であること（新規ヘルパ導入等の過剰設計はNG）。

```diff
commit f0c6abbb5d7fe2a2c5aa5511a9a9d4ac89aa74c8
Author: sakastudio <sakastudio100@gmail.com>
Date:   Thu Jun 4 17:35:23 2026 +0900

    テストの修正

diff --git a/moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/FilterSplitterTest.cs b/moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/FilterSplitterTest.cs
index 5d96c512c..ecacf74c0 100644
--- a/moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/FilterSplitterTest.cs
+++ b/moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/FilterSplitterTest.cs
@@ -31,8 +31,12 @@ namespace Tests.CombinedTest.Core
             var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
             var (_, component, dummies) = CreateSplitterWithDummies(new BlockInstanceId(1));
 
-            // 全方向 Default のまま 3 アイテム挿入 → 各方向に 1 個ずつラウンドロビン
-            // All directions stay Default; 3 inserts should round-robin one item each
+            // 初期値は Whitelist のため、全方向を明示的に Default に設定する
+            // Initial mode is Whitelist, so explicitly set every direction to Default
+            for (var d = 0; d < component.DirectionCount; d++) component.SetMode(d, FilterSplitterMode.Default);
+
+            // 全方向 Default で 3 アイテム挿入 → 各方向に 1 個ずつラウンドロビン
+            // All directions Default; 3 inserts should round-robin one item each
             for (var i = 0; i < 3; i++)
             {
                 var stack = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
@@ -78,10 +82,12 @@ namespace Tests.CombinedTest.Core
             var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
             var (_, component, dummies) = CreateSplitterWithDummies(new BlockInstanceId(3));
 
-            // dir0 を Blacklist[ItemId1]、dir1/dir2 は Default
-            // dir0 = Blacklist[ItemId1], dir1/dir2 = Default
+            // dir0 を Blacklist[ItemId1]、dir1/dir2 は Default（初期値 Whitelist から明示変更）
+            // dir0 = Blacklist[ItemId1], dir1/dir2 = Default (explicitly changed from initial Whitelist)
             component.SetMode(0, FilterSplitterMode.Blacklist);
             component.SetFilterItem(0, 0, ForUnitTestItemId.ItemId1);
+            component.SetMode(1, FilterSplitterMode.Default);
+            component.SetMode(2, FilterSplitterMode.Default);
 
             // ItemId1 は dir0 では拒否され Default 方向 (dir1/dir2) にラウンドロビンで流れる
             // ItemId1 is rejected by dir0; it round-robins through Default (dir1/dir2)
@@ -112,6 +118,10 @@ namespace Tests.CombinedTest.Core
             var (_, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
             var (splitter, component, dummies) = CreateSplitterWithDummies(new BlockInstanceId(4));
 
+            // 初期値は Whitelist のため、全方向を明示的に Default に設定する
+            // Initial mode is Whitelist, so explicitly set every direction to Default
+            for (var d = 0; d < component.DirectionCount; d++) component.SetMode(d, FilterSplitterMode.Default);
+
             // dir1 の接続を外す
             // Disconnect dir1 from the splitter
             var connectedTargets = (Dictionary<IBlockInventory, ConnectedInfo>)splitter.GetComponent<BlockConnectorComponent<IBlockInventory>>().ConnectedTargets;
diff --git a/moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/FilterSplitterStateProtocolTest.cs b/moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/FilterSplitterStateProtocolTest.cs
index e61f586a3..1e10eedfe 100644
--- a/moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/FilterSplitterStateProtocolTest.cs
+++ b/moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/FilterSplitterStateProtocolTest.cs
@@ -33,11 +33,11 @@ namespace Tests.CombinedTest.Server.PacketTest
             Assert.IsTrue(response.Success);
             Assert.AreEqual(3, response.DirectionCount);
             Assert.AreEqual(4, response.FilterSlotCountPerDirection);
-            // 初期状態は全方向 Default
-            // Initial state is Default for all directions
+            // 初期状態は全方向 Whitelist
+            // Initial state is Whitelist for all directions
             for (var d = 0; d < response.DirectionCount; d++)
             {
-                Assert.AreEqual(FilterSplitterMode.Default, response.Directions[d].Mode);
+                Assert.AreEqual(FilterSplitterMode.Whitelist, response.Directions[d].Mode);
             }
         }
 
@@ -72,11 +72,13 @@ namespace Tests.CombinedTest.Server.PacketTest
             var (packet, _) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
             PlaceFilterSplitter();
 
-            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetModeRequest(SplitterPos, 1, FilterSplitterMode.Whitelist));
+            // dir1 のみ Blacklist へ変更（初期値 Whitelist との差分で「dir1 だけ変わる」ことを検証）
+            // Change only dir1 to Blacklist; dir0 must stay at the initial Whitelist
+            var response = Send(packet, FilterSplitterStateProtocol.FilterSplitterStateRequest.CreateSetModeRequest(SplitterPos, 1, FilterSplitterMode.Blacklist));
 
             Assert.IsTrue(response.Success);
-            Assert.AreEqual(FilterSplitterMode.Whitelist, response.Directions[1].Mode);
-            Assert.AreEqual(FilterSplitterMode.Default, response.Directions[0].Mode);
+            Assert.AreEqual(FilterSplitterMode.Blacklist, response.Directions[1].Mode);
+            Assert.AreEqual(FilterSplitterMode.Whitelist, response.Directions[0].Mode);
         }
 
         [Test]
```
