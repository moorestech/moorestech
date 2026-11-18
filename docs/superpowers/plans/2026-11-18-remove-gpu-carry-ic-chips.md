# GPU を消し ICチップを軌道へ運ぶ Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** GPU・GPU衛星・データセンター・金属3Dプリンタと研究6本を消し、ICチップLv1〜5 を1便20個で軌道へ運ぶと計算データが出る形にする。

**Architecture:** 積荷は既に `items.json` の `orbitalPayloads` 表でデータ駆動なので、発射台はマスタの書き換えだけで ICチップを運べる。宇宙エレベーター拠点の昇降機だけが「1回に1個」をコードに固定しているので、1回に運ぶ個数を積荷表の「1便の個数」へ替える。それ以外はマスタ（`../moorestech_master`）の削除・書き換えと、GPU を参照する録画シナリオ・画面文言の追従。

**Tech Stack:** Unity C#（moorestech_server）、マスタ JSON（moorestech_master）、mooreseditor の nodeGraph、プレイテストDSL、Playwright（webui e2e）

## Requirements

設計の正は `docs/adr/0102-remove-gpu-and-carry-ic-chips-to-orbit.md`。

- R1 研究「金属超精密加工」「GPU設計」「GPULv2」「GPULv3」「GPULv4」「GPULv5」が research.json に無い。
- R2 前提が次のとおり: 品質モジュールLv1 ← 半導体製造／ロケット技術 ← 半導体製造／軌道エレベーターの建設 ← ロケット技術／核融合炉 ← ロケット技術／反物質爆弾 ← 核融合炉・軌道エレベーターの建設／品質モジュールLv2 ← 品質モジュールLv1・ロケット技術／品質モジュールLv3 ← 品質モジュールLv2。
- R3 アイテム GPULv1〜5・GPU衛星Lv1〜5、ブロック「金属3Dプリンタ」「データセンター」、それらを入力・出力・設置先に持つ機械レシピが、マスタのどの JSON にも残っていない（GUID の grep が0件）。
- R4 `orbitalPayloads` が ICチップLv1〜5（`countPerLaunch` 20、計算データ 20/40/80/160/320）とグラフェンケーブル（1000、成果なし）の6行。
- R5 宇宙エレベーター拠点の `carriedPayloadItemGuids` が ICチップLv1〜5。昇降機は1回に20個を入口から取り、240秒後に発射台と同じ計算データを出口へ出す。入口が20個未満なら昇らず待つ。
- R6 研究コスト: 品質モジュールLv1 から計算データ40 を外す。ロケット技術から計算データ200 を外す（ICチップLv1 2000・鋼鉄板1000・コンピュータ300）。他の研究コストは変えない。
- R7 ロケット技術の解放物: 機械レシピ（ロケット筐体素材・ロケット燃料）、レシピ表示（ロケット筐体素材・ロケット燃料・計算データ）、ブロック（ロケット発射台）。
- R8 mooreseditor で nodeGraph を開いて保存し直しても research.json の解放（`unlockItemRecipeView`・`unlockBlock`）と前提（`prevResearchNodeGuids`）が変わらない（意味差分0件）。
- R9 半導体製造から最終研究まで、各研究のコストに要るアイテムが、その研究の前提までの解放物で作れる（詰みが無い）。
- R10 画面文言とローカライズに「GPU」「データセンター」「金属3Dプリンタ」「金属超精密加工」が残らない。拠点の入口の表示は ICチップを指す。

やらないこと:
- 消したアイテム・ブロック・研究を持つセーブ済みワールドの手当て（ADR の agent前提）。セーブ形式は変えない。
- 消した研究のコストを他の研究へ上乗せすること。
- C# の識別子・セーブのキー・画面との通信の名前に残る `Satellite` の改名（判断記録 J2）。
- テスト用 mod（`Tests.Module/TestMod/ForUnitTest`）の GPU 連鎖の削除（本番マスタと独立。判断記録 J3）。
- クライアントに残る DataCenter・金属3Dプリンタのプレハブの削除（Unity 固有ファイルで、参照が切れても害が無い。判断記録 J4）。

## Global Constraints

- 作業場所: 本体は worktree `/Users/katsumi/moorestech-worktrees/remove-gpu`（ブランチ `feature/remove-gpu-simplify-tier9`）。マスタは `/Users/katsumi/moorestech_master` に別 worktree を切る（`git -C /Users/katsumi/moorestech_master worktree add -b feature/remove-gpu-simplify-tier9 /Users/katsumi/moorestech-worktrees/remove-gpu-master origin/master`）。`/Users/katsumi/moorestech` と `/Users/katsumi/moorestech_master` の作業ツリーは触らない（ユーザーの未コミット変更がある）。
- マスタの変更は push して PR を作り、本体の `.moorestech-external-revisions.json` のピンをその push 済みコミットへ向ける。マージはマージコミット（squash しない）。
- マスタのパス: `server_v8/mods/moorestechAlphaMod_8/master/{research,items,blocks,machineRecipes}.json`、`server_v8/mods/moorestechAlphaMod_8/master/.mooreseditor/nodeGraph.v1.json`、`server_v8/mods/moorestechAlphaMod_8/localization/localization.csv`。
- 一時的なスクリプト・CI・到達チェックをどちらの repo にもコミットしない（`.decisions/2026-11-16-研究解放の再発防止にmooreseditorの挙動変更や到達警告は入れない.md`）。JSON の書き換えに使うスクリプトは scratchpad に置いて捨てる。
- mooreseditor は保存時に、アイテム・ブロックの解放（`unlockItemRecipeView`・`unlockBlock`）を nodeGraph のノード位置から作り直す。機械レシピの解放（`unlockMachineRecipe`）は research.json の手書きが正。よって解放を変えるときは research.json と nodeGraph の両方を直す。
- JSON は既存の整形（インデント・キー順・末尾改行）を保つ。`json.dump(..., ensure_ascii=False, indent=<既存の幅>)` で書き、`git diff --stat` で意図しない全行差分が無いことを確かめる。
- AGENTS.md のコーディング規約（2行コメント・`#region Internal`・try-catch 禁止・デフォルト引数禁止・200行以下）に従う。.cs を変えたら `uloop compile --project-path ./moorestech_client`。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / 層の置き場所・機構・死活表とも前例どおり。注目点4件は判断記録 J6〜J8 に記載
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強1・弱2・第3バケツ1 / 強（1便の個数の置き場が ADR 0091 とずれる）は plan を ADR に合わせて直した（J6）。弱2件は J1・J9。第3バケツは末尾の節

