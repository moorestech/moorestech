# クリーンルーム フェーズ5（I/O境界ブロック挙動 ＋ 永続化仕上げ）実装プラン

- **改訂: 2026-06-12 — codemap v2 整合＋批判的レビュー反映**（ブロック/コンポーネント命名のハッチ系統一、`CleanRoomDatastore` 前提化、ドアバーストの N 直接加算＋peek/latch 分離、境界ブロック用部屋クエリ `GetAdjacentCleanRooms` の新設、共有境界の計上規則、`CleanRoomSaveData` 非回帰の RunFrames 順序修正、in-transit バッファ上限、レート窓減衰テスト、搬出向きの明文化、テンプレ合成のスタブ方式確定、テストID実名修正）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **moorestech 固有の必須ルール:**
> - `.cs` 編集後は必ず `uloop compile --project-path ./moorestech_client` でコンパイル確認する。
> - テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"` で実行（クライアントプロジェクトからサーバーテストも走る）。
> - 新規サーバー `.cs`／新規 blockType／コネクタスキーマ追加を認識させるには Unity の **再起動**が要る場合がある（Refresh では不足）。「型が見つからない」「BlockType が無い」で失敗したら uloop で Unity を再起動してから再試行。各チェックポイントに再起動の注記を置く。
> - blockType／コネクタパラメータのスキーマ追加・SourceGenerator の手順は `edit-schema` スキルに従う。テスト作成は `creating-server-tests` スキルに従う。
> - 非ASCIIファイル編集時は AGENTS.md の「文字化け防止ワークフロー」を順守。`.cs` は UTF-16 LE BOM が多い。`縺`/`繧`/`繝` が連続したら破棄して読み直す。
> - **APIシグネチャ確認の原則:** 本プランの NEW コードは実コードベース（`VanillaBeltConveyorBlockInventoryInserter` / `FluidPipeComponent` / `FluidPipeSaveComponent` / `BlockConnectorComponent` / `BlockTemplateUtil` / `VanillaChestTemplate` 等）を開いて検証済みのシグネチャに接地している。フェーズ1〜4成果物（`CleanRoomDatastore` / `CleanRoom` / `CleanRoomPollutionCalculator` 等）は本プラン執筆時点で未マージのため、各 `.cs` を書く前に該当ファイルを開いて実シグネチャに合わせること。コンパイル/テストのチェックポイントが安全網。

**Goal:** クリーンルーム境界3ハッチの搬送挙動を実装する。アイテムハッチ（`CleanRoomItemHatchComponent`）は壁を貫通して外↔内のインベントリを中継し搬送レート `RecentThroughputPerSecond` を公開する。パイプハッチ（`CleanRoomPipeHatchComponent`）は壁を貫通して流体を中継する。ドアハッチ（`CleanRoomDoorHatchComponent`）はプレイヤー通過イベントを受けてバーストを溜める。これらの計量を配線する：`A_hatch` は `CleanRoomPollutionCalculator`（フェーズ3）の A_total へレートとして合算、**`A_door` バーストは A_total へ合算せず `CleanRoom.AddImpurity(burst)` で N へ直接加算**（バランス確定書§2 の単位注意）。境界ブロックから「面する部屋」を引くクエリ `CleanRoomDatastore.GetAdjacentCleanRooms(IBlock)` を本フェーズで実装する。I/O 固有状態を各ブロックの `IBlockSaveState` で round-trip させ、`CleanRoomSaveData`（N/thresholdIndex/status/猶予残/Cells）の非回帰を固定してフェーズ2導入の永続化を完結させる。

**Architecture:** 3種の I/O ブロックは、境界テンプレート `VanillaCleanRoomBoundaryTemplate` の各 `CleanRoomBoundaryKind` 分岐に**挙動コンポーネントを合成**して作る。各ブロックには (a) 汎用 `CleanRoomBoundaryComponent(kind)` マーカー（密閉境界として flood-fill 検出に見える）と (b) フェーズ5の挙動コンポーネント（＋アイテム/パイプはコネクタ）が**両方**付く。マーカーが「密閉」を担い、挙動コンポーネントが「離散イベントでの物の出入り」を担う。アイテムハッチは `VanillaBeltConveyorBlockInventoryInserter` のレイ機構（`BlockConnectorComponent<IBlockInventory>.ConnectedTargets` → `InsertItemContext` → `target.InsertItem`）を踏襲して毎tick中継しレート窓を更新する。パイプハッチは `FluidPipeComponent`（`FluidContainer` ＋ `BlockConnectorComponent<IFluidInventory>` ＋ push型 `Update`）を踏襲する（codemap §5 のとおり `IUpdatableBlockComponent` を実装）。ドアハッチは離散メソッド `NotifyPlayerPassage()` でバーストを溜め、**peek（非破壊）／latch（自前tickで確定・クリア）の二相**で `CleanRoomDatastore` の評価順に依存せず計上する。永続化は各ブロックの `IBlockSaveState`（中継中アイテム/流体）で行い、グローバルセーブスキーマ（`WorldSaveAllInfoV1` 等）には触れない。

**Tech Stack:** C# (Unity, moorestech_server), R3/UniRx, NUnit (Server.Tests), Mooresmaster Source Generator (blocks.yml / inventoryConnects / fluidInventoryConnects → 自動生成モジュール)。

---

## 0. 前提・契約・本フェーズの確定事項

### 0.1 契約（正）はコードマップ v2 とバランス確定書

本プランは **`2026-06-06-cleanroom-phases2-5-codemap.md`（v2・2026-06-12 整合済み）と `2026-06-06-cleanroom-balance-parameters.md` を契約（正）とする**。食い違いを見つけたら契約側を正として本プランを直す。フェーズ1〜4の成果物（**現ブランチ未マージ前提**）のうち本プランが使う契約名：

| 契約名 | 提供フェーズ | 本プランでの用途 |
|---|---|---|
| `CleanRoomDatastore`（`TryGetCleanRoomAt(Vector3Int, out CleanRoom)` / `GetCleanRoom(BlockInstanceId)` / `GetSaveData()` / `Restore(...)`、DI singleton・eager） | 1〜2 | 部屋クエリ・tick純度更新・永続化。**本フェーズで `GetAdjacentCleanRooms(IBlock)` を追加**（0.5） |
| `CleanRoom`（`Cells` / `Volume` / `SurfaceArea` / `ImpurityCount` / `Status` / `ThresholdIndex` / `AddImpurity(double)`） | 1〜2 | バーストの N 直接加算・非回帰アサート |
| `CleanRoomBoundaryComponent` ＋ `ICleanRoomBoundaryComponent`（`Game.Block.Interface`）＋ `CleanRoomBoundaryKind { Wall, DoorHatch, ItemHatch, PipeHatch }` | 1 | 密閉マーカー・境界種別 |
| `VanillaCleanRoomBoundaryTemplate` | 1 | kind 別合成（本フェーズで I/O 分岐を実装） |
| `CleanRoomPollutionCalculator`（`Game.CleanRoom/Pollution/`、具体クラス・注入IFなし） | 3 | `A_hatch` の合算先 |
| `CleanRoomSaveData`（`impurityCount` / `thresholdIndex` / `status` / `graceRemainingSeconds` / `cells`） | 2 | 非回帰テスト（改変しないことの証明） |

> **廃名警告（使用禁止）**: `CleanRoomDetectionSystem` / `CleanRoomPurityService` / `CleanRoomPuritySaveData` / `CleanRoomPollutionInput` / `CleanRoomDoor`・`CleanRoomPipeConnector`（blockType）/ `CleanRoomDoorComponent`・`CleanRoomPipeConnectorComponent`（コンポーネント）は **codemap v2 で廃止された旧名**。本プラン内にもコードにも一切登場させない。

### 0.2 確定数値（バランス確定書 §2/§6 が唯一のソース。本フェーズ確定分を含む）

- `k_hatch = 0.30`（`A_hatch = k_hatch · RecentThroughputPerSecond[個/秒]`。**A_total に合算するレート項**）
- `burst_door = 15`（**個/通過の瞬間量。A_total に合算してはならない**。`CleanRoom.AddImpurity(15)` で N へ直接加算。多重通過は合算）
- `a_connector = 0.50`（接続点1個あたりの恒常項。フェーズ3で計上済み。**BlockInstanceId 単位で重複排除**）
- レート窓長 `HatchRateWindowTicks = 20`（= 1.0秒。`RecentThroughputPerSecond = (直近20tickで中継した個数) / (20 · GameUpdater.SecondsPerTick)`）
- **in-transit バッファ上限 `MaxInTransitStacks = 4`（本フェーズで確定）**: 満杯時は `InsertionCheck=false`・`InsertItem` は差し戻し。上流ベルトを停滞させることで「ハッチ＝低スループット（集約の根拠）」（設計書§7）を実挙動にする

### 0.3 ドアハッチの「空気密閉」セマンティクス（フェーズ1と整合）

設計書 §7 の「ドアは貫通してよい」は **「ドアハッチは正当な密閉境界である（隙間/リークとしてカウントしない）」** の意味であり、**「開いた空セルとして空気が流れる」意味ではない**。4種すべて（Wall / DoorHatch / ItemHatch / PipeHatch）は**壁と同一の air-sealing flood-fill 境界**であり、閉じたドアハッチは空気を漏らさない。アイテム/流体/プレイヤーは**コンポーネントの離散ロジックと離散イベント**でのみ越える（空セル経由では決して越えない）。本プランで各 I/O ブロックに付く `CleanRoomBoundaryComponent(kind)` マーカーが密閉を担い、ドアハッチのプレイヤー通過は離散イベントで N バーストを足すだけで密閉性には影響しない。

### 0.4 ドアバーストの単位規律（A_total に混ぜない・peek/latch 分離）

バランス確定書 §2 の単位注意のとおり、`burst_door` は「個/通過」の瞬間量で、「個/秒」の `A_total` に直接合算すると `dN = A_total · SecondsPerTick` の積分で **N 増分が 15 × 0.05 = 0.75 ＝仕様の 1/20 に化ける**。よって：

- **計上**: `CleanRoomDatastore.Update` が部屋ごとに、隣接ドアハッチの保留バーストを `CleanRoom.AddImpurity(burst)` で **N へ直接加算**する（`A_total` 経路を通さない）。
- **読み出しの評価順独立**: ドアハッチは `_incomingBurst`（通過の受け皿）と `_pendingBurst`（今tickの公開値）の二相を持つ。`NotifyPlayerPassage()` は `_incomingBurst` へ加算、コンポーネント自身の `Update()`（latch）で `_pendingBurst = _incomingBurst; _incomingBurst = 0` と確定する。データストアは `PeekPendingBurst()`（**非破壊**）で読むだけ。これにより (a) 同一tick内で複数部屋が同じバーストを全額読める（0.5 の共有境界規則）、(b) どの部屋から評価しても結果が変わらない、(c) latch は毎tick自前で走るため「計上は正確に1tick分・二重計上なし」が部屋側の消費操作なしで保証される。
- **テスト**: 「A_total が +15 される」ではなく**「通過1回で N が +15 される」「次tick以降に再加算されない」を直接アサート**する（Task 6）。

> サーバーは単一スレッドでtickを回し、プロトコル処理（通過通知の発生源）はtick間に走るため、latch とデータストア評価の間に新規通過が割り込むことはない。データストアとブロックの `GameUpdater` 購読順がどちらでも「各バーストはちょうど1回だけ観測される」（順序はレイテンシ1tickの差にしかならない）。

### 0.5 境界ブロックの部屋クエリ（`GetAdjacentCleanRooms` を本フェーズで実装）＋ 共有境界の計上規則

