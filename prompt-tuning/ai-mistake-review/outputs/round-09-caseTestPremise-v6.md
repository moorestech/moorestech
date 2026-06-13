# FilterSplitter レビュー（base→ai）

Warning | FilterSplitterTest.cs:79-93 (DefaultModeCatchAllTest) | T | テストが「全方向 Default のまま」を前提にしているが、実装の `FilterSplitterMode` の初期値が本当に `Default` かを diff 内で突き合わせていない。`SetMode` を一切呼ばず初期値依存でラウンドロビン分配を期待しているため、実装の既定値が `Default` 以外（enum 0 が Whitelist 等）なら 3 アイテムが各方向 1 個ずつにならずテストが破綻する。修正: テスト冒頭で全方向に `component.SetMode(d, FilterSplitterMode.Default)` を明示するか、実装の初期値を確認し期待を合わせる。

Warning | FilterSplitterStateProtocolTest.cs:330-335 (GetReturnsCurrentSnapshotTest) | T | 「初期状態は全方向 Default」とコメントし `Assert.AreEqual(FilterSplitterMode.Default, ...)` で初期値に依存。実装側でモードを Default 初期化していなければ落ちる。設置直後に SetMode を経ていない状態の実初期値を diff 内で検証しておらず受け身。修正: 実装の初期化が Default であることを確認するか、テストで設置後に Get 前のモード初期値前提を明示する。

Nit | FilterSplitterTest.cs:143-151 (BlacklistRoutingTest) | T | 「Blacklist 非マッチ = 明示許可で dir0 に入る」という挙動前提が実装仕様と一致しているか不明（コメント上の自己申告）。`Assert.IsTrue(0 < TotalCount(dummies[0]))` と緩いので、実装が Blacklist 非マッチを単に fallback 扱いにしている場合に偽陰性（dir0 にもラウンドロビンで偶然入る）で通る。修正: 期待挙動を実装で確認し、必要なら厳密なカウント比較に変える。

Nit | FilterSplitterTest.cs:251-256 (TotalCount/DummyBlockInventory) | T | DummyBlockInventory が「同 ID をスタックして 1 スロットにまとめる」前提でカウント集計しているが、その挙動も自己申告コメント。容量制限やスタック上限がある実装なら 10 個集中（WhitelistPriorityOverDefaultTest）でオーバーフロー分が黙って失われ偽陽性になりうる。修正: Dummy の容量無制限を確認するか、各 Update 後に詰まりが無いことを `remain.Count==0` で確認する。

Nit | FilterSplitterStateProtocolTest.cs:436-440 | S | `new ItemId(int.MaxValue)` を「master に存在しない ID」として使うが、`ExistItemId` が範囲外で例外（辞書未登録キー直接アクセス）にならず確実に false を返す実装かは未確認。修正: 実装の `ExistItemId` が存在確認ベースであることを確認（観点 S の辞書直接アクセス回避）。

---

最有力: **DefaultModeCatchAllTest（FilterSplitterTest.cs:79-93）の T 級前提ズレ**。テストが `FilterSplitterMode` の初期値を `Default` と暗黙に仮定し SetMode を呼ばずラウンドロビンを期待している。実装の実初期値を diff 内で突き合わせていないため、初期値が Default でなければ全 Default 系テストが連鎖的に破綻する。修正は「テストで全方向 `SetMode(d, Default)` を明示」または「実装の実初期値に期待を合わせる」。
