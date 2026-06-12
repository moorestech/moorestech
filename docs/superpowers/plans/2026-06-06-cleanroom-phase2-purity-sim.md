# クリーンルーム フェーズ2（純度シミュレーション）実装プラン

> **改訂: 2026-06-12 — codemap v2 整合＋批判的レビュー反映**
> 主な変更: (1) `CleanRoomPurityService`/`CleanRoomPurityState` を廃止し **`CleanRoomDatastore`**（検出＋純度tick＋永続化を1つの世界システムで担う）へ統合、(2) `CleanRoomClass` 列挙を廃止し **`cleanRoomThresholds.yml` マスタ＋`ThresholdIndex`(int)** へ移行、(3) 再検出引き継ぎを **N＋ThresholdIndex＋Status＋猶予残** に拡張（再検出のたびにヒステリシス保持帯の部屋が恒久降格するバグの修正）、(4) 孤立状態のライフサイクル確定（Degradedはセーブ対象／Invalidは次の再検出で破棄）、(5) **dirty分割処理（8192セル/tick）と触れた壁AABB+1局所化を本フェーズのDoDに編入**、(6) DI橋渡し方式・`IWorldSaveDataLoader` キャスト・`BlockRemoveReason.ManualRemove`・asmdef `UniRx` 参照など既存コードベースとの不適合を修正。

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **moorestech 固有の必須ルール:**
> - `.cs` 編集後は必ず `uloop compile --project-path ./moorestech_client` でコンパイル確認する。
> - テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"` で実行（クライアントプロジェクトからサーバーテストも走る）。
> - 新規サーバー `.cs`／新規 asmdef を認識させるには Unity の **再起動**が要る場合がある（Refresh では不足）。「型が見つからない」で失敗したら uloop で Unity 再起動してから再試行。「Domain Reload in progress」なら45秒待って再試行。
> - マスタスキーマ追加・SourceGenerator の手順は `edit-schema` スキルに従う。テスト作成は `creating-server-tests` スキルに従う。
> - 非ASCIIファイル編集時は AGENTS.md の「文字化け防止ワークフロー」を順守。編集前にエンコーディングを確認し、編集後に同じエンコーディングへ戻す。`git diff` に `縺`/`繧`/`繝` 等の化け文字が連続したら破棄してやり直す。
> - **APIシグネチャ確認の原則:** 本プランのコードは既存コードベースのパターンから書いているが、メソッド名・引数順は推定を含む。各 `.cs` を書く前に、本文で「確認」と指示した既存ファイルを開いて実シグネチャに合わせること。コンパイル/テストのチェックポイントが安全網。
> - ブロック破壊は `BlockRemoveReason.ManualRemove`（`Game.Block.Interface` 名前空間）。**`Destroy` というメンバは存在しない**（実enumは `Broken`/`ManualRemove` の2値）。

**Goal:** フェーズ1で新設した `CleanRoomDatastore`（検出部）に「純度の継続状態」を統合する。各 `CleanRoom` が N（不純物総数）を持ち、`dN/dt = A_total − n·q·C` を tick 積分して平衡濃度 `C_eq = A_total/(n·q)` に収束させる。二条件（濃度 `C ≤ maxConcentration` ＋ 換気 `ACH = n·q/V ≥ requiredAirChangeRate`）＋ヒステリシスで **閾値行インデックス（ThresholdIndex）** を決定し、Valid/Degraded/Invalid＋猶予（5.0秒）を運用する。再検出（リーク・結合・分割）をまたいで **N＋ThresholdIndex＋Status＋猶予残** をセル重なりで引き継ぎ、セーブ/ロードで永続化する。あわせて **再検出の dirty 分割処理（8192セル/tick）** と **触れた壁AABB+1 のリーク判定局所化** を実装する（バランス確定書§5で本フェーズ担当と確定）。汚染源 `A_total` の実係数とエアフィルター実体はフェーズ3（本フェーズは既定0＋テスト用定数注入）。

**Architecture:** codemap v2 のデータストア方式。`CleanRoomDatastore`（DI singleton・initializer側登録→main側へインスタンス橋渡し・eager）が、ブロック設置/削除の購読→dirty積み→tick分割再検出、`GameUpdater.UpdateObservable`（UniRx、20/秒・50ms）購読→毎tick純度積分、セーブ/ロード（`GetSaveData`/`Restore`）のすべてを担う。`CleanRoom` 自身が幾何（Cells/Volume/SurfaceArea）＋純度（ImpurityCount/Status/ThresholdIndex/猶予残）を持つ（状態クラスの分離はしない）。判定は純関数 `CleanRoomPurityRules`（二条件＋ヒステリシス・tick積分・按分）。閾値はマスタ `cleanRoomThresholds.yml`→`CleanRoomThresholdMaster`（`MasterHolder` 経由）。`A_total` はデータストアが直接算出（フェーズ2の既定は0。テストは `SetPollutionPerSecondProvider` で定数注入＝バランス確定書§7の「係数を定数で注入」）。`n·q` は `ICleanRoomAirFilter`（Game.Block.Interface）の登録レジストリ（`AddAirFilter`/`RemoveAirFilter`、フェーズ3のブロックが設置時に登録）。永続化は鉄道 `RailGraphSaveLoadService` を前例に `WorldSaveAllInfoV1`/`AssembleSaveJsonText`/`WorldLoaderFromJson` の3点改修、ロード復元は **`LoadBlockDataList` → `RebuildAll()`（dirtyクリア込み） → `Restore()`** の順。

**Tech Stack:** C# (Unity, moorestech_server), UniRx `IObservable<Unit>`（`GameUpdater.UpdateObservable` / テストは `GameUpdater.RunFrames(uint)`）, NUnit (Server.Tests), Newtonsoft.Json（セーブ）, Mooresmaster SourceGenerator（`cleanRoomThresholds.yml` → `Mooresmaster.Model.CleanRoomThresholdsModule`）。

**数値ソース:** 全ての数値（閾値行・ヒステリシス係数 0.8/1.25・必要換気・猶予5.0秒・worked example・dirty上限8192）は `docs/superpowers/plans/2026-06-06-cleanroom-balance-parameters.md`（改訂版）を唯一のソースとする。契約（正）は同書と `2026-06-06-cleanroom-phases2-5-codemap.md`（v2）。本プランと食い違ったら契約側が正。

---

## 後続プランのロードマップ（このプランの対象外）

| フェーズ | 内容 | 主産物 |
|---|---|---|
| 1（前提） | 境界ブロック4種＋3D密閉部屋検出＋`CleanRoomDatastore`（検出部）＋部屋クエリ | 壁で囲うと部屋検出、壊すと無効化 |
| **2（本プラン）** | 純度シミュ（N/V/S、A_total注入シーム、エアフィルター n·q·C 除去、平衡、閾値行＋ACH、ヒステリシス、Valid/Degraded/Invalid＋猶予）＋再検出引き継ぎ＋dirty分割処理＋触れた壁AABB局所化＋永続化＋閾値マスタ | 部屋に閾値行が付き、汚染/清浄に応答し、再検出・セーブをまたいで継続 |
| 3 | エアフィルターブロック（`CleanRoomAirFilter`）＋フィルター仕事量消費＋電力＋汚染源実係数（`CleanRoomPollutionCalculator`） | 維持ループが回る |
| 4 | 専用機械統合（`CleanRoomMachine`・効果プッシュ・最大グレード天井・down-bin・Invalid停止） | 半導体生産が部屋純度に依存 |
| 5 | ハッチ挙動（ドアハッチ/アイテムハッチ/パイプハッチ）＋I/O固有セーブ | 完全な遊べる形 |

---

## 本プランの前提（フェーズ1の確定API — codemap v2 命名）

本プランは codemap v2 §1 のフェーズ1産物に載る。**書く前に各実ファイルを開いてシグネチャを確認すること。**

- `Game.CleanRoom.CleanRoom` — `int Id`（一時参照用。永続キー禁止）／`IReadOnlyCollection<Vector3Int> Cells`（**機械等の占有セルを含む全内部セル**。帰属判定用）／`int Volume`（**Cells のうち空セル数**。占有セルは除外）／`int SurfaceArea`／`bool IsValid`／`bool Contains(Vector3Int)`
- `Game.CleanRoom.CleanRoomDatastore` — `IReadOnlyList<CleanRoom> Rooms`／`void RebuildAll()`（全走査再検出。**dirty キューもクリアする**）／`bool TryGetCleanRoomAt(Vector3Int, out CleanRoom)`／`bool TryGetCleanRoomContainingBlock(IBlock, out CleanRoom)`。`WorldBlockUpdateEvent` 購読で dirty を積む
- `Game.CleanRoom.CleanRoomDetector` — 検出純関数（6近傍 flood-fill）
- `Game.Block.Interface.Component.ICleanRoomBoundaryComponent`（`CleanRoomBoundaryKind BoundaryKind`）、enum `CleanRoomBoundaryKind { Wall, DoorHatch, ItemHatch, PipeHatch }`
- テスト mod の境界ブロックID `Tests.Module.TestMod.ForUnitTestModBlockId.CleanRoomWall` 等と `BuildWallShell(world, min, max)` ヘルパ（フェーズ1テストに実装済み。別クラスから使う場合は本テストファイルへコピー）

> **⚠ フェーズ1プラン（2026-06-05版ファイル）は codemap v2 改訂前の記述を含む。** 旧名 `CleanRoomDetectionSystem`・旧ブロック名（`CleanRoomDoor`/`CleanRoomPipeConnector`）・「機械セルを V に算入」等が残っている場合がある。**契約は codemap v2**（`CleanRoomDatastore`／`DoorHatch`・`PipeHatch`／Cells=全内部セル・Volume=空セル数）。フェーズ1実装が旧仕様のままなら本プラン Task 0 で合わせる。
>
> **⚠ DI 登録の注意（必読）:** `IWorldBlockDatastore` は `MoorestechServerDIContainerGenerator` の **initializer 側コンテナにのみ登録**されており、main `services` には無い。`CleanRoomDatastore` を main 側に素の ctor 注入で `services.AddSingleton<CleanRoomDatastore>()` すると**起動時に解決例外で必ず落ちる**（コンパイルは通る）。`GearNetworkDatastore` と同じく **initializer 側で登録→`services.AddSingleton(initializerProvider.GetService<CleanRoomDatastore>())` でインスタンス橋渡し**する。
>
> **⚠ asmdef:** `Game.CleanRoom.asmdef` の `references` に `"UniRx"` が必要（`Subject<T>`／`Subscribe` 拡張は UniRx asmdef 由来。`Game.Gear.asmdef` 参照）。`UnityEngine` は asmdef 参照ではない（暗黙）。

---

## File Structure（フェーズ2で作成/変更するファイル）

**マスタ（閾値テーブル）**
- Create: `VanillaSchema/cleanRoomThresholds.yml` — 閾値行（label/maxConcentration/maxGrade/downBinRate/requiredAirChangeRate）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` — SourceGenerator トリガ
- Create: `moorestech_server/Assets/Scripts/Core.Master/CleanRoomThresholdMaster.cs` — アクセサ＋バリデーション
- Modify: `moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs` — `CleanRoomThresholdMaster` 静的プロパティ＋Load
- Create: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/cleanRoomThresholds.json` — テスト mod 実データ（**無いと `MasterHolder.GetJson` が KeyNotFoundException**）

**純度（Game.CleanRoom 拡張）**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomPurityRules.cs` — 判定純関数（二条件＋ヒステリシス・tick積分・按分）＋`CleanRoomThresholdRow` 構造体
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomRoomStatus.cs` — Valid/Degraded/Invalid 列挙
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoom.cs` — 純度状態（N/Status/ThresholdIndex/猶予残）を統合
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomAirFilter.cs` — n·q をデータストアが読む口（フェーズ3のエアフィルターが実装）
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs` — 純度tick・フィルター登録・汚染シーム・引き継ぎ・孤立状態・dirty分割処理
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetector.cs` — 触れた壁AABB+1 局所化＋visited数の計測

**セーブ（3点改修）**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/SaveLoad/CleanRoomSaveData.cs` — 保存レコード
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs`

**DI / 参照**
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs` — initializer 登録＋橋渡し＋eager
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/Game.CleanRoom.asmdef`（`UniRx`/`Core.Master` 等、コンパイルで判明したら追加）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Game.SaveLoad.asmdef`（`Game.CleanRoom` 参照を追加）

**テスト**
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDirtyRebuildTest.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPurityPersistenceTest.cs`