---

## 配置と前例

| # | 項目 | 配置先 | 機構・前例 |
|---|---|---|---|
| 1 | 運ぶ積荷ごとの1便の個数 | `SpaceElevatorBaseInventoryComponent`（生成時にマスタの積荷表から読んで持つ） | ADR 0091「1便の個数はマスタの表から引く」。発射台の `Game.Block.Interface/Component/RocketLaunchPad/RocketLaunchPadSlotLayout.cs:26` が生成時に `OrbitalPayloads` の `CountPerLaunch` を読むのと同じ。到着サービスには問い合わせを足さない |
| 2 | 昇降機が運ぶ個数 | `Game.Block/Blocks/SpaceElevator/SpaceElevatorBaseInventoryComponent`（入口の在庫の持ち主） | 「収納確認から取り出しまでを在庫の所有者が完結する」既存の `TryTakeSatelliteForClimb` を広げる |
| 3 | ロード時の個数の検査 | `SpaceElevatorClimberSaveRestorer`（既存の壊れたセーブの扱い） | 既存の「2機以上なら1機だけ昇らせ余りを戻す」分岐を個数つきに広げる |
| 4 | マスタ検証 | `Core.Master/Validator/Block/SpaceElevatorBaseParamValidator` | `countPerLaunch must be 1` を外す。上限は `OrbitalPayloadMasterValidator` の「Lv1 のスタック上限以下」が既に見ている |

データフロー: 入口スロット →（昇降機が1便ぶん取る）→ 昇降中の積荷 →（頂上）→ `IOrbitalArrivalService.Arrive` → 出口スロット。新しい書き手・読み手は増えず、取る個数だけが 1 から「1便の個数」へ替わる。

死活表（拠点にぶら下がる操作）:

| 操作 | 計画後 | 根拠 |
|---|---|---|
| ケーブルを入れて接続する | 生きる | ケーブルプールは触らない |
| 入口へ積荷を入れる（手・ベルト） | 生きる | `IsCarriedPayload` はそのまま。運ぶ物が ICチップに替わる |
| 昇降して成果を受け取る | 生きる | 20個そろうと昇る。20個未満は「積荷待ち」（今の「衛星待ち」と同じ状態） |
| 撤去時の返却・セーブ・ロード | 生きる | 昇降中の積荷は個数つきで保存済み。Task 2 のテストで確かめる |
| 発射台の自動・手動の打ち上げ | 生きる | コードは触らない。積荷表の行が替わるだけ |

---

### Task 1: 昇降機が1便の個数を運ぶ（サーバー）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/SpaceElevator/SpaceElevatorBaseInventoryComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/SpaceElevator/SpaceElevatorClimberComponent.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Blocks/SpaceElevator/SpaceElevatorClimberSaveRestorer.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/Validator/Block/SpaceElevatorBaseParamValidator.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestItemId.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorBaseMasterValidationTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/Robustness/SpaceElevatorMultipleOutputTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorClimbTest.cs`（補助 `SetupConnected`・`Inventory` を共有へ移す）
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorTestUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorBaseSaveAndRefundTest.cs`（145行付近のログ期待）
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/Recovery/SpaceElevatorLoadFailureSaveLoadTest.cs`（101行付近のログ期待）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorBulkPayloadClimbTest.cs`（Create。.meta は Unity に作らせる）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Game/SpaceElevator/SpaceElevatorBulkPayloadSaveTest.cs`（Create）

**Interfaces:**
- Produces: `internal int SpaceElevatorBaseInventoryComponent.GetCountPerLaunch(ItemId carriedPayloadItemId)`（運ぶ積荷だけ引ける。呼ぶ前に `IsCarriedPayload` で確かめる）
- Produces: `internal bool SpaceElevatorBaseInventoryComponent.TryCompleteArrival(IItemStack climbingPayload)`（引数を `ItemId` から昇降中のスタックへ替える）
- Produces: テスト用 `ForUnitTestItemId.TestElevatorBulkPayload`（1便3個・成果は `TestElevatorReward` 7個）

- [ ] **Step 1: 運ぶ積荷ごとの1便の個数を、生成時にマスタから読んで持つ**

`SpaceElevatorBaseInventoryComponent.cs` の `private readonly HashSet<ItemId> _carriedPayloads;` を次に替え、コンストラクタの `_carriedPayloads = new HashSet<ItemId>(GetCarriedPayloadItemIds(param));` も替える（運ぶ積荷が積荷表に載っていることは `SpaceElevatorBaseParamValidator` が起動時に保証する）:

```csharp
        // 運ぶ積荷と、その1便の個数（マスタの積荷表から引く）
        // Carried payloads and their per-launch counts, read from the master payload table
        private readonly Dictionary<ItemId, int> _countPerLaunchByCarriedPayload;
```

```csharp
            _countPerLaunchByCarriedPayload = param.CarriedPayloadItemGuids.ToDictionary(
                guid => MasterHolder.ItemMaster.GetItemId(guid),
                guid => MasterHolder.ItemMaster.Items.OrbitalPayloads.Single(payload => payload.PayloadItemGuid == guid).CountPerLaunch);
```

