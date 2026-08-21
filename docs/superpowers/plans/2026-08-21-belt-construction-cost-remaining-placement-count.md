# ベルト建設コストを「残り設置数」の財布で1セットN個にする Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** ブロックマスタに必須フィールド `placementsPerCost`（設置数/1セット）を追加し、プレイヤー×財布の「残り設置数」をサーバーで永続管理して、ベルトコンベアを建設コスト1セットでN個（歯車=3・電気=5）設置できるようにする。撤去は残り+1・N到達で素材1セットへ凝縮返却。クライアントには3点セットで同期し、ビルドメニュー詳細に「N個分」と「残り設置数」を表示する。

**Architecture:** 新アセンブリ `Game.Construction` に `RemainingPlacementCountDataStore`（Lookup/Mutation分離・UniRx通知・セーブJSON）と財布キー解決 util を置く。`PlaceBlockProtocol`/`RemoveBlockProtocol` は `Server.Protocol/.../Util/Construction/RemainingPlacementChargeService` 経由で財布を見てから `ConstructionCostService` を呼ぶ。同期は `RemainingPlacementCountChangedEventPacket`（per-player）＋ `InitialHandshakeProtocol` 同梱＋クライアント `ClientRemainingPlacementCountDatastore`/`RemainingPlacementCountEventHandler`。プレビューの「置ける数」は `ConstructionCostPreviewCalculator.CalculateAffordablePlacementCount` で残り設置数込みに統一。webui は `BuildMenuEntryDto` に2フィールド足して `BuildMenuDetailSidebar` で表示する。`placementsPerCost == 1` のブロックは財布を素通りし現行挙動と完全一致。

**Tech Stack:** Unity / C# / MessagePack / Newtonsoft JSON / UniRx / VContainer / Mooresmaster SourceGenerator（YAML→C#）/ NUnit（uloop）/ React + zod + vitest（webui）

## Requirements

設計裁定: `docs/adr/0026-belt-construction-cost-remaining-placement-count.md`、`.decisions/2026-08-21-ベルト設置コストは永続クレジット台帳で3個1セットにする.md`、用語集 `CONTEXT.md`（建設コスト／設置数/1セット／残り設置数／財布）。

- R1. `blocks.yml` に **必須** フィールド `placementsPerCost`（整数・`default: 1`）を追加し、リポジトリ内およびmoorestech_masterの全 `blocks.json` を一括更新する（既存は1）。受け入れ: 全blocks.jsonの全要素が `placementsPerCost` を持ち、マスタロードが通る。
- R2. moorestech_master の値は 歯車ベルト（直線歯車/鉄の歯車の各ファミリー3ブロック）=3、電気ベルト（ベルトコンベア/高速ベルトコンベアの各3ブロック）=5、分岐器・他全ブロック=1。受け入れ: JSONの該当12ブロックが3/5、他が1。
- R3. バリデータ: `placementsPerCost >= 1`。ベルトファミリー内の全メンバーで `requiredItems`（itemGuid+count集合）と `placementsPerCost` が一致。受け入れ: 違反JSONで `BeltConveyorFamilyValidator`/`BlockMasterUtil` がエラー文字列を返すテストが通る。
- R4. サーバーに残り設置数DataStore（プレイヤー×財布）を新設。財布キーはファミリー所属なら直線代表BlockId、非所属なら自BlockId。セーブJSONに永続（Guid保存・0件は保存しない）。受け入れ: セーブ→ロード往復テスト。
- R5. 設置: `placementsPerCost>1` のとき残り>0なら素材消費なしで残り-1、残り=0なら建設コスト1セット消費→残り+N→-1。`placementsPerCost==1` は従来どおり毎セル全額。TryAddBlock失敗時は財布も素材も変えない。電線自動接続の予約コストは「そのセルで実際に消費する素材」（財布で賄うなら空）。受け入れ: PlaceBlockProtocolTestの新規4本。
- R6. 撤去: `placementsPerCost>1` のブロックは残り+1し、Nに達したら残り=0にして建設コスト1セットを返却。凝縮返却がインベントリに入り切らないなら撤去失敗（財布も変えない）。`placementsPerCost==1` は従来どおり全額返却。受け入れ: RemoveBlockProtocolTestの新規3本。
- R7. 同期3点セット: `RemainingPlacementCountChangedEventPacket`（該当プレイヤーのみ・財布BlockId＋残数）、`InitialHandshakeProtocol` に全財布の残数を同梱、クライアント `RemainingPlacementCountEventHandler` が `ClientRemainingPlacementCountDatastore` へ適用。受け入れ: サーバー側イベント発火テスト＋handshake同梱テスト。
- R8. クライアントプレビュー: 置けるセル数 = 残り設置数 + floor(所持素材で買えるセット数)×N（コスト未定義ならMaxValue）。CommonBlockPlaceSystem と BeltConveyorPlaceSystem の両経路で使う。受け入れ: `ConstructionCostPreviewCalculatorTest` の新規テスト。
- R9. webui: ビルドメニュー詳細のコスト欄に `placementsPerCost>1` のとき「（N個分）」と「残り設置数: k」を表示。語彙に「クレジット」「支払い」を使わない。残数変化でトピック再配信。受け入れ: zodスキーマテスト＋wire fixture更新＋表示。
- R10. コード・スキーマ・UIとも名前は用語集に揃え、`Credit`/`Payment` を使わない。
- やらないこと: HUD・カーソルツールチップへの残数表示／n連ベルトの再導入／旧セーブの自動移行コード（セーブに欠けていれば空で起動）／ベルト以外のplacementsPerCost>1設定。

## Global Constraints

- `pwd` で作業worktreeを確認してから着手（メインworktreeでのUnity起動禁止、`moores-wt new` で作ったworktreeを使う）
- 1ファイル200行以下・1ディレクトリ10ファイルまで・`partial` 禁止・`Func<>` 禁止・try-catch原則禁止・デフォルト引数禁止・単純getter/setter禁止（`{ get; private set; }` 可）
- 主要処理に日本語→英語の2行セットコメント（各1行）
- `.cs` 変更後は必ず `uloop compile --project-path ./moorestech_client`
- テストは `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "..."`。「Unity is reloading」なら45秒待ってリトライ
- `.meta` 手動作成禁止。Prefab/シーンのテキスト編集禁止
- スキーマ編集は edit-schema スキルの手順（`_CompileRequester.cs` の dummyText を変えてSourceGenerator再生成）。`optional: true`／`?? Default`／ローダー補完は禁止（全JSON一括更新が正規手順）
- `Mooresmaster.Model.*` 生成物の手動編集禁止
- イベント通知は UniRx `Subject<T>`/`IObservable<T>`。C# `event Action` 禁止
- 永続化は Newtonsoft JSON、キーはGuid（揮発BlockId保存禁止）
- 新DataStoreは読み取り `I*Lookup` / 変更 `I*Mutation` に分離しDI登録（前例 `Game.Hotbar`）
- moorestech_master のJSON編集時は mooreseditor.app を終了しておく。コミット後 `.moorestech-external-revisions.json` の `moorestech_master.commitHash` を更新し、そのSHAを **push済み** にする
- 各タスク終了時に必ずコミット（worktree運用のため作業消失防止）
- 語彙: 用語集（`CONTEXT.md`）の「建設コスト／設置数/1セット／残り設置数／財布」。`Credit`/`Payment`/「クレジット」「支払い」はコード・UIとも禁止

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置先 | 機構 / 前例 | 判定 |
|---|---|---|---|---|
| 1 | `placementsPerCost` | `VanillaSchema/blocks.yml`（requiredItems直後・必須・default 1） | `requiredItems.count`（`default: 1`）、idlePowerRate必須化前例 | 適合 |
| 2 | `PlacementsPerCostValidation` / ファミリー一致検証 | `Core.Master/Validator/BlockMasterUtil.cs` / `BeltConveyorFamilyValidator.cs` | `BlockRequiredItemsValidation`（汎用Validate） | 適合 |
| 3 | `ConstructionWalletUtil`（財布キー解決・マスタ読取のみ） | 新 `Game.Construction` | `BeltConveyorPlaceFamilyUtil`（Game.Block.Interface static util） | 適合（同ドメインにDataStoreと同居） |
| 4 | `RemainingPlacementCountDataStore` + `IRemainingPlacementCountLookup`/`Mutation` + SaveJsonObject | 新 `Game.Construction` | `Game.Hotbar/HotbarAssignmentDatastore` 一式 | 適合 |
| 5 | セーブ配線 | `WorldSaveAllInfoV1` / `AssembleSaveJsonText` / `WorldLoaderFromJson` | `hotbarAssignments` の配線 | 適合 |
| 6 | `RemainingPlacementCountChangedEventPacket` | `Server.Event/EventReceive` | `HotbarUpdateEventPacket`（per-player AddEvent, IBootInitializable） | 適合 |
| 7 | handshake同梱 `[Key(8)]` | `InitialHandshakeProtocol` | `HotbarAssignments [Key(7)]` | 適合 |
| 8 | `RemainingPlacementChargeService`（財布を見て消費コストを決める） | `Server.Protocol/PacketResponse/Util/Construction/` | `ConstructionCostService`（同階層static） | 適合 |
| 9 | DI登録 | `MoorestechServerDIContainerGenerator.cs` | Hotbar3行＋EventPacket登録 | 適合 |
| 10 | `ClientRemainingPlacementCountDatastore` / `RemainingPlacementCountEventHandler` | `Client.Game/InGame/Construction/` | `ClientHotbarDatastore`/`HotbarNetworkEventHandler` | 適合 |
| 11 | プレビュー計算 | `ConstructionCostPreviewCalculator` に新メソッド追加 | 同ファイル既存メソッド | 適合 |
| 12 | webui DTO/トピック | `BuildMenuEntryDto` 2フィールド、`BuildMenuTopic` 購読追加 | `HotbarTopic` の購読パターン | 適合 |

データフロー: `va:placeBlock/va:removeBlock` →（書き手）`RemainingPlacementCountDataStore` →（読み手）EventPacket → client datastore → プレビュー計算／webui トピック。新しい制御経路は足さない。

### 操作死活表

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 非ベルトブロックの設置/撤去 | 生存・挙動不変 | placementsPerCost==1 は財布素通り |
| ベルトのドラッグ設置 | 生存 | 同じプロトコル・セル毎に財布を通る |
| ブループリント貼り付け | 生存 | 同じ `va:placeBlock` |
| Ctrl+Z 取消 | 生存 | 撤去経路で残り+1（凝縮返却） |
| 無料設置デバッグ | 生存 | 既存の早期return前にコスト処理が無い |
| 電柱の自動接続 | 生存 | 予約コストは実消費分を渡す |

---

### Task 1: スキーマ `placementsPerCost` 追加・全JSON更新・バリデータ

