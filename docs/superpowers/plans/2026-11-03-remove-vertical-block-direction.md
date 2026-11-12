# 縦向き（Up*/Down*）の削除とセーブマイグレーション Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** `BlockDirection` を North/East/South/West の4値だけにし、旧セーブ（版9）に残る縦置きブロックとブループリントをロード時に水平へ変換する。

**Architecture:** 列挙を4値（North=0, East=1, South=2, West=3）へ振り直し、12向き前提の分岐をサーバー・クライアントから消す。セーブ版を 9→10 に上げ、`SaveMigrationStepV9ToV10` が world と blueprints の `direction` 整数を読み替える。縦向きは同じ方位の水平へ寝かせ（クリーンルームパネルは壁パネルへ置き換え）、寝かせた占有範囲が他ブロックと重なるものは撤去し、撤去したブロックを指すレール・列車はマスタ欠損除去と同じ除去器で巻き添えにする。

**Tech Stack:** Unity C# / Newtonsoft.Json（JObject）/ NUnit / uloop

## Requirements

- R1: `BlockDirection` は `North, East, South, West` の4値で、整数値は 0,1,2,3。受け入れ: `Enum.GetValues(typeof(BlockDirection)).Length == 4`、Up*/Down* の識別子が repo の .cs・VanillaSchema に1件も残らない（`git grep -nE '(Up|Down)(North|South|East|West)' -- '*.cs' 'VanillaSchema/*.yml'` が、マイグレーションの旧番号表コメント以外で0件）
- R2: 12向き前提の分岐（回転・原点補正・最大座標・水平回転・水平化・Undo の縦判定・相対ガイドの縦の失敗経路）を4向きへ畳む。受け入れ: コンパイル エラー0、既存の EditMode テスト（縦向きを前提にしたケースは削除）が緑
- R3: `WorldSaveAllInfo.CurrentVersion` を 10 にし、`SaveMigrationStepV9ToV10` を `SaveAndEventServiceRegistration` の連鎖へ登録する。受け入れ: 連鎖の構築（起動）が通る
- R4: 版9の world の各ブロックの `direction` を読み替える: 旧 4..7 → 新 0..3（-4）。旧 0..3（Up*）と旧 8..11（Down*）は方位 `旧値 % 4`（N=0,E=1,S=2,W=3）の水平へ寝かせる。`X,Y,Z`（占有範囲の最小の隅）は変えない。受け入れ: ユニットテスト
- R5: 縦向きのクリーンルームパネル（blockGuid `a069613c-bcb5-4b19-a956-1e264a23f81e`）は、blockGuid をクリーンルーム壁パネル `de157d57-7b41-49f5-9228-3762fb196e71` に替え、向きは同じ方位（R4 と同じ `旧値 % 4`）にする。水平のパネルは替えない。受け入れ: ユニットテスト
- R6: 寝かせた占有範囲が、他のブロック（もともと水平のもの・先に寝かせたもの）の占有セルと1セルでも重なるブロックは world から消す。消したブロックの件数・blockGuid・座標を `Debug.LogWarning` に出す。消したブロックの座標を指すレール節・列車節は `RailSegmentsSectionPruner`・`TrainUnitsSectionPruner` で除去する。受け入れ: ユニットテスト＋ロードテスト
- R7: ブループリント（`blueprints[].blocks[]`）にも R4〜R6 を当てる。占有は `offsetX/Y/Z` を最小の隅としてブループリントごとに計算する。消したブロックを端点に持つ `wires`・`chains` の線は消し、残る線の `blockIndexA/B` を詰め直した添字へ付け替える。受け入れ: ユニットテスト
- R8: 旧セーブがロードされ、寝かせたブロックがワールドに水平向きで載り、再保存が版10になることをロードテストで確かめる
- R9: 縦向き起因の bd（moorestech-vd9u 外接枠ロードの例外・moorestech-dou2 相対ガイドの消失）は、縦向きが存在しなくなったことで解消したとして理由付きで close する
- やらないこと: `BlockVerticalDirection`（Up/Horizontal/Down。ベルトの坂の上り下り。`Game.Block.Interface/Direction/BlockVerticalDirection.cs`）は別概念なので残す。`RemovedBlock.cs` の `ToVerticalDirection`（BlockDirection→BlockVerticalDirection の写像）は縦分岐を消すと常に Horizontal を返すので、呼び出し側ごと Horizontal 固定へ畳めるかを Task 1 Step 4 で判断する／撤去したブロックの中身をプレイヤーへ返すこと／撤去データの別ファイル保存（ロード前の原本は `SaveArchiveWriter.WriteBackup` が版9として退避済み）／ベルトの坂（上り・下りの別ブロック）への変更／ネットワークプロトコルの互換（同一ビルド同士なので不要）

## Global Constraints

