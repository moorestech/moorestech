# 電線接続判定の範囲ボックス相互判定化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 電線の接続可否判定をユークリッド距離(MaxWireLength)から「双方の接続範囲ボックスが相手の占有AABBと重なる相互判定」へ全面移行し、クライアントの接続範囲表示を廃止してプレビュー線表示に一本化する。

**Architecture:** 判定の唯一のエントリポイントを `ElectricConnectionRangeService.IsMutuallyConnectable`（純粋関数、Server.Protocol配下・クライアントとソース共有）に集約する。候補探索は「範囲内セル走査」から「ワールド全コネクタ列挙＋相互AABB判定」へ転換（高圧電柱の300×300×100=900万セル走査問題も解消）。電線アイテムの消費コストのみユークリッド距離比例を維持する（ユーザー裁定済み）。機械側にはスキーマ新パラメータ `connectionRange`/`connectionHeightRange` を追加し、`maxWireLength` は全10箇所から完全削除する。

**Tech Stack:** Unity / C# / Mooresmaster SourceGenerator（YAMLスキーマ→自動生成）/ uloop（コンパイル・テスト実行）/ NUnit / moorestech_web（React+zod契約）

## Global Constraints（AGENTS.md・レビュー規約より。全タスクに適用）

- コード変更後は必ず `uloop compile --project-path ./moorestech_client` を実行（サーバーコードもクライアントプロジェクトでコンパイルされる）
- 「Unity is reloading (Domain Reload in progress)」エラー時は45秒待ってリトライ
- 1ファイル200行以下・partial絶対禁止・try-catch原則禁止・デフォルト引数禁止
- 主要処理に日本語/英語2行セットコメント（各1行、3〜10行ごと）
- `#region Internal` はメソッド内ローカル関数まとめ専用。クラス直下のprivateメソッド囲いは禁止
- イベントはUniRx（`Subject<T>`+`IObservable<T>`）。C# `event Action` 禁止
- スキーマ(yml)編集時は **edit-schema スキルを必ず読む**。`Mooresmaster.Model.*` の手動編集禁止
- スキーマ新フィールドは必須（optional禁止）+ 全JSONマスタ一括更新（forUnitTest / EditModeInPlayingTestMod / ../moorestech_master）。`?? Default` フォールバック禁止
- .metaファイル手動作成禁止。シーン/PrefabのYAML直接編集禁止（uloop execute-dynamic-code 経由のみ可）
- git worktree運用のため作業開始時に `pwd` 確認。各タスク末で必ずコミット
- コミットメッセージ末尾: `Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>` と `Claude-Session: https://claude.ai/code/session_015zj4NW9GTmHNbNtqXgk2TC`

## 確定済みのユーザー裁定

1. 接続可否＝範囲ボックス相互判定のみ。ユークリッド距離判定は全廃
2. 電線アイテム消費コストは距離比例（`ConnectToolCostCalculator`: distance/LengthPerUnit切り上げ）を**維持**
3. 接続範囲の箱表示（DisplayEnergizedRange）は**廃止**。設置/手動配線プレビューの「どこに繋がるか」線表示に一本化
4. 実マスタの機械側初期値: `connectionRange=30`, `connectionHeightRange=20`

## 機能パリティ死活表（spec-architecture-review Phase 2.5）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| ブロック設置時の自動接続（電柱→最寄り電柱+未接続機械 / 機械→最寄り電柱） | 生存 | 判定方式のみ変更、選定ルール（最寄り1本等）は不変 |
| 設置ゴースト時の接続先プレビュー線+コスト表示 | 生存 | ElectricWireAutoConnectPreview は無変更、Collector内部のみ差し替え |
| 電線ツールでの手動接続/切断/レール式延長 | 生存 | ElectricWireConnectSystem/Modes は判定呼び出しのみ追従 |
| 設置モード中の電柱範囲の箱表示（DisplayEnergizedRange） | **廃止** | ユーザー裁定済み（本リファクタの明示要求） |
| Web UI HUDの「Energized Range」表示行（placement_modeトピック） | **廃止** | 上記の従属機能。契約フィールド `energizedRangeVisible` ごと削除（ユーザー裁定の範囲内） |
| 既存セーブの接続維持 | 生存 | WireConnections は距離非依存・ロード時再検証なし（現行仕様維持） |

## 配置と前例（spec-architecture-review 済み）

| 新規/変更 | 配置先 | 前例 |
|---|---|---|
| `ConnectionRangeProfile` / `IsMutuallyConnectable` | `Server.Protocol/.../ElectricWire/ConnectionRange/` | 同フォルダの `ElectricConnectionRangeService`（既存のAABB判定の置き場所） |
| 範囲ボックス＝占有AABBを低側floor(range/2)/高側(range-1-floor(range/2))膨張 | 同上 | 既存 `EnumerateCandidatePolePositions`（占有セルごとの範囲列挙の合併と数学的に一致）・`IsWithinMachineRange` |
| 全コネクタ列挙 | `IWorldBlockDatastore.BlockMasterDictionary`（既存API）を読むだけ | `WorldBlockData.BlockPositionInfo` が MinPos/MaxPos を保持 |
| resolver拡張（CleanRoom 2種追加） | `ElectricWireBlockParamResolver`（既存の集約switch） | 既存7ケースと同形 |
| スキーマ新フィールド必須化+全JSON更新 | blocks.yml + 3マスタJSON | PR978（idlePowerRate。optional+フォールバックが全面差し戻しされた前例） |
| enum `TooFar`→`OutOfRange` 同位置リネーム | `ElectricWirePlacementJudgement.cs` | MessagePackはint運搬・クライアント同時ビルドのため安全 |

新規パターン（前例なし・レビュー注目点）: 「接続可否から距離を排除しつつコストのみ距離比例を残す」ため、`EvaluateWireConnection` の `distance` 引数がコスト計算専用になる。シグネチャのXMLコメントで明記する。

---

## ファイル構造

**新規:**
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/ConnectionRange/ConnectionRangeProfile.cs` — 相手種別ごとの範囲寸法を持つ純粋struct
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Server/ElectricConnectionRangeServiceTest.cs` — 相互判定の純粋単体テスト

**削除:**
- `.../ConnectionRange/MaxElectricPoleMachineConnectionRange.cs`（列挙方式で不要化）
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Electric/DisplayEnergizedRange.cs` + `EnergizedRangeObject.cs`（.metaはUnityに任せる。ファイル削除後のUnity起動で自動消滅）

**主変更（サーバー、クライアントとソース共有）:** `ElectricConnectionRangeService.cs`（刷新）/ `ElectricWireBlockParamResolver.cs` / `ElectricWireAutoConnectTargetCollector.cs`（列挙方式化）/ `ElectricWireAutoConnectService.cs` / `ElectricWirePlacementEvaluator.cs` / `ElectricWirePlacementJudgement.cs` / `ElectricWireSystemUtil.cs` / `ElectricWireExtendService.cs` / `IElectricWireConnector.cs` / `ElectricWireConnectorComponent.cs` / BlockTemplate 9ファイル / `MoorestechServerDIContainerGenerator.cs`

**主変更（クライアント）:** `ClientElectricWireAutoConnectCollector.cs` / `ElectricWireExtendPreviewCalculator.cs` / `ElectricWireExtendMode.cs` / `MainGameStarter.cs` / `WebUiGameBinder.cs` / `PlacementModeTopic.cs`

**スキーマ・マスタ:** `VanillaSchema/blocks.yml` / `Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json` / `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json` / `/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`（別リポジトリ）

**Web（別リポジトリ moorestech_web）:** `webui/src/bridge/contract/schemas/ui.ts` / `webui/src/features/modeHud/PlacementModeHud.tsx` / e2eフィクスチャ / 契約テスト / dist リビルド→`moorestech_client/Assets/StreamingAssets/WebUi/dist`

## 判定仕様（全タスク共通の正）

- 範囲ボックス: 自ブロックの占有AABB(`MinPos`〜`MaxPos`)を、水平は低側 `floor(r/2)`・高側 `r-1-floor(r/2)`、高さも同様に膨張した閉区間AABB。`r`,`h` は最低1にクランプ
- 接続可 ⇔ 「Aのボックス ∩ Bの占有AABB ≠ ∅」**かつ**「Bのボックス ∩ Aの占有AABB ≠ ∅」
- 使用ボックスの選択: 電柱は相手が電柱なら `poleConnectionRange/poleConnectionHeightRange`、機械なら `machineConnectionRange/machineConnectionHeightRange`。機械は常に `connectionRange/connectionHeightRange`
- 「電柱」判定: マスタ上は `BlockParam is ElectricPoleBlockParam`、実行時コネクタは `EnergyRole is IElectricTransformer`（両者は常に一致する）
- 「最寄り」の順序付け（自動接続の1本選定）は従来通り `Vector3Int.Distance`（原点座標同士）。これは判定でなく順序付けなので距離使用可
- コスト計算は従来通り `Vector3Int.Distance(原点A, 原点B)` を `TryCalculateWireCost` に渡す

テストマスタ（forUnitTest）の寸法（Task 1 で設定する値。テスト座標設計の基準）:

| ブロック | 水平範囲 | 高さ範囲 | 1x1同士の接続限界（軸方向） |
|---|---|---|---|
| TestElectricPole 電柱↔電柱 | poleConnectionRange=7 (±3) | poleConnectionHeightRange=5 (±2) | X/Z差3まで、Y差2まで |
| TestElectricPole 電柱→機械 | machineConnectionRange=5 (±2) | machineConnectionHeightRange=5 (±2) | X/Z差2まで |
| 全機械 → 相手 | connectionRange=9 (±4) | connectionHeightRange=9 (±4) | X/Z差4まで |

電柱↔機械の実効限界 = 両者のAND = X/Z差2・Y差2（電柱側が律速）。

---

### Task 1: スキーマに機械側 connectionRange/connectionHeightRange を追加し全JSONを更新

**Files:**
- Modify: `VanillaSchema/blocks.yml`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json`