- **`TryGetCleanRoomAt` や「部屋内」系クエリは境界ブロックには常に失敗する**。`CleanRoom.Cells` は内部セル（機械占有セルは含むが**境界セルは含まない**）であり、ハッチ/ドアハッチの占有セルは部屋に属さない。境界ブロックに対しては必ず本フェーズ新設の **`CleanRoomDatastore.GetAdjacentCleanRooms(IBlock)`**（占有セルの6近傍を部屋セルマップに照合し、面する部屋を重複なしで返す。**複数あり得る**）を使う。テストも同様。
- **2部屋の共有境界にあるハッチの扱い（バランス確定書 §2 で確定）**: 恒常項 `a_connector` と `A_hatch` は**面する各部屋に計上**（両部屋が汚染リスクを負う）。`burst_door` は**面する全部屋へ全額加算**（部屋Aから部屋Bへ通過すれば両方が汚れる）。peek が非破壊なのでこの規則は自然に実装できる（0.4）。
- 接続点・ハッチ計量の集計は**セル単位ではなく `BlockInstanceId` 単位**で重複排除する（マルチセル境界ブロックの多重カウント防止）。

### 0.6 占有セルと V / Cells（確定値・再litigationしない）

バランス確定書 §5 で確定：**`Cells` = 機械等の占有セルを含む全内部セル（帰属判定用）、`Volume` = `Cells` のうち空セルのみ**。本プランの I/O 境界ブロック（アイテム/パイプ/ドアハッチ）は**境界（boundary）であって内部（interior）ではない**ので `Cells` にも `Volume` にも入らず、`a_connector · 接続点数`（フェーズ3）と本フェーズの `A_hatch`/バーストで `A_total`・N に寄与する。本プランで V/Cells の定義は変更しない。

### 0.7 合成設計（kind 別・**空実装スタブ方式に確定**）

codemap §5 のシグネチャどおり、`CleanRoomItemHatchComponent` / `CleanRoomPipeHatchComponent` / `CleanRoomDoorHatchComponent` は **`ICleanRoomBoundaryComponent` を実装しない**（それぞれ `IBlockInventory` / `IFluidInventory` / 独自）。テンプレートは各 kind について**2つ（以上）**のコンポーネントを付ける：

| kind | 付与コンポーネント |
|---|---|
| `Wall` | `CleanRoomBoundaryComponent(Wall)` のみ（フェーズ1のまま） |
| `DoorHatch` | `CleanRoomBoundaryComponent(DoorHatch)` ＋ `CleanRoomDoorHatchComponent` |
| `ItemHatch` | `CleanRoomBoundaryComponent(ItemHatch)` ＋ `CleanRoomItemHatchComponent` ＋ `BlockConnectorComponent<IBlockInventory>` |
| `PipeHatch` | `CleanRoomBoundaryComponent(PipeHatch)` ＋ `CleanRoomPipeHatchComponent` ＋ `BlockConnectorComponent<IFluidInventory>`（`IFluidInventory.CreateFluidInventoryConnector` で生成） |

**実装順序は「空実装スタブ方式」一本に確定する**（旧版の「テンプレを書いて分岐をコメントアウト」案は廃止。コメントアウト運用は「.cs 編集後は必ずコンパイル」ルールと相性が悪く事故源になるため）：Task 3 で `CleanRoomPipeHatchComponent` / `CleanRoomDoorHatchComponent` を**コンパイルが通る最小スタブ**（テンプレが使うコンストラクタ＋`IBlockComponent` のみ）として先に置き、テンプレートは最初から全 kind 分岐の完全形で書く。Task 4/5 はスタブを本実装で置き換えるだけでテンプレートに触らない。

### 0.8 コネクタ面のジオメトリと搬入用/搬出用の向き（中継テストの load-bearing 仕様）

アイテム/パイプハッチは「入力面から受けて出力面へ流す」中継で、**面はブロックローカルに固定**する：
- アイテムハッチ: `inputConnects` = −X 面、`outputConnects` = +X 面。
- パイプハッチ: `inflowConnects` = −X 面、`outflowConnects` = +X 面。

**ハッチは内外を知らない**。部屋のどちら側の壁面に置くか（＋設置向き）で同じブロックが**搬入用にも搬出用にもなる**：部屋の −X 側壁面に置けば +X（出力）が室内を向き搬入用、+X 側壁面に置けば −X（入力）が室内を向き搬出用になる。`A_hatch` は向きに関係なく `RecentThroughputPerSecond` で計上される（搬出も等しく汚染源。Task 6 でテスト固定）。

テストは壁シェルの1面をハッチに置換し、入力側に隣接1セルへソース、出力側に隣接1セルへターゲットを置く。面が噛み合わないとコネクタが繋がらずアサートが空振りするので、**配置座標と向きをテスト本文で具体値にする**。

---

## File Structure（フェーズ5で作成/変更するファイル）

**スキーマ／テスト用 mod（コネクタパラメータ追加）**
- Modify: `VanillaSchema/blocks.yml` — フェーズ1で追加済みの `CleanRoomItemHatch` 分岐に `inventoryConnectors`（ref: inventoryConnects）、`CleanRoomPipeHatch` 分岐に `fluidInventoryConnectors`（ref: fluidInventoryConnects）を追加
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` — SourceGenerator トリガ
- Modify: テスト用 mod `.../ForUnitTest/mods/forUnitTest/master/blocks.json` — `TestCleanRoomItemHatch`/`TestCleanRoomPipeHatch` に入出力面を付与

**I/O 挙動コンポーネント（Game.Block）**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomItemHatchComponent.cs` — `IBlockInventory, IUpdatableBlockComponent, IBlockSaveState`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomPipeHatchComponent.cs` — `IFluidInventory, IUpdatableBlockComponent, IBlockSaveState`（codemap §5 のとおり push 型）
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomDoorHatchComponent.cs` — `IUpdatableBlockComponent`。通過バースト（peek/latch）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs` — kind 別に挙動コンポーネント＋コネクタを合成（New/Load）

**汚染計量の配線（Game.CleanRoom）**
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs` — `GetAdjacentCleanRooms(IBlock)` 追加＋tick でドアバーストを `AddImpurity` 計上
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/Pollution/CleanRoomPollutionCalculator.cs` — `A_hatch`（ハッチレート集計）を A_total へ取り込む

**テスト**
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs`

> 各 `.cs` 新規ファイルは Unity が `.meta` を自動生成する。`.meta` は手動作成禁止。

---

## Task 1: アイテム/パイプハッチのコネクタパラメータをスキーマに追加

フェーズ1では境界4種を param 無しで作った。フェーズ5でアイテムハッチに inventory コネクタ、パイプハッチに fluid コネクタのパラメータを足す。`edit-schema` スキルの手順に従う。

**Files:**
- Modify: `VanillaSchema/blocks.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`

- [ ] **Step 1: blocks.yml の該当 blockType 分岐にコネクタ param を追加**

まず `VanillaSchema/blocks.yml` の既存 `Chest` / `ElectricMachine` 分岐を読み、`inventoryConnectors`/`fluidInventoryConnectors` の書式（`ref: inventoryConnects` / `ref: fluidInventoryConnects` と `implementationInterface: - IInventoryConnectors`）を確認する（本プラン執筆時点の実ファイルで書式一致を確認済み）。フェーズ1で追加した `CleanRoomItemHatch` / `CleanRoomPipeHatch` の `when:` 分岐を、空オブジェクトから次へ拡張：

```yaml
      - when: CleanRoomItemHatch
        type: object
        implementationInterface:
        - IInventoryConnectors
        properties:
        - key: inventoryConnectors
          ref: inventoryConnects
      - when: CleanRoomPipeHatch
        type: object
        properties:
        - key: fluidInventoryConnectors
          ref: fluidInventoryConnects
```

> `CleanRoomDoorHatch` / `CleanRoomWall` は param 無しのまま。`IInventoryConnectors` を付ける書式は既存 `Chest` 分岐に正確に合わせる。

- [ ] **Step 2: SourceGenerator をトリガ**

`Core.Master/_CompileRequester.cs` の `dummyText` 定数を変更：

```csharp
private const string dummyText = "regenerate-cleanroom-phase5-io-connectors";
```

- [ ] **Step 3: 再生成を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。`Mooresmaster.Model.BlocksModule.CleanRoomItemHatchBlockParam.InventoryConnectors`（型 `InventoryConnects`）、`CleanRoomPipeHatchBlockParam.FluidInventoryConnectors`（型 `FluidInventoryConnects`）が生成される（Task 3/4 で参照確認）。

> 「Domain Reload in progress」なら45秒待って再試行。型未検出なら Unity 再起動。

- [ ] **Step 4: Commit**

```bash
git add VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git commit -m "feat(cleanroom): アイテム/パイプハッチにI/Oコネクタparamをスキーマ追加"
```

---

## Task 2: テスト用 mod にコネクタ面を付与

NUnit テストは `ForUnitTest` mod のマスタを使う。フェーズ1で追加済みの `TestCleanRoomItemHatch` / `TestCleanRoomPipeHatch` に入出力コネクタ面を付ける。0.8 のとおり入力面=−X／出力面=+X に固定する。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`

- [ ] **Step 1: 既存のコネクタ面 JSON 書式を確認**

`.../master/blocks.json` で既存 `TestChest`（`inventoryConnectors`）／`TestElectricGenerator`（`fluidInventoryConnectors`）の書式（`inputConnects`/`outputConnects`・`inflowConnects`/`outflowConnects` の配列、各要素の `offset`/`connectType`/`directions`/`connectOption`/`connectorGuid`）を読む。**そのキー名・構造をそのまま踏襲する**（本プラン執筆時点の実 mod で確認済みの書式が下のサンプル）。

- [ ] **Step 2: アイテムハッチに入出力面を付与**

`TestCleanRoomItemHatch` の `blockParam` に、−X 入力・+X 出力のコネクタを追加。`connectorGuid` は既存と衝突しない新規GUIDを割り当てる：

```json
"blockParam": {
  "inventoryConnectors": {
    "inputConnects": [
      {
        "offset": [0, 0, 0],
        "connectType": "Inventory",
        "directions": [ [-1, 0, 0] ],
        "connectOption": { "inventoryOptions": [] },
        "connectorGuid": "<新規GUID-HatchInput>"
      }
    ],
    "outputConnects": [
      {
        "offset": [0, 0, 0],
        "connectType": "Inventory",
        "directions": [ [1, 0, 0] ],
        "connectOption": { "inventoryOptions": [] },
        "connectorGuid": "<新規GUID-HatchOutput>"
      }
    ]
  }
}
```

- [ ] **Step 3: パイプハッチに inflow/outflow 面を付与**

`TestCleanRoomPipeHatch` の `blockParam` に、−X inflow・+X outflow を追加。**`connectOption.flowCapacity` を必ず含めること**（欠落すると `CleanRoomPipeHatchComponent.GetMaxFlowRate` が 0 を返し、流れず中継テストが空振りする）：

```json
"blockParam": {
  "fluidInventoryConnectors": {
    "inflowConnects": [
      {
        "connectType": "Fluid",
        "offset": [0, 0, 0],
        "directions": [ [-1, 0, 0] ],
        "connectOption": { "flowCapacity": 100, "connectTankIndex": 0 },
        "connectorGuid": "<新規GUID-PipeInflow>"
      }
    ],
    "outflowConnects": [
      {
        "connectType": "Fluid",
        "offset": [0, 0, 0],
        "directions": [ [1, 0, 0] ],
        "connectOption": { "flowCapacity": 100, "connectTankIndex": 0 },
        "connectorGuid": "<新規GUID-PipeOutflow>"
      }
    ]
  }
}
```

> `flowCapacity=100` は `TestElectricGenerator` と同値（テスト mod の `TestFluidPipe` は flowCapacity=10 なので、ハッチ↔パイプ間の実効流量は min(10,100)=10/秒×0.05秒/tick になる点に注意してテストの tick 数を見積もる）。`connectorGuid` は新規GUID。

- [ ] **Step 4: コンパイル（マスタ読み込み確認）**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json
git commit -m "test(cleanroom): テスト用アイテム/パイプハッチにI/Oコネクタ面を付与"
```