- AGENTS.md 準拠: 1ファイル200行未満、1ディレクトリ10ファイルまで、partial・`Func<>`・try-catch・デフォルト引数禁止、日英2行コメント、`#region Internal` はメソッド内ローカル関数のみ
- セーブ形式の規約: `CurrentVersion` を1つ上げ、`Game.SaveLoad/Migration/Steps/` に `ISaveMigrationStep` を1本足して連鎖へ登録し、旧版→新版の変換テストを同梱する（moorestech-save-migration スキルが正本。着手前に読む）。変換できない形は例外でなく `SaveMigrationStepResult.Failed(reason)`
- マイグレーションのステップは現行の型（`BlockDirection`・`BlockPositionInfo`）を参照しない。旧12向きの番号表と占有範囲の計算はステップ側に自前で持つ（既存ステップの流儀: `MigrationSaveJson.CleanRoomMachineSaveKey` を文字列で持つのと同じ理由。現行の型が将来変わってもステップの意味が変わらないようにする）
- ブロックのサイズは `MasterHolder.BlockMaster.GetBlockIdOrNull(guid)` → `GetBlockMaster(id).BlockSize` で引く（前例: `Game.SaveLoad/Pruning/Sections/WorldSectionPruner.cs:49`）。マスタに無い blockGuid のブロックは占有に数えず、縦向きなら向きだけ寝かせる（直後の `MissingMasterPruner` が除去する）
- .cs を変えたら `uloop compile --project-path ./moorestech_client` を必ず通す。.meta は手で作らない（Unity が作ったものはコミット可）
- worktree: `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/remove-vertical-block-direction`（branch `feat/remove-vertical-block-direction`、#1581 のブランチ `fix/placement-vertical-rotation-delete-feedback` の上に積む）。Editor はこの worktree のものだけ使う。Unity の外部 repo 同期が `.moorestech-external-revisions.json` を `1c913db0…` へ書き戻すことがある。コミット前に `git diff .moorestech-external-revisions.json` を見て、書き戻されていたら `git checkout -- .moorestech-external-revisions.json` で戻す（正: `a00d3b62ec8607471fd38d14c09381062274a6c9`）。`Client.Localization/_CompileRequester.cs` の自動書き換えもコミットしない

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反1件・修正1件 / 移行が列車除去器を丸ごと呼びマスタ欠損除去が退避・報告から漏れる件を、原因中立の RemovedBlockRailCollateral 切り出しで修正。死活表で退化操作0件
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱3・第3バケツ1 / TryRead の失敗理由の畳み・wasVertical の合成・東西の xz 入替の二重化が弱、占有判定の二重表現を本PR外の提案へ

---

## File Structure

| ファイル | 責務 |
|---|---|
| `moorestech_server/Assets/Scripts/Game.Block.Interface/Direction/BlockDirection.cs`（Modify） | 4値の列挙と回転・原点補正 |
| `moorestech_server/Assets/Scripts/Game.Block.Interface/Direction/BlockDirectionStepRotationExtension.cs`（Modify） | 水平90度回転だけ残し、`ToHorizontalDirection` を消す |
| `moorestech_server/Assets/Scripts/Game.Block.Interface/BlockPositionInfo.cs`（Modify） | 最大座標の計算を4向きへ |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/Removal/RemovedBlock.cs`（Modify） | 縦判定の分岐を消す |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/AnchorRelative/AnchorRelativeDirectionUtil.cs`（Modify） | 12向きで表せない組の失敗経路を消す |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlacementPick/BlockPickResolver.cs`（Modify） | 水平化の呼び出しを消す |
| `VanillaSchema/challenges.yml`（Modify） | `blockDirection` enum の Up*/Down* を消す（3か所） |
| `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV9ToV10.cs`（Create） | V9→V10 の部品を並べる |
| `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V9ToV10/LegacyTwelveDirection.cs`（Create） | 旧12向きの番号→新水平番号・縦かどうか・寝かせた占有セル列 |
| `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V9ToV10/LaidFlatOccupancy.cs`（Create） | 占有セル集合への登録と重なり判定 |
| `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V9ToV10/VerticalDirectionWorldRewriter.cs`（Create） | world の向き読み替え・パネル置換・重なり撤去・レール/列車の巻き添え |
| `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V9ToV10/VerticalDirectionBlueprintRewriter.cs`（Create） | blueprints の同じ処理と線の付け替え |
| `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs`（Modify） | `CurrentVersion = 10` |
| `moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs`（Modify） | 連鎖へ `new SaveMigrationStepV9ToV10()` を足す |
| `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/Migration/SaveMigrationStepV9ToV10Test.cs`（Create） | ステップ単体のテスト |
| `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/VerticalBlockDirectionMigrationLoadTest.cs`（Create） | 旧セーブのロードテスト |

---

### Task 1: 列挙を4値にし、12向き前提の分岐をサーバー・クライアント・スキーマから消す

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/Direction/BlockDirection.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/Direction/BlockDirectionStepRotationExtension.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/BlockPositionInfo.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/Removal/RemovedBlock.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/AnchorRelative/AnchorRelativeDirectionUtil.cs`（と、その Try の失敗を受けている呼び出し側: `git grep -n AnchorRelativeDirectionUtil` で列挙）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlacementPick/BlockPickResolver.cs`
- Modify: `VanillaSchema/challenges.yml`（`key: blockDirection` の enum 3か所）
- Modify（テスト）: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SimpleTrainTestStation.cs:81`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/MultiSizeBlockTest.cs`、`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltConnection/Topology/BeltTopologyBeltTest.cs`、`moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/ConnectorShapeConnectionTest.cs`、`moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoom/Usability/CleanRoomPanelOrientationTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlockPickResolverTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Shared/PreviewSurfaceTypeResolverTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/UnitTest/Tutorial/AnchorRelativeDirectionUtilTest.cs`
- Delete（テスト）: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltConnection/BeltConnectionVerticalOrientationTest.cs`（と .meta）