**Interfaces:**
- Produces: 自動生成 `ElectricMachineBlockParam.ConnectionRange` / `.ConnectionHeightRange`（int）が8機械パラメータ型すべてに生える。`maxWireLength` はこのタスクでは**まだ削除しない**（Task 8で削除。コンパイルを常に通すため追加が先）

- [ ] **Step 1: edit-schema スキルを読む**

Skillツールで `edit-schema` を起動し、再生成手順（csc.rsp / _CompileRequester）を確認する。

- [ ] **Step 2: blocks.yml の8機械when句に新キーを追加**

対象when句: `ElectricMachine` / `ElectricGenerator` / `ElectricMiner` / `ElectricPump` / `GearToElectricGenerator` / `ElectricToGearGenerator` / `CleanRoomAirFilter` / `CleanRoomMachine`（`grep -n "key: maxWireLength" VanillaSchema/blocks.yml` で9箇所ヒットし、うち `ElectricPole` 以外の8箇所が対象）。各句の `maxWireLength` の直後に追加:

```yaml
        - key: connectionRange
          type: integer
          default: 30
        - key: connectionHeightRange
          type: integer
          default: 20
```

- [ ] **Step 3: forUnitTest の blocks.json を更新**

`maxWireLength` を持つ18エントリのうち **ElectricPole 2件（TestElectricPole / TestLockedElectricPole）以外の16件**の `blockParam` に `"connectionRange": 9, "connectionHeightRange": 9` を追加する（値の根拠は冒頭の寸法表）。対象: TestElectricMachine / TestElectricGenerator / TestElectricMiner / TestInfinityElectricGenerator / MultiBlock1〜3 / MachineRecipeTest1〜3 / FluidMachine / ElectricPump / TestGearToElectricGenerator / TestElectricToGearGenerator / TestCleanRoomAirFilter / TestCleanRoomMachine。jqやPythonで機械的に適用してよい。

- [ ] **Step 4: EditModeInPlayingTestMod の blocks.json を更新**

`maxWireLength` を持つ3エントリ（キャンプファイア / 釜 / TestElectricToGearGeneratorUI）に `"connectionRange": 30, "connectionHeightRange": 20` を追加。

- [ ] **Step 5: 再生成+コンパイル**

edit-schemaスキルの手順でSourceGeneratorを再生成し、`uloop compile --project-path ./moorestech_client` を実行。
Expected: エラー0。`Mooresmaster.Model.BlocksModule.ElectricMachineBlockParam` に `ConnectionRange` プロパティが存在する（`grep -rn "ConnectionRange" 生成物ディレクトリ` で確認）。

- [ ] **Step 6: Commit**

```bash
git add VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Tests.Module moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat: 機械系ブロックにconnectionRange/connectionHeightRangeスキーマを追加"
```

---

### Task 2: ConnectionRangeProfile と IsMutuallyConnectable（TDD）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/ConnectionRange/ConnectionRangeProfile.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/ConnectionRange/ElectricConnectionRangeService.cs`（既存メソッドは残したまま追記。旧メソッド削除はTask 8）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Server/ElectricConnectionRangeServiceTest.cs`

**Interfaces:**
- Produces:
  - `readonly struct ConnectionRangeProfile { ctor(int,int,int,int); static CreatePole(ElectricPoleBlockParam); static CreateUniform(int connectionRange, int connectionHeightRange); (int Horizontal, int Height) GetRangeAgainst(bool targetIsPole); }`
  - `static bool ElectricConnectionRangeService.IsMutuallyConnectable(BlockPositionInfo aInfo, ConnectionRangeProfile aProfile, bool aIsPole, BlockPositionInfo bInfo, ConnectionRangeProfile bProfile, bool bIsPole)`
  - `static bool ElectricConnectionRangeService.Covers(BlockPositionInfo self, (int Horizontal, int Height) range, BlockPositionInfo target)`

- [ ] **Step 1: 失敗するテストを書く**

`ElectricConnectionRangeServiceTest.cs` を新規作成（純粋関数のためサーバーコンテキスト不要）:

```csharp
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;

namespace Tests.UnitTest.Server
{
    /// <summary>
    /// 範囲ボックス相互判定の純粋単体テスト。ワールド状態には依存しない
    /// Pure unit tests for the mutual range-box judgement; no world state involved
    /// </summary>
    public class ElectricConnectionRangeServiceTest
    {
        // 1x1x1ブロックのBlockPositionInfoを作る
        // Build a BlockPositionInfo for a 1x1x1 block
        private static BlockPositionInfo Cell(int x, int y, int z)
        {
            return new BlockPositionInfo(new Vector3Int(x, y, z), BlockDirection.North, Vector3Int.one);
        }

        [Test]
        public void 双方の範囲内なら接続可能()
        {
            // 水平7(±3)の電柱同士がX差3で相互に届く
            // Poles with horizontal 7 (±3) reach each other at X distance 3
            var profile = ConnectionRangeProfile.CreateUniform(7, 5);
            var result = ElectricConnectionRangeService.IsMutuallyConnectable(
                Cell(0, 0, 0), profile, true,
                Cell(3, 0, 0), profile, true);
            Assert.IsTrue(result);
        }

        [Test]
        public void 範囲境界の外なら接続不可()
        {
            // X差4は水平7(±3)の外
            // X distance 4 is outside horizontal 7 (±3)
            var profile = ConnectionRangeProfile.CreateUniform(7, 5);
            var result = ElectricConnectionRangeService.IsMutuallyConnectable(
                Cell(0, 0, 0), profile, true,
                Cell(4, 0, 0), profile, true);
            Assert.IsFalse(result);
        }

        [Test]
        public void 片側だけ届く非対称構成は接続不可()
        {
            // Aは広い(±4)がBは狭い(±1)。相互判定なので不可
            // A reaches (±4) but B does not (±1); mutual judgement fails
            var wide = ConnectionRangeProfile.CreateUniform(9, 9);
            var narrow = ConnectionRangeProfile.CreateUniform(3, 3);
            var result = ElectricConnectionRangeService.IsMutuallyConnectable(
                Cell(0, 0, 0), wide, false,
                Cell(3, 0, 0), narrow, false);
            Assert.IsFalse(result);
        }

        [Test]
        public void 高さ範囲も独立に判定される()
        {
            // 水平は届くがY差3が高さ5(±2)の外
            // Horizontal reaches but Y distance 3 exceeds height 5 (±2)
            var profile = ConnectionRangeProfile.CreateUniform(7, 5);
            var result = ElectricConnectionRangeService.IsMutuallyConnectable(
                Cell(0, 0, 0), profile, true,
                Cell(0, 3, 0), profile, true);
            Assert.IsFalse(result);
        }

        [Test]
        public void 電柱は相手種別で使用ボックスが切り替わる()
        {
            // 対電柱7(±3)・対機械5(±2)の電柱と、X差3の機械。電柱側の対機械ボックスが届かず不可
            // Pole with pole-range 7 (±3) and machine-range 5 (±2) versus a machine at X distance 3
            var pole = new ConnectionRangeProfile(7, 5, 5, 5);
            var machine = ConnectionRangeProfile.CreateUniform(9, 9);
            var result = ElectricConnectionRangeService.IsMutuallyConnectable(
                Cell(0, 0, 0), pole, true,
                Cell(3, 0, 0), machine, false);
            Assert.IsFalse(result);

            // 同じX差3でも相手が電柱なら対電柱ボックス(±3)で届く
            // The same X distance 3 connects when the target is a pole (±3 box)
            var result2 = ElectricConnectionRangeService.IsMutuallyConnectable(
                Cell(0, 0, 0), pole, true,
                Cell(3, 0, 0), new ConnectionRangeProfile(7, 5, 5, 5), true);
            Assert.IsTrue(result2);
        }

        [Test]
        public void マルチブロックは占有AABB全体で判定される()
        {
            // 3x1x1の機械の遠端セル(x=5)に±2の電柱ボックスが重なる
            // The pole's ±2 box overlaps the far cell (x=5) of a 3-wide machine
            var pole = new ConnectionRangeProfile(7, 5, 5, 5);
            var machine = ConnectionRangeProfile.CreateUniform(9, 9);
            var machineInfo = new BlockPositionInfo(new Vector3Int(5, 0, 0), BlockDirection.North, new Vector3Int(3, 1, 1));
            var result = ElectricConnectionRangeService.IsMutuallyConnectable(
                Cell(3, 0, 0), pole, true,
                machineInfo, machine, false);
            Assert.IsTrue(result);
        }

        [Test]
        public void 範囲0はクランプされ自セルのみ判定になる()
        {
            // 範囲0でも最低1にクランプされ、同一セル重なりのみ許容
            // Range 0 clamps to 1, allowing only same-cell overlap
            var zero = ConnectionRangeProfile.CreateUniform(0, 0);
            Assert.IsTrue(ElectricConnectionRangeService.Covers(Cell(0, 0, 0), (0, 0), Cell(0, 0, 0)));
            Assert.IsFalse(ElectricConnectionRangeService.IsMutuallyConnectable(
                Cell(0, 0, 0), zero, false, Cell(1, 0, 0), zero, false));
        }
    }
}
```