**Files:**
- Modify: `VanillaSchema/blocks.yml:178-193`（requiredItems の直後に追加）
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs:8`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/BlockMasterUtil.cs:11-23, 177-212`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/BeltConveyorFamilyValidator.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`（全59要素に追加、GearBeltConveyorファミリー3件=3）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json`（全29要素=1）
- Modify: `mooresmaster/mooresmaster.SandBox/TestMod/blocks.json`（1要素=1）
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`（全73要素、12ブロックは3/5）
- Modify: `.moorestech-external-revisions.json`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltConveyorFamilyTest.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BlockPlacementsPerCostValidationTest.cs`（新規）

**Interfaces:**
- Produces: 生成物 `BlockMasterElement.PlacementsPerCost : int`

- [x] **Step 1: スキーマ追加**

`VanillaSchema/blocks.yml` の `requiredItems` ブロック（`- key: count / type: integer / default: 1` の直後、`- key: imagePath` の直前）に追加:

```yaml
    # 建設コスト1セットで設置できる個数。ベルトのみ>1。財布はベルトファミリー単位（ADR 0026）
    # Placements granted per one construction-cost set; >1 only for belts. Wallet is per belt family (ADR 0026)
    - key: placementsPerCost
      type: integer
      default: 1
```

`_CompileRequester.cs:8` の `dummyText` を新しいランダム文字列に変える（edit-schema スキル手順）。

- [x] **Step 2: 全 blocks.json を一括更新（スクリプト）**

```bash
cd "$(git rev-parse --show-toplevel)"  # 自worktreeのroot
python3 - <<'EOF'
import json, collections
FILES = [
  "moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json",
  "moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ServerData/mods/EditModeInPlayingTestMod/master/blocks.json",
  "mooresmaster/mooresmaster.SandBox/TestMod/blocks.json",
  "../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json",
]
# ベルトファミリー: 直線Guid → N（歯車=3・電気=5）。直線/上り/下りの3ブロックへ同値を書く
FAMILY_N = {
  "7743a779-1d62-4b94-b306-4a0670bd8b48": 3,  # 直線歯車ベルトコンベア
  "8388e6a8-8a2e-4b0d-b869-610c204889fa": 3,  # 鉄の歯車ベルトコンベア
  "019e0b27-1b23-765b-99c3-52d15f5cc74e": 5,  # ベルトコンベア
  "019eeaa5-e9b0-70bb-9ecd-13706d8a7bd4": 5,  # 高速ベルトコンベア
  "00000000-0000-0000-0000-000000000015": 3,  # forUnitTest GearBeltConveyor family
}
for path in FILES:
    d = json.load(open(path), object_pairs_hook=collections.OrderedDict)
    n_by_guid = {}
    for fam in d.get("beltConveyorFamilies", []):
        n = FAMILY_N.get(fam["straightBlockGuid"])
        if n is None: continue
        for k in ("straightBlockGuid", "upBlockGuid", "downBlockGuid"):
            if k in fam: n_by_guid[fam[k]] = n
    for b in d["data"]:
        new = collections.OrderedDict()
        for k, v in b.items():
            new[k] = v
            if k == "requiredItems":
                new["placementsPerCost"] = n_by_guid.get(b["blockGuid"], 1)
        if "placementsPerCost" not in new:
            new["placementsPerCost"] = n_by_guid.get(b["blockGuid"], 1)
        b.clear(); b.update(new)
    json.dump(d, open(path, "w"), ensure_ascii=False, indent=2)
    open(path, "a").write("\n")
    print(path, sum(1 for b in d["data"] if b["placementsPerCost"] > 1), "blocks >1")
EOF
```

Expected 出力末尾: forUnitTest `3 blocks >1`、EditModeInPlaying `0`、SandBox `0`、alpha `12 blocks >1`。
注意: 既存ファイルのインデント/改行末尾が変わっていないか `git diff --stat` で確認し、差分がフィールド追加のみであること。

- [x] **Step 3: 失敗するバリデータテストを書く**

`Tests/UnitTest/Game/BeltConveyorFamilyTest.cs` に追加（既存の `LoadBlocksJson`/`FindBlock` を使う）:

```csharp
        [Test]
        public void ファミリー内でplacementsPerCostが異なれば検証エラーになる()
        {
            var blocksJToken = LoadBlocksJson();
            var upBlockGuid = blocksJToken["beltConveyorFamilies"][2]["upBlockGuid"].Value<string>();
            FindBlock(blocksJToken, upBlockGuid)["placementsPerCost"] = 1;

            var logs = BeltConveyorFamilyValidator.Validate(new BlockMaster(blocksJToken).Blocks);

            StringAssert.Contains("placementsPerCost must match the family's straight block", logs);
        }

        [Test]
        public void ファミリー内でrequiredItemsが異なれば検証エラーになる()
        {
            var blocksJToken = LoadBlocksJson();
            var downBlockGuid = blocksJToken["beltConveyorFamilies"][2]["downBlockGuid"].Value<string>();
            FindBlock(blocksJToken, downBlockGuid)["requiredItems"][0]["count"] = 2;

            var logs = BeltConveyorFamilyValidator.Validate(new BlockMaster(blocksJToken).Blocks);

            StringAssert.Contains("requiredItems must match the family's straight block", logs);
        }
```

新規 `Tests/UnitTest/Game/BlockPlacementsPerCostValidationTest.cs`:

```csharp
using System.IO;
using Core.Master;
using Core.Master.Validator;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    public class BlockPlacementsPerCostValidationTest
    {
        [Test]
        public void placementsPerCostが0以下なら検証エラーになる()
        {
            var path = Path.Combine(TestModDirectory.ForUnitTestModDirectory, "mods", "forUnitTest", "master", "blocks.json");
            var blocksJToken = JToken.Parse(File.ReadAllText(path));
            blocksJToken["data"][0]["placementsPerCost"] = 0;

            BlockMasterUtil.Validate(new BlockMaster(blocksJToken).Blocks, out var logs);

            StringAssert.Contains("invalid PlacementsPerCost:0", logs);
        }
    }
}
```

- [x] **Step 4: コンパイルして生成物を確認 → テストが失敗することを確認**

Run: `uloop compile --project-path ./moorestech_client`（SourceGenerator再生成で `PlacementsPerCost` が生えること。エラー0）
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "BeltConveyorFamilyTest|BlockPlacementsPerCostValidationTest"`
Expected: 新規3本 FAIL（文字列が含まれない）

- [x] **Step 5: バリデータ実装**

`BlockMasterUtil.Validate` に `errorLogs += PlacementsPerCostValidation();` を `BlockRequiredItemsValidation()` の直後に追加し、ローカル関数を追加:

```csharp
            string PlacementsPerCostValidation()
            {
                // 0以下は設置ごとの消費が定義できないためマスタエラー
                // Non-positive values cannot define per-placement consumption, so treat them as master errors
                var logs = "";
                foreach (var block in blocks.Data)
                {
                    if (block.PlacementsPerCost <= 0)
                        logs += $"[BlockMaster] Name:{block.Name} has invalid PlacementsPerCost:{block.PlacementsPerCost}\n";
                }
                return logs;
            }
```

`BeltConveyorFamilyValidator.ValidateFamily` を、直線ブロックを基準にメンバーのコスト一致を検証する形へ拡張:

```csharp
            string ValidateFamily(BeltConveyorFamiliesElement family)
            {
                var familyLogs = "";
                familyLogs += ValidateMember(family.StraightBlockGuid, "straightBlockGuid");
                familyLogs += ValidateOptionalMember(family.UpBlockGuid, "upBlockGuid");
                familyLogs += ValidateOptionalMember(family.DownBlockGuid, "downBlockGuid");

                // 財布をファミリーで共有するため、直線基準で建設コストと設置数/1セットの一致を要求する
                // The wallet is shared per family, so require cost and placementsPerCost to match the straight block
                if (!elementByGuid.TryGetValue(family.StraightBlockGuid, out var straight)) return familyLogs;
                familyLogs += ValidateCostMatches(straight, family.UpBlockGuid);
                familyLogs += ValidateCostMatches(straight, family.DownBlockGuid);
                return familyLogs;
            }

            string ValidateCostMatches(BlockMasterElement straight, Guid? memberGuid)
            {
                if (!memberGuid.HasValue || !elementByGuid.TryGetValue(memberGuid.Value, out var member)) return "";
                var logs = "";
                if (member.PlacementsPerCost != straight.PlacementsPerCost)
                    logs += $"[BlockMaster] BeltConveyorFamily member {member.Name} placementsPerCost must match the family's straight block {straight.Name}\n";
                if (!SameRequiredItems(straight.RequiredItems, member.RequiredItems))
                    logs += $"[BlockMaster] BeltConveyorFamily member {member.Name} requiredItems must match the family's straight block {straight.Name}\n";
                return logs;
            }

            bool SameRequiredItems(ConstructionRequiredItemElement[] a, ConstructionRequiredItemElement[] b)
            {
                var aList = a ?? Array.Empty<ConstructionRequiredItemElement>();
                var bList = b ?? Array.Empty<ConstructionRequiredItemElement>();
                if (aList.Length != bList.Length) return false;
                foreach (var x in aList)
                {
                    var found = false;
                    foreach (var y in bList) found |= x.ItemGuid == y.ItemGuid && x.Count == y.Count;
                    if (!found) return false;
                }
                return true;
            }
```

ファイルが200行を超える場合は `SameRequiredItems` を同ディレクトリの `ConstructionRequiredItemsEquality.cs`（static）へ出す。

- [x] **Step 6: コンパイル → テストが通ることを確認**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "BeltConveyorFamilyTest|BlockPlacementsPerCostValidationTest|PlaceBlockProtocolTest"`
Expected: 全PASS（既存PlaceBlockProtocolTestもマスタロード成功で緑）

- [x] **Step 7: moorestech_master をコミットし、ピンを更新**

```bash
cd ../moorestech_master && git add server_v8/mods/moorestechAlphaMod_8/master/blocks.json && git commit -m "feat(master): blocks に placementsPerCost を追加しベルトを3/5にする（ADR 0026）" && git push && git rev-parse HEAD
cd - && python3 - <<'EOF'
import json,subprocess
sha=subprocess.check_output(["git","-C","../moorestech_master","rev-parse","HEAD"],text=True).strip()
p=".moorestech-external-revisions.json"; d=json.load(open(p))
for r in d["repositories"]:
    if r["key"]=="moorestech_master": r["commitHash"]=sha
json.dump(d,open(p,"w"),indent=4); open(p,"a").write("\n"); print(sha)
EOF
```

注意: masterブランチがpush保護ならブランチを切ってpushし、そのSHAをピンに書く（ピンSHAは必ずpush済みであること）。

- [x] **Step 8: コミット**

```bash
git add VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Core.Master moorestech_server/Assets/Scripts/Tests.Module moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest mooresmaster/mooresmaster.SandBox/TestMod/blocks.json .moorestech-external-revisions.json moorestech_server/Assets/Scripts/Tests/UnitTest/Game
git commit -m "feat(master): blocks に必須フィールド placementsPerCost を追加しファミリー一致を検証する"
```