---

## Task 3: アイテムハッチ — 壁貫通中継 ＋ レート窓 ＋ バッファ上限 ＋ セーブ

`CleanRoomItemHatchComponent` は `IBlockInventory` として外のベルト/機械から `InsertItem` を受け、内部バッファ（**上限 `MaxInTransitStacks=4`**）に保持し、`Update` で `BlockConnectorComponent<IBlockInventory>.ConnectedTargets`（= 出力面の接続先）へ中継する。中継した個数をレート窓に記録し `RecentThroughputPerSecond` を公開する。中継待ちアイテムは `IBlockSaveState` で保存する。あわせて Pipe/Door のスタブとテンプレート完全版を入れる（0.7 のスタブ方式）。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomItemHatchComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomPipeHatchComponent.cs`（スタブ）
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomDoorHatchComponent.cs`（スタブ）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs`

- [ ] **Step 1: 失敗テスト（中継到達＋レート＋上限＋減衰）を書く**

`Tests/CombinedTest/Core/CleanRoomIoTest.cs` を新規作成：

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomIoTest
    {
        // ハッチが入力面から受けたアイテムを出力面のターゲットへ中継し、レートを公開する
        // Hatch relays an item from the input side to the output-side target and reports throughput
        [Test]
        public void ItemHatch_RelaysItemAndReportsThroughput()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // ハッチを (0,0,0)、出力面(+X)側ターゲット(チェスト)を (1,0,0) に置く
            // Place hatch at (0,0,0) and the output-side chest target at (1,0,0)
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomItemHatch, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var hatchBlock);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(1, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var targetChest);

            Assert.True(hatchBlock.TryGetComponent<CleanRoomItemHatchComponent>(out var hatch));

            // 入力面ソースの代役として、ハッチに直接 InsertItem する
            // Insert directly into the hatch as a stand-in for the input-side source
            var item = ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1);
            var remain = hatch.InsertItem(item, InsertItemContext.Empty);
            Assert.AreEqual(0, remain.Count, "Hatch accepts the item into its in-transit buffer");

            // 中継が完了するまで tick を回す
            // Tick until the relay completes
            GameUpdater.RunFrames(5);

            // ターゲットチェストにアイテムが届いている
            // The item has arrived in the target chest
            Assert.True(targetChest.TryGetComponent<IBlockInventory>(out var chestInv));
            var arrived = Enumerable.Range(0, chestInv.GetSlotSize())
                .Sum(i => chestInv.GetItem(i).Count);
            Assert.AreEqual(1, arrived, "Relayed item reaches the target inventory");

            // レート窓に搬送が反映されている（直近窓に1個 → >0）
            // Throughput window reflects the relay (1 item in the recent window → >0)
            Assert.Greater(hatch.RecentThroughputPerSecond, 0.0);
        }

        // 満杯のハッチは受け取りを拒否し差し戻す（低スループットの根拠）
        // A full hatch rejects insertion and hands the stack back (the basis of low throughput)
        [Test]
        public void ItemHatch_RejectsWhenInTransitBufferIsFull()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // ターゲット無しで設置 → 中継が完了せずバッファに溜まる
            // Place without a target so items stay in-transit
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomItemHatch, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var hatchBlock);
            Assert.True(hatchBlock.TryGetComponent<CleanRoomItemHatchComponent>(out var hatch));

            for (var i = 0; i < CleanRoomItemHatchComponent.MaxInTransitStacks; i++)
            {
                var r = hatch.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);
                Assert.AreEqual(0, r.Count);
            }

            // 上限到達後は InsertionCheck=false・InsertItem は差し戻し
            // Once full, InsertionCheck is false and InsertItem hands the stack back untouched
            Assert.False(hatch.InsertionCheck(new List<IItemStack>()));
            var rejected = hatch.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);
            Assert.AreEqual(1, rejected.Count, "Full buffer rejects further insertion");
        }

        // 搬送停止後、レート窓1周（20tick）で RecentThroughputPerSecond は 0 へ戻る
        // After relays stop, throughput decays to zero within one full window (20 ticks)
        [Test]
        public void ItemHatch_ThroughputDecaysToZeroAfterIdleWindow()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomItemHatch, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var hatchBlock);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(1, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            Assert.True(hatchBlock.TryGetComponent<CleanRoomItemHatchComponent>(out var hatch));

            hatch.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);
            GameUpdater.RunFrames(2);
            Assert.Greater(hatch.RecentThroughputPerSecond, 0.0, "Relay lands in the window");

            // 窓1周ぶん無搬送で回す → 0 に減衰
            // Run one full idle window → decays to zero
            GameUpdater.RunFrames(CleanRoomItemHatchComponent.HatchRateWindowTicks + 1);
            Assert.AreEqual(0.0, hatch.RecentThroughputPerSecond, 1e-9);
        }
    }
}
```

> `ForUnitTestModBlockId.ChestId` / `ForUnitTestItemId.ItemId1` / `MoorestechServerDIContainerOptions` / `IBlock.TryGetComponent<T>`（`Game.Block.Interface.Extension.BlockExtension`）/ `InsertItemContext.Empty` / `GameUpdater.RunFrames(uint)` は実コードで確認済みの実名。`ForUnitTestModBlockId.CleanRoomItemHatch`（フェーズ1のテスト mod 追加分）はフェーズ1改訂版の定義名に合わせる。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ItemHatch_(RelaysItemAndReportsThroughput|RejectsWhenInTransitBufferIsFull|ThroughputDecaysToZeroAfterIdleWindow)"`
Expected: FAIL（`CleanRoomItemHatchComponent` 未定義）。

- [ ] **Step 3: CleanRoomItemHatchComponent を実装（＋Pipe/Door の空実装スタブ）**

`Game.Block/Blocks/CleanRoom/CleanRoomItemHatchComponent.cs`：

```csharp
using System.Collections.Generic;
using System.Linq;
using Core.Item.Interface;
using Core.Update;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using Newtonsoft.Json;

namespace Game.Block.Blocks.CleanRoom
{
    // 壁貫通アイテムハッチ。入力面から受け、出力面の接続先へ毎tick中継し搬送レートを公開する
    // Wall-piercing item hatch: accepts on the input face, relays to output-side targets each tick, reports throughput
    public class CleanRoomItemHatchComponent : IBlockInventory, IUpdatableBlockComponent, IBlockSaveState
    {
        public string SaveKey => SaveKeyStatic;
        public static string SaveKeyStatic { get; } = typeof(CleanRoomItemHatchComponent).FullName;

        // レート窓長（tick）。1.0秒 = 20tick。RecentThroughputPerSecond の分母に使う
        // Rate-window length in ticks (1.0s = 20 ticks); denominator for RecentThroughputPerSecond
        public const int HatchRateWindowTicks = 20;

        // 中継待ちバッファの上限スタック数（0.2 確定値）。満杯時は受け取りを拒否し上流を停滞させる
        // Max in-transit stacks (fixed in §0.2); a full buffer rejects insertion so the upstream stalls
        public const int MaxInTransitStacks = 4;

        // 直近の窓で中継した個数のリングバッファ（合計を窓秒で割る）
        // Ring buffer of relayed counts over the recent window (sum divided by window seconds)
        private readonly int[] _relayedPerTick = new int[HatchRateWindowTicks];
        private int _windowCursor;

        // 中継待ちアイテム（入力面から受けてまだ出力面へ流していない分）
        // In-transit items received on the input face but not yet pushed out
        private readonly List<IItemStack> _inTransit = new();

        private readonly BlockInstanceId _blockInstanceId;
        private readonly BlockConnectorComponent<IBlockInventory> _connector;

        // 直近窓の合計搬送個数 / 窓秒。汚染計量 A_hatch = k_hatch · この値
        // Sum of relayed counts over the window / window seconds; pollution A_hatch = k_hatch * this
        public double RecentThroughputPerSecond
        {
            get
            {
                var sum = 0;
                for (var i = 0; i < _relayedPerTick.Length; i++) sum += _relayedPerTick[i];
                return sum / (HatchRateWindowTicks * GameUpdater.SecondsPerTick);
            }
        }

        public CleanRoomItemHatchComponent(BlockInstanceId blockInstanceId, BlockConnectorComponent<IBlockInventory> connector)
        {
            _blockInstanceId = blockInstanceId;
            _connector = connector;
        }

        // セーブからの復元: 中継中アイテムだけ戻す。レート窓は揮発（ロード後0から再充填）
        // Restore from save: only the in-transit items; the rate window is transient (refills from 0 after load)
        public CleanRoomItemHatchComponent(Dictionary<string, string> componentStates, BlockInstanceId blockInstanceId, BlockConnectorComponent<IBlockInventory> connector)
            : this(blockInstanceId, connector)
        {
            if (!componentStates.TryGetValue(SaveKey, out var raw)) return;
            var saved = JsonConvert.DeserializeObject<List<ItemStackSaveJsonObject>>(raw);
            if (saved == null) return;
            foreach (var s in saved)
            {
                var stack = s?.ToItemStack();
                if (stack != null && stack.Count > 0) _inTransit.Add(stack);
            }
        }

        // 入力面から受け取り、中継バッファへ積む。満杯時は差し戻す（上限 = MaxInTransitStacks）
        // Accept into the in-transit buffer; hand the stack back when full (cap = MaxInTransitStacks)
        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context)
        {
            BlockException.CheckDestroy(this);
            if (itemStack == null || itemStack.Count == 0) return itemStack;
            if (_inTransit.Count >= MaxInTransitStacks) return itemStack;
            _inTransit.Add(itemStack);
            return ServerContext.ItemStackFactory.CreatEmpty();
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            BlockException.CheckDestroy(this);
            return _inTransit.Count < MaxInTransitStacks;
        }

        // 毎tick: 中継待ちを出力面ターゲットへ押し出し、押し出した個数をレート窓へ記録
        // Each tick: push in-transit items to output-side targets and record the pushed count into the rate window
        public void Update()
        {
            BlockException.CheckDestroy(this);

            var relayedThisTick = AdvanceRelay();
            RecordRate(relayedThisTick);

            #region Internal

            // 中継待ちの各アイテムを接続先へ InsertItem。受け入れられた個数を返す
            // Push each in-transit item to a connected inventory; return accepted count
            int AdvanceRelay()
            {
                var targets = _connector.ConnectedTargets;
                if (targets.Count == 0) return 0;

                var relayed = 0;
                for (var idx = _inTransit.Count - 1; idx >= 0; idx--)
                {
                    var stack = _inTransit[idx];
                    var before = stack.Count;
                    var remain = InsertToAnyTarget(stack, targets);
                    relayed += before - remain.Count;
                    if (remain.Count == 0) _inTransit.RemoveAt(idx);
                    else _inTransit[idx] = remain;
                }
                return relayed;
            }

            IItemStack InsertToAnyTarget(IItemStack stack, IReadOnlyDictionary<IBlockInventory, ConnectedInfo> targets)
            {
                var current = stack;
                foreach (var target in targets)
                {
                    if (current.Count == 0) break;
                    var ctx = new InsertItemContext(_blockInstanceId, target.Value.SelfConnector, target.Value.TargetConnector);
                    current = target.Key.InsertItem(current, ctx);
                }
                return current;
            }

            // リングバッファの現在tick枠に今回の搬送数を入れ、カーソルを進める
            // Write this tick's relayed count into the ring slot and advance the cursor
            void RecordRate(int relayed)
            {
                _relayedPerTick[_windowCursor] = relayed;
                _windowCursor = (_windowCursor + 1) % HatchRateWindowTicks;
            }

            #endregion
        }

        public IItemStack GetItem(int slot)
        {
            BlockException.CheckDestroy(this);
            return slot >= 0 && slot < _inTransit.Count ? _inTransit[slot] : ServerContext.ItemStackFactory.CreatEmpty();
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            BlockException.CheckDestroy(this);
            while (_inTransit.Count <= slot) _inTransit.Add(ServerContext.ItemStackFactory.CreatEmpty());
            _inTransit[slot] = itemStack;
        }

        public int GetSlotSize()
        {
            BlockException.CheckDestroy(this);
            return _inTransit.Count;
        }

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var serialized = _inTransit.Select(s => new ItemStackSaveJsonObject(s)).ToList();
            return JsonConvert.SerializeObject(serialized);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; }
    }
}
```