- [ ] **Step 2: テストが失敗（コンパイルエラー）することを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ConnectionRangeProfile` 未定義でコンパイルエラー。

- [ ] **Step 3: ConnectionRangeProfile.cs を実装**

```csharp
using Mooresmaster.Model.BlocksModule;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange
{
    /// <summary>
    /// 相手種別（電柱/機械）ごとの接続範囲ボックス寸法
    /// Connection range box sizes per target kind (pole / machine)
    /// </summary>
    public readonly struct ConnectionRangeProfile
    {
        public readonly int HorizontalAgainstPole;
        public readonly int HeightAgainstPole;
        public readonly int HorizontalAgainstMachine;
        public readonly int HeightAgainstMachine;

        public ConnectionRangeProfile(int horizontalAgainstPole, int heightAgainstPole, int horizontalAgainstMachine, int heightAgainstMachine)
        {
            HorizontalAgainstPole = horizontalAgainstPole;
            HeightAgainstPole = heightAgainstPole;
            HorizontalAgainstMachine = horizontalAgainstMachine;
            HeightAgainstMachine = heightAgainstMachine;
        }

        // 電柱: 対電柱と対機械で別ボックスを持つ
        // Pole: separate boxes against poles and against machines
        public static ConnectionRangeProfile CreatePole(ElectricPoleBlockParam param)
        {
            return new ConnectionRangeProfile(param.PoleConnectionRange, param.PoleConnectionHeightRange, param.MachineConnectionRange, param.MachineConnectionHeightRange);
        }

        // 機械: 相手種別によらず単一ボックス
        // Machine: a single box regardless of target kind
        public static ConnectionRangeProfile CreateUniform(int connectionRange, int connectionHeightRange)
        {
            return new ConnectionRangeProfile(connectionRange, connectionHeightRange, connectionRange, connectionHeightRange);
        }

        public (int Horizontal, int Height) GetRangeAgainst(bool targetIsPole)
        {
            return targetIsPole ? (HorizontalAgainstPole, HeightAgainstPole) : (HorizontalAgainstMachine, HeightAgainstMachine);
        }
    }
}
```

- [ ] **Step 4: ElectricConnectionRangeService に相互判定を追記**

既存メソッドの上（クラス先頭）に追加（既存の `EnumeratePoleRange` 等はTask 8まで残す。一時的に200行を超えるのは移行中のみ許容し、Task 8の削除で200行以下に戻す）:

```csharp
        /// <summary>
        /// 双方の範囲ボックスが相手の占有AABBと重なる場合のみ接続可とする相互判定
        /// Mutual judgement: connectable only when both range boxes overlap the partner's occupied AABB
        /// </summary>
        public static bool IsMutuallyConnectable(
            BlockPositionInfo aInfo, ConnectionRangeProfile aProfile, bool aIsPole,
            BlockPositionInfo bInfo, ConnectionRangeProfile bProfile, bool bIsPole)
        {
            return Covers(aInfo, aProfile.GetRangeAgainst(bIsPole), bInfo) &&
                   Covers(bInfo, bProfile.GetRangeAgainst(aIsPole), aInfo);
        }

        public static bool Covers(BlockPositionInfo self, (int Horizontal, int Height) range, BlockPositionInfo target)
        {
            var (rangeMin, rangeMax) = CreateBounds();
            return HasOverlap();

            #region Internal

            (Vector3Int min, Vector3Int max) CreateBounds()
            {
                // 占有AABBを低側floor(r/2)・高側r-1-floor(r/2)だけ膨張させる（従来のセル列挙の合併と一致）
                // Inflate the occupied AABB by floor(r/2) low and r-1-floor(r/2) high (matches the union of legacy cell enumeration)
                var horizontal = Mathf.Max(range.Horizontal, 1);
                var height = Mathf.Max(range.Height, 1);
                var lowHorizontal = horizontal / 2;
                var highHorizontal = horizontal - 1 - lowHorizontal;
                var lowHeight = height / 2;
                var highHeight = height - 1 - lowHeight;

                var min = new Vector3Int(self.MinPos.x - lowHorizontal, self.MinPos.y - lowHeight, self.MinPos.z - lowHorizontal);
                var max = new Vector3Int(self.MaxPos.x + highHorizontal, self.MaxPos.y + highHeight, self.MaxPos.z + highHorizontal);
                return (min, max);
            }

            bool HasOverlap()
            {
                return target.MinPos.x <= rangeMax.x && rangeMin.x <= target.MaxPos.x &&
                       target.MinPos.y <= rangeMax.y && rangeMin.y <= target.MaxPos.y &&
                       target.MinPos.z <= rangeMax.z && rangeMin.z <= target.MaxPos.z;
            }

            #endregion
        }
```

- [ ] **Step 5: コンパイル+テスト実行**

```
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricConnectionRangeServiceTest"
```
Expected: 全7テストPASS。

- [ ] **Step 6: Commit**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol moorestech_server/Assets/Scripts/Tests
git commit -m "feat: 電線接続の範囲ボックス相互判定IsMutuallyConnectableを追加"
```

---

### Task 3: ElectricWireBlockParamResolver に範囲プロファイル解決を追加（CleanRoom対応込み）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireBlockParamResolver.cs`

**Interfaces:**
- Consumes: Task 2 の `ConnectionRangeProfile`
- Produces: `static bool TryGetWireRangeParam(IBlockParam blockParam, out int maxWireConnectionCount, out ConnectionRangeProfile rangeProfile, out bool isPole)`。旧 `TryGetWireParam` はTask 8まで併存

- [ ] **Step 1: 新メソッドを追加**

既存 `TryGetWireParam` の下に追加（using に `Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange` を追加）:

```csharp
        /// <summary>
        /// 電気系ブロックのパラメータから接続数上限・範囲プロファイル・電柱かどうかを取り出す
        /// Extract connection limit, range profile and pole-ness from an electric block param
        /// </summary>
        public static bool TryGetWireRangeParam(IBlockParam blockParam, out int maxWireConnectionCount, out ConnectionRangeProfile rangeProfile, out bool isPole)
        {
            switch (blockParam)
            {
                case ElectricPoleBlockParam pole:
                    maxWireConnectionCount = pole.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreatePole(pole);
                    isPole = true;
                    return true;
                case ElectricMachineBlockParam machine:
                    maxWireConnectionCount = machine.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(machine.ConnectionRange, machine.ConnectionHeightRange);
                    isPole = false;
                    return true;
                case ElectricGeneratorBlockParam generator:
                    maxWireConnectionCount = generator.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(generator.ConnectionRange, generator.ConnectionHeightRange);
                    isPole = false;
                    return true;
                case ElectricMinerBlockParam miner:
                    maxWireConnectionCount = miner.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(miner.ConnectionRange, miner.ConnectionHeightRange);
                    isPole = false;
                    return true;
                case ElectricPumpBlockParam pump:
                    maxWireConnectionCount = pump.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(pump.ConnectionRange, pump.ConnectionHeightRange);
                    isPole = false;
                    return true;
                case GearToElectricGeneratorBlockParam gearToElectric:
                    maxWireConnectionCount = gearToElectric.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(gearToElectric.ConnectionRange, gearToElectric.ConnectionHeightRange);
                    isPole = false;
                    return true;
                case ElectricToGearGeneratorBlockParam electricToGear:
                    maxWireConnectionCount = electricToGear.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(electricToGear.ConnectionRange, electricToGear.ConnectionHeightRange);
                    isPole = false;
                    return true;
                case CleanRoomAirFilterBlockParam airFilter:
                    maxWireConnectionCount = airFilter.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(airFilter.ConnectionRange, airFilter.ConnectionHeightRange);
                    isPole = false;
                    return true;
                case CleanRoomMachineBlockParam cleanRoomMachine:
                    maxWireConnectionCount = cleanRoomMachine.MaxWireConnectionCount;
                    rangeProfile = ConnectionRangeProfile.CreateUniform(cleanRoomMachine.ConnectionRange, cleanRoomMachine.ConnectionHeightRange);
                    isPole = false;
                    return true;
                default:
                    // 電気系以外のブロックパラメータには対応しない
                    // Not an electric block param
                    maxWireConnectionCount = 0;
                    rangeProfile = default;
                    isPole = false;
                    return false;
            }
        }
```

注意: CleanRoom 2種は旧 `TryGetWireParam` に**入っていなかった**（既存不整合）。新メソッドで対応することで、CleanRoom機械が初めて自動接続対象になる（意図的挙動変更・Task 9のテストでカバー）。ファイルが200行を超える場合はswitchのケース本体を共通ローカル関数化せず、旧メソッド削除（Task 8）で解消する。

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0（`CleanRoomAirFilterBlockParam` 等の型名が生成物と一致しない場合は生成物を `grep` して正しい型名に合わせる）。

- [ ] **Step 3: Commit**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol
git commit -m "feat: resolverに範囲プロファイル解決を追加しCleanRoom2種に対応"
```

---

### Task 4: サーバー自動接続の収集を全コネクタ列挙+相互判定へ転換

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireAutoConnectTargetCollector.cs`（全面書き換え）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/AutoConnect/ElectricWireAutoConnectService.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/ElectricWireExtendService.cs`（CollectPoleMachineTargets呼び出し行のみ）

**Interfaces:**
- Consumes: `IsMutuallyConnectable` / `TryGetWireRangeParam` / `IWorldBlockDatastore.BlockMasterDictionary`（`IReadOnlyDictionary<BlockInstanceId, WorldBlockData>`、`WorldBlockData.Block`/`.BlockPositionInfo`）
- Produces（シグネチャ変更、戻り値型は従来同一）:
  - `CollectPoleTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo)`
  - `CollectPoleMachineTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo, int usedCount)`
  - `CollectMachineTargets(BlockMasterElement blockMaster, BlockPositionInfo ownInfo)`

- [ ] **Step 1: ElectricWireAutoConnectTargetCollector.cs を全面書き換え**

```csharp
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Context;
using Game.EnergySystem;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;

