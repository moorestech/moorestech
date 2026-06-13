# FilterSplitter テスト＋実装 レビュー（v5）

Warning | FilterSplitterStateProtocolTest.cs:317 | T | 全テストが固定座標 `SplitterPos=(20,0,20)` を共有し、`GetForWrongBlockTypeReturnsNotFilterSplitterTest` はそこに Chest、他テストは FilterSplitter を置く。各テストが新 DIContainer を作って world をリセットする前提だが、WorldBlockDatastore が同一 Generate 内で別テストの残骸を引き継ぐと「既に同座標にブロックがある」で TryAddBlock が無言失敗し別テストが汚染される。人間の最小修正: テストごとに座標をずらす（各 `[Test]` でローカル pos を使う）か、各テスト先頭で確実に world をクリアする確認を入れる。

Warning | FilterSplitterStateProtocolTest.cs:355 | T | `GetForWrongBlockTypeReturnsNotFilterSplitterTest` は `TryAddBlock` の out 結果（成功可否）を `out _` で捨てている。Chest 配置が失敗していてもテストは BlockNotFound ではなく NotFilterSplitter を期待しており、配置失敗時に誤って通る／落ちる。人間の最小修正: `out var added` を受け `Assert.IsTrue(added)` を前段に足す。

Warning | FilterSplitterTest.cs:272 | T | `CreateSplitterWithDummies` が `connectedTargets.Clear()` してから master 由来 `outputs[i].ConnectorGuid` で再登録するが、これは実機の接続生成過程（隣接ブロック方向→ConnectorGuid 解決）を一切経由しないテスト専用ショートカット。実装の `HasConnection`/`FindConnectedTargetByGuid` は selfConnector.ConnectorGuid 一致に依存するので、master の outputs 順と `_directions` 順が一致する前提に乗っている。テストは通るが「接続が実際に張られる経路」を検証していない。人間の修正: 重大ではないので注記のみ／本来は接続を実生成する CombinedTest 形にする。

Nit | FilterSplitterTest.cs:141 | T | `BlacklistRoutingTest` は `dir1+dir2 == 4` の合算しか見ず、ラウンドロビンが片寄って 4:0 でも通る。round-robin の分散（2:2 付近）を検証する意図なら個別 dummy の Count も assert すべき。人間の修正: 必要なら個別 assert 追加、不要なら据え置き。

Nit | FilterSplitterTest.cs:188-190 | T | `DuplicateFilterSlotItemsHandledCorrectlyTest` は slot0/slot1 に ItemId1 → slot0 を ItemId2 に上書き、slot1 の ItemId1 残存を検証。`ContainsFilterItem` が配列直接走査なので通る。premise は実装と整合。指摘なし（健全性確認）。

Nit | VanillaFilterSplitterComponent.cs:36 | S | `dir.BufferedItem = (result.Id == EmptyItemId || result.Count <= 0) ? null : result;` の修正自体は妥当（コミットメッセージ通りの本番バグ修正）。ただし `InsertItem` 実装が「全量受領なら EmptyItemStack（Count0）」を返す設計なら `Count <= 0` 片方で足り、`||` の `result.Id == EmptyItemId` 判定は冗長。人間は片方に整理する可能性あり。挙動は正しいので Nit。

---

最有力: **FilterSplitterStateProtocolTest.cs:317 の固定座標共有（観点T）**。10 件のパケットテストが同一 `SplitterPos` を使い、片方で Chest・他方で FilterSplitter を置く。world リセットが DIContainer 再生成に完全依存しており、リセットが甘いと TryAddBlock の無言失敗で別テストが偽陽性／偽陰性になる。テスト前提が「毎回まっさらな world」に暗黙依存している点が最も人間の手直し対象になりやすい。