**Interfaces:**
- Produces: `public enum BlockDirection { North, East, South, West }`（0..3）。`BlockDirectionStepRotationExtension.HorizonRotation(this BlockDirection)` は残す。`ToHorizontalDirection` は削除

- [ ] **Step 1: 列挙と回転を4値にする**

`BlockDirection.cs` の列挙・`GetRotation`・`GetBlockModelOriginPos`・`GetBlockDirectionOffset` から Up*/Down* の行を消し、summary を4方向の説明に直す:

```csharp
    /// <summary>
    ///     ブロックの水平4方向の向き。上下に回した設置は無い
    ///     The block's four horizontal headings; there is no up/down placement
    /// </summary>
    public enum BlockDirection
    {
        North,
        East,
        South,
        West,
    }
```

`GetRotation` は North/East/South/West の4 case と default の throw を残す。`GetBlockModelOriginPos` は4 case を switch 式にする:

```csharp
        public static Vector3Int GetBlockModelOriginPos(this BlockDirection blockDirection, Vector3Int pos, Vector3Int size)
        {
            var addPos = blockDirection switch
            {
                BlockDirection.North => new Vector3Int(0, 0, 0),
                BlockDirection.East => new Vector3Int(0, 0, size.x),
                BlockDirection.South => new Vector3Int(size.x, 0, size.z),
                BlockDirection.West => new Vector3Int(size.z, 0, 0),
                _ => throw new ArgumentOutOfRangeException(nameof(blockDirection), blockDirection, null),
            };
            return pos + addPos;
        }
```

`GetBlockDirectionOffset` も4 case と `_ => throw new ArgumentOutOfRangeException(...)` にする（旧の `_ => Vector3Int.zero` は無音の既定値なので throw に替える）。

- [ ] **Step 2: 水平回転だけ残す**

`BlockDirectionStepRotationExtension.cs` を次にする（`ToHorizontalDirection` は削除）:

```csharp
using System;

namespace Game.Block.Interface
{
    /// <summary>
    ///     設置時の方向切替。水平に90度ずつ回す
    ///     Direction cycling during placement; turns 90 degrees horizontally
    /// </summary>
    public static class BlockDirectionStepRotationExtension
    {
        public static BlockDirection HorizonRotation(this BlockDirection blockDirection)
        {
            return blockDirection switch
            {
                BlockDirection.North => BlockDirection.East,
                BlockDirection.East => BlockDirection.South,
                BlockDirection.South => BlockDirection.West,
                BlockDirection.West => BlockDirection.North,
                _ => throw new ArgumentOutOfRangeException(nameof(blockDirection), blockDirection, null),
            };
        }
    }
}
```

- [ ] **Step 3: 最大座標を4向きにする**

`BlockPositionInfo.CalcBlockMaxPos` の switch から Up*/Down* の case を消し、switch 式にする:

```csharp
            var addPos = direction switch
            {
                BlockDirection.North or BlockDirection.South => new Vector3Int(blockSize.x, blockSize.y, blockSize.z),
                BlockDirection.East or BlockDirection.West => new Vector3Int(blockSize.z, blockSize.y, blockSize.x),
                _ => throw new System.ArgumentOutOfRangeException(nameof(direction), direction, null),
            };
```

- [ ] **Step 4: クライアントの縦向き分岐を消す**

- `RemovedBlock.cs:84-85` の Up*/Down* を並べた分岐を読み、縦向きでしか真にならない分岐ごと削る（水平の経路は残す）
- `AnchorRelativeDirectionUtil.cs`: 12向きで表せない組（縦が混ざる組）を「未検出＋警告」にしていた失敗経路を消す。4向き同士の合成は必ず成功するので、`Try…(…, out …)` 形なら戻り値を `BlockDirection` を直接返す形へ変え、呼び出し側の失敗分岐（ガイドを消す・警告を出す）も削る。これで moorestech-dou2 の原因（最寄りアンカーが縦だとガイドが消える）が無くなる
- `BlockPickResolver.cs:33`: `pickedDirection.ToHorizontalDirection()` を `pickedDirection` にする
- `git grep -nE 'BlockPreviewBoundingBox'` で `SetBoundingBox` の switch を開き、4 case の switch 式のまま残っていることを確かめる（moorestech-vd9u は Up* の値が来ていたことが原因。値が無くなるので変更不要）

- [ ] **Step 5: スキーマの enum から縦を消す**

`VanillaSchema/challenges.yml` の `key: blockDirection`（3か所）の `options` を `North, East, South, West` の4つにする。`../moorestech_master`（pin `a00d3b62`）の JSON に Up*/Down* の文字列が無いことは確認済み（`git grep -cE '"(Up|Down)(North|South|East|West)"' a00d3b62` が0件）。edit-schema スキルの手順で SourceGenerator を再生成させる

- [ ] **Step 6: テストを4向きに合わせる**