namespace Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect
{
    /// <summary>
    /// 設置位置の周辺から自動接続対象の候補を収集する。電柱設置と機械設置で選定ルールが異なる
    /// Collects auto-connect target candidates around a placement position; rules differ for pole vs machine
    /// 判定はワールド全コネクタ列挙＋範囲ボックス相互判定。距離は最寄り順序付けとコスト計算にのみ使う
    /// Judged by enumerating all world connectors with mutual range boxes; distance is used only for ordering and cost
    /// </summary>
    public static class ElectricWireAutoConnectTargetCollector
    {
        public static List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectPoleTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo)
        {
            var results = new List<(BlockInstanceId, IElectricWireConnector, float)>();
            var usedCount = 0;
            var ownProfile = ConnectionRangeProfile.CreatePole(ownParam);

            // ①相互範囲内で接続可能な最寄り電柱1本
            // Nearest mutually-in-range connectable pole
            var nearestPole = EnumerateConnectableCandidates(ownInfo, ownProfile, true)
                .Where(c => c.Connector.EnergyRole is IElectricTransformer)
                .Where(c => !c.Connector.IsWireConnectionFull)
                .OrderBy(c => c.Distance).ThenBy(c => c.Connector.BlockInstanceId.AsPrimitive())
                .FirstOrDefault();

            if (nearestPole.Connector != null && usedCount < ownParam.MaxWireConnectionCount)
            {
                results.Add((nearestPole.Connector.BlockInstanceId, nearestPole.Connector, nearestPole.Distance));
                usedCount++;
            }

            // ②相互範囲内の未接続機械/発電機を残数まで収集
            // Collect unconnected machines/generators mutually in range up to remaining capacity
            results.AddRange(CollectPoleMachineTargets(ownParam, ownInfo, usedCount));

            return results;
        }

        // レール式延長で使う。起点との明示接続分を差し引いた残り本数で機械のみを収集する
        // Used by rail-style extend; collects machines only, given the capacity already spent on the explicit origin wire
        public static List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectPoleMachineTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo, int usedCount)
        {
            var results = new List<(BlockInstanceId, IElectricWireConnector, float)>();
            var ownProfile = ConnectionRangeProfile.CreatePole(ownParam);

            // 相互範囲内の未接続機械/発電機を近い順に残数まで
            // Unconnected machines/generators mutually in range, nearest first, up to remaining capacity
            var machineCandidates = EnumerateConnectableCandidates(ownInfo, ownProfile, true)
                .Where(c => c.Connector.EnergyRole is IElectricConsumer or IElectricGenerator && c.Connector.WireConnections.Count == 0)
                .Where(c => !c.Connector.IsWireConnectionFull)
                .OrderBy(c => c.Distance).ThenBy(c => c.Connector.BlockInstanceId.AsPrimitive());

            foreach (var candidate in machineCandidates)
            {
                if (ownParam.MaxWireConnectionCount <= usedCount) break;
                results.Add((candidate.Connector.BlockInstanceId, candidate.Connector, candidate.Distance));
                usedCount++;
            }

            return results;
        }

        public static List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectMachineTargets(BlockMasterElement blockMaster, BlockPositionInfo ownInfo)
        {
            // 自身の範囲プロファイルを解決する（非電気系は対象なし）
            // Resolve own range profile (non-electric yields no targets)
            if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(blockMaster.BlockParam, out _, out var ownProfile, out var ownIsPole))
                return new List<(BlockInstanceId, IElectricWireConnector, float)>();

            // 相互範囲内で接続可能な最寄り電柱1本のみ
            // Only the nearest mutually-in-range connectable pole
            var nearestPole = EnumerateConnectableCandidates(ownInfo, ownProfile, ownIsPole)
                .Where(c => c.Connector.EnergyRole is IElectricTransformer)
                .Where(c => !c.Connector.IsWireConnectionFull)
                .OrderBy(c => c.Distance).ThenBy(c => c.Connector.BlockInstanceId.AsPrimitive())
                .FirstOrDefault();

            if (nearestPole.Connector == null) return new List<(BlockInstanceId, IElectricWireConnector, float)>();

            return new List<(BlockInstanceId, IElectricWireConnector, float)> { (nearestPole.Connector.BlockInstanceId, nearestPole.Connector, nearestPole.Distance) };
        }

        // ワールド全ブロックから、自分と相互範囲内にあるワイヤー端点を距離付きで列挙する
        // Enumerate wire endpoints mutually in range with self from all world blocks, with distances
        private static IEnumerable<(IElectricWireConnector Connector, float Distance)> EnumerateConnectableCandidates(BlockPositionInfo ownInfo, ConnectionRangeProfile ownProfile, bool ownIsPole)
        {
            var datastore = ServerContext.WorldBlockDatastore;
            foreach (var worldBlock in datastore.BlockMasterDictionary.Values)
            {
                if (!worldBlock.Block.TryGetComponent<IElectricWireConnector>(out var connector)) continue;
                if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(worldBlock.Block.BlockMasterElement.BlockParam, out _, out var targetProfile, out var targetIsPole)) continue;
                if (!ElectricConnectionRangeService.IsMutuallyConnectable(ownInfo, ownProfile, ownIsPole, worldBlock.BlockPositionInfo, targetProfile, targetIsPole)) continue;

                // 距離は原点座標同士。順序付けとコスト計算にのみ使う
                // Distance between origin cells; used only for ordering and cost
                yield return (connector, Vector3Int.Distance(ownInfo.OriginalPos, worldBlock.BlockPositionInfo.OriginalPos));
            }
        }
    }
}
```

注意: `TryGetComponent<IElectricWireConnector>` は `Game.Block.Interface.Extension` の拡張メソッド。実際の拡張メソッド名（`TryGetComponent` / `GetComponent`+`ExistsComponent`）は `ElectricWireSystemUtil.TryGetWireConnector` の実装を参照して同じ取得方法に合わせること。旧コードの `IsWireConnectionFull` 判定は `IsGeometricallyConnectable`（Evaluatorプローブ）内にあったが、上記では直接 `IsWireConnectionFull` を見る（AlreadyConnected相当の重複判定は接続実行側 `TryConnectBothSides` が防ぐ。従来もプローブは `AlreadyConnected=false` 固定だった）。

- [ ] **Step 2: ElectricWireAutoConnectService.cs を追従**

`EvaluateAutoConnect` 内（29-35行付近）を変更:

```csharp
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var ownInfo = new BlockPositionInfo(position, direction, blockMaster.BlockSize);

            // 電柱設置か機械/発電機設置かで対象選定ロジックが異なる
            // Target selection differs between pole placement and machine/generator placement
            var candidates = blockMaster.BlockParam is ElectricPoleBlockParam poleParam
                ? ElectricWireAutoConnectTargetCollector.CollectPoleTargets(poleParam, ownInfo)
                : CollectMachineTargets(blockMaster, ownInfo);
```

ローカル関数 `CollectMachineTargets`（57-65行）を差し替え:

```csharp
            List<(BlockInstanceId TargetId, IElectricWireConnector Connector, float Distance)> CollectMachineTargets(BlockMasterElement master, BlockPositionInfo info)
            {
                // 自身の接続容量が0なら探索するまでもなく対象なし
                // No point searching when this block has zero connection capacity
                if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(master.BlockParam, out var ownCapacity, out _, out _) || ownCapacity <= 0)
                    return new List<(BlockInstanceId, IElectricWireConnector, float)>();

                return ElectricWireAutoConnectTargetCollector.CollectMachineTargets(master, info);
            }
```

usingに `Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange` は不要（Profileを直接触らない）。

- [ ] **Step 3: ElectricWireExtendService.cs の CollectPoleMachineTargets 呼び出しを追従**

`ExecuteExtendWithOrigin` 内の `ElectricWireAutoConnectTargetCollector.CollectPoleMachineTargets(poleParam, polePlaceInfo.Position, 1)` を:

```csharp
                var poleGhostInfo = new BlockPositionInfo(polePlaceInfo.Position, polePlaceInfo.Direction, blockMaster.BlockSize);
                var machineTargets = ElectricWireAutoConnectTargetCollector.CollectPoleMachineTargets(poleParam, poleGhostInfo, 1);
```

`ExecuteIsolatedPlace` 側にも同collector呼び出しがあれば同様に `BlockPositionInfo` を渡す形に変更する（ファイル全体をReadして呼び出し箇所を全て追従）。この時点で79-81行の距離直接判定は**まだ残す**（Task 5で置換）。

- [ ] **Step 4: コンパイル+自動接続テスト**

```
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWireAutoConnectPlaceTest"
```
Expected: コンパイル成功。テストは旧座標が新範囲（電柱→機械 ±2）内なら PASS。範囲外座標を使うシナリオが FAIL した場合は、冒頭の寸法表に合わせて座標を再配置して PASS させる（例: 機械は電柱からX/Z差2以内・電柱同士は差3以内へ）。テストの意図（近い方が選ばれる等）は変えない。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts
git commit -m "refactor: 自動接続候補収集を全コネクタ列挙+相互範囲判定へ転換"
```

---

### Task 5: Evaluator署名変更・TooFar→OutOfRange・手動接続/延長/クライアントプレビューの相互判定化

サーバーとクライアントはソース共有のため、Evaluatorの署名変更と全呼び出し側の追従を単一タスク（単一コンパイル単位）で行う。

**Files:**
- Modify: `.../Util/ElectricWire/Placement/ElectricWirePlacementJudgement.cs`（enum `TooFar` → `OutOfRange` 同位置リネーム）
- Modify: `.../Util/ElectricWire/Placement/ElectricWirePlacementEvaluator.cs`
- Modify: `.../Util/ElectricWire/Connection/ElectricWireSystemUtil.cs`
- Modify: `.../Util/ElectricWire/ElectricWireExtendService.cs`
- Modify: `moorestech_client/.../ElectricWireConnect/Parts/ElectricWireExtendPreviewCalculator.cs`
- Modify: `moorestech_client/.../ElectricWireConnect/Modes/ElectricWireExtendMode.cs`
- Modify: `moorestech_client/.../ElectricWireConnect/Modes/ElectricWireEditMode.cs`（TryResolveWireParam呼び出しのout引数追従のみ）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Server/ElectricWirePlacementEvaluatorTest.cs` / `ElectricWireSystemUtilTest.cs`

**Interfaces:**
- Produces:
  - `EvaluateWireConnection(float distance, bool alreadyConnected, bool anyConnectionFull, Guid connectToolGuid, IEnumerable<IItemStack> inventoryItems, IReadOnlyList<ConnectToolMaterialCost> reservedMaterials)`（distanceはコスト計算専用）
  - `ElectricWirePlacementFailureReason.OutOfRange`（旧TooFarと同int値）
  - `ElectricWireExtendPreviewCalculator.TryResolveWireParam(BlockGameObject block, out int maxWireConnectionCount, out ConnectionRangeProfile rangeProfile, out bool isPole)` と `(BlockMasterElement master, ...)` オーバーロード
  - `ElectricWireExtendPreviewCalculator.Evaluate(BlockGameObject source, BlockGameObject target, int sourceMaxConnectionCount, int targetMaxConnectionCount, float distance, Guid connectToolGuid, IEnumerable<IItemStack> inventoryItems)`（範囲判定を内包）
  - `ElectricWireExtendPreviewCalculator.EvaluateNewPole(BlockGameObject source, int sourceMaxConnectionCount, ElectricPoleBlockParam poleParam, BlockPositionInfo poleGhostInfo, float distance, Guid connectToolGuid, IEnumerable<IItemStack> inventoryItems)`

- [ ] **Step 1: 先にテストを書き換える（RED確認用）**

`ElectricWirePlacementEvaluatorTest.cs`:
- テスト `距離が上限を超えるとTooFarになる` を**削除**（距離ゲートはEvaluatorから消える）
- 残り7テストの `EvaluateWireConnection(距離, 10f, 12f, ...)` 呼び出しから `10f, 12f` を除去。例:

```csharp
            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                5f, false, false, ConnectToolGuid, inventory, null);
