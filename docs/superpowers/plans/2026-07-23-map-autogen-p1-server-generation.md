# マップ自動生成 P1（サーバー側生成基盤）Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ワールド新規作成時にseedからmap.json＋地形データを1回だけ生成しワールドディレクトリに永続化、以後は既存ロード経路で起動できるサーバー側基盤を作る。

**Architecture:** ワールドディレクトリ（world.json / map.json / terrain/ / cache/ / save.json）の**レイアウト真実源として `WorldDataDirectory` 値オブジェクトを新設**し、プロビジョナ・DIコンテナ・セーブ/ロードの全員がこのクラス経由でパスを得る（パスの文字列連結を各所に散らさない）。`TmpUnityPjt/MapMaking` の MapGenerator からデータ生成部（分類・高さ・木/オブジェクト配置・鉱脈）のみを新アセンブリ `Game.MapGeneration` に移植し、DIコンテナ構築**前**に走る `WorldProvisioner` がワールドディレクトリを**アトミックに**（一時ディレクトリ生成→リネーム確定）整備する。既存の `MapInfoJson`→Datastore ロード経路は無改修。見た目専用ステージ（テクスチャ・草花）は移植しない。

**Tech Stack:** Unity 6 / C# / Newtonsoft.Json / Unity.Burst + Collections + Mathematics（Jobsステージ用）/ NUnit（Server.Tests）

**親スペック:** `docs/plans/map-autogen-world-design.md`（P2以降=プロトコル/クライアントは本プランのスコープ外。P1完了後に別プランを起こす）

## Global Constraints

- 1ファイル200行以下。超える場合はディレクトリ分割（partial は如何なる条件でも絶対禁止）
- 1ディレクトリ10ファイルまで。超えたらサブディレクトリ化
- try-catch 基本禁止（外部境界のみ可・境界根拠コメント必須）。デフォルト引数禁止。単純getter/setterプロパティ禁止（Setは `SetHoge` メソッド）
- コメントは日本語→英語の2行セット（各1行）を3〜10行ごと。自明なコメント禁止
- イベントは Action でなく UniRx（本プランではイベント新設なし）
- .metaファイル手動作成禁止（Unity起動時の自動生成に任せ、生成されたものはコミット可）
- .cs変更後は必ず `uloop compile --project-path ./moorestech_client` を実行（サーバーコードはクライアントに `tech.moores.server` として取り込まれるためクライアント側コンパイルで両方検証できる）
- テスト実行: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。ドメインリロードエラー時は45秒待ってリトライ
- 永続化はGUID・可読JSON（揮発int ID保存禁止・マスタ由来値保存禁止）。terrain/のみ画像相当データとしてバイナリ例外（spec明記済み）
- 開発フェーズにつき旧セーブ（saves/save_1.json）との互換・マイグレーション対応は不要
- 各タスク完了ごとにコミット。作業ブランチ: `feat/map-autogen-p1`（masterから作成）。コミット前に `git log`/`git status` で無関係ファイルの巻き込みがないか確認

---

## 配置と前例（spec-architecture-review 実施結果）

### データフロー地図

```
(seed/CLI設定) → WorldProvisioner【書き手】 → [WorldDataDirectory が指すワールドディレクトリのファイル群] → 既存ローダー(MoorestechServerDIContainerGenerator) → MapInfoJson(DI) → Datastore群 → ゲーム
```

WorldProvisioner はワールドディレクトリへの**書き手が1人増えるだけ**。WorldDataDirectory は「どこに何があるか」を答えるだけの値オブジェクトで、制御フローに参加しない。既存ロード機構への抑止・迂回・並行経路なし（検査4: 受動的統合。既存機構は無傷で正のまま）。

### 配置決定インベントリと前例

| # | 項目 | 配置先 | 前例（役割同型） | 判定 |
|---|---|---|---|---|
| 1 | `WorldDataDirectory`（ワールドディレクトリのレイアウト真実源・値オブジェクト） | `Game.Paths` | `GameSystemPaths`（パス解決の所有層）・`SaveJsonFilePath`（DI注入されるパス値オブジェクト。本クラスが**置換・吸収**する） | ok（置換ゲート: 置換対象と同じ「コンストラクタ注入の値オブジェクト」機構を踏襲。機構変更なし） |
| 2 | `SaveJsonFilePath` 削除・全参照元（Game.SaveLoad/Server.Boot/Tests 計9ファイル）を WorldDataDirectory へ更新 | 各所 | AGENTS「変更の波及を恐れない」 | ok |
| 3 | 新アセンブリ `Game.MapGeneration` | `moorestech_server/Assets/Scripts/Game.MapGeneration/` | `Game.Map/Game.Map.asmdef`（Game.*層の構成・参照形式） | ok |
| 4 | 生成ステージ・Jobs・Util（移植） | Game.MapGeneration 配下 | MapMaking の `Pipeline/` 構造を踏襲しつつ200行/10ファイル規約に再分割 | ok |
| 5 | 生成設定マスタ（Mooresmaster管理・mooreseditor編集可。アルゴリズムenum＋switchパラメータ＋優先度の3要素） | `VanillaSchema/generation.yml` → `Mooresmaster.Model.GenerationModule`、JSONは mod マスタデータ内 `generation.json` | 既存マスタ4段階管理＋blocks.yml の blockParam switch（アルゴリズム別パラメータの前例） | ok（**ユーザー裁定済み**: 手書きPOCO案棄却→Mooresmaster化、複数mod優先度解決も裁定済み） |
| 5b | 鉱脈マスタ `mapVeins`（veinGuid・veinType[item\|fluid] switch・対象GUID） | `mapObjects.yml` を **`map.yml` にリネーム**しトップレベル第2配列として統合（新yaml新設は禁止・親spec ADR#2/#3）。Core.Master に `MapVeinMaster.cs` 新設 | 統合の前例: items.yml の modules/levelFamilies（41b3eacb4）・buildMenu.yml の connectTools。ラッパー前例: `ConnectToolMaster.cs` | ok（**ユーザー裁定済み 2026-07-24**: 新yaml不可・map.yml名称） |
| 6 | `WorldProvisioner`（DI構築前に実行・アトミック確定） | Game.MapGeneration（呼び出しは `Server.Boot/ServerInstanceManager`） | アトミック書き込みは `WorldSaverForJson` の .tmp→File.Replace パターンをディレクトリ単位に適用 | **新規パターン注目点②**（DI前工程自体が初） |
| 7 | `world.json`（seed・generatorVersion・mapMode） | Game.MapGeneration の手書きPOCO | `WorldSettingJsonObject`（可読JSONメタ） | ok（seedの真実源をworld.jsonに置く点は注目点②に含む） |
| 8 | `MoorestechServerDIContainerOptions` の `saveJsonFilePath` を `worldDataDirectory` プロパティに置換 | `Server.Boot` | 同クラスの既存プロパティ形式（初期化子でデフォルト値） | ok |
| 9 | `WorldDataDirectory.FromServerDataMap` レガシー形状ファクトリ（テスト・クライアント早期DI用） | Game.Paths | 前例なし（427箇所のテスト直構築が「ワールドディレクトリを持たずDIを組む」既存現実の明示化） | **新規パターン注目点③** |
| 10 | `--saveFilePath` を `--worldDirectory`/`--mapMode`/`--seed` に置換（呼び出し側全更新） | `Server.Boot/Args` | Option属性形式は現行踏襲 | ok |
| 11 | instanceId採番・鉱脈クラスタ→AABB変換 | Game.MapGeneration/Export | `MapExportAndSetting.cs`（クライアントEditor。シーン→MapInfoJson変換の役割同型） | ok |
| 12 | terrain/バイナリ書き出し | Game.MapGeneration/Export | 永続化JSON原則の例外（画像相当・spec明記） | ok |

