# レビュー結果 — round-11-caseTestPremise-sonnet-v7

Warning | FilterSplitterStateProtocolTest.cs:331-335 | T | `GetReturnsCurrentSnapshotTest` が「初期状態は全方向 Default」と前提コメントを書き、`response.Directions[d].Mode == FilterSplitterMode.Default` をアサートしているが、diff 内に `VanillaFilterSplitterComponent` の初期化コードがなく、実装の実際の初期値が Default であることを検証していない。実装が Default 以外で初期化されていれば全テストが誤検知で通過し続ける。修正方針: テスト側で `component.SetMode(d, FilterSplitterMode.Default)` を明示するか、diff に含まれる実装の初期化箇所を確認してアサートを実際の初期値に合わせる。

Nit | FilterSplitterTest.cs:162-163 | S | `ConnectedTargets` を `(Dictionary<IBlockInventory, ConnectedInfo>)` にキャストして直接 `Remove` している。`ConnectedTargets` の実際の型が `IReadOnlyDictionary` などであればキャストが実行時例外になる。また内部コレクションを直接書き換えることで `BlockConnectorComponent` の内部状態が不整合になるリスクがある。修正方針: テスト用のヘルパー/接続解除 API があればそちらを使う。なければキャストの型を `IDictionary<IBlockInventory, ConnectedInfo>` に緩めるか、実装の内部型に合わせる。

Nit | FilterSplitterTest.cs:77 | S | `BlockInstanceId(1)` を各テストに固定でベタ書きしており、テストを同一 DI コンテナで連続実行した場合（例: テストランナーがコンテナをリセットしない環境）に ID 衝突が起きうる。`DefaultModeCatchAllTest` では 1、`WhitelistPriorityOverDefaultTest` では 2、… と IDが増えているので意図的ではあるが、テストが独立した DI コンテナを都度生成しているため実害は低い。ただし `SaveLoadPreservesFilterConfigTest` のみ ID 6/7 を使い BlockFactory.Load に 7 を渡しているので、別テストが ID 7 を先に使っていたら再登録で失敗しうる（現状は問題なし）。

---

**最有力の1件**

Warning | FilterSplitterStateProtocolTest.cs:331-335 | T | `GetReturnsCurrentSnapshotTest` が初期状態を Default と仮定してアサートしているが、実装の初期値が diff 内で確認できず前提ズレの可能性がある。修正方針: テスト側で初期状態を明示設定するか、実装の初期化コードを突き合わせて期待値を確定させる。