---

### Task 2: `Game.Construction` アセンブリ — 財布util・残り設置数DataStore・セーブ配線

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Construction/Game.Construction.asmdef`
- Create: `moorestech_server/Assets/Scripts/Game.Construction/ConstructionWalletUtil.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Construction/RemainingPlacementCountChange.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Construction/IRemainingPlacementCountLookup.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Construction/IRemainingPlacementCountMutation.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Construction/RemainingPlacementCountDataStore.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Construction/PlayerRemainingPlacementCountSaveJsonObject.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Construction/RemainingPlacementCountEntrySaveJsonObject.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs:198-200`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Server.Boot.asmdef`（references に `"Game.Construction"`）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Game.SaveLoad.asmdef`（references に `"Game.Construction"`）
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/Server.Tests.asmdef`（references に `"Game.Construction"`）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/RemainingPlacementCountDataStoreTest.cs`（新規）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/RemainingPlacementCountSaveLoadTest.cs`（新規）

**Interfaces:**
- Produces:
  - `static BlockId ConstructionWalletUtil.ResolveWalletBlockId(BlockId blockId)`
  - `readonly struct RemainingPlacementCountChange(int PlayerId, BlockId WalletBlockId, int RemainingCount)`
  - `IRemainingPlacementCountLookup { IObservable<RemainingPlacementCountChange> OnRemainingCountChanged; int GetRemainingCount(int playerId, BlockId walletBlockId); IReadOnlyList<(BlockId walletBlockId, int remainingCount)> GetRemainingCounts(int playerId); }`
  - `IRemainingPlacementCountMutation { bool TryConsumeOne(int playerId, BlockId walletBlockId); void Refill(int playerId, BlockId walletBlockId, int placementsPerCost); bool ReturnOne(int playerId, BlockId walletBlockId, int placementsPerCost); }`
  - `RemainingPlacementCountDataStore : IRemainingPlacementCountLookup, IRemainingPlacementCountMutation` with `List<PlayerRemainingPlacementCountSaveJsonObject> GetSaveJsonObject()` / `void LoadRemainingCounts(List<PlayerRemainingPlacementCountSaveJsonObject> saveData)`
  - `WorldSaveAllInfoV1.RemainingPlacementCounts`（`[JsonProperty("remainingPlacementCounts")]`）

- [x] **Step 1: 失敗するテストを書く（DataStore単体）**

`Tests/UnitTest/Game/RemainingPlacementCountDataStoreTest.cs`:

```csharp
using System.Linq;
using Game.Construction;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UniRx;

namespace Tests.UnitTest.Game
{
    public class RemainingPlacementCountDataStoreTest
    {
        private const int PlayerId = 1;

        [Test]
        public void 財布キーはファミリー所属なら直線代表で非所属なら自分()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, ConstructionWalletUtil.ResolveWalletBlockId(ForUnitTestModBlockId.TestGearBeltConveyorUp));
            Assert.AreEqual(ForUnitTestModBlockId.GearBeltConveyor, ConstructionWalletUtil.ResolveWalletBlockId(ForUnitTestModBlockId.GearBeltConveyor));
            Assert.AreEqual(ForUnitTestModBlockId.MachineId, ConstructionWalletUtil.ResolveWalletBlockId(ForUnitTestModBlockId.MachineId));
        }

        [Test]
        public void 補充と消費と返却で残り設置数が遷移し変更が通知される()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<RemainingPlacementCountDataStore>();
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;
            var changes = 0;
            store.OnRemainingCountChanged.Subscribe(_ => changes++);

            // 残り0では消費できない
            // Nothing to consume while the wallet is empty
            Assert.IsFalse(store.TryConsumeOne(PlayerId, wallet));
            Assert.AreEqual(0, store.GetRemainingCount(PlayerId, wallet));

            store.Refill(PlayerId, wallet, 3);
            Assert.AreEqual(3, store.GetRemainingCount(PlayerId, wallet));
            Assert.IsTrue(store.TryConsumeOne(PlayerId, wallet));
            Assert.AreEqual(2, store.GetRemainingCount(PlayerId, wallet));

            // 返却は+1、Nに達したら0へ戻りtrue（凝縮返却の合図。設置と撤去が完全な逆操作になる閾値）
            // Return adds one; reaching N resets to zero and returns true (refund signal; the threshold that makes removal the exact inverse of placement)
            Assert.IsTrue(store.ReturnOne(PlayerId, wallet, 3));
            Assert.AreEqual(0, store.GetRemainingCount(PlayerId, wallet));

            // Nに達していなければ加算するだけでfalse
            // Below N it simply accumulates and returns false
            Assert.IsFalse(store.ReturnOne(PlayerId, wallet, 3));
            Assert.AreEqual(1, store.GetRemainingCount(PlayerId, wallet));
            Assert.AreEqual(4, changes);
        }

        [Test]
        public void 読み取りだけではセーブに現れず0件はセーブしない()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<RemainingPlacementCountDataStore>();
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;

            store.GetRemainingCount(PlayerId, wallet);
            Assert.IsEmpty(store.GetSaveJsonObject());

            store.Refill(PlayerId, wallet, 3);
            store.TryConsumeOne(PlayerId, wallet); store.TryConsumeOne(PlayerId, wallet); store.TryConsumeOne(PlayerId, wallet);
            Assert.IsEmpty(store.GetSaveJsonObject().SelectMany(p => p.Entries));
        }
    }
}
```

`Tests/CombinedTest/Game/RemainingPlacementCountSaveLoadTest.cs`:

```csharp
using Game.Construction;
using Game.SaveLoad.Interface;
using Game.SaveLoad.Json;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Game
{
    public class RemainingPlacementCountSaveLoadTest
    {
        private const int PlayerId = 0;

        [Test]
        public void セーブしてロードすると残り設置数が復元される()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var store = serviceProvider.GetService<RemainingPlacementCountDataStore>();
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;
            store.Refill(PlayerId, wallet, 3);
            store.TryConsumeOne(PlayerId, wallet);
            var saveJson = serviceProvider.GetService<AssembleSaveJsonText>().AssembleSaveJson();

            var (_, loadServiceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            (loadServiceProvider.GetService<IWorldSaveDataLoader>() as WorldLoaderFromJson).Load(saveJson);

            Assert.AreEqual(2, loadServiceProvider.GetService<IRemainingPlacementCountLookup>().GetRemainingCount(PlayerId, wallet));
        }
    }
}
```

- [x] **Step 2: テストがコンパイルエラーで失敗することを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `Game.Construction` 未定義のコンパイルエラー

- [x] **Step 3: アセンブリと型を作る**

`Game.Construction.asmdef`:

```json
{
    "name": "Game.Construction",
    "rootNamespace": "",
    "references": [
        "Core.Master",
        "Game.Block.Interface",
        "UniRx"
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

`ConstructionWalletUtil.cs`:

```csharp
using Core.Master;
using Game.Block.Interface.Extension;

namespace Game.Construction
{
    /// <summary>
    /// 残り設置数の財布キーを解決する。ベルトファミリーは直線代表へ正規化し、それ以外は自分自身（ADR 0026）
    /// Resolves the wallet key for remaining placements: belt families normalize to the straight block, others are themselves (ADR 0026)
    /// </summary>
    public static class ConstructionWalletUtil
    {
        public static BlockId ResolveWalletBlockId(BlockId blockId)
        {
            return BeltConveyorPlaceFamilyUtil.TryGetFamily(blockId, out var family) ? family.StraightBlockId : blockId;
        }
    }
}
```

`RemainingPlacementCountChange.cs`:

```csharp
using Core.Master;

namespace Game.Construction
{
    // 残り設置数の変更通知。財布単位で最新値を運ぶ
    // Change notification for remaining placements, carrying the latest value per wallet
    public readonly struct RemainingPlacementCountChange
    {
        public readonly int PlayerId;
        public readonly BlockId WalletBlockId;
        public readonly int RemainingCount;

        public RemainingPlacementCountChange(int playerId, BlockId walletBlockId, int remainingCount)
        {
            PlayerId = playerId;
            WalletBlockId = walletBlockId;
            RemainingCount = remainingCount;
        }
    }
}
```

`IRemainingPlacementCountLookup.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.Construction
{
    // 残り設置数の読み取り口。配信・初期データ同梱・返却判定はこちらへ依存する
    // The read side of remaining placements; publishers, initial-data bundlers, and refund checks depend on this
    public interface IRemainingPlacementCountLookup
    {
        IObservable<RemainingPlacementCountChange> OnRemainingCountChanged { get; }
        int GetRemainingCount(int playerId, BlockId walletBlockId);
        IReadOnlyList<(BlockId walletBlockId, int remainingCount)> GetRemainingCounts(int playerId);
    }
}
```

`IRemainingPlacementCountMutation.cs`:

```csharp
using Core.Master;

namespace Game.Construction
{
    // 残り設置数の変更口。設置・撤去プロトコルだけがこちらへ依存する
    // The write side of remaining placements; only the place/remove protocols depend on this
    public interface IRemainingPlacementCountMutation
    {
        // 残り>0なら1消費してtrue
        // Consumes one when remaining>0 and returns true
        bool TryConsumeOne(int playerId, BlockId walletBlockId);

        // 建設コスト1セット消費の対価として設置数/1セット分を補充する
        // Refills one set's worth of placements after one construction-cost set was consumed
        void Refill(int playerId, BlockId walletBlockId, int placementsPerCost);

        // 撤去で1戻す。設置数/1セットに達したら0へ戻しtrue（呼び手が1セット返却する）
        // Returns one on removal; reaching placementsPerCost resets to zero and returns true (caller refunds one set)
        bool ReturnOne(int playerId, BlockId walletBlockId, int placementsPerCost);
    }
}
```

`RemainingPlacementCountDataStore.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using UniRx;

namespace Game.Construction
{
    /// <summary>
    /// プレイヤー×財布の残り設置数。読み取りではレコードを作らず、0件はセーブしない（前例 HotbarAssignmentDatastore）
    /// Remaining placements per player x wallet; reads never create records and zero entries are not saved (precedent: HotbarAssignmentDatastore)
    /// </summary>
    public class RemainingPlacementCountDataStore : IRemainingPlacementCountLookup, IRemainingPlacementCountMutation
    {
        public IObservable<RemainingPlacementCountChange> OnRemainingCountChanged => _onRemainingCountChanged;
        private readonly Subject<RemainingPlacementCountChange> _onRemainingCountChanged = new();

        private readonly Dictionary<int, Dictionary<BlockId, int>> _remainingCounts = new();

        public int GetRemainingCount(int playerId, BlockId walletBlockId)
        {
            if (!_remainingCounts.TryGetValue(playerId, out var wallets)) return 0;
            return wallets.TryGetValue(walletBlockId, out var remaining) ? remaining : 0;
        }