`IsCarriedPayload` を `return _countPerLaunchByCarriedPayload.ContainsKey(itemId);` に替え、その下に足す:

```csharp
        internal int GetCountPerLaunch(ItemId carriedPayloadItemId) { return _countPerLaunchByCarriedPayload[carriedPayloadItemId]; }
```

- [ ] **Step 2: 入口から1便ぶんを取る**

`SpaceElevatorBaseInventoryComponent.cs` の `PeekSatellite` を次に替える（1便に満たない積荷は「空」と同じ待ちにする）:

```csharp
        internal SatelliteInletState PeekSatellite(out ItemId satelliteId)
        {
            CheckDestroy(this);
            var stack = _itemDataStoreService.GetItem(SatelliteSlot);
            satelliteId = stack.Id;
            if (stack.Count == 0) return SatelliteInletState.Empty;
            if (!IsCarriedPayload(stack.Id)) return SatelliteInletState.NotCarried;
            // silent-ok: 1便に満たない積荷は通常の待機 / プレイヤーは拠点UIの積荷待ちと入口の個数で気づく
            return stack.Count < GetCountPerLaunch(stack.Id) ? SatelliteInletState.Empty : SatelliteInletState.Carriable;
        }
```

`TryTakeSatelliteForClimb` の後半を次に替える:

```csharp
            var countPerLaunch = GetCountPerLaunch(satelliteId);
            // silent-ok: 成果の収納待機 / 呼出側が出口待ちをUIへ配信する
            if (!InventoryInsertItem.TryFillAllPreferringExisting(_arrivalService.PreviewArrivalOutputs(satelliteId, countPerLaunch), CopyOutlets())) return SatelliteTakeResult.OutputBlocked;
            var stack = _itemDataStoreService.GetItem(SatelliteSlot);
            _itemDataStoreService.SetItem(SatelliteSlot, stack.SubItem(countPerLaunch));
            satellite = ServerContext.ItemStackFactory.Create(stack.Id, countPerLaunch);
            return SatelliteTakeResult.Taken;
```

`TryCompleteArrival` の引数とサービス呼び出しを替える:

```csharp
        internal bool TryCompleteArrival(IItemStack climbingPayload)
        {
            CheckDestroy(this);
            var outputs = _arrivalService.PreviewArrivalOutputs(climbingPayload.Id, climbingPayload.Count);
            var simulated = CopyOutlets();
            // silent-ok: 容量不足では到着させない / 昇降機が出口待ちをUIと初回ログへ反映する
            if (!InventoryInsertItem.TryFillAllPreferringExisting(outputs, simulated)) return false;
            _arrivalService.Arrive(climbingPayload.Id, climbingPayload.Count);
```

（以降の for ループは変えない。）

`SpaceElevatorClimberComponent.cs` の `AdvanceClimb` 内 `_inventory.TryCompleteArrival(_climbingSatellite.Id)` を `_inventory.TryCompleteArrival(_climbingSatellite)` に替え、ログ `衛星1機の成果が出口に入りきらないため` を `1便の成果が出口に入りきらないため` に替える。

- [ ] **Step 3: ロード時に個数を1便へそろえる**

`SpaceElevatorClimberSaveRestorer.cs` の「昇降機は1機ずつ運ぶ」ブロック（`if (1 < satellite.Count) { ... }`）を次に替える。1便の個数は Step 1 の `inventory.GetCountPerLaunch` で引く（直前の `IsCarriedPayload` の分岐を通った後なので必ず引ける）:

```csharp
            // 昇降機は1便ぶんを運ぶ。マスタの変更や壊れたセーブで個数が合わなければ、1便ぶんだけ昇らせ余りを入口へ戻す
            // The climber carries one launch worth; on a mismatch keep one launch climbing and return the rest to the inlet
            var countPerLaunch = inventory.GetCountPerLaunch(satellite.Id);
            if (satellite.Count < countPerLaunch)
            {
                Debug.LogError($"[SpaceElevatorBase] 昇降中の積荷 {satellite.Id} が {satellite.Count} 個で1便の {countPerLaunch} 個に足りないため、昇降を取り消して入口へ戻します");
                if (!inventory.TryReturnSatelliteWithoutEvent(satellite)) throw new InvalidOperationException("Space elevator load failed: short payload cannot be returned; preserve the source save");
                return (ServerContext.ItemStackFactory.CreatEmpty(), 0);
            }
            if (countPerLaunch < satellite.Count)
            {
                Debug.LogError($"[SpaceElevatorBase] 昇降中の積荷が {satellite.Count} 個セーブにあるため、1便の {countPerLaunch} 個だけ昇らせ残りを入口へ戻します");
                if (!inventory.TryReturnSatelliteWithoutEvent(ServerContext.ItemStackFactory.Create(satellite.Id, satellite.Count - countPerLaunch)))
                    throw new InvalidOperationException("Space elevator load failed: excess satellites cannot be returned; preserve the source save");
                satellite = ServerContext.ItemStackFactory.Create(satellite.Id, countPerLaunch);
            }
```

- [ ] **Step 4: マスタ検証から「1便1個」を外す**

`SpaceElevatorBaseParamValidator.cs` の `if (payload.CountPerLaunch != 1) result += ...countPerLaunch must be 1...;` の1行を消す。直後の出口スロット数の検査は1便ぶんの成果で数えているのでそのまま残す。

`SpaceElevatorBaseMasterValidationTest.cs` の `[TestCase("count")]` と `if (variant == "count") row["countPerLaunch"] = 2;`、期待文言の `variant == "count" ? "countPerLaunch must be 1" :` の分岐を消す（1便2個は正しいマスタになったため）。

- [ ] **Step 5: テスト用 mod に1便3個の積荷を足す**

