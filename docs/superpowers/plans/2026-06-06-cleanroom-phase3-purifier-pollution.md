# クリーンルーム フェーズ3（エアフィルターブロック＋フィルター＋電力＋汚染源）実装プラン

> **改訂: 2026-06-12 — codemap v2 整合+批判的レビュー反映**
> - ブロック名を `CleanRoomAirFilter` に変更（旧 `CleanRoomAirPurifier` 廃名）。コンポーネントは codemap §3 の**単一コンポーネント初版** `CleanRoomAirFilterComponent`。
> - 中核は `CleanRoomDatastore`（旧 `CleanRoomPurityService` 廃止）。`CleanRoomPollutionInput` 注入インターフェースは廃止し、A_total は具体ヘルパ `CleanRoomPollutionCalculator` でデータストアが直接算出。
> - スキーマ: `requiredPower`（既存規約名）・`filterCapacity`/`filterItemGuid` の blockParam 化・実 blocks.yml 方言（`- key:` リスト）。
> - フィルターインベントリは `IOpenableBlockInventoryComponent` 実装として components 登録（ベルト搬入/UIが成立）。消費時にフィルターアイテム種チェック。
> - V/Cells 規則: `Cells`=占有セル含む（帰属判定）、`Volume`=空セルのみ（バランス確定書§5 確定値）。
> - keystone テストは**機械なし基準部屋**（`CleanRoomMachine` はフェーズ4で導入のため）＋**摩耗アサーション必須**＋ n=2 加算テスト追加。`ComputeATotal` の doorBurst 引数は廃止（バーストは N 直接加算・フェーズ5）。

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **moorestech 固有の必須ルール:**
> - `.cs` 編集後は必ず `uloop compile --project-path ./moorestech_client` でコンパイル確認する。
> - テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"` で実行（クライアントプロジェクトからサーバーテストも走る）。
> - 新規サーバー `.cs`／新規 blockType／新規スキーマ生成型を認識させるには Unity の **再起動**が要る場合がある（Refresh では不足）。「型が見つからない」で失敗したら uloop で Unity 再起動してから再試行。「Domain Reload in progress」なら45秒待って再試行。
> - blockType スキーマ追加・SourceGenerator の手順は `edit-schema` スキルに従う。テスト作成は `creating-server-tests` スキルに従う。
> - 非ASCIIファイル編集時は AGENTS.md の「文字化け防止ワークフロー」を順守。`blocks.json`/`items.json`/`blocks.yml` は UTF-8 系。
> - **契約（正）はコードマップ v2（`2026-06-06-cleanroom-phases2-5-codemap.md`）とバランス確定書（`2026-06-06-cleanroom-balance-parameters.md`）。** 本プランと食い違ったらそちらを正として本プランを直す。
> - **APIシグネチャ確認の原則:** 本プランのコードは既存コードベースの実ファイル（`VanillaElectricMachineComponent`/`VanillaMachineProcessorComponent`/`FuelGearGeneratorItemComponent`/`VanillaFuelGearGeneratorTemplate`/`EnergySegment`/`ConnectElectricPoleToElectricSegment` 等）を開いて照合済み。ただしフェーズ1/2 の成果物（`CleanRoomDatastore`/`CleanRoom`/`ICleanRoomAirFilter`/境界コンポーネント）は並行改訂中のため、各 `.cs` を書く前に実ファイルでメンバ名を確認すること。コンパイル/テストのチェックポイントが安全網。

**Goal:** フェーズ2で用意した注入口（`ICleanRoomAirFilter` の n·q、`CleanRoomDatastore` の A_total 算出箇所）を実供給する。電力で動くエアフィルターブロック（`CleanRoomAirFilter`）を作り、満電時 q=5 m³/秒の除去能力・電力割合での減衰・フィルター仕事量消費（除去不純物の累計が `filterCapacity`=5000 ごとに1個消費・残0で除去停止・**フィルターアイテム種チェック付き**）を実装する。同時に汚染源（`a_volume·V + a_surface·S + a_connector·接続点数 + A_machine·稼働機械数`、ハッチ/ドア計量はフェーズ5）を算出する `CleanRoomPollutionCalculator` を作り `CleanRoomDatastore.Update` に配線する。基準部屋（機械なし・エアフィルター1台内蔵で **V=74, S=109**, 接続点2）で `A_total=13.85` → `C_eq=2.77` → 閾値行A・**摩耗累計 ≈ A_total×t（±10%）** を統合テストで固定する。

**Architecture:** エアフィルターは `VanillaCleanRoomAirFilterTemplate`（New/Load）が組み立てる3コンポーネント構成: (1) `CleanRoomAirFilterItemComponent`（`IOpenableBlockInventoryComponent, IBlockSaveState`、`FuelGearGeneratorItemComponent` 方式のフィルタースロット。フィルター種チェック付きカウント/消費）、(2) `CleanRoomAirFilterComponent`（**単一コンポーネント初版**: `IElectricConsumer, IUpdatableBlockComponent, IBlockSaveState, ICleanRoomAirFilter`。電力保持・実効q・摩耗累計）、(3) inventory コネクタ（ベルトからフィルター搬入可能。挿入先は (1) の `IBlockInventory`）。設置時は既存 `ConnectMachineToElectricSegment`/`ConnectElectricPoleToElectricSegment` が `EnergySegment` へ自動配線する（機械→ポールの設置順でも接続されることを実コードで確認済み）。フィルター摩耗は「エアフィルターが C を知らない」ため**データストアからのプッシュ**で行う: `CleanRoomDatastore` が毎tick各台の除去寄与を `ICleanRoomAirFilter.ApplyRemovedImpurity(removed)` で渡す（asmdef 依存方向 `Game.CleanRoom → Game.Block.Interface` を保つ。`Game.Block` 実装asmdefへの参照は**不可**）。A_total は静的ヘルパ `CleanRoomPollutionCalculator` の純関数 `ComputeATotal` ＋ `BlockInstanceId` 単位で重複排除する接続点カウントで算出する。

**Tech Stack:** C# (Unity, moorestech_server), UniRx `IObservable<Unit>`（`GameUpdater.UpdateObservable` / テストは `GameUpdater.RunFrames(uint)`）, NUnit (Server.Tests), Newtonsoft.Json（ブロックstate保存）, Mooresmaster Source Generator (blocks.yml → BlocksModule), `Game.EnergySystem`（`IElectricConsumer`/`EnergySegment`）。

---

## 依存・前提（このプランの外）

| 前提 | 内容 | 影響 |
|---|---|---|
| **フェーズ1完了（v2改訂版）** | 境界ブロック群（`CleanRoomWall`/`CleanRoomDoorHatch`/`CleanRoomItemHatch`/`CleanRoomPipeHatch`）／`ICleanRoomBoundaryComponent`＋`CleanRoomBoundaryKind { Wall, DoorHatch, ItemHatch, PipeHatch }`（`Game.Block.Interface`）／`CleanRoomDetector`（6近傍flood-fill）／asmdef `Game.CleanRoom` | 本プランの汚染計算・部屋検出はこの土台に載る |
| **フェーズ2完了（v2改訂版）** | `CleanRoomDatastore`（DI singleton, eager, 設置/削除購読＋tick購読）／`CleanRoom`（`Cells`/`Volume`/`SurfaceArea`/`ImpurityCount`/`Concentration`/`ThresholdIndex`/`Status`/`AddImpurity`/`RemoveImpurity`）／`CleanRoomPurityRules`（二条件＋ヒステリシス純関数）／`ICleanRoomAirFilter`（`Game.Block.Interface/Component`）／`cleanRoomThresholds.yml`／セーブ統合 | **本プランは `ICleanRoomAirFilter` を「埋める」段**。データストアの tick へ n·q 集計と摩耗配分を差し込む |

> **⚠ 実装着手前の確認事項（フェーズ1/2 は並行改訂中のため、着手時に実ファイルで確定する）**
> 1. **`ICleanRoomAirFilter` のメンバ**: 本プランは `double RemovalVolumePerSecond { get; }` ＋ `void ApplyRemovedImpurity(double removed)` の2メンバ前提（codemap §3）。フェーズ2版が `RemovalVolumePerSecond` のみなら本プラン Task 2 で `ApplyRemovedImpurity` を追加する。`IBlockComponent` 継承（`TryGetComponent<T>` の型制約に必要）も確認。
> 2. **`CleanRoom` の API 名**: `AddImpurity`/`RemoveImpurity`/`ImpurityCount`/`Concentration`（引数なしプロパティ）/`ThresholdIndex`/`Volume`/`SurfaceArea`/`Cells` は codemap §1.2 の契約名。実装と違えば実装に合わせる。
> 3. **`ThresholdIndex` の行順**: 本プランのテストは「index 0 = A行（最上位）」前提。`cleanRoomThresholds.yml` の行順がそうなっているか確認し、違えばテスト期待値を読み替える。
> 4. **部屋内ブロックの収集方法（確定）**: フェーズ2のレジストリ（`AddAirFilter`/`RemoveAirFilter`）が正。**登録はデータストア自身**が既存の設置/削除購読内で `block.TryGetComponent<ICleanRoomAirFilter>` を見て行う（依存方向 `Game.CleanRoom → Game.Block.Interface` のまま。ブロック側はデータストアを知らないため自己登録はできない）。毎tick の `room.Cells` 走査は行わない（レジストリのセル ∈ `room.Cells` で部屋内判定）。
> 5. **テストmod のクリーンルーム境界ブロック**: フェーズ1がテストmodへ追加済みの壁/ハッチの `ForUnitTestModBlockId` アクセサ名（例: `CleanRoomWallId`/`CleanRoomItemHatchId`/`CleanRoomPipeHatchId`）を確認して keystone テストで使う。

---

## File Structure（フェーズ3で作成/変更するファイル）