```

`ElectricWireSystemUtilTest.cs`:
- テスト `距離が離れすぎると接続に失敗する` を範囲ベースへ書き換え+境界テストを追加:

```csharp
        [Test]
        public void 電柱の相互範囲外だと接続に失敗する()
        {
            // poleConnectionRange(7)=±3の外（X差4）に電柱を置く
            // Place poles outside poleConnectionRange 7 (±3): X distance 4
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(4, 0, 0);
            PlaceTwoPoles(posA, posB);
            GiveWire(50);

            var connected = ElectricWireSystemUtil.TryConnect(posA, posB, PlayerId, ConnectToolGuid, out var error);

            Assert.IsFalse(connected);
            Assert.AreEqual(ElectricWirePlacementFailureReason.OutOfRange, error);
        }

        [Test]
        public void 電柱の範囲境界ちょうどは接続できる()
        {
            // poleConnectionRange(7)=±3の境界（X差3）は接続可能
            // The boundary of poleConnectionRange 7 (±3), X distance 3, connects
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(3, 0, 0);
            var (connectorA, connectorB) = PlaceTwoPoles(posA, posB);
            GiveWire(50);

            var connected = ElectricWireSystemUtil.TryConnect(posA, posB, PlayerId, ConnectToolGuid, out _);

            Assert.IsTrue(connected);
            Assert.IsTrue(connectorA.ContainsWireConnection(connectorB.BlockInstanceId));
        }

        [Test]
        public void 高さ範囲外の電柱同士は接続できない()
        {
            // poleConnectionHeightRange(5)=±2の外（Y差3）は接続不可
            // Outside poleConnectionHeightRange 5 (±2): Y distance 3 fails
            var posA = Vector3Int.zero;
            var posB = new Vector3Int(0, 3, 0);
            PlaceTwoPoles(posA, posB);
            GiveWire(50);

            var connected = ElectricWireSystemUtil.TryConnect(posA, posB, PlayerId, ConnectToolGuid, out var error);

            Assert.IsFalse(connected);
            Assert.AreEqual(ElectricWirePlacementFailureReason.OutOfRange, error);
        }
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `OutOfRange` 未定義・引数不一致でコンパイルエラー（RED）。

- [ ] **Step 3: enumリネームとEvaluator実装**

`ElectricWirePlacementJudgement.cs`: enumメンバー `TooFar` を `OutOfRange` に**同位置で**リネーム（int互換維持。他の値の追加・並び替え禁止）。

`ElectricWirePlacementEvaluator.cs` の `EvaluateWireConnection` を変更:
- 引数 `float fromMaxWireLength, float toMaxWireLength` を削除
- `var maxDistance = Mathf.Min(...); if (maxDistance < distance) return ...TooFar;` の2行を削除
- XMLコメントを追加: 「distanceはコスト計算専用。接続可否の範囲判定は呼び出し側がIsMutuallyConnectableで行う / distance is for cost only; range judgement is the caller's duty via IsMutuallyConnectable」
- `using UnityEngine;` が不要になるので削除

- [ ] **Step 4: ElectricWireSystemUtil.TryConnect に相互範囲判定を追加**

`TryConnect` 内、`IsConnectToolUnlocked` チェックの後・評価器呼び出しの前に挿入し、評価器呼び出しから `MaxWireLength` を除去:

```csharp
            // 双方の範囲ボックス相互判定を行う。範囲外なら接続不可
            // Mutual range-box check between both endpoints; out of range fails
            var datastore = ServerContext.WorldBlockDatastore;
            var blockA = datastore.GetBlock(connectorA.BlockInstanceId);
            var blockB = datastore.GetBlock(connectorB.BlockInstanceId);
            if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(blockA.BlockMasterElement.BlockParam, out _, out var profileA, out var isPoleA) ||
                !ElectricWireBlockParamResolver.TryGetWireRangeParam(blockB.BlockMasterElement.BlockParam, out _, out var profileB, out var isPoleB))
            {
                failureReason = ElectricWirePlacementFailureReason.InvalidTarget;
                return false;
            }
            if (!ElectricConnectionRangeService.IsMutuallyConnectable(blockA.BlockPositionInfo, profileA, isPoleA, blockB.BlockPositionInfo, profileB, isPoleB))
            {
                failureReason = ElectricWirePlacementFailureReason.OutOfRange;
                return false;
            }

            // 距離はコスト計算専用。既存接続・所持アイテムを評価に渡す
            // Distance feeds cost only; pass existing connection state and held items to the evaluation
            var distance = Vector3Int.Distance(posA, posB);
            var alreadyConnected = connectorA.ContainsWireConnection(connectorB.BlockInstanceId) || connectorB.ContainsWireConnection(connectorA.BlockInstanceId);
            var anyConnectionFull = connectorA.IsWireConnectionFull || connectorB.IsWireConnectionFull;
            var inventory = ServerContext.GetService<IPlayerInventoryDataStore>().GetInventoryData(playerId).MainOpenableInventory;

            var judgement = ElectricWirePlacementEvaluator.EvaluateWireConnection(
                distance, alreadyConnected, anyConnectionFull, connectToolGuid, inventory.InventoryItems, null);
```

usingに `Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect` と `Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange` を追加。

- [ ] **Step 5: ElectricWireExtendService の距離直接判定を置換**

`ExecuteExtendWithOrigin` 内の

```csharp
                var distance = Vector3Int.Distance(fromPos, polePlaceInfo.Position);
                if (Mathf.Min(fromConnector.MaxWireLength, poleParam.MaxWireLength) < distance)
                    return ExtendResult.Failure(ElectricWirePlacementFailureReason.TooFar);
```

を以下へ置換（`poleGhostInfo` はTask 4 Step 3で導入済みの変数を利用。定義位置がこの判定より後なら判定の前へ移動する）:

```csharp
                // 起点と新設電柱の相互範囲判定を行う。距離はコスト計算専用に残す
                // Mutual range check between origin and the new pole; distance remains for cost only
                var fromBlock = ServerContext.WorldBlockDatastore.GetBlock(fromConnector.BlockInstanceId);
                if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(fromBlock.BlockMasterElement.BlockParam, out _, out var fromProfile, out var fromIsPole))
                    return ExtendResult.Failure(ElectricWirePlacementFailureReason.InvalidTarget);
                if (!ElectricConnectionRangeService.IsMutuallyConnectable(fromBlock.BlockPositionInfo, fromProfile, fromIsPole, poleGhostInfo, ConnectionRangeProfile.CreatePole(poleParam), true))
                    return ExtendResult.Failure(ElectricWirePlacementFailureReason.OutOfRange);
                var distance = Vector3Int.Distance(fromPos, polePlaceInfo.Position);
```

usingに `Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange` を追加。

- [ ] **Step 6: クライアント ElectricWireExtendPreviewCalculator を書き換え**

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Core.Item.Interface;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using Server.Protocol.PacketResponse.Util.ElectricWire;