`ForUnitTest/mods/forUnitTest/master/items.json` の `data` に、`5e1ea7a0-0002-4000-8000-000000000002`（TestElevatorSatellite）の行を複写して `itemGuid` を `5e1ea7a0-0002-4000-8000-000000000004`、`name` を `TestElevatorBulkPayload` にした行を足す。`orbitalPayloads` に足す:

```json
        {
            "payloadItemGuid": "5e1ea7a0-0002-4000-8000-000000000004",
            "countPerLaunch": 3,
            "arrivalOutputs": [
                { "itemGuid": "5e1ea7a0-0002-4000-8000-000000000003", "count": 7 }
            ]
        }
```

`blocks.json` で `carriedPayloadItemGuids` に `5e1ea7a0-0002-4000-8000-000000000002` を持つ全ブロックの配列へ `5e1ea7a0-0002-4000-8000-000000000004` を足す。発射台は積荷表の全行にスロットを割り当てるので、`grep -n '"RocketLaunchPad"' -A40 blocks.json` で発射台の入力スロット数に関わる値があれば、既存の `RocketLaunchPadParamValidator.cs` を読んで1行増えても通ることを確かめる（通らなければその値を1増やす）。

`ForUnitTestItemId.cs` の `TestElevatorSatellite` の下に足す:

```csharp
        public static ItemId TestElevatorBulkPayload => MasterHolder.ItemMaster.GetItemId(Guid.Parse("5e1ea7a0-0002-4000-8000-000000000004"));
```

- [ ] **Step 6: テストを書く**

`SpaceElevatorMultipleOutputTest.cs` の `inventory.TryCompleteArrival(ForUnitTestItemId.TestElevatorSatellite)` 3か所を `inventory.TryCompleteArrival(ServerContext.ItemStackFactory.Create(ForUnitTestItemId.TestElevatorSatellite, 1))` に替える。

補助 `SetupConnected` と `Inventory` は `SpaceElevatorClimbTest.cs`（183・193行付近）に `private static` で置かれている。2つを本体ごと `SpaceElevatorTestUtil.cs` へ `public static` で移し、`SpaceElevatorClimbTest.cs` 側の定義を消す（同ファイルは既に `using static ...SpaceElevatorTestUtil` を持つので呼び出しは変えない。ほかのテストに同名の private 複製があれば、それも消して共有を使う）。

Step 3 でログ文言が変わるので、`SpaceElevatorBaseSaveAndRefundTest.cs:145` と `Recovery/SpaceElevatorLoadFailureSaveLoadTest.cs:101` の `LogAssert.Expect` の正規表現「1機だけ昇らせ」を、新しい文言に合う「1便の .* 個だけ昇らせ」に替える（テスト用の衛星は1便1個なので、挙動の期待は変えない）。`grep -rn "1機だけ昇らせ\|衛星1機の成果" moorestech_server/Assets/Scripts/Tests` で他に無いことを確かめる。

`SpaceElevatorBulkPayloadClimbTest.cs` を作る:

```csharp
using Game.Block.Interface.State;
using NUnit.Framework;
using Tests.Module.TestMod;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorTestUtil;
using static Tests.CombinedTest.Game.SpaceElevator.SpaceElevatorConnectionTest;

namespace Tests.CombinedTest.Game.SpaceElevator
{
    public class SpaceElevatorBulkPayloadClimbTest
    {
        [Test]
        public void 一便に満たない積荷では昇らず入口に残る()
        {
            var elevatorBase = SetupConnected(100f);
            Inventory(elevatorBase).SetItem(0, ForUnitTestItemId.TestElevatorBulkPayload, 2);

            RunSeconds(2.2);

            Assert.AreEqual(2, Inventory(elevatorBase).GetItem(0).Count);
            Assert.AreEqual(0, Inventory(elevatorBase).GetItem(1).Count);
            Assert.AreEqual(SpaceElevatorBaseStatus.WaitingSatellite, ReadDetail(elevatorBase).Status);
        }

        [Test]
        public void 一便ぶんをまとめて運び一便ぶんの成果が出る()
        {
            var elevatorBase = SetupConnected(100f);
            Inventory(elevatorBase).SetItem(0, ForUnitTestItemId.TestElevatorBulkPayload, 3);

            RunSeconds(2.2);

            Assert.AreEqual(0, Inventory(elevatorBase).GetItem(0).Count);
            Assert.AreEqual(ForUnitTestItemId.TestElevatorReward, Inventory(elevatorBase).GetItem(1).Id);
            Assert.AreEqual(7, Inventory(elevatorBase).GetItem(1).Count);
        }

        [Test]
        public void 端数は入口に残り二便ぶんの成果が出る()
        {
            var elevatorBase = SetupConnected(100f);
            Inventory(elevatorBase).SetItem(0, ForUnitTestItemId.TestElevatorBulkPayload, 7);

            RunSeconds(4.6);

            Assert.AreEqual(1, Inventory(elevatorBase).GetItem(0).Count);
            Assert.AreEqual(14, Inventory(elevatorBase).GetItem(1).Count);
        }
    }
}
```

さらに `SpaceElevatorBulkPayloadSaveTest.cs` を新しく作る（`SpaceElevatorBaseSaveAndRefundTest.cs` は180行あり、足すと200行を超えるため別ファイルにする）。同ファイルの「昇降中にセーブしてロードすると続きから昇る」型のテストを複写し、積荷を `TestElevatorBulkPayload` 3個に替える（ロード後に昇り切って成果が7個出ることと、入口が0個のままであることを確かめる）。「昇降中の撤去で積荷が返る」型のテストも同じく複写して3個が返ることを確かめる。

`SpaceElevatorBaseInventoryComponent.cs` は今197行。Step 1〜2 の追加で200行を超えたら、ロード復元まわり（`LoadState` 相当と `TryReturnSatelliteWithoutEvent`）を既存の `SpaceElevatorInventoryRestorer.cs` へ寄せて200行以下に収める。