**スキーマ／マスタ（エアフィルターブロック＋フィルターアイテム）**
- Modify: `VanillaSchema/blocks.yml` — `blockType` enum に `CleanRoomAirFilter` 追加＋blockParam の case（`removalVolumePerSecond`(=q) / `requiredPower` / `filterCapacity` / `filterItemGuid` / `filterItemSlotCount` / `inventoryConnectors`）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` — SourceGenerator トリガ
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json` — エアフィルターブロック追加
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json` — フィルターアイテム＋エアフィルターブロックアイテム追加
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs` — `CleanRoomAirFilterId` / `CleanRoomFilterItemGuid` アクセサ追加

**注入インターフェース（Game.Block.Interface） — フェーズ2作成済みの確認/拡張**
- Modify(or Create): `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomAirFilter.cs` — `RemovalVolumePerSecond` ＋ `ApplyRemovedImpurity(double)`（確認事項#1）

**エアフィルターブロック実装（Game.Block）**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomAirFilterItemComponent.cs` — `IOpenableBlockInventoryComponent, IBlockSaveState`。フィルタースロット＋種チェック付きカウント/消費
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomAirFilterComponent.cs` — 単一コンポーネント本体: `IElectricConsumer, IUpdatableBlockComponent, IBlockSaveState, ICleanRoomAirFilter`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomAirFilterTemplate.cs` — `IBlockTemplate`（New/Load）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs` — `CleanRoomAirFilter` を登録

**汚染計算＋データストア配線（Game.CleanRoom）**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Pollution/CleanRoomPollutionCalculator.cs` — 静的ヘルパ。純関数 `ComputeATotal` ＋ 接続点カウント（注入インターフェースは作らない）
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs` — tick に A_total 算出・部屋内エアフィルター集計（n·q）・除去寄与の `ApplyRemovedImpurity` 配分を差し込む

**テスト**
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomAirFilterTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPollutionTest.cs`

> 各 `.cs` 新規ファイルは Unity が `.meta` を自動生成する。`.meta` は手動作成禁止。

---

## 数値の唯一ソース（`2026-06-06-cleanroom-balance-parameters.md`）

| パラメータ | 値 | 出典 |
|---|---|---|
| `q`（処理体積, 満電1台。スキーマキーは `removalVolumePerSecond`） | 5.0 m³/秒 | §3 |
| `requiredPower`（**既存規約名。`requestPower` は使わない**） | 100 | §3 |
| `filterCapacity`（**blockParam。C#ハードコード禁止**） | 5000 個/フィルター | §3 |
| `filterItemGuid`（**blockParam。消費時の種チェックに使用**） | テストmodではフィルターアイテムのGUID | §3 |
| `a_volume` | 0.10 個/(m³·秒) | §2 |
| `a_surface` | 0.05 個/(m²·秒) | §2 |
| `a_connector` | 0.50 個/(接続点·秒) | §2 |
| `A_machine` | 2.0 個/(稼働機械·秒)（**部屋内 `CleanRoomMachine`（フェーズ4）の稼働中フラグ。汎用 Vanilla 機械は数えない**） | §2 / codemap §3 |
| `k_hatch` | 0.30（フェーズ3では搬送0なので寄与0） | §2 |
| `burst_door` | 15 個/通過（**A_total に合算しない。フェーズ5で `CleanRoom.AddImpurity` へ直接加算**） | §2 単位注意 |
| tick | 50ms（`GameUpdater.SecondsPerTick` = 0.05。ローカル定数を作らない） | §0 |

### 基準部屋（worked example・フェーズ3版）

バランス確定書§4 の worked example（V=75, S=110, 機械1）は**理想形**。フェーズ3の統合テストは以下2点で具体化する:

1. **機械なし**: `A_machine` の対象は `CleanRoomMachine`（専用機械、フェーズ4で導入）であり、フェーズ3には存在しない。汎用 `VanillaMachineProcessorComponent` を数えるのは契約違反（codemap §3）。よって統合テストは **A_machine 項=0** の部屋で行い、係数 2.0 自体は純関数テスト（Task 6）で固定する。これにより「機械を長時間稼働させ続ける」というテスト脆弱性も消える。
2. **占有セルは V から除外**（バランス確定書§5）: 内寸 5×5×3=75 セルにエアフィルター1台（1×1×1）を**側壁に接しない床セル**へ置くと、`Cells`=75（帰属判定用・占有セル含む）、`Volume`=**74**（空セルのみ）、`SurfaceArea`=**109**（占有床セルの床接触1面が空セル面から消える）。

```
基準部屋（機械なし）: V=74, S=109, 接続点2（ItemHatch 1 + PipeHatch 1）, エアフィルター1台満電
A_total = a_volume·74 + a_surface·109 + a_connector·2
        = 7.4 + 5.45 + 1.0 = 13.85 個/秒
C_eq    = A_total / (n·q) = 13.85 / 5 = 2.77 個/m³  → A行域（≤10）
ACH     = n·q/V = 5/74 ≈ 0.0676 /秒 ≥ A行要求 0.0167 → 閾値行A 成立
τ       = V/(n·q) = 14.8 秒（平衡への時定数）
```

**摩耗アサーション（必須・バランス確定書§3）**: t=300秒（6000 tick ≈ 20τ）回した時点で、
```
A_total·t        = 13.85 × 300 = 4155
理論摩耗累計      = A_total·t − N(t) ≈ 4155 − C_eq·V ≈ 4155 − 205 ≈ 3950
アサート帯（契約） = A_total·t ± 10% = [3739.5, 4570.5]   ← 理論値 3950 は帯内
```
（除去累計は「加算累計 − 室内残存N」なので A_total·t よりわずかに小さい。±10% 帯は t ≥ 10τ で初めて成立する点に注意 — 短すぎる t でアサートしない）

**n=2 加算検証用**: 同じ部屋にエアフィルター2台 → V=73, S=108, `A_total = 7.3 + 5.4 + 1.0 = 13.7`, `n·q=10`, `C_eq = 1.37`, τ=7.3秒。

> 接続点は ItemHatch＋PipeHatch を使う（v2 では**全ハッチが気密境界**なので DoorHatch でも密閉は成立するが、フェーズ5の搬送実装と並びを揃える）。`a_connector` は種別一律なので内訳はどれでも 2×0.50=1.0。

---

## Task 1: エアフィルター blockType ＋ フィルターアイテムをスキーマ／テストmodに追加

エアフィルターブロックの blockType・param と、フィルターアイテムをテスト用 mod に足す。コード生成のみ。`edit-schema` スキルの手順に従うこと。

**Files:**
- Modify: `VanillaSchema/blocks.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`
- Modify: `.../ForUnitTest/mods/forUnitTest/master/blocks.json`
- Modify: `.../ForUnitTest/mods/forUnitTest/master/items.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs`

- [ ] **Step 1: blocks.yml の blockType enum に追加**

`VanillaSchema/blocks.yml` の `blockType` の `options:` 配列末尾へ（フェーズ1が `CleanRoomWall` 等を追加済みならその並びに続ける）:

```yaml
      - CleanRoomAirFilter
```

- [ ] **Step 2: blockParam の cases に追加（実スキーマ方言で書く）**

blocks.yml の blockParam case は **`properties:` を `- key:` のリスト**で書く方言（`when: ElectricMachine` の実物を参照。JSON-Schema 風の `properties:` マップは**不可**）。`inventoryConnectors` は `ref: inventoryConnects` の1行参照、`filterItemGuid` は既存 `itemGuid` キーと同じ `type: uuid`＋`foreignKey` 記法をコピーする:

```yaml
      - when: CleanRoomAirFilter
        type: object
        implementationInterface:
        - IInventoryConnectors
        properties:
        - key: removalVolumePerSecond
          type: number
          default: 5
        - key: requiredPower
          type: number
          default: 100
        - key: filterCapacity
          type: number
          default: 5000
        - key: filterItemGuid
          type: uuid
          foreignKey:
            schemaId: items
            foreignKeyIdPath: /data/[*]/itemGuid
            displayElementPath: /data/[*]/name
        - key: filterItemSlotCount
          type: integer
          default: 1
        - key: inventoryConnectors
          ref: inventoryConnects
```

> `implementationInterface: IInventoryConnectors` は `ElectricMachine` の case を確認し、`inventoryConnectors` を持つ param の慣例に合わせて付ける（不要なら外す。`IMachineParam` はスロット/タンク系メンバを要求するため**付けない**）。生成される型は `CleanRoomAirFilterBlockParam`、プロパティは `RemovalVolumePerSecond`/`RequiredPower`/`FilterCapacity`/`FilterItemGuid`(Guid)/`FilterItemSlotCount`/`InventoryConnectors` になる想定。Task 4/5 で参照確認する。

- [ ] **Step 3: SourceGenerator をトリガ**

`Core.Master/_CompileRequester.cs` の `dummyText` 定数の値を変更:

```csharp
private const string dummyText = "regenerate-cleanroom-phase3";
```

- [ ] **Step 4: テストmod の items.json にフィルターアイテム＋エアフィルターブロックアイテムを追加**

既存エントリの形式（`maxStack`/`name`/`itemGuid`/`imagePath`/`sortPriority`/`recipeViewType`/`initialUnlocked`）に合わせて2件追加:

```json
    {
      "maxStack": 100,
      "name": "TestCleanRoomFilter",
      "itemGuid": "00000000-0000-0000-1234-0000000000f1",
      "imagePath": "TestCleanRoomFilter",
      "sortPriority": 100,
      "recipeViewType": "ForceView",
      "initialUnlocked": true
    },
    {
      "maxStack": 100,
      "name": "TestCleanRoomAirFilter",
      "itemGuid": "00000000-0000-0000-1234-0000000000f2",
      "imagePath": "TestCleanRoomAirFilter",
      "sortPriority": 100,
      "recipeViewType": "ForceView",
      "initialUnlocked": true
    }