using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;
using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;
using Server.Protocol.PacketResponse.Util.ElectricWire.Placement;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.ElectricWireConnect.Parts
{
    /// <summary>
    /// クライアント側でワイヤー接続可否を評価する。範囲相互判定と評価器はサーバーとソース共有
    /// Evaluate wire connections on the client, sharing the mutual range check and evaluator with the server
    /// </summary>
    public static class ElectricWireExtendPreviewCalculator
    {
        /// <summary>
        /// ブロックが電気系（ワイヤー端点）かを判定し、接続数上限と範囲プロファイルを返す
        /// Judge whether a block is electric and return its connection limit and range profile
        /// </summary>
        public static bool TryResolveWireParam(BlockGameObject block, out int maxWireConnectionCount, out ConnectionRangeProfile rangeProfile, out bool isPole)
        {
            return TryResolveWireParam(block.BlockMasterElement, out maxWireConnectionCount, out rangeProfile, out isPole);
        }

        public static bool TryResolveWireParam(BlockMasterElement master, out int maxWireConnectionCount, out ConnectionRangeProfile rangeProfile, out bool isPole)
        {
            // 9種の電気系BlockParamから上限と範囲を取り出す（非電気系はfalse）
            // Extract limits and ranges from the 9 electric block params (non-electric returns false)
            return ElectricWireBlockParamResolver.TryGetWireRangeParam(master.BlockParam, out maxWireConnectionCount, out rangeProfile, out isPole);
        }

        /// <summary>
        /// 既存ブロック同士の接続可否を評価する。範囲相互判定→評価器の順で判定する
        /// Evaluate connecting two existing blocks: mutual range check first, then the evaluator
        /// </summary>
        public static ElectricWirePlacementJudgement Evaluate(BlockGameObject source, BlockGameObject target, int sourceMaxConnectionCount, int targetMaxConnectionCount, float distance, Guid connectToolGuid, IEnumerable<IItemStack> inventoryItems)
        {
            // 範囲相互判定に失敗したらOutOfRangeで確定する
            // Fail fast with OutOfRange when the mutual range check does not pass
            if (!IsMutuallyInRange(source, target)) return ElectricWirePlacementJudgement.Failure(ElectricWirePlacementFailureReason.OutOfRange);

            var alreadyConnected = IsAlreadyConnected(source, target);
            var anyConnectionFull = IsConnectionFull(source, sourceMaxConnectionCount) || IsConnectionFull(target, targetMaxConnectionCount);

            return ElectricWirePlacementEvaluator.EvaluateWireConnection(
                distance, alreadyConnected, anyConnectionFull, connectToolGuid, inventoryItems, null);
        }

        /// <summary>
        /// 新設電柱への延長可否を評価する。新設側は未接続のため起点の状態のみ内部で判定する
        /// Evaluate extending to a newly placed pole; only the origin's state matters since the new pole has no connections
        /// </summary>
        public static ElectricWirePlacementJudgement EvaluateNewPole(BlockGameObject source, int sourceMaxConnectionCount, ElectricPoleBlockParam poleParam, BlockPositionInfo poleGhostInfo, float distance, Guid connectToolGuid, IEnumerable<IItemStack> inventoryItems)
        {
            // 起点と新設電柱ゴーストの範囲相互判定を行う
            // Mutual range check between the origin and the new pole ghost
            if (!TryResolveWireParam(source, out _, out var sourceProfile, out var sourceIsPole))
                return ElectricWirePlacementJudgement.Failure(ElectricWirePlacementFailureReason.InvalidTarget);
            if (!ElectricConnectionRangeService.IsMutuallyConnectable(source.BlockPosInfo, sourceProfile, sourceIsPole, poleGhostInfo, ConnectionRangeProfile.CreatePole(poleParam), true))
                return ElectricWirePlacementJudgement.Failure(ElectricWirePlacementFailureReason.OutOfRange);

            var sourceFull = IsConnectionFull(source, sourceMaxConnectionCount);

            return ElectricWirePlacementEvaluator.EvaluateWireConnection(
                distance, false, sourceFull, connectToolGuid, inventoryItems, null);
        }

        // 双方のプロファイルを解決して相互範囲判定にかける
        // Resolve both profiles and run the mutual range check
        private static bool IsMutuallyInRange(BlockGameObject blockA, BlockGameObject blockB)
        {
            if (!TryResolveWireParam(blockA, out _, out var profileA, out var isPoleA)) return false;
            if (!TryResolveWireParam(blockB, out _, out var profileB, out var isPoleB)) return false;

            return ElectricConnectionRangeService.IsMutuallyConnectable(blockA.BlockPosInfo, profileA, isPoleA, blockB.BlockPosInfo, profileB, isPoleB);
        }

        // どちらか一方の接続先集合に相手が含まれていれば接続済み
        // Connected when either side's partner set contains the other
        private static bool IsAlreadyConnected(BlockGameObject blockA, BlockGameObject blockB)
        {
            if (blockA.TryGetComponent<ElectricWireStateChangeProcessor>(out var processorA) &&
                processorA.CurrentPartnerIds.Contains(blockB.BlockInstanceId)) return true;

            return blockB.TryGetComponent<ElectricWireStateChangeProcessor>(out var processorB) &&
                   processorB.CurrentPartnerIds.Contains(blockA.BlockInstanceId);
        }

        // 受信済みワイヤー状態とマスタ上限から接続数が満杯かを判定する
        // Judge whether the connection count is full, using received wire state and the master limit
        private static bool IsConnectionFull(BlockGameObject block, int maxWireConnectionCount)
        {
            return block.TryGetComponent<ElectricWireStateChangeProcessor>(out var processor) &&
                   maxWireConnectionCount <= processor.CurrentPartnerIds.Count;
        }
    }
}
```

注意: `ElectricWirePlacementJudgement.Failure` がpublicであることを確認（SystemUtilで使用済みのため既にpublicのはず）。

- [ ] **Step 7: ElectricWireExtendMode / ElectricWireEditMode を追従**

`ElectricWireExtendMode.cs`:
- 41行: `TryResolveWireParam(source, out var sourceMaxCount, out var sourceMaxLength)` → `TryResolveWireParam(source, out var sourceMaxCount, out _, out _)`
- 52行: `TryResolveWireParam(target, out var targetMaxCount, out var targetMaxLength)` → `TryResolveWireParam(target, out var targetMaxCount, out _, out _)`、`ConnectToTarget(target, targetMaxCount, targetMaxLength)` → `ConnectToTarget(target, targetMaxCount)`
- `ConnectToTarget` ローカル関数: シグネチャを `void ConnectToTarget(BlockGameObject targetBlock, int targetMaxConnectionCount)` にし、72行の呼び出しを `ElectricWireExtendPreviewCalculator.Evaluate(source, targetBlock, sourceMaxCount, targetMaxConnectionCount, distance, connectToolGuid, _context.Inventory)` へ
- `ExtendToEmptySpace`: 104行の `TryResolveWireParam(poleMaster, out _, out var poleMaxLength)` チェックを `poleMaster.BlockParam is not ElectricPoleBlockParam poleParam` チェックに置換し、125行を以下へ:

```csharp
                // 新設電柱の仮AABBを構築して範囲相互判定込みで評価する
                // Build the new pole's ghost AABB and evaluate including the mutual range check
                var poleGhostInfo = new BlockPositionInfo(placeInfo.Position, BlockDirection.North, poleMaster.BlockSize);
                var distance = Vector3Int.Distance(fromPos, placeInfo.Position);
                var judgement = ElectricWireExtendPreviewCalculator.EvaluateNewPole(source, sourceMaxCount, poleParam, poleGhostInfo, distance, connectToolGuid, _context.Inventory);
```

usingに `Mooresmaster.Model.BlocksModule` を追加。

`ElectricWireEditMode.cs` 46行: `TryResolveWireParam(block, out _, out _)` → `TryResolveWireParam(block, out _, out _, out _)`。

- [ ] **Step 8: コンパイル+テスト（GREEN確認）**

```
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWirePlacementEvaluatorTest|ElectricWireSystemUtilTest"
```
Expected: 全PASS（旧TooFarテストは削除済み、新規範囲テスト3件を含む）。

- [ ] **Step 9: Commit**

```bash
git add moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts
git commit -m "refactor: 接続可否を相互範囲判定へ移行しTooFarをOutOfRangeへ変更"
```

---

### Task 6: クライアント自動接続コレクターの列挙化と MaxElectricPoleMachineConnectionRange 削除

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/ElectricWireAutoConnect/ClientElectricWireAutoConnectCollector.cs`（全面書き換え）
- Delete: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/ElectricWire/ConnectionRange/MaxElectricPoleMachineConnectionRange.cs`（+.metaはUnityが処理）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`（154行の `AddSingleton<MaxElectricPoleMachineConnectionRange, ...>` を削除）

**Interfaces:**
- Consumes: `BlockGameObjectDataStore.BlockGameObjectByInstanceIdDictionary`（`IReadOnlyDictionary<BlockInstanceId, BlockGameObject>`）、`BlockGameObject.BlockPosInfo`（完全な `BlockPositionInfo`）
- Produces: `Collect(BlockId blockId, Vector3Int position, BlockDirection direction, BlockGameObjectDataStore blockDataStore)` は既存シグネチャ維持（呼び出し元 ElectricWireAutoConnectPreview は無変更）

- [ ] **Step 1: ClientElectricWireAutoConnectCollector.cs を全面書き換え**

