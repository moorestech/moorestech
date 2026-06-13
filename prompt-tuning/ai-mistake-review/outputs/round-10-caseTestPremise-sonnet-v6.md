# レビュー結果 — round-10-caseTestPremise-sonnet-v6

Warning | FilterSplitterStateProtocolTest.cs:331-335 | T | `GetReturnsCurrentSnapshotTest` が「初期状態は全方向 Default」とコメントで前提を記述しているが、テストコード内で `FilterSplitterMode.Default` を明示設定しておらず実装の初期値に依存している。`VanillaFilterSplitterComponent` の初期化コードが diff に含まれていないため、実際の初期値が `Default` かどうかを diff 内で確認できない。実装の初期値が `Default` でなければテストはサイレントに誤った前提で通過する。修正方針: テスト側で `CreateSplitterWithDummies` 後に各方向の Mode を明示確認するか、`GetReturnsCurrentSnapshotTest` 内でアサート前に `component.GetMode(d)` を使わず `response.Directions[d].Mode` を検証する前にコンポーネントが Default であることを明示設定する（`component.SetMode(d, FilterSplitterMode.Default)` を先頭に入れる）。

Warning | FilterSplitterTest.cs:79-92 | T | `DefaultModeCatchAllTest` が「全方向 Default のまま」と前提コメントを書いているが、`CreateSplitterWithDummies` を呼んだだけで Mode を明示設定していない。実装の初期 Mode が `Default` でなければ期待するラウンドロビン分配にならない。修正方針: ループ前に `for (var d = 0; d < 3; d++) component.SetMode(d, FilterSplitterMode.Default);` を明示追加する。

Nit | FilterSplitterTest.cs:162-163 | S | `UnconnectedDirectionSkippedTest` で `ConnectedTargets` を `(Dictionary<IBlockInventory, ConnectedInfo>)` にキャストして直接 `Remove` している。これはテスト内部からコレクションの実装型に依存する操作であり、`ConnectedTargets` が `IReadOnlyDictionary` 等を返す設計の場合はキャストが失敗する。diff 内には `BlockConnectorComponent<IBlockInventory>.ConnectedTargets` の型宣言が無いため確認できない。修正方針: キャストの安全性をコンパイル時に保証するか、テスト用の接続解除 API を使う（ただし現状 diff の範囲内では最小修正としてキャスト維持も許容）。

Nit | FilterSplitterStateProtocolTest.cs:317 | V | `SplitterPos` が `static readonly` フィールドで定義されており、各テストが同じ座標を共有している。各テストは独立した DI コンテナを生成するが、`PlaceFilterSplitter()` は毎回同一座標に配置するため、テスト間でワールドデータが共有される場合（DI コンテナがシングルトン等）は干渉リスクがある。ただし `MoorestechServerDIContainerGenerator().Create()` が毎回新規ワールドを生成するなら問題なし。diff 内ではワールドのスコープが確認できないため、Nit レベルとする。修正方針: 問題が起きたときにテストごとに異なる座標を使うか確認する。

---

**最有力の1件**

Warning | FilterSplitterStateProtocolTest.cs:331-335 | T | `GetReturnsCurrentSnapshotTest` で「初期状態は全方向 Default」という前提をコメントに書いているが、テスト内で Mode を明示設定せず実装の初期値に依存している。diff に `VanillaFilterSplitterComponent` の初期化コードが無いため、実際の初期値が `Default` かどうかを検証できない。修正方針: テスト先頭で各方向に `SetMode(d, FilterSplitterMode.Default)` を明示設定する、または `GetReturnsCurrentSnapshotTest` の期待値を実装の実初期値に合わせる。