> 実コードで確認済みの実名（本プラン執筆時に該当ファイルを開いて照合）: `ItemStackSaveJsonObject`（`Core.Item.Interface`）＋ `ToItemStack()` 拡張（`Game.Context.ItemStackSaveJsonObjectExtension`）／`ItemStackFactory.CreatEmpty()`（タイポ風だが実名）／`BlockException.CheckDestroy`（`Game.Block.Interface`）／`ConnectedInfo.SelfConnector`・`TargetConnector`（`IBlockConnectorComponent.cs`）／`GameUpdater.SecondsPerTick`。

あわせて **Pipe/Door の空実装スタブ**を置く（0.7。Task 4/5 で本実装に置き換える。テンプレが参照するコンストラクタ署名のみ先に確定させる）：

`Game.Block/Blocks/CleanRoom/CleanRoomPipeHatchComponent.cs`（スタブ）：

```csharp
using System.Collections.Generic;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom
{
    // 壁貫通パイプハッチ（Task 4 で本実装に置き換える空実装スタブ）
    // Wall-piercing pipe hatch (empty stub; replaced by the real implementation in Task 4)
    public class CleanRoomPipeHatchComponent : IBlockComponent
    {
        public CleanRoomPipeHatchComponent(float capacity, BlockConnectorComponent<IFluidInventory> connector) { }
        public CleanRoomPipeHatchComponent(Dictionary<string, string> componentStates, float capacity, BlockConnectorComponent<IFluidInventory> connector) { }

        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; }
    }
}
```

`Game.Block/Blocks/CleanRoom/CleanRoomDoorHatchComponent.cs`（スタブ）：

```csharp
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom
{
    // クリーンルームのドアハッチ（Task 5 で本実装に置き換える空実装スタブ）
    // Clean-room door hatch (empty stub; replaced by the real implementation in Task 5)
    public class CleanRoomDoorHatchComponent : IBlockComponent
    {
        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; }
    }
}
```

- [ ] **Step 4: VanillaCleanRoomBoundaryTemplate を kind 別合成の完全版に更新**

スタブが存在するため、**全 kind 分岐を最初から有効な完全形で書ける**（コメントアウト運用はしない）。`Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs` をフェーズ1の版から次へ置き換える：

```csharp
using System.Collections.Generic;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    // 4種のクリーンルーム境界ブロック共通テンプレート。kind 別に密閉マーカー＋I/O挙動を合成
    // Shared template for the 4 boundary block types; composes the sealing marker + I/O behavior per kind
    public class VanillaCleanRoomBoundaryTemplate : IBlockTemplate
    {
        private readonly CleanRoomBoundaryKind _kind;

        public VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind kind)
        {
            _kind = kind;
        }

        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId,
            BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return Build(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement,
            BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return Build(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        // componentStates が null なら New、非nullなら Load。kind で合成内容を分岐する
        // null componentStates → New, non-null → Load; switch composition by kind
        private IBlock Build(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement,
            BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            // 全 kind 共通で密閉マーカーを付ける（flood-fill 検出が境界として見る）
            // Every kind carries the sealing marker (flood-fill detection sees it as a boundary)
            var components = new List<IBlockComponent>
            {
                new CleanRoomBoundaryComponent(_kind),
            };

            // kind 別に I/O 挙動コンポーネント（＋コネクタ）を合成する
            // Compose the I/O behavior component (+ connector) per kind
            switch (_kind)
            {
                case CleanRoomBoundaryKind.ItemHatch:
                    AddItemHatch();
                    break;
                case CleanRoomBoundaryKind.PipeHatch:
                    AddPipeHatch();
                    break;
                case CleanRoomBoundaryKind.DoorHatch:
                    components.Add(new CleanRoomDoorHatchComponent());
                    break;
                case CleanRoomBoundaryKind.Wall:
                default:
                    break; // 壁はマーカーのみ
            }

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);

            #region Internal

            void AddItemHatch()
            {
                var param = (CleanRoomItemHatchBlockParam)blockMasterElement.BlockParam;
                var connector = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);
                var hatch = componentStates == null
                    ? new CleanRoomItemHatchComponent(blockInstanceId, connector)
                    : new CleanRoomItemHatchComponent(componentStates, blockInstanceId, connector);
                components.Add(connector);
                components.Add(hatch);
            }

            void AddPipeHatch()
            {
                var param = (CleanRoomPipeHatchBlockParam)blockMasterElement.BlockParam;
                var connector = IFluidInventory.CreateFluidInventoryConnector(param.FluidInventoryConnectors, blockPositionInfo);
                const float capacity = 100f; // FluidPipe(本番mod) と同値
                var pipe = componentStates == null
                    ? new CleanRoomPipeHatchComponent(capacity, connector)
                    : new CleanRoomPipeHatchComponent(componentStates, capacity, connector);
                components.Add(connector);
                components.Add(pipe);
            }

            #endregion
        }
    }
}
```

> 実コードで確認済み: `IBlockTemplate.New/Load` のシグネチャ／`BlockSystem(blockInstanceId, blockGuid, components, blockPositionInfo)`／`BlockTemplateUtil.CreateInventoryConnector(inventoryConnects, blockPositionInfo)`（`VanillaChestTemplate` と同流儀）／`IFluidInventory.CreateFluidInventoryConnector`。生成 param 型名 `CleanRoomItemHatchBlockParam.InventoryConnectors` / `CleanRoomPipeHatchBlockParam.FluidInventoryConnectors` は Task 1 の生成物。

- [ ] **Step 5: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。型未検出なら Unity 再起動（新規 `.cs`＋新規コネクタ param 生成のため初回は再起動が要る）。

- [ ] **Step 6: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ItemHatch_(RelaysItemAndReportsThroughput|RejectsWhenInTransitBufferIsFull|ThroughputDecaysToZeroAfterIdleWindow)"`
Expected: PASS。

- [ ] **Step 7: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/ moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs
git commit -m "feat(cleanroom): アイテムハッチの壁貫通中継・レート窓・バッファ上限を実装"
```

---

## Task 4: パイプハッチ — 壁貫通流体中継

`CleanRoomPipeHatchComponent` のスタブを本実装に置き換える。`IFluidInventory` として外パイプから `AddLiquid` を受け、内部 `FluidContainer` に溜め、`Update` で `BlockConnectorComponent<IFluidInventory>.ConnectedTargets` へ push する。`FluidPipeComponent` を簡略化した push 型（**codemap §5 のとおり `IUpdatableBlockComponent` を実装**。moorestech の流体は push モデルで、受動 `AddLiquid` のみだと溜まるだけで流れない）。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomPipeHatchComponent.cs`（スタブ→本実装）
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs`

- [ ] **Step 1: 失敗テスト（流体が出力側へ届く）を追加**

`CleanRoomIoTest` に追加：

```csharp
        // パイプハッチが inflow 面から受けた流体を outflow 面のパイプへ中継する
        // Pipe hatch relays fluid received on the inflow face to the outflow-side pipe
        [Test]
        public void PipeHatch_RelaysFluidToOutflowSide()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // ハッチを (0,0,0)、outflow(+X) 側パイプを (1,0,0) に置く
            // Hatch at (0,0,0), outflow-side pipe at (1,0,0)
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomPipeHatch, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var hatchBlock);
            world.TryAddBlock(ForUnitTestModBlockId.FluidPipe, new Vector3Int(1, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var outflowPipe);

            Assert.True(hatchBlock.TryGetComponent<CleanRoomPipeHatchComponent>(out var hatch));

            // 外パイプの代役として、ハッチへ直接 AddLiquid する（流体IDは既存 FluidTest と同じGUID流儀）
            // Add fluid directly to the hatch; resolve the fluid id the same way as the existing FluidTest
            var fluidId = Core.Master.MasterHolder.FluidMaster.GetFluidId(new Guid("00000000-0000-0000-1234-000000000001"));
            var stack = new Game.Fluid.FluidStack(50.0, fluidId);
            hatch.AddLiquid(stack, Game.Fluid.FluidContainer.Empty);

            // 中継が進むまで tick を回す（テストmodパイプの flowCapacity=10 → 0.5/tick）
            // Tick until the relay propagates (test-mod pipe flowCapacity=10 → 0.5/tick)
            GameUpdater.RunFrames(10);

            // outflow 側パイプに流体が届いている
            // Fluid has arrived in the outflow-side pipe
            Assert.True(outflowPipe.TryGetComponent<Game.Block.Blocks.Fluid.IFluidInventory>(out var outflowInv));
            var outflowAmount = outflowInv.GetFluidInventory().Sum(f => f.Amount);
            Assert.Greater(outflowAmount, 0.0, "Relayed fluid reaches the outflow-side pipe");
        }
```

> `ForUnitTestModBlockId.FluidPipe` は実名（`FluidPipeId` ではない。実ファイルで確認済み）。流体IDの GUID `00000000-0000-0000-1234-000000000001` は既存 `FluidTest.FluidGuid` と同一（テスト mod の流体）。`FluidStack(amount, fluidId)` / `FluidContainer.Empty` / `IFluidInventory`（`Game.Block.Blocks.Fluid` 名前空間）も実コードで確認済み。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PipeHatch_RelaysFluidToOutflowSide"`
Expected: FAIL（スタブは中継しないので outflow 量 0。`AddLiquid` 未定義のコンパイルエラーの場合も本実装で解消）。

- [ ] **Step 3: CleanRoomPipeHatchComponent を本実装に置き換え**

`Game.Block/Blocks/CleanRoom/CleanRoomPipeHatchComponent.cs`：

