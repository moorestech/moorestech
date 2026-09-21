# 鉄道系設置プレビューの建設コスト不足表示 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 橋脚単体設置・レール接続モードの橋脚ゴースト・車両設置の3箇所で、建設コスト不足のとき設置不可色・送信停止・不足素材ツールチップ行を出す。

**Architecture:** 可否の判断を純関数の static クラス2つ（橋脚用・車両用）へ切り出し、各 PlaceSystem はそれを呼んで色と送信を決める。橋脚ゴーストを描く共有サービス `TrainRailPlaceSystemService` はコストを知らないまま保ち、最終色の塗り直し口だけを足す。

**Tech Stack:** Unity C#（Client.Game asmdef）／NUnit EditMode（Client.Tests）／VContainer DI

## Requirements

- R1 橋脚単体設置: 建設コスト不足のとき、橋脚プレビューが設置不可色になり、クリックしても `PlaceBlockProtocol` を送らず、不足素材行がツールチップに出る。受入: `TrainRailPierPlaceability.ApplyCostShortage` が不足時に `Placeable=false` と不足行1本以上を返すテストが通る。
- R2 レール接続モード: `previewData.IsPlaceable == false`（橋脚コスト不足・レール素材不足・長さ超過・曲率NG）または地面干渉のとき、橋脚ゴーストが設置不可色になる。受入: `TrainRailPierPlaceability.ApplyConnectJudgement` のテストが通る。
- R3 レール接続モードで不足行を二重に積まない（既存の `TrainRailPlacementFailureTooltipKey.Report` だけが積む）。受入: `TrainRailConnectSystem` から `ConstructionMaterialShortageReporter` を呼ばない。
- R4 車両設置: `trainCarMaster.RequiredItems` 1セットが不足のとき、車両プレビューが設置不可色になり、新規編成・既存編成への追加のどちらも送信せず、不足素材行が出る。受入: `TrainCarConstructionCostShortage.Calculate` のテストが通り、`TrainCarPlaceSystem` が不足時に `RequestPlacementAsync` へ到達しない。
- やらないこと: 曲線プレビューの色決定の変更（橋脚の地面干渉で曲線を赤にする案は棄却済み）。サーバー側の変更。橋脚の既存ブロック重複判定の追加。車両側への `FreeBlockPlacement` スキップ追加。

## Global Constraints

- 作業場所: `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/train-placement-cost-shortage-preview`（ブランチ `fix/train-placement-cost-shortage-preview`）。最初に `pwd` を確認する。メインクローンでは作業しない。
- 1ファイル200行未満。`TrainRailConnectSystem.cs` は現在222行で既に超過しているため、Task 2 で純増させない（行を足したら同数以上を削る）。
- 1ディレクトリ10ファイルまで。`PlaceSystem/TrainCar/` は既に13ファイルなので新規ファイルはサブディレクトリ `TrainCar/Cost/` に置く。
- `.meta` は手で作らない（Unity が生成したものをコミットする）。`partial`・`Func<>`・デフォルト引数・try-catch 禁止。
- コメントは「// 日本語 → // English」の2行セット、各1行。
- .cs を変更したら `uloop compile --project-path ./moorestech_client` を必ず通す。「Domain Reload in progress」は45秒待って再試行。
- テスト実行: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。
- コミットメッセージ末尾に `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`。
- タスク台帳: bd `moorestech-zjx5`（claim 済み）。

## File Structure

| ファイル | 責務 |
|---|---|
| Create `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRail/TrainRailPierPlaceability.cs` | 橋脚1セルの最終可否を決める純関数2つ（コスト不足／接続判定との合成） |
| Modify `.../PlaceSystem/TrainRail/TrainRailPlaceService.cs` | 最終色の塗り直し口 `UpdatePreviewColor(PlaceInfo)` を足す |
| Modify `.../PlaceSystem/TrainRail/TrainRailPlaceSystem.cs` | 財布と所持を受け取り、コスト不足を反映してから送信可否を決める |
| Modify `.../PlaceSystem/TrainRailConnect/TrainRailConnectSystem.cs` | 橋脚ゴーストへ接続判定を合成して塗り直す |
| Create `.../PlaceSystem/TrainCar/Cost/TrainCarConstructionCostShortage.cs` | 車両1両分の不足素材を返す純関数 |
| Modify `.../PlaceSystem/TrainCar/TrainCarPlaceSystem.cs` | 所持を受け取り、不足を色・行・送信停止へ反映 |
| Create `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/TrainRailConnect/TrainRailPierPlaceabilityTest.cs` | R1・R2 のテスト |
| Create `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/TrainCar/TrainCarConstructionCostShortageTest.cs` | R4 のテスト |