- `SimpleTrainTestStation.cs:81` の `(BlockDirection)4 + i` を `(BlockDirection)i` にする（旧 North=4 前提）
- `BeltConnectionVerticalOrientationTest.cs` は縦向きのベルトだけを検査するので .cs と .meta ごと削除
- 他のテストの Up*/Down* を使うケース（`MultiSizeBlockTest` の UpEast・`BeltTopologyBeltTest:136` の UpNorth・`ConnectorShapeConnectionTest:46-49` の UpNorth 歯車・`BlockPickResolverTest:30-31`・`PreviewSurfaceTypeResolverTest:43-45`・`AnchorRelativeDirectionUtilTest:28-33,58-59`）は、そのケース（TestCase 行または Test メソッド）を削除する。水平のケースは残す。`CleanRoomPanelOrientationTest` は `Enum.GetValues` で全向きを回しているので、そのまま4向きで通るか確かめ、縦向き前提の期待値があれば削る
- `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/sprint/sprint-toggle-mode.cs` は `HorizonRotation` だけなので変更不要

- [ ] **Step 7: 残りが無いことを確かめ、コンパイルとテスト**

Run: `git grep -nE '(Up|Down)(North|South|East|West)' -- '*.cs' 'VanillaSchema/*.yml'`
Expected: 0件

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlockDirection|BlockPositionInfo|MultiSizeBlock|BeltTopology|BeltConnection|ConnectorShape|CleanRoom|BlockPickResolver|PreviewSurfaceType|AnchorRelative|SimpleTrain|Blueprint|PlaceSystem|Tutorial" --timeout-seconds 1500`
Expected: ErrorCount 0 / 全件 PASS

- [ ] **Step 8: コミット**

```bash
git add -A moorestech_server moorestech_client VanillaSchema
git reset -q moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs
git commit -m "ブロックの向きを水平4方向だけにし、縦向き(Up*/Down*)の分岐をサーバー・クライアント・スキーマから消す"
```

---