**検査1（層責務）**: WorldDataDirectory は「ワールドデータのパスレイアウト」というドメイン非依存知識のみを持ち（ゲーム概念のロジックなし）、パス所有層 Game.Paths に置く。マップ生成ロジックは `Game.MapGeneration` に閉じ、`Core.*`・`Game.Map`（ロード側）・`Server.Boot` への生成ロジック混入なし。Server.Boot は「呼ぶだけ」。
**検査3（イディオム）**: 永続化はGUID＋可読JSON、通信変更なし、Mooresmaster生成物には手を出さない（GUID参照のみ）。

### 新規パターン（ユーザーレビュー注目点）

1. ~~生成設定を手書きPOCO+JSONにする~~ → **裁定済み（2026-07-23）: Mooresmaster管理に変更**。`VanillaSchema/generation.yml` を新設し、mooreseditor で編集可能にする。`mapObjectGuid`/`veinGuid` は `foreignKey` で map.yml の mapObjects/mapVeins への実在検証を掛ける（validate-schema スキル準拠のC#バリデーションも追加。veinGuid参照化は2026-07-24ユーザー裁定・親spec ADR#1〜#6）。**複数mod対応も裁定済み**: generation.yml は「アルゴリズム(enum)・パラメーター(`switch: ./algorithm` のcase別)・優先度(int)」の3要素構造。優先度最大のmodの設定を1件採用、同優先度は mod id 文字列のOrdinal昇順で若い方。アルゴリズムenum→実装のテーブル（`MapGenerationAlgorithmTable`）でディスパッチして生成する
2. **DIコンテナ構築前のプロビジョニング工程の新設**: `MapInfoJson` ロードがDI構築冒頭にあるため、生成はその前に完了している必要がある。既存に前例のない工程のため裁定対象。**裁定①の帰結**: 生成設定がマスタになったため、`EnsureWorld`（generated時）の前に `MasterJsonFileContainer` ロード＋`MasterHolder.Load` を先行実行する（`Create()` 内の再ロードは冪等な上書きで許容・コストは起動時1回）
3. **レガシー形状ファクトリ `FromServerDataMap`**: 427箇所のテストとクライアント早期DIは「ワールドディレクトリなしでDIを組む」使い方をしており、これを `WorldDataDirectory` の名前付きファクトリとして明示化した（実行時分岐やフォールバックではなく、構築時にどちらの形かを呼び出し側が宣言する）。テスト427箇所を無改修に保つための設計判断

### 発見した設計の穴と対処（本改訂で追加）

| 穴 | 対処 |
|---|---|
| ワールドディレクトリのパスが `SaveJsonFilePath`・`mapJsonFilePath`・文字列連結に分散し、レイアウトの真実源が無い | `WorldDataDirectory` に一元化（Task 2）。以後P2以降のプロトコル実装も同クラスを参照する |
| プロビジョニング途中のクラッシュで「world.jsonはあるがterrainが無い」等の壊れたワールドが残り、次回起動で不完全なまま走る | 一時ディレクトリ `<root>.provisioning` に全ファイルを書き切ってから `Directory.Move` で確定（Task 8）。起動時に残骸 `.provisioning` を削除。root が在るのに world.json が無い場合は破損として即例外 |
| テストのセーブパスがデフォルト＝実ユーザーの `saves/save_1.json` を指している（保存を呼ばないため顕在化していないだけ） | 既存の穴として認識。`FromServerDataMap` に明示パスを渡す形は維持しつつ、本プランでは挙動を変えない（テスト既定パスの隔離は別課題として親スペックのリスク欄に記録） |
| エディタの「セーブをロード・保存しない再生」（`InitializeScenePipeline.ApplySkipSaveLoadModeIfNeeded` のランダムセーブファイル名）が `--saveFilePath` 廃止で壊れる | ランダムな一時ワールドディレクトリ指定（`--worldDirectory`）へ書き換える（Task 9で明示的に対象化） |
| `CliConvertTest` が `SaveFilePath` オプションを参照しており置換で壊れる | Task 9 の参照元全更新に明示的に含める |

### 機能パリティ（死活表）

| 現在使える操作 | P1後 | 根拠 |
|---|---|---|
| シングルプレイ起動（現行v8マップ） | 生存 | デフォルト mapMode=template が `ServerDataDirectory/map/map.json` を world_1 にコピー。vein部分は veinGuid参照の `mapVeins` 単一配列へ一括移行済みの内容になる（Task 7。レイアウト・採掘挙動は等価）。クライアントのベイクシーンとinstanceId整合も維持 |
| Miner/Pumpの採掘・流体生成判定 | 生存・無改修 | `ItemMapVeinDatastore`/`FluidMapVeinDatastore` がロード時に `MasterHolder.MapVeinMaster` の veinType で振り分けて従来と同じ内部表現を構築（Task 7）。ブロック側コードは触らない |
| 既存テスト群（options直構築 427箇所） | 生存・無改修 | options コンストラクタが従来通り `serverDataDirectory` のみで成立（内部で `FromServerDataMap` を組む）。map.json解決パス・セーブ既定パスとも従来値 |
| エディタ「セーブをロード・保存しない再生」 | 生存 | ランダム一時ワールドディレクトリ指定に書き換え（Task 9） |
| 旧 `saves/save_1.json` の続きプレイ | **消える**（新パスは `saves/world_1/save.json`） | 開発フェーズにつき互換不要（ユーザー裁定済み・AGENTS準拠） |
| セーブ/オートセーブ/バックアップ(.bak) | 生存 | `WorldSaverForJson` の .tmp/.bak 機構は無改修。パスの出所が WorldDataDirectory になるだけ |

---

## 移植対象と除外（Task 3-5 の共通リファレンス）

移植元: `/Users/katsumi/moorestech/TmpUnityPjt/MapMaking/Assets/MapGenerator/`

**移植する（ゲームプレイに効くデータ生成）:**
- `Pipeline/Jobs/` 全ファイル（ClassificationJob, HeightSampleJob, BurstNoise 等 — 数値計算のみでシーン非依存）
- `Pipeline/Generators/` のうち `TreePlacementGenerator.cs`, `ObjectPlacementGenerator.cs`, `OrePlacementGenerator.cs`, `OreBandPlanner.cs`, `Util/`（PoissonDiskSampler, SpatialGrid, BiomeMaskBuilder, SdfMapGenerator, ManagedNoise, CurvatureComputer）
- `Pipeline/Biomes/` 全ファイル（BiomeType, BiomeFlags, 各BiomeConfig — SO継承を外しPOCO化）
- `Pipeline/Config/` のうち高さ・配置・鉱脈系（TerrainGenerationConfig, TerrainDimensions, OreEntry, OreBand, WorldOreConfig, TreePlacementConfig, TreePrototypeEntry, TreeDensityConfig, UnderstoryConfig, RockProximityTreeConfig, ObjectClusterEntry, ObjectClusterSecondary, ObjectAlgorithmConfig, PlacementEntry, PlacementFilter, PlacementNoise, NoiseType, NoiseOp, BiomeBoundaryConfig, BiomeShoreConfig）
- `Pipeline/Spawn/` 全ファイル（SpawnRegionFinder系）
- `Pipeline/TerrainGenerator.cs` の Stage1-4,6 相当（**200行規約に合わせステージ別クラスへ分割**）