```csharp
using System.Collections.Generic;
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.StateProcessor.ElectricWire;
using Core.Master;
using Game.Block.Interface;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

using Server.Protocol.PacketResponse.Util.ElectricWire.AutoConnect;
using Server.Protocol.PacketResponse.Util.ElectricWire.ConnectionRange;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Common.ElectricWireAutoConnect
{
    /// <summary>
    /// 自動接続候補を受信済みクライアント状態のみで収集する。サーバーのワールド状態には触れない
    /// Collects auto-connect candidates using only received client state; never touches server world state
    /// 収集ルールはサーバーのElectricWireAutoConnectTargetCollectorと同一（相互範囲判定＋最寄り電柱1本＋未接続機械）
    /// The rules mirror the server's ElectricWireAutoConnectTargetCollector (mutual range, nearest pole, unconnected machines)
    /// </summary>
    public static class ClientElectricWireAutoConnectCollector
    {
        public static List<(Vector3Int TargetPos, float Distance)> Collect(BlockId blockId, Vector3Int position, BlockDirection direction, BlockGameObjectDataStore blockDataStore)
        {
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);

            // 自身の接続容量が0なら探索するまでもなく対象なし
            // No point searching when this block has zero connection capacity
            if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(blockMaster.BlockParam, out var ownCapacity, out var ownProfile, out var ownIsPole) || ownCapacity <= 0)
                return new List<(Vector3Int, float)>();

            var ownInfo = new BlockPositionInfo(position, direction, blockMaster.BlockSize);

            return ownIsPole
                ? CollectPoleTargets((ElectricPoleBlockParam)blockMaster.BlockParam, ownInfo, ownProfile, blockDataStore)
                : CollectMachineTargets(ownInfo, ownProfile, blockDataStore);
        }

        // 電柱設置: 最寄り電柱1本＋相互範囲内の未接続機械を残り本数まで
        // Pole placement: nearest pole plus unconnected machines mutually in range up to remaining capacity
        private static List<(Vector3Int, float)> CollectPoleTargets(ElectricPoleBlockParam ownParam, BlockPositionInfo ownInfo, ConnectionRangeProfile ownProfile, BlockGameObjectDataStore dataStore)
        {
            var results = new List<(Vector3Int, float)>();
            var usedCount = 0;

            var nearestPole = EnumerateConnectableCandidates(ownInfo, ownProfile, true, dataStore)
                .Where(c => c.IsPole)
                .OrderBy(c => c.Distance).ThenBy(c => c.Block.BlockInstanceId.AsPrimitive())
                .FirstOrDefault();

            if (nearestPole.Block != null && usedCount < ownParam.MaxWireConnectionCount)
            {
                results.Add((nearestPole.Block.BlockPosInfo.OriginalPos, nearestPole.Distance));
                usedCount++;
            }

            var machineCandidates = EnumerateConnectableCandidates(ownInfo, ownProfile, true, dataStore)
                .Where(c => !c.IsPole && GetPartnerCount(c.Block) == 0)
                .OrderBy(c => c.Distance).ThenBy(c => c.Block.BlockInstanceId.AsPrimitive());

            foreach (var candidate in machineCandidates)
            {
                if (ownParam.MaxWireConnectionCount <= usedCount) break;
                results.Add((candidate.Block.BlockPosInfo.OriginalPos, candidate.Distance));
                usedCount++;
            }

            return results;
        }

        // 機械設置: 相互範囲内の最寄り電柱1本のみ
        // Machine placement: only the nearest pole mutually in range
        private static List<(Vector3Int, float)> CollectMachineTargets(BlockPositionInfo ownInfo, ConnectionRangeProfile ownProfile, BlockGameObjectDataStore dataStore)
        {
            var nearestPole = EnumerateConnectableCandidates(ownInfo, ownProfile, false, dataStore)
                .Where(c => c.IsPole)
                .OrderBy(c => c.Distance).ThenBy(c => c.Block.BlockInstanceId.AsPrimitive())
                .FirstOrDefault();

            if (nearestPole.Block == null) return new List<(Vector3Int, float)>();

            return new List<(Vector3Int, float)> { (nearestPole.Block.BlockPosInfo.OriginalPos, nearestPole.Distance) };
        }

        // 受信済み全ブロックから、相互範囲内で接続上限未満のワイヤー端点を距離付きで列挙する
        // Enumerate wire endpoints mutually in range and below capacity from all received blocks, with distances
        private static IEnumerable<(BlockGameObject Block, bool IsPole, float Distance)> EnumerateConnectableCandidates(BlockPositionInfo ownInfo, ConnectionRangeProfile ownProfile, bool ownIsPole, BlockGameObjectDataStore dataStore)
        {
            foreach (var block in dataStore.BlockGameObjectByInstanceIdDictionary.Values)
            {
                if (!ElectricWireBlockParamResolver.TryGetWireRangeParam(block.BlockMasterElement.BlockParam, out var capacity, out var targetProfile, out var targetIsPole)) continue;
                if (capacity <= GetPartnerCount(block)) continue;
                if (!ElectricConnectionRangeService.IsMutuallyConnectable(ownInfo, ownProfile, ownIsPole, block.BlockPosInfo, targetProfile, targetIsPole)) continue;

                yield return (block, targetIsPole, Vector3Int.Distance(ownInfo.OriginalPos, block.BlockPosInfo.OriginalPos));
            }
        }

        // 受信済みワイヤー状態から接続本数を得る
        // Read the connection count from received wire state
        private static int GetPartnerCount(BlockGameObject block)
        {
            return block.TryGetComponent<ElectricWireStateChangeProcessor>(out var processor) ? processor.CurrentPartnerIds.Count : 0;
        }
    }
}
```

注意: `BlockGameObjectByInstanceIdDictionary` は `BlockGameObjectDataStore.cs:22` に実在確認済み。「原点座標のみの近似」注記は解消されるため削除している。

- [ ] **Step 2: MaxElectricPoleMachineConnectionRange を削除**

- `MaxElectricPoleMachineConnectionRange.cs` を `rm` で削除（.metaはUnityコンパイル時に自動整理。手動で.meta単体を残さないよう `rm` は .cs と .cs.meta の両方）
- `MoorestechServerDIContainerGenerator.cs:154` の `services.AddSingleton<MaxElectricPoleMachineConnectionRange, MaxElectricPoleMachineConnectionRange>();` を削除
- `grep -rn "MaxElectricPoleMachineConnectionRange" moorestech_server moorestech_client --include="*.cs"` で参照0件を確認

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。

- [ ] **Step 4: Commit**

```bash
git add -A moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts
git commit -m "refactor: クライアント自動接続を列挙+相互判定化しMaxElectricPoleMachineConnectionRangeを削除"
```

---

### Task 7: DisplayEnergizedRange 廃止（コード・シーン・Web契約C#側）

**Files:**
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/Electric/DisplayEnergizedRange.cs` / `EnergizedRangeObject.cs`（+各.meta）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（134行の SerializeField / 326行の Resolve / DI登録行）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs`（97行）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/PlacementModeTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractC2Test.cs` / `WireFixtures/placement_mode.json`
- シーン: MainGameシーン上の DisplayEnergizedRange GameObject（uloop execute-dynamic-code で削除）

- [ ] **Step 1: PlacementModeTopic から range 依存を除去**

`PlacementModeTopic.cs` の変更:
- `using Client.Game.InGame.Electric;` を削除
- フィールド `private readonly DisplayEnergizedRange _range;` を削除
- コンストラクタ引数 `DisplayEnergizedRange range` と代入、`range.OnRangeVisibleChanged...` の購読行（35行）を削除
- `BuildJson()` の `EnergizedRangeVisible = _range.IsRangeVisible(),` を削除
- `PlacementModeDto` の `public bool EnergizedRangeVisible;` フィールドを削除

- [ ] **Step 2: WebUiGameBinder / MainGameStarter から参照除去**

- `WebUiGameBinder.cs:97`: `resolver.Resolve<DisplayEnergizedRange>()` 引数を削除（PlacementModeTopic の新コンストラクタに合わせる）
- `MainGameStarter.cs`: 134行の `[SerializeField] private DisplayEnergizedRange displayEnergizedRange;`、326行の `_resolver.Resolve<DisplayEnergizedRange>();`、および `displayEnergizedRange` をDIコンテナに登録している行（`RegisterComponent` 等。`grep -n "displayEnergizedRange" MainGameStarter.cs` で全行特定）を削除

- [ ] **Step 3: C#コード削除とフィクスチャ更新**

```bash
rm moorestech_client/Assets/Scripts/Client.Game/InGame/Electric/DisplayEnergizedRange.cs*
rm moorestech_client/Assets/Scripts/Client.Game/InGame/Electric/EnergizedRangeObject.cs*
```

- `WireFixtures/placement_mode.json` から `energizedRangeVisible` キーを削除
- `WireContractC2Test.cs:15` の `EnergizedRangeVisible = true` を削除

- [ ] **Step 4: コンパイルとシーン整理**

`uloop compile --project-path ./moorestech_client` → エラー0を確認後、uloop-execute-dynamic-code スキルを使い、MainGameシーンから DisplayEnergizedRange の GameObject（rangePrefab参照ごと）を削除して MainGameStarter の missing 参照を解消し、シーンを保存する。`uloop get-logs --project-path ./moorestech_client --log-type Error` で missing script エラーがないことを確認。EnergizedRange用のプレハブアセットが専用フォルダにあれば同様にUnity経由で削除する（`uloop` でアセット検索）。

- [ ] **Step 5: 契約テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContract"`
Expected: PASS（C#側フィクスチャ整合。Web側の追従はTask 10）。

- [ ] **Step 6: Commit**

```bash
git add -A moorestech_client/Assets
git commit -m "feat: 接続範囲表示DisplayEnergizedRangeを廃止しプレビュー線表示へ一本化"
```

---

### Task 8: maxWireLength 全廃（スキーマ・JSON・interface・コンポーネント・テンプレート・旧API削除）

**Files:**
- Modify: `VanillaSchema/blocks.yml`（9箇所の `maxWireLength` 削除）
- Modify: forUnitTest / EditModeInPlayingTestMod の blocks.json（`maxWireLength` キー全削除）
- Modify: `Game.EnergySystem/ElectricWire/IElectricWireConnector.cs` / `Game.Block/Blocks/ElectricWire/ElectricWireConnectorComponent.cs`
- Modify: BlockTemplate 9ファイル（`VanillaElectricPoleTemplate.cs:29` / `VanillaMachineTemplate.cs:46,95` / `VanillaMinerTemplate.cs:35,61` / `VanillaElectricPumpTemplate.cs:39` / `VanillaPowerGeneratorTemplate.cs:38` / `VanillaGearToElectricGeneratorTemplate.cs:35` / `VanillaElectricToGearGeneratorTemplate.cs:39` / `VanillaCleanRoomAirFilterTemplate.cs:37` / `VanillaCleanRoomMachineTemplate.cs:53`）
- Modify: `ElectricWireBlockParamResolver.cs`（旧 `TryGetWireParam` 削除）/ `ElectricConnectionRangeService.cs`（旧セル列挙・旧判定メソッド削除）
- Modify: `Tests/UnitTest/Game/FakeWireConnector.cs`（`MaxWireLength => 10f` 削除）

- [ ] **Step 1: interface・コンポーネント・テンプレートから MaxWireLength を削除**

- `IElectricWireConnector.cs`: `float MaxWireLength { get; }` の行を削除
- `ElectricWireConnectorComponent.cs`: `public float MaxWireLength { get; }` プロパティ、コンストラクタ引数 `float maxWireLength` と代入 `MaxWireLength = maxWireLength;` を削除。クラス冒頭コメント「最大接続距離」も「最大接続数を保持する / Hold max connection count」に修正
- BlockTemplate 9ファイル: `new ElectricWireConnectorComponent(param.MaxWireConnectionCount, param.MaxWireLength, blockInstanceId, ...)` の第2引数 `param.MaxWireLength,` を削除（`grep -rn "MaxWireLength" moorestech_server/Assets/Scripts/Game.Block` で全箇所特定して機械的に適用）
- `FakeWireConnector.cs`: `public float MaxWireLength => 10f;` を削除