        public IReadOnlyList<(BlockId walletBlockId, int remainingCount)> GetRemainingCounts(int playerId)
        {
            if (!_remainingCounts.TryGetValue(playerId, out var wallets)) return Array.Empty<(BlockId, int)>();
            return wallets.Where(pair => pair.Value > 0).Select(pair => (pair.Key, pair.Value)).ToList();
        }

        public bool TryConsumeOne(int playerId, BlockId walletBlockId)
        {
            var remaining = GetRemainingCount(playerId, walletBlockId);
            if (remaining <= 0) return false;
            Set(playerId, walletBlockId, remaining - 1);
            return true;
        }

        public void Refill(int playerId, BlockId walletBlockId, int placementsPerCost)
        {
            Set(playerId, walletBlockId, GetRemainingCount(playerId, walletBlockId) + placementsPerCost);
        }

        public bool ReturnOne(int playerId, BlockId walletBlockId, int placementsPerCost)
        {
            var returned = GetRemainingCount(playerId, walletBlockId) + 1;
            // 設置数/1セットに達した分は素材へ凝縮されるので財布からは消える
            // Reaching one set's worth condenses into materials, so it leaves the wallet
            var condensed = placementsPerCost <= returned;
            Set(playerId, walletBlockId, condensed ? 0 : returned);
            return condensed;
        }

        public List<PlayerRemainingPlacementCountSaveJsonObject> GetSaveJsonObject()
        {
            return _remainingCounts
                .Select(player => new PlayerRemainingPlacementCountSaveJsonObject(player.Key, player.Value
                    .Where(wallet => wallet.Value > 0)
                    .Select(wallet => new RemainingPlacementCountEntrySaveJsonObject(MasterHolder.BlockMaster.GetBlockMaster(wallet.Key).BlockGuid.ToString(), wallet.Value))
                    .ToList()))
                .Where(player => player.Entries.Count > 0)
                .ToList();
        }

        public void LoadRemainingCounts(List<PlayerRemainingPlacementCountSaveJsonObject> saveData)
        {
            _remainingCounts.Clear();
            foreach (var player in saveData)
            {
                foreach (var entry in player.Entries)
                {
                    // マスタから消えたブロックの財布は捨てる（形状不正で全体を落とさない）
                    // Drop wallets whose block vanished from the master so a stale save never aborts the load
                    if (!Guid.TryParse(entry.BlockGuid, out var blockGuid)) continue;
                    var blockId = MasterHolder.BlockMaster.GetBlockIdOrNull(blockGuid);
                    if (blockId == null || entry.Count <= 0) continue;
                    GetOrCreate(player.PlayerId)[blockId.Value] = entry.Count;
                }
            }
        }

        private void Set(int playerId, BlockId walletBlockId, int remaining)
        {
            GetOrCreate(playerId)[walletBlockId] = remaining;
            _onRemainingCountChanged.OnNext(new RemainingPlacementCountChange(playerId, walletBlockId, remaining));
        }

        private Dictionary<BlockId, int> GetOrCreate(int playerId)
        {
            if (_remainingCounts.TryGetValue(playerId, out var wallets)) return wallets;
            wallets = new Dictionary<BlockId, int>();
            _remainingCounts[playerId] = wallets;
            return wallets;
        }
    }
}
```

`PlayerRemainingPlacementCountSaveJsonObject.cs`:

```csharp
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Game.Construction
{
    public class PlayerRemainingPlacementCountSaveJsonObject
    {
        [JsonProperty("PlayerId")] public int PlayerId;
        [JsonProperty("Entries")] public List<RemainingPlacementCountEntrySaveJsonObject> Entries;

        public PlayerRemainingPlacementCountSaveJsonObject()
        {
        }

        public PlayerRemainingPlacementCountSaveJsonObject(int playerId, List<RemainingPlacementCountEntrySaveJsonObject> entries)
        {
            PlayerId = playerId;
            Entries = entries;
        }
    }
}
```

`RemainingPlacementCountEntrySaveJsonObject.cs`:

```csharp
using Newtonsoft.Json;

namespace Game.Construction
{
    // 財布ブロックはGuidで保存する（揮発BlockIdは保存しない）
    // The wallet block is saved as a GUID, never the volatile BlockId
    public class RemainingPlacementCountEntrySaveJsonObject
    {
        [JsonProperty("BlockGuid")] public string BlockGuid;
        [JsonProperty("Count")] public int Count;

        public RemainingPlacementCountEntrySaveJsonObject()
        {
        }

        public RemainingPlacementCountEntrySaveJsonObject(string blockGuid, int count)
        {
            BlockGuid = blockGuid;
            Count = count;
        }
    }
}
```

- [x] **Step 4: DI登録とセーブ配線**

`MoorestechServerDIContainerGenerator.cs` の Hotbar 3行の直後:

```csharp
            services.AddSingleton<RemainingPlacementCountDataStore>();
            services.AddSingleton<IRemainingPlacementCountLookup>(provider => provider.GetRequiredService<RemainingPlacementCountDataStore>());
            services.AddSingleton<IRemainingPlacementCountMutation>(provider => provider.GetRequiredService<RemainingPlacementCountDataStore>());
```

`Server.Boot.asmdef` / `Game.SaveLoad.asmdef` / `Tests/Server.Tests.asmdef` の references に `"Game.Construction"` を追加（既存の `"Game.Hotbar"` の隣）。

`WorldSaveAllInfoV1.cs`: ctor引数 `List<PlayerHotbarSaveJsonObject> hotbarAssignments,` の直後に `List<PlayerRemainingPlacementCountSaveJsonObject> remainingPlacementCounts,` を追加し、本体に `RemainingPlacementCounts = remainingPlacementCounts ?? new List<PlayerRemainingPlacementCountSaveJsonObject>();`、プロパティ `[JsonProperty("remainingPlacementCounts")] public List<PlayerRemainingPlacementCountSaveJsonObject> RemainingPlacementCounts { get; set; }` を `HotbarAssignments` の直後に追加。`using Game.Construction;` を追加。

`AssembleSaveJsonText.cs`: フィールド `private readonly RemainingPlacementCountDataStore _remainingPlacementCountDataStore;`、ctor引数 `RemainingPlacementCountDataStore remainingPlacementCountDataStore`（`hotbarAssignmentDatastore` の直後）と代入、`AssembleSaveJson` の `_hotbarAssignmentDatastore.GetSaveJsonObject(),` の直後に `_remainingPlacementCountDataStore.GetSaveJsonObject(),`。

`WorldLoaderFromJson.cs`: 同様にフィールド・ctor引数（`hotbarAssignmentDatastore` の直後）・代入を追加し、`Load` 末尾（`_hotbarAssignmentDatastore.LoadHotbar(...)` の直後）に:

```csharp
            // 残り設置数はマスタだけに依存するためロード順の制約なし
            // Remaining placements depend only on the master, so there is no load-order constraint
            _remainingPlacementCountDataStore.LoadRemainingCounts(load.RemainingPlacementCounts);
```

- [x] **Step 5: コンパイル → テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "RemainingPlacementCountDataStoreTest|RemainingPlacementCountSaveLoadTest|HotbarSaveLoadTest"`
Expected: 全PASS

- [x] **Step 6: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.Construction moorestech_server/Assets/Scripts/Server.Boot moorestech_server/Assets/Scripts/Game.SaveLoad moorestech_server/Assets/Scripts/Tests
git commit -m "feat(server): 残り設置数DataStoreと財布util（Game.Construction）を追加しセーブへ配線する"
```

---

### Task 3: 同期3点セット（サーバー側）— イベントパケットとhandshake同梱

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/RemainingPlacementCountChangedEventPacket.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Event/Server.Event.asmdef`（references に `"Game.Construction"`）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs:268`（`HotbarUpdateEventPacket` の隣に登録）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/InitialHandshakeProtocol.cs:30-41, 78-88, 128-155`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/Server.Protocol.asmdef`（references に `"Game.Construction"`）
- Modify: `moorestech_client/Assets/Scripts/Client.DebugSystem/CharacterTestDebug.cs:46`（ctor引数追加に追従）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/RemainingPlacementCountEventPacketTest.cs`（新規）

**Interfaces:**
- Produces:
  - `RemainingPlacementCountChangedEventPacket.EventTag = "va:event:remainingPlacementCountChanged"`
  - `RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack { [Key(0)] int WalletBlockId; [Key(1)] int RemainingCount; }`
  - `InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack.RemainingPlacementCounts : RemainingPlacementCountMessagePack[]`（`[Key(8)]`、ctor末尾引数）

- [x] **Step 1: 失敗するテストを書く**

`Tests/CombinedTest/Server/PacketTest/RemainingPlacementCountEventPacketTest.cs`:

```csharp
using System.Linq;
using Game.Construction;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RemainingPlacementCountEventPacketTest
    {
        private const int PlayerId = 1;

        [Test]
        public void 残り設置数の変更が該当プレイヤーへイベント配信される()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;

            serviceProvider.GetService<IRemainingPlacementCountMutation>().Refill(PlayerId, wallet, 3);

            var events = sink.TakeAll().Where(e => e.Tag == RemainingPlacementCountChangedEventPacket.EventTag).ToList();
            Assert.AreEqual(1, events.Count);
            var data = MessagePackSerializer.Deserialize<RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack>(events[0].Payload);
            Assert.AreEqual(wallet.AsPrimitive(), data.WalletBlockId);
            Assert.AreEqual(3, data.RemainingCount);
        }

        [Test]
        public void 初期ハンドシェイクに残り設置数が同梱される()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var wallet = ForUnitTestModBlockId.GearBeltConveyor;
            serviceProvider.GetService<IRemainingPlacementCountMutation>().Refill(PlayerId, wallet, 3);

            var payload = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(PlayerId, "test"));
            var response = (InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack)packet.GetPacketResponse(payload, new PacketResponseContext(null))[0];

            Assert.AreEqual(1, response.RemainingPlacementCounts.Length);
            Assert.AreEqual(wallet.AsPrimitive(), response.RemainingPlacementCounts[0].WalletBlockId);
            Assert.AreEqual(3, response.RemainingPlacementCounts[0].RemainingCount);
        }
    }
}
```

注: 前例 `InitialHandshakeProtocolTest.cs:36`（`GetPacketResponse(...)[0]` をキャスト、`PacketResponseContext(null)` で可）。

- [x] **Step 2: コンパイルエラーで失敗を確認**

Run: `uloop compile --project-path ./moorestech_client` → `RemainingPlacementCountChangedEventPacket` 未定義

- [x] **Step 3: イベントパケット実装**

`Server.Event/EventReceive/RemainingPlacementCountChangedEventPacket.cs`:

```csharp
using System;
using Game.Construction;
using MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    /// <summary>
    /// 残り設置数の変更を該当プレイヤーへ通知する。財布1件の最新値だけを送る
    /// Notifies the owning player of a remaining-placement change; carries the latest value of one wallet
    /// </summary>
    public class RemainingPlacementCountChangedEventPacket : IBootInitializable
    {
        public const string EventTag = "va:event:remainingPlacementCountChanged";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IRemainingPlacementCountLookup _remainingPlacementCountLookup;

        public RemainingPlacementCountChangedEventPacket(EventProtocolProvider eventProtocolProvider, IRemainingPlacementCountLookup remainingPlacementCountLookup)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _remainingPlacementCountLookup = remainingPlacementCountLookup;
        }

        public void Load()
        {
            _remainingPlacementCountLookup.OnRemainingCountChanged.Subscribe(OnRemainingCountChanged);
        }

        private void OnRemainingCountChanged(RemainingPlacementCountChange change)
        {
            var payload = MessagePackSerializer.Serialize(new RemainingPlacementCountMessagePack(change.WalletBlockId.AsPrimitive(), change.RemainingCount));
            _eventProtocolProvider.AddEvent(change.PlayerId, EventTag, payload);
        }

        #region MessagePack

        // handshake同梱にも使う共通型
        // Shared shape also bundled into the initial handshake
        [MessagePackObject]
        public class RemainingPlacementCountMessagePack
        {
            [Key(0)] public int WalletBlockId { get; set; }
            [Key(1)] public int RemainingCount { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RemainingPlacementCountMessagePack() { }

            public RemainingPlacementCountMessagePack(int walletBlockId, int remainingCount)
            {
                WalletBlockId = walletBlockId;
                RemainingCount = remainingCount;
            }
        }

        #endregion
    }
}
```

`MoorestechServerDIContainerGenerator.cs`: `services.AddSingleton<HotbarUpdateEventPacket>();` の直後に `services.AddSingleton<RemainingPlacementCountChangedEventPacket>();`。eager init が `IBootInitializable` 登録の列挙で行われているか（`:305` 付近）を確認し、Hotbar と同じ経路で `Load()` が呼ばれることを確かめる。

- [x] **Step 4: handshake同梱**

`InitialHandshakeProtocol.cs`:
- フィールド `private readonly IRemainingPlacementCountLookup _remainingPlacementCountLookup;` とctorで `serviceProvider.GetService<IRemainingPlacementCountLookup>()`。
- `CreateResponse` 内、hotbar取得の直後:

```csharp
                // 残り設置数も初期データとして同梱し、ログイン直後からプレビュー・表示に使えるようにする
                // Bundle remaining placements as initial data so previews and displays work right after login
                var remainingPlacementCounts = _remainingPlacementCountLookup.GetRemainingCounts(data.PlayerId)
                    .Select(pair => new RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack(pair.walletBlockId.AsPrimitive(), pair.remainingCount))
                    .ToArray();

                return new ResponseInitialHandshakeMessagePack(playerPos, ridingTarget, ridingSeatIndex, itemStackLevels, hotbarAssignments, remainingPlacementCounts);
```

- `ResponseInitialHandshakeMessagePack` に `[Key(8)] public RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack[] RemainingPlacementCounts { get; set; }` とctor末尾引数 `RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack[] remainingPlacementCounts` を追加し代入。`using Server.Event.EventReceive;` を追加（既に `ItemStackLevelMessagePack` で参照しているはずなので確認）。
- `CharacterTestDebug.cs:46` のctor呼び出し末尾に `Array.Empty<RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack>()` を追加。

- [x] **Step 5: コンパイル → テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "RemainingPlacementCountEventPacketTest|HotbarProtocolTest|InitialHandshake"`
Expected: 全PASS

- [x] **Step 6: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Event moorestech_server/Assets/Scripts/Server.Protocol moorestech_server/Assets/Scripts/Server.Boot moorestech_client/Assets/Scripts/Client.DebugSystem moorestech_server/Assets/Scripts/Tests
git commit -m "feat(server): 残り設置数のイベントパケットとhandshake同梱を追加する"
```

---

### Task 4: 設置時の財布課金（PlaceBlockProtocol）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/RemainingPlacementChargeService.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs:28-35, 85-118`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/PlaceBlockProtocolTest.cs`（または200行超過回避のため新規 `PlaceBlockRemainingPlacementTest.cs`）

**Interfaces:**
- Consumes: `IRemainingPlacementCountLookup` / `IRemainingPlacementCountMutation` / `ConstructionWalletUtil`（Task 2）
- Produces:
  - `static (ItemId itemId, int count)[] RemainingPlacementChargeService.ResolveCostToConsume(BlockMasterElement blockMaster, int playerId, IRemainingPlacementCountLookup lookup)` — 財布で賄えるなら空配列
  - `static void RemainingPlacementChargeService.Charge(BlockMasterElement blockMaster, int playerId, IRemainingPlacementCountMutation mutation, IReadOnlyList<(ItemId itemId, int count)> costToConsume, IOpenableInventory inventory)` — 素材消費＋財布更新

- [x] **Step 1: 失敗するテストを書く**

新規 `Tests/CombinedTest/Server/PacketTest/PlaceBlockRemainingPlacementTest.cs`:

```csharp
using System;
using Game.Construction;
using Game.Context;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    /// 設置数/1セット=3の歯車ベルトで財布課金（ADR 0026）を検証する
    /// Verifies wallet charging (ADR 0026) with the gear belt whose placementsPerCost is 3
    /// </summary>
    public class PlaceBlockRemainingPlacementTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [Test]
        public void 一本ずつ3回置いても建設コストは1セットしか消費されない()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);
            var belt = ForUnitTestModBlockId.GearBeltConveyor;
            UnlockBlock(serviceProvider, belt);
            SetItem(inventory, 0, Material1Guid, 2);
            SetItem(inventory, 1, Material2Guid, 2);
            var lookup = serviceProvider.GetService<IRemainingPlacementCountLookup>();

            packet.GetPacketResponse(CreatePlaceBlockPayload(belt, (10, 0)), new PacketResponseContext(null));
            Assert.AreEqual(1, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(2, lookup.GetRemainingCount(PlayerId, belt));

            packet.GetPacketResponse(CreatePlaceBlockPayload(belt, (11, 0)), new PacketResponseContext(null));
            packet.GetPacketResponse(CreatePlaceBlockPayload(belt, (12, 0)), new PacketResponseContext(null));
            Assert.AreEqual(1, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, GetItemCount(inventory, Material2Guid));
            Assert.AreEqual(0, lookup.GetRemainingCount(PlayerId, belt));
            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(12, 0)));
        }

        [Test]
        public void 残り0で素材もなければ設置されず財布も変わらない()
        {
            var (packet, serviceProvider) = CreateServer();
            var belt = ForUnitTestModBlockId.GearBeltConveyor;
            UnlockBlock(serviceProvider, belt);

            packet.GetPacketResponse(CreatePlaceBlockPayload(belt, (10, 0)), new PacketResponseContext(null));

            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(10, 0)));
            Assert.AreEqual(0, serviceProvider.GetService<IRemainingPlacementCountLookup>().GetRemainingCount(PlayerId, belt));
        }

        [Test]
        public void 上り下りは直線と同じ財布を使う()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);
            var straight = ForUnitTestModBlockId.GearBeltConveyor;
            UnlockBlock(serviceProvider, straight);
            SetItem(inventory, 0, Material1Guid, 1);
            SetItem(inventory, 1, Material2Guid, 1);
            var lookup = serviceProvider.GetService<IRemainingPlacementCountLookup>();

            packet.GetPacketResponse(CreatePlaceBlockPayload(straight, (10, 0)), new PacketResponseContext(null));
            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.TestGearBeltConveyorUp, (11, 0)), new PacketResponseContext(null));

            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(11, 0)));
            Assert.AreEqual(0, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, lookup.GetRemainingCount(PlayerId, straight));
            Assert.AreEqual(0, lookup.GetRemainingCount(PlayerId, ForUnitTestModBlockId.TestGearBeltConveyorUp));
        }

        [Test]
        public void ドラッグ5本は1セットと残り1の消費でセット2つ分になる()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);
            var belt = ForUnitTestModBlockId.GearBeltConveyor;
            UnlockBlock(serviceProvider, belt);
            SetItem(inventory, 0, Material1Guid, 2);
            SetItem(inventory, 1, Material2Guid, 2);

            packet.GetPacketResponse(CreatePlaceBlockPayload(belt, (10, 0), (11, 0), (12, 0), (13, 0), (14, 0)), new PacketResponseContext(null));

            // 5本 = 1セット(3本) + 2本目のセット開始 → 素材2セット消費・残り1
            // Five cells = one full set (3) + the start of a second set → two sets consumed, one remaining
            Assert.AreEqual(0, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, serviceProvider.GetService<IRemainingPlacementCountLookup>().GetRemainingCount(PlayerId, belt));
            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(14, 0)));
        }
    }
}
```

注: `UnlockBlock` はファミリー直線を解放する（上り下りは直線のunlock状態で判定される）。`ForUnitTestModBlockId.GearBeltConveyor` が `initialUnlocked` ならUnlockは冪等で問題ない。

- [x] **Step 2: 実行して失敗を確認**

Run: `uloop compile --project-path ./moorestech_client` → PASS、Run tests → `一本ずつ3回置いても…` が素材消費3セットでFAIL

- [x] **Step 3: サービス実装**

`Server.Protocol/PacketResponse/Util/Construction/RemainingPlacementChargeService.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Inventory;
using Core.Master;
using Game.Construction;
using Mooresmaster.Model.BlocksModule;

namespace Server.Protocol.PacketResponse.Util.Construction
{
    /// <summary>
    /// 残り設置数の財布を見て、このセルで実際に消費する建設コストを決め、設置後に財布と素材を更新する（ADR 0026）
    /// Decides the construction cost actually consumed for a cell from the remaining-placement wallet, then updates wallet and materials after placement (ADR 0026)
    /// </summary>
    public static class RemainingPlacementChargeService
    {
        // 設置数/1セット=1は財布を素通りし全額消費、財布に残りがあれば消費ゼロ
        // placementsPerCost==1 bypasses the wallet and consumes the full cost; a non-empty wallet consumes nothing
        public static (ItemId itemId, int count)[] ResolveCostToConsume(BlockMasterElement blockMaster, int playerId, IRemainingPlacementCountLookup lookup)
        {
            var fullCost = ConstructionCostService.ToItemCounts(blockMaster.RequiredItems);
            if (blockMaster.PlacementsPerCost <= 1 || fullCost.Length == 0) return fullCost;

            var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid));
            return 0 < lookup.GetRemainingCount(playerId, walletBlockId) ? Array.Empty<(ItemId, int)>() : fullCost;
        }

        public static void Charge(BlockMasterElement blockMaster, int playerId, IRemainingPlacementCountMutation mutation, IReadOnlyList<(ItemId itemId, int count)> costToConsume, IOpenableInventory inventory)
        {
            ConstructionCostService.ConsumeRequiredItems(costToConsume, inventory);
            if (blockMaster.PlacementsPerCost <= 1) return;

            // 素材を消費したセルは設置数/1セット分を補充してから1消費する（残り=N-1）
            // A cell that consumed materials refills one set's worth and then consumes one (remaining = N-1)
            var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid));
            if (0 < costToConsume.Count) mutation.Refill(playerId, walletBlockId, blockMaster.PlacementsPerCost);
            mutation.TryConsumeOne(playerId, walletBlockId);
        }
    }
}
```