**コードは移植しない（見た目適用ロジック・クライアントP3で扱う）:**
- `TerrainApplier.cs`, `MapGeneratorFacade.cs`, `InfiniteTerrainManager.cs`, `Editor/` 全部
- `Pipeline/Generators/DetailPlacementGenerator.cs`（草花配置の適用ロジック）
- `Pipeline/Diagnostics/`, `PipelineProfiler.cs`（診断。必要になったら後続で）
- `LabelAttribute.cs`（Inspector表示用。スキーマ化で不要）

**ただしデータは統合する（ユーザー裁定済み・複数箇所管理の禁止）:**
- 見た目系Config（BiomeTextureConfig, BiomeDetailConfig, DetailFilter, DetailNoiseLayer, DetailNoiseStack, DetailPrototypeConfig, DetailTextureFilter, TextureChannel, ObjectSurroundTextureConfig, BiomeObjectConfig内の見た目フィールド）**のスキーマは generation.yml に含める**（Task 4）。クライアント側SOアセットとしての分離管理は禁止
- 見た目Config内のアセット参照（`TerrainLayer`/`Texture2D`/プレハブ）は全て **Addressablesアドレス(string)** フィールドに置換。データの真実源はgenerationマスタ、アセット実体はAddressablesの2層のみ
- サーバーの生成パイプライン（Task 5）は見た目セクションを読まない。P3のクライアント地形構築が `MasterHolder.GenerationMaster` から読む

**Config群のスキーマ化ルール（Task 4。ユーザー裁定でPOCO移植からMooresmaster管理へ変更）:**
- `Pipeline/Config/`・`Pipeline/Biomes/` の設定クラスはコード移植せず、`VanillaSchema/generation.yml` としてスキーマ化し SourceGenerator の生成型（`Mooresmaster.Model.GenerationModule`）を使う
- `GameObject`/`Transform`/`Texture2D` 等のアセット参照フィールドは**削除**し、対応する `mapObjectGuid` / `veinGuid`（string, foreignKey付き。veinGuid は map.yml の mapVeins を参照）フィールドに置き換える
- 型変換の詳細（AnimationCurve→keyframe配列、BiomeFlags→enum配列、未使用のテクスチャノイズソース削除）は Task 4 に記載
- Jobs/Generators 等のロジックコードは従来どおり移植し、`UnityEngine.Mathf`/`Vector2`/`Vector3`・`Unity.Mathematics` は使用可（サーバーもUnityプロセス）

---

### Task 1: Game.MapGeneration アセンブリ新設とパッケージ参照

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Game.MapGeneration.asmdef`
- Modify: `moorestech_server/Packages/manifest.json`（dependencies に3行追加）

**Interfaces:**
- Produces: アセンブリ `Game.MapGeneration`（後続タスクの生成コードがここに入る）

- [ ] **Step 1: ブランチ作成**

```bash
cd /Users/katsumi/moorestech && git switch -c feat/map-autogen-p1
```

- [ ] **Step 2: manifest.json に Burst 系を明示追加**

`moorestech_server/Packages/manifest.json` の `"dependencies"` 内に追加（packages-lock で解決済みのバージョンに合わせる）:

```json
    "com.unity.burst": "1.8.23",
    "com.unity.collections": "2.4.3",
    "com.unity.mathematics": "1.3.2",
```

- [ ] **Step 3: asmdef 作成**

```json
{
  "name": "Game.MapGeneration",
  "rootNamespace": "",
  "references": [
    "Game.Map.Interface",
    "Game.Paths",
    "Unity.Burst",
    "Unity.Collections",
    "Unity.Mathematics"
  ],
  "includePlatforms": [],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}
```

- [ ] **Step 4: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件（クライアント側で `tech.moores.server` 経由の新asmdefが解決されることを確認。クライアントmanifestへの追加が必要というエラーが出たら `moorestech_client/Packages/manifest.json` にもStep 2と同じ3行を追加して再実行）

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.MapGeneration moorestech_server/Packages/manifest.json
git status --short   # 巻き込み確認
git commit -m "feat: Game.MapGenerationアセンブリを新設"
```

---

### Task 2: WorldDataDirectory（ワールドデータ統括クラス）と SaveJsonFilePath の置換

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Paths/WorldDataDirectory.cs`
- Delete: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/SaveJsonFilePath.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs`（`SaveJsonFilePath` → `WorldDataDirectory`、`_saveJsonFilePath.Path` → `_worldDataDirectory.SaveJsonFilePath`）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldSaverForJson.cs`（同上）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`（options の `saveJsonFilePath` プロパティを `worldDataDirectory` に置換・DI登録型を差し替え・126行目の mapPath を `options.worldDataDirectory.MapJsonFilePath` に変更）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/ServerInstanceManager.cs`（暫定: `worldDataDirectory = WorldDataDirectory.FromServerDataMap(settings.ServerDataDirectory, settings.SaveFilePath)`。Task 9 で FromWorldRoot に切り替える）
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveJsonFileTest.cs`・`Tests/CombinedTest/Core/ElectricToGearGeneratorTest.cs`（`saveJsonFilePath = new SaveJsonFilePath(x)` → `worldDataDirectory = WorldDataDirectory.FromServerDataMap(TestModDirectory.ForUnitTestModDirectory, x)`）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/WorldDataDirectoryTest.cs`

**Interfaces:**
- Produces:
```csharp
namespace Game.Paths
{
    /// <summary>ワールドディレクトリ内の全ファイル配置を一元定義する値オブジェクト。パス連結はここ以外で行わない</summary>
    /// <summary>Value object owning the entire world-directory layout; no path joins elsewhere</summary>
    public class WorldDataDirectory
    {
        public string Root { get; }                 // FromServerDataMap時はnull（ワールドディレクトリ非所持の形）
        public string WorldMetaFilePath { get; }    // Root/world.json
        public string MapJsonFilePath { get; }      // Root/map.json
        public string SaveJsonFilePath { get; }     // Root/save.json
        public string TerrainDirectory { get; }     // Root/terrain
        public string CacheDirectory { get; }       // Root/cache
        public string CacheReadmeFilePath { get; }  // Root/cache/README.txt
        public string ProvisioningTempDirectory { get; } // Root + ".provisioning"（アトミック確定用）

        // 本来形: ワールドディレクトリのルートから全レイアウトを導出する
        // Canonical form: derive the full layout from a world root directory
        public static WorldDataDirectory FromWorldRoot(string worldRootDirectory);

        // レガシー形: ワールドディレクトリを持たない構成(テスト427箇所・クライアント早期DI)。
        // mapはServerDataDirectory/map/map.json、saveは明示パス。Root系プロパティはnull
        // Legacy form for DI without a world dir (tests / client early init)
        public static WorldDataDirectory FromServerDataMap(string serverDataDirectory, string saveJsonFilePath);
    }
}
```
- `MoorestechServerDIContainerOptions`: コンストラクタは従来通り `(string serverDataDirectory)` のみ。コンストラクタ本体で `worldDataDirectory = WorldDataDirectory.FromServerDataMap(serverDataDirectory, DefaultSaveJsonFilePath);` を設定し、settableプロパティとして公開（**427箇所のテスト直構築は無改修で従来挙動**）
- DI: `AddSingleton<WorldDataDirectory>(options.worldDataDirectory)`（旧 `SaveJsonFilePath` 登録の置き換え。`WorldLoaderFromJson`/`WorldSaverForJson` のコンストラクタ引数型を変更）

- [ ] **Step 1: WorldDataDirectoryTest を書く**

```csharp
using Game.Paths;
using NUnit.Framework;