> 各新規 `.cs` は Unity が `.meta` を自動生成する。`.meta` は手動作成禁止。

---

## Task 0: フェーズ1産物の codemap v2 整合確認（チェックのみ・必要時に一点改修）

実装に入る前に、フェーズ1の実装が codemap v2 と一致しているかを確認する。TDDサイクルは無い（確認と機械的リネームのみ）。

- [ ] **Step 1: 型名・ブロック名の確認**
  - `Game.CleanRoom/CleanRoomDatastore.cs` が存在するか。旧名 `CleanRoomDetectionSystem` のままなら**クラス名・ファイル名をリネーム**（参照箇所は grep で追従。`git mv` 後に Unity を一度起動して `.meta` を整合させる）。
  - `CleanRoomBoundaryKind` が `{ Wall, DoorHatch, ItemHatch, PipeHatch }` か。旧 `Door`/`PipeConnector` なら enum 名・テスト mod のブロック名を codemap v2 名へ更新。
- [ ] **Step 2: `CleanRoom.Volume` の定義確認**
  - `Volume` が「空セル数（占有セル除外）」、`Cells` が「占有セル含む全内部セル」になっているか（バランス確定書§5）。旧仕様（機械セルもVに算入）のままなら `CleanRoomDetector` を一点改修し、フェーズ1の体積テストの期待値を更新する。
- [ ] **Step 3: `RebuildAll()` が dirty キュー/フラグをクリアするか確認**
  - クリアしない実装だと「手動 RebuildAll → 次tickにもう一度全再検出 → 状態リセット」事故の温床（ロード直後に致命的、Task 7 の非回帰テストで検出される）。クリアしないなら一点改修。
- [ ] **Step 4: コンパイル＋フェーズ1テスト全緑を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomDetection"`
Expected: 全 PASS。

- [ ] **Step 5: Commit（変更が発生した場合のみ）**

```bash
git add -A moorestech_server/Assets/Scripts/Game.CleanRoom/ moorestech_server/Assets/Scripts/Tests/
git commit -m "refactor(cleanroom): フェーズ1産物をcodemap v2命名・仕様に整合"
```

---

## Task 1: cleanRoomThresholds マスタ（スキーマ＋実データ＋アクセサ）

閾値テーブルをマスタデータ化する（codemap v2 の中央決定: `CleanRoomClass` 列挙は作らない）。`edit-schema` スキルの手順に従うこと。

**Files:**
- Create: `VanillaSchema/cleanRoomThresholds.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`
- Create: `moorestech_server/Assets/Scripts/Core.Master/CleanRoomThresholdMaster.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs`
- Create: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/cleanRoomThresholds.json`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs`（新規作成）

### 仕様（バランス確定書§1）

行は**清浄な順（index 0 が最良）**。`maxConcentration` 昇順・`requiredAirChangeRate` 降順であること（バリデーションで強制）。

| index | label | maxConcentration（個/m³） | maxGrade | downBinRate | requiredAirChangeRate（1/秒） |
|---|---|---|---|---|---|
| 0 | A | 10 | 4 | 0.0 | 0.0167 |
| 1 | B | 50 | 3 | 0.05 | 0.0083 |
| 2 | C | 200 | 2 | 0.15 | 0.0042 |
| 3 | D | 1000 | 1 | 0.35 | 0.0014 |
| （行数=4） | Out | — | 0 | — | — |

- **ThresholdIndex = 行数（=4）が Out**（どの行も不成立）。Out は行として持たない。
- `maxGrade`/`downBinRate` はフェーズ4（`CleanRoomEffectResolver`）が使う。本フェーズで使うのは `maxConcentration`/`requiredAirChangeRate` のみだが、スキーマは最初から4列とも定義する（後からのスキーマ変更を避ける）。

- [ ] **Step 1: 失敗テストを書く（マスタがロードされ4行・値が正しい）**

`Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs` を新規作成:

```csharp
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomPuritySimulationTest
    {
        [Test]
        public void ThresholdMaster_LoadsFourRows_BestFirst()
        {
            // DIコンテナ生成で MasterHolder.Load が走る。
            // Creating the DI container loads MasterHolder.
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var master = MasterHolder.CleanRoomThresholdMaster;
            Assert.AreEqual(4, master.Rows.Count);
            Assert.AreEqual(4, master.OutThresholdIndex);

            // 行0が最良（A相当）。値はバランス確定書§1。
            // Row 0 is the cleanest tier; values from balance §1.
            Assert.AreEqual(10.0, master.Rows[0].MaxConcentration, 1e-9);
            Assert.AreEqual(0.0167, master.Rows[0].RequiredAirChangeRate, 1e-9);
            Assert.AreEqual(1000.0, master.Rows[3].MaxConcentration, 1e-9);
            Assert.AreEqual(0.0014, master.Rows[3].RequiredAirChangeRate, 1e-9);
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ThresholdMaster_LoadsFourRows_BestFirst"`
Expected: FAIL（`CleanRoomThresholdMaster` 未定義でコンパイル不可）。

- [ ] **Step 3: スキーマを作成して SourceGenerator をトリガ**

`VanillaSchema/cleanRoomThresholds.yml`（記法は `fluids.yml`/`items.yml` の実例と `edit-schema` スキルに合わせる。数値型キーワードは既存 yml の `integer`/`number` を踏襲）:

```yaml
# NOTE このyamlに記述されているスキーマのコード、JSONローダーはSourceGeneratorによって自動生成されます。
# NOTE The schema code and JSON loader described in this YAML are automatically generated by the SourceGenerator.

id: cleanRoomThresholds
type: object
isDefaultOpen: true
properties:
- key: data
  type: array
  openedByDefault: true
  overrideCodeGeneratePropertyName: CleanRoomThresholdMasterElement
  items:
    type: object
    properties:
    - key: label
      type: string
    - key: maxConcentration
      type: number
    - key: maxGrade
      type: integer
    - key: downBinRate
      type: number
    - key: requiredAirChangeRate
      type: number
```

`Core.Master/_CompileRequester.cs` の `dummyText` を変更（例: `"regenerate-cleanroom-phase2-thresholds"`）。

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。`Mooresmaster.Model.CleanRoomThresholdsModule` が生成される（生成型名は実際の出力で確認）。

- [ ] **Step 4: テスト mod に実データを追加**

`Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/cleanRoomThresholds.json`:

```json
{
  "data": [
    { "label": "A", "maxConcentration": 10.0, "maxGrade": 4, "downBinRate": 0.0,  "requiredAirChangeRate": 0.0167 },
    { "label": "B", "maxConcentration": 50.0, "maxGrade": 3, "downBinRate": 0.05, "requiredAirChangeRate": 0.0083 },
    { "label": "C", "maxConcentration": 200.0, "maxGrade": 2, "downBinRate": 0.15, "requiredAirChangeRate": 0.0042 },
    { "label": "D", "maxConcentration": 1000.0, "maxGrade": 1, "downBinRate": 0.35, "requiredAirChangeRate": 0.0014 }
  ]
}
```

> **重要:** `MasterHolder.GetJson` は `JsonContents[jsonFileName]` の辞書インデクサ直叩きなので、**ロード対象 mod に cleanRoomThresholds.json が無いと KeyNotFoundException で落ちる**。テストで使う mod（`forUnitTest`）に必ず追加する。本番 mod（`../moorestech_master`）への追加は playable 化時に行う（このフェーズでは不要だが、欠落時の挙動として把握しておくこと）。

- [ ] **Step 5: アクセサと MasterHolder 登録を実装**

`Core.Master/CleanRoomThresholdMaster.cs`（`FluidMaster.cs` の形を踏襲。Loader/Model の実型名は Step 3 の生成結果に合わせる）:

```csharp
using System.Collections.Generic;
using Mooresmaster.Loader.CleanRoomThresholdsModule;
using Mooresmaster.Model.CleanRoomThresholdsModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    // クリーンルーム閾値テーブル。行0が最良、行数=Out。
    // Clean room threshold table; row 0 is cleanest, row count means Out.
    public class CleanRoomThresholdMaster : IMasterValidator
    {
        public readonly CleanRoomThresholds CleanRoomThresholds;

        public IReadOnlyList<CleanRoomThresholdMasterElement> Rows => CleanRoomThresholds.Data;
        public int OutThresholdIndex => Rows.Count;

        public CleanRoomThresholdMaster(JToken jToken)
        {
            CleanRoomThresholds = CleanRoomThresholdsLoader.Load(jToken);
        }

        public bool Validate(out string errorLogs)
        {
            // 行の単調性（濃度昇順・必要換気降順）を強制。ヒステリシス判定の前提。
            // Enforce monotonic rows (concentration asc, required ACH desc); hysteresis relies on this.
            for (var i = 1; i < Rows.Count; i++)
            {
                if (Rows[i].MaxConcentration <= Rows[i - 1].MaxConcentration ||
                    Rows[i].RequiredAirChangeRate >= Rows[i - 1].RequiredAirChangeRate)
                {
                    errorLogs = $"cleanRoomThresholds rows must be sorted (cleanest first). row={i}";
                    return false;
                }
            }
            errorLogs = null;
            return true;
        }

        public void Initialize() { }
    }
}
```

`Core.Master/MasterHolder.cs` に静的プロパティと Load を追加（「基盤Master（依存なし）」群の位置）:

```csharp
        public static CleanRoomThresholdMaster CleanRoomThresholdMaster { get; private set; }
```

```csharp
            CleanRoomThresholdMaster = new CleanRoomThresholdMaster(GetJson(masterJsonFileContainer, new JsonFileName("cleanRoomThresholds")));
            InitializeMaster(CleanRoomThresholdMaster);
```

> `Data` プロパティ名・要素型のプロパティ名（`MaxConcentration` 等）は SourceGenerator の実生成結果に合わせる。`IMasterValidator` のメンバは実ファイルで確認。

- [ ] **Step 6: 実行して緑を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ThresholdMaster_LoadsFourRows_BestFirst"`
Expected: PASS。あわせて既存テストのスモーク（`--filter-value "MachineSaveLoadTest"` 等）で MasterHolder.Load の非回帰を確認（json 欠落で全テストが落ちていないか）。

- [ ] **Step 7: Commit**

```bash
git add VanillaSchema/cleanRoomThresholds.yml moorestech_server/Assets/Scripts/Core.Master/ moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/cleanRoomThresholds.json moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs
git commit -m "feat(cleanroom): 閾値マスタcleanRoomThresholdsを追加"
```

---

## Task 2: CleanRoomPurityRules（判定純関数・tick積分・按分）

ヒステリシス込みの閾値行判定・tick積分・再検出按分を、ワールドもデータストアも要らない**純関数**として固定する。worked example の平衡収束もここで純関数ループとして検証する（DI 不要・高速）。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomPurityRules.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs`

### 仕様（バランス確定書§1.1・§3・§5）

- **二条件:** 行 i が成立するのは `C ≤ maxConcentration(i)` **かつ** `ACH ≥ requiredAirChangeRate(i)`。成立する最良（最小 index）の行を返す。どの行も不成立なら `rows.Count`（=Out）。
- **ヒステリシス（濃度・ACH の両条件に適用 — レビューA-6の確定）:** 現在行より**上位（improvement, i < current）**を狙う場合のみ厳しい閾値を使う。
  - 濃度: `maxConcentration × 0.8` を下回って初めて昇格（`PromoteConcentrationFactor = 0.8`）。
  - ACH: `requiredAirChangeRate × 1.25`（= ÷0.8）を上回って初めて昇格（`PromoteAirChangeFactor = 1.25`）。フェーズ3で実効 q が電力割合で連続変動するため、ACH 側にもマージンが無いと要求値近傍で行が毎tick点滅する。**平滑化ではなく昇格側マージン方式を採用**（状態を持たない純関数で完結するため）。
  - 現在行以下（同じ行に留まる・降格する）は素の閾値。
- **tick積分:** `N' = max(0, N + (A_total − n·q·(N/V))·dt)`。V≤0 のときは濃度0扱い（除去項なし）。陽的オイラー。安定条件 `n·q·dt/V < 2` は現行 q=5・1セル1台上限で常に満たす（バランス確定書§3）。
- **按分（再検出引き継ぎ）:** `RedistributeImpurity(N_old, oldCellCount, overlapCells) = N_old · overlap / oldCellCount`。
  - **分母は V ではなく |Cells|（占有セル含む全内部セル数）。** overlap は Cells 同士で数えるため、分母を V（空セルのみ）にすると機械入りの部屋で Σ寄与 > N_old となり保存則が壊れる。
  - 意味: 結合＝N合算／縮小＝濃度保存（除去セル分のNは消滅）／**拡張＝N保存（新規セルは清浄空気扱い＝希釈）**。「N = C·V_new」は縮小・等分割の特殊形でしかない（バランス確定書§5）。

