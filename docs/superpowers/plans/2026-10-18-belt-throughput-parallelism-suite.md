# Belt Throughput & Parallelism Regression Suite Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ベルトのスループット100%と並列可能性を、実装クラスに依存しない回帰テスト（配置カタログ＋並列性テスト）で固定する。

**Architecture:** テストは「ブロックの世界」（CreateServer→設置→GameUpdater→受けチェストの個数）と「同期の契約」（複製ハッシュ一致）だけを通す。forUnitTest に本番速度のベルト族を足し、`Tests/Util/Throughput/` のシナリオ DSL が設置順3通り・計測開始・窓・期待値（簡単な配置は周期、複雑系は最大流）を共通化する。並列性は Core に段階実行器を公開し、段数一定・順序独立・Parallel 一致・アロケーション0で測る。

**Tech Stack:** NUnit（uloop）、Core.BeltTransport、Game.Block、forUnitTest マスタ

## Requirements

- R1. forUnitTest に木/鉄/電気/高速の4族（直線・上り・下り・分岐器、歯車族は歯車付き）と大スタックの搬送用アイテムを追加し、既存ブロック・既存テストは変えない。受け入れ: `BlockMasterUtil.Validate` が通り、既存ベルトテストが全件 pass。
- R2. シナリオ DSL: 配置一覧を正順・逆順・ランダム（シード1）で設置し、供給チェストに大スタックを入れ、全受けチェストが2個以上受けた tick から W tick 数え、合計と各受けの個数を返す。毎 tick 複製へ差分を再生しハッシュ一致を assert。受け入れ: A1（木直線100マス）が3順とも `合計 == floor(W/20)`。
- R3. A カタログ（シナリオ案 §2 A）を1配置1テストクラス（設置順は `[Values]` で3倍）。合計は厳密一致、分配は ±1。
- R4. B カタログは現実装の正確な値をピン止めし、根拠コメント（README §1tickの順序）を付ける。
- R5. C カタログは期待値を容量グラフの最大流で出す（テスト内の小さな Edmonds-Karp）。
- R6. 並列性: Core に段階実行器 API を置き、段数一定・順序独立・Parallel 一致・アロケーション0 のテスト。
- R7. テスト README に「新機構を足す PR はその機構が稼働した配置を A/B に足す」「B の値を変える PR は ADR に理由」「段数を増やす PR は定数性を示す」を書く。
- やらないこと: 時間閾値のテスト、alpha マスタの使用、既存テストベルト（0.333 等）の速度変更、1マスだけの歯車網（機構上不可）。

## Global Constraints

- AGENTS.md の規約全部。`Func<>` 禁止、デフォルト引数禁止、200行/ファイル、10ファイル/ディレクトリ、日英2行コメント、`#region Internal` はローカル関数用途のみ。
- テストは forUnitTest（`TestModDirectory.ForUnitTestModDirectory`）で `MoorestechServerDIContainerGenerator` を起こす既存流儀（`GearBeltConveyorTest` 参照）。
- 設置順は `BeltPlacementOrder { Forward, Reverse, RandomSeed1 }`。ランダムは `System.Random(1)` の Fisher–Yates で決定論。
- W: 分岐を含まない配置 361×8=2888、含む配置 361×16=5776（`BeltThroughputWindow.Plain / WithSplitter`）。
- 出所: ADR `docs/adr/0084-belt-throughput-and-parallelism-regression-suite.md`、シナリオ案 `../MyBeltConvSegment/2026-10-17-スループットと並列性の回帰テスト_シナリオ案.md`。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / DSL は Tests/Util（前例 BeltWorldTestUtil・TrainAutoRunTestScenario）、段階実行器は Core.BeltTransport（参照実装 `Tick(parallel:)` に相当）
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施 / ユーザーが実装の即時着手を指示したため省略（判断記録 J2）

---

### Task 1: forUnitTest に本番速度のベルト族と搬送用アイテムを足す

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs`
- Create: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModItemId.cs`（無ければ。既存のアイテム定数置き場があればそこへ）

**Interfaces:**
- Produces: `ForUnitTestModBlockId.WoodGearBelt / WoodGearBeltUp / WoodGearBeltDown / WoodGearBeltSplitter`、同 `IronGearBelt*`、`ElectricBelt*`、`FastBelt*`（計16）、`ForUnitTestModItemId.ThroughputItem`（stack 1,000,000）。