public class WorldDataDirectoryTest
{
    [Test]
    public void FromWorldRootDerivesAllPaths()
    {
        var dir = WorldDataDirectory.FromWorldRoot("/tmp/world_x");
        Assert.That(dir.WorldMetaFilePath, Is.EqualTo("/tmp/world_x/world.json"));
        Assert.That(dir.MapJsonFilePath, Is.EqualTo("/tmp/world_x/map.json"));
        Assert.That(dir.SaveJsonFilePath, Is.EqualTo("/tmp/world_x/save.json"));
        Assert.That(dir.TerrainDirectory, Is.EqualTo("/tmp/world_x/terrain"));
        Assert.That(dir.CacheReadmeFilePath, Is.EqualTo("/tmp/world_x/cache/README.txt"));
        Assert.That(dir.ProvisioningTempDirectory, Is.EqualTo("/tmp/world_x.provisioning"));
    }

    [Test]
    public void FromServerDataMapUsesServerDataMapAndExplicitSave()
    {
        var dir = WorldDataDirectory.FromServerDataMap("/data/server_v8", "/tmp/save_test.json");
        Assert.That(dir.Root, Is.Null);
        Assert.That(dir.MapJsonFilePath, Is.EqualTo("/data/server_v8/map/map.json"));
        Assert.That(dir.SaveJsonFilePath, Is.EqualTo("/tmp/save_test.json"));
    }
}
```

（パス結合は `Path.Combine` 実装のため、実際のAssertは `Path.Combine` で期待値を組む形で書くこと）

- [ ] **Step 2: FAIL確認 → WorldDataDirectory 実装 → PASS確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WorldDataDirectoryTest"`

- [ ] **Step 3: SaveJsonFilePath を削除し全参照元を置換**（Files欄の各ファイル。`grep -rn "SaveJsonFilePath" moorestech_server moorestech_client` で漏れゼロを確認）
- [ ] **Step 4: コンパイル → 既存回帰テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveJsonFileTest|WorldDataDirectoryTest"`
Expected: 全PASS

- [ ] **Step 5: コミット**

```bash
git add -A moorestech_server/Assets/Scripts
git status --short   # 巻き込み確認
git commit -m "feat: WorldDataDirectoryを新設しSaveJsonFilePathを置換"
```

---

### Task 3: 数値基盤の移植（Jobs / Generators.Util / Biomes列挙）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Jobs/`（移植元 `Pipeline/Jobs/` の全17ファイル、同名）
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Generators/Util/`（PoissonDiskSampler.cs, SpatialGrid.cs, BiomeMaskBuilder.cs, SdfMapGenerator.cs, ManagedNoise.cs, CurvatureComputer.cs）
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Biomes/BiomeType.cs`, `BiomeFlags.cs`

**Interfaces:**
- Produces: `Game.MapGeneration.Pipeline.Jobs.*`（Burstジョブ群・シグネチャ移植元と同一）、`BiomeType` enum（値順序を移植元から**変えない**。terrain/biome binの数値になるため）

- [ ] **Step 1: ファイルコピーと名前空間変更**

各ファイルを移植元からコピーし、`namespace MapGenerator...` を `namespace Game.MapGeneration.Pipeline...`（ディレクトリ対応）へ一括変更。ロジックの変更は一切しない。Jobs内で `LabelAttribute` や Inspector 専用属性を参照している行があれば属性行のみ削除。

```bash
grep -rn "namespace MapGenerator" moorestech_server/Assets/Scripts/Game.MapGeneration/ && echo "未変換あり" || echo "OK"
```

- [ ] **Step 2: 200行超ファイルの分割チェック**

`wc -l` で各ファイルを確認し、200行超のものは責務単位でファイル分割する（例: `BurstBiomeSampler.cs` が超える場合はバイオーム群ごとに分ける）。partial禁止のため、分割は必ず独立クラス/静的クラスへの切り出しで行う。

```bash
find moorestech_server/Assets/Scripts/Game.MapGeneration -name "*.cs" | xargs wc -l | sort -rn | head
```

- [ ] **Step 3: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 4: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.MapGeneration
git commit -m "feat: MapGenerator数値基盤(Jobs/Util/Biome列挙)をGame.MapGenerationへ移植"
```

---

### Task 4: マスタスキーマ定義（map.ymlリネーム＋mapVeins新設＋generation.yml）

**Files:**
- Rename: `VanillaSchema/mapObjects.yml` → `VanillaSchema/map.yml`（`id: map`。既存 `data` 配列を `mapObjects` に改名し、トップレベル第2配列 `mapVeins` を追加。ユーザー裁定 2026-07-24・親spec ADR#2/#3）
- Create: `VanillaSchema/generation.yml`（生成パラメータ全体のスキーマ。edit-schema スキル必読）
- Create: `moorestech_server/Assets/Scripts/Core.Master/GenerationMaster.cs`（MasterHolder 統合。既存 `~Master` クラスの形式踏襲）
- Create: `moorestech_server/Assets/Scripts/Core.Master/MapVeinMaster.cs`（mapVeins配列のラッパー。`ConnectToolMaster` と同型）＋ `Validator/MapVeinMasterUtil.cs`（foreignKeyのC#バリデーション）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs`（`GenerationMaster`・`MapVeinMaster` 静的プロパティ追加）、`MapObjectMaster.cs` ほかモジュール名変更（`MapObjectsModule`→`MapModule`）の参照追従（コンパイルエラー駆動で全置換）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/`（`mapObjects.json`→`map.json` リネーム＋`mapVeins` エントリ＋テスト用 `generation.json` 追加）
- Modify: `/Users/katsumi/moorestech_master/server_v8/mods/.../mapObjects.json` → `map.json` リネーム＋現行ワールドmap.jsonのvein GUID種別ごとに `mapVeins` エントリを起こす（item鉱脈＋fluid鉱脈全種。`outcropAddressablePath` はP2で追加するためP1では持たない）

**mapVeins 要素のスキーマ（MapVeinMasterElement）:**

```yaml
- key: mapVeins
  type: array
  overrideCodeGeneratePropertyName: MapVeinMasterElement
  items:
    - veinGuid: uuid (autoGenerated)
    - veinName: string
    - veinType: enum [item, fluid]
    - veinParam: switch ./veinType
        item:  itemGuid  (foreignKey → items)
        fluid: fluidGuid (foreignKey → fluids)