### Task 2: V9→V10 マイグレーション（world・blueprints）と版の引き上げ

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/SaveMigrationStepV9ToV10.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V9ToV10/LegacyTwelveDirection.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V9ToV10/LaidFlatOccupancy.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V9ToV10/VerticalDirectionWorldRewriter.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Migration/Steps/V9ToV10/VerticalDirectionBlueprintRewriter.cs`
- Create: `moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning/RemovedBlockRailCollateral.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning/Sections/RailSegmentsSectionPruner.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning/Sections/TrainUnitsSectionPruner.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfo.cs:26`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Composition/SaveAndEventServiceRegistration.cs:117`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/Migration/SaveMigrationStepV9ToV10Test.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SaveLoad/VerticalBlockDirectionMigrationLoadTest.cs`

**Interfaces:**
- Consumes: `ISaveMigrationPart`（`TryPrepare(JObject, out string)` / `ApplyAndSummarize()`）、`MigrationSaveJson.RunParts/Reject/IsAbsentOrNull`、`new RemovedWorldBlockPositions(JArray removedBlocks)`（要素は `X/Y/Z/instanceId` を持つブロック JObject）
- Produces:
  - `public sealed class SaveMigrationStepV9ToV10 : ISaveMigrationStep`（`FromVersion => 9`）
  - `internal static class LegacyTwelveDirection` — `bool TryRead(JToken directionToken, out int horizontal, out bool wasVertical)`（旧 0..11 以外・整数でなければ false）、`IEnumerable<Vector3Int> OccupiedCells(Vector3Int minCorner, int horizontal, Vector3Int blockSize)`
  - `internal sealed class LaidFlatOccupancy` — `void Add(IEnumerable<Vector3Int> cells)`、`bool Overlaps(IEnumerable<Vector3Int> cells)`
  - `public static class RemovedBlockRailCollateral`（`Game.SaveLoad.Pruning`）— `public static JArray RemoveRailSegmentsTouching(JArray railSegments, RemovedWorldBlockPositions removed)`（外したレール接続を返す）、`public static JArray RemoveTrainUnitsOnRemovedBlocks(JArray trainUnits, RemovedWorldBlockPositions removed)`（外した列車を返す）。原因に依らない巻き添え処理だけを持ち、ログ文言も原因中立（「除去したブロックにつながる…」）。マスタ欠損の判定（貨車・積荷）は持たない
  - `public const string` の GUID: `LegacyTwelveDirection.CleanRoomPanelGuid = "a069613c-bcb5-4b19-a956-1e264a23f81e"`、`CleanRoomWallPanelGuid = "de157d57-7b41-49f5-9228-3762fb196e71"`

- [ ] **Step 1: moorestech-save-migration スキルを読む**

`.agents/skills/moorestech-save-migration/SKILL.md` を読み、版の上げ方・登録・テストの流儀を確かめる（本 plan と食い違えばスキル側に従い、判断記録へ1行書く）

- [ ] **Step 2a: レール・列車の巻き添え処理を原因中立のクラスへ切り出す**

`TrainUnitsSectionPruner.Prune` を丸ごと呼ぶと、マスタ欠損の貨車・積荷の除去まで移行の中で先に走り、`SaveLoadPreparer` の退避（`WritePruned`）と報告（`MissingMasterPruneReportStore`）から漏れる。そこで巻き添えだけを切り出す:
- `Pruning/RemovedBlockRailCollateral.cs` を新設し、`RailSegmentsSectionPruner.Prune` の foreach（start/end のどちらかが除去座標なら外す）を `RemoveRailSegmentsTouching` へ、`TrainUnitsSectionPruner` のローカル関数 `RemoveTrainsOnRemovedBlocks` の本体を `RemoveTrainUnitsOnRemovedBlocks` へ移す。どちらも外した要素の `DeepClone` を JArray で返す。ログは「除去したブロックにつながるレール接続を外しました」「除去したブロックのレールに載る列車を外しました」と原因を書かない文言にする
- `RailSegmentsSectionPruner`・`TrainUnitsSectionPruner` はこの2関数を呼び、戻りを `result.RemovedRailSegments`・`result.RemovedTrainUnits` へ `Add` する（マスタ欠損の文言のログは各除去器側に残す）
- 既存の除去器テスト（`git grep -ln 'RailSegmentsSectionPruner\|TrainUnitsSectionPruner' -- '*Test*.cs'`）が緑のままであること

- [ ] **Step 2: 旧12向きの表と占有を書く**

`V9ToV10/LegacyTwelveDirection.cs`:

```csharp
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Game.SaveLoad.Migration.Steps.V9ToV10
{
    /// <summary>
    /// 版9の12向きの番号（Up 0..3・水平 4..7・Down 8..11、各組は北東南西の順）を版10の水平番号（北0東1南2西3）へ読む。現行の BlockDirection は参照しない
    /// Reads the version-9 twelve-direction numbers (Up 0..3, flat 4..7, Down 8..11, each group N/E/S/W) as version-10 horizontal numbers (N0 E1 S2 W3); the live BlockDirection is never referenced
    /// </summary>
    internal static class LegacyTwelveDirection
    {
        public const string CleanRoomPanelGuid = "a069613c-bcb5-4b19-a956-1e264a23f81e";
        public const string CleanRoomWallPanelGuid = "de157d57-7b41-49f5-9228-3762fb196e71";

        public static bool TryRead(JToken directionToken, out int horizontal, out bool wasVertical)
        {
            horizontal = 0;
            wasVertical = false;
            if (directionToken == null || directionToken.Type != JTokenType.Integer) return false;
            var legacy = directionToken.Value<int>();
            if (legacy < 0 || legacy > 11) return false;

            // 縦は同じ方位の水平へ寝かせる
            // A vertical direction lies flat on the same heading
            horizontal = legacy % 4;
            wasVertical = legacy < 4 || legacy >= 8;
            return true;
        }

        // 最小の隅から、水平向きで占めるセルを列挙する（東西はxとzが入れ替わる）
        // Enumerate the cells a horizontal block occupies from its min corner (east/west swap x and z)
        public static IEnumerable<Vector3Int> OccupiedCells(Vector3Int minCorner, int horizontal, Vector3Int blockSize)
        {
            var isEastOrWest = horizontal == 1 || horizontal == 3;
            var extent = isEastOrWest ? new Vector3Int(blockSize.z, blockSize.y, blockSize.x) : blockSize;
            for (var x = 0; x < extent.x; x++)
            for (var y = 0; y < extent.y; y++)
            for (var z = 0; z < extent.z; z++)
                yield return minCorner + new Vector3Int(x, y, z);
        }
    }
}
```

`V9ToV10/LaidFlatOccupancy.cs`: `HashSet<Vector3Int>` を持ち、`Add(cells)` で登録、`Overlaps(cells)` で1つでも含まれれば true を返す（両メソッドとも `IEnumerable<Vector3Int>` を受ける。`Overlaps` は列挙を1回だけ回す）。

- [ ] **Step 3: world の書き換え部品を書く**

`V9ToV10/VerticalDirectionWorldRewriter.cs`（`ISaveMigrationPart`）。処理:

`TryPrepare(save, out reason)`:
1. `save["world"]` が `JArray` でなければ `MigrationSaveJson.Reject(...)`
2. 各要素が `JObject` で、`X/Y/Z` が整数、`direction` が `LegacyTwelveDirection.TryRead` で読めることを確かめる。読めなければ `Reject($"world要素の向き・座標が読めません。 instanceId={block["instanceId"]} direction={block["direction"]}", out reason)`
3. 検査が通ったブロックを (block, minCorner, horizontal, wasVertical, size or null) の対象リストへ集める。size は `MasterHolder.BlockMaster.GetBlockIdOrNull(Guid.Parse(blockGuid))` が null なら null（マスタ欠損。占有に数えない）。blockGuid が Guid として読めなければ Reject
4. `instanceId` の昇順に並べる（重なる2つの縦置きのどちらを残すかを決めるため。判断記録 D3）

`ApplyAndSummarize()`:
1. 水平だったブロック（`wasVertical == false`）のうち size のあるものの `OccupiedCells` を `LaidFlatOccupancy` へ登録
2. 全ブロックの `direction` を `horizontal` に書き換える
3. 縦だったブロックを順に処理: blockGuid が `CleanRoomPanelGuid` なら `CleanRoomWallPanelGuid` に替える。size があれば `OccupiedCells(minCorner, horizontal, size)` を `Overlaps` で判定し、重なれば world から外して `removed`（JArray）へ `DeepClone` を足す。重ならなければ `Add`
4. `removed` が1件以上なら `Debug.LogWarning($"縦向きを寝かせると他のブロックと重なるため撤去しました。 件数={removed.Count} 対象=[{string.Join(", ", …"{blockGuid}@({X},{Y},{Z})")}]")`、`new RemovedWorldBlockPositions(removed)` を作り、`save["railSegments"]`・`save["trainUnits"]` が JArray なら `RemovedBlockRailCollateral.RemoveRailSegmentsTouching(...)`・`RemoveTrainUnitsOnRemovedBlocks(...)` を当てる（無い／null なら飛ばす。配列以外の形は `TryPrepare` で Reject しておく）
5. 要約 `"縦向きを寝かせた={n}件 壁パネルへ置換={m}件 重なりで撤去={k}件 巻き添えのレール={r}件 列車={t}件"` を返す（r・t は2関数の戻り JArray の Count）

ローカル関数は `#region Internal` に入れ、200行を超えたら判定（集める・寝かせる）を別 internal クラスへ分ける。