- [ ] **Step 1: 失敗テストを書く（判定・積分・按分）**

`CleanRoomPuritySimulationTest.cs` に追加（`using Game.CleanRoom;` を先頭へ）:

```csharp
        // バランス確定書§1 の4行（A/B/C/D相当）。判定純関数テスト用。
        // The four rows from balance §1, for pure decision tests.
        private static readonly CleanRoomThresholdRow[] Rows =
        {
            new CleanRoomThresholdRow(10.0, 0.0167),
            new CleanRoomThresholdRow(50.0, 0.0083),
            new CleanRoomThresholdRow(200.0, 0.0042),
            new CleanRoomThresholdRow(1000.0, 0.0014),
        };

        // 全行のACH要求（昇格マージン込み）を満たす十分大きい値。
        // ACH large enough to satisfy every row incl. promotion margin.
        private const double AchAllPass = 1.0;

        [Test]
        public void Decide_Row0_HoldBand_StaysRow0()
        {
            // 現在行0・C=9.5（保持帯 8〜10）→ 行0維持。C=11 で素閾値10超え → 行1へ降格。
            // Row 0 holds at C=9.5 (8..10 band); C=11 exceeds 10 -> demote to row 1.
            Assert.AreEqual(0, CleanRoomPurityRules.DecideThresholdIndex(0, 9.5, AchAllPass, Rows));
            Assert.AreEqual(1, CleanRoomPurityRules.DecideThresholdIndex(0, 11.0, AchAllPass, Rows));
        }

        [Test]
        public void Decide_Row1_PromotesOnlyAtOrBelowMargin()
        {
            // C=9（昇格境界 10×0.8=8 超）→ 行1維持。C=8.0（境界ちょうど）→ 行0へ昇格。
            // C=9 above the 8.0 promote bound stays row 1; C=8.0 promotes to row 0.
            Assert.AreEqual(1, CleanRoomPurityRules.DecideThresholdIndex(1, 9.0, AchAllPass, Rows));
            Assert.AreEqual(0, CleanRoomPurityRules.DecideThresholdIndex(1, 8.0, AchAllPass, Rows));
        }

        [Test]
        public void Decide_AchShortfall_Demotes()
        {
            // 現在行0・C=3.2 だが ACH=0.01 < 0.0167。行0→行1 は降格なので
            // 行1の素の要求 0.0083 を使い（昇格境界8は無関係）、行1 に落ち着く。
            // Demotion from row 0 uses row 1's raw ACH requirement (0.0083), not promote bounds.
            Assert.AreEqual(1, CleanRoomPurityRules.DecideThresholdIndex(0, 3.2, 0.01, Rows));
        }

        [Test]
        public void Decide_AchPromotion_RequiresMargin()
        {
            // 行1から行0への昇格は ACH ≥ 0.0167×1.25 = 0.020875 が必要。
            // Promotion to row 0 needs ACH ≥ required × 1.25 (anti-flicker margin).
            Assert.AreEqual(1, CleanRoomPurityRules.DecideThresholdIndex(1, 5.0, 0.018, Rows));
            Assert.AreEqual(0, CleanRoomPurityRules.DecideThresholdIndex(1, 5.0, 0.021, Rows));
        }

        [Test]
        public void Decide_VeryDirtyOrFromOut_BehavesPerHysteresis()
        {
            // C=2000 は全行不成立 → Out（=rows.Count=4）。
            Assert.AreEqual(4, CleanRoomPurityRules.DecideThresholdIndex(0, 2000.0, AchAllPass, Rows));
            // Out(4)からの復帰は全行が昇格扱い: C=9 は行0(≤8)不成立・行1(≤40)成立 → 1。
            // Recovery from Out treats every row as promotion: C=9 fails row0 (≤8), meets row1 (≤40).
            Assert.AreEqual(1, CleanRoomPurityRules.DecideThresholdIndex(4, 9.0, AchAllPass, Rows));
        }

        [Test]
        public void Integrate_ConvergesToWorkedExampleEquilibrium()
        {
            // worked example: V=75, A_total=16, n·q=5 → N_eq=240（C_eq=3.2）。
            // 2000tick（100秒 ≒ 6.7τ, τ=15秒）回して平衡へ。
            // Balance §4 worked example; run 2000 ticks (≈6.7τ) to equilibrium.
            var n = 0.0;
            for (var i = 0; i < 2000; i++)
                n = CleanRoomPurityRules.IntegrateTick(n, 75.0, 16.0, 5.0, 0.05);

            Assert.AreEqual(240.0, n, 2.0, "N_eq = A_total/(n·q) · V = 240");
            Assert.AreEqual(3.2, n / 75.0, 0.05, "C_eq = 16/5 = 3.2");
        }

        [Test]
        public void Integrate_ClampsAtZero()
        {
            // 除去が過剰でも N は負にならない。
            // Over-removal never drives N negative.
            var n = CleanRoomPurityRules.IntegrateTick(1.0, 1.0, 0.0, 100.0, 0.05);
            Assert.AreEqual(0.0, n, 1e-9);
        }

        [Test]
        public void Redistribute_SplitMergeExpand_ConserveCorrectly()
        {
            // 分割: 旧{Cells=10, N=100} → 新2部屋(各overlap=5) → 各50・総和100保存。
            // Split conserves total N; each part gets C_old·overlap.
            var n1 = CleanRoomPurityRules.RedistributeImpurity(100.0, 10, 5);
            var n2 = CleanRoomPurityRules.RedistributeImpurity(100.0, 10, 5);
            Assert.AreEqual(50.0, n1, 1e-9);
            Assert.AreEqual(100.0, n1 + n2, 1e-9);

            // 結合: 旧2部屋{Cells=5, N=50}が全セル重なりで1新部屋へ → 合算100。
            // Merge sums N.
            var merged = CleanRoomPurityRules.RedistributeImpurity(50.0, 5, 5)
                       + CleanRoomPurityRules.RedistributeImpurity(50.0, 5, 5);
            Assert.AreEqual(100.0, merged, 1e-9);

            // 拡張: 旧{Cells=27, N=100}を全て含む大部屋（overlap=27）→ N=100保存（濃度は希釈）。
            // Expansion preserves N; new cells are clean air (dilution).
            Assert.AreEqual(100.0, CleanRoomPurityRules.RedistributeImpurity(100.0, 27, 27), 1e-9);

            // 縮小: overlap=10/27 → N按分（残り17セル分のNは消滅）。
            // Shrink keeps concentration; N outside the overlap vanishes.
            Assert.AreEqual(100.0 * 10 / 27, CleanRoomPurityRules.RedistributeImpurity(100.0, 27, 10), 1e-9);
        }
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPuritySimulationTest.(Decide_|Integrate_|Redistribute_)"`
Expected: FAIL（`CleanRoomPurityRules`/`CleanRoomThresholdRow` 未定義）。

- [ ] **Step 3: 純関数を実装**

`Game.CleanRoom/CleanRoomPurityRules.cs`:

```csharp
using System.Collections.Generic;

namespace Game.CleanRoom
{
    // 閾値1行ぶんの判定値。マスタ要素からデータストアが初期化時に変換して保持する。
    // One threshold row for decisions; converted once from master elements by the datastore.
    public readonly struct CleanRoomThresholdRow
    {
        public readonly double MaxConcentration;       // 個/m³（降格側の素閾値）
        public readonly double RequiredAirChangeRate;  // 1/秒（降格側の素要求）

        public CleanRoomThresholdRow(double maxConcentration, double requiredAirChangeRate)
        {
            MaxConcentration = maxConcentration;
            RequiredAirChangeRate = requiredAirChangeRate;
        }
    }

    // 純度の判定・積分・按分の純関数群。数値はバランス確定書が唯一ソース。
    // Pure functions for purity decisions, integration, and apportionment.
    public static class CleanRoomPurityRules
    {
        // 昇格側ヒステリシス係数。濃度は×0.8、ACHは×1.25（=÷0.8）を超えて初めて昇格。
        // Promotion-side hysteresis: concentration ×0.8, ACH ×1.25 (anti-flicker for both conditions).
        public const double PromoteConcentrationFactor = 0.8;
        public const double PromoteAirChangeFactor = 1.25;

        // Degraded 猶予秒数（バランス確定書§1.2: 5.0秒 = 100tick）。
        // Grace seconds for Degraded (balance §1.2).
        public const double GraceSeconds = 5.0;

        // 現在行・濃度C・換気ACHから次の閾値行を決める。戻り値 rows.Count は Out。
        // Decide the next threshold row; returning rows.Count means Out.
        public static int DecideThresholdIndex(int currentIndex, double concentration, double airChangeRate,
            IReadOnlyList<CleanRoomThresholdRow> rows)
        {
            for (var i = 0; i < rows.Count; i++)
            {
                // 上位行を狙う（昇格）ときだけ両条件にマージンを掛ける。
                // Apply margins to both conditions only when aiming above the current row.
                var isImprovement = i < currentIndex;
                var concentrationLimit = isImprovement ? rows[i].MaxConcentration * PromoteConcentrationFactor : rows[i].MaxConcentration;
                var achRequired = isImprovement ? rows[i].RequiredAirChangeRate * PromoteAirChangeFactor : rows[i].RequiredAirChangeRate;

                if (concentration <= concentrationLimit && airChangeRate >= achRequired) return i;
            }

            return rows.Count;
        }

        // 1tick分の積分: N' = max(0, N + (A − n·q·(N/V))·dt)。
        // One-tick explicit Euler with zero clamp.
        public static double IntegrateTick(double impurityCount, double volume, double aTotalPerSecond,
            double removalVolumePerSecond, double deltaSeconds)
        {
            var concentration = volume > 0.0 ? impurityCount / volume : 0.0;
            var next = impurityCount + (aTotalPerSecond - removalVolumePerSecond * concentration) * deltaSeconds;
            return next < 0.0 ? 0.0 : next;
        }

        // 再検出按分: N_old·overlap/oldCellCount。分母は |Cells|（Vではない。保存則のため）。
        // Apportionment across re-detection; denominator is |Cells|, NOT V, to conserve N.
        public static double RedistributeImpurity(double oldImpurity, int oldCellCount, int overlapCellCount)
        {
            if (oldCellCount <= 0 || overlapCellCount <= 0) return 0.0;
            return oldImpurity * overlapCellCount / oldCellCount;
        }
    }
}
```

- [ ] **Step 4: 実行して緑を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPuritySimulationTest.(Decide_|Integrate_|Redistribute_)"`
Expected: 全 PASS。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomPurityRules.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs
git commit -m "feat(cleanroom): 閾値行判定・tick積分・按分の純関数を追加"
```

---

## Task 3: CleanRoom への純度状態統合 ＋ ICleanRoomAirFilter

codemap v2 §1.2 のとおり、`CleanRoom` 自身に純度状態を持たせる（状態クラスの分離はしない）。あわせてエアフィルターの注入口 `ICleanRoomAirFilter` を定義する。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomRoomStatus.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoom.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomAirFilter.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs`

- [ ] **Step 1: 失敗テストを書く（N加減・0クランプ・濃度）**

`CleanRoomPuritySimulationTest.cs` に追加（`CleanRoom` の実コンストラクタはフェーズ1実装に合わせる。以下は cells/surfaceArea/volume を受ける想定の例）:

```csharp
        [Test]
        public void Room_AddRemoveImpurity_ClampsAtZero_AndConcentrationUsesVolume()
        {
            // 純度状態の最小単位テスト。実ctorはフェーズ1の CleanRoom.cs に合わせる。
            // Minimal purity-state test; align ctor with the actual phase-1 CleanRoom.
            var cells = new HashSet<Vector3Int> { new Vector3Int(0, 0, 0), new Vector3Int(1, 0, 0) };
            var room = new CleanRoom(0, cells, volume: 2, surfaceArea: 10, isValid: true);

            room.AddImpurity(100.0);
            Assert.AreEqual(100.0, room.ImpurityCount, 1e-9);
            Assert.AreEqual(50.0, room.Concentration, 1e-9, "C = N/V = 100/2");

            room.RemoveImpurity(30.0);
            Assert.AreEqual(70.0, room.ImpurityCount, 1e-9);

            // 過剰除去は 0 でクランプ。
            // Over-removal clamps at zero.
            room.RemoveImpurity(1000.0);
            Assert.AreEqual(0.0, room.ImpurityCount, 1e-9);

            // 段階と猶予のセッター。
            // Status and grace setter.
            room.SetStatus(CleanRoomRoomStatus.Degraded, 5.0);
            Assert.AreEqual(CleanRoomRoomStatus.Degraded, room.Status);
            Assert.AreEqual(5.0, room.GraceRemainingSeconds, 1e-9);

            room.SetThresholdIndex(2);
            Assert.AreEqual(2, room.ThresholdIndex);
        }
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Room_AddRemoveImpurity"`
Expected: FAIL（純度メンバ未定義）。