- [ ] **Step 2: 旧APIを削除**

- `ElectricWireBlockParamResolver.cs`: 旧 `TryGetWireParam(IBlockParam, out int, out float)` メソッドを削除
- `ElectricConnectionRangeService.cs`: `EnumeratePoleRange` / `EnumerateMachineRange` / `EnumerateRange` / `EnumerateCandidatePolePositions` / `IsWithinMachineRange` / `IsWithinPoleRange` を削除（残るのは `IsMutuallyConnectable` と `Covers` のみ。200行制限内に収まる）
- `grep -rn "EnumeratePoleRange\|EnumerateMachineRange\|EnumerateCandidatePolePositions\|IsWithinMachineRange\|IsWithinPoleRange\|TryGetWireParam\b" moorestech_server moorestech_client --include="*.cs"` で参照0件を確認（残っていればその呼び出し側の移行漏れなので先に修正）

- [ ] **Step 3: スキーマとJSONから maxWireLength を削除**

- `VanillaSchema/blocks.yml`: `- key: maxWireLength` とその `type`/`default` 行を9箇所すべて削除（edit-schemaスキルの手順で再生成）
- forUnitTest blocks.json: 18箇所の `"maxWireLength": ...` キーを削除
- EditModeInPlayingTestMod blocks.json: 3箇所の `"maxWireLength": ...` キーを削除

- [ ] **Step 4: 再生成+コンパイル+残存グレップ**

```
uloop compile --project-path ./moorestech_client
grep -rn "MaxWireLength\|maxWireLength" moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts VanillaSchema
```
Expected: コンパイルエラー0。grepは0件（Mooresmaster生成物ディレクトリからも消えていること）。

- [ ] **Step 5: Commit**

```bash
git add -A VanillaSchema moorestech_server moorestech_client
git commit -m "refactor: maxWireLengthをスキーマ・コード・マスタから全廃"
```

---

### Task 9: 電線系テストの全回帰と座標再設計

**Files:**
- Modify: `Tests/CombinedTest/Server/PacketTest/ElectricWireExtendProtocolTest.cs` / `ElectricWireExtendProtocolFailureTest.cs` / `ElectricWireAutoConnectPlaceTest.cs` / `ElectricWireConnectionEditProtocolTest.cs` / `GearChainPoleExtendProtocolTest.cs`（存在すれば）/ `ElectricWireSaveLoadTest.cs` ほか regex に掛かる全て

- [ ] **Step 1: 電線系テストを一括実行して失敗を洗い出す**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWire"`
Expected: 座標が新範囲の外にあるシナリオがFAILする（例: ExtendProtocolTest の電柱 (4,0,0)→(6,0,0) は電柱間±3の外）。

- [ ] **Step 2: 失敗テストを寸法表基準で修正**

方針（テストの意図は変えず座標・期待値のみ更新）:
- 電柱↔電柱の接続成功シナリオ: X/Z差 ≤3・Y差 ≤2 に再配置
- 電柱↔機械の接続成功シナリオ: X/Z差 ≤2・Y差 ≤2 に再配置
- `TooFar` を期待していた失敗シナリオ: 範囲外座標（電柱同士なら差4以上）+ 期待値 `OutOfRange` へ
- 「範囲外で孤立設置」系（(50,50)等）: そのまま成立するはず。期待FailureReasonのみ確認
- QA原則（AGENTS.md「問題がある前提で進める」）: 修正後も境界±1のケースが最低1つずつあることを確認し、なければ追加

- [ ] **Step 3: CleanRoom自動接続の新規テストを追加**

`ElectricWireAutoConnectPlaceTest.cs` に追加（同ファイルの既存ヘルパー `CreateServer` / `SetupWire` / `GrantRequiredItems` / `PlaceBlock` をそのまま使う。`ForUnitTestModBlockId.CleanRoomMachineId` は既存定数）:

```csharp
        [Test]
        public void クリーンルーム機械も電柱の自動接続対象になる()
        {
            // 電柱の機械範囲(±2)内にクリーンルーム機械を置いてから電柱をプロトコル経由で設置する
            // Place a clean room machine within the pole's machine range (±2), then place the pole via the protocol
            var (packet, serviceProvider) = CreateServer();
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;

            worldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.CleanRoomMachineId, new Vector3Int(1, 0, 0), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var cleanRoomMachine);

            SetupWire(serviceProvider, 5);
            GrantRequiredItems(serviceProvider, ForUnitTestModBlockId.ElectricPoleId);
            PlaceBlock(packet, ForUnitTestModBlockId.ElectricPoleId, new Vector3Int(0, 0, 0));

            // 旧resolverはCleanRoom未対応で自動接続されなかった。新resolverで接続されることを確認する
            // The old resolver skipped CleanRoom blocks; verify the new resolver wires them up
            var pole = worldBlockDatastore.GetBlock(new Vector3Int(0, 0, 0));
            var poleConnector = pole.GetComponent<IElectricWireConnector>();
            var machineConnector = cleanRoomMachine.GetComponent<IElectricWireConnector>();

            Assert.IsTrue(poleConnector.ContainsWireConnection(machineConnector.BlockInstanceId));
            Assert.IsTrue(machineConnector.ContainsWireConnection(poleConnector.BlockInstanceId));
        }
```

- [ ] **Step 4: 全電線系+トポロジ回帰**

```
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWire"
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ElectricWireNetworkDatastore|ConnectElectricSegment|DisconnectElectricSegment|ElectricWireSaveLoad"
```
Expected: 全PASS（セグメント/セーブロードは距離非依存なので無修正PASSのはず。落ちたら波及バグ）。

- [ ] **Step 5: Commit**

```bash
git add moorestech_server/Assets/Scripts/Tests
git commit -m "test: 電線系テストを範囲ボックス相互判定の座標体系へ再設計"
```

---

### Task 10: moorestech_web の契約更新と dist 反映

**Files（リポジトリ /Users/katsumi/moorestech/moorestech_web）:**
- Modify: `webui/src/bridge/contract/schemas/ui.ts`（33行 `energizedRangeVisible: z.boolean(),` を削除）
- Modify: `webui/src/features/modeHud/PlacementModeHud.tsx`（24行 `{data.energizedRangeVisible && <Text>{energized}</Text>}` と `energized` 文言定義を削除）
- Modify: `webui/e2e/mock-host/topics/topicFixtures.ts` / `topicControls.ts`（`energizedRangeVisible` フィールド削除）
- Modify: `webui/src/bridge/contract/wireContract.test.ts` / `validators.test.ts`（該当フィールドの期待値削除）
- Build出力: `moorestech_client/Assets/StreamingAssets/WebUi/dist`

- [ ] **Step 1: pwd確認と該当箇所の削除**

`cd /Users/katsumi/moorestech/moorestech_web && pwd` を確認後、`grep -rn "energizedRange" webui/src webui/e2e` で全ヒットを列挙し、上記5ファイル+ヒットした残り全てから `energizedRangeVisible` 関連の行を削除する。UI装飾は既存CSS/DOMのまま（webuiパリティ規約: 画像アセット追加禁止）。

- [ ] **Step 2: Webテスト実行**

Run: `cd webui && npm test`（コマンドがなければ package.json の scripts を確認して test 相当を実行）
Expected: wireContract / validators テストPASS。

- [ ] **Step 3: dist をビルドして Unity 側へ反映**

webui の build スクリプト（package.json の `build`）を実行し、出力を `moorestech_client/Assets/StreamingAssets/WebUi/dist` へ反映（既存の反映手順スクリプトがあればそれを使用。`git log` で dist 更新コミットの前例を確認して同じ手順を踏む）。

- [ ] **Step 4: C#側契約テスト再実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "WireContract"`
Expected: PASS。

- [ ] **Step 5: 両リポジトリでCommit**

```bash
cd /Users/katsumi/moorestech/moorestech_web && git add -A && git commit -m "feat: placement_mode契約からenergizedRangeVisibleを削除"
cd /Users/katsumi/moorestech && git add moorestech_client/Assets/StreamingAssets && git commit -m "chore: WebUI distを契約変更に追従"
```

---

### Task 11: 実マスタ（moorestech_master）の更新と起動確認

**Files（リポジトリ /Users/katsumi/moorestech_master）:**
- Modify: `server_v8/mods/moorestechAlphaMod_8/master/blocks.json`

- [ ] **Step 1: 実マスタJSONを更新**

`pwd` 確認後、Pythonで機械的に適用:
- `maxWireLength` を持つ全エントリからキーを削除
- `blockType` が ElectricPole **以外**の該当エントリに `"connectionRange": 30, "connectionHeightRange": 20` を追加（ユーザー裁定値）
- ElectricPole系（電柱/高圧電柱/広範囲電柱）は既存の4range値を維持

- [ ] **Step 2: 実マスタで起動確認**

`uloop compile --project-path ./moorestech_client` 後、Unityでプレイ開始（uloopのプレイモード起動）し、`uloop get-logs --project-path ./moorestech_client --log-type Error` でマスタロード例外（MooresmasterLoaderException等）がないことを確認。可能なら unity-playmode-recorded-playtest スキルで「電柱設置→範囲内機械へ自動接続線が張られる」を1シナリオ確認。

- [ ] **Step 3: Commit（別リポジトリ）**

```bash
cd /Users/katsumi/moorestech_master && git add server_v8 && git commit -m "feat: 電線接続の範囲相互判定化に伴いconnectionRangeを追加しmaxWireLengthを削除"
```

---

### Task 12: 最終レビュー（必須ゲート・省略不可）

- [ ] **Step 1: 全リポジトリのコミット漏れ確認**

`/Users/katsumi/moorestech`・`/Users/katsumi/moorestech_master`・`/Users/katsumi/moorestech/moorestech_web` それぞれで `git status` を確認し、未コミットの作業をコミットする。

- [ ] **Step 2: moores-code-review スキルで全ブランチレビューを実行**

必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。指摘が出たら修正して再コンパイル・再テスト・コミットまで行う。