```

- [ ] **Step 5: テストmod の blocks.json にエアフィルターブロックを追加**

`ElectricMachine` エントリを開いて必須キー（`blockGuid`/`itemGuid`/`modelTransform` 等）のネスト形を一致させつつ、末尾へ追加。param 値は本プランの数値ソースに合わせる:

```json
    {
      "maxStack": 100,
      "blockSize": [1, 1, 1],
      "name": "TestCleanRoomAirFilter",
      "blockGuid": "00000000-0000-0000-0000-0000000000f2",
      "itemGuid": "00000000-0000-0000-1234-0000000000f2",
      "blockType": "CleanRoomAirFilter",
      "blockParam": {
        "removalVolumePerSecond": 5.0,
        "requiredPower": 100,
        "filterCapacity": 5000,
        "filterItemGuid": "00000000-0000-0000-1234-0000000000f1",
        "filterItemSlotCount": 1,
        "inventoryConnectors": { "...": "ElectricMachine エントリの inventoryConnectors（outputConnects/inputConnects 実構造）をコピー" }
      }
    }
```

> `inventoryConnectors` の実構造（`outputConnects`/`inputConnects` の `offset`/`directions`/`connectorGuid` 等）は既存 `TestElectricMachine` エントリからコピーし、`connectorGuid` だけ新規GUIDに差し替える。

- [ ] **Step 6: ForUnitTestModBlockId にアクセサを追加**

`Tests.Module/TestMod/ForUnitTestModBlockId.cs` に追加:

```csharp
        public static BlockId CleanRoomAirFilterId => GetBlock("00000000-0000-0000-0000-0000000000f2");
        public static System.Guid CleanRoomFilterItemGuid => System.Guid.Parse("00000000-0000-0000-1234-0000000000f1");
```

- [ ] **Step 7: 再生成を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。`Mooresmaster.Model.BlocksModule.BlockTypeConst.CleanRoomAirFilter` と `CleanRoomAirFilterBlockParam` が生成される。

> 型未検出なら Unity 再起動（新規 blockType＋生成型のため Refresh では不足しうる）。「Domain Reload in progress」なら45秒待つ。

- [ ] **Step 8: Commit**

```bash
git add VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs moorestech_server/Assets/Scripts/Tests.Module/TestMod/
git commit -m "feat(cleanroom): エアフィルターblockTypeとフィルターアイテムをスキーマ/テストmodに追加"
```

---

## Task 2: `ICleanRoomAirFilter` の確認/拡張（Game.Block.Interface）

フェーズ2が `Game.Block.Interface/Component/ICleanRoomAirFilter.cs` を作成済み（codemap §2）。メンバが `RemovalVolumePerSecond` のみなら、フィルター摩耗プッシュ用の `ApplyRemovedImpurity` を追加する（確認事項#1）。

**Files:**
- Modify(or Create): `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomAirFilter.cs`

- [ ] **Step 1: インターフェースを確認し、最終形に揃える**

```csharp
namespace Game.Block.Interface.Component
{
    // 部屋の不純物を除去する供給源（エアフィルター）。CleanRoomDatastore が n·q 集計と摩耗プッシュに使う。
    // Impurity-removal source (air filter); CleanRoomDatastore reads n·q and pushes wear through this.
    public interface ICleanRoomAirFilter : IBlockComponent
    {
        // 満電時 q × 電力割合(≤1) × (フィルター残>0 ? 1 : 0)。n·q の自台寄与。
        // q × power-ratio(≤1) × (filter present ? 1 : 0); this unit's contribution to n·q.
        double RemovalVolumePerSecond { get; }

        // データストアがこの台の今tickの除去不純物量を渡す。フィルター摩耗に使う。
        // Datastore pushes this unit's removed impurity for the tick; drives filter wear.
        void ApplyRemovedImpurity(double removed);
    }
}
```

> `IBlockComponent` 継承は `block.TryGetComponent<T>()` の型制約（`where T : IBlockComponent`）に必要。`IBlockComponent` のメンバ（`bool IsDestroy`/`void Destroy()`）は `Game.Block.Interface/Component/IBlockComponent.cs` で確認。

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功（フェーズ2側に既存実装があれば追従修正）。

- [ ] **Step 3: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomAirFilter.cs
git commit -m "feat(cleanroom): ICleanRoomAirFilterにApplyRemovedImpurityを追加"
```

---

## Task 3: フィルターインベントリコンポーネント（IOpenableBlockInventoryComponent・種チェック付き）

