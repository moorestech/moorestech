# ベルトのスループットと並列性の回帰テスト

ADR: `docs/adr/0084-belt-throughput-and-parallelism-regression-suite.md`。シナリオ一覧(確定版)はユーザー管理 repo `MyBeltConvSegment/2026-10-17-スループットと並列性の回帰テスト_シナリオ案.md`。

## 何を守っているか

- **スループット100%**: 配置ごとに「定常状態で100%でなければならない segment」を `Watch(マス)` で指定し、窓(周期の倍数: 2880 tick。分岐を含む配置も同じ)の間の搬出数が `W/P` にちょうど一致することを要求する。合わせて供給の受け入れ個数 == `W/P` と保存則(供給 − 受け == ベルト上の増減)。
- **並列可能性**: `Tests/UnitTest/Core/BeltTransport/Simulation/Parallel/`。1 tick の段数が網の大きさによらず7、段階内の要素順を入れ替えても・`Parallel.For` でも同じ結果、定常 tick のアロケーション0。
- 全シナリオを正順・逆順・ランダム順(シード1)の設置順で回す(`[Values] BeltPlacementOrder`)。

## カタログ

- A(`BeltThroughput{Straight,Merge,Splitter,Blocking,Machine,Gear,Ring}Test`): 100% を保証する配置。
- B(`BeltThroughputPinnedTest`、`BeltThroughputMergeTest.B7`、`BeltThroughputGearTest.A12`): 仕様上100%にならない配置。今の実装が出す正確な値をピン止め。
- C(`BeltThroughputComplexTest`): 複雑系。期待値は `BeltCapacityMaxFlow`(組から作る容量グラフの最大流)。
- D(判別器): A3(100 segment 直列)と A16(輪)で段数一定を見る。下流から順に更新する実装は段数が増えるか、輪の切れ目で100%を割る。

## 新しい機構を足す人へ

- ベルトに新しい機構(例: 回路条件で特定マスを止める)を足す PR は、**その機構が稼働した状態**の配置を A か B に1件以上追加する。稼働していない状態で100%でも不可。
- B の値を変える PR は、変えた理由と新しい値を同じ PR の ADR に書く。黙って変えない。
- 段階(`IBeltTickStage`)を増やす PR は、増えた段が網の大きさによらず定数であることを並列性テストで示す。
- 供給・受けには `ThroughputSourceChest` / `ThroughputSinkChest`(出力口だけ / 入力口だけ)を使う。普通のチェストは全面に出力口があり、隣のチェストやベルトの横面へ漏れて計測が壊れる。
- ブロック ID(`ForUnitTestModBlockId`)はサーバー起動後(`BeltThroughputScenario` 生成後)に解決する。

## segment の内部を固定するテストは置かない（2026-10-18）

`BeltConveyorSegment` の非公開メンバーをリフレクションで叩くテスト（Advance / DequeueHead / ブロック合体 / 段階ごとの予約プロトコル）は 2026-10-18 に削除した。再実装（Segment2）で丸ごと書き直しになる一方、挙動はこのスイートと B のピン値で世界経由に固定されているため。新しい挙動を守りたいときは、ここに配置を足すか B にピン値を足す。クラス直叩きで残っているのは、世界から届かない契約（坂の進入方向、保存値の定義、停止した分岐・合流の buffer）だけ。