```

**スキーマの3要素構造（複数mod対応・ユーザー裁定済み）:**

```yaml
# generation.yml のトップ構造（イメージ。詳細はedit-schemaスキル準拠で実装時に確定）
- key: algorithm      # enum: None | VanillaGenerator。デフォルト None（=生成器を提供しない）
- key: priority       # int。デフォルト 0。複数modの生成設定から1件選ぶための優先度
- key: algorithmParam # switch: ./algorithm — アルゴリズム別パラメータ（前例: blocks.yml の blockParam switch）
#   case None: パラメータなし
#   case VanillaGenerator: 現行MapMakingパイプラインのConfig一式
#     - サーバー生成用: TerrainGenerationConfig, OreEntry/OreBand, TreePlacementConfig系,
#                       ObjectClusterEntry系, PlacementEntry/Filter/Noise,
#                       BiomeBoundary/Shore, 各BiomeConfig
#     - 見た目用（P3のクライアント地形構築が読む。サーバーは読まない）:
#                       BiomeTextureConfig, BiomeDetailConfig, DetailFilter/NoiseLayer/NoiseStack,
#                       DetailPrototypeConfig, DetailTextureFilter, TextureChannel,
#                       ObjectSurroundTextureConfig, BiomeObjectConfigの見た目フィールド
#                       — TerrainLayer/Texture2D/プレハブ参照は addressablePath(string) に置換
```

- v8 mod の実データ（Task 6で生成）は `algorithm: VanillaGenerator`・`priority: 1000` を設定する
- `None` は選択対象から除外する（「このmodは生成器を提供しない」の宣言。デフォルト値なので generation.json を持たないmod相当の挙動になる）

**Interfaces:**
- Produces:
  - `Mooresmaster.Model.GenerationModule`（SourceGenerator自動生成。手動作成禁止）
  - `MasterHolder.GenerationMaster`（他マスタ同様 `Load(MasterJsonFileContainer)` でロード）
  - `GenerationMaster.SelectedGeneration` — 全modの generation.json のうち `algorithm != None` のものから **priority最大の1件**を選択して保持。同priorityは `MasterJsonContents.ModId` の文字列Ordinal昇順で若い方を採用。有効な定義が1件も無い場合は「未定義」を示す（generated新規作成時に即例外）
  - `MasterHolder.MapVeinMaster`（map.yml の mapVeins 配列。veinGuid→要素の辞書引きと全件列挙を提供）
  - `OreEntry.VeinGuid`／新規 `FluidVeinEntry.VeinGuid`（いずれも `foreignKey: map` の mapVeins。配置ロジック実装はP5・P1ではFluidVeinEntryのスキーマのみ確保）、`TreePrototypeEntry`/`ObjectClusterEntry`/`PlacementEntry` の `MapObjectGuid`（`foreignKey: map` の mapObjects）— プレハブ参照フィールドの置き換え。OreEntryは`mapObjectGuid`を持たない（鉱脈上の鉱石岩は出力しない・親spec ADR#10）。item/fluid の区別・対象アイテム/流体GUIDは mapVeins マスタから導出（SSOT・親spec ADR#6）
  - veinType一致バリデーション（`Validator/`）: `OreEntry.VeinGuid`→参照先veinTypeがitem・`FluidVeinEntry.VeinGuid`→fluid であることをロード時検証（foreignKeyは実在のみでtype不一致を素通しするため。不一致は起動時即例外）

**スキーマ化の変換ルール:**
- `Vector2`/`Vector3` → スキーマの `vector2`/`vector3` 型（前例: blocks.yml の `vector3Int`）
- `AnimationCurve`（PlacementFilter.curve）→ keyframe配列 `{time, value, inTangent, outTangent}` のスキーマ表現。ロード側で `UnityEngine.AnimationCurve` に再構築（空配列=線形）
- `PlacementNoise.texture`（Texture2Dノイズソース）→ **機能ごと削除**。全プリセット156箇所で未使用（全て `fileID: 0`）を確認済み
- `BiomeFlags`（[Flags] enum）→ スキーマはenum配列とし、ロード側でビットフラグに合成

- [ ] **Step 1: edit-schema スキルを読み、mapObjects.yml→map.yml リネーム＋`data`→`mapObjects` 改名＋`mapVeins` 配列追加を実施**（SourceGenerator再生成→モジュール名変更（`MapObjectsModule`→`MapModule`）の参照をコンパイルエラー駆動で全置換。実測波及: using経由6ファイル（client: MapObjectMining系3＋MapObjectGameObject／server: WorldSaveAllInfoV1・VanillaStaticMapObject）＋`MasterHolder` の `JsonFileName("mapObjects")`→`("map")`＋`mooresmaster.SandBox/schema/` のスキーマ複製同期。TestMod（ForUnitTest・ConfigOnly）・v8 mod の `mapObjects.json`→`map.json` リネームも同時に行う。mooreseditor でリネーム後スキーマが開けること（スキーマ名ハードコード無し）を実行確認する）
- [ ] **Step 2: MapVeinMaster/Validator を実装し MasterHolder に統合、v8 mod・TestMod に mapVeins 実データを追加**（v8は現行ワールドmap.jsonのvein GUID種別を `jq` で集計してエントリを起こす。ユニットテスト: veinGuid引き・veinType振り分け・foreignKey違反の検出）→ FAIL確認 → PASS確認
- [ ] **Step 3: generation.yml を定義**（MapMaking側Config/BiomeConfigの全フィールドを上記変換ルールで写経。フィールド名・意味は変えない。鉱脈参照は `veinGuid`）
- [ ] **Step 4: SourceGenerator を起動し GenerationModule 生成を確認**（edit-schema スキルのトリガー手順）
- [ ] **Step 5: MasterHolder.GenerationMaster 統合＋TestMod に小規模 generation.json を追加し、ユニットテストを作成**（FAIL確認→実装→PASS。ロードに加え選択規則もテスト: ①priority違いの複数mod→最大を採用 ②同priority→mod id Ordinal昇順の若い方 ③定義ゼロ→未定義扱い）
- [ ] **Step 6: validate-schema スキルで foreignKey バリデーション漏れを確認**（mapVeins の itemGuid/fluidGuid・generation.yml の veinGuid/mapObjectGuid）
- [ ] **Step 7: コンパイル・コミット**（moorestech本体と moorestech_master それぞれ）

```bash
uloop compile --project-path ./moorestech_client
git add VanillaSchema moorestech_server/Assets/Scripts/Core.Master moorestech_server/Assets/Scripts/Tests.Module
git commit -m "feat: map.ymlリネーム+mapVeinsマスタ+生成設定スキーマgeneration.ymlをMasterHolderに統合"
```

---

### Task 5: 生成パイプライン本体の移植（ステージ別分割）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Pipeline/Stages/ClassificationStage.cs`（移植元 TerrainGenerator の Stage1: 陸海判定・バイオーム分類・ブラー）
- Create: `.../Stages/HeightmapStage.cs`（Stage2: 高さサンプリング・ブラー・Alpine台地）
- Create: `.../Stages/TreePlacementStage.cs`（Stage3: TreePlacementGenerator 呼び出し）
- Create: `.../Stages/ObjectPlacementStage.cs`（Stage4/4.5: ObjectPlacementGenerator 呼び出し）
- Create: `.../Stages/OrePlacementStage.cs`（Stage6: OrePlacementGenerator 呼び出し・クラスタ→AABB変換）
- Create: `.../Pipeline/MapGenerationPipeline.cs`（ステージを順に呼ぶオーケストレータ・200行以下）
- Create: `.../Pipeline/MapGenerationOutput.cs`（結果の値オブジェクト）
- Create: `.../Pipeline/Generators/`（TreePlacementGenerator.cs, ObjectPlacementGenerator.cs, OrePlacementGenerator.cs, OreBandPlanner.cs — 移植元から名前空間変更＋見た目専用処理の削除）
- Create: `.../Pipeline/Spawn/`（SpawnRegionFinder系 移植）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/MapGenerationPipelineTest.cs`, `TestGenerationConfigFactory.cs`

**Interfaces:**
- Consumes: Task 3 の Jobs / Util、Task 4 の `GenerationModule`（Mooresmaster生成型）
- Produces:
```csharp
public class MapGenerationOutput
{
    public float[] Heights;              // [worldRes*worldRes] 0-1正規化高さ
    public byte[] BiomeIndices;          // [worldRes*worldRes] BiomeTypeの値
    public int Resolution;               // worldRes（1辺セル数）
    public Vector3 SpawnPoint;           // SpawnRegionFinder結果のワールド座標
    public List<PlacedMapObject> MapObjects;   // 木・石・鉱石見た目（guid+ワールド座標）
    public List<PlacedVein> ItemVeins;         // 鉱脈クラスタ（mapVeinsマスタのveinGuid+整数AABB）
}
public class PlacedMapObject { public string MapObjectGuid; public Vector3 Position; }
public class PlacedVein { public string VeinGuid; public Vector3Int Min; public Vector3Int Max; }
```
- `MapGenerationAlgorithmTable`: アルゴリズムenum→生成器実装のテーブル（ディスパッチの真実源）。P1では `VanillaGenerator` の1実装のみ登録。未登録enumは即例外
- エントリポイント: `public static MapGenerationOutput MapGenerationPipeline.Generate(GenerationMasterElement selected, int seed)` — `selected.Algorithm` でテーブルを引き、対応するcase別パラメータ（`AlgorithmParam`）を生成器に渡す
- `TestGenerationConfigFactory.CreateSmall()`: 解像度129・1タイル・バイオーム2種・OreEntry1種（テスト用GUID固定文字列）の `GenerationMasterElement`（algorithm=VanillaGenerator）を返す static クラス（TestModの `generation.json` ロードか直構築。後続タスクのテストでも使用）

- [ ] **Step 1: Generators/Spawn を移植**（Task 3 Step 1 と同じ要領。TreePlacementGenerator 内の TreePrototype/見た目参照はGUID化済みConfigに合わせて修正）
- [ ] **Step 2: TerrainGenerator(約1880行) をステージ別クラスに分割移植**

移植元の `Generate()` 本体を読み、Stage1→2→3→4→6 の各ブロックを上記 Stages/ クラスの `public static` メソッドに切り出す。Stage5（Details）とテクスチャ関連ブロックは移植しない。`MapGenerationPipeline.Generate` は各ステージを順に呼ぶだけの薄いオーケストレータにする。

- [ ] **Step 3: 鉱脈クラスタ→AABB変換を OrePlacementStage に実装**

OrePlacementGenerator のクラスタ結果（中心＋メンバー座標群）ごとに、メンバー座標のmin/maxを整数グリッドへスナップし `PlacedVein` を1件生成する（GUIDは `OreEntry.VeinGuid` ＝ mapVeins マスタのGUID）。鉱石岩の `PlacedMapObject` 出力は行わない（地表ビジュアルは露頭に一本化・親spec ADR#10。移植元の見た目メンバー生成処理は削除する）。

- [ ] **Step 4: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 5: 決定論テストを書く**

```csharp
using System.Linq;
using Game.MapGeneration.Pipeline;
using NUnit.Framework;