フィルタースロットを保持する**ブロックコンポーネント**。`FuelGearGeneratorItemComponent`（`OpenableInventoryItemDataStoreService` への委譲一式＋`IBlockSaveState`）を雛形にする。components に登録されるため、ベルト（`IBlockInventory`）からの搬入・プレイヤーUI・テストからの取得が全て成立する。**フィルターのカウント/消費は `filterItemGuid` のアイテムのみ対象**（誤投入アイテムを食わない）。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomAirFilterItemComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomAirFilterTest.cs`

- [ ] **Step 1: 失敗するテストを書く（種チェック付きカウント／消費）**

`Tests/CombinedTest/Core/CleanRoomAirFilterTest.cs` を新規作成:

```csharp
using Core.Master;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Interface;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomAirFilterTest
    {
        private const double FilterCapacity = 5000;

        [Test]
        public void ItemComponent_CountsAndConsumesOnlyFilterItems()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var filterItemId = MasterHolder.ItemMaster.GetItemId(ForUnitTestModBlockId.CleanRoomFilterItemGuid);

            // スロット2: フィルター以外のアイテムはカウントも消費もされない。
            // 2 slots: non-filter items are neither counted nor consumed.
            var inventory = new CleanRoomAirFilterItemComponent(slotCount: 2, filterItemId, new BlockInstanceId(1));
            inventory.SetItem(0, itemStackFactory.Create(ForUnitTestModBlockId.CleanRoomFilterItemGuid, 2));
            inventory.SetItem(1, itemStackFactory.Create(new System.Guid("00000000-0000-0000-1234-000000000001"), 5)); // Test1（非フィルター）

            Assert.AreEqual(2, inventory.FilterCount, "filter items only");
            Assert.IsTrue(inventory.HasFilter);

            // 消費はフィルタースロットだけ減る。
            // Consumption only decrements the filter slot.
            Assert.IsTrue(inventory.TryConsumeOneFilter());
            Assert.IsTrue(inventory.TryConsumeOneFilter());
            Assert.IsFalse(inventory.TryConsumeOneFilter(), "no filters left");
            Assert.AreEqual(0, inventory.FilterCount);
            Assert.IsFalse(inventory.HasFilter);
            Assert.AreEqual(5, inventory.GetItem(1).Count, "non-filter item untouched");
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ItemComponent_CountsAndConsumesOnlyFilterItems"`
Expected: FAIL（`CleanRoomAirFilterItemComponent` 未定義）。型未検出なら Unity 再起動。

- [ ] **Step 3: フィルターインベントリコンポーネントを実装**

`Game.Block/Blocks/CleanRoom/CleanRoomAirFilterItemComponent.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Block.Event;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Event;
using Game.Context;
using Newtonsoft.Json;

namespace Game.Block.Blocks.CleanRoom
{
    // フィルタースロット。filterItemGuid のアイテムだけをフィルターとして数え・消費する。
    // Filter slots; only items matching filterItemGuid are counted/consumed as filters.
    public class CleanRoomAirFilterItemComponent : IOpenableBlockInventoryComponent, IBlockSaveState
    {
        public string SaveKey => "cleanRoomAirFilterItem";
        public bool IsDestroy { get; private set; }

        public bool HasFilter => FilterCount > 0;
        public IReadOnlyList<IItemStack> InventoryItems => _inventoryService.InventoryItems;

        private readonly OpenableInventoryItemDataStoreService _inventoryService;
        private readonly ItemId _filterItemId;
        private readonly BlockInstanceId _blockInstanceId;

        public CleanRoomAirFilterItemComponent(int slotCount, ItemId filterItemId, BlockInstanceId blockInstanceId)
        {
            _filterItemId = filterItemId;
            _blockInstanceId = blockInstanceId;
            _inventoryService = new OpenableInventoryItemDataStoreService(InvokeEvent, ServerContext.ItemStackFactory, Math.Max(1, slotCount));
        }

        public CleanRoomAirFilterItemComponent(Dictionary<string, string> componentStates, int slotCount, ItemId filterItemId, BlockInstanceId blockInstanceId)
            : this(slotCount, filterItemId, blockInstanceId)
        {
            if (!componentStates.TryGetValue(SaveKey, out var stateRaw)) return;
            var items = JsonConvert.DeserializeObject<List<ItemStackSaveJsonObject>>(stateRaw);
            RestoreItems(items);
        }

        // フィルターアイテムだけを数える（誤投入アイテムは無視）。
        // Count only filter items; foreign items are ignored.
        public int FilterCount
        {
            get
            {
                var count = 0;
                for (var i = 0; i < _inventoryService.GetSlotSize(); i++)
                {
                    var item = _inventoryService.GetItem(i);
                    if (item.Id == _filterItemId) count += item.Count;
                }
                return count;
            }
        }

        // フィルターを1個消費する。フィルターアイテム以外は消費しない。
        // Consume exactly one filter; never consumes non-filter items.
        public bool TryConsumeOneFilter()
        {
            BlockException.CheckDestroy(this);
            for (var i = 0; i < _inventoryService.GetSlotSize(); i++)
            {
                var item = _inventoryService.GetItem(i);
                if (item.Id != _filterItemId || item.Count <= 0) continue;
                _inventoryService.SetItem(i, item.SubItem(1));
                return true;
            }
            return false;
        }

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            var slotSize = _inventoryService.GetSlotSize();
            var serialized = new List<ItemStackSaveJsonObject>(slotSize);
            for (var i = 0; i < slotSize; i++) serialized.Add(new ItemStackSaveJsonObject(_inventoryService.GetItem(i)));
            return JsonConvert.SerializeObject(serialized);
        }

        public void Destroy()
        {
            IsDestroy = true;
        }

        #region IOpenableBlockInventoryComponent 委譲 / delegation

        public IItemStack InsertItem(IItemStack itemStack) { BlockException.CheckDestroy(this); return _inventoryService.InsertItem(itemStack); }
        public IItemStack InsertItem(IItemStack itemStack, InsertItemContext context) { return InsertItem(itemStack); }
        public IItemStack InsertItem(ItemId itemId, int count) { BlockException.CheckDestroy(this); return _inventoryService.InsertItem(itemId, count); }
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks) { BlockException.CheckDestroy(this); return _inventoryService.InsertItem(itemStacks); }
        public bool InsertionCheck(List<IItemStack> itemStacks) { BlockException.CheckDestroy(this); return _inventoryService.InsertionCheck(itemStacks); }
        public IItemStack GetItem(int slot) { BlockException.CheckDestroy(this); return _inventoryService.GetItem(slot); }
        public void SetItem(int slot, IItemStack itemStack) { BlockException.CheckDestroy(this); _inventoryService.SetItem(slot, itemStack); }
        public void SetItem(int slot, ItemId itemId, int count) { BlockException.CheckDestroy(this); _inventoryService.SetItem(slot, itemId, count); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { BlockException.CheckDestroy(this); return _inventoryService.ReplaceItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, ItemId itemId, int count) { BlockException.CheckDestroy(this); return _inventoryService.ReplaceItem(slot, itemId, count); }
        public int GetSlotSize() { BlockException.CheckDestroy(this); return _inventoryService.GetSlotSize(); }
        public ReadOnlyCollection<IItemStack> CreateCopiedItems() { BlockException.CheckDestroy(this); return _inventoryService.CreateCopiedItems(); }

        #endregion

        #region Internal

        // セーブデータからスロットを復元（ロード時はイベント抑止）。
        // Restore slots from save data without firing events during load.
        void RestoreItems(List<ItemStackSaveJsonObject> items)
        {
            if (items == null) return;
            var slotSize = _inventoryService.GetSlotSize();
            for (var i = 0; i < Math.Min(slotSize, items.Count); i++)
            {
                var stack = items[i]?.ToItemStack();
                if (stack == null) continue;
                _inventoryService.SetItemWithoutEvent(i, stack);
            }
        }

        // インベントリ更新をクライアントへ同期。
        // Sync inventory updates to clients.
        void InvokeEvent(int slot, IItemStack itemStack)
        {
            if (IsDestroy) return;
            var blockInventoryUpdate = (BlockOpenableInventoryUpdateEvent)ServerContext.BlockOpenableInventoryUpdateEvent;
            blockInventoryUpdate.OnInventoryUpdateInvoke(new BlockOpenableInventoryUpdateEventProperties(_blockInstanceId, slot, itemStack));
        }

        #endregion
    }
}
```

> 委譲メンバ一式・`InvokeEvent`・`RestoreItems` は `FuelGearGeneratorItemComponent.cs` の実装と一致させる（`IOpenableBlockInventoryComponent : IBlockInventory, IOpenableInventory` のため必須メンバが多い。差分が出たら実ファイルを正とする）。`ItemStackSaveJsonObject` は `Core.Item.Interface`、`.ToItemStack()` 拡張は **`Game.Context`**（`using Game.Context;` を忘れない）。

- [ ] **Step 4: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ItemComponent_CountsAndConsumesOnlyFilterItems"`
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomAirFilterItemComponent.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomAirFilterTest.cs
git commit -m "feat(cleanroom): 種チェック付きフィルターインベントリCleanRoomAirFilterItemComponentを追加"
```

---

## Task 4: エアフィルター本体（単一コンポーネント: 電力・実効q・摩耗・セーブ）

codemap §3 の単一コンポーネント初版。`IElectricConsumer, IUpdatableBlockComponent, IBlockSaveState, ICleanRoomAirFilter` を1クラスで実装する（電力/フィルター/セーブの細分は後から）。`RemovalVolumePerSecond = q × (currentPower/requiredPower クランプ1) × (フィルター残>0?1:0)`。データストアがプッシュする `ApplyRemovedImpurity` で摩耗を累計し、`filterCapacity` を跨ぐごとにフィルターを1個消費する。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomAirFilterComponent.cs`

- [ ] **Step 1: 失敗するテストを書く（電力割合・フィルター有無・摩耗消費・セーブround-trip）**

`CleanRoomAirFilterTest.cs` に追加:

```csharp
        [Test]
        public void Component_RemovalScalesWithPowerRatioAndFilterPresence()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var filterItemId = MasterHolder.ItemMaster.GetItemId(ForUnitTestModBlockId.CleanRoomFilterItemGuid);

            var inventory = new CleanRoomAirFilterItemComponent(slotCount: 1, filterItemId, new BlockInstanceId(1));
            inventory.InsertItem(itemStackFactory.Create(ForUnitTestModBlockId.CleanRoomFilterItemGuid, 1));

            // q=5, requiredPower=100, filterCapacity=5000。
            // q=5, requiredPower=100, filterCapacity=5000.
            var component = new CleanRoomAirFilterComponent(new BlockInstanceId(1), removalVolumePerSecond: 5.0, requiredPower: 100f, filterCapacity: FilterCapacity, inventory);

            // 給電前は0。
            // No power yet => 0.
            Assert.AreEqual(0.0, component.RemovalVolumePerSecond, 1e-9);

            // 満電で q=5.0、半電で 2.5、過給電は1にクランプ。
            // Full power => 5.0, half => 2.5, over-supply clamps to 1.
            component.SupplyEnergy(new Game.EnergySystem.ElectricPower(100f));
            Assert.AreEqual(5.0, component.RemovalVolumePerSecond, 1e-9);
            component.SupplyEnergy(new Game.EnergySystem.ElectricPower(50f));
            Assert.AreEqual(2.5, component.RemovalVolumePerSecond, 1e-9);
            component.SupplyEnergy(new Game.EnergySystem.ElectricPower(1000f));
            Assert.AreEqual(5.0, component.RemovalVolumePerSecond, 1e-9);

            // 給電が来ないまま2回目のUpdateで電力decay→0（常時消費のため毎tick電力を使う）。
            // Second Update without fresh supply decays power to 0 (always-on consumer).
            component.SupplyEnergy(new Game.EnergySystem.ElectricPower(100f));
            component.Update();
            Assert.AreEqual(5.0, component.RemovalVolumePerSecond, 1e-9);
            component.Update();
            Assert.AreEqual(0.0, component.RemovalVolumePerSecond, 1e-9);
        }

        [Test]
        public void Component_NoFilter_RemovalIsZero()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var filterItemId = MasterHolder.ItemMaster.GetItemId(ForUnitTestModBlockId.CleanRoomFilterItemGuid);

            // フィルター無し → 満電でも除去0。
            // No filter loaded => removal is 0 even at full power.
            var inventory = new CleanRoomAirFilterItemComponent(slotCount: 1, filterItemId, new BlockInstanceId(1));
            var component = new CleanRoomAirFilterComponent(new BlockInstanceId(1), 5.0, 100f, FilterCapacity, inventory);
            component.SupplyEnergy(new Game.EnergySystem.ElectricPower(100f));
            Assert.AreEqual(0.0, component.RemovalVolumePerSecond, 1e-9);
        }

        [Test]
        public void Component_WearCrossingCapacityConsumesFilter_AndDepletionStopsRemoval()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var filterItemId = MasterHolder.ItemMaster.GetItemId(ForUnitTestModBlockId.CleanRoomFilterItemGuid);

            var inventory = new CleanRoomAirFilterItemComponent(slotCount: 1, filterItemId, new BlockInstanceId(1));
            inventory.InsertItem(itemStackFactory.Create(ForUnitTestModBlockId.CleanRoomFilterItemGuid, 2));
            var component = new CleanRoomAirFilterComponent(new BlockInstanceId(1), 5.0, 100f, FilterCapacity, inventory);
            component.SupplyEnergy(new Game.EnergySystem.ElectricPower(100f));

            // capacity 未満では消費しない。
            // No consumption below one capacity of wear.
            component.ApplyRemovedImpurity(FilterCapacity - 1);
            Assert.AreEqual(2, inventory.FilterCount);

            // capacity を跨いだら1個消費。
            // Crossing capacity consumes exactly one filter.
            component.ApplyRemovedImpurity(2);
            Assert.AreEqual(1, inventory.FilterCount);

            // 残り1個も使い切ると除去0（フィルター切れ）。
            // Wearing out the last filter stops removal.
            component.ApplyRemovedImpurity(FilterCapacity);
            Assert.AreEqual(0, inventory.FilterCount);
            Assert.AreEqual(0.0, component.RemovalVolumePerSecond, 1e-9);
        }

        [Test]
        public void Component_SaveState_RoundTripsWearProgressAndSlots()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            var filterItemId = MasterHolder.ItemMaster.GetItemId(ForUnitTestModBlockId.CleanRoomFilterItemGuid);

            var inventory = new CleanRoomAirFilterItemComponent(slotCount: 1, filterItemId, new BlockInstanceId(1));
            inventory.InsertItem(itemStackFactory.Create(ForUnitTestModBlockId.CleanRoomFilterItemGuid, 3));
            var component = new CleanRoomAirFilterComponent(new BlockInstanceId(1), 5.0, 100f, FilterCapacity, inventory);
            component.SupplyEnergy(new Game.EnergySystem.ElectricPower(100f));
            component.ApplyRemovedImpurity(1234); // 進捗を残す（消費は跨がない）

            // 2コンポーネントの保存stateを componentStates 辞書に集めてロード経路を再現。
            // Collect both components' states into a componentStates dict to mimic the load path.
            var componentStates = new System.Collections.Generic.Dictionary<string, string>
            {
                { inventory.SaveKey, inventory.GetSaveState() },
                { component.SaveKey, component.GetSaveState() },
            };

            var restoredInventory = new CleanRoomAirFilterItemComponent(componentStates, slotCount: 1, filterItemId, new BlockInstanceId(2));
            var restoredComponent = new CleanRoomAirFilterComponent(componentStates, new BlockInstanceId(2), 5.0, 100f, FilterCapacity, restoredInventory);

            Assert.AreEqual(3, restoredInventory.FilterCount);
            Assert.AreEqual(1234, restoredComponent.WearProgress, 1e-6);
        }
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Component_RemovalScales|Component_NoFilter|Component_WearCrossing|Component_SaveState"`
Expected: FAIL（`CleanRoomAirFilterComponent` 未定義）。

- [ ] **Step 3: 単一コンポーネントを実装**

`Game.Block/Blocks/CleanRoom/CleanRoomAirFilterComponent.cs`:

```csharp
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Newtonsoft.Json;

namespace Game.Block.Blocks.CleanRoom
{
    // エアフィルター本体（単一コンポーネント初版）。電力割合で実効q、除去量に比例してフィルター摩耗。
    // Air filter core (single-component v1): effective q scales with power; filter wears by removed amount.
    public class CleanRoomAirFilterComponent : IElectricConsumer, IUpdatableBlockComponent, IBlockSaveState, ICleanRoomAirFilter
    {
        public BlockInstanceId BlockInstanceId { get; }
        public bool IsDestroy { get; private set; }
        public string SaveKey => "cleanRoomAirFilter";

        // セーブ/テスト検証用の摩耗累計（filterCapacity 未満の端数）。
        // Wear accumulator below one filterCapacity; exposed for save/tests.
        public double WearProgress => _wearProgress;

        private readonly double _removalVolumePerSecond; // 満電1台の q
        private readonly float _requiredPower;
        private readonly double _filterCapacity;
        private readonly CleanRoomAirFilterItemComponent _filterInventory;

        // 常時消費のため毎Updateで電力を使う（Vanilla機械の「Processing中のみ消費」とは意図的に異なる）。
        // Always-on consumer: power is spent every Update (deliberately unlike Vanilla's processing-only spend).
        private bool _usedPower;
        private float _currentPower;
        private double _wearProgress;

        public CleanRoomAirFilterComponent(BlockInstanceId blockInstanceId, double removalVolumePerSecond, float requiredPower, double filterCapacity, CleanRoomAirFilterItemComponent filterInventory)
        {
            BlockInstanceId = blockInstanceId;
            _removalVolumePerSecond = removalVolumePerSecond;
            _requiredPower = requiredPower;
            _filterCapacity = filterCapacity;
            _filterInventory = filterInventory;
        }

        public CleanRoomAirFilterComponent(Dictionary<string, string> componentStates, BlockInstanceId blockInstanceId, double removalVolumePerSecond, float requiredPower, double filterCapacity, CleanRoomAirFilterItemComponent filterInventory)
            : this(blockInstanceId, removalVolumePerSecond, requiredPower, filterCapacity, filterInventory)
        {
            if (!componentStates.TryGetValue(SaveKey, out var stateRaw)) return;
            var json = JsonConvert.DeserializeObject<CleanRoomAirFilterSaveJsonObject>(stateRaw);
            _wearProgress = json.WearProgress;
        }

        #region IElectricConsumer

        public ElectricPower RequestEnergy => new ElectricPower(_requiredPower);

        public void SupplyEnergy(ElectricPower power)
        {
            BlockException.CheckDestroy(this);
            _usedPower = false;
            _currentPower = power.AsPrimitive();
        }

        #endregion

        // q × 電力割合(≤1) × (フィルター残>0 ? 1 : 0)。
        // q × power-ratio(≤1) × (filter present ? 1 : 0).
        public double RemovalVolumePerSecond
        {
            get
            {
                if (!_filterInventory.HasFilter) return 0.0;
                if (_requiredPower <= 0f) return _removalVolumePerSecond;
                var ratio = _currentPower / _requiredPower;
                if (ratio > 1f) ratio = 1f;
                if (ratio < 0f) ratio = 0f;
                return _removalVolumePerSecond * ratio;
            }
        }

        // データストアが今tickの除去量を渡す。累計が filterCapacity を跨ぐごとに1個消費。
        // Datastore pushes this tick's removed amount; consume one filter per capacity crossed.
        public void ApplyRemovedImpurity(double removed)
        {
            BlockException.CheckDestroy(this);
            if (removed <= 0) return;
            _wearProgress += removed;
            while (_wearProgress >= _filterCapacity && _filterInventory.TryConsumeOneFilter())
            {
                _wearProgress -= _filterCapacity;
            }
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
            if (_usedPower)
            {
                _usedPower = false;
                _currentPower = 0f;
            }
            _usedPower = true;
        }

        public string GetSaveState()
        {
            BlockException.CheckDestroy(this);
            return JsonConvert.SerializeObject(new CleanRoomAirFilterSaveJsonObject { WearProgress = _wearProgress });
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }

    public class CleanRoomAirFilterSaveJsonObject
    {
        [JsonProperty("wearProgress")] public double WearProgress;
    }
}
```

> スロット内容のセーブは `CleanRoomAirFilterItemComponent`（SaveKey `cleanRoomAirFilterItem`）が自前で持つ。1ブロックに `IBlockSaveState` が複数あってもよい（`FuelGearGenerator` がitem/fluid/本体の3つで前例）。`BlockException.CheckDestroy` の名前空間は `VanillaMachineProcessorComponent.cs` で確認。フィルター切れで `_wearProgress` に端数が残っても、新フィルター挿入後にその端数から摩耗が再開する仕様でよい（除去が止まっている間は `removed=0` しか来ない）。

- [ ] **Step 4: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Component_RemovalScales|Component_NoFilter|Component_WearCrossing|Component_SaveState"`
Expected: PASS。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomAirFilterComponent.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomAirFilterTest.cs
git commit -m "feat(cleanroom): 単一コンポーネントCleanRoomAirFilterComponent（電力/実効q/摩耗/セーブ）を追加"
```

---

## Task 5: テンプレートと登録

3コンポーネント（item/本体/コネクタ）を組み立てる `VanillaCleanRoomAirFilterTemplate`（New/Load）。`VanillaFuelGearGeneratorTemplate` の「componentStates 有無で ctor を切り替える」方式を踏襲する。`VanillaIBlockTemplates` に登録すると、設置→`CleanRoomAirFilter` ブロックが生成され、`IElectricConsumer` の自動配線が効くようになる。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomAirFilterTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs`

- [ ] **Step 1: 失敗するテストを書く（設置するとコンポーネントが揃う）**

`CleanRoomAirFilterTest.cs` に追加:

```csharp
        [Test]
        public void Template_PlacesAirFilterWithAllComponents()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, UnityEngine.Vector3Int.one,
                BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out var block);

            Assert.IsTrue(block.ExistsComponent<CleanRoomAirFilterComponent>());
            Assert.IsTrue(block.ExistsComponent<CleanRoomAirFilterItemComponent>());
            Assert.IsTrue(block.ExistsComponent<Game.Block.Interface.Component.ICleanRoomAirFilter>());
            Assert.IsTrue(block.ExistsComponent<Game.Block.Interface.Component.IOpenableBlockInventoryComponent>());
            Assert.IsTrue(block.ExistsComponent<Game.Block.Interface.Component.IBlockSaveState>());
        }
```

> `ExistsComponent<T>` は `Game.Block.Interface.Extension.BlockExtension` の拡張（実在確認済み）。`using Game.Block.Interface;`/`using Game.Block.Interface.Extension;` を足す。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Template_PlacesAirFilterWithAllComponents"`
Expected: FAIL（`CleanRoomAirFilter` 未登録で生成不可、または `VanillaCleanRoomAirFilterTemplate` 未定義）。

- [ ] **Step 3: テンプレートを実装**

`Game.Block/Factory/BlockTemplate/VanillaCleanRoomAirFilterTemplate.cs`:

```csharp
using System.Collections.Generic;
using Core.Master;
using Game.Block.Blocks;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    // エアフィルターブロックを組み立てる。item / 本体 / inventoryコネクタ の3コンポーネント。
    // Builds the air filter block: item inventory / core / inventory-connector components.
    public class VanillaCleanRoomAirFilterTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return Build(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return Build(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private IBlock Build(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var param = blockMasterElement.BlockParam as CleanRoomAirFilterBlockParam;
            var filterItemId = MasterHolder.ItemMaster.GetItemId(param.FilterItemGuid);

            // フィルタースロット（Load時はstateを復元）。
            // Filter slots; restore saved state on Load.
            var itemComponent = componentStates == null
                ? new CleanRoomAirFilterItemComponent(param.FilterItemSlotCount, filterItemId, blockInstanceId)
                : new CleanRoomAirFilterItemComponent(componentStates, param.FilterItemSlotCount, filterItemId, blockInstanceId);

            // 本体（電力/実効q/摩耗）。
            // Core component (power / effective q / wear).
            var filterComponent = componentStates == null
                ? new CleanRoomAirFilterComponent(blockInstanceId, param.RemovalVolumePerSecond, param.RequiredPower, param.FilterCapacity, itemComponent)
                : new CleanRoomAirFilterComponent(componentStates, blockInstanceId, param.RemovalVolumePerSecond, param.RequiredPower, param.FilterCapacity, itemComponent);

            // ベルト等からフィルターを搬入できるよう inventory コネクタを付ける（挿入先は itemComponent の IBlockInventory）。
            // Inventory connector so belts can feed filters; insertion targets itemComponent's IBlockInventory.
            var connector = BlockTemplateUtil.CreateInventoryConnector(param.InventoryConnectors, blockPositionInfo);

            var components = new List<IBlockComponent>
            {
                itemComponent,
                filterComponent,
                connector,
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
```

> 確認: `BlockTemplateUtil.CreateInventoryConnector` の引数型と `BlockSystem` のコンストラクタ署名は `VanillaMachineTemplate.cs` と一致（照合済み）。`CleanRoomAirFilterBlockParam` の生成プロパティ名（`RemovalVolumePerSecond`/`RequiredPower`/`FilterCapacity`/`FilterItemGuid`/`FilterItemSlotCount`/`InventoryConnectors`）は Task 1 の生成結果に合わせる。`param.RemovalVolumePerSecond`/`RequiredPower` の生成型（float/double）に合わせてキャストを調整。

- [ ] **Step 4: VanillaIBlockTemplates に登録**

`Game.Block/Factory/VanillaIBlockTemplates.cs` のコンストラクタ内 `BlockTypesDictionary.Add(...)` 群の末尾へ:

```csharp
            BlockTypesDictionary.Add(BlockTypeConst.CleanRoomAirFilter, new VanillaCleanRoomAirFilterTemplate());
```

- [ ] **Step 5: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Template_PlacesAirFilterWithAllComponents"`
Expected: PASS。型未検出なら Unity 再起動。

- [ ] **Step 6: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomAirFilterTemplate.cs moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomAirFilterTest.cs
git commit -m "feat(cleanroom): VanillaCleanRoomAirFilterTemplateを追加しblockType登録"
```

---

## Task 6: 汚染計算（CleanRoomPollutionCalculator・静的ヘルパ＋純関数）

A_total を部屋の V/S・接続点数・稼働機械数から算出する**静的ヘルパ**（注入インターフェースは作らない。codemap §7.9）。worked example の値を**純関数 `ComputeATotal` で固定**する（決定的・部屋構築不要）。

- **doorBurst 引数は持たない**: `burst_door` は瞬間量（個/通過）であり 個/秒 の A_total に合算しない。フェーズ5で `CleanRoom.AddImpurity(burst)` により N へ直接加算する（バランス確定書§2 単位注意）。
- **接続点カウントは `BlockInstanceId` 単位で重複排除**（マルチセル境界ブロックの多重カウント防止。バランス確定書§2）。
- **A_machine の稼働機械数はフェーズ3では常に0**: 対象は `CleanRoomMachine`（フェーズ4導入の専用機械）のみで、その稼働フラグは `Game.Block.Interface` のインターフェース経由でフェーズ4が供給する。**`Game.CleanRoom` から `Game.Block`（実装asmdef）への参照は不可**。係数2.0は純関数テストで固定する。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Pollution/CleanRoomPollutionCalculator.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPollutionTest.cs`

- [ ] **Step 1: 失敗するテストを書く（純関数で基準部屋の A_total）**

`Tests/CombinedTest/Core/CleanRoomPollutionTest.cs` を新規作成:

```csharp
using Game.CleanRoom.Pollution;
using NUnit.Framework;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomPollutionTest
    {
        [Test]
        public void ComputeATotal_ReferenceRoom_MachineLess()
        {
            // 基準部屋(機械なし): V=74, S=109, 接続点2(ItemHatch1+PipeHatch1), ハッチ搬送0。
            // Machine-less reference room: V=74, S=109, connectors=2, hatch throughput 0.
            var aTotal = CleanRoomPollutionCalculator.ComputeATotal(
                volume: 74, surfaceArea: 109, connectorCount: 2, runningMachineCount: 0,
                hatchThroughputPerSecond: 0.0);

            // 0.10*74 + 0.05*109 + 0.50*2 = 13.85
            Assert.AreEqual(13.85, aTotal, 1e-9);
        }

        [Test]
        public void ComputeATotal_MachineTermAddsTwoPerRunningMachine()
        {
            // A_machine=2.0 個/(稼働機械·秒) の係数を固定（実機械の配線はフェーズ4）。
            // Pin the A_machine=2.0 coefficient; actual machine wiring lands in phase 4.
            var withMachine = CleanRoomPollutionCalculator.ComputeATotal(74, 109, 2, 1, 0.0);
            Assert.AreEqual(15.85, withMachine, 1e-9);
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ComputeATotal_ReferenceRoom_MachineLess|ComputeATotal_MachineTermAddsTwoPerRunningMachine"`
Expected: FAIL（`CleanRoomPollutionCalculator` 未定義）。

- [ ] **Step 3: 汚染計算を実装**

`Game.CleanRoom/Pollution/CleanRoomPollutionCalculator.cs`:

```csharp
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom.Pollution
{
    // A_total を部屋ジオメトリ・接続点・稼働機械から算出する静的ヘルパ。CleanRoomDatastore が利用。
    // Static helper computing A_total from geometry, connectors, machines; used by CleanRoomDatastore.
    public static class CleanRoomPollutionCalculator
    {
        // 数値ソース §2（balance-parameters）。
        // Coefficients from balance-parameters §2.
        private const double AVolume = 0.10;
        private const double ASurface = 0.05;
        private const double AConnector = 0.50;
        private const double AMachine = 2.0;
        private const double KHatch = 0.30;

        // 純関数。worked example の固定アサーションはここを叩く。
        // ドアバーストは A_total に含めない（瞬間量。フェーズ5で CleanRoom.AddImpurity へ直接加算）。
        // Pure function; door bursts are NOT part of A_total (instant amount, added straight to N in phase 5).
        public static double ComputeATotal(int volume, int surfaceArea, int connectorCount, int runningMachineCount, double hatchThroughputPerSecond)
        {
            return AMachine * runningMachineCount
                   + KHatch * hatchThroughputPerSecond
                   + AVolume * volume
                   + ASurface * surfaceArea
                   + AConnector * connectorCount;
        }

        // 部屋に面する境界ブロックのうち Wall 以外（各種ハッチ）を接続点として数える。
        // BlockInstanceId 単位で重複排除（マルチセル境界ブロックの多重カウント防止）。
        // Count non-Wall boundary blocks (hatches) facing the room; dedupe by BlockInstanceId.
        public static int CountConnectors(CleanRoom room)
        {
            var world = ServerContext.WorldBlockDatastore;
            var seen = new HashSet<BlockInstanceId>();
            var count = 0;
            foreach (var cell in room.Cells)
            foreach (var n in SixNeighbors(cell))
            {
                if (room.Cells.Contains(n)) continue;
                if (!world.TryGetBlock(n, out var block)) continue;
                if (!seen.Add(block.BlockInstanceId)) continue;
                if (!block.TryGetComponent<ICleanRoomBoundaryComponent>(out var boundary)) continue;
                if (boundary.BoundaryKind != CleanRoomBoundaryKind.Wall) count++;
            }
            return count;
        }

        #region Internal

        static IEnumerable<Vector3Int> SixNeighbors(Vector3Int p)
        {
            yield return new Vector3Int(p.x + 1, p.y, p.z);
            yield return new Vector3Int(p.x - 1, p.y, p.z);
            yield return new Vector3Int(p.x, p.y + 1, p.z);
            yield return new Vector3Int(p.x, p.y - 1, p.z);
            yield return new Vector3Int(p.x, p.y, p.z + 1);
            yield return new Vector3Int(p.x, p.y, p.z - 1);
        }

        #endregion
    }
}
```

> 確認: `ICleanRoomBoundaryComponent`/`CleanRoomBoundaryKind { Wall, DoorHatch, ItemHatch, PipeHatch }` の実名・名前空間はフェーズ1（v2改訂版）の実ファイルで確認（`Game.Block.Interface` 配下のはず）。`room.Cells` の型が `IReadOnlyCollection<Vector3Int>` で `Contains` が遅い実装なら、フェーズ1/2 が持つ高速判定（`HashSet` 直叩き等）に合わせる。フェーズ2のデータストアが境界ブロック集合を既に保持しているなら `CountConnectors` はそれを使ってよい（確認事項#4。Cells 走査は fallback）。`Game.CleanRoom.asmdef` は `Game.Block.Interface`/`Game.Context`/`Game.World.Interface` 参照（フェーズ1で設定済み）で足りる — **`Game.Block` 実装参照を追加してはならない**。

- [ ] **Step 4: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ComputeATotal_ReferenceRoom_MachineLess|ComputeATotal_MachineTermAddsTwoPerRunningMachine"`
Expected: PASS。型未検出なら Unity 再起動。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/Pollution/CleanRoomPollutionCalculator.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPollutionTest.cs
git commit -m "feat(cleanroom): A_totalを算出するCleanRoomPollutionCalculator（純関数+接続点カウント）を追加"
```

---

## Task 7: `CleanRoomDatastore` へ配線し、基準部屋で平衡＋摩耗＋n=2加算を統合テスト

`CleanRoomDatastore.Update` の各部屋ループ（フェーズ2実装済み）に、(a) `CleanRoomPollutionCalculator` で A_total、(b) レジストリから部屋内エアフィルターを取得し `Σ RemovalVolumePerSecond = n·q`、(c) 各台への除去寄与配分 `ApplyRemovedImpurity`（フィルター摩耗）を差し込む。統合テストはポール＋無限発電機で自動給電（`IElectricConsumer` 自動配線の検証）し、**平衡濃度・閾値行A・摩耗累計・n=2加算**の4点を固定する。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPollutionTest.cs`

### データストア改修の要点（フェーズ2の tick に差し込む）

各 `CleanRoom` room について毎tick:
1. `connectorCount = CleanRoomPollutionCalculator.CountConnectors(room)`、`aTotal = ComputeATotal(room.Volume, room.SurfaceArea, connectorCount, runningMachineCount: 0, hatchThroughputPerSecond: 0.0)`（機械数・ハッチ計量はフェーズ4/5で実供給）。
2. エアフィルター収集: `room.Cells` を走査し `TryGetComponent<ICleanRoomAirFilter>` を持つブロックを集める（`BlockInstanceId` で重複排除。`Cells` は占有セルを含むため内部ブロックは必ず引っかかる）。`nq = Σ src.RemovalVolumePerSecond`。
3. `removedTotal = min(nq · room.Concentration · GameUpdater.SecondsPerTick, room.ImpurityCount)`（N をマイナスにしない）。
4. `room.AddImpurity(aTotal · GameUpdater.SecondsPerTick)` → `room.RemoveImpurity(removedTotal)`。
5. **除去寄与の配分**: 各台へ `src.ApplyRemovedImpurity(removedTotal · src.RemovalVolumePerSecond / nq)`（nq>0 かつ removedTotal>0 のとき）。これでフィルターが汚染レートに比例して摩耗。
6. 閾値行/状態判定はフェーズ2の既存ロジック（`CleanRoomPurityRules`。`ACH = nq/V` を渡す。ヒステリシスは `ThresholdIndex` 保持）。

実装中核（メンバ名はフェーズ2実装に合わせる）:

```csharp
        // 部屋内のエアフィルターを集めて n·q を得る。重複は BlockInstanceId で排除。
        // Collect in-room air filters for n·q; dedupe by BlockInstanceId.
        // フェーズ2レジストリから部屋内のエアフィルターを引く（毎tickのCells走査はしない。確認事項#4）
        // Pull in-room air filters from the phase-2 registry (no per-tick cell scan; see reconcile #4)
        private List<ICleanRoomAirFilter> CollectAirFilters(CleanRoom room)
        {
            var result = new List<ICleanRoomAirFilter>();
            foreach (var (cell, filter) in _airFilterRegistry)
                if (room.Cells.Contains(cell)) result.Add(filter);
            return result;
        }

        // 実ブロックの登録/解除はデータストア自身の設置/削除購読が行う（フェーズ2の AddAirFilter/RemoveAirFilter を呼ぶ）
        // Real blocks are (un)registered by the datastore's own place/remove subscription via AddAirFilter/RemoveAirFilter
        // OnBlockPlace: block.TryGetComponent<ICleanRoomAirFilter>(out var f) → AddAirFilter(pos, f)
        // OnBlockRemove: RemoveAirFilter(pos)
```

```csharp
            // 汚染加算と除去（除去は N を負にしない範囲）。
            // Add pollution and remove impurity; removal never drives N below zero.
            var connectorCount = CleanRoomPollutionCalculator.CountConnectors(room);
            var aTotal = CleanRoomPollutionCalculator.ComputeATotal(room.Volume, room.SurfaceArea, connectorCount, 0, 0.0);

            var filters = CollectAirFilters(room);
            double nq = 0;
            foreach (var f in filters) nq += f.RemovalVolumePerSecond;

            var removedTotal = nq * room.Concentration * GameUpdater.SecondsPerTick;
            if (removedTotal > room.ImpurityCount) removedTotal = room.ImpurityCount;

            room.AddImpurity(aTotal * GameUpdater.SecondsPerTick);
            room.RemoveImpurity(removedTotal);

            // 除去寄与をフィルターへ配分（汚染レート比例の摩耗）。
            // Distribute removed impurity to filters (wear proportional to pollution rate).
            if (nq > 0 && removedTotal > 0)
                foreach (var f in filters)
                    f.ApplyRemovedImpurity(removedTotal * (f.RemovalVolumePerSecond / nq));

            // 以降は フェーズ2 既存の閾値行/状態更新（ACH = nq / room.Volume を渡す）。
            // Then phase-2's existing threshold/status update with ACH = nq / room.Volume.
```

> `GameUpdater.SecondsPerTick`（=0.05）は `Core.Update.GameUpdater` の実在定数。ローカルに `const double secondsPerTick = 0.05;` を作らない。フェーズ2のデータストアが既にブロック→部屋登録マップ（codemap §1.4）を持つなら `CollectAirFilters` はそれを引く形に置き換えてよい（確認事項#4）。

- [ ] **Step 1: 失敗する統合テストを書く（基準部屋・ポール給電・平衡＋摩耗）**

`CleanRoomPollutionTest.cs` に追加。基準部屋は内寸 5×5×3 の空洞を `CleanRoomWall` で囲い、境界の壁2枚を `CleanRoomItemHatch`/`CleanRoomPipeHatch` に差し替える（接続点2）。エアフィルター1台を**側壁に接しない床セル**に置く（V=74, S=109 になる）。電柱＋無限発電機は部屋外（接続レンジ内）:

```csharp
        [Test]
        public void AirFilter_PoweredInSealedReferenceRoom_EquilibratesAndWearsFilter()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<Game.CleanRoom.CleanRoomDatastore>();

            // 内寸 5x5x3 を壁で囲い、ItemHatch 1 + PipeHatch 1 を境界に差し込む（接続点2）。
            // Seal a 5x5x3 cavity; swap 2 wall blocks for ItemHatch + PipeHatch (connectors=2).
            BuildReferenceRoom(world);

            // エアフィルター1台を側壁に接しない床セルへ（V=74, S=109）。
            // One air filter on a floor cell not touching side walls (V=74, S=109).
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomAirFilterId, AirFilterPos, BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out var filterBlock);
            var filterComponent = filterBlock.GetComponent<CleanRoomAirFilterComponent>();
            var filterInventory = filterBlock.GetComponent<CleanRoomAirFilterItemComponent>();

            // フィルター2個投入（摩耗は filterCapacity=5000 未満に収まり消費は起きない想定）。
            // Load 2 filters; expected wear stays below filterCapacity=5000 (no consumption).
            filterInventory.InsertItem(ServerContext.ItemStackFactory.Create(ForUnitTestModBlockId.CleanRoomFilterItemGuid, 2));

            // 電柱1本+無限発電機で自動給電（機械→ポールの設置順でも ConnectElectricPoleToElectricSegment が接続する）。
            // One pole + infinite generator auto-powers the filter (pole-after-machine order is handled).
            world.TryAddBlock(ForUnitTestModBlockId.ElectricPoleId, PolePos, BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out _);
            world.TryAddBlock(ForUnitTestModBlockId.InfinityGeneratorId, GeneratorPos, BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out _);

            // τ=V/(nq)=74/5≈14.8s。±10%摩耗帯は t≥10τ で成立するため 300s（6000tick≈20τ）回す。
            // τ≈14.8s; run 300s (6000 ticks ≈ 20τ) so the ±10% wear band holds (needs t ≥ 10τ).
            GameUpdater.RunFrames(6000);

            Assert.IsTrue(datastore.TryGetCleanRoomAt(InsideEmptyCellPos, out var room), "room exists");
            Assert.AreEqual(74, room.Volume, "V=74 (占有セル除外)");
            Assert.AreEqual(109, room.SurfaceArea, "S=109");

            // C_eq = 13.85/5 = 2.77（±0.3）。閾値行はA(index 0)。ACH=5/74≈0.0676≥0.0167。
            // C_eq=2.77 (±0.3); threshold row A (index 0); ACH satisfied.
            Assert.AreEqual(2.77, room.Concentration, 0.3, "equilibrium concentration ~2.77");
            Assert.AreEqual(0, room.ThresholdIndex, "threshold row A");

            // 摩耗配線の検証（必須・バランス確定書§3）: A_total·t=4155 の±10%帯。理論値 ≈ 4155−N_eq ≈ 3950。
            // Wear-wiring assertion (mandatory): within ±10% of A_total·t=4155; theory ≈ 3950.
            Assert.That(filterComponent.WearProgress, Is.InRange(3739.5, 4570.5), "wear ≈ A_total×t (±10%)");
            Assert.AreEqual(2, filterInventory.FilterCount, "5000未満なのでフィルター未消費");
        }
```

- [ ] **Step 2: 失敗する n=2 加算テストを書く**

`CleanRoomPollutionTest.cs` に追加（同じ部屋ヘルパでエアフィルター2台 → V=73, S=108, nq=10）:

```csharp
        [Test]
        public void AirFilter_TwoUnits_RemovalAddsAndWearIsShared()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<Game.CleanRoom.CleanRoomDatastore>();

            BuildReferenceRoom(world);

            // 2台（どちらも側壁に接しない床セル）→ V=73, S=108, A_total=13.7。
            // Two units on interior floor cells => V=73, S=108, A_total=13.7.
            var blocks = PlaceTwoAirFiltersWithOneFilterEach(world);
            PlacePoleAndInfinityGenerator(world);

            // τ=73/10=7.3s → 75s(1500tick≈10τ) で平衡。
            // τ=7.3s; 75s (1500 ticks ≈ 10τ) reaches equilibrium.
            GameUpdater.RunFrames(1500);

            Assert.IsTrue(datastore.TryGetCleanRoomAt(InsideEmptyCellPos, out var room));
            Assert.AreEqual(73, room.Volume);
            Assert.AreEqual(108, room.SurfaceArea);

            // n·q が加算されていれば C_eq = 13.7/10 = 1.37。1台分(nq=5)なら 2.74 になり明確に区別できる。
            // If additive, C_eq=1.37; a non-additive bug (nq=5) would read 2.74 — clearly distinguishable.
            Assert.AreEqual(1.37, room.Concentration, 0.2, "n·q additive equilibrium");

            // 摩耗は同能力2台で等分配される。
            // Wear splits equally across two identical units.
            var w1 = blocks.filter1.GetComponent<CleanRoomAirFilterComponent>().WearProgress;
            var w2 = blocks.filter2.GetComponent<CleanRoomAirFilterComponent>().WearProgress;
            Assert.Greater(w1, 300.0, "unit1 wears");
            Assert.Greater(w2, 300.0, "unit2 wears");
            Assert.AreEqual(w1, w2, 1.0, "equal share for identical units");
        }
```

> ヘルパ（`BuildReferenceRoom`/`AirFilterPos`/`InsideEmptyCellPos`/`PolePos`/`GeneratorPos`/`PlaceTwoAirFiltersWithOneFilterEach`/`PlacePoleAndInfinityGenerator`）は実コードに合わせて埋める。壁/ハッチのブロックIDはフェーズ1がテストmodへ追加済みのアクセサ（確認事項#5）を使う。ポール/発電機はポールの machineConnectionRange 内にエアフィルターが入る座標を選ぶ（既存 `DisconnectElectricSegmentTest` 等の配置を参考。壁越しでも電力接続は幾何距離のみで成立する）。部屋検出は dirty 処理経由で数tick遅れて成立するが、t が十分長いので期待帯に収まる。`room.Concentration ≈ 2.77` が出ない場合の切り分け: (a) V/S が 74/109 か（占有セル除外の確認）、(b) 給電が届いているか（`filterComponent.RemovalVolumePerSecond > 0`）、(c) 接続点カウントが2か、(d) tick 数が足りているか。

- [ ] **Step 3: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "AirFilter_PoweredInSealedReferenceRoom|AirFilter_TwoUnits"`
Expected: FAIL（データストア未配線で nq=0 → C 発散、または摩耗0）。

- [ ] **Step 4: `CleanRoomDatastore` を改修（上記「要点 1〜6」を実装）**

- [ ] **Step 5: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "AirFilter_PoweredInSealedReferenceRoom|AirFilter_TwoUnits"`
Expected: PASS。型未検出なら Unity 再起動。

- [ ] **Step 6: CleanRoom 全体回帰**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"`
Expected: フェーズ1〜3の全テストPASS。

- [ ] **Step 7: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPollutionTest.cs
git commit -m "feat(cleanroom): エアフィルターをCleanRoomDatastoreへ配線し平衡/摩耗/n=2加算を統合テストで固定"
```

---

## Self-Review

### コードマップ v2 §3 の網羅

| §3 の項目 | 対応タスク |
|---|---|
| blocks.yml `CleanRoomAirFilter`＋param（q/requiredPower/filterCapacity/filterItemGuid/スロット）、`_CompileRequester` トリガ、テストmod | Task 1 |
| `ICleanRoomAirFilter`（`Game.Block.Interface/Component`、`RemovalVolumePerSecond`＋`ApplyRemovedImpurity`） | Task 2 |
| `CleanRoomAirFilterComponent`（**単一コンポーネント初版**: `IElectricConsumer, IUpdatableBlockComponent, IBlockSaveState, ICleanRoomAirFilter`） | Task 4 |
| フィルタースロット（`IOpenableBlockInventoryComponent`・ベルト搬入/UI成立・種チェック） | Task 3 |
| `VanillaCleanRoomAirFilterTemplate`（New/Load）＋ `VanillaIBlockTemplates` 登録 | Task 5 |
| `CleanRoomPollutionCalculator`（具体ヘルパ。注入インターフェース無し） | Task 6 |
| `CleanRoomDatastore` 配線（n·q 集計＋`ApplyRemovedImpurity` プッシュ配分） | Task 7 |
| 自動電力接続（`ConnectMachineToElectricSegment`/`ConnectElectricPoleToElectricSegment`）／電力割合換算 | Task 4（割合）/ Task 7（自動接続を統合テストで検証） |

### 設計書 §5/§6 の網羅

- §5 汚染源: `a_volume·V`/`a_surface·S`/`a_connector·接続点`（`BlockInstanceId` 重複排除）を Task 6/7 で実供給。`A_machine` は係数と純関数の口を Task 6 で固定し、実機械（`CleanRoomMachine` の稼働フラグ）はフェーズ4が供給。`A_hatch`（レート換算）は `ComputeATotal` の引数に口だけ用意し0、`A_door` バーストは **A_total に入れず** フェーズ5で N 直接加算（単位整合）。
- §6 エアフィルター＋フィルター: 電力消費（Task 4・常時消費）、仕事量ベースのフィルター消費＝除去量比例（Task 4＋Task 7 の配分）、**摩耗配線を統合テストで固定**（Task 7・±10%帯）、複数台で n·q 加算（Task 7 の n=2 テスト）、フィルター切れで除去0（Task 4）、誤投入アイテムを消費しない種チェック（Task 3）。汚い部屋ほどフィルターを食う＝平衡時消費が A_total 比例（摩耗アサーションが直接固定）。高級フィルター種別は後追い（§6「後から足せる」。`filterItemGuid` が拡張点）。

### 批判的レビュー指摘の反映状況

| 指摘 | 反映 |
|---|---|
| M1 フィルターインベントリへ到達不能 | Task 3 をブロックコンポーネント化＋Task 5 で components 登録。`GetComponent<CleanRoomAirFilterItemComponent>` で取得可能 |
| M2 inventoryConnectors が死にパラメータ | `IOpenableBlockInventoryComponent`（=`IBlockInventory`）実装とセットで登録。ベルト搬入が成立 |
| M3 摩耗配線が無検証 | Task 7 keystone に摩耗アサーション必須化（A_total·t ±10% 帯。t≥10τ の成立条件も明記） |
| M4 スキーマ方言誤り | Task 1 Step 2 を実 blocks.yml 方言（`- key:` リスト＋`ref: inventoryConnects`＋uuid foreignKey）で記載 |
| S1 filterCapacity ハードコード／フィルター識別なし | `filterCapacity`/`filterItemGuid` を blockParam 化（Task 1）。消費時種チェック（Task 3） |
| S2 requestPower 命名 | `requiredPower` に統一（スキーマ/JSON。C#プロパティは `RequestEnergy` 流儀のまま） |
| S3 接続点の多重カウント | `CountConnectors` を `BlockInstanceId` 重複排除に（Task 6） |
| S4 メソッド名の計画間不一致 | `CleanRoomPollutionInput` 自体を廃止（codemap v2）。静的ヘルパ直呼びで契約名問題を解消 |
| S5 占有セルとVの矛盾 | バランス確定書§5 の確定値（Cells=占有含む/V=空セルのみ）として記載。基準部屋を V=74/S=109 で再計算 |
| S6 汎用機械を汚染源に数える | `A_machine` 対象を `CleanRoomMachine`（フェーズ4）に限定。フェーズ3は純関数で係数のみ固定。`Game.Block` 実装asmdef参照は不可と明記 |
| S7 機械稼働持続のテスト脆弱性 | keystone を**機械なし部屋**基準に変更（A_machine 配線テストはフェーズ4へ） |
| C1 doorBurst の次元不整合 | `ComputeATotal` から doorBurst 引数を削除。フェーズ5は `AddImpurity` 直接加算 |
| C2 using 不足 | `.ToItemStack()`＝`Game.Context` 拡張である旨を Task 3 に明記 |
| C3 未使用依存 | `CleanRoomPollutionCalculator` を静的クラス化（ctor依存なし） |
| C4 コメントの接続点内訳矛盾 | 全テストコメントを ItemHatch+PipeHatch に統一 |
| C5 n=2加算/枯渇テスト不在 | Task 7 に n=2 統合テスト追加。枯渇は Task 4 の単体テスト（`Component_WearCrossing...`）で固定 |
| C6 「Vanillaと同じパターン」コメント不正確 | 常時消費が意図的差分である旨を実装コメントに明記（Task 4） |

### プレースホルダ・スキャン

- `...`（省略記法）は Task 1 の blocks.json `inventoryConnectors`（「既存 `TestElectricMachine` エントリをコピー」と明記。コピー元実在を確認済み）のみ。
- 数値は全てバランス確定書の値（q=5.0/requiredPower=100/filterCapacity=5000/a_volume=0.10/a_surface=0.05/a_connector=0.50/A_machine=2.0/k_hatch=0.30/`GameUpdater.SecondsPerTick`=0.05）。worked example はフェーズ3用に V=74/S=109（占有セル除外・機械なし）で再導出し、`13.85`/`2.77`/摩耗帯 `[3739.5, 4570.5]` をテストで固定。

### 型整合（実コード照合済み）

- `IElectricConsumer`（`BlockInstanceId`/`RequestEnergy`/`SupplyEnergy(ElectricPower)`）、`ElectricPower(.AsPrimitive)`、`IBlockSaveState`（`SaveKey`/`GetSaveState`）、`IUpdatableBlockComponent`、`IOpenableBlockInventoryComponent : IBlockInventory, IOpenableInventory`、`OpenableInventoryItemDataStoreService` ctor、`IItemStack.SubItem(int)`、`ItemStackSaveJsonObject`＋`ToItemStack()`（Game.Context拡張）、`MasterHolder.ItemMaster.GetItemId(Guid)`、`BlockSystem` ctor、`BlockTemplateUtil.CreateInventoryConnector`、`TryAddBlock(blockId, pos, dir, createParams, out block)`、`GameUpdater.RunFrames/SecondsPerTick`、`EnergySegment`（毎tick供給・供給率≤1）、`ConnectElectricPoleToElectricSegment`（ポール後置きでも接続）は実ファイルで確認済み。
- フェーズ1/2 並行改訂中の型（`CleanRoomDatastore`/`CleanRoom`/`ICleanRoomAirFilter`/`ICleanRoomBoundaryComponent`/`CleanRoomPurityRules`）は冒頭「確認事項」リストに従い着手時に照合。