`PlaceBlockProtocol.cs`:
- フィールド `private readonly IRemainingPlacementCountLookup _remainingPlacementCountLookup; private readonly IRemainingPlacementCountMutation _remainingPlacementCountMutation;` をctorで解決。`using Game.Construction;`。
- `PlaceBlock` 内のコスト判定を差し替え:

```csharp
                // 財布で賄えるセルは消費ゼロ、それ以外は全額。不足セルはスキップ
                // Wallet-covered cells consume nothing, others the full cost; skip cells that cannot be covered
                var inventory = inventoryData.MainOpenableInventory;
                var costItemCounts = RemainingPlacementChargeService.ResolveCostToConsume(blockMaster, data.PlayerId, _remainingPlacementCountLookup);
                if (!ConstructionCostService.HasRequiredItems(costItemCounts, inventory.InventoryItems)) { costShortageCount++; return; }
```

（`costItemCounts` は従来どおり電線自動接続の予約に渡る。財布で賄うセルは空配列なので予約なし）
- `ConstructionCostService.ConsumeRequiredItems(costItemCounts, inventory);` を `RemainingPlacementChargeService.Charge(blockMaster, data.PlayerId, _remainingPlacementCountMutation, costItemCounts, inventory);` に置換。

- [x] **Step 4: コンパイル → テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlaceBlockRemainingPlacementTest|PlaceBlockProtocolTest|ElectricWireAutoConnectPlaceTest"`
Expected: 全PASS（既存テストは placementsPerCost=1 のブロックなので挙動不変）

- [x] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol moorestech_server/Assets/Scripts/Tests
git commit -m "feat(server): 設置時に残り設置数の財布で建設コストを課金する"
```

---

### Task 5: 撤去時の凝縮返却（RemoveBlockProtocol）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RemoveBlockProtocol.cs:24-52, 80-92`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/Util/Construction/RemainingPlacementChargeService.cs`（返却判定を追加）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/RemoveBlockRemainingPlacementTest.cs`（新規）

**Interfaces:**
- Produces:
  - `static bool RemainingPlacementChargeService.WouldCondenseOnReturn(BlockMasterElement blockMaster, int playerId, IRemainingPlacementCountLookup lookup)` — 撤去すると1セット返却になるか（N==1は常にtrue）
  - `static void RemainingPlacementChargeService.ReturnOne(BlockMasterElement blockMaster, int playerId, IRemainingPlacementCountMutation mutation)` — N>1のときだけ財布+1（凝縮時は0へ）

- [ ] **Step 1: 失敗するテストを書く**

`Tests/CombinedTest/Server/PacketTest/RemoveBlockRemainingPlacementTest.cs`:

```csharp
using System;
using Game.Construction;
using Game.Context;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;
using static Tests.CombinedTest.Server.PacketTest.PlaceBlockProtocolTestSupport;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RemoveBlockRemainingPlacementTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [Test]
        public void 三本置いて三本壊すと建設コスト1セットが戻る()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);
            var belt = ForUnitTestModBlockId.GearBeltConveyor;
            UnlockBlock(serviceProvider, belt);
            SetItem(inventory, 0, Material1Guid, 1);
            SetItem(inventory, 1, Material2Guid, 1);
            packet.GetPacketResponse(CreatePlaceBlockPayload(belt, (10, 0), (11, 0), (12, 0)), new PacketResponseContext(null));
            Assert.AreEqual(0, GetItemCount(inventory, Material1Guid));
            var lookup = serviceProvider.GetService<IRemainingPlacementCountLookup>();

            Remove(packet, new Vector3Int(10, 0));
            Assert.AreEqual(0, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, lookup.GetRemainingCount(PlayerId, belt));
            Remove(packet, new Vector3Int(11, 0));
            Assert.AreEqual(2, lookup.GetRemainingCount(PlayerId, belt));
            Remove(packet, new Vector3Int(12, 0));

            // 3本目で設置数/1セットに達し、素材1セットへ凝縮して返る
            // The third removal reaches one set's worth and condenses into one set of materials
            Assert.AreEqual(1, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, GetItemCount(inventory, Material2Guid));
            Assert.AreEqual(0, lookup.GetRemainingCount(PlayerId, belt));
        }

        [Test]
        public void 凝縮返却が入り切らなければ撤去も財布も変わらない()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);
            var belt = ForUnitTestModBlockId.GearBeltConveyor;
            UnlockBlock(serviceProvider, belt);
            SetItem(inventory, 0, Material1Guid, 1);
            SetItem(inventory, 1, Material2Guid, 1);
            packet.GetPacketResponse(CreatePlaceBlockPayload(belt, (10, 0)), new PacketResponseContext(null));
            var mutation = serviceProvider.GetService<IRemainingPlacementCountMutation>();
            mutation.TryConsumeOne(PlayerId, belt); mutation.TryConsumeOne(PlayerId, belt); // 残り0にする

            // 全スロットを別アイテムで埋めて返却不能にする
            // Fill every slot with another item so the refund cannot be inserted
            var filler = MasterHolder_ItemId(Guid.Parse("00000000-0000-0000-1234-000000000005"));
            for (var i = 0; i < inventory.GetSlotSize(); i++) inventory.SetItem(i, ServerContext.ItemStackFactory.Create(filler, 1));
            mutation.Refill(PlayerId, belt, 2); // 残り2 → 次の撤去で凝縮

            Remove(packet, new Vector3Int(10, 0));

            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(new Vector3Int(10, 0)));
            Assert.AreEqual(2, serviceProvider.GetService<IRemainingPlacementCountLookup>().GetRemainingCount(PlayerId, belt));
        }

        [Test]
        public void 設置数1のブロックは従来どおり全額返却される()
        {
            var (packet, serviceProvider) = CreateServer();
            var inventory = GetInventory(serviceProvider);
            SetItem(inventory, 0, Material1Guid, 2);
            SetItem(inventory, 1, Material2Guid, 1);
            packet.GetPacketResponse(CreatePlaceBlockPayload(ForUnitTestModBlockId.BlockId, (10, 0)), new PacketResponseContext(null));

            Remove(packet, new Vector3Int(10, 0));

            Assert.AreEqual(2, GetItemCount(inventory, Material1Guid));
            Assert.AreEqual(1, GetItemCount(inventory, Material2Guid));
        }

        private static void Remove(PacketResponseCreator packet, Vector3Int pos)
        {
            var payload = MessagePackSerializer.Serialize(new RemoveBlockProtocol.RemoveBlockProtocolMessagePack(PlayerId, pos));
            packet.GetPacketResponse(payload, new PacketResponseContext(null));
        }

        private static global::Core.Master.ItemId MasterHolder_ItemId(Guid itemGuid) => global::Core.Master.MasterHolder.ItemMaster.GetItemId(itemGuid);
    }
}
```

注: `RemoveBlockProtocolMessagePack` のctorシグネチャは `RemoveBlockProtocolTest.cs` の `RemoveBlock(new Vector3Int(0, 0), PlayerId)` ヘルパ（同ファイル末尾）に倣い、実際の引数順を確認して合わせる。

- [ ] **Step 2: 失敗確認**

Run tests → `三本置いて三本壊すと…` が1本目で全額返却されFAIL

- [ ] **Step 3: 実装**

`RemainingPlacementChargeService.cs` に追加:

```csharp
        // 撤去がこのセルで素材1セットの返却になるか。設置数/1セット=1は常に全額返却
        // Whether this removal refunds one set of materials; placementsPerCost==1 always refunds in full
        public static bool WouldCondenseOnReturn(BlockMasterElement blockMaster, int playerId, IRemainingPlacementCountLookup lookup)
        {
            if (blockMaster.PlacementsPerCost <= 1) return true;
            var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid));
            return blockMaster.PlacementsPerCost <= lookup.GetRemainingCount(playerId, walletBlockId) + 1;
        }

        public static void ReturnOne(BlockMasterElement blockMaster, int playerId, IRemainingPlacementCountMutation mutation)
        {
            if (blockMaster.PlacementsPerCost <= 1) return;
            var walletBlockId = ConstructionWalletUtil.ResolveWalletBlockId(MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid));
            mutation.ReturnOne(playerId, walletBlockId, blockMaster.PlacementsPerCost);
        }
```

`RemoveBlockProtocol.cs`:
- フィールド `_remainingPlacementCountLookup` / `_remainingPlacementCountMutation` をctorで解決。`using Game.Construction; using Server.Protocol.PacketResponse.Util.Construction;`（後者は既存かも）。
- `GetRefundItems` の建設コスト返却を:

```csharp
                // 建設コストは財布が1セット分に達する撤去でのみ返る（設置数/1セット=1は毎回）
                // The construction cost returns only when this removal completes one set's worth in the wallet (every time when placementsPerCost==1)
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(block.BlockId);
                if (blockMaster.RequiredItems != null && blockMaster.RequiredItems.Length != 0
                    && RemainingPlacementChargeService.WouldCondenseOnReturn(blockMaster, data.PlayerId, _remainingPlacementCountLookup))
                {
                    result.AddRange(ConstructionCostService.CreateRefundItems(ConstructionCostService.ToItemCounts(blockMaster.RequiredItems)));
                }
```

- 削除処理の直後（`InsertItemsToPlayerInventory(refundItems);` の前）に財布を更新:

```csharp
            // 撤去確定後に財布へ1戻す（凝縮時は0へ戻り、返却分は上で確保済み）
            // After removal is final, return one to the wallet (condensing resets it; the refund was reserved above)
            RemainingPlacementChargeService.ReturnOne(MasterHolder.BlockMaster.GetBlockMaster(block.BlockId), data.PlayerId, _remainingPlacementCountMutation);
```

`RemoveBlockProtocol.cs` が200行を超える場合は `GetRefundItems` を `Util/Construction/BlockRefundItemsCollector.cs`（static）へ切り出す。

- [ ] **Step 4: コンパイル → テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "RemoveBlockRemainingPlacementTest|RemoveBlockProtocolTest|PlaceBlockRemainingPlacementTest"`
Expected: 全PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol moorestech_server/Assets/Scripts/Tests
git commit -m "feat(server): 撤去時に残り設置数を戻し1セット分で建設コストを凝縮返却する"
```

---