### 配置と前例（spec-architecture-review の結果）

| # | 項目 | 配置先 | 機構 | 前例・判定 |
|---|---|---|---|---|
| 1 | `TrainRailPierPlaceability` | Client.Game / PlaceSystem/TrainRail | static 純関数。`ConstructionMaterialShortageReporter`→`ConstructionCostPreviewMarker` の順で委譲 | `BeltConveyorPlaceSystem.cs:152-153`。ok |
| 2 | `TrainRailPlaceSystemService.UpdatePreviewColor` | 同上 | 既存 `IPlacementPreviewBlockGameObjectController.UpdatePlaceableColors` の素通し | サービスはコスト（上位の業務概念）を知らず、具体側が可否を決めて結果を渡す（AGENTS.md「汎用基盤にドメイン語彙を持ち込まない」）。ok |
| 3 | `TrainRailPlaceSystem` ctor へ `ILocalPlayerInventory`・`ConstructionWalletQuery` | 同上 | VContainer のコンストラクタ注入（両方 `MainGameModelRegistration.cs:44,73` で登録済み） | `BeltConveyorPlaceSystem.cs:42`。ok |
| 4 | `TrainCarConstructionCostShortage` | Client.Game / PlaceSystem/TrainCar/Cost | static 純関数。`ConstructionCostShortageCalculator.Calculate(requiredItems, 1, inventory)` | `ElectricWirePoleGhostPart.cs:45`。財布を通さないのはサーバー `PlaceTrainCarOnRailProtocol.cs:76-77` と同じ。ok |
| 5 | `TrainCarPlaceSystem` ctor へ `ILocalPlayerInventory` | 同上 | コンストラクタ注入 | 3 と同じ。ok |

データフロー: 入力（カーソル）→ 各 PlaceSystem（可否の書き手）→ `PlaceInfo.Placeable`／`isPlaceable` → プレビュー色・`PlacementFeedback`・送信。新規コンポーネントはいずれも「書き手が呼ぶ純関数」で、交差点（bool戻りでの逆流・第2の書き込み経路）は足さない。

死活表（同じ機構にぶら下がる既存操作）: 橋脚の R キー回転＝生きる（サービス内・未変更）／距離外の TooFar 行＝生きる／地面干渉の赤表示と理由行＝生きる（`ApplyGroundOverlapsAndReport` 未変更、合成は AND のみ）／接続モードの起点選択・右クリック解除・橋脚応答の引き継ぎ＝生きる（未変更）／車両の R キー候補送り・重なりハイライト・幾何理由行＝生きる。死ぬ操作なし。

---