- [ ] **Step 3: CleanRoom を拡張・列挙とフィルター口を作成**

`Game.CleanRoom/CleanRoomRoomStatus.cs`:

```csharp
namespace Game.CleanRoom
{
    // 部屋の運用段階。猶予で flicker を吸収する。
    // Operational status of a room; grace absorbs flicker.
    public enum CleanRoomRoomStatus
    {
        Valid,
        Degraded,
        Invalid,
    }
}
```

`Game.CleanRoom/CleanRoom.cs` に追加するメンバ（既存の幾何メンバは変更しない。codemap v2 §1.2 準拠）:

```csharp
        // ---- 純度状態（データストアが毎tick更新。再検出/ロードで引き継ぐ） ----
        // ---- Purity state, updated by the datastore each tick; carried across re-detection/load ----
        public double ImpurityCount { get; private set; }                  // N（個）
        public CleanRoomRoomStatus Status { get; private set; } = CleanRoomRoomStatus.Valid;
        public int ThresholdIndex { get; private set; } = int.MaxValue;    // 生成直後は未判定（最悪側）。データストアが Out 値で初期化する
        public double GraceRemainingSeconds { get; private set; }
        public double Concentration => Volume > 0 ? ImpurityCount / Volume : 0.0;

        public void AddImpurity(double delta)
        {
            ImpurityCount += delta;
            if (ImpurityCount < 0.0) ImpurityCount = 0.0;
        }

        public void RemoveImpurity(double removed)
        {
            ImpurityCount -= removed;
            if (ImpurityCount < 0.0) ImpurityCount = 0.0;
        }

        public void SetStatus(CleanRoomRoomStatus status, double graceSeconds)
        {
            Status = status;
            GraceRemainingSeconds = graceSeconds < 0.0 ? 0.0 : graceSeconds;
        }

        public void SetThresholdIndex(int index)
        {
            ThresholdIndex = index;
        }
```

> **ThresholdIndex の初期値は「最悪側」**（`int.MaxValue`。`DecideThresholdIndex` は `i < currentIndex` 比較なので全行が昇格扱いになり安全）。データストアは部屋生成時に `MasterHolder.CleanRoomThresholdMaster.OutThresholdIndex` で明示初期化する（Task 4）。初期値を 0（最良）にすると、新規部屋が保持帯の恩恵を不正に受ける。
> 単純な値の Set は `SetXxx` メソッドで行う（AGENTS.md）。`get; private set;` の公開読み取りは許容。

`Game.Block.Interface/Component/ICleanRoomAirFilter.cs`:

```csharp
namespace Game.Block.Interface.Component
{
    // エアフィルター1台の除去能力 q（m³/秒）をデータストアが読むための口。
    // フェーズ3の CleanRoomAirFilterComponent が実装（実効値=q×電力割合×フィルター残有無）。
    // Datastore-facing view of one air filter's removal volume q (m^3/sec); implemented in phase 3.
    public interface ICleanRoomAirFilter : IBlockComponent
    {
        double RemovalVolumePerSecond { get; }
    }
}
```

> `IBlockComponent` の必須メンバ（`IsDestroy` 等）は `Game.Block.Interface/Component/IBlockComponent.cs` を開いて確認。

- [ ] **Step 4: 実行して緑を確認 ＋ フェーズ1非回帰**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Room_AddRemoveImpurity|CleanRoomDetection"`
Expected: 全 PASS（純度メンバ追加が検出を壊していない）。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoom.cs moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomRoomStatus.cs moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomAirFilter.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs
git commit -m "feat(cleanroom): CleanRoomに純度状態を統合しエアフィルター注入口を追加"
```

---

## Task 4: CleanRoomDatastore の純度 tick 統合（積分＋閾値行更新）＋ DI 橋渡し

データストアの tick に純度更新を統合する。`A_total` は provider シーム（既定0・テストは定数注入）、`n·q` はフィルター登録レジストリから合算。worked example（V=75, A_total=16, q=5 → C_eq=3.2, 行0）を DI 経由の実 tick で固定する。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs`

### 数値（バランス確定書§4 worked example）

- 基準部屋: 外殻 7×7×5 → 内寸 5×5×3 = **V=75**（内部に占有ブロック無し）。
- `A_total = 16.0 個/秒`（provider が固定で返す）。
- フィルター1台 `q = 5.0` → `n·q = 5`。**平衡 `C_eq = 16/5 = 3.2`（N_eq=240）**、`ACH = 5/75 ≈ 0.0667 ≥ 0.0167×1.25`。
- 時定数 `τ = V/(n·q) = 15秒 = 300tick` → `RunFrames(2000)`（100秒 ≈ 6.7τ）で十分収束。

- [ ] **Step 1: 失敗テストを書く（平衡収束／フィルター0台は非トートロジーで Out 落ち／新規部屋は Out 初期化）**

`CleanRoomPuritySimulationTest.cs` に追加（using は既存テスト `CleanRoomDetectionTest` に合わせる。`BuildWallShell` ヘルパをこのファイルへコピー）:

```csharp
        // テスト用フィルタースタブ。固定 q を返す。
        // Test stub filter returning a fixed q.
        private sealed class AirFilterStub : Game.Block.Interface.Component.ICleanRoomAirFilter
        {
            public double RemovalVolumePerSecond { get; }
            public bool IsDestroy { get; private set; }
            public AirFilterStub(double q) { RemovalVolumePerSecond = q; }
            public void Destroy() { IsDestroy = true; }
        }

        [Test]
        public void Datastore_ReferenceRoom_ConvergesToCeq3p2_Row0()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            // 外殻 7x7x5 → 内部 5x5x3 = V75。
            // Shell 7x7x5 -> interior 5x5x3 = V75.
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(6, 6, 4));
            datastore.RebuildAll();
            Assert.AreEqual(1, datastore.Rooms.Count);
            Assert.AreEqual(75, datastore.Rooms[0].Volume, "Reference room must be V=75");

            // 室内セルに q=5 のフィルター1台＋A_total=16 を定数注入（バランス確定書§7の流儀）。
            // One filter q=5 at an interior cell; inject constant A_total=16 (balance §7).
            var insideCell = new Vector3Int(3, 2, 3);
            Assert.True(datastore.Rooms[0].Contains(insideCell));
            datastore.AddAirFilter(insideCell, new AirFilterStub(5.0));
            datastore.SetPollutionPerSecondProvider(_ => 16.0);

            GameUpdater.RunFrames(2000);

            // 再検出で部屋オブジェクトが入れ替わっている可能性があるため再取得。
            // Re-fetch: re-detection may have replaced the room instance.
            var room = datastore.Rooms[0];
            Assert.AreEqual(3.2, room.Concentration, 0.05, "C_eq = 16/5 = 3.2");
            Assert.AreEqual(0, room.ThresholdIndex, "best row (A) at equilibrium");
        }

        [Test]
        public void Datastore_NoFilter_FallsToOut_EvenFromBestRow()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4)); // V27
            datastore.RebuildAll();
            var room = datastore.Rooms[0];

            // 非トートロジー化: いったん最良行0にセットし、「tickで Out へ落ちる」ことを検証する。
            // Anti-tautology: seed row 0 first, then assert one tick drops it to Out (ACH=0).
            room.SetThresholdIndex(0);
            datastore.SetPollutionPerSecondProvider(_ => 16.0);
            GameUpdater.RunFrames(1);

            var outIndex = Core.Master.MasterHolder.CleanRoomThresholdMaster.OutThresholdIndex;
            Assert.AreEqual(outIndex, datastore.Rooms[0].ThresholdIndex, "ACH=0 fails every row -> Out");
            // 積分も走っている（N = 16×0.05 = 0.8）。
            // Integration also ran (N accumulated one tick of A_total).
            Assert.AreEqual(0.8, datastore.Rooms[0].ImpurityCount, 1e-6);
        }

        [Test]
        public void Datastore_FreshRoom_StartsAtOut()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            datastore.RebuildAll();

            // 新規部屋の初期行は Out（最良で生まれて保持帯の恩恵を受けてはならない）。
            // A fresh room must start at Out, not at the best row.
            var outIndex = Core.Master.MasterHolder.CleanRoomThresholdMaster.OutThresholdIndex;
            Assert.AreEqual(outIndex, datastore.Rooms[0].ThresholdIndex);
        }
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPuritySimulationTest.Datastore_"`
Expected: FAIL（`AddAirFilter`/`SetPollutionPerSecondProvider`/純度tick 未実装）。

- [ ] **Step 3: データストアに純度 tick を実装**

`Game.CleanRoom/CleanRoomDatastore.cs` に追加（フェーズ1の tick 処理（dirty→再検出）の**後**に純度更新を呼ぶ。`using Core.Master;` 等を追加）:

```csharp
        // 汚染レート供給シーム。既定はゼロ。フェーズ3が CleanRoomPollutionCalculator の算出に差し替える。
        // Pollution provider seam; defaults to zero. Phase 3 wires the real calculator here.
        private Func<CleanRoom, double> _pollutionPerSecondProvider = _ => 0.0;

        // エアフィルター登録（セル→フィルター）。フェーズ3のブロックが設置/破壊時に呼ぶ。
        // Air filter registry (cell -> filter); phase-3 blocks register on place/remove.
        private readonly Dictionary<Vector3Int, ICleanRoomAirFilter> _airFilters = new();

        // 閾値行（マスタから1回変換してキャッシュ）。
        // Threshold rows converted once from the master.
        private IReadOnlyList<CleanRoomThresholdRow> _thresholdRows;

        public void SetPollutionPerSecondProvider(Func<CleanRoom, double> provider)
        {
            _pollutionPerSecondProvider = provider;
        }

        public void AddAirFilter(Vector3Int cell, ICleanRoomAirFilter filter)
        {
            _airFilters[cell] = filter;
        }

        public void RemoveAirFilter(Vector3Int cell)
        {
            _airFilters.Remove(cell);
        }

        // 毎tick: 全部屋の N を積分し、閾値行を二条件＋ヒステリシスで更新する。
        // Each tick: integrate N for every room and update the threshold row.
        private void UpdatePurity()
        {
            EnsureThresholdRows();

            foreach (var room in _rooms)
            {
                var aTotal = _pollutionPerSecondProvider(room);
                var nq = SumRemovalVolume(room);

                // dN 積分（0クランプは純関数内）。
                // Integrate dN (zero clamp inside the pure function).
                var newN = CleanRoomPurityRules.IntegrateTick(room.ImpurityCount, room.Volume, aTotal, nq, GameUpdater.SecondsPerTick);
                var delta = newN - room.ImpurityCount;
                if (delta >= 0.0) room.AddImpurity(delta);
                else room.RemoveImpurity(-delta);

                // 閾値行の更新（ACH = n·q/V）。
                // Update threshold row with ACH = n·q/V.
                var ach = room.Volume > 0 ? nq / room.Volume : 0.0;
                room.SetThresholdIndex(CleanRoomPurityRules.DecideThresholdIndex(room.ThresholdIndex, room.Concentration, ach, _thresholdRows));
            }

            // 孤立部屋の猶予減算は Task 5 でここに追加する。
            // Orphan grace ticking is added here in Task 5.
        }

        // 部屋に属するフィルターの q 合算（登録セルが部屋の Cells に含まれるもの）。
        // Sum q of filters whose registered cell lies in the room's Cells.
        private double SumRemovalVolume(CleanRoom room)
        {
            var sum = 0.0;
            foreach (var kvp in _airFilters)
            {
                if (room.Contains(kvp.Key)) sum += kvp.Value.RemovalVolumePerSecond;
            }
            return sum;
        }

        private void EnsureThresholdRows()
        {
            if (_thresholdRows != null) return;

            // マスタ要素 → 判定行へ1回だけ変換（生成型のプロパティ名は実生成結果に合わせる）。
            // Convert master elements to decision rows once.
            var rows = new List<CleanRoomThresholdRow>();
            foreach (var element in MasterHolder.CleanRoomThresholdMaster.Rows)
                rows.Add(new CleanRoomThresholdRow(element.MaxConcentration, element.RequiredAirChangeRate));
            _thresholdRows = rows;
        }
```