```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Blocks.Fluid;
using Game.Fluid;
using Mooresmaster.Model.BlockConnectInfoModule;
using Newtonsoft.Json;

namespace Game.Block.Blocks.CleanRoom
{
    // 壁貫通パイプハッチ。inflow 面から受けた流体を内部コンテナに溜め、毎tick outflow 面へ push する
    // Wall-piercing pipe hatch: buffers fluid from the inflow face and pushes it to the outflow side each tick
    public class CleanRoomPipeHatchComponent : IFluidInventory, IUpdatableBlockComponent, IBlockSaveState
    {
        public string SaveKey => SaveKeyStatic;
        public static string SaveKeyStatic { get; } = typeof(CleanRoomPipeHatchComponent).FullName;

        private readonly FluidContainer _container;
        private readonly BlockConnectorComponent<IFluidInventory> _connector;

        public CleanRoomPipeHatchComponent(float capacity, BlockConnectorComponent<IFluidInventory> connector)
        {
            _container = new FluidContainer(capacity);
            _connector = connector;
        }

        // セーブからの復元: 内部流体の ID/量を戻す（FluidPipeComponent と同方式）
        // Restore from save: fluid id/amount of the inner container (same as FluidPipeComponent)
        public CleanRoomPipeHatchComponent(Dictionary<string, string> componentStates, float capacity, BlockConnectorComponent<IFluidInventory> connector)
            : this(capacity, connector)
        {
            if (!componentStates.TryGetValue(SaveKey, out var raw)) return;
            var json = JsonConvert.DeserializeObject<FluidPipeSaveJsonObject>(raw);
            if (json == null) return;
            _container.FluidId = json.FluidId;
            _container.Amount = json.Amount;
        }

        // inflow 面から受ける。ソース帰属は単純化し Empty で受ける
        // Accept on the inflow face; simplify source attribution by accepting with Empty
        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            BlockException.CheckDestroy(this);
            return _container.AddLiquid(fluidStack, FluidContainer.Empty);
        }

        public List<FluidStack> GetFluidInventory()
        {
            var list = new List<FluidStack>();
            if (_container.Amount > 0) list.Add(new FluidStack(_container.Amount, _container.FluidId));
            return list;
        }

        // 毎tick: 内部流体を接続先の IFluidInventory へ流量上限まで push する
        // Each tick: push the buffered fluid to connected IFluidInventory up to the flow cap
        public void Update()
        {
            BlockException.CheckDestroy(this);
            if (_container.Amount <= 0) { _container.ClearPreviousSources(); return; }

            DistributeToTargets();
            _container.ClearPreviousSources();
            if (_container.Amount <= 0) _container.FluidId = FluidMaster.EmptyFluidId;

            #region Internal

            void DistributeToTargets()
            {
                var targets = _connector.ConnectedTargets;
                if (targets.Count == 0) return;

                foreach (var kvp in targets)
                {
                    if (_container.Amount <= 0) break;
                    var maxFlow = GetMaxFlowRate(kvp.Value);
                    if (maxFlow <= 0) continue;

                    var sendAmount = Math.Min(_container.Amount, maxFlow);
                    var stack = new FluidStack(sendAmount, _container.FluidId);
                    var remain = kvp.Key.AddLiquid(stack, _container);
                    var accepted = sendAmount - remain.Amount;
                    _container.Amount -= accepted;
                }
            }

            // 自他のFlowCapacityの最小×1tick秒。FluidPipeComponent と同流儀
            // min(self,target FlowCapacity) * seconds-per-tick; same as FluidPipeComponent
            double GetMaxFlowRate(ConnectedInfo info)
            {
                var selfOption = info.SelfConnector?.ConnectOption as FluidConnectOption;
                var targetOption = info.TargetConnector?.ConnectOption as FluidConnectOption;
                if (selfOption == null || targetOption == null) return 0;
                return Math.Min(selfOption.FlowCapacity, targetOption.FlowCapacity) * GameUpdater.SecondsPerTick;
            }

            #endregion
        }

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var json = new FluidPipeSaveJsonObject
            {
                FluidIdValue = _container.FluidId.AsPrimitive(),
                Amount = (float)_container.Amount,
                Capacity = (float)_container.Capacity,
            };
            return JsonConvert.SerializeObject(json);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; }
    }
}
```

> 実コードで確認済み: `FluidConnectOption.FlowCapacity`／`ConnectedInfo.SelfConnector?.ConnectOption`／`FluidContainer(double capacity)`・`Amount`・`FluidId`・`Capacity`（readonly）・`ClearPreviousSources()`・`Empty`／`FluidMaster.EmptyFluidId`／`FluidPipeSaveJsonObject`（`fluidId`/`amount`/`capacity` の JsonProperty と `FluidId` 派生プロパティ）。outflow 面しか出力コネクタを持たないため、outflow 側のパイプからハッチへは接続されず ping-pong は構造的に起きない。

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。型未検出なら Unity 再起動。

- [ ] **Step 5: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PipeHatch_RelaysFluidToOutflowSide"`
Expected: PASS。

- [ ] **Step 6: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomPipeHatchComponent.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs
git commit -m "feat(cleanroom): パイプハッチの壁貫通流体中継を実装"
```

---

## Task 5: ドアハッチ — 通過バースト（peek/latch 分離）

`CleanRoomDoorHatchComponent` のスタブを本実装に置き換える。プレイヤー通過を離散メソッド `NotifyPlayerPassage()` で受けて `burst_door = 15` を `_incomingBurst` に溜め、**自前の `Update()`（latch）で `_pendingBurst` へ確定**する。データストアは `PeekPendingBurst()`（非破壊）で読むだけで、消費操作は存在しない（0.4）。ドアハッチはマーカーで密閉境界のまま（0.3）。

> **プレイヤー通過の seam（正直な注記）:** サーバー側に「プレイヤーがドアハッチを跨いだ」イベントは**存在しない**。プレイヤー座標はクライアントが `SetPlayerCoordinateProtocol`（`va:playerCoordinate`）でストリームし `IEntitiesDatastore.SetPosition` に入るのみ（実コードで確認済み）。座標を監視してドア通過を検出する watcher の実装は**本プランのスコープ外だが、明示的バックログ項目**として「フェーズ5完了の定義」と「スコープ外」節に記載する（宙吊りにしない）。テストは `NotifyPlayerPassage()` を直接呼んでバーストを検証する。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomDoorHatchComponent.cs`（スタブ→本実装）
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs`

- [ ] **Step 1: 失敗テスト（多重通過の合算 latch、peek 非破壊、1tick で失効、密閉維持）**

`CleanRoomIoTest` に追加：

```csharp
        // ドアハッチは通過を合算して次tickで latch し、peek は非破壊、さらに次の latch で 0 に戻る
        // The door hatch latches accumulated passages on the next tick; peek is non-destructive; the next latch clears it
        [Test]
        public void DoorHatch_PassageBurstLatchesForExactlyOneTick()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomDoorHatch, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var doorBlock);

            // ドアハッチは密閉境界マーカーを持つ（0.3 のドア整合）
            // The door hatch carries the sealing-boundary marker (door reconciliation §0.3)
            Assert.True(doorBlock.TryGetComponent<ICleanRoomBoundaryComponent>(out var marker));
            Assert.AreEqual(CleanRoomBoundaryKind.DoorHatch, marker.BoundaryKind);

            Assert.True(doorBlock.TryGetComponent<CleanRoomDoorHatchComponent>(out var door));

            // 2回通過 → latch 前は 0、latch 後は 2 * burst_door(15) = 30
            // Two passages → 0 before the latch, 30 (= 2 * 15) after
            door.NotifyPlayerPassage();
            door.NotifyPlayerPassage();
            Assert.AreEqual(0.0, door.PeekPendingBurst(), 1e-9, "Not visible until latched");

            GameUpdater.RunFrames(1);
            Assert.AreEqual(30.0, door.PeekPendingBurst(), 1e-9, "Latched for this tick");
            Assert.AreEqual(30.0, door.PeekPendingBurst(), 1e-9, "Peek is non-destructive");

            // 次の latch で 0（公開はちょうど1tick分）
            // Cleared by the next latch (visible for exactly one tick)
            GameUpdater.RunFrames(1);
            Assert.AreEqual(0.0, door.PeekPendingBurst(), 1e-9);
        }
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "DoorHatch_PassageBurstLatchesForExactlyOneTick"`
Expected: FAIL（スタブに `NotifyPlayerPassage`/`PeekPendingBurst` が無いのでコンパイルエラー → 本実装で解消）。

- [ ] **Step 3: CleanRoomDoorHatchComponent を本実装に置き換え**

`Game.Block/Blocks/CleanRoom/CleanRoomDoorHatchComponent.cs`：

```csharp
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom
{
    // クリーンルームのドアハッチ。通過バーストを溜め、自前tickのlatchで1tick分だけ公開する（peekは非破壊）
    // Clean-room door hatch: accumulates passage bursts and latches them for exactly one tick (peek is non-destructive)
    public class CleanRoomDoorHatchComponent : IUpdatableBlockComponent
    {
        // プレイヤー1通過あたりの瞬間加算量（balance §2: burst_door。個/通過＝A_totalに混ぜない）
        // Per-passage instantaneous addition (balance §2: burst_door; per-passage units, never folded into A_total)
        public const double DoorPassageBurst = 15.0;

        // tick間に発生した通過の累積（次の latch で pending へ移る）
        // Passages accumulated between ticks (moved into pending at the next latch)
        private double _incomingBurst;

        // 今tickに各部屋へ計上されるべきバースト（peek は非破壊）
        // Burst visible to room evaluation this tick (peek is non-destructive)
        private double _pendingBurst;

        // 統合seam: 将来の座標watcher/クライアント通知がプレイヤー通過時に呼ぶ。多重通過は合算
        // Integration seam: a future coordinate watcher / client notification calls this; multiple passages accumulate
        public void NotifyPlayerPassage()
        {
            _incomingBurst += DoorPassageBurst;
        }

        // データストアが部屋ごとに読む。非破壊なので面する全部屋が全額を計上できる（0.5 の共有境界規則）
        // Read per room by the datastore; non-destructive so every facing room books the full amount (§0.5)
        public double PeekPendingBurst()
        {
            return _pendingBurst;
        }

        // 自前tickの latch（=advance）。評価順に依存せず、公開は正確に1tick分・二重計上なし
        // Self-ticked latch (=advance): order-independent, visible for exactly one tick, never double-booked
        public void Update()
        {
            _pendingBurst = _incomingBurst;
            _incomingBurst = 0;
        }

        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; }
    }
}
```

> ドアハッチの保留バーストは**保存しない**（揮発。最大1tick分の通過が失われるだけで実害は無視できる）。`NotifyPlayerPassage` はプロトコル処理（tick間）から呼ばれる前提（0.4 の単一スレッド注記）。データストアとブロックの購読順がどちらでも、各バーストは「データストアの Update からちょうど1回観測される」（順序差はレイテンシ1tickにしかならない）。

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 5: テスト実行（ドア＋既出テストの回帰）**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(DoorHatch_PassageBurstLatchesForExactlyOneTick|ItemHatch_|PipeHatch_RelaysFluidToOutflowSide)"`
Expected: 全 PASS。

- [ ] **Step 6: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomDoorHatchComponent.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs
git commit -m "feat(cleanroom): ドアハッチの通過バースト(peek/latch分離)を実装"
```

---

## Task 6: GetAdjacentCleanRooms ＋ 汚染計量の配線（A_hatch はレート、バーストは N 直接加算）

2つの配線を行う。(1) **`CleanRoomDatastore.GetAdjacentCleanRooms(IBlock)` を新設**（0.5。境界ブロックは `Cells` に属さないため既存の部屋内クエリでは引けない）。(2) `CleanRoomPollutionCalculator`（フェーズ3）の A_total 算出に `A_hatch = k_hatch · Σ throughput` を合算し、`CleanRoomDatastore.Update` の部屋評価で隣接ドアハッチの `PeekPendingBurst()` 合計を `CleanRoom.AddImpurity` で **N へ直接加算**する（0.4）。

> **A_hatch と A_door の形の違い（確定）:** `A_hatch` は窓で減衰するレート（個/秒）で **A_total の項**。`A_door` はバースト（個/通過）で **A_total を経由せず N へ直接加算**。テストは A_total ではなく **N の増分**で検証する（calculator を tick 外から直接叩くテストは作らない。実運用と同じく `GameUpdater.RunFrames` でデータストアに評価させる）。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/Pollution/CleanRoomPollutionCalculator.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs`

- [ ] **Step 1: 失敗テストを書く**

このテストはフェーズ1〜3の成果物（`CleanRoomDatastore` / `CleanRoom` / 壁シェルヘルパ）に依存する。`BuildWallShell(world, min, max)` はフェーズ1テストの private ヘルパなので、本テストファイルへコピーする（フェーズ2プランと同じ方針）。部屋の参照は**毎回 `TryGetCleanRoomAt(室内セル)` で再取得**する（再検出で部屋インスタンスが入れ替わっても壊れないように）。