public class MapGenerationPipelineTest
{
    // 同一seedは完全同一出力、異なるseedは異なる出力になることを検証
    // Same seed must reproduce identical output; different seeds must differ
    [Test]
    public void SameSeedProducesIdenticalOutput()
    {
        var config = TestGenerationConfigFactory.CreateSmall();
        var a = MapGenerationPipeline.Generate(config, 12345);
        var b = MapGenerationPipeline.Generate(config, 12345);
        Assert.That(a.Heights, Is.EqualTo(b.Heights));
        Assert.That(a.BiomeIndices, Is.EqualTo(b.BiomeIndices));
        Assert.That(a.MapObjects.Count, Is.EqualTo(b.MapObjects.Count));
        Assert.That(a.ItemVeins.Count, Is.EqualTo(b.ItemVeins.Count));
    }

    [Test]
    public void DifferentSeedProducesDifferentHeights()
    {
        var config = TestGenerationConfigFactory.CreateSmall();
        var a = MapGenerationPipeline.Generate(config, 1);
        var b = MapGenerationPipeline.Generate(config, 2);
        Assert.That(a.Heights.SequenceEqual(b.Heights), Is.False);
    }

    [Test]
    public void VeinAabbIsIntegerSnappedAndNonEmpty()
    {
        var config = TestGenerationConfigFactory.CreateSmall();
        var output = MapGenerationPipeline.Generate(config, 12345);
        Assert.That(output.ItemVeins, Is.Not.Empty);
        foreach (var vein in output.ItemVeins)
        {
            Assert.That(vein.Min.x, Is.LessThanOrEqualTo(vein.Max.x));
            Assert.That(vein.Min.y, Is.LessThanOrEqualTo(vein.Max.y));
            Assert.That(vein.Min.z, Is.LessThanOrEqualTo(vein.Max.z));
        }
    }
}
```

- [ ] **Step 6: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapGenerationPipelineTest"`
Expected: 3件 PASS（seed実装前に一度FAILを確認してから通すこと）