再検出で新しい `CleanRoom` を生成する箇所（フェーズ1実装）に、**新規部屋の行初期化**を追加:

```csharp
            // 新規部屋は Out で開始（保持帯の恩恵を受けない）。引き継ぎがあれば Task 5 で上書き。
            // Fresh rooms start at Out; carry-over (Task 5) overwrites when applicable.
            room.SetThresholdIndex(MasterHolder.CleanRoomThresholdMaster.OutThresholdIndex);
```

tick 購読（フェーズ1の `Update()` 相当）の末尾で `UpdatePurity()` を呼ぶ。**順序は「dirty再検出 → 純度更新」**（同一tick内で部屋集合を確定してから積分する）。

> `MasterHolder.CleanRoomThresholdMaster` は静的アクセスのため ctor 注入不要（コアコンポーネントなので null チェックもしない。AGENTS.md）。`Game.CleanRoom.asmdef` に `Core.Master` 参照が無ければ追加。

- [ ] **Step 4: DI 橋渡しを確認/追加**

`Server.Boot/MoorestechServerDIContainerGenerator.cs`。フェーズ1で橋渡し済みならスキップ。無ければ `GearNetworkDatastore` の前例どおり:

initializer 側（`initializerCollection.AddSingleton<GearNetworkDatastore>();` の近く）:
```csharp
            initializerCollection.AddSingleton<Game.CleanRoom.CleanRoomDatastore>();
```
main 側橋渡し（`services.AddSingleton(initializerProvider.GetService<GearNetworkDatastore>());` の近く）:
```csharp
            services.AddSingleton(initializerProvider.GetService<Game.CleanRoom.CleanRoomDatastore>());
```
eager 実体化（`serviceProvider.GetService<GearNetworkDatastore>();` の近く）:
```csharp
            serviceProvider.GetService<Game.CleanRoom.CleanRoomDatastore>();
```

> **main 側に素の `services.AddSingleton<CleanRoomDatastore>()` を書くのは禁止**（`IWorldBlockDatastore` が解決できず起動時例外）。ctor は initializer 側で解決できる依存（`IWorldBlockDatastore`/`IWorldBlockUpdateEvent`）のみ受けること。`ServerContext` 静的プロパティを ctor 内で読むのも禁止（ServerContext 構築前に解決されるため）。

- [ ] **Step 5: コンパイル ＋ テスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPuritySimulationTest.Datastore_"`
Expected: 全 PASS。

- [ ] **Step 6: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs
git commit -m "feat(cleanroom): データストアに純度tick積分と閾値行更新を統合"
```

---

## Task 5: 再検出引き継ぎ（N＋ThresholdIndex＋Status＋猶予）＋孤立状態＋Valid/Degraded/Invalid

再検出をまたいだ状態継続と段階遷移は**一つの機構**。引き継ぐのは **N だけではなく ThresholdIndex・Status・猶予残も含む**（N のみだと、再検出のたびに行が Out リセットされ、ヒステリシス保持帯（例: C=9 の行0）の部屋が恒久降格する — 旧プランの must-fix バグ）。

### 仕様（バランス確定書§5・§6 ＋ codemap §1.3）

- **マッチ:** 旧部屋（検出中＋Degraded孤立）と新部屋を **Cells の重なり**で対応付け。
- **N:** `N_new = Σ RedistributeImpurity(N_old, |Cells_old|, overlap)`（結合=合算／縮小=濃度保存／拡張=N保存・希釈）。
- **ThresholdIndex:** **最大重なりの旧部屋**の値を引き継ぐ（複数旧部屋の結合時も最大重なり優先）。
- **消滅（どの新部屋にも重ならない旧部屋）:** 破棄せず**孤立状態**へ。Valid だったものは Degraded＋猶予5.0秒開始。既に Degraded なら猶予継続。**Invalid の孤立状態はこの時点で破棄**（無期限保持で過去の汚染が跡地の新部屋へ「転生」するのを防ぐ）。
- **再出現（猶予中に重なりマッチ）:** N・ThresholdIndex を引き継ぎ Valid 復帰・猶予クリア。
- **猶予切れ:** Invalid（N は保持。フェーズ4が稼働停止判定に使う）。孤立のまま次の再検出が来たら破棄。
- 孤立状態は純度 tick（積分・行判定）の対象外。猶予減算のみ行う。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs`

- [ ] **Step 1: 失敗テストを書く**

`CleanRoomPuritySimulationTest.cs` に追加:

```csharp
        [Test]
        public void Datastore_SealBreak_KeepsImpurity_AndGoesDegraded()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4)); // V27
            datastore.RebuildAll();

            datastore.Rooms[0].AddImpurity(150.0);

            // 壁を1枚壊して密閉を崩す → 再検出で部屋が消える。
            // Break one wall -> the room vanishes on re-detection.
            world.RemoveBlock(new Vector3Int(2, 2, 0), BlockRemoveReason.ManualRemove);
            datastore.RebuildAll();
            Assert.AreEqual(0, datastore.Rooms.Count, "room must vanish");

            // 旧状態は破棄されず Degraded・N=150・猶予作動。
            // Old state survives as a Degraded orphan with N preserved and grace running.
            Assert.True(datastore.TryGetDegradedOrphan(out var orphan));
            Assert.AreEqual(CleanRoomRoomStatus.Degraded, orphan.Status);
            Assert.AreEqual(150.0, orphan.ImpurityCount, 1e-6);
            Assert.Greater(orphan.GraceRemainingSeconds, 0.0);
        }

        [Test]
        public void Datastore_ResealWithinGrace_RecoversValid_CarriesImpurityAndThresholdIndex()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            datastore.RebuildAll();

            datastore.Rooms[0].AddImpurity(150.0);
            datastore.Rooms[0].SetThresholdIndex(2); // 引き継ぎ検証用に行2を仕込む

            world.RemoveBlock(new Vector3Int(2, 2, 0), BlockRemoveReason.ManualRemove);
            datastore.RebuildAll();
            GameUpdater.RunFrames(50); // 猶予100tick未満

            // 同じ位置に壁を戻す → 再検出で部屋復活。
            // Restore the wall -> room reappears on re-detection.
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWall, new Vector3Int(2, 2, 0),
                BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out _);
            datastore.RebuildAll();

            Assert.AreEqual(1, datastore.Rooms.Count);
            var recovered = datastore.Rooms[0];
            Assert.AreEqual(CleanRoomRoomStatus.Valid, recovered.Status);
            Assert.AreEqual(150.0, recovered.ImpurityCount, 1e-6, "N carried across reseal");
            Assert.AreEqual(2, recovered.ThresholdIndex, "threshold row carried across reseal");
            Assert.False(datastore.TryGetDegradedOrphan(out _), "orphan consumed by reseal");
        }

        [Test]
        public void Datastore_GraceExpires_GoesInvalid_ThenDiscardedOnNextRebuild()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            datastore.RebuildAll();

            datastore.Rooms[0].AddImpurity(150.0);
            world.RemoveBlock(new Vector3Int(2, 2, 0), BlockRemoveReason.ManualRemove);
            datastore.RebuildAll();
            GameUpdater.RunFrames(120); // 猶予100tick超

            // 猶予切れ → Invalid（N保持）。
            // Grace expired -> Invalid, N retained.
            Assert.True(datastore.TryGetDegradedOrphan(out var expired));
            Assert.AreEqual(CleanRoomRoomStatus.Invalid, expired.Status);
            Assert.AreEqual(150.0, expired.ImpurityCount, 1e-6);

            // 猶予切れ後に再封 → Invalid 孤立は破棄され、新部屋は N=0 から（汚染の「転生」禁止）。
            // Reseal after expiry: the Invalid orphan is discarded; the new room starts clean.
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWall, new Vector3Int(2, 2, 0),
                BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out _);
            datastore.RebuildAll();
            Assert.AreEqual(1, datastore.Rooms.Count);
            Assert.AreEqual(0.0, datastore.Rooms[0].ImpurityCount, 1e-9, "no impurity resurrection");
            Assert.False(datastore.TryGetDegradedOrphan(out _), "Invalid orphan discarded");
        }

        [Test]
        public void Datastore_UnrelatedRebuild_KeepsHoldBandThresholdIndexAndImpurity()
        {
            // must-fix A-1 の非回帰: 無関係な再検出で保持帯（C=9, 行0）の部屋が降格しないこと。
            // Regression for review A-1: an unrelated rebuild must not demote a hold-band room.
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4)); // V27
            datastore.RebuildAll();

            // 平衡 C=9 を作る: A_total = nq·C = 5×9 = 45、N = 9×27 = 243。
            // Steady C=9: A_total = nq·C = 45, N = 9·27 = 243.
            var room = datastore.Rooms[0];
            datastore.AddAirFilter(new Vector3Int(2, 2, 2), new AirFilterStub(5.0));
            datastore.SetPollutionPerSecondProvider(_ => 45.0);
            room.AddImpurity(243.0);
            room.SetThresholdIndex(0); // 保持帯（8〜10）に居る行0の部屋

            // 平衡なので tick しても行0のまま（サニティ）。
            // Sanity: stays at row 0 under ticking (hold band).
            GameUpdater.RunFrames(1);
            Assert.AreEqual(0, datastore.Rooms[0].ThresholdIndex);

            // 無関係な場所に壁を1個置いて全再検出を強制。
            // Place an unrelated wall far away and force a full rebuild.
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWall, new Vector3Int(50, 0, 0),
                BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out _);
            datastore.RebuildAll();
            GameUpdater.RunFrames(1);

            // 引き継ぎが無いと行が Out リセット→ Decide(Out, 9, …) は行1 へ恒久降格してしまう。
            // Without carry-over the row resets to Out and re-promotes only to row 1.
            Assert.AreEqual(0, datastore.Rooms[0].ThresholdIndex, "hold-band row survives unrelated rebuild");
            Assert.AreEqual(243.0, datastore.Rooms[0].ImpurityCount, 1.0, "N survives unrelated rebuild");
        }
```

> `BlockRemoveReason` は `Game.Block.Interface` 名前空間、メンバは `ManualRemove`（`Destroy` は存在しない）。`BlockDirection`/`BlockCreateParam` の using は既存テストに合わせる。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPuritySimulationTest.Datastore_(SealBreak|Reseal|GraceExpires|UnrelatedRebuild)"`
Expected: FAIL（`TryGetDegradedOrphan` 未定義・引き継ぎ未実装）。

- [ ] **Step 3: 引き継ぎと孤立状態を実装**

`CleanRoomDatastore.cs`。再検出の結果反映箇所（旧 `_rooms` を新検出結果で差し替える所）を、以下の引き継ぎ込みに変更する:

```csharp
        // どの検出部屋にも紐付かない継続状態（消滅→Degraded/Invalid 中）。猶予で復活待ち。
        // Orphan rooms (vanished -> Degraded/Invalid) awaiting reseal within grace.
        private readonly List<CleanRoom> _orphanRooms = new();

        public bool TryGetDegradedOrphan(out CleanRoom orphan)
        {
            // テスト/フェーズ4用: 最初の孤立状態を返す。
            // For tests/phase 4: return the first orphan.
            orphan = _orphanRooms.Count > 0 ? _orphanRooms[0] : null;
            return orphan != null;
        }

        // 新検出結果へ旧状態（検出中＋Degraded孤立）を引き継ぐ。Invalid孤立はここで破棄。
        // Carry old states (tracked + Degraded orphans) onto new rooms; Invalid orphans are discarded here.
        private void ApplyDetectionResult(List<CleanRoom> newRooms)
        {
            // 旧状態プール: 直前まで検出されていた部屋 ＋ Degraded 孤立。Invalid 孤立は破棄。
            // Old-state pool: previously detected rooms + Degraded orphans; Invalid orphans dropped.
            var pool = new List<CleanRoom>(_rooms);
            foreach (var orphan in _orphanRooms)
                if (orphan.Status == CleanRoomRoomStatus.Degraded) pool.Add(orphan);
            _orphanRooms.Clear();

            var outIndex = MasterHolder.CleanRoomThresholdMaster.OutThresholdIndex;
            var matched = new HashSet<CleanRoom>();

            foreach (var room in newRooms)
            {
                // 重なる旧状態の寄与を合算し、最大重なりの旧状態から行を引き継ぐ。
                // Sum N contributions; carry the threshold row from the max-overlap old room.
                var carriedN = 0.0;
                CleanRoom best = null;
                var bestOverlap = 0;
                foreach (var old in pool)
                {
                    var overlap = CountOverlap(old.Cells, room);
                    if (overlap <= 0) continue;
                    matched.Add(old);
                    carriedN += CleanRoomPurityRules.RedistributeImpurity(old.ImpurityCount, old.Cells.Count, overlap);
                    if (overlap > bestOverlap) { bestOverlap = overlap; best = old; }
                }

                room.SetThresholdIndex(best != null ? best.ThresholdIndex : outIndex);
                if (carriedN > 0.0) room.AddImpurity(carriedN);
                room.SetStatus(CleanRoomRoomStatus.Valid, 0.0);
            }

            // どの新部屋にも対応しなかった旧状態は孤立へ: Valid→Degraded＋猶予開始、Degraded→猶予継続。
            // Unmatched old states become orphans: Valid -> Degraded with fresh grace; Degraded keeps its grace.
            foreach (var old in pool)
            {
                if (matched.Contains(old)) continue;
                if (old.Status == CleanRoomRoomStatus.Valid)
                    old.SetStatus(CleanRoomRoomStatus.Degraded, CleanRoomPurityRules.GraceSeconds);
                _orphanRooms.Add(old);
            }

            _rooms = newRooms;
        }

        // 旧状態のセル集合と新部屋の重なりセル数。
        // Overlapping cell count between an old room and a new room.
        private static int CountOverlap(IReadOnlyCollection<Vector3Int> oldCells, CleanRoom room)
        {
            var count = 0;
            foreach (var cell in oldCells)
                if (room.Contains(cell)) count++;
            return count;
        }
```