```csharp
        // 境界ハッチから「面する部屋」を引ける。境界セル自体は部屋に属さない
        // A boundary hatch resolves its facing room(s); the boundary cell itself belongs to no room
        [Test]
        public void AdjacentRooms_BoundaryHatchResolvesItsFacingRoom()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<Game.CleanRoom.CleanRoomDatastore>();

            // 壁シェル (0,0,0)-(4,4,4)。x=0 面の壁1枚 (0,2,2) をアイテムハッチに置換（出力+Xが室内向き＝搬入用）
            // Wall shell (0,0,0)-(4,4,4); replace one x=0 wall block with an item hatch (output +X faces inside = import)
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            ReplaceWith(world, new Vector3Int(0, 2, 2), ForUnitTestModBlockId.CleanRoomItemHatch);
            GameUpdater.RunFrames(2);

            Assert.True(world.TryGetBlock(new Vector3Int(0, 2, 2), out IBlock hatchBlock));

            // 境界セルは部屋に属さない（部屋内クエリは false）
            // The boundary cell belongs to no room (the in-room query returns false)
            Assert.False(datastore.TryGetCleanRoomAt(new Vector3Int(0, 2, 2), out _));

            // 面する部屋はちょうど1つで、室内セルの部屋と一致する
            // Exactly one facing room, identical to the interior cell's room
            var adjacent = datastore.GetAdjacentCleanRooms(hatchBlock);
            Assert.AreEqual(1, adjacent.Count);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room));
            Assert.AreSame(room, adjacent[0]);
        }

        // 搬入ハッチの搬送が続く窓では、無搬送窓より N の増分が大きい（A_hatch がレートとして効く）
        // While the import hatch keeps relaying, N grows faster than in the idle window (A_hatch as a rate term)
        [Test]
        public void Pollution_ImportHatchThroughputRaisesImpurity()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<Game.CleanRoom.CleanRoomDatastore>();

            // x=0 面ハッチ＋室内チェスト (1,2,2)（ハッチ出力+X の先）
            // Hatch on the x=0 face + interior chest at (1,2,2) (ahead of the hatch's +X output)
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            ReplaceWith(world, new Vector3Int(0, 2, 2), ForUnitTestModBlockId.CleanRoomItemHatch);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(1, 2, 2),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            world.TryGetBlock(new Vector3Int(0, 2, 2), out IBlock hatchBlock);
            Assert.True(hatchBlock.TryGetComponent<CleanRoomItemHatchComponent>(out var hatch));
            GameUpdater.RunFrames(2);

            // 無搬送の10tick窓の N 増分（恒常項のみ）
            // N delta over a 10-tick idle window (continuous terms only)
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room0));
            var n0 = room0.ImpurityCount;
            GameUpdater.RunFrames(10);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room1));
            var idleDelta = room1.ImpurityCount - n0;

            // 毎tick1個搬入する10tick窓の N 増分
            // N delta over a 10-tick window with one item relayed per tick
            var n1 = room1.ImpurityCount;
            for (var i = 0; i < 10; i++)
            {
                hatch.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 1), InsertItemContext.Empty);
                GameUpdater.RunFrames(1);
            }
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room2));
            var activeDelta = room2.ImpurityCount - n1;

            // A_hatch ≈ k_hatch(0.30) × スループット の分だけ増分が上回る（窓の遅延を踏まえ下限で判定）
            // The delta exceeds idle by ~k_hatch × throughput (lower-bounded due to window lag)
            Assert.Greater(activeDelta, idleDelta + 1.0, "A_hatch raises N while throughput is positive");
        }

        // 搬出ハッチ（max-X 面・入力が室内向き）でもスループットが N に計上される（0.8 の向き仕様）
        // An export hatch (max-X face, input facing inside) also books its throughput into N (§0.8)
        [Test]
        public void Pollution_ExportHatchThroughputAlsoRaisesImpurity()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<Game.CleanRoom.CleanRoomDatastore>();

            // x=4 面の壁1枚 (4,2,2) をハッチに置換（入力−X が室内向き＝搬出用）。室内チェストが自動 push する
            // Replace one x=4 wall block with a hatch (input −X faces inside = export); the interior chest auto-pushes
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            ReplaceWith(world, new Vector3Int(4, 2, 2), ForUnitTestModBlockId.CleanRoomItemHatch);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(3, 2, 2),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var innerChest);
            world.TryAddBlock(ForUnitTestModBlockId.ChestId, new Vector3Int(5, 2, 2),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            GameUpdater.RunFrames(2);

            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room0));
            var n0 = room0.ImpurityCount;
            GameUpdater.RunFrames(10);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room1));
            var idleDelta = room1.ImpurityCount - n0;

            // 室内チェストへアイテムを補充 → チェストがハッチへ自動 push → ハッチが外チェストへ中継
            // Stock the interior chest → it auto-pushes into the hatch → the hatch relays outward
            Assert.True(innerChest.TryGetComponent<IBlockInventory>(out var chestInv));
            var n1 = room1.ImpurityCount;
            chestInv.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 10));
            GameUpdater.RunFrames(10);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room2));
            var activeDelta = room2.ImpurityCount - n1;

            Assert.Greater(activeDelta, idleDelta + 0.5, "Export throughput also counts toward A_hatch");
        }

        // 通過1回でちょうど burst_door(15) が N へ加算され、以降のtickで再加算されない
        // One passage adds exactly burst_door (15) to N, with no re-addition on later ticks
        [Test]
        public void Pollution_DoorPassageAddsBurstToImpurityExactlyOnce()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<Game.CleanRoom.CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            ReplaceWith(world, new Vector3Int(0, 2, 2), ForUnitTestModBlockId.CleanRoomDoorHatch);
            world.TryGetBlock(new Vector3Int(0, 2, 2), out IBlock doorBlock);
            Assert.True(doorBlock.TryGetComponent<CleanRoomDoorHatchComponent>(out var door));
            GameUpdater.RunFrames(2);

            // 無通過の2tick窓の増分（恒常項のみ。除去0なので毎tick一定）
            // Delta over an idle 2-tick window (continuous terms only; constant per tick with zero removal)
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room0));
            var n0 = room0.ImpurityCount;
            GameUpdater.RunFrames(2);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room1));
            var idleDelta = room1.ImpurityCount - n0;

            // 通過1回 → 2tick以内に latch→計上が完了し、増分 = idleDelta + 15
            // One passage → latch + booking complete within 2 ticks; delta = idleDelta + 15
            var n1 = room1.ImpurityCount;
            door.NotifyPlayerPassage();
            GameUpdater.RunFrames(2);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room2));
            var burstDelta = room2.ImpurityCount - n1;
            Assert.AreEqual(15.0, burstDelta - idleDelta, 1e-6, "Exactly burst_door lands in N");

            // さらに2tick → 増分は恒常項のみ（バーストの二重計上なし）
            // Two more ticks → only the continuous terms (no double-booking of the burst)
            var n2 = room2.ImpurityCount;
            GameUpdater.RunFrames(2);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var room3));
            Assert.AreEqual(idleDelta, room3.ImpurityCount - n2, 1e-6, "No re-addition on later ticks");
        }

        // 2部屋の共有境界にあるドアハッチは、面する両部屋へ全額バーストを加算する（0.5 の確定規則）
        // A door hatch on a shared boundary books the full burst into every facing room (§0.5)
        [Test]
        public void Pollution_SharedBoundaryDoorBurstsAllFacingRooms()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<Game.CleanRoom.CleanRoomDatastore>();

            // x=4 平面を共有する2つの壁シェル。共有壁の1枚 (4,2,2) をドアハッチに置換
            // Two shells sharing the x=4 plane; replace one shared-wall block (4,2,2) with a door hatch
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            BuildWallShell(world, new Vector3Int(4, 0, 0), new Vector3Int(8, 4, 4));
            ReplaceWith(world, new Vector3Int(4, 2, 2), ForUnitTestModBlockId.CleanRoomDoorHatch);
            world.TryGetBlock(new Vector3Int(4, 2, 2), out IBlock doorBlock);
            Assert.True(doorBlock.TryGetComponent<CleanRoomDoorHatchComponent>(out var door));
            GameUpdater.RunFrames(2);

            // 面する部屋は2つ
            // The door faces two rooms
            Assert.AreEqual(2, datastore.GetAdjacentCleanRooms(doorBlock).Count);

            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var roomA0));
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(6, 2, 2), out var roomB0));
            var a0 = roomA0.ImpurityCount;
            var b0 = roomB0.ImpurityCount;
            GameUpdater.RunFrames(2);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var roomA1));
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(6, 2, 2), out var roomB1));
            var idleDeltaA = roomA1.ImpurityCount - a0;
            var idleDeltaB = roomB1.ImpurityCount - b0;

            // 通過1回 → 両部屋とも +15（全額。按分しない）
            // One passage → both rooms gain the full +15 (no splitting)
            var a1 = roomA1.ImpurityCount;
            var b1 = roomB1.ImpurityCount;
            door.NotifyPlayerPassage();
            GameUpdater.RunFrames(2);
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out var roomA2));
            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(6, 2, 2), out var roomB2));
            Assert.AreEqual(15.0, (roomA2.ImpurityCount - a1) - idleDeltaA, 1e-6, "Room A books the full burst");
            Assert.AreEqual(15.0, (roomB2.ImpurityCount - b1) - idleDeltaB, 1e-6, "Room B books the full burst");
        }
```

> `BuildWallShell` / `ReplaceWith`（壁1枚を撤去して別ブロックを設置）はフェーズ1テストのヘルパをこのファイルへコピーする。`ForUnitTestModBlockId.CleanRoomDoorHatch` / `CleanRoomItemHatch` はフェーズ1改訂版のテスト mod 定義名。室内チェストの自動 push は `VanillaChestComponent`＋`ConnectingInventoryListPriorityInsertItemService`（実コードで確認済み）の既存挙動を利用する。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(AdjacentRooms_|Pollution_)"`
Expected: FAIL（`GetAdjacentCleanRooms` 未定義／A_hatch・バースト未配線）。

- [ ] **Step 3: CleanRoomDatastore に GetAdjacentCleanRooms とバースト計上を実装**

`Game.CleanRoom/CleanRoomDatastore.cs` に追加（実装はフェーズ2の実コードの構造に合わせて組み込む。骨子）：

```csharp
        // 境界ブロックの占有セルの6近傍を部屋セルマップに照合し、面する部屋を重複なしで返す
        // Resolve the rooms facing a boundary block via the 6-neighbors of its occupied cells (deduplicated)
        public IReadOnlyList<CleanRoom> GetAdjacentCleanRooms(IBlock boundaryBlock)
        {
            var result = new List<CleanRoom>();
            foreach (var cell in EnumerateOccupiedCells(boundaryBlock)) // BlockPositionInfo.MinPos..MaxPos
            foreach (var neighbor in SixNeighbors(cell))
            {
                if (!TryGetCleanRoomAt(neighbor, out var room)) continue;
                if (!result.Contains(room)) result.Add(room);
            }
            return result;
        }
```

tick（`CleanRoomDatastore.Update`）の部屋評価に、**dN 積分（フェーズ2）とは別の行として**バースト計上を足す：

```csharp
            // ドアハッチの通過バーストは A_total を経由せず N へ直接加算（balance §2 の単位注意）
            // Door-passage bursts go straight into N, never through A_total (unit note in balance §2)
            var burst = 0.0;
            foreach (var door in EnumerateAdjacentDoorHatches(room)) // 境界走査＋BlockInstanceId重複排除
                burst += door.PeekPendingBurst();
            if (burst > 0) room.AddImpurity(burst);
```

> `EnumerateAdjacentDoorHatches(room)` は「部屋の `Cells` の6近傍 → `ICleanRoomBoundaryComponent` 持ちブロック → `TryGetComponent<CleanRoomDoorHatchComponent>`」で集める。**フェーズ3の接続点集計（`a_connector`）が同じ境界走査を既にやっているので、その走査ループに相乗りして二重走査を避ける**こと。重複排除は `BlockInstanceId` 単位（balance §2）。peek は非破壊なので、共有境界のドアは面する各部屋の評価で全額計上される（0.5）。

- [ ] **Step 4: CleanRoomPollutionCalculator に A_hatch を合算**

`Game.CleanRoom/Pollution/CleanRoomPollutionCalculator.cs` の A_total 算出（フェーズ3の実装。境界走査で `a_connector` を数えている箇所）に `A_hatch` を足す（骨子。実メソッド名はフェーズ3成果物の実ファイルに合わせる）：