- [ ] **Step 4: blueprints の書き換え部品を書く**

`V9ToV10/VerticalDirectionBlueprintRewriter.cs`（`ISaveMigrationPart`）。`save["blueprints"]` が無い／null なら何もしない（要約 `"ブループリント 対象なし"`）。配列でなければ Reject。各ブループリントごとに Step 3 と同じ規則を当てる（占有はブループリント内だけで計算。最小の隅は `offsetX/Y/Z`。重なり判定の順序はブロック配列の添字順）。撤去したブロックがあれば:
- `blocks` から外し、旧添字→新添字の対応表を作る
- `wires`・`chains` の各線で `blockIndexA`/`blockIndexB` のどちらかが撤去した添字なら線を消し、残りは対応表で付け替える
- `Debug.LogWarning($"ブループリント内の縦向きを寝かせると重なるため撤去しました。 blueprint={name} 件数={k}")`

`TryPrepare` では `blocks` が配列であること・各要素の `direction`・`offsetX/Y/Z`・`blockGuid` が読めること・`wires`/`chains` が配列で添字が範囲内であることを検査し、外れたら Reject。

- [ ] **Step 5: ステップ・版・登録**

```csharp
using Game.SaveLoad.Migration.Steps.Shared;
using Game.SaveLoad.Migration.Steps.V9ToV10;
using Newtonsoft.Json.Linq;

namespace Game.SaveLoad.Migration.Steps
{
    // V9→V10移行: 縦向き(Up*/Down*)の廃止に合わせ、向きを水平4方向の番号へ読み替え、縦置きを寝かせる
    // V9->V10 migration: with Up*/Down* removed, renumber directions to the four horizontal headings and lay vertical blocks flat
    public sealed class SaveMigrationStepV9ToV10 : ISaveMigrationStep
    {
        public int FromVersion => 9;

        public SaveMigrationStepResult Migrate(JObject save)
        {
            var parts = new ISaveMigrationPart[]
            {
                new VerticalDirectionWorldRewriter(),
                new VerticalDirectionBlueprintRewriter(),
            };
            return MigrationSaveJson.RunParts(FromVersion, parts, save);
        }
    }
}
```

`WorldSaveAllInfo.CurrentVersion` を `10` にし、`SaveAndEventServiceRegistration.cs:117` の `SaveMigrationChain.ForCurrentVersion(new ISaveMigrationStep[]{…})`（AGENTS.md の `new SaveMigrationChain(...)` に当たる箇所）の配列末尾に `new SaveMigrationStepV9ToV10()` を足す。`git grep -n 'CurrentVersion\|worldVersion"\] = 9\|ForCurrentVersion'` で版9を前提にした既存テスト（例: `SaveMigrationChainTest`）を探し、期待値を 10 へ合わせる

- [ ] **Step 6: ステップのユニットテスト**

`SaveMigrationStepV9ToV10Test.cs`（`[SetUp]` で `new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory))` を呼んでテスト mod のマスタを読む）。セーブ JSON は手で組む（`{"worldVersion":9,"world":[...],"blueprints":[...]}`）。ケース:

1. 水平 4,5,6,7 → 0,1,2,3（座標・blockGuid は不変）
2. Up 0..3 と Down 8..11 の各1つ（1×1×1 の `ForUnitTestModBlockId` のブロックの guid）→ `旧 % 4`
3. 縦のクリーンルームパネル（`a069613c-…`）UpWest(3)・DownEast(9) → blockGuid `de157d57-…`、向き 3・1。水平のパネル(4) は guid 不変・向き 0
4. 1×2×3 ブロック（`MultiSizeBlockTest` の `Block_1x2x3_Id` の guid）を UpNorth(0) で (0,0,0) に置き、寝かせた占有（x0, y0..1, z0..2）と重なる (0,1,2) に水平の 1×1×1 → 1×2×3 が world から消え、水平ブロックは残る。`LogAssert.Expect(LogType.Warning, new Regex("重なるため撤去"))`
5. 同じ 1×2×3 を UpNorth で、寝かせた占有と重ならない位置にだけ他ブロック → 残って向き 0
6. 撤去したブロックの座標を `connectionDestination.blockPosition` に持つ `railSegments` の要素が消える（要素の形は `RailSegmentsSectionPruner` のテストから写す: `git grep -ln RailSegmentsSectionPruner -- '*Test*.cs'`）
7. ブループリント: 3ブロック（[0]=1×2×3 UpNorth・[1]=重なる水平・[2]=離れた水平）、`wires` に (0,2) と (1,2)、`chains` 空 → blocks が2件、wires は (0,1)（旧(1,2)の付け替え）だけ
8. direction が 12・文字列・欠損のいずれかで `IsConverted == false`（`LogAssert.Expect(LogType.Warning, …)`）、原本 JSON が書き換わっていない（`JToken.DeepEquals` で事前コピーと比較）
9. 戻ったセーブの `worldVersion` は 9 のまま（連鎖側が上げる。V8ToV9 テストと同じ確認）