- [ ] **Step 7: コンパイルしテストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SpaceElevator|RocketLaunchPad|OrbitalArrival|Tier9"`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 8: コミットする**

```bash
git add moorestech_server/Assets/Scripts
git commit -m "feat(space-elevator): 昇降機が1回に運ぶ個数を積荷表の1便の個数にする"
```

### Task 2: マスタから GPU 関連を消し、積荷を ICチップにする（moorestech_master）

**Files（すべて `/Users/katsumi/moorestech-worktrees/remove-gpu-master/server_v8/mods/moorestechAlphaMod_8/` 配下）:**
- Modify: `master/research.json`
- Modify: `master/items.json`
- Modify: `master/blocks.json`
- Modify: `master/machineRecipes.json`
- Modify: `master/.mooreseditor/nodeGraph.v1.json`
- Modify: `localization/localization.csv`
- Delete: 消したアイテム・ブロックのアイコン画像（`git grep -l` で GUID・名前から探し、他から参照されていないものだけ）

**Interfaces:**
- Produces: push 済みのマスタのコミット（Task 3 がピンに使う）

名前から GUID を引いて作業する。主な GUID: 計算データ `01a167cf-af88-74d3-9cd6-7d61b71052bb`、ICチップLv1〜5 `f6dd7835-0845-4877-87d0-b719d037ae7d` / `878ea106-3689-4bcd-8d9a-e18b0e03072c` / `d403eb63-f913-456f-842d-41980cdfc0bc` / `2ef1886a-30b0-4792-8c20-0f39c370443a` / `01a17356-2aff-701f-9a1c-17524cddb030`、GPULv1 `01a167ed-2703-7014-98e4-11bd15f38571`、データセンター `01a19f64-4833-7477-85f6-d354d805f154`、宇宙エレベーター拠点 `01a162aa-cec0-738c-93c5-f58adc87fa7f`。

- [ ] **Step 1: worktree を作る**

```bash
git -C /Users/katsumi/moorestech_master fetch origin
git -C /Users/katsumi/moorestech_master worktree add -b feature/remove-gpu-simplify-tier9 /Users/katsumi/moorestech-worktrees/remove-gpu-master origin/master
```

- [ ] **Step 2: items.json を直す**

- `data` から name が `GPULv1`〜`GPULv5`、`GPU衛星Lv1`〜`GPU衛星Lv5` の10行を消す。
- `levelFamilies` から、`levelItemGuids` に上の GUID を含む行（GPU の系列。GPU衛星の系列があればそれも）を消す。
- `orbitalPayloads` の GPU衛星5行を消し、ICチップLv1〜5 の5行を足す（`countPerLaunch` 20、`arrivalOutputs` は計算データを 20・40・80・160・320）。グラフェンケーブルの行は残す。
- `modules` などほかのキーに消した GUID が残っていないことを grep で確かめる。

- [ ] **Step 3: blocks.json を直す**

- name が `金属3Dプリンタ`・`データセンター` の2行を消す。
- `宇宙エレベーター拠点` の `blockParam.carriedPayloadItemGuids` を ICチップLv1〜5 の GUID 5本に替える。
- 末尾付近の `beltConveyorFamilies` などほかのキーに消した blockGuid が無いことを grep で確かめる。

- [ ] **Step 4: machineRecipes.json を直す**

次の機械レシピを消す（消す前に `machineRecipeGuid` を控える。research.json から外すのに使う）:
- `blockGuid` が金属3Dプリンタ・データセンターのもの全部（金属3Dプリンタ2本、データセンター5本）
- 出力が GPULv1 のもの（電気精密組立機、1本）
- 出力が GPU衛星Lv1〜5 のもの（電気精密組立機、5本）

- [ ] **Step 5: research.json を直す**

- 研究「金属超精密加工」「GPU設計」「GPULv2」「GPULv3」「GPULv4」「GPULv5」を消す。
- `prevResearchNodeGuids` を R2 のとおりにする: 品質モジュールLv1 = [半導体製造]、ロケット技術 = [半導体製造]、核融合炉 = [ロケット技術]、品質モジュールLv2 = [品質モジュールLv1, ロケット技術]、品質モジュールLv3 = [品質モジュールLv2]。軌道エレベーターの建設・反物質爆弾は今のまま。
- `consumeItems`: 品質モジュールLv1 とロケット技術から計算データの行を消す。
- ロケット技術の `clearedActions`: `unlockMachineRecipe` から GPU衛星Lv1 のレシピ GUID を外す。`unlockItemRecipeView` を [ロケット筐体素材, ロケット燃料, 計算データ] にする。`unlockBlock` は [ロケット発射台] のまま。
- 残りの全研究の `clearedActions` から、Step 2〜4 で消したアイテム・ブロック・レシピの GUID を外す（例: 反物質爆弾の `unlockItemRecipeView` にある GPU衛星Lv2・GPULv2）。配列が空になった action は action ごと消す。
- 全研究の `prevResearchNodeGuids` に消した研究の GUID が残っていないことを確かめる。

- [ ] **Step 6: nodeGraph.v1.json を直す**

mooreseditor はアイテム・ブロックの解放をノードの位置から作り直すので、research.json と同じ結果になるようにノードを直す。