`UpdatePurity()` の末尾（Task 4 のコメント位置）に孤立猶予の減算を追加:

```csharp
            // 孤立状態の猶予を毎tick減らし、切れたら Invalid（破棄は次の再検出時）。
            // Tick down orphan grace; on expiry mark Invalid (discarded at the next re-detection).
            foreach (var orphan in _orphanRooms)
            {
                if (orphan.Status != CleanRoomRoomStatus.Degraded) continue;
                var remaining = orphan.GraceRemainingSeconds - GameUpdater.SecondsPerTick;
                if (remaining > 0.0) orphan.SetStatus(CleanRoomRoomStatus.Degraded, remaining);
                else orphan.SetStatus(CleanRoomRoomStatus.Invalid, 0.0);
            }
```

> `RebuildAll()`・dirty 経由の再検出の**両方**が `ApplyDetectionResult` を通ること（引き継ぎの一本化）。Task 4 で入れた「新規部屋の Out 初期化」は `ApplyDetectionResult` に吸収される（`best == null` 分岐）。重複初期化が残らないよう整理する。

- [ ] **Step 4: テスト実行 ＋ 全体非回帰**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPuritySimulationTest"`
Expected: 全 PASS（Task 4 の平衡テスト含む。`Datastore_UnrelatedRebuild...` が落ちたら ThresholdIndex の引き継ぎ漏れ）。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPuritySimulationTest.cs
git commit -m "feat(cleanroom): 再検出引き継ぎ(N+閾値行+状態+猶予)と孤立状態を実装"
```

---

## Task 6: dirty 分割処理（8192セル/tick）＋ 触れた壁AABB+1 局所化

仕様書§2「同期即時の全再検出は禁止」とバランス確定書§5（担当: フェーズ2）を実装する。**この項目を後続フェーズへ再先送りしない**（先送りループ解消は本改訂の決定事項）。

### 仕様（設計書§2「リーク判定境界の局所化」＋バランス確定書§5）

- **触れた壁AABB+1:** flood-fill 中に**フロンティアで実際に触れた境界セル**の外接箱を逐次拡張し、fill セルがその外接箱 **+1** の外へ出たら即リーク。事前の連結成分計算はしない。正常な密閉部屋は自分の壁で閉じるため正しさは不変で、未密閉構造の探索コストだけが下がる。`MaxRoomVolume`（4096）は安全網として残す。
- **dirty 分割処理:** ブロック設置/削除の購読では**シードセル（変更ブロックの占有セル＋6近傍）をキューに積むだけ**。tick で処理する。1tick の処理量は **visited 8192 セル**を超えたら打ち切り、残りのシードは次 tick へ繰り越す。**ただし 1tick に最低1シードは必ず完了させる**（前進保証。予算はフィル開始前にチェックするソフト上限で、フィル途中の中断・再開はしない）。
- **差分更新:** シードから局所 fill した結果、(a) 密閉部屋になればその領域の既存部屋を置換、(b) リークなら重なる既存部屋を消滅（孤立化）させる。**シードに触れない既存部屋はオブジェクトごと維持**（純度状態もそのまま）。置換・消滅は Task 5 の `ApplyDetectionResult` 系の引き継ぎ規則を通す。
- `RebuildAll()` は従来どおり全走査（テスト・ロード用）で、**実行時に dirty キューをクリアする**。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetector.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDirtyRebuildTest.cs`

- [ ] **Step 1: 失敗テストを書く**

`Tests/CombinedTest/Core/CleanRoomDirtyRebuildTest.cs` を新規作成（using・`BuildWallShell` は既存テストからコピー）:

```csharp
        [Test]
        public void TouchedWallAabb_OpenStructureNearDistantWalls_LeaksCheaply()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            // 未密閉のコの字壁＋遠方(100セル先)に無関係な装飾壁の塊。
            // An unsealed U-shape plus an unrelated decorative wall cluster 100 cells away.
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            world.RemoveBlock(new Vector3Int(2, 2, 0), BlockRemoveReason.ManualRemove); // 穴
            BuildWallShell(world, new Vector3Int(100, 0, 0), new Vector3Int(104, 4, 4));

            datastore.RebuildAll();

            // 穴あき側は部屋にならず、遠方の密閉部屋だけ成立。
            // The holed shell forms no room; only the distant sealed shell does.
            Assert.AreEqual(1, datastore.Rooms.Count);

            // 触れた壁AABB+1 なら、穴あき側のリーク探索は局所bbox脱出で即終了する。
            // グローバルAABB（スパン100超）や MaxRoomVolume 到達だと visited が数千に膨らむ。
            // 上限値はコスト退行ガード（厳密値でない）。初回実行で実測し MaxRoomVolume(4096) より十分小さい値に固定する。
            // With touched-wall AABB+1 the leak search exits the local bbox quickly; the bound is a cost-regression guard.
            Assert.Less(datastore.LastRebuildVisitedCellCount, 1000,
                "leak search must be bounded by the touched-wall AABB, not the global AABB");
        }

        [Test]
        public void DirtyBudget_TwoShells_AppearAcrossTicks_NotInOne()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            // 予算を「1tickに1シード相当」へ絞る（本番値8192はテストで扱える形状にならないため）。
            // Shrink the budget so one tick can finish only ~one seed (production 8192 is untestably large).
            datastore.SetDirtyCellBudgetPerTickForTest(1);

            // 2つの離れた密閉シェルを同時に建てる（tickを挟まない）。
            // Build two separate sealed shells without ticking in between.
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            BuildWallShell(world, new Vector3Int(20, 0, 0), new Vector3Int(24, 4, 4));

            // 予算1では1tickで両方は検出できない（前進保証で最低1シードずつは進む）。
            // Budget 1 cannot finish both rooms in a single tick.
            GameUpdater.RunFrames(1);
            Assert.Less(datastore.Rooms.Count, 2, "budget must defer work to later ticks");

            // 繰り越し処理で最終的に両方検出される。シード総数（壁98個×近傍）に対して十分なtick数を回す。
            // 予算は「実際にfillで訪問したセル数」で数え、境界/訪問済みセルのスキップは0コスト（進行を妨げない）。
            // Carried-over seeds eventually detect both rooms; skipped seeds cost zero budget.
            GameUpdater.RunFrames(2000);
            Assert.AreEqual(2, datastore.Rooms.Count, "all rooms appear eventually");
        }

        [Test]
        public void DirtyIncremental_UntouchedRoomInstanceAndPuritySurvive()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            GameUpdater.RunFrames(50); // dirty経由で検出させる
            Assert.AreEqual(1, datastore.Rooms.Count);
            var room = datastore.Rooms[0];
            room.AddImpurity(150.0);
            room.SetThresholdIndex(1);

            // 遠方に壁を1個置く → 差分更新では既存部屋に触れない。
            // Place a distant wall; incremental update must not touch the existing room.
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWall, new Vector3Int(30, 0, 0),
                BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out _);
            GameUpdater.RunFrames(10);

            // 部屋インスタンスも純度状態もそのまま（差分更新の核心）。
            // Same instance, same purity state — the essence of incremental update.
            Assert.AreEqual(1, datastore.Rooms.Count);
            Assert.AreSame(room, datastore.Rooms[0], "untouched room keeps its instance");
            Assert.AreEqual(150.0, datastore.Rooms[0].ImpurityCount, 1e-6);
            Assert.AreEqual(1, datastore.Rooms[0].ThresholdIndex);
        }
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomDirtyRebuildTest"`
Expected: FAIL（`LastRebuildVisitedCellCount`/`SetDirtyCellBudgetPerTickForTest` 未定義、差分更新未実装）。

- [ ] **Step 3: Detector に「触れた壁AABB+1」と visited 計測を実装**

`CleanRoomDetector.cs` の flood-fill を変更:

- fill 開始時の境界 bbox は**空**。フロンティアの6近傍で境界セルに触れるたびに bbox を**その境界セルで拡張**。
- fill セルが「現在の bbox を**全方向+1** した範囲」の外に出たら即リーク（**+1 を忘れない**: 外への一歩を検知するため。設計書§2）。
- bbox が空のうち（まだ壁に触れていない）はリーク判定を保留し、`MaxRoomVolume` のみで打ち切る。
- visited セル数を out で返し、呼び出し側（データストア）が `LastRebuildVisitedCellCount` に合算して公開する（テスト計測用）。

```csharp
        // 触れた壁AABBの逐次拡張＋1マージン。fillが触れた壁だけで探索を縛る。
        // Grow the touched-wall AABB as the frontier touches boundaries; bound the fill by bbox+1.
        // bboxが未初期化の間は MaxRoomVolume のみが上限。
```

- [ ] **Step 4: Datastore に dirty キュー＋予算＋差分更新を実装**

`CleanRoomDatastore.cs`:

```csharp
        // 再検出待ちのシードセル。設置/削除の購読で積み、tickで予算内消化する。
        // Seed cells awaiting re-detection; enqueued on place/remove, drained per tick within budget.
        private readonly Queue<Vector3Int> _dirtySeeds = new();
        private readonly HashSet<Vector3Int> _dirtySeedSet = new(); // 重複防止

        // 1tickのfill visited予算（バランス確定書§5: 8192）。テストは縮小注入。
        // Per-tick visited budget (balance §5: 8192); tests inject a smaller value.
        public const int DirtyCellBudgetPerTick = 8192;
        private int _dirtyCellBudgetPerTick = DirtyCellBudgetPerTick;
        public int LastRebuildVisitedCellCount { get; private set; }

        public void SetDirtyCellBudgetPerTickForTest(int budget)
        {
            _dirtyCellBudgetPerTick = budget;
        }
```

tick 処理（純度更新の前）:

```csharp
        // dirtyシードを予算内で消化する。最低1シードは必ず処理（前進保証）。
        // Drain dirty seeds within budget; always finish at least one seed per tick.
        private void ProcessDirtySeeds()
        {
            if (_dirtySeeds.Count == 0) return;
            var visitedTotal = 0;
            var processedAny = false;

            while (_dirtySeeds.Count > 0 && (!processedAny || visitedTotal < _dirtyCellBudgetPerTick))
            {
                var seed = _dirtySeeds.Dequeue();
                _dirtySeedSet.Remove(seed);

                // シード周辺を局所fillし、影響部屋の置換/消滅を引き継ぎ規則込みで適用する。
                // Locally fill around the seed and apply room replace/vanish with carry-over rules.
                visitedTotal += DetectAroundSeed(seed);
                processedAny = true;
            }

            LastRebuildVisitedCellCount = visitedTotal;
        }
```

- `DetectAroundSeed`: シードが境界セルなら6近傍の通過セルを起点に、通過セルならそのまま起点に局所 fill（触れた壁AABB+1・`MaxRoomVolume`）。
  - 密閉成立 → fill 結果と Cells が重なる既存部屋を旧状態プールに入れ、`ApplyDetectionResult` 相当の引き継ぎで**その領域だけ**置換。
  - リーク → fill 領域に重なる既存部屋を消滅（孤立化）。
  - fill 済みセルが既存部屋の Cells と完全一致なら何もしない（部屋インスタンス維持）。