### Task 1: 橋脚の可否ルール（純関数）とテスト

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRail/TrainRailPierPlaceability.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/TrainRailConnect/TrainRailPierPlaceabilityTest.cs`

**Interfaces:**
- Consumes: `ConstructionMaterialShortageReporter.ReportShortages(List<PlaceInfo>, BlockId, ConstructionWalletQuery, IEnumerable<IItemStack>, PlacementFeedback)`、`ConstructionCostPreviewMarker.MarkUnaffordableCellsAsNotPlaceable(List<PlaceInfo>, BlockId, ConstructionWalletQuery, IEnumerable<IItemStack>)`、`TrainRailConnectPreviewData.IsPlaceable`
- Produces:
  - `static void TrainRailPierPlaceability.ApplyCostShortage(PlaceInfo placeInfo, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems, PlacementFeedback feedback)`
  - `static void TrainRailPierPlaceability.ApplyConnectJudgement(PlaceInfo placeInfo, TrainRailConnectPreviewData previewData)`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.Construction;
using Client.Localization;
using Core.Item.Interface;
using Core.Master;
using Game.Construction;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.TrainRailConnect
{
    /// <summary>
    /// 橋脚1セルの最終可否（コスト不足・接続判定との合成）を検証する
    /// Verifies the final placeability of a single pier cell: cost shortage and the merge with the connect judgement
    /// </summary>
    public class TrainRailPierPlaceabilityTest
    {
        // TestTrainRailの建設コストはTest3×2
        // TestTrainRail costs Test3 x2
        private static readonly Guid PierMaterialGuid = Guid.Parse("00000000-0000-0000-1234-000000000003");

        [SetUp]
        public void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            Localize.Initialize();
        }

        [Test]
        public void コスト不足なら設置不可になり不足行が積まれる()
        {
            var placeInfo = BuildPierCell();
            var feedback = new PlacementFeedback();

            TrainRailPierPlaceability.ApplyCostShortage(placeInfo, BuildWalletQuery(), BuildInventory(1), feedback);

            Assert.IsFalse(placeInfo.Placeable);
            Assert.AreEqual(1, feedback.Lines.Count);
        }

        [Test]
        public void コストが足りていれば設置可のまま不足行も無い()
        {
            var placeInfo = BuildPierCell();
            var feedback = new PlacementFeedback();

            TrainRailPierPlaceability.ApplyCostShortage(placeInfo, BuildWalletQuery(), BuildInventory(2), feedback);

            Assert.IsTrue(placeInfo.Placeable);
            Assert.IsEmpty(feedback.Lines);
        }

        [Test]
        public void 地面干渉で既に不可のセルには不足行を積まない()
        {
            // 設置予定セルが0なので支払いも発生しない
            // No cell is about to be placed, so nothing is paid for
            var placeInfo = BuildPierCell();
            placeInfo.Placeable = false;
            var feedback = new PlacementFeedback();

            TrainRailPierPlaceability.ApplyCostShortage(placeInfo, BuildWalletQuery(), BuildInventory(0), feedback);

            Assert.IsFalse(placeInfo.Placeable);
            Assert.IsEmpty(feedback.Lines);
        }

        [Test]
        public void 接続判定が不可なら橋脚も不可になる()
        {
            var placeInfo = BuildPierCell();

            // Invalidは FailureReason が None でないため IsPlaceable=false
            // Invalid carries a non-None failure reason, so IsPlaceable is false
            TrainRailPierPlaceability.ApplyConnectJudgement(placeInfo, TrainRailConnectPreviewData.Invalid);

            Assert.IsFalse(placeInfo.Placeable);
        }

        private static PlaceInfo BuildPierCell()
        {
            return new PlaceInfo { Position = Vector3Int.zero, Placeable = true, BlockId = ForUnitTestModBlockId.TestTrainRail };
        }

        private static ConstructionWalletQuery BuildWalletQuery()
        {
            return new ConstructionWalletQuery(new ClientRemainingPlacementCountDatastore());
        }

        private static List<IItemStack> BuildInventory(int pierMaterialCount)
        {
            return new List<IItemStack> { ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(PierMaterialGuid), pierMaterialCount) };
        }
    }
}
```

確認済み: `TrainRailConnectPreviewData.Invalid` は `failureReason: InvalidNode`・`isCurvePlaceable: false`（`TrainRailConnectPreviewData.cs:42-43`）なので `IsPlaceable` は false。

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `TrainRailPierPlaceability` が存在しない旨の CS0103/CS0246