- 消した研究6本・アイテム10個・ブロック2個・機械レシピ13本に対応するノードと、それらにつながる辺を消す（材料の札として置かれた複製ノードも含む。GUID で全件探す）。
- 研究ノードの前提の辺を R2 に合わせる。mooreseditor は保存時に `prevResearchNodeGuids` もノードの辺から作り直す（`exportToMasterResearchPatch.ts` の `record.prevResearchNodeGuids = dependencyMap.get(...)`）ので、辺が R2 と1本でも違うと次の保存で前提が黙って変わる。
- 計算データのアイテムノードを、ロケット技術の解放に割り当たる位置へ動かす。割り当ての規則は `/Users/katsumi/WebstormProjects/mooreseditor` の `spatialUnlock.ts`（`calculateResearchUnlocks`）を読む。
- 消した研究が占めていた場所が空くので、ロケット技術・品質モジュールLv1 の研究ノードを半導体製造の右隣へ寄せ、ノード同士が重ならないようにする（ノードの実寸は幅と高さがアイテム数で変わる。重なりの確認は mooreseditor で開いて目で見るか、矩形の交差を数える）。
- ほかのアイテム・ブロックノードの割り当てが変わらないことを Step 7 で確かめる。

- [ ] **Step 7: mooreseditor の保存で解放が変わらないことを確かめる**

mooreseditor の実コードを Node で動かし、nodeGraph から research.json の解放を作り直して、Step 5 の research.json と意味差分が0件であることを確かめる。

```bash
cd /Users/katsumi/WebstormProjects/mooreseditor && git status -sb | head -2
grep -rn "calculateResearchUnlocks\|updateClearedActions\|prevResearchNodeGuids" plugins/node-graph/src --include='*.ts' -l
```

上で出る `spatialUnlock.ts`・`exportToMasterResearchPatch.ts` を scratchpad へ複写し（相対 import を `.ts` つきに書き換える）、`node --experimental-strip-types` で「nodeGraph を読む → 解放を計算 → research.json の各研究の `unlockItemRecipeView`・`unlockBlock`・`prevResearchNodeGuids` と集合で比べる」ドライバを書いて走らせる。前回の実例が `/private/tmp/claude-501/-Users-katsumi-moorestech/ea0c3b75-736a-45ea-bca8-b60b69bceaeb/scratchpad/editor-save/driver.ts` に残っていれば流用する（無ければ書き直す）。

Expected: 差分 0 件。差分が出たら、その研究とアイテムの名前を見てノードの位置（Step 6）か research.json（Step 5）のどちらが R7 と違うかを直す。

- [ ] **Step 8: 詰みが無いことを確かめる（R9）**

scratchpad のスクリプトで、研究を前提の順にたどり、各研究の `consumeItems` の全アイテムについて「それを出力する機械レシピ・クラフトレシピ・採掘・`orbitalPayloads` の成果のどれかが、その研究の前提（推移閉包）までに解放されている」ことを確かめる。計算データは「ロケット発射台が解放済みで、積荷の ICチップLv1 が作れる」ことを条件にする。レベル付きアイテム（`items.json` の `levelFamilies` の Lv2 以降。ICチップLv2〜5・モジュールLv2〜5 など）は専用のレシピを持たず、Lv1 のレシピのレベル継承で作られるので、「同じ系列の Lv1 が作れる」ことを条件にする（ICチップは、半導体製造で解放される精密切断機のレシピがシリコンダイLv1〜5 を同時に出す）。半導体製造より前の研究は今回変えていないので、半導体製造以降の研究だけ見ればよい。

Expected: 作れないアイテムを要求する研究が0件。とくに 品質モジュールLv2（計算データ400）がロケット技術の後であること、ロケット技術が計算データを要求しないこと。

- [ ] **Step 9: 残骸が無いことを確かめる（R3・R10）**

```bash
cd /Users/katsumi/moorestech-worktrees/remove-gpu-master/server_v8/mods/moorestechAlphaMod_8
grep -rn "GPU\|データセンター\|金属3Dプリンタ\|金属超精密" master localization | grep -v "^Binary" | head
```

- `localization/localization.csv`: 消したアイテム・ブロック・研究の行（`item.<guid>.name`・`block.<guid>.*`・`research.<guid>.*`）を消す。ロケット技術の説明文（`research.01a167cf-af9d-7f31-817c-3069b4f6e499.description`）を、日本語「ロケット発射台と、ロケットの筐体素材・燃料のレシピを解放する。ICチップを20個積んで打ち上げると計算データが届く」、英語 "Unlocks the rocket launch pad and recipes for rocket casings and fuel. Launch 20 IC chips to receive compute data."、他の言語列は既存の行の列の並びに合わせて同じ意味で書く。軌道エレベーター・拠点・計算データの説明に GPU衛星が出る行も、同じ言い回しで ICチップに直す。拠点の入口を指す画面文言（本体側にあれば Task 3）もここで grep して把握する。
- 消した GUID 25本（アイテム10・ブロック2・レシピ13）と研究6本の GUID を、`master/` と `.mooreseditor/` と `localization/` で grep して0件。

Expected: 上の grep が0件（説明文に意図して残す語は無い）。

- [ ] **Step 10: 並び順と研究の座標を整える**

master-refine スキルの操作 A（sortPriority）と B（research.json の UIPosition）を v8 mod に実行する（消した研究の分の座標の空きを詰めるため）。実行後に Step 7 をもう一度走らせて差分0件を確かめる。

- [ ] **Step 11: コミットして push し、PR を作る**

```bash
cd /Users/katsumi/moorestech-worktrees/remove-gpu-master
git add -A && git commit -m "feat(tier9): GPU・データセンター・金属3Dプリンタを消し、ICチップを1便20個で軌道へ運ぶ"
git push -u origin feature/remove-gpu-simplify-tier9
gh pr create --title "GPU関連を消しICチップをそのまま軌道へ運ぶ" --body "moorestech 本体 ADR 0102。研究6本・アイテム10個・ブロック2個・機械レシピ13本を消し、積荷を ICチップLv1〜5（1便20個）にする。"
```

### Task 3: 本体をマスタに追従させる（ピン・録画シナリオ・画面文言・用語集）