- 設置/削除イベントは（フェーズ1同様）**境界ブロックと内部ブロックの両方**で dirty を積む（内部ブロックは V に影響するため。バランス確定書§5）。占有セル＋6近傍をシード化。
- `RebuildAll()` は全走査＋`_dirtySeeds.Clear()`/`_dirtySeedSet.Clear()`＋`LastRebuildVisitedCellCount` 更新。

> 差分更新の引き継ぎは Task 5 の規則（最大重なり・按分・孤立化）を**部分集合に対して**適用する共通メソッドに集約し、`RebuildAll` と `DetectAroundSeed` の二経路で同じコードを通すこと（二重実装禁止）。

- [ ] **Step 5: テスト実行 ＋ 検出・純度の全非回帰**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomDirtyRebuildTest"`
Expected: 全 PASS。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"`
Expected: フェーズ1検出テスト・Task 1〜5 テストが全 PASS（差分更新化で既存挙動を壊していない）。

- [ ] **Step 6: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/ moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDirtyRebuildTest.cs
git commit -m "feat(cleanroom): dirty分割処理(8192セル/tick)と触れた壁AABB+1局所化を実装"
```

---

## Task 7: 永続化（CleanRoomSaveData ＋ 3点改修 ＋ ロード順）

純度状態を `CleanRoomSaveData`（N＋thresholdIndex＋status＋猶予残＋全セル署名）で保存する。`GetSaveData`/`Restore` は `CleanRoomDatastore` 自身が持つ（鉄道 `RailGraphSaveLoadService` の GetSaveData/Restore 形をデータストアに内蔵）。**Degraded 孤立状態も保存対象**。ロード復元は `LoadBlockDataList` → `RebuildAll()`（dirtyクリア込み） → `Restore()` の順。

### セーブ形式（バランス確定書§6）

| フィールド | 型 | 内容 |
|---|---|---|
| `impurityCount` | double | N |
| `thresholdIndex` | int | 現在の閾値行（ヒステリシス保持用） |
| `status` | int | Valid/Degraded/Invalid |
| `graceRemainingSeconds` | float | 猶予残（Degraded時） |
| `cells` | int[][]（x,y,z） | 同一性照合用の全セル署名 |

- V/S は再検出から再導出するため保存しない。
- **保存対象 = 検出中の全部屋（Valid） ＋ Degraded 孤立状態**。Invalid 孤立は保存しない（次の再検出で破棄される運命のため）。
- **復元規則:** レコードを再検出部屋と最大セル重なりで照合。**複数レコードが同一部屋にマッチしたら N は合算**（後勝ち上書き禁止。結合の規則と一貫）。thresholdIndex/status/猶予は**最大重なりレコード**のものを採用。どの部屋にも重ならない **Degraded レコードは孤立状態として復元**（猶予継続）。それ以外の未マッチレコードは破棄。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/SaveLoad/CleanRoomSaveData.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs`
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPurityPersistenceTest.cs`

- [ ] **Step 1: 失敗テストを書く（ラウンドトリップ＋ロード後1tick維持＋Degraded孤立保存）**

`Tests/CombinedTest/Core/CleanRoomPurityPersistenceTest.cs` を新規作成。**ローダ取得は `GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson`**（具象型では DI 登録されていないため `GetService<WorldLoaderFromJson>()` は null を返す。`MachineSaveLoadTest.cs` の実パターン）:

```csharp
using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.CleanRoom;
using Game.Context;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomPurityPersistenceTest
    {
        [Test]
        public void SaveLoad_RoundTrip_PreservesImpurityAndThresholdIndex_AcrossFirstTick()
        {
            // 1. 保存側コンテナ: 部屋を作り N=1215（C=45, 行1の保持帯）・行1 を仕込んで JSON 化。
            // 1. Save-side container: build a room, seed N=1215 (C=45, row-1 hold band) and row 1.
            var (_, saveProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var saveWorld = ServerContext.WorldBlockDatastore;
            var saveDatastore = saveProvider.GetService<CleanRoomDatastore>();

            BuildWallShell(saveWorld, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4)); // V27
            saveDatastore.RebuildAll();
            var room = saveDatastore.Rooms[0];
            room.AddImpurity(1215.0);          // C = 1215/27 = 45（行1の保持帯 40〜50）
            room.SetThresholdIndex(1);

            var json = saveProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            // 2. 新規コンテナで Load → ブロック・部屋・純度を復元。
            // 2. Fresh container; Load restores blocks, rooms, and purity.
            var (_, loadProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var loader = loadProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson;
            loader.Load(json);

            var loadDatastore = loadProvider.GetService<CleanRoomDatastore>();
            Assert.AreEqual(1, loadDatastore.Rooms.Count, "room re-detected after load");
            Assert.AreEqual(1215.0, loadDatastore.Rooms[0].ImpurityCount, 1e-6, "N survived save/load");
            Assert.AreEqual(1, loadDatastore.Rooms[0].ThresholdIndex, "threshold row survived save/load");

            // 3. ロード直後の1tickで再々検出リセットが起きないこと（dirty残骸の非回帰・レビューA-1）。
            //    平衡条件（A_total=nq·C=5×45=225・q=5フィルター）を入れて C=45 を維持したままtickする。
            // 3. One tick after load must not wipe the state (stale-dirty regression, review A-1).
            var insideCell = new Vector3Int(2, 2, 2);
            loadDatastore.AddAirFilter(insideCell, new AirFilterStub(5.0));
            loadDatastore.SetPollutionPerSecondProvider(_ => 225.0);
            GameUpdater.RunFrames(1);

            Assert.AreEqual(1215.0, loadDatastore.Rooms[0].ImpurityCount, 1.0, "N survives the first tick after load");
            Assert.AreEqual(1, loadDatastore.Rooms[0].ThresholdIndex,
                "hold-band row survives the first tick (would fall to row 2 if reset to Out)");
        }

        [Test]
        public void SaveLoad_DegradedOrphan_IsSavedAndRestored_WithRunningGrace()
        {
            // 猶予中（壁破壊直後）にセーブしても N が消えない（「猶予内再封でN継続」との整合）。
            // Saving during grace must not lose N (consistent with reseal-within-grace).
            var (_, saveProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var saveWorld = ServerContext.WorldBlockDatastore;
            var saveDatastore = saveProvider.GetService<CleanRoomDatastore>();

            BuildWallShell(saveWorld, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            saveDatastore.RebuildAll();
            saveDatastore.Rooms[0].AddImpurity(150.0);

            saveWorld.RemoveBlock(new Vector3Int(2, 2, 0), BlockRemoveReason.ManualRemove);
            saveDatastore.RebuildAll();
            GameUpdater.RunFrames(40); // 猶予 5.0 → 3.0 秒
            var json = saveProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            var (_, loadProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(json);

            var loadDatastore = loadProvider.GetService<CleanRoomDatastore>();
            Assert.AreEqual(0, loadDatastore.Rooms.Count, "broken room is not detected");
            Assert.True(loadDatastore.TryGetDegradedOrphan(out var orphan), "Degraded orphan restored");
            Assert.AreEqual(150.0, orphan.ImpurityCount, 1e-6);
            Assert.AreEqual(3.0, orphan.GraceRemainingSeconds, 0.2, "grace keeps running across save/load");

            // 猶予内に再封 → N 復活。
            // Reseal within grace -> N recovers.
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.CleanRoomWall,
                new Vector3Int(2, 2, 0), BlockDirection.North, System.Array.Empty<BlockCreateParam>(), out _);
            loadDatastore.RebuildAll();
            Assert.AreEqual(1, loadDatastore.Rooms.Count);
            Assert.AreEqual(150.0, loadDatastore.Rooms[0].ImpurityCount, 1e-6);
        }

        // BuildWallShell・AirFilterStub は CleanRoomPuritySimulationTest と同じものをコピーする。
        // Copy BuildWallShell / AirFilterStub helpers from CleanRoomPuritySimulationTest.
    }
}
```

> **2コンテナ手順の注意:** `ServerContext` は static のため2つ目の `Create` で後勝ちになる。「保存→JSON確保→新コンテナ→Load」の順を厳守（`MachineSaveLoadTest` と同形）。`Create` 冒頭の `GameUpdater.ResetUpdate()` が旧コンテナの tick 購読を切るので購読リークは起きない。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPurityPersistenceTest"`
Expected: FAIL（`CleanRoomSaveData` 未定義・3点未改修）。

- [ ] **Step 3: 保存レコードと GetSaveData/Restore を実装**

`Game.CleanRoom/SaveLoad/CleanRoomSaveData.cs`:

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.CleanRoom.SaveLoad
{
    // 1部屋（または孤立状態）1レコード。V/S は再検出から再導出するため保存しない。
    // One record per room (or orphan); V/S re-derived from detection, so not saved.
    public class CleanRoomSaveData
    {
        [JsonProperty("impurityCount")] public double ImpurityCount;
        [JsonProperty("thresholdIndex")] public int ThresholdIndex;
        [JsonProperty("status")] public int Status;
        [JsonProperty("graceRemainingSeconds")] public float GraceRemainingSeconds;

        // 同一性照合用の全セル署名（x,y,z の配列の配列）。
        // Full-cell signature for identity matching.
        [JsonProperty("cells")] public List<int[]> Cells;
    }
}
```

`CleanRoomDatastore.cs` に追加:

```csharp
        // 検出中の全部屋＋Degraded孤立を保存する（Invalid孤立は保存しない）。
        // Save all detected rooms plus Degraded orphans (Invalid orphans are not saved).
        public List<CleanRoomSaveData> GetSaveData()
        {
            var result = new List<CleanRoomSaveData>();
            foreach (var room in _rooms) result.Add(ToSaveData(room));
            foreach (var orphan in _orphanRooms)
                if (orphan.Status == CleanRoomRoomStatus.Degraded) result.Add(ToSaveData(orphan));
            return result;
        }

        // 再検出済みの部屋へ最大セル重なりで照合して復元する。複数レコード同部屋は N 合算。
        // Restore by max cell overlap; multiple records on one room sum their N.
        public void Restore(IReadOnlyList<CleanRoomSaveData> saveData)
        {
            if (saveData == null) return;

            // 部屋ごとの最大重なりレコードを記録しつつ N を合算する。
            // Track the max-overlap record per room while summing N.
            var bestByRoom = new Dictionary<CleanRoom, (int overlap, CleanRoomSaveData record)>();

            foreach (var record in saveData)
            {
                if (record?.Cells == null) continue;
                var recordCells = ParseCells(record.Cells);

                CleanRoom best = null;
                var bestOverlap = 0;
                foreach (var room in _rooms)
                {
                    var overlap = 0;
                    foreach (var cell in recordCells)
                        if (room.Contains(cell)) overlap++;
                    if (overlap > bestOverlap) { bestOverlap = overlap; best = room; }
                }

                if (best == null)
                {
                    // 未マッチ: Degraded レコードだけ孤立状態として復元（猶予継続）。他は破棄。
                    // Unmatched: only Degraded records become orphans (grace keeps running).
                    if ((CleanRoomRoomStatus)record.Status == CleanRoomRoomStatus.Degraded)
                        _orphanRooms.Add(CreateOrphanFromRecord(record, recordCells));
                    continue;
                }

                best.AddImpurity(record.ImpurityCount); // 合算（後勝ち上書き禁止）
                if (!bestByRoom.TryGetValue(best, out var current) || bestOverlap > current.overlap)
                    bestByRoom[best] = (bestOverlap, record);
            }

            // 行・状態・猶予は最大重なりレコードを採用。
            // Threshold row / status / grace come from the max-overlap record.
            foreach (var kvp in bestByRoom)
            {
                kvp.Key.SetThresholdIndex(kvp.Value.record.ThresholdIndex);
                kvp.Key.SetStatus((CleanRoomRoomStatus)kvp.Value.record.Status, kvp.Value.record.GraceRemainingSeconds);
            }
        }
```

> `ToSaveData`/`ParseCells`/`CreateOrphanFromRecord` は素直な変換ヘルパ。孤立復元の `CleanRoom` 生成は cells から作り、`Volume`=cells数・`SurfaceArea`=0 で良い（孤立中は純度tick対象外で V/S を使わないため。再封時に検出が正値で作り直す）。`Game.CleanRoom.asmdef` に Newtonsoft.Json 参照が要れば既存セーブ系 asmdef の参照名に合わせて追加。

- [ ] **Step 4: 3点改修**

`WorldSaveAllInfoV1.cs` — `using Game.CleanRoom.SaveLoad;` を追加し、コンストラクタ**最終引数**（現状 `playerRidingStates` の後ろ）に追加:

```csharp
            List<CleanRoomSaveData> cleanRoom)
        {
            // ... 既存代入 ...
            CleanRoom = cleanRoom ?? new List<CleanRoomSaveData>();
        }

        [JsonProperty("cleanRoom")] public List<CleanRoomSaveData> CleanRoom { get; }
```

`AssembleSaveJsonText.cs` — `CleanRoomDatastore` をフィールド＋コンストラクタ注入で追加し、`new WorldSaveAllInfoV1(...)` の末尾引数に追加:

```csharp
                _playerRidingDatastore.GetSaveData(),
                _cleanRoomDatastore.GetSaveData()
            );