- [ ] **Step 1: blocks.json に16ブロックとファミリー4件を足す。** 木・鉄の歯車族は既存 `GearBeltConveyor`(…0015) の `gear`/`gearConsumption`/`inventoryConnectors` を複製し `beltSpeedPerSecond` を 1.0 / 2.222222 に、`gearConsumption.baseTorque` は alpha と同じ 0.1685。上り/下りは alpha の上り/下りの `inventoryConnectors`（上り入力 `[0,0,-1],[0,-1,-1]`… alpha の `上り歯車ベルトコンベア` と同じ directions）を使い `slopeType` を `Up`/`Down`。分岐器は既存 `GearBeltConveyorSplitter`(…9040) の出力3方向を複製。電気・高速は `BeltConveyor` 型で既存 `TestBeltConveyor` の connectors、速度 6.666667 / 10。GUID は `7b3e1a10-0001-4000-8000-0000000000NN`（NN=01..16）。`beltConveyorFamilies` に `{up, down, straight}` を4件と `{straight: 分岐器}` を4件追加。
- [ ] **Step 2: items.json に `ThroughputTestItem`（itemGuid `7b3e1a10-0002-4000-8000-000000000001`、stackLevelTableGuid `7b3e1a10-0003-4000-8000-000000000001`）と `itemStackLevelTables` に `{"tableGuid":"7b3e1a10-0003-4000-8000-000000000001","name":"Stack1000000","stackCounts":[1000000]}` を足す。**
- [ ] **Step 3: `ForUnitTestModBlockId` に16定数、アイテム定数を足す。**
- [ ] **Step 4: コンパイルし、`BeltSpeedPerSecondMasterTest|BeltConveyorFamily|ExtractionSettingsValidationTest` を回してマスタ検証が通ることを確認。**
- [ ] **Step 5: コミット** `test: forUnitTest に本番速度のベルト族と搬送用アイテムを追加`

### Task 2: シナリオ DSL（Tests/Util/Throughput）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Tests/Util/Throughput/BeltPlacementOrder.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/Util/Throughput/BeltThroughputScenario.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/Util/Throughput/BeltThroughputRun.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/Util/Throughput/BeltThroughputWindow.cs`

**Interfaces:**
- Produces:
  - `enum BeltPlacementOrder { Forward, Reverse, RandomSeed1 }`
  - `sealed class BeltThroughputScenario` — `Belt(BlockId, Vector3Int, BlockDirection)`, `Source(Vector3Int, BlockDirection)`, `Sink(Vector3Int, BlockDirection)`, `Block(BlockId, Vector3Int, BlockDirection)`（発電機等）, `Window(int ticks)`, `Run(BeltPlacementOrder order) : BeltThroughputRun`
  - `sealed class BeltThroughputRun` — `int Window`, `int Total`, `IReadOnlyList<int> SinkCounts`, `int StartTick`
  - `static class BeltThroughputWindow { const int Plain = 361*8; const int WithSplitter = 361*16; }`
  - `static int BeltThroughputScenario.ExpectedTotal(int window, int periodTicks)` = `window / periodTicks`
- Consumes: `BeltWorldTestUtil.Place/Inventory/CountOf`、`BeltDiffReplayTestUtil.StartReplica/TickServer/ReplayAndAssert`（`internal` → 同アセンブリなので可）、`ForUnitTestModBlockId.ChestId`、`ForUnitTestModItemId.ThroughputItem`。

- [ ] **Step 1: 実装。** `Run`: `new MoorestechServerDIContainerGenerator().Create(...)` → 設置一覧を順序どおり `Place` → 供給チェストの slot0 に 1,000,000 個 → `StartReplica` → ループ: `TickServer` + `ReplayAndAssert`、全受けが2個以上になった tick を `StartTick`（上限 20,000 tick、超えたら Assert.Fail「計測開始に届かない」）→ そこから `Window` tick 数えて各受けの増分を `SinkCounts`、和を `Total`。
- [ ] **Step 2: コンパイル。コミット** `test: ベルトのスループット計測シナリオ DSL を追加`

### Task 3: A1（木直線100マス）で基盤を通す

**Files:**
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/Transport/Throughput/BeltThroughputStraightTest.cs`

- [ ] **Step 1: テスト。** `[Test] public void A1_WoodStraight100([Values] BeltPlacementOrder order)`: S(0,0,-1) → 木直線 z=0..99 → E(0,0,100)、歯車は `InfinityTestSimpleGearGenerator` を (1,0,0) に。`Assert.AreEqual(ExpectedTotal(Plain, 20), run.Total)`。鉄(9)・電気(3)・高速(2)も同じ形で `[TestCase]`。
- [ ] **Step 2: 実行して3順×4種が pass。落ちたら原因を観測して報告（期待値の式か実装か）。コミット** `test: A1 直線100マスのスループット100%を固定`

### Task 4〜7: カタログ A/B/C、§3 並列性、README（ユーザーの追記確定後に Task 分割して追記する）

### 閉じタスク

- [ ] moores-code-review スキルで全ブランチレビュー（Codex 監査はスキップ、記録に明記）
- [ ] 全作業をローカルコミット。push・PR はユーザー。

## 判断記録（ADR）

- 設計ADR: `docs/adr/0084-belt-throughput-and-parallelism-regression-suite.md`
- J1. DSL は `Tests/Util/Throughput/` に置く（前例 `BeltWorldTestUtil`・`TrainAutoRunTestScenario`）。本番コードに計測用の口は足さない。出所: agent前提
- J2. Phase 2.6・user-simulator・Codex は省略。出所: ユーザー 2026-10-18「テストまずは実装を」、メモ「Codex 使用禁止」
- J3. 供給は TestChest の slot0 に 1,000,000 個（チェストは毎 tick 全スロットを搬出試行するので、ベルトに空きがあれば毎 tick 1個入る）。無限チェストブロックは作らない。出所: agent前提（`VanillaChestComponent.Update`）
- J4. 歯車族の動力は `InfinityTestSimpleGearGenerator`（10 RPM・トルク 1e6）。A11 だけ `FuelGearGenerator`。出所: agent前提
- J5. unityプレイ録画テストは入れない（サーバー結合テストで完結、表示は関係ない）。出所: agent前提