- [ ] **Step 7: ロードテスト**

`VerticalBlockDirectionMigrationLoadTest.cs` を `BeltConveyorStateMigrationLoadTest.cs` と同じ形で書く（`SaveLoadPreparerTestFixture.ArchiveRootForThisRun()`・一時セーブパス・`IWorldSaveDataLoader.LoadOrInitialize()`）。版9セーブの作り方: 現行で 1×2×3 ブロックを (3,0,5) に `BlockDirection.East` で、1×1×1 ブロックを (10,0,10) に North で置いてセーブを組み立て、world の各 `direction` を旧番号へ戻す（1×2×3 は旧 UpEast=1、1×1×1 は旧 North=4）、`worldVersion` を 9 にする。検査:
- ロード後 `ServerContext.WorldBlockDatastore.GetBlock(new Vector3Int(3,0,5))` が 1×2×3 で `BlockPositionInfo.BlockDirection == BlockDirection.East`、占有の最大座標が `BlockPositionInfo.CalcBlockMaxPos((3,0,5), East, size)` と一致
- (10,0,10) のブロックが North
- 版9の原本が `BackupSaveJsonPath(9)` に退避され、再保存（`AssembleSaveJsonText`）の `worldVersion` が `WorldSaveAllInfo.CurrentVersion`（10）

- [ ] **Step 8: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SaveMigration|MigrationLoad|SaveLoad|Blueprint" --timeout-seconds 1500`
Expected: ErrorCount 0 / 全件 PASS

- [ ] **Step 9: コミット**

```bash
git add moorestech_server
git commit -m "セーブ版を10へ上げ、版9の縦向きブロックとブループリントを同じ方位の水平へ寝かせる（パネルは壁パネルへ、重なるものは撤去）"
```

---

### Task 3: unityプレイ録画テストで設置・回転・クリーンルームを通しで確かめる

向きの列挙と回転表を変えたので、ブロックの見た目・設置・回転のランタイム挙動に触れる。

**Files:**
- 既存シナリオのみ（変更が要れば Modify）: `.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/cleanroom-room-usability.cs`、`.agents/skills/unity-playmode-recorded-playtest/scenarios/misc/sprint/sprint-toggle-mode.cs`（#1581 の PR 本文にある `shift-r-and-delete-result-clear` はどのブランチにもコミットされていないので使わない）

- [ ] **Step 1:** unity-playmode-recorded-playtest スキルを読み、`scripts/run-scenario.sh` で `cleanroom-room-usability` と `sprint-toggle-mode` を実行する（後者はベルト設置中の回転キーが水平回転になることを見る段を含む）。Expected: 両方の全アサーション成功、result.json に失敗 0
- [ ] **Step 2:** 録画（mp4）とスクショは worktree の gitignore 下に置いたままにし、PR の報告ページの素材にする
- [ ] **Step 3:** シナリオを直した場合だけコミット: `git commit -m "録画シナリオを4向きの列挙に合わせる"`

---

### Task 4: 縦向き起因の bd を閉じる

- [ ] **Step 1:** `bd close moorestech-vd9u --reason "BlockDirection から Up*/Down* を削除し、旧セーブの縦置きは版10移行で水平へ寝かせるため、外接枠の switch に縦の値が来なくなった（feat/remove-vertical-block-direction）"`
- [ ] **Step 2:** `bd close moorestech-dou2 --reason "縦向きが列挙から消え、AnchorRelativeDirectionUtil の『12向きで表せない組』の失敗経路ごと削除したため、最寄りアンカーでガイドが消える経路が無くなった（feat/remove-vertical-block-direction）"`

---

### Task 5: moores-code-review で全ブランチレビュー

- [ ] 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー指摘の反映がソースの判定経路・条件式に触れたら、Task 3 の unityプレイ録画テストを反映後のバイナリで再実施する

### Task 6: PR 作成とセッション終了可能状態