```

`WorldLoaderFromJson.cs` — `CleanRoomDatastore` をコンストラクタ注入で追加し、`Load()` の `_worldBlockDatastore.LoadBlockDataList(load.World);` の直後（`RestoreRailSegments` の前後どちらでも可、「ブロック後」が条件）に追加:

```csharp
            // ブロック生成後に全再検出（dirtyクリア込み）し、その後で純度をセル重なり復元する。
            // After blocks exist: full rebuild (clears dirty), then restore purity by cell overlap.
            _cleanRoomDatastore.RebuildAll();
            _cleanRoomDatastore.Restore(load.CleanRoom);
```

> `RebuildAll()` が dirty キューをクリアしないと、`LoadBlockDataList` 中の設置イベントで積まれたシードが次 tick に再々検出を起こし状態を消す（Step 1 のテスト3項が検出する）。Task 0/6 でクリア仕様にしてあることを確認。
> 古いセーブ（`cleanRoom` キー無し）は ctor の `?? new List<>` と `Restore(null)` ガードで安全に素通りする。
> `Game.SaveLoad.asmdef` に `Game.CleanRoom` 参照を追加（コンパイルエラーで判明する）。逆方向参照（Game.CleanRoom→Game.SaveLoad）は無いので循環しない。

- [ ] **Step 5: コンパイル ＋ テスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomPurityPersistenceTest"`
Expected: 全 PASS。

- [ ] **Step 6: フェーズ全体＋セーブ系の非回帰**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"`
Expected: フェーズ1＋2 全 PASS。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(SaveLoad|WorldLoad|Rail).*Test"`
Expected: 既存セーブ/ロード/鉄道テストが非回帰（3点改修が既存を壊していない）。

- [ ] **Step 7: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/ moorestech_server/Assets/Scripts/Game.SaveLoad/ moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomPurityPersistenceTest.cs
git commit -m "feat(cleanroom): 純度永続化(CleanRoomSaveData+3点改修)を追加しフェーズ2完了"
```

---

## フェーズ2 完了の定義（Definition of Done）

- `cleanRoomThresholds.yml`＋`CleanRoomThresholdMaster` がマスタとしてロードされ、4行（A〜D相当）＋`OutThresholdIndex` を公開する。テスト mod に実データ JSON がある。
- `CleanRoomPurityRules.DecideThresholdIndex` が二条件（濃度＋ACH）＋**両条件のヒステリシス**（昇格時のみ 濃度×0.8／ACH×1.25、降格は素閾値）で閾値行を返す。ACH 側マージンの根拠（フェーズ3の電力変動による点滅防止）を本プランに明記済み。
- `CleanRoom` 自身が N（0クランプ）・`Concentration=N/V`・`Status`・`ThresholdIndex`・猶予残を保持する（状態クラスの分離なし）。新規部屋の行初期値は Out。
- `CleanRoomDatastore` が毎tick `dN=(A_total−n·q·C)·0.05` を積分し、基準部屋（V=75・A_total=16・q=5）で **C_eq=3.2・行0** へ収束する。フィルター0台は**行0から1tickで Out へ落ちる**（非トートロジーで検証）。
- `A_total` はデータストア直接算出（既定0・`SetPollutionPerSecondProvider` で定数注入可）。`n·q` は `ICleanRoomAirFilter` の `AddAirFilter`/`RemoveAirFilter` レジストリ（フェーズ3のブロックが設置/破壊時に登録する前提の口）。
- 再検出引き継ぎは **N（按分 `N_old·overlap/|Cells_old|`、結合=合算・縮小=濃度保存・拡張=N保存）＋ThresholdIndex＋Status＋猶予残**。**無関係な再検出で保持帯の部屋が降格しない**ことをテストで固定。
- 密閉が崩れた部屋は Degraded 孤立（N保持・猶予5.0秒）→ 猶予内再封で Valid 復帰（N・行継続）→ 猶予切れで Invalid → **次の再検出で破棄**（汚染の転生禁止）。
- **dirty 分割処理（visited 8192セル/tick・最低1シード前進保証・繰り越し）と触れた壁AABB+1 局所化**が実装され、未密閉構造のリーク探索が局所 bbox で縛られる。差分更新でシードに触れない部屋はインスタンス・純度とも維持。`RebuildAll()` は dirty キューをクリアする。
- `CleanRoomSaveData`（impurityCount/thresholdIndex/status/graceRemainingSeconds/cells）＋3点改修で永続化。**Degraded 孤立も保存**され猶予が継続する。復元の複数マッチは **N 合算**。ロードは `LoadBlockDataList`→`RebuildAll()`→`Restore()` の順で、**ロード直後1tickで状態が消えない**ことをテストで固定。
- DI は initializer 側登録→main 側インスタンス橋渡し＋eager（`GearNetworkDatastore` 同型）。`Game.CleanRoom.asmdef` に `UniRx`/`Core.Master` 参照。
- フェーズ1テスト・既存セーブ/鉄道テストが非回帰。

## フェーズ2で意図的に先送りした事項（後続プラン）

- **汚染源の実係数**（`A_machine`/`k_hatch`/`burst_door`/`a_volume`/`a_surface`/`a_connector`）＝ `CleanRoomPollutionCalculator` → フェーズ3。本フェーズは provider 既定0。
- **エアフィルターブロック実体**（電力・フィルター仕事量消費・実効 q・`AddAirFilter` の設置時配線）→ フェーズ3。
- **ドアバースト**はレートに混ぜず `CleanRoom.AddImpurity(burst)` で N へ直接加算する（バランス確定書§2 の単位注意）。読み出しは peek/advance 分離 → フェーズ5（`A_total` の口とは別経路であることを本フェーズで確定済み）。
- **効果プッシュ**（`CleanRoomEffectResolver`→`ICleanRoomStateReceiver`、MaxGrade/DownBinRate 算出）→ フェーズ4。本フェーズの `ThresholdIndex`/`Status` がその入力。
- **Cells 署名のアンカー方式**（保存サイズ最適化）→ 必要になったら。初期は全セル。

---

## Self-Review

### 中央決定（codemap v2・バランス確定書改訂版）との整合

| 中央決定 | 反映箇所 |
|---|---|
| `CleanRoomDatastore` へ統合（Service/State 分離廃止） | Architecture／Task 3（CleanRoom に状態統合）／Task 4（tick）／Task 7（GetSaveData/Restore 内蔵） |
| `CleanRoomClass` 列挙廃止 → `cleanRoomThresholds.yml`＋`ThresholdIndex`(int)＋`CleanRoomPurityRules` | Task 1／Task 2 |
| 引き継ぎ = N＋ThresholdIndex＋Status＋猶予残、`N_new = Σ C_old·overlap`（縮小=濃度保存/拡張=N保存） | Task 5（按分分母は Cells数 とし保存則を確保。バランス確定書§5の式の趣旨どおり） |
| 孤立状態: Degradedはセーブ対象／Invalidは次の再検出で破棄／ロード複数マッチは合算 | Task 5／Task 7 |
| dirty分割処理（8192セル/tick）＋触れた壁AABB+1 を本フェーズ DoD に | Task 6／DoD |
| DI橋渡し／`IWorldSaveDataLoader` as キャスト／`ManualRemove`／asmdef `UniRx` | 前提注意書き／Task 4 Step 4／Task 7 Step 1／冒頭ルール |
| セーブデータ名 `CleanRoomSaveData`（impurityCount/thresholdIndex/status/graceRemainingSeconds/cells） | Task 7 |

### 批判的レビュー指摘の反映

| 指摘 | 反映 |
|---|---|
| A-1（must-fix）: 再検出で ThresholdIndex が Out リセット→保持帯恒久降格 | Task 5 で行・状態・猶予も引き継ぎ。`Datastore_UnrelatedRebuild_KeepsHoldBandThresholdIndexAndImpurity` と Task 7 の「ロード後1tick維持」で固定 |
| A-2: Degraded 孤立がセーブされず猶予がセーブをまたいで消える | Task 7 で Degraded 孤立を保存対象に。`SaveLoad_DegradedOrphan_IsSavedAndRestored_WithRunningGrace` で固定。status/grace フィールドの死にフィールド化も解消 |
| A-3: Invalid 孤立の無限蓄積と汚染転生 | Task 5 で「次の再検出で破棄」。`Datastore_GraceExpires_GoesInvalid_ThenDiscardedOnNextRebuild` で固定 |
| A-4: AABB局所化・dirty分割の宙吊り | Task 6 として本フェーズに編入（バランス確定書§5の担当明記と整合） |
| A-6: ACH側ヒステリシス欠如 | Task 2 で昇格側マージン方式（×1.25）を確定・テスト固定（平滑化は不採用＝純関数で完結するため） |
| A-7: バースト注入の口が無い | 先送り事項に「`CleanRoom.AddImpurity(burst)` 直接加算・レートに混ぜない」を明記（フェーズ5実装） |
| B-1: 按分の拡張ケース矛盾 | Task 2 で「縮小=濃度保存/拡張=N保存・希釈」を明文化＋拡張ケースの単体テスト追加。さらに分母を V でなく Cells数 とする保存則上の必然を明記 |
| B-2: ACH 0.017 と 60/時の不一致 | バランス確定書改訂値 0.0167 に全テスト期待値を統一 |
| B-3/D-3: stale文言・誤コメント | 旧「tick数を実測で詰めてOut」記述を削除。`Decide_AchShortfall_Demotes` のコメントを降格規則の正しい説明に修正 |
| C-1: `BlockRemoveReason.Destroy` 不在 | 全テストコードを `ManualRemove`（`Game.Block.Interface`）に修正＋冒頭ルールに明記 |
| C-2: `GetService<WorldLoaderFromJson>()` が null | `GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson`（実テストの前例どおり）に修正 |
| C-3: main services で `IWorldBlockDatastore` 解決不能 | 前提注意書き＋Task 4 Step 4 で initializer 登録→橋渡し方式を明記（素の main 登録を明示的に禁止） |
| C-4: asmdef UniRx 欠落 | 前提注意書きに明記 |
| D-1: NoPurifier テストのトートロジー | `Datastore_NoFilter_FallsToOut_EvenFromBestRow` は行0を仕込んでから落ちることを検証。`Datastore_FreshRoom_StartsAtOut` で初期値も別途固定 |
| D-2: yml残骸の不整合 | 新方針では `cleanRoomThresholds.yml` を Task 1 で正式作成。Tech Stack/File Structure/タスクが一貫 |
| D-4: Restore の後勝ち上書き | 複数マッチは N 合算＋最大重なりレコードの行/状態採用（Task 7） |
| D-5: フィルター注入の固定リスト | `AddAirFilter`/`RemoveAirFilter` の動的レジストリに変更（ブロック設置と整合、フェーズ3がそのまま使う） |
| D-6: interface 命名の不統一 | `ICleanRoomAirFilter` で I 付きに統一（旧 `CleanRoomPollutionInput` インターフェースは廃止し Func シームへ） |

### プレースホルダ走査

全タスクが実 C# コード（テスト＋実装）＋実 `uloop` コマンド＋実 `git` コミットを含む。"TODO"/"後で"/"similar to above" によるコード省略は無い。実装者が実ファイルで確認すべき箇所（生成型のプロパティ名・フェーズ1の実コンストラクタ・`IBlockComponent` メンバ・asmdef 参照名）はすべて「確認」指示付きで、コンパイル/テストのチェックポイントが安全網になっている。Task 6 の `DetectAroundSeed` は方針記述（局所fill→置換/消滅/不変の3分岐＋Task 5 規則の共通化）でコード全文を載せていないが、受け入れ条件は `CleanRoomDirtyRebuildTest` の3テストで具体的に固定されている。