- [ ] **Step 7: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.MapGeneration moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration
git commit -m "feat: 生成パイプライン本体をステージ分割で移植し決定論テストを追加"
```

---

### Task 6: MapMaking側エクスポータと実データ generation.json 生成

**Files:**
- Create: `TmpUnityPjt/MapMaking/Assets/Editor/GenerationConfigExporter.cs`（MapMakingプロジェクト側・MenuItem）
- Create: `/Users/katsumi/moorestech_master/server_v8/mods/.../generation.json`（エクスポータ実行の成果物。他マスタJSONと同じmod内配置）

**Interfaces:**
- Consumes: Task 4 の `generation.yml` スキーマ（エクスポータはスキーマと同一キーのJSONを出力する）
- Produces: v8 mod の実データ `generation.json`（mooreseditor で開いて編集できること）

- [ ] **Step 1: MapMaking側エクスポータを実装**

`Tools/MapGenerator/Export Generation Config` MenuItem。シーン上のMapGeneratorから `TerrainGenerationConfig`(SO) と全 `BiomeConfig` に加え、**見た目系Config（BiomeTextureConfig/BiomeDetailConfig等）も**収集し、generation.yml スキーマと同一キーのMooresmaster形式JSONにシリアライズして保存する。見た目Config内の `TerrainLayer`/`Texture2D`/プレハブ参照は、SOに併設する `addressablePath` 文字列フィールドから出力する（Addressables側のアドレス整備はP3。P1では空文字も許容し、エクスポート時に空欄の一覧を警告ログで列挙する）。prefab参照フィールドは、SOに併設する `mapObjectGuid`/`veinGuid` 文字列フィールド（このタスクでMapMaking側SOにも追加）から出力する。GUID値は `moorestech_master/server_v8/mods/.../map.json`（旧mapObjects.json。mapObjects/mapVeins両配列）の実GUIDを設定してから書き出す（スキーマの foreignKey でロード時に実在検証されるため、タイポは起動時に即検出される）。AnimationCurve は keyframe 配列へ、BiomeFlags は enum 配列へ変換して出力。トップレベルは `algorithm: VanillaGenerator`・`priority: 1000` を設定して書き出す（ユーザー裁定済みのv8実データ値）。

- [ ] **Step 2: エクスポータを実行し v8 mod 内に `generation.json` を生成、`MasterHolder.Load` で読めることをテスト追加で確認**
- [ ] **Step 3: mooreseditor で generation.json が開けることを確認**（スキーマ駆動UIで表示される想定。プラグイン対応が要る場合は別課題として記録。dist差し替え時はアプリ再起動必須）
- [ ] **Step 4: コミット**（moorestechリポジトリ・MapMakingリポジトリ・moorestech_masterリポジトリそれぞれでコミット）

---

### Task 7: MapInfoJsonのmapVeins刷新とワールドファイル書き出し（map.json / terrain / world.json / cache）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Map.Interface/Json/MapInfoJson.cs`（`itemMapVeins`/`fluidVeins` 2配列を廃止し、`mapVeins` 単一配列（`MapVeinInfoJson`: `veinGuid`＋min/max）へ刷新。親spec ADR#6）
- Modify: `moorestech_server/Assets/Scripts/Game.Map/ItemMapVeinDatastore.cs`・`FluidMapVeinDatastore.cs`（ロード時に `MasterHolder.MapVeinMaster` の veinType で振り分け、対象item/fluid GUIDをマスタから導出して従来と同じ内部表現を構築。Miner/Pump側は無改修）
- Modify: ワールドテンプレート `ServerDataDirectory/map/map.json`・テスト用map.json群（**実在3件を明示**: `Tests.Module/TestMod/ForUnitTest/map/map.json`・`Tests.Module/TestMod/ConfigOnly/map/map.json`・`moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/map/map.json`。旧2配列→新 `mapVeins` 配列へ一括移行。jqスクリプト・Task 4で起こしたmapVeinsマスタのGUIDへ差し替え。v8実測: itemMapVeins 1389件＋fluidVeins 383件・distinct GUIDはitem 9種+fluid 1種）
- Modify: `moorestech_client/Assets/Scripts/Editor/MapExportAndSetting.cs`（旧 `ItemMapVeinInfoJson`/`FluidVeinInfoJson` を直接newする唯一の利用側。新 `MapVeinInfoJson` 単一配列出力へ更新）＋ `Client.Game/InGame/Map/MapVein/ItemMapVeinGameObject.cs`・`FluidMapVeinGameObject.cs`・`Editor/Inspector/` の両Inspector（シーン上のauthoring GameObjectが保持するitem/fluid GUID直接指定を mapVeins マスタの `veinGuid` 指定へ変更）
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Export/MapInfoJsonBuilder.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Export/TerrainFileWriter.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Export/WorldMetaJson.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/MapInfoJsonBuilderTest.cs`, `TerrainFileWriterTest.cs`

**Interfaces:**
- Consumes: `MapGenerationOutput`（Task 5）、`WorldDataDirectory`（Task 2。書き出し先パスは全てここから取得）
- Produces:
  - `public static MapInfoJson MapInfoJsonBuilder.Build(MapGenerationOutput output)` — instanceIdを0から連番採番し、`MapInfoJson`（本タスクで刷新した `mapVeins` 単一配列形式）を構築
  - `public static void TerrainFileWriter.Write(WorldDataDirectory worldDataDirectory, MapGenerationOutput output)` — `TerrainDirectory` に `height_0_0.r16`（16bit little-endian, 行優先）と `biome_0_0.bin`（1byte/セル）を書き、`CacheReadmeFilePath` に「このディレクトリは削除可能です。削除しても次回起動時に自動で再構築されます。」を書く
  - `WorldMetaJson`: `[JsonProperty]` で `seed`(int), `generatorVersion`(string), `algorithm`(string。採用したアルゴリズムenum名。デバッグ用記録), `mapMode`("generated"|"template"), `createdAt`(ISO8601文字列), `terrainResolution`(int), `terrainTileCount`(int)

- [ ] **Step 1: MapInfoJson を `mapVeins` 単一配列へ刷新し、Datastore振り分けロードとテンプレートmap.json一括移行を行う**（コンパイルエラー駆動で旧2配列の参照を全置換。旧DTOの利用側は `MapExportAndSetting.cs` が唯一＝Files欄のauthoring系と同時に更新する。既存のMapVein系テスト＋Miner/Pumpテストが移行後データでPASSすることを確認）
- [ ] **Step 2: MapInfoJsonBuilderTest を書く**（MapGenerationOutputのダミーを渡し、instanceIdが連番・veinGuid文字列がそのまま入る・veinのmin/maxが転記されることをAssert）→ FAIL確認 → 実装 → PASS確認
- [ ] **Step 3: TerrainFileWriterTest を書く**（`WorldDataDirectory.FromWorldRoot(一時パス)` に書き、r16のバイト長 = 解像度^2*2、biome binのバイト長 = 解像度^2、cache/README.txtの存在をAssert）→ FAIL確認 → 実装 → PASS確認
- [ ] **Step 4: コンパイル・テスト実行・コミット**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapInfoJsonBuilderTest|TerrainFileWriterTest|MapVein|MinerMining|PumpFluidVein"
git add moorestech_server/Assets/Scripts/Game.MapGeneration moorestech_server/Assets/Scripts/Game.Map moorestech_server/Assets/Scripts/Game.Map.Interface moorestech_server/Assets/Scripts/Tests
git commit -m "feat: MapInfoJsonをmapVeins単一配列へ刷新しワールドファイル書き出しを実装"
```

---

### Task 8: WorldProvisioner（新規作成時1回生成・アトミック確定・以後no-op）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisioner.cs`
- Create: `moorestech_server/Assets/Scripts/Game.MapGeneration/Provisioning/WorldProvisionSettings.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MapGeneration/WorldProvisionerTest.cs`

**Interfaces:**
- Consumes: `WorldDataDirectory`（Task 2）, `MasterHolder.GenerationMaster`（Task 4。generated時は `EnsureWorld` 呼び出し前に `MasterHolder.Load` 済みであることを前提とし、未ロードなら即例外）, `MapGenerationPipeline.Generate`（Task 5）, `MapInfoJsonBuilder`/`TerrainFileWriter`/`WorldMetaJson`（Task 7）
- Produces:
```csharp
public class WorldProvisionSettings
{
    public readonly WorldDataDirectory WorldDataDirectory;
    public readonly string ServerDataDirectory;
    public readonly string MapMode;   // "template" | "generated"
    public readonly int Seed;
    public WorldProvisionSettings(WorldDataDirectory worldDataDirectory, string serverDataDirectory, string mapMode, int seed) { ... }
}
public static class WorldProvisioner
{
    // world.jsonがあれば何もしない。無ければ一時ディレクトリに書き切ってからリネーム確定する
    // No-op when world.json exists; otherwise builds in a temp dir and commits via rename
    public static void EnsureWorld(WorldProvisionSettings settings);
}
```
- 挙動（アトミック性・破損検出を含む）:
  1. `ProvisioningTempDirectory` が残っていれば削除（前回クラッシュの残骸）
  2. `WorldMetaFilePath` が存在 → 即return（既存ワールド）
  3. `Root` ディレクトリが存在するのに `world.json` が無い → 破損として例外（無言の再生成はしない）
  4. 新規: `ProvisioningTempDirectory` に全ファイルを書く。`generated` → `GenerationMaster.SelectedGeneration`（優先度解決済みの1件。未定義なら即例外）→`MapGenerationPipeline.Generate`（アルゴリズムテーブルでディスパッチ）→map.json/terrain/cache書き出し。`template` → `ServerDataDirectory/map/map.json` をコピー＋cache/README.txt作成。**world.json（コミットマーカー）は最後に書く**
  5. `Directory.Move(ProvisioningTempDirectory, Root)` で確定