- [ ] セッション終了可能状態にすること: pr-createスキルでPRを作成し、masterとのコンフリクトがあればmasterをマージして解消・コンパイル確認のうえpushする（解消の実作業はpr-create経由でopus subagentに委譲される）。全作業がコミット・push済みで、このセッションをそのまま閉じてもPRがマージ可能な状態になっていることを確認して終える
- PR の base は `fix/placement-vertical-rotation-delete-feedback`（#1581）。#1581 がマージ済みなら base は master。壁パネル `de157d57…` は moorestech_master#97（a00d3b62）にだけあるので、本 PR のマージは #1581 と moorestech_master#97 の後になることを PR 本文に書く
- PR 本文の先頭行は `# https://review.moores.tech/pr/<PR番号>/report/`（公開手順は `~/hermes-agent/data/services/pr-review/publish/README.md`）
- PR を出したら `moores-wt rm remove-vertical-block-direction --prune-branch` で worktree と Editor を畳む

---

## 判断記録（ADR）

- 設計 ADR: `docs/adr/0095-remove-vertical-block-direction-with-save-migration.md`、裁定: `.decisions/2026-11-03-縦向きは列挙から消し重なる縦置きはマイグレーションで撤去する.md`
- D1 番号は North=0..West=3 に振り直す。出所: agent前提（整数で保存しているので欠番を残しても全向きの読み替えは要り、欠番を残す理由が無い）
- D2 パネル→壁パネルの向きは `旧 % 4`（同じ方位）。出所: agent前提（CleanRoomPanel の板はローカル +Y 上端、CleanRoomWallPanel の板はローカル -Z 面。`GetRotation` の Euler(∓90,0,z) で Up/Down の各向きの板の面を計算すると、どちらも同じ方位の壁パネルの板の面と一致する。#1581 の録画シナリオの置き換え UpWest→West・DownEast→East・UpSouth→South とも一致）
- D3 重なる縦置き同士は instanceId の小さい方（ブループリントは添字の小さい方）を残す。出所: agent前提（どちらを残しても「1つ残り1つ撤去」で下流の形は同じ。実例はほぼ無い）
- D4 撤去の巻き添えはレール節・列車節だけ当て、マスタ欠損除去器から原因中立の `RemovedBlockRailCollateral` を切り出して両方から使う。出所: agent前提（前例 `MissingMasterPruner` がブロック撤去の巻き添えとして扱うのがこの2節。電線は `ElectricWireConnectionRestorer.cs:56-75`、歯車チェーンは `GearChainConnectionRestorer.cs:48-63` が相手の無い BlockInstanceId を警告付きで読み飛ばし、cleanRoomRooms は `CleanRoomSaveRestore.cs:22-38` がセル照合で復元するので欠けに耐える＝設計検査 subagent が確認。除去器を丸ごと呼ぶとマスタ欠損の列車除去が退避・報告から漏れるため切り出す＝配置検査の違反1件の修正）
- D5 マイグレーションはサイズを `MasterHolder.BlockMaster` から引く。**新規パターン**: マスタを参照する移行ステップはこれが初（Migration 配下に前例0件。`WorldSectionPruner.cs:49` は除去器で役割が違う）。連鎖は `SaveLoadPreparer.cs:36` でマスタを使う除去器と同じ準備処理の中で走るので動く。帰結としてステップの単体テストに DI 初期化が要る（Step 6 で対応）。出所: agent前提（寝かせた占有範囲はブロックのサイズ無しに計算できない）
- D9 重なり撤去の `Debug.LogWarning` は部品の中で出す（既存部品は要約文字列だけ返し `RunParts` が `Debug.Log` でまとめる）。出所: agent前提（データ損失なので Log でなく Warning にする。要約にも件数は載る）
- D10 型検査の弱い当たり3件は拾わない: ①`TryRead` の失敗理由（無い・整数でない・範囲外）は Reject のログに生の direction 値が出るので読める／②Up と Down を `wasVertical` 1つに畳む（下流は区別しない）／③東西の xz 入れ替えの二重は「移行は現行型を参照しない」規約による意図的な写し。出所: シミュレーター予測（確信高）→前提宣言（拒否権つき）。ユーザーの承認は未取得
- 本PR外のリファクタ提案（第3バケツ・plan には足さない）: `IWorldBlockDatastore.IsOverlapExistingBlock`（`BlockPositionInfo.IsOverlap` の外接箱判定）と新設 `LaidFlatOccupancy.Overlaps`（セル集合）が同じ「占有の重なり判定」を別表現で持つ。移行は現行の型を参照しない制約で意図的に分けている。PR 本文の「本PR外のリファクタ提案」節に載せる
- D6 Codex 独立質問の消し込み段（grill §1.7）は Codex の利用枠切れで省略。出所: ユーザー裁定 2026-11-03 質問「Codex の利用枠切れで失敗しました。どうしますか？」→ 選択「省いて進める（推奨）」
- D8 `VanillaSchema/challenges.yml` の `blockDirection` enum から Up*/Down* を消す（optional 化や既定値での吸収はしない）。出所: agent前提（R1 の列挙と揃える。マスタ JSON に縦の値が0件なので一括更新は不要）
- D11 user-simulator review の Warning 3件（BlockVerticalDirection を残す明記・ForCurrentVersion の書き添え・マージ順の明記）と適用推奨1件（架空の録画シナリオ名を sprint-toggle-mode へ差し替え）を適用。出所: agent前提（シミュレーター予測の適用）
- D7 ランタイム挙動に触れるので unityプレイ録画テスト（Task 3）を入れる。出所: agent前提（moores-writing-plans の必須規定）