### Task 6: クライアント同期 — 残り設置数モデルとイベント購読・初期データ適用

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Construction/ClientRemainingPlacementCountDatastore.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Construction/RemainingPlacementCountEventHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Client.Game.asmdef`（references に `"Game.Construction"`）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Tests.asmdef`（references に `"Game.Construction"`）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/InitialHandshakeResponse.cs:29, 54`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs:182-183`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/MainGameInitializationFinalizer.cs:45`

**Interfaces:**
- Produces:
  - `ClientRemainingPlacementCountDatastore { int GetRemainingCount(BlockId walletBlockId); IObservable<Unit> OnChanged; void ApplyAll(RemainingPlacementCountMessagePack[]); void Apply(int walletBlockId, int remainingCount); }`
  - `InitialHandshakeResponse.RemainingPlacementCounts : RemainingPlacementCountMessagePack[]`

- [ ] **Step 1: 実装（表示モデルのため単体テストは Task 7 の計算テストで担保）**

`ClientRemainingPlacementCountDatastore.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using UniRx;
using static Server.Event.EventReceive.RemainingPlacementCountChangedEventPacket;

namespace Client.Game.InGame.Construction
{
    /// <summary>
    ///     残り設置数の参照モデル(非MonoBehaviour)。購読・初期データからのみ更新する（前例 ClientHotbarDatastore）
    ///     Client-side model of remaining placements; updated only from the subscription/initial data (precedent: ClientHotbarDatastore)
    /// </summary>
    public class ClientRemainingPlacementCountDatastore
    {
        public IObservable<Unit> OnChanged => _onChanged;
        private readonly Subject<Unit> _onChanged = new();
        private readonly Dictionary<BlockId, int> _remainingCounts = new();

        public int GetRemainingCount(BlockId walletBlockId)
        {
            return _remainingCounts.TryGetValue(walletBlockId, out var remaining) ? remaining : 0;
        }

        public void ApplyAll(RemainingPlacementCountMessagePack[] counts)
        {
            _remainingCounts.Clear();
            foreach (var count in counts) _remainingCounts[new BlockId(count.WalletBlockId)] = count.RemainingCount;
            _onChanged.OnNext(Unit.Default);
        }

        public void Apply(int walletBlockId, int remainingCount)
        {
            _remainingCounts[new BlockId(walletBlockId)] = remainingCount;
            _onChanged.OnNext(Unit.Default);
        }
    }
}
```

`RemainingPlacementCountEventHandler.cs`:

```csharp
using Client.Network.API;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;

namespace Client.Game.InGame.Construction
{
    /// <summary>
    ///     残り設置数の変更イベントを購読しモデルへ適用する（前例 HotbarNetworkEventHandler）
    ///     Subscribes to remaining-placement change events and applies them to the model (precedent: HotbarNetworkEventHandler)
    /// </summary>
    public class RemainingPlacementCountEventHandler : IInitializable
    {
        private readonly IVanillaApiEvent _vanillaApiEvent;
        private readonly ClientRemainingPlacementCountDatastore _datastore;

        public RemainingPlacementCountEventHandler(IVanillaApiEvent vanillaApiEvent, ClientRemainingPlacementCountDatastore datastore)
        {
            _vanillaApiEvent = vanillaApiEvent;
            _datastore = datastore;
        }

        public void Initialize()
        {
            _vanillaApiEvent.SubscribeEventResponse(RemainingPlacementCountChangedEventPacket.EventTag, OnChanged);
        }

        private void OnChanged(byte[] payload)
        {
            var packet = MessagePackSerializer.Deserialize<RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack>(payload);
            _datastore.Apply(packet.WalletBlockId, packet.RemainingCount);
        }
    }
}
```

`InitialHandshakeResponse.cs`: `public RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack[] RemainingPlacementCounts { get; }` とctorで `RemainingPlacementCounts = initialHandshake.RemainingPlacementCounts;`（`using Server.Event.EventReceive;`）。

`MainGameStarter.cs` の Hotbar 2行の直後:

```csharp
            // 残り設置数モデルと更新購読
            // Remaining-placement model and its update-event subscription
            builder.Register<ClientRemainingPlacementCountDatastore>(Lifetime.Singleton);
            builder.RegisterEntryPoint<RemainingPlacementCountEventHandler>();
```

`MainGameInitializationFinalizer.cs` の hotbar 適用直後:

```csharp
            // 残り設置数もhandshake同梱。イベント購読開始前に適用する
            // Remaining placements ride along with the handshake too; applied before event dispatch starts
            resolver.Resolve<ClientRemainingPlacementCountDatastore>().ApplyAll(_serverResult.HandshakeResponse.RemainingPlacementCounts);
```

`BlockId` は `Core.Master` 名前空間の struct（`Core.Master/BlockMaster.cs:15`、`new BlockId(int)` 前例 `Client.Tests/.../GearChainPolePlaceExtendModeTest.cs:155`）。

- [ ] **Step 2: コンパイル**

Run: `uloop compile --project-path ./moorestech_client` → エラー0

- [ ] **Step 3: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game moorestech_client/Assets/Scripts/Client.Network moorestech_client/Assets/Scripts/Client.Starter moorestech_client/Assets/Scripts/Client.Tests/Tests.asmdef
git commit -m "feat(client): 残り設置数モデルとイベント購読・handshake適用を追加する"
```

---

### Task 7: 設置プレビューを残り設置数込みにする

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionCostPreviewCalculator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs:36-44, 214-235`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs:31-44, 127`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorCostPreviewMarker.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ConstructionCostPreviewCalculatorTest.cs`

**Interfaces:**
- Consumes: `ClientRemainingPlacementCountDatastore`（Task 6）、`ConstructionWalletUtil`（Task 2）
- Produces: `static int ConstructionCostPreviewCalculator.CalculateAffordablePlacementCount(ConstructionRequiredItemElement[] requiredItems, int placementsPerCost, int remainingCount, IEnumerable<IItemStack> inventoryItems)`
- Removes: `CalculateAffordableEntityCount`（ファミリー内コスト一致が保証されたため不要。既存テストがあれば置き換える）

- [ ] **Step 1: 失敗するテストを書く**

`ConstructionCostPreviewCalculatorTest.cs` に追加:

```csharp
        [Test]
        public void 残り設置数と買えるセット数から置ける数を算出する()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).RequiredItems;
            var factory = ServerContext.ItemStackFactory;
            // 素材は2セット分、残り設置数1 → 1 + 2×3 = 7
            // Materials cover two sets and one placement remains → 1 + 2×3 = 7
            var inventory = new List<global::Core.Item.Interface.IItemStack>
            {
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 2),
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), 2),
            };

            Assert.AreEqual(7, ConstructionCostPreviewCalculator.CalculateAffordablePlacementCount(requiredItems, 3, 1, inventory));
        }

        [Test]
        public void 設置数1なら従来のセル数計算と一致する()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;
            var factory = ServerContext.ItemStackFactory;
            var inventory = new List<global::Core.Item.Interface.IItemStack>
            {
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 5),
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), 2),
            };

            Assert.AreEqual(2, ConstructionCostPreviewCalculator.CalculateAffordablePlacementCount(requiredItems, 1, 0, inventory));
        }

        [Test]
        public void コスト未定義なら残り設置数に関わらずMaxValue()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).RequiredItems;
            Assert.AreEqual(int.MaxValue, ConstructionCostPreviewCalculator.CalculateAffordablePlacementCount(requiredItems, 1, 0, new List<global::Core.Item.Interface.IItemStack>()));
        }
```

（`CreateServer()` ヘルパが同ファイルに既にある前提。無ければ `new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));` を呼ぶローカル関数を追加）

- [ ] **Step 2: 失敗確認** — コンパイルエラー（メソッド未定義）

- [ ] **Step 3: 計算メソッド実装**

`ConstructionCostPreviewCalculator.cs` に追加（`CalculateAffordableEntityCount` は削除。参照が残っていれば次ステップで置換）:

```csharp
        /// <summary>
        /// 残り設置数 + 所持素材で買えるセット数×設置数/1セット を返す（ADR 0026）
        /// Returns remaining placements + affordable sets × placementsPerCost (ADR 0026)
        /// </summary>
        public static int CalculateAffordablePlacementCount(ConstructionRequiredItemElement[] requiredItems, int placementsPerCost, int remainingCount, IEnumerable<IItemStack> inventoryItems)
        {
            var affordableSets = CalculateAffordableCellCount(requiredItems, inventoryItems);
            if (affordableSets == int.MaxValue) return int.MaxValue;

            // 大量所持でのオーバーフローを避ける
            // Avoid overflow on very large holdings
            var total = remainingCount + (long)affordableSets * placementsPerCost;
            return total > int.MaxValue ? int.MaxValue : (int)total;
        }
```

- [ ] **Step 4: 設置システム2経路に配線**

`CommonBlockPlaceSystem.cs`: ctorに `ClientRemainingPlacementCountDatastore remainingPlacementCountDatastore` を追加しフィールド保持（VContainerが解決）。`MarkInsufficientItemPreviewsAsNotPlaceable` を:

```csharp
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(target.BlockId);
                var remaining = _remainingPlacementCountDatastore.GetRemainingCount(ConstructionWalletUtil.ResolveWalletBlockId(target.BlockId));
                var affordableCellCount = ConstructionCostPreviewCalculator.CalculateAffordablePlacementCount(blockMaster.RequiredItems, blockMaster.PlacementsPerCost, remaining, _localPlayerInventory);
```

`BeltConveyorPlaceSystem.cs`: 同様にctorへ `ClientRemainingPlacementCountDatastore` を追加し、`:127` の呼び出しを `BeltConveyorCostPreviewMarker.MarkInsufficientEntitiesAsNotPlaceable(_currentPlaceInfos, _localPlayerInventory, _remainingPlacementCountDatastore);` に変更。

`BeltConveyorCostPreviewMarker.cs` を置き換え:

```csharp
        public static void MarkInsufficientEntitiesAsNotPlaceable(List<PlaceInfo> currentPlaceInfos, IEnumerable<IItemStack> inventoryItems, ClientRemainingPlacementCountDatastore remainingPlacementCountDatastore)
        {
            if (DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement)) return;

            // ファミリー内は建設コストと設置数/1セットが一致する（マスタ検証済み）ので先頭の設置可セルを代表にする
            // Cost and placementsPerCost match within a family (validated at master load), so the first placeable cell is representative
            var representativeIndex = currentPlaceInfos.FindIndex(info => info.Placeable);
            if (representativeIndex < 0) return;
            var blockId = currentPlaceInfos[representativeIndex].BlockId;
            var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(blockId);
            var remaining = remainingPlacementCountDatastore.GetRemainingCount(ConstructionWalletUtil.ResolveWalletBlockId(blockId));
            var affordableCount = ConstructionCostPreviewCalculator.CalculateAffordablePlacementCount(blockMaster.RequiredItems, blockMaster.PlacementsPerCost, remaining, inventoryItems);

            var placeableCount = 0;
            for (var i = 0; i < currentPlaceInfos.Count; i++)
            {
                if (!currentPlaceInfos[i].Placeable) continue;
                placeableCount++;
                if (placeableCount > affordableCount) currentPlaceInfos[i].Placeable = false;
            }
        }