**Files:**
- Modify: `.moorestech-external-revisions.json`（Task 2 の push 済みコミットへ）
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-2-modules-gpu.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-4-rocket-launch.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-4b-rocket-cargo-shift-reject.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-5-compute-research.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-6-fusion-elevator.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-7-antimatter-final-research.cs`
- Delete: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-8-data-center.cs`
- Modify: `moorestech_web/webui/e2e/tests/block/spaceElevator/spaceElevator.spec.ts` と、その文言の出所
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/tier9/tier9-1a-chip-line.cs`（30行付近で研究「GPU設計」の GUID `01a167f3-bd74-73f8-8c43-1869db833299` を持ち、112行付近で前提を引く。消した研究を引くと null 参照で落ちる）
- Modify: `Localization/localization.csv`（本体。488・492行付近の「GPU衛星を待っています」「GPU衛星の入口」）
- Modify: `CONTEXT.md`

- [ ] **Step 1: ピンを更新する**

`.moorestech-external-revisions.json` の moorestech_master のコミットを、Task 2 Step 11 で push したコミットの SHA（`git -C /Users/katsumi/moorestech-worktrees/remove-gpu-master rev-parse HEAD`）に替える。録画シナリオが読むマスタは `docs/development/worktree-and-review-pitfalls.md` の手順でこのピンに合わせる。

- [ ] **Step 2: 本番マスタの GPU を参照する箇所を洗い出して直す**

```bash
grep -rn "GPU\|データセンター\|DataCenter\|金属3Dプリンタ\|金属超精密\|01a167ed-27\|01a167cf-af8b\|01a167cf-af8e\|01a167cf-af9[147]\|01a19f64-4833" \
  .agents/skills moorestech_web/webui/src moorestech_web/webui/e2e moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts Localization CONTEXT.md \
  --include='*.cs' --include='*.ts' --include='*.tsx' --include='*.md' --include='*.json' --include='*.csv' | grep -v "ForUnitTest\|Tests.Module\|/Tests/\|V7ToV8\|Rail\|lilToon\|steamworks\|Recorder\|IconCapture\|MapGeneration"
```

消した研究6本の GUID（Task 2 Step 5 で控える。GPU設計は `01a167f3-bd74-73f8-8c43-1869db833299`、GPULv2〜5 は `01a19cf1-4847…`・`01a19cf1-4852…`・`01a19cf1-485d…`・`01a19cf1-4867…`、金属超精密加工は research.json から引く）でも同じ場所を grep する。

出た箇所を次の規則で直す:
- 録画シナリオ: GPU・GPU衛星を作って積む手順を「ICチップLv1 を20個（Lv2 なら20個）積む」に替える。期待する計算データは1便あたり Lv1:20・Lv2:40 のまま。GPU設計・金属超精密加工・GPULvN の研究を完了させる手順は消し、ロケット技術の前提は半導体製造だけにする。`tier9-2-modules-gpu.cs` は GPU の部分を消してモジュールだけにする（ファイル名は `tier9-2-modules.cs` に改名し、シナリオ一覧やスキル本文に旧名があれば直す）。`tier9-8-data-center.cs` は消す。
- 画面文言: 出所は本体の `Localization/localization.csv`。「GPU衛星の入口」は「ICチップの入口」に、ほかの列（英語など）も同じ意味に替える。キー名は変えないので、生成物 `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts` は触らない。状態「衛星待ち」の文言は「積荷が1便ぶんそろうのを待っています」の意味に替える（個数はマスタの値なので文言に書かない）。e2e の期待文字列も合わせる。
- `V7ToV8` のマイグレーションとテスト用 mod は触らない（過去のセーブ変換と、本番マスタから独立したテスト資産）。

- [ ] **Step 3: CONTEXT.md を直す**

- 「積荷」: 例を「ICチップ・グラフェンケーブル」にする。
- 「GPU衛星」「データセンター」の項を消す。
- 「計算データ」: 「ICチップLv1〜5 を1便20個運んだ打ち上げ成果（Lv1:20、以降レベルごとに2倍）。品質モジュールLv2〜5・軌道エレベーターの建設・反物質爆弾の研究素材。」
- 「打ち上げ成果」「接続」「昇降機」の説明の GPU衛星を ICチップ（1便20個）に替える。昇降機は「1便ぶん（20個）ずつ運ぶ」と書く。
- レベルと電気精密組立機の説明の例から GPU・GPU衛星を外す。
- 研究の本線を書いた箇所があれば ADR 0102 の形に替える。

- [ ] **Step 4: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "SpaceElevator|RocketLaunchPad|OrbitalArrival|Tier9|MasterValidat|Research"`
Expected: ErrorCount 0 / 全 PASS。本番マスタを読むテスト（マスタ検証）が、新しいマスタ（1便20個の積荷を拠点が運ぶ）で通ること。
webui: `moorestech_web/webui` で既存の手順どおり単体テストと、変更した e2e（spaceElevator）を走らせる。

- [ ] **Step 5: コミットする**

```bash
git add -A . && git commit -m "feat(tier9): マスタの GPU 削除に追従する（ピン・録画シナリオ・文言・用語集）"
```

### Task 4: unityプレイ録画テスト

**Files:**
- 実行のみ（Task 3 で直したシナリオ）

- [ ] **Step 0: 消した物を持つワールドが読み込めることを確かめる（J10）**

テスト用のワールド（録画スキルのワールド複写の手順で作る。`Saves/` に作ったら終わりに `list-work-saves.sh` で探して消す）に、ピン更新前のマスタで GPULv1・GPU衛星Lv1 をインベントリへ入れ、データセンターを1台置いてセーブする。ピンを新しいマスタに替えてロードし、ロードが完了して、GPU・データセンターが消えた状態で始まることを確かめる。落ちたら、落ちた箇所とログを添えてユーザーへ報告する（移行ステップを足すかは裁定事項）。

- [ ] **Step 1: 発射台と拠点を実プレイで確かめる**