- [ ] **Step 1: テストを書く**（5ケース: ①template新規→map.jsonが元と同一内容＋world.json存在 ②generated新規→map.jsonがMapInfoJsonとしてデシリアライズ可能＋terrain/存在 ③2回目呼び出し→ファイルのタイムスタンプ不変=no-op ④.provisioning残骸がある状態で呼ぶ→残骸が消えて正常生成 ⑤Rootがあるのにworld.jsonが無い→例外。テスト用一時ディレクトリ＋TestModのServerDataDirectoryを使用）
- [ ] **Step 2: FAIL確認 → 実装 → PASS確認**
- [ ] **Step 3: 生成時間を計測しログ出力**（generatedケースのテスト内で `Stopwatch` 計測し `Debug.Log`。実測値を親スペックのリスク欄に追記する）
- [ ] **Step 4: コンパイル・コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.MapGeneration moorestech_server/Assets/Scripts/Tests docs/plans/map-autogen-world-design.md
git commit -m "feat: WorldProvisionerを実装(アトミック確定・破損検出付き)"
```

---

### Task 9: 起動フロー統合（--worldDirectory / FromWorldRoot切り替え）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Args/StartServerSettings.cs`（`SaveFilePath` を削除し `WorldDirectory`・`MapMode`・`Seed` を追加）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/ServerInstanceManager.cs`（`Create()` 前に `WorldProvisioner.EnsureWorld` を呼び、`FromWorldRoot` でoptionsを組む）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Server.Boot.asmdef`（`Game.MapGeneration` 参照追加）
- Modify: `--saveFilePath`/`SaveFilePath` の全参照元を更新（`grep -rn "SaveFilePath\|saveFilePath" moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts` で全件洗い出し。**`Client.Tests/EditModeInPlayingTest/Util/EditModeInPlayingTestUtil.cs`（テスト隔離パス指定）、`Client.Starter/InitializeScenePipeline.cs` の「セーブをロード・保存しない再生」のランダムセーブファイル名（→ランダム一時ワールドディレクトリ指定に変更）、`Tests/UnitTest/Server/CliConvertTest.cs`（オプション名変更に追従）を必ず含める**）

**Interfaces:**
- Consumes: `WorldProvisioner.EnsureWorld`（Task 8）, `WorldDataDirectory.FromWorldRoot`（Task 2）
- Produces:
  - `StartServerSettings`: `WorldDirectory`（`--worldDirectory`、デフォルト `GameSystemPaths.GetSaveFilePath("world_1")` ※ディレクトリとして使用）, `MapMode`（`--mapMode`、デフォルト `"template"`）, `Seed`（`--seed`、デフォルト `0`。generated時に0なら `Guid.NewGuid().GetHashCode()` で採番）

- [ ] **Step 1: StartServerSettings を改修**

```csharp
public class StartServerSettings
{
    [Option(isFlag: false, "--worldDirectory", "-w")]
    public string WorldDirectory { get; set; } = GameSystemPaths.GetSaveFilePath("world_1");

    [Option(isFlag: false, "--mapMode")]
    public string MapMode { get; set; } = "template";

    [Option(isFlag: false, "--seed")]
    public int Seed { get; set; } = 0;

    [Option(isFlag: false, "--autoSave", "-a")]
    public bool AutoSave { get; set; } = true;

    [Option(isFlag: false, "--serverDataDirectory")]
    public string ServerDataDirectory { get; set; } = ServerDirectory.GetDirectory();
}
```

- [ ] **Step 2: SaveFilePath 参照元を grep で全洗い出しして更新**（Files欄の必須3ファイルを含む全件。テストユーティリティ・skip-save再生は隔離ワールドディレクトリ指定に変更）
- [ ] **Step 3: ServerInstanceManager.Start に統合**

```csharp
var settings = CliConvert.Parse<StartServerSettings>(args);
var worldDataDirectory = WorldDataDirectory.FromWorldRoot(settings.WorldDirectory);

// 生成設定はマスタなのでプロビジョニング前にマスタをロードする（Create()内の再ロードは冪等）
// Generation config lives in master data, so load masters before provisioning (reload in Create() is idempotent)
var modResource = new ModsResource(Path.Combine(settings.ServerDataDirectory, "mods"));
MasterHolder.Load(new MasterJsonFileContainer(ModJsonStringLoader.GetMasterString(modResource)));

// ワールドディレクトリをDI構築前に整備する（無ければ生成/テンプレートコピー）
// Provision the world directory before DI container construction
WorldProvisioner.EnsureWorld(new WorldProvisionSettings(
    worldDataDirectory, settings.ServerDataDirectory, settings.MapMode, settings.Seed));

var options = new MoorestechServerDIContainerOptions(settings.ServerDataDirectory)
{
    worldDataDirectory = worldDataDirectory,
};
```

- [ ] **Step 4: コンパイル → 既存テスト回帰確認**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MapObject|MapVein|WorldSetting|SaveLoad|CliConvert"`
Expected: 全PASS（options直構築427箇所はFromServerDataMapデフォルトのため従来挙動）

- [ ] **Step 5: 起動E2E確認**

`uloop execute-dynamic-code` で一時ワールドディレクトリを指定して `ServerInstanceManager` を起動→ `world_1/{world.json, map.json, save.json}` が生成されることを確認（generated モードでも1回実行し terrain/ 生成と起動成功を確認）。確認後プロセス/スレッドを停止。

- [ ] **Step 6: コミット**

```bash
git add -A moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts
git status --short   # 巻き込み確認
git commit -m "feat: 起動フローにワールドプロビジョニングを統合(--worldDirectory)"
```

---

### Task 10: 全ブランチレビュー（必須クローズタスク）

- [ ] **Step 1:** 必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）
- [ ] **Step 2:** レビュー指摘を修正し、再コンパイル・関連テスト再実行・コミット

---

## Self-Review 結果

- **Spec coverage**: 親スペックのP1範囲（Game.MapGeneration移植・WorldProvisioner・seed→map.json生成・既存ローダー起動・生成時間実測・cache/README生成・テンプレート共存）は Task 1-9 で網羅。加えてユーザー指摘の「セーブデータ全体を統括するクラス」を Task 2（WorldDataDirectory）として先頭近くに配置し、後続タスク全部がこれを消費する構造にした。P2以降（プロトコル・クライアント）は意図的にスコープ外
- **Placeholder scan**: 移植タスク（3-5）はコード全文でなく移植元パス＋変換規則で記述しているが、これは移植元が実在するコードであるための意図的な形式。新規ファイル（WorldDataDirectory/Loader/Builder/Writer/Provisioner/Settings）はシグネチャ・挙動・テストコードを明記済み
- **Type consistency**: `WorldDataDirectory`（Task 2定義）を Task 7-9 が消費、`MapGenerationOutput`/`PlacedMapObject`/`PlacedVein`（Task 5定義）を Task 7-8 が消費、`WorldProvisionSettings`（Task 8定義）を Task 9 が消費、で名称一致を確認済み
- **穴の再点検**: プロビジョニング中断（アトミック確定＋残骸掃除＋破損例外）、skip-save再生モード、CliConvertTest、AutoSave/.bakのパス出所、テストの実save_1.json既定パス（既存の穴として記録のみ）を「発見した設計の穴と対処」表に反映済み