- [ ] **Step 3: 最小限の実装を書く**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Feedback;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainRailConnect;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Item.Interface;
using Game.Construction;
using Server.Protocol.PacketResponse;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainRail
{
    /// <summary>
    /// 橋脚1セルの最終的な設置可否を決める
    /// Settles the final placeability of a single pier cell
    /// </summary>
    public static class TrainRailPierPlaceability
    {
        // 橋脚単体設置用。不足行は可否を落とす前に積む（Reporterの契約）
        // For standalone pier placement; shortage lines are pushed before placeability drops (the reporter's contract)
        public static void ApplyCostShortage(PlaceInfo placeInfo, ConstructionWalletQuery walletQuery, IEnumerable<IItemStack> inventoryItems, PlacementFeedback feedback)
        {
            var placeInfos = new List<PlaceInfo> { placeInfo };
            ConstructionMaterialShortageReporter.ReportShortages(placeInfos, placeInfo.BlockId, walletQuery, inventoryItems, feedback);
            ConstructionCostPreviewMarker.MarkUnaffordableCellsAsNotPlaceable(placeInfos, placeInfo.BlockId, walletQuery, inventoryItems);
        }

        // 接続モード用。橋脚とレールは1リクエストなので、レール側が不可なら橋脚も不可
        // For connect mode; the pier and the rail travel in one request, so a failed rail judgement blocks the pier too
        public static void ApplyConnectJudgement(PlaceInfo placeInfo, TrainRailConnectPreviewData previewData)
        {
            if (!previewData.IsPlaceable) placeInfo.Placeable = false;
        }
    }
}
```

- [ ] **Step 4: コンパイルしてテストを通す**

Run: `uloop compile --project-path ./moorestech_client` → Expected: エラー0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainRailPierPlaceabilityTest"` → Expected: 4件 PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRail/TrainRailPierPlaceability.cs* moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/TrainRailConnect/TrainRailPierPlaceabilityTest.cs*
git commit -m "feat(place): 橋脚1セルの最終可否ルールを純関数として切り出す"
```

---

### Task 2: 橋脚単体設置と接続モードの橋脚ゴーストへ配線する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRail/TrainRailPlaceService.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRail/TrainRailPlaceSystem.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRailConnect/TrainRailConnectSystem.cs:110-124`

**Interfaces:**
- Consumes: Task 1 の `ApplyCostShortage`／`ApplyConnectJudgement`
- Produces: `public void TrainRailPlaceSystemService.UpdatePreviewColor(PlaceInfo placeInfo)`

- [ ] **Step 1: サービスへ塗り直し口を足す**

`TrainRailPlaceService.cs` の `Enable()` の直前に追加:

```csharp
        // 呼び出し側が確定させた最終可否でゴーストを塗り直す
        // Repaint the ghost with the final placeability the caller settled
        public void UpdatePreviewColor(PlaceInfo placeInfo)
        {
            _previewBlockController.UpdatePlaceableColors(new List<PlaceInfo> { placeInfo });
        }
```

- [ ] **Step 2: 橋脚単体設置へ配線する**

`TrainRailPlaceSystem.cs` を次の形にする（using に `Client.Game.InGame.UI.Inventory.Main` と `Game.Construction` を足す）:

```csharp
    public class TrainRailPlaceSystem : PlaceSystemBase<BlockPlacementTarget>
    {
        private readonly TrainRailPlaceSystemService _trainRailPlaceSystemService;
        private readonly ILocalPlayerInventory _localPlayerInventory;
        private readonly ConstructionWalletQuery _constructionWalletQuery;

        public TrainRailPlaceSystem(Camera mainCamera, IPlacementPreviewBlockGameObjectController previewBlockController, ILocalPlayerInventory localPlayerInventory, ConstructionWalletQuery constructionWalletQuery)
        {
            _trainRailPlaceSystemService = new TrainRailPlaceSystemService(mainCamera, previewBlockController);
            _localPlayerInventory = localPlayerInventory;
            _constructionWalletQuery = constructionWalletQuery;
        }
```

`ManualUpdate` の本体:

```csharp
            var blockId = target.BlockId;
            var placeInfo = _trainRailPlaceSystemService.ManualUpdate(blockId, feedback);

            // 距離外でプレビューが無ければ何もしない（理由行はサービスが積み済み）
            // Nothing to do without a preview beyond range (the service already pushed the reason)
            if (placeInfo == null) return;

            // 建設コスト不足を可否と理由行へ反映し、最終可否で塗り直す
            // Fold the construction cost shortage into placeability and the reason lines, then repaint with the final state
            TrainRailPierPlaceability.ApplyCostShortage(placeInfo, _constructionWalletQuery, _localPlayerInventory, feedback);
            _trainRailPlaceSystemService.UpdatePreviewColor(placeInfo);

            // 地面干渉・コスト不足で設置不可なセルは送信しない（理由行は上で積み済み）
            // Do not send a cell blocked by terrain or cost shortage (the reason lines are already pushed above)
            if (!placeInfo.Placeable) return;
            if (!InputManager.Playable.ScreenLeftClick.GetKeyUp || UiPointerHitTest.IsPointerOverAnyUi()) return;

            PlaceBlockProtocolSender.SendPlaceBlockProtocol(new List<PlaceInfo> { placeInfo });
```

`ILocalPlayerInventory` が `IEnumerable<IItemStack>` として渡せることは `BeltConveyorPlaceSystem.cs:152` が同じ形で渡しているのが根拠。

- [ ] **Step 3: 接続モードの橋脚ゴーストへ配線する**

`TrainRailConnectSystem.cs` の `ShowPreview(previewData);`（現119行）の直後、既存の「地面干渉・橋脚コスト不足…」コメント2行と `if (!placeInfo.Placeable || !previewData.IsPlaceable) return;` を次へ置き換える（コメント2行→2行、if 1行→3行で純増2行。代わりに現101-102行のコメント「橋脚未定義の場合は設置不可。仮に…」「No pier defined: …」を1行ずつに保ったまま、現107-108行の古いコメント2行「橋脚がある場合は設置可能。…」「Pier available: …」を削除して相殺する）:

```csharp
                        // レール判定の不可を橋脚ゴーストへ合成して塗り直す。どれか1つでも不可なら送らない（不足行はReportが積み済み）
                        // Merge a failed rail judgement into the pier ghost and repaint; any single failure blocks the send (Report already pushed the shortage lines)
                        TrainRailPierPlaceability.ApplyConnectJudgement(placeInfo, previewData);
                        _trainRailPlaceSystemService.UpdatePreviewColor(placeInfo);
                        if (!placeInfo.Placeable) return;
```

`ApplyConnectJudgement` 後の `placeInfo.Placeable` は「地面OK かつ previewData.IsPlaceable」と同値なので、送信ガードの意味は変更前と同じ。`TrainRailPierPlaceability` は `...PlaceSystem.TrainRail` 名前空間で、このファイルは既に using 済み（7行目）。

- [ ] **Step 4: 行数とコンパイルを確認する**

Run: `wc -l moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRailConnect/TrainRailConnectSystem.cs`
Expected: 222 以下（純増なし）
Run: `uloop compile --project-path ./moorestech_client` → Expected: エラー0（DI は VContainer がコンストラクタを解決するので登録側 `MainGameInteractionRegistration.cs:88` の変更は不要）

- [ ] **Step 5: 関連テストを回す**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainRail|PlaceSystemStateController|PlacementTarget"`
Expected: 全件 PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRail moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainRailConnect/TrainRailConnectSystem.cs
git commit -m "fix(place): 橋脚単体設置と接続モードの橋脚ゴーストへ建設コスト不足と接続判定を反映する"
```

---

### Task 3: 車両設置のコスト不足

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainCar/Cost/TrainCarConstructionCostShortage.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainCar/TrainCarPlaceSystem.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/TrainCar/TrainCarConstructionCostShortageTest.cs`