```csharp
        // バランス §2: A_hatch = k_hatch × Σ(隣接アイテムハッチの直近スループット)
        // Balance §2: A_hatch = k_hatch × Σ(recent throughput of adjacent item hatches)
        private const double KHatch = 0.30;

        // 既存の境界走査（接続点集計）と同一ループで集計し、BlockInstanceId で重複排除する
        // Computed in the same boundary scan as the connector count, deduplicated by BlockInstanceId
        // foreach (境界ブロック block in 部屋の境界走査)
        // {
        //     if (block.TryGetComponent<CleanRoomItemHatchComponent>(out var hatch))
        //         aHatch += KHatch * hatch.RecentThroughputPerSecond;
        // }
```

> `Game.CleanRoom` asmdef が `Game.Block`（CleanRoom コンポーネント型）を参照できることを確認（フェーズ3が稼働機械判定のため既に参照している見込み。無ければ追加）。共有境界のアイテムハッチは面する各部屋の走査に現れるため、`A_hatch` は**両部屋に計上**される（0.5 の確定規則どおり。バグではない）。

- [ ] **Step 5: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 6: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(AdjacentRooms_|Pollution_)"`
Expected: 全 PASS。

- [ ] **Step 7: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/ moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs
git commit -m "feat(cleanroom): GetAdjacentCleanRoomsとA_hatch/ドアバーストの計上を配線"
```

---

## Task 7: 永続化仕上げ — I/O state round-trip ＋ CleanRoomSaveData の非回帰

I/O 固有状態（アイテムハッチ中継中アイテム・パイプハッチ内流体）を `IBlockSaveState` で round-trip させ、グローバルセーブスキーマを触らずに完結することを固定する。あわせて I/O ブロックが部屋境界に在っても `CleanRoomSaveData`（N/thresholdIndex/status/猶予残/Cells）が round-trip することを確認する。

> **確定事項:**
> - **コネクタ re-link に IPostBlockLoad は不要。** `BlockConnectorComponent` はコンストラクタで `WorldBlockUpdateEvent` を購読し、既存隣接があればその場で接続する（実コードで確認済み。`FluidPipeComponent` が IPostBlockLoad 無しでロード後も繋がるのと同じ）。
> - **`CleanRoomSaveData` は変更不要の見込み。** I/O state は各ブロックの `IBlockSaveState` に載るため、純度セーブは codemap §1.3 の5フィールド（impurityCount / thresholdIndex / status / graceRemainingSeconds / cells）のまま。これを**テストで証明**し、テストが強制しない限り `CleanRoomSaveData` は改変しない。
> - **非回帰テストの検証順序（重要）**: ロード直後（`RunFrames` を挟まずに）N/thresholdIndex/status の一致を厳密にアサートする。その後 `RunFrames(1)` して「リセットされない」ことを別途アサートする（恒常汚染で N が僅かに増えるのは正常なので、ここは**厳密一致ではなく許容窓**で判定する。balance §6 の「ロード→1tick→維持」非回帰）。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs`
- （必要が判明した場合のみ）Modify: 各 I/O コンポーネントの `GetSaveState`/復元コンストラクタ

- [ ] **Step 1: 失敗テスト（ハッチ中継中アイテムの save/load round-trip）**

`CleanRoomIoTest` に追加。セーブ/ロードの実 API は `AssembleSaveJsonText.AssembleSaveJson()`（→ JSON 文字列）と `WorldLoaderFromJson.Load(string)`（実コードで確認済み）：

```csharp
        // ハッチが中継待ちアイテムを保持した状態でsave→loadし、アイテムが復元される
        // Save while the hatch holds in-transit items, then load and confirm they are restored
        [Test]
        public void ItemHatch_InTransitItemsSurviveSaveLoad()
        {
            // --- セーブ側ワールド ---
            var (_, providerA) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldA = ServerContext.WorldBlockDatastore;

            // ターゲットを置かず、ハッチに中継待ちを溜めたまま保存する（中継が完了しないように）
            // Save with no target so items stay in-transit (relay cannot complete)
            worldA.TryAddBlock(ForUnitTestModBlockId.CleanRoomItemHatch, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var hatchA);
            Assert.True(hatchA.TryGetComponent<CleanRoomItemHatchComponent>(out var hatchCompA));
            hatchCompA.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId1, 3), InsertItemContext.Empty);
            GameUpdater.RunFrames(2); // ターゲット無し → バッファに残る

            var json = providerA.GetService<Game.SaveLoad.Json.AssembleSaveJsonText>().AssembleSaveJson();

            // --- ロード側ワールド ---
            var (_, providerB) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            providerB.GetService<Game.SaveLoad.Json.WorldLoaderFromJson>().Load(json);

            var worldB = ServerContext.WorldBlockDatastore;
            Assert.True(worldB.TryGetBlock(new Vector3Int(0, 0, 0), out IBlock hatchB));
            Assert.True(hatchB.TryGetComponent<CleanRoomItemHatchComponent>(out var hatchCompB));

            // 復元後、中継待ちの3個が残っている
            // After load, the 3 in-transit items are restored
            var restored = 0;
            for (var i = 0; i < hatchCompB.GetSlotSize(); i++) restored += hatchCompB.GetItem(i).Count;
            Assert.AreEqual(3, restored);
        }
```

> `ServerContext.WorldBlockDatastore` は DI コンテナ生成ごとに差し替わる点に注意（既存 SaveLoad テストの流儀を踏襲）。

- [ ] **Step 2: パイプハッチの流体 round-trip テストを追加**

```csharp
        // パイプハッチが内部流体を保持した状態でsave→loadし、量が復元される
        // Save while the pipe hatch holds fluid, then load and confirm the amount is restored
        [Test]
        public void PipeHatch_BufferedFluidSurvivesSaveLoad()
        {
            var (_, providerA) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldA = ServerContext.WorldBlockDatastore;
            worldA.TryAddBlock(ForUnitTestModBlockId.CleanRoomPipeHatch, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var pipeA);
            Assert.True(pipeA.TryGetComponent<CleanRoomPipeHatchComponent>(out var compA));

            var fluidId = Core.Master.MasterHolder.FluidMaster.GetFluidId(new Guid("00000000-0000-0000-1234-000000000001"));
            compA.AddLiquid(new Game.Fluid.FluidStack(40.0, fluidId), Game.Fluid.FluidContainer.Empty);
            // 接続先なし・tickも進めない → 内部コンテナに 40 が残ったまま
            // No target, no ticks → 40 stays in the inner container

            var json = providerA.GetService<Game.SaveLoad.Json.AssembleSaveJsonText>().AssembleSaveJson();

            var (_, providerB) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            providerB.GetService<Game.SaveLoad.Json.WorldLoaderFromJson>().Load(json);

            Assert.True(ServerContext.WorldBlockDatastore.TryGetBlock(new Vector3Int(0, 0, 0), out IBlock pipeB));
            Assert.True(pipeB.TryGetComponent<CleanRoomPipeHatchComponent>(out var compB));
            Assert.AreEqual(40.0, compB.GetFluidInventory().Sum(f => f.Amount), 1e-6);
        }
```

- [ ] **Step 3: 実行して失敗を確認・必要なら復元経路を修正**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(ItemHatch_InTransitItemsSurviveSaveLoad|PipeHatch_BufferedFluidSurvivesSaveLoad)"`
Expected: FAIL の可能性（テンプレ Load 経路が `componentStates` を復元コンストラクタへ渡していなければ落ちる）。落ちたら `VanillaCleanRoomBoundaryTemplate.Build` の Load 分岐（`componentStates` 非null時に復元コンストラクタを使う）を直す。`SaveKey` の一意性（`typeof(...).FullName`）でグローバルセーブ JSON に衝突なく載ることを確認。

- [ ] **Step 4: 純度セーブ（CleanRoomSaveData）の非回帰テストを追加**

I/O ブロックを含む部屋で `CleanRoomSaveData` が round-trip することを確認する。**N に加えて thresholdIndex / status をアサート**し、**ロード直後（RunFrames なし）に厳密一致 → その後1tick進めて「リセットされない」**の2段で検証する：

```csharp
        // I/Oブロックが境界に在っても純度セーブ(CleanRoomSaveData)はそのまま round-trip する（スキーマ改変不要）
        // Purity save (CleanRoomSaveData) round-trips even with I/O blocks on the boundary (no schema change)
        [Test]
        public void CleanRoomSave_RoundTripsWithIoBlocksPresent()
        {
            // --- セーブ側: 壁シェルの1面をハッチに置換した密閉部屋を作り、N をシードする ---
            // Save side: a sealed room with one wall face replaced by a hatch; seed N
            var (_, providerA) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var worldA = ServerContext.WorldBlockDatastore;
            var datastoreA = providerA.GetService<Game.CleanRoom.CleanRoomDatastore>();

            BuildWallShell(worldA, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            ReplaceWith(worldA, new Vector3Int(0, 2, 2), ForUnitTestModBlockId.CleanRoomItemHatch);
            GameUpdater.RunFrames(2);

            var insideCell = new Vector3Int(2, 2, 2);
            Assert.True(datastoreA.TryGetCleanRoomAt(insideCell, out var roomA));

            // 既知の N をシードし、1tick回して閾値行/状態を最新化してから期待値を採取する
            // Seed a known N, run one tick so threshold/status settle, then capture expectations
            roomA.AddImpurity(123.0);
            GameUpdater.RunFrames(1);
            Assert.True(datastoreA.TryGetCleanRoomAt(insideCell, out var roomA2));
            var expectedN = roomA2.ImpurityCount;
            var expectedThresholdIndex = roomA2.ThresholdIndex;
            var expectedStatus = roomA2.Status;

            var json = providerA.GetService<Game.SaveLoad.Json.AssembleSaveJsonText>().AssembleSaveJson();

            // --- ロード側: RunFrames を挟まずロード直後に厳密一致を検証する ---
            // Load side: assert exact equality right after Load, before any RunFrames
            var (_, providerB) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            providerB.GetService<Game.SaveLoad.Json.WorldLoaderFromJson>().Load(json);
            var datastoreB = providerB.GetService<Game.CleanRoom.CleanRoomDatastore>();

            Assert.True(datastoreB.TryGetCleanRoomAt(insideCell, out var roomB));
            Assert.AreEqual(expectedN, roomB.ImpurityCount, 1e-6, "N restored exactly (no ticks yet)");
            Assert.AreEqual(expectedThresholdIndex, roomB.ThresholdIndex, "Threshold row restored");
            Assert.AreEqual(expectedStatus, roomB.Status, "Status restored");

            // --- 1tick 進めてもリセットされない（dirty 再々検出事故の非回帰。balance §6）---
            // One tick later, nothing resets (non-regression for the dirty re-detection accident; balance §6)
            GameUpdater.RunFrames(1);
            Assert.True(datastoreB.TryGetCleanRoomAt(insideCell, out var roomB2));
            // 恒常汚染で僅かに増えるのは正常。リセット(→~0)や二重復元(→~2倍)を弾く許容窓で判定する
            // A slight increase from continuous pollution is normal; the window rejects resets (~0) and double-restores (~2x)
            Assert.GreaterOrEqual(roomB2.ImpurityCount, expectedN - 1e-6, "N not reset by the first tick");
            Assert.Less(roomB2.ImpurityCount, expectedN + 2.0, "No reset/double-restore sized jump");
            Assert.AreEqual(expectedThresholdIndex, roomB2.ThresholdIndex, "Threshold row survives the first tick");
        }
```

> `CleanRoom.ThresholdIndex` / `Status` / `AddImpurity`、純度復元が `WorldLoaderFromJson.Load` 内の「ブロック後」フック（`LoadBlockDataList` → 検出 → `CleanRoomDatastore.Restore`、codemap §1.3）で走ることは、フェーズ2成果物の実シグネチャに合わせて確認・調整する。**結論: テストが緑なら `CleanRoomSaveData` は5フィールドのまま変更不要。** 許容窓 `+2.0` は基準部屋級の A_total（数個/秒）× 0.05秒 を大きく上回り、かつシード値 123 のリセット/二重復元とは確実に区別できる値。