unity-playmode-recorded-playtest スキルの `scripts/run-scenario.sh` で次を走らせる（マスタは Task 3 のピンに合わせた worktree）:
- `tier9-4-rocket-launch`: ICチップLv1 20個を積んで打ち上げ、出口に計算データ20個が出る。
- `tier9-6-fusion-elevator`: 接続済みの拠点の入口に ICチップLv1 を19個入れても昇らず、20個目で昇り、240秒後に計算データ20個が出る（シナリオ内の待ち時間は既存の書き方に合わせる）。
- `tier9-1a-chip-line`: GPU設計を経ずにチップのラインが組める（直したシナリオが落ちない）。
- `tier9-5-compute-research`: ロケットで得た計算データで品質モジュールLv2 の研究が完了できる。
- `tier9-7-antimatter-final-research`: 半導体製造 → ロケット技術 → 軌道エレベーターの建設・核融合炉 → 反物質爆弾 → 最終研究 が、消した研究を経ずに完了できる。

Expected: 5本とも result.json が成功。失敗したら原因（シナリオの古い手順か、マスタか、Task 1 のコードか）を切り分けて直し、通るまで繰り返す。録画と result.json は worktree の gitignore 下に置く。

### Task 5: 全ブランチレビュー

- [ ] 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。指摘の反映がソースの判定経路・条件式・その評価時点に触れたら、Task 4 の録画テストを反映後のバイナリで再実施してから完了とする。

### Task 6: PR 作成とセッション終了可能状態

- [ ] セッション終了可能状態にすること: pr-create スキルで本体の PR を作成し、master とのコンフリクトがあれば master をマージして解消・コンパイル確認のうえ push する。マスタの PR（Task 2）と本体の PR の両方が作成済みで、本体のピンがマスタの PR の push 済みコミットを指し、全作業がコミット・push 済みで、このセッションをそのまま閉じても PR がマージ可能な状態になっていることを確認して終える。マージはしない（ユーザーが行う）。

## 判断記録（ADR）

- 設計の正: `docs/adr/0102-remove-gpu-and-carry-ic-chips-to-orbit.md`、`.decisions/2026-11-18-GPUを消しICチップをそのまま軌道へ運んで計算データを得る.md`。
- J1 1便に満たない積荷が入口にある状態は、新しい状態を足さず既存の「空（積荷待ち）」に入れる。出所: agent前提（プレイヤーに見える待ちの理由は「ICチップが20個そろっていない」で、今の「衛星待ち」と同じ対処＝積荷を入れる、になるため。文言で個数を伝える）。
- J2 C# の識別子・セーブのキー（`climbingSatellite`）・画面との通信の状態名（`waitingSatellite`）に残る Satellite は改名しない。出所: agent前提（セーブ形式と通信契約を変えずに済ませる。改名だけの変更は別の PR に切り出せる）。プレイヤーに見える文言は ICチップに直す。
- J3 テスト用 mod の GPU 連鎖（`ForUnitTestTier9ChainIds` と `Tier9ComputeLoopEndToEndTest`）は残す。出所: agent前提（本番マスタから独立した「レベル付きアイテムを積荷にして研究素材を得る連鎖」の結合テストで、積荷表の仕組みの回帰を守っている）。
- J4 クライアントの DataCenter・金属3Dプリンタのプレハブとアドレスは消さない。出所: agent前提（Unity 固有ファイルの手編集は禁止で、マスタから参照が消えれば読まれない）。
- J5 unityプレイ録画テストを Task 4 に含める（拠点の昇降というランタイム挙動に触れるため）。
- J6 1便の個数は拠点の在庫コンポーネントが生成時にマスタの積荷表から読む。到着サービスには問い合わせを足さない。出所: agent前提（ADR 0091「1便の個数はマスタの表から引く」と発射台の `RocketLaunchPadSlotLayout` の前例。Phase 2.6 の検査7で、到着サービスへ足す初案が ADR 0091 の線引きとずれると分かり直した）。
- J7 `SpaceElevatorBaseParamValidator.cs` の「運ぶ積荷は1便1個」の検査を消す。出所: agent前提（ADR 0102 の「昇降機が1回に運ぶ個数を積荷表の1便の個数に変える」。上限は `OrbitalPayloadMasterValidator.cs` の「Lv1 のスタック上限以下」が見ており、入口スロット1つに1便ぶんが入ることはそれで保証される）。
- J8 拠点の待ちの文言に個数（20）を書かない。出所: agent前提（個数はマスタの値で、画面へ送る経路を新しく作らないため、書くとマスタ変更で表示がずれる）。
- J10 消したアイテム・ブロック・研究を持つワールドは、既存のロード時の刈り込み（`moorestech_server/Assets/Scripts/Game.SaveLoad/Pruning/MissingMasterPruner.cs`。マスタに無いブロック・アイテム・研究をセーブから外して読み込む）に任せ、今回は移行ステップを足さない。出所: agent前提（ADR 0102 の「手当てしない」。Task 4 の前に、GPU を持つ手元のワールドが刈り込まれて読み込めることを1回確かめる）。
- J9 Phase 2.6 の弱い当たり: 「空」と「1便に足りない」を入口の状態 `Empty` 1つに入れる（J1）／出口に収まるかの判定が取り出し時と到着時の2か所にある（既存の作りのまま）。出所: agent前提（どちらもプレイヤーに見える差が無い）。

## 本PR外のリファクタ提案

- 既存 `moorestech_server/Assets/Scripts/Game.Block/Blocks/RocketLaunchPad/Save/RocketLaunchPadRetainedItemValidator.cs:41`（ロード時に積荷の個数が1便の個数と同じかを検査）と、新設の `SpaceElevatorClimberSaveRestorer` の個数の分岐は同じ役割。打ち上げ口で共有する形にまとめるかは人が決める。