**Interfaces:**
- Consumes: `ConstructionCostShortageCalculator.Calculate(ConstructionRequiredItemElement[] requiredItems, int entityCount, IEnumerable<IItemStack> inventoryItems)`、`MasterHolder.TrainUnitMaster.GetTrainCarMaster(Guid)`、`PlacementFeedback.AddMaterialShortages(IReadOnlyList<ConstructionMaterialShortage>)`
- Produces: `static List<ConstructionMaterialShortage> TrainCarConstructionCostShortage.Calculate(Guid trainCarGuid, IEnumerable<IItemStack> inventoryItems)`

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar.Cost;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.PlaceSystem.TrainCar
{
    /// <summary>
    /// 車両1両分の建設コスト不足がサーバーと同じ基準（RequiredItems 1セット・財布なし）で出ることを検証する
    /// Verifies a single car's cost shortage follows the server's gate: one RequiredItems set, no wallet
    /// </summary>
    public class TrainCarConstructionCostShortageTest
    {
        // TestTrainCarの建設コストはTest3×3とTest4×2
        // TestTrainCar costs Test3 x3 and Test4 x2
        private static readonly Guid TestTrainCarGuid = Guid.Parse("dc82cf3f-709d-49eb-bdb2-67ffcaff561b");
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003");
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004");

        [SetUp]
        public void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 片方の素材だけ足りなければその素材だけが不足になる()
        {
            var shortages = TrainCarConstructionCostShortage.Calculate(TestTrainCarGuid, BuildInventory(3, 1));

            Assert.AreEqual(1, shortages.Count);
            Assert.AreEqual(MasterHolder.ItemMaster.GetItemId(Material2Guid), shortages[0].ItemId);
            Assert.AreEqual(1, shortages[0].Held);
            Assert.AreEqual(2, shortages[0].Required);
        }

        [Test]
        public void 全素材が足りていれば不足は空()
        {
            Assert.IsEmpty(TrainCarConstructionCostShortage.Calculate(TestTrainCarGuid, BuildInventory(3, 2)));
        }

        [Test]
        public void 何も持っていなければ全素材が不足になる()
        {
            Assert.AreEqual(2, TrainCarConstructionCostShortage.Calculate(TestTrainCarGuid, new List<IItemStack>()).Count);
        }

        private static List<IItemStack> BuildInventory(int material1Count, int material2Count)
        {
            return new List<IItemStack>
            {
                ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), material1Count),
                ServerContext.ItemStackFactory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), material2Count),
            };
        }
    }
}
```

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `TrainCarConstructionCostShortage` が存在しない旨のエラー

- [ ] **Step 3: 純関数を実装する**

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Item.Interface;
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar.Cost
{
    /// <summary>
    /// 車両1両分の建設コスト不足を返す
    /// Returns the construction cost shortage for a single train car
    /// </summary>
    public static class TrainCarConstructionCostShortage
    {
        // サーバーの車両設置は財布を通さずRequiredItems 1セットを直接検証するため、同じ基準で突き合わせる
        // The server validates one RequiredItems set directly without the wallet, so match against the same gate
        public static List<ConstructionMaterialShortage> Calculate(Guid trainCarGuid, IEnumerable<IItemStack> inventoryItems)
        {
            var trainCarMaster = MasterHolder.TrainUnitMaster.GetTrainCarMaster(trainCarGuid);
            return ConstructionCostShortageCalculator.Calculate(trainCarMaster.RequiredItems, 1, inventoryItems);
        }
    }
}
```

確認済み: `ConstructionMaterialShortage` は `PlaceSystem/Util/ConstructionMaterialShortage.cs` の readonly struct で、名前空間は `Client.Game.InGame.BlockSystem.PlaceSystem.Util`。

- [ ] **Step 4: TrainCarPlaceSystem へ配線する**

コンストラクタへ `ILocalPlayerInventory localPlayerInventory` を末尾引数として足し、フィールド `_localPlayerInventory` に保持する（using に `Client.Game.InGame.UI.Inventory.Main` と `Client.Game.InGame.BlockSystem.PlaceSystem.TrainCar.Cost`）。

`ManualUpdate` の「railpositionからpreviewを描画する」以降、クリック判定の手前までを次へ置き換える:

```csharp
            // 建設コスト不足は幾何の可否と合成して色へ反映する（サーバーは不足で拒否する）
            // Fold the construction cost shortage into the geometric placeability for the color (the server rejects on shortage)
            var materialShortages = TrainCarConstructionCostShortage.Calculate(target.TrainCarGuid, _localPlayerInventory);
            var isPlaceable = hit.IsPlaceable && materialShortages.Count == 0;

            // railpositionからpreviewを描画する
            // Render the preview directly from railposition
            var railPosition = hit.RailPosition;
            var hasPreview = railPosition != null && _previewController.ShowPreview(target.TrainCarGuid, railPosition, isPlaceable);
            _previewController.SetActive(hasPreview);

            // 候補が立たない理由と不足素材をツールチップへ積み、どちらかがあれば送らない
            // Push why no candidate holds and the short materials into the tooltip; either one blocks the send
            if (!hit.IsPlaceable) feedback.Add(new TooltipLine(TrainCarPlacementBlockReasonTooltipKey.ToKey(hit.BlockReason)));
            feedback.AddMaterialShortages(materialShortages);
            if (!isPlaceable) return;
```

確認済み: `AddMaterialShortages` は foreach で1件ずつ積むだけなので、空リストでは何も積まない（`PlacementFeedback.cs:39-41`）。`ILocalPlayerInventory` は `IEnumerable<IItemStack>` を継承しており（`LocalPlayerInventory.cs:13`）、そのまま渡せる。

- [ ] **Step 5: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → Expected: エラー0
Run: `wc -l .../TrainCar/TrainCarPlaceSystem.cs` → Expected: 200未満
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainCar"` → Expected: 全件 PASS（新規3件を含む）

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/TrainCar moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/TrainCar
git commit -m "fix(place): 車両設置プレビューへ建設コスト不足を反映する"
```

---

### Task 4: 全ブランチレビューと締め

- [ ] **Step 1:** `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceSystem"` を回し全件 PASS を確認する。`uloop get-logs --project-path ./moorestech_client --log-type Error` が空であることを確認する。
- [ ] **Step 2:** 必ず最後に moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。機械的修正を適用したら Step 1 のテストを再実行する（Workflow の apply は compile しか見ない）。レビュー反映が可否の判定経路に触れた場合は Task 1・3 のテストを再実行してから完了とする。
- [ ] **Step 3:** 残課題は `bd create --parent moorestech-zjx5` で1件ずつ起票し、PR本文には issue 番号を列挙する。既知の残課題候補: 実機（PlayMode）での目視確認は本planに含めていない — 行うか起票する。
- [ ] **Step 4:** pr-create スキルで master 向け PR を作る。作成直後に `bd close moorestech-zjx5 --reason="PR #<番号>"` と `moores-wt rm train-placement-cost-shortage-preview` を実行する。

## 判断記録（ADR）

- 設計ADR: `docs/adr/0068-train-placement-previews-reflect-construction-cost-shortage.md`／裁定記録: `.decisions/2026-09-21-鉄道系設置プレビューは建設コスト不足を全箇所で赤にし橋脚ゴーストは操作全体の可否に連動させる.md`
- **ADR 0068 の Consequences からの変更: `TrainRailPlaceSystemService` のコンストラクタには財布・所持を渡さない。** 受け取るのは `TrainRailPlaceSystem` 側で、サービスには最終色の塗り直し口だけを足す。サービスは単体設置と接続モードで共有される基盤であり、コスト判断を持たせると接続モードで不足行の二重積みを避ける分岐（モード引数）が要るため。
  出所: agent前提（AGENTS.md 設計原則「汎用基盤にドメイン語彙を持ち込まない。判断は具体側で行い、基盤には値をプッシュする」）
- **可否の判断を static 純関数2クラスへ切り出す。** PlaceSystem 本体は Camera・InputManager に依存し EditMode で起動できないため、テスト可能な単位をここに置く。
  出所: agent前提（`TrainRailConnectPreviewCalculator.EvaluateWithPierReservation` と `TrainRailPierReservationTest` の前例）
- **車両の幾何理由行と不足行は両方出す。** 幾何が不可のときも不足行を併記する。
  出所: agent前提（`BeltConveyorPlaceSystem` がセル理由と不足行を併記する前例）
- ユーザー方針: 2026-09-21 原文「重要な意思決定だけしたい」。上記3点は前例準拠の細部としてユーザーへ質問していない。