- [ ] **Step 5: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(ItemHatch_InTransitItemsSurviveSaveLoad|PipeHatch_BufferedFluidSurvivesSaveLoad|CleanRoomSave_RoundTripsWithIoBlocksPresent)"`
Expected: 全 PASS。

- [ ] **Step 6: フェーズ5全テスト＋既存回帰**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"`
Expected: 全 PASS。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(GearBeltConveyor|Fluid|SaveLoad)Test"`
Expected: 従来どおり PASS（I/O 追加・セーブ流用が既存を壊していない）。

- [ ] **Step 7: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/ moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomIoTest.cs
git commit -m "test(cleanroom): I/O state round-tripとCleanRoomSaveData非回帰を固定しフェーズ5完了"
```

---

## フェーズ5 完了の定義（Definition of Done）

- アイテム/パイプハッチに I/O コネクタ param がスキーマ・テスト mod まで通り、設置できる。
- `CleanRoomItemHatchComponent` が入力面→出力面へアイテムを中継し、`RecentThroughputPerSecond`（窓 = 20tick = 1秒）を公開する。**バッファ上限 `MaxInTransitStacks=4` で満杯時は受け取りを拒否**し（`InsertionCheck=false`・差し戻し）、搬送停止後は窓1周でレートが 0 へ減衰する。中継待ちアイテムが save/load で復元される。
- `CleanRoomPipeHatchComponent` が inflow→outflow へ流体を中継する（push 型 `Update`、codemap §5 のシグネチャどおり）。内部流体が save/load で復元される。
- `CleanRoomDoorHatchComponent` が `NotifyPlayerPassage()` で `burst_door=15` を合算し、**peek（非破壊）／latch（自前tick）の二相**で正確に1tick分だけ公開する。ドアハッチは密閉境界マーカーを保持（フェーズ1整合）。
- `VanillaCleanRoomBoundaryTemplate` が kind 別（Wall / DoorHatch / ItemHatch / PipeHatch）に密閉マーカー＋I/O挙動を New/Load で合成する。実装順序はスタブ方式（コメントアウト運用なし）。
- **`CleanRoomDatastore.GetAdjacentCleanRooms(IBlock)` が境界ブロックから面する部屋（複数あり得る）を返す**。境界セルが部屋に属さないこと（部屋内クエリ false）もテストで固定。
- `CleanRoomPollutionCalculator` が `A_hatch = k_hatch·Σ throughput` を A_total に取り込む（搬入・搬出どちらの向きでも計上）。**ドアバーストは A_total を経由せず `CleanRoom.AddImpurity` で N へ直接加算され、通過1回で N がちょうど +15、以降の再加算なし**をテストで固定。共有境界では `A_hatch` は面する各部屋に、バーストは面する全部屋へ全額計上される。
- `CleanRoomSaveData`（impurityCount/thresholdIndex/status/graceRemainingSeconds/cells）は I/O ブロック在室でも**改変なし**で round-trip し、**N に加えて thresholdIndex/status の復元**と「ロード→1tick→非リセット」をテストで固定。コネクタ再リンクは `BlockConnectorComponent` の自動接続に任せ、IPostBlockLoad は不要。
- 既存テスト（belt/fluid/saveload）が非回帰。
- **（完了条件外・明示的バックログ）** ドア通過のゲームプレイ発火（下記スコープ外参照）は本フェーズに含まれないが、後続タスクとして起票済みであること（宙吊り禁止）。

## フェーズ5で意図的にスコープ外とした事項

- **プレイヤー通過の自動発火（明示的バックログ・担当未割当のまま放置しない）**: 本プランは seam（`NotifyPlayerPassage()`）まで。**このままでは `A_door` はゲームプレイで一度も発火しない**ため、フェーズ5マージ時に「ドア通過検出」を独立タスクとして起票する。実装候補は (a) サーバー側でプレイヤー座標ストリーム（`SetPlayerCoordinateProtocol`/`IEntitiesDatastore`）を監視してドアセル跨ぎを検出する watcher、(b) クライアントが通過を判定してサーバーへ通知する専用プロトコル（`creating-server-protocol` スキル参照）。どちらも `NotifyPlayerPassage()` を呼ぶだけで本フェーズの成果物に変更は不要。
- **ハッチのスループット上限の数値調整**: 本フェーズで `MaxInTransitStacks=4` の構造（満杯拒否→上流停滞）は入れた。バランス調整（スタック数・レート上限の追加）はプレイテスト後。
- 本番 mod（moorestech_master）の blocks.json への I/O ブロック配線・モデル/画像アセット。
- 高度なソース帰属（パイプの per-source バケット。本ハッチは Empty 帰属で簡略化）。

---

## Self-Review

**codemap §5 / バランス確定書との整合（v2 契約名で全面確認）:**
- ブロック/kind/コンポーネント名: `CleanRoomDoorHatch`/`CleanRoomItemHatch`/`CleanRoomPipeHatch`、`CleanRoomBoundaryKind { Wall, DoorHatch, ItemHatch, PipeHatch }`、`CleanRoomItemHatchComponent`/`CleanRoomPipeHatchComponent`/`CleanRoomDoorHatchComponent` を verbatim 使用。旧名（Door/PipeConnector 系、`CleanRoomDetectionSystem`/`CleanRoomPurityService`/`CleanRoomPuritySaveData`/`CleanRoomPollutionInput`）は 0.1 の廃名警告以外に登場しない ✓
- `CleanRoomPipeHatchComponent` のシグネチャは codemap §5 どおり `IFluidInventory, IUpdatableBlockComponent, IBlockSaveState`（push 型。codemap §7.9 にも明記）✓
- 中核は `CleanRoomDatastore`、A_total は `Game.CleanRoom/Pollution/CleanRoomPollutionCalculator`（具体クラス・注入IFなし）✓
- セーブは `CleanRoomSaveData`（5フィールド）。本フェーズは改変せず、非回帰テストで N＋thresholdIndex＋status を固定 ✓

**批判的レビュー指摘の織り込み:**
- must-fix 1（ドアバーストの単位バグ）: 0.4 で規律化。バーストは `AddImpurity` 直接加算・A_total 不経由。テストは「N が +15」を直接アサート（`Pollution_DoorPassageAddsBurstToImpurityExactlyOnce`）✓
- must-fix 2（境界ブロックへの部屋内クエリ誤用）: `GetAdjacentCleanRooms` を Task 6 で実装。境界セルが部屋に属さないこと自体もテスト化（`AdjacentRooms_BoundaryHatchResolvesItsFacingRoom`）✓
- must-fix 3（codemap との二重正）: 本改訂で codemap v2 を契約（正）として全名称・配置を統一。0.1 に契約表と廃名警告 ✓
- must-fix 4（ロード後 RunFrames による N ドリフトで等値アサート崩壊）: Task 7 Step 4 を「ロード直後に厳密一致 → 1tick 後は許容窓で非リセット確認」の2段に修正 ✓
- should-fix 5（共有境界）: 0.5 で確定（a_connector/A_hatch は面する各部屋、バーストは面する全部屋へ全額）。`Pollution_SharedBoundaryDoorBurstsAllFacingRooms` で固定 ✓
- should-fix 6（ドア通過検知の宙吊り）: DoD と「スコープ外」に明示的バックログとして記載、実装候補2案を提示 ✓
- should-fix 7（計算APIの別名乱立）: `CalculateTotalPollution` 等の独自名を排除。汚染テストは calculator を直接叩かず、データストアの tick 経由で N を観測（peek/latch とも整合し、tick外破壊読みの問題自体が消滅）。calculator 側はフェーズ3実ファイルの既存メソッドへの「合算指示」のみ ✓
- should-fix 8（セーブデータの誤要約）: 「N＋Cells」表記を廃し5フィールドで記述。thresholdIndex/status をアサート ✓
- should-fix 9（V 定義の食い違い）: 0.6 で確定値（Cells=占有込み・Volume=空セルのみ、balance §5）を引用。フェーズ1も並行改訂中 ✓
- should-fix 10（tick外の破壊的読み出し）: peek/latch 分離により破壊的読み出しが存在しなくなった。テストも tick 経由 ✓
- consider 11: `ForUnitTestModBlockId.FluidPipe`（実名）・流体IDは `MasterHolder.FluidMaster.GetFluidId(guid)` 流儀（`FluidTest.FluidGuid` と同一GUID）に修正 ✓
- consider 12: ハッチ単方向（入力面→出力面）の設計判断を 0.8 で明文化し、搬出向きの計上テスト（`Pollution_ExportHatchThroughputAlsoRaisesImpurity`）を追加 ✓
- consider 13: `MaxInTransitStacks=4` のバッファ上限＋満杯拒否を実装・テスト（`ItemHatch_RejectsWhenInTransitBufferIsFull`）✓
- consider 14: レート窓減衰テスト（`ItemHatch_ThroughputDecaysToZeroAfterIdleWindow`）✓
- consider 15: テンプレ合成は空実装スタブ方式に一本化（コメントアウト二択を廃止、0.7）✓

**design §5/§7/§9 の網羅:**
- §5 汚染源: `A_hatch` はレート換算（1個ごと加算でない。スタック個数ベースで集計するためスタックサイズでの回避も不可）✓、`A_door` は通過バーストの瞬間加算（N 直接・単位正しく15）✓、`a_connector` は接続点数（フェーズ3計上・BlockInstanceId 重複排除）✓
- §7 I/O 役割分担: アイテムハッチ=低スループット（バッファ上限で実挙動化）＋汚染源、パイプハッチ=流体、ドアハッチ=人で汚染大 → 3コンポーネントで実装 ✓
- §9 アイテム外部取り出しで物が変化しない: I/O は**アイテム本体を一切改変せず中継するのみ**（`InsertItem`/`AddLiquid` をそのまま転送、グレード等の書き換えなし）。純度はアイテムに焼き込まれない（製造の瞬間に部屋属性として参照されるのみ。フェーズ4の責務）✓

**実コード接地（本プラン執筆時に開いて確認したシグネチャ）:** `InsertItemContext.Empty`／`ConnectedInfo.SelfConnector`・`TargetConnector`／`BlockConnectorComponent(BlockConnectInfo, BlockConnectInfo, BlockPositionInfo)` とコンストラクタでの既存隣接接続／`BlockTemplateUtil.CreateInventoryConnector`／`IFluidInventory.CreateFluidInventoryConnector`／`FluidPipeSaveJsonObject`（fluidId/amount/capacity）／`FluidContainer`（Amount/FluidId 可変・Capacity readonly・ClearPreviousSources・Empty）／`FluidMaster.EmptyFluidId`／`GameUpdater.SecondsPerTick`・`RunFrames(uint)`／`BlockSystem` コンストラクタ／`IBlockTemplate.New/Load`／`AssembleSaveJsonText.AssembleSaveJson()`・`WorldLoaderFromJson.Load(string)`／`ForUnitTestModBlockId.FluidPipe`・`ChestId`／`ForUnitTestItemId.ItemId1`／`FluidTest.FluidGuid`／テスト mod の `inventoryConnectors`/`fluidInventoryConnectors` JSON 書式／チェストの自動 push（`ConnectingInventoryListPriorityInsertItemService`）。フェーズ1〜4成果物の名前は契約（codemap v2）準拠で、未マージのため実装時に実ファイルで最終確認する。

**プレースホルダ走査:** コード本体に未確定値なし。`k_hatch=0.30`/`burst_door=15`/窓=20tick/`MaxInTransitStacks=4`/容量100f は具体値。残る `<...>` は Task 2 の新規GUID（実装時に採番）のみで、これは捏造防止の意図的な穴。