```

`using Client.Game.InGame.Construction; using Game.Construction;` を各ファイルに追加。`ElectricWirePoleGhostPart.cs:58` 等 `CalculateAffordableCellCount` の既存利用は電柱（N=1）なのでそのまま。

- [ ] **Step 5: コンパイル → テスト**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "ConstructionCostPreviewCalculatorTest|BeltConveyor"`
Expected: 全PASS

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat(client): 設置プレビューの置ける数を残り設置数込みで算出する"
```

---

### Task 8: webui — ビルドメニュー詳細に「N個分」と「残り設置数」を表示

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuDtos.cs:23-38`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuEntryDtoFactory.cs:24-49`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:154`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Client.WebUiHost.asmdef`（references に `"Game.Construction"`）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireContractTest.cs:162-175`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/build_menu_snapshot.json`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/buildMenu.ts:12-18`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/buildMenu.test.ts`
- Modify: `moorestech_web/webui/src/features/buildMenu/BuildMenuDetailSidebar.tsx`
- Modify: `Localization/localization.csv`（2行追加）→ `moorestech_web/webui` で `npm run gen:i18n`

**Interfaces:**
- Consumes: `ClientRemainingPlacementCountDatastore`（Task 6）、`ConstructionWalletUtil`
- Produces: DTO/contract fields `placementsPerCost:int(>=1)`, `remainingPlacementCount:int(>=0)`（全kind共通。非ブロックは 1 / 0）
- i18n keys: `ui.buildMenu.requiredItemsPerSet`（`"Required Items (per {count})"` / `"必要素材（{count}個分）"`）、`ui.buildMenu.remainingPlacementCount`（`"Remaining placements: {count}"` / `"残り設置数: {count}"`）

- [ ] **Step 1: 失敗するテスト（zod + wire fixture）**

`schemas/buildMenu.test.ts` に追加:

```ts
  it("placementsPerCost と remainingPlacementCount を必須で受理する", () => {
    const entry = BuildMenuEntryDataSchema.parse({
      id: "30000000-0000-4000-8000-000000000001",
      kind: "block",
      categoryGuid: "10000000-0000-4000-8000-000000000001",
      subCategoryGuid: "20000000-0000-4000-8000-000000000001",
      requiredItems: [{ itemId: 3, count: 1 }],
      placementsPerCost: 3,
      remainingPlacementCount: 2,
    });
    expect(entry.placementsPerCost).toBe(3);
    expect(() => BuildMenuEntryDataSchema.parse({ ...entry, placementsPerCost: 0 })).toThrow();
  });
```

既存テストのpayloadにも `placementsPerCost: 1, remainingPlacementCount: 0` を追加（必須化のため）。

`WireFixtures/build_menu_snapshot.json` の全5エントリに `"placementsPerCost": 1, "remainingPlacementCount": 0` を追加し、block エントリだけ `"placementsPerCost": 3, "remainingPlacementCount": 2` にする。`WireContractTest.cs:162-175` のDTO側も同じ値を設定。

Run: `cd moorestech_web/webui && npx vitest run src/bridge/contract` → 新テストFAIL（strictで未知キー拒否）

- [ ] **Step 2: contract とホストDTO**

`schemas/buildMenu.ts` の `BuildMenuEntryCommonFields` に追加:

```ts
  // 建設コスト1セットで置ける数と、支払い済みで未設置の残り（用語集: 設置数/1セット・残り設置数）
  // Placements per one cost set and the paid-but-unplaced remainder (glossary: 設置数/1セット / 残り設置数)
  placementsPerCost: z.number().int().min(1),
  remainingPlacementCount: z.number().int().min(0),
```

`BuildMenuDtos.cs` の `BuildMenuEntryDto` に:

```csharp
        // 設置数/1セットと残り設置数。ブロック以外は常に 1 / 0
        // Placements per cost set and remaining placements; always 1 / 0 for non-block entries
        public int PlacementsPerCost;
        public int RemainingPlacementCount;
```

`BuildMenuEntryDtoFactory.CreateDtos(IReadOnlyList<IPlacementTarget>)` に `ClientRemainingPlacementCountDatastore remainingPlacementCountDatastore` 引数を追加（`CreateDtos(PlacementTargetResolver, ClientRemainingPlacementCountDatastore)` も同様に引数を増やす）。エントリ生成に:

```csharp
                    PlacementsPerCost = ResolvePlacementsPerCost(target),
                    RemainingPlacementCount = ResolveRemainingPlacementCount(target),
```

ローカル関数:

```csharp
            int ResolvePlacementsPerCost(IPlacementTarget target)
            {
                return target is BlockPlacementTarget block ? MasterHolder.BlockMaster.GetBlockMaster(block.BlockId).PlacementsPerCost : 1;
            }

            int ResolveRemainingPlacementCount(IPlacementTarget target)
            {
                if (target is not BlockPlacementTarget block) return 0;
                return remainingPlacementCountDatastore.GetRemainingCount(ConstructionWalletUtil.ResolveWalletBlockId(block.BlockId));
            }
```

`BuildMenuTopic`: ctorに `ClientRemainingPlacementCountDatastore remainingPlacementCountDatastore` を追加して保持、`_remainingSubscription = remainingPlacementCountDatastore.OnChanged.Subscribe(_ => SchedulePublish());` を追加し `Dispose` で破棄、`BuildJson` の `CreateDtos(_placementTargetResolver, _remainingPlacementCountDatastore)`。`WebUiGameBinder.cs:154` で `resolver.Resolve<ClientRemainingPlacementCountDatastore>()` を渡す。他に `BuildMenuEntryDtoFactory.CreateDtos` を呼ぶ箇所（`grep -rn "BuildMenuEntryDtoFactory.CreateDtos" moorestech_client/Assets/Scripts`）も全て更新する。

- [ ] **Step 3: i18n とサイドバー**

`Localization/localization.csv` に2行追加（既存 `ui.buildMenu.requiredItems` 行の直後）:

```
ui.buildMenu.requiredItemsPerSet,Required Items (per {count}),Required Items (per {count}),必要素材（{count}個分）
ui.buildMenu.remainingPlacementCount,Remaining placements: {count},Remaining placements: {count},残り設置数: {count}
```

`cd moorestech_web/webui && npm run gen:i18n`

`BuildMenuDetailSidebar.tsx` のコスト部分:

```tsx
          {entry.requiredItems.length > 0 && (
            <>
              <span className={styles.detailCostLabel}>
                {entry.placementsPerCost > 1
                  ? t(L.ui.buildMenu.requiredItemsPerSet, { count: entry.placementsPerCost })
                  : t(L.ui.buildMenu.requiredItems)}
              </span>
              <SlotGrid cols={3}>
                {entry.requiredItems.map((item) => (
                  <ItemSlot key={item.itemId} itemId={item.itemId} count={item.count} />
                ))}
              </SlotGrid>
              {entry.placementsPerCost > 1 && (
                <span className={styles.detailCostLabel} data-testid="build-menu-remaining-placements">
                  {t(L.ui.buildMenu.remainingPlacementCount, { count: entry.remainingPlacementCount })}
                </span>
              )}
            </>
          )}
```

- [ ] **Step 4: 検証**

Run: `cd moorestech_web/webui && npx vitest run src/bridge/contract src/features/buildMenu && npm run build`
Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "WireContractTest"`
Expected: 全PASS・ビルド成功

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests Localization/localization.csv moorestech_web/webui
git commit -m "feat(webui): ビルドメニュー詳細に設置数/1セットと残り設置数を表示する"
```

---

### Task 9: 通し確認（unityプレイ録画テスト）と全テスト

**Files:** なし（検証のみ。問題があれば該当タスクへ戻る）

- [ ] **Step 1: 全EditModeテスト**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode`
Expected: 失敗0（失敗があれば修正してから進む）

- [ ] **Step 2: プレイ確認**

unity-playmode-recorded-playtest スキルのDSLで: 歯車ベルト（青銅シート1＋木の棒1を所持）を3本ポン置き → 素材が1回だけ減ること／ビルドメニュー詳細に「必要素材（3個分）」「残り設置数: 2」が出ること／3本撤去で素材が戻ることを録画で確認。Editor.log/Console に Error が無いこと（`uloop get-logs --project-path ./moorestech_client --log-type Error`）。

- [ ] **Step 3: コミット（録画・スクリプトの成果物があれば）**

---

### Task 10: 最終ブランチレビュー（必須・省略不可）

- [ ] **Step 1:** `moores-code-review` スキルでブランチ全体をレビューし、機械的修正を適用。設計判断は AskUserQuestion で裁定を仰ぐ。
- [ ] **Step 2:** 指摘対応後に再コンパイル・関連テスト再実行・コミット。
- [ ] **Step 3:** `bd close moorestech-uh5a --reason="ADR 0026 実装完了・PR作成"`、pr-create スキルでPR作成。

---

## 判断記録（ADR）

- 設計裁定: `docs/adr/0026-belt-construction-cost-remaining-placement-count.md`、`.decisions/2026-08-21-ベルト設置コストは永続クレジット台帳で3個1セットにする.md`（すべてユーザー裁定 2026-08-21）。
- planning中の追加判断（agent前提）:
  - 新アセンブリ名 `Game.Construction`（用語集「建設コスト」ドメイン。`Game.Hotbar` と同形の独立アセンブリ）。出所: agent前提（層マップ・Hotbar前例）
  - 財布の変更APIは `TryConsumeOne / Refill / ReturnOne` の3操作に限定し、課金アルゴリズムは `Server.Protocol/.../RemainingPlacementChargeService` に置く（DataStoreはインベントリを知らない）。出所: agent前提（DataStore分離レンズ）
  - 撤去時の返却判定は lookup で事前計算（`WouldCondenseOnReturn`）し、財布の更新は撤去確定後に行う（返却不能時に財布が変わらないため）。出所: agent前提（R6の受け入れ基準から導出）
  - イベント payload は財布BlockId（int）と残数。保存のみGuid。出所: agent前提（永続化キー規約はセーブにのみ適用・wireは `va:placeBlock` と同じBlockId）
  - `CalculateAffordableEntityCount` はファミリー内コスト一致の検証導入により不要となるため削除。出所: agent前提
  - webui DTO の2フィールドは全kind共通（非ブロックは 1/0）。zodの `.strict()` とC#単一DTOの整合のため。出所: agent前提
  - 旧セーブに `remainingPlacementCounts` が無い場合は空で起動（`?? new` は `hotbarAssignments` と同じ既存パターン）。移行スクリプトは書かない。出所: agent前提（AGENTS.md 後方互換不要）
  - `ReturnOne` の凝縮閾値は N 到達（`<=`）。計画のサンプルテストが N+1 到達（`<`）を期待していたのは誤り。出所: ユーザー裁定 2026-08-21
