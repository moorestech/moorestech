# Satisfactory式設置システム プラン3: クライアント通常ブロック移行 実装プラン

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 通常ブロックの設置を「ホットバー保持アイテム駆動＋旧プロトコル」から「ビルドメニュー選択（`BlockPlacementSelection`）＋新`PlaceBlockProtocol`（BlockId直指定・建設コスト消費）」へクライアント側で全面切替する。

**Architecture:** サーバー側基盤（`va:placeBlock`・`ConstructionCostService`・Block/TrainCarアンロック配信）はプラン1で実装済み。本プランは (1)アンロック同期のクライアント3段配線、(2)選択状態ホルダー`BlockPlacementSelection`と`PlaceSystemUpdateContext`拡張、(3)`CommonBlockPlaceSystem`のBlockId駆動化、(4)シーン常駐の`BuildMenuView`＋`BuildMenuState`（UIStateパターン準拠・TryConsumeポーリングの一方通行フロー）を積み上げる。特殊設置システム（TrainRail/TrainCar/Connect系）は保持アイテム駆動のまま残す（プラン4）。

**Tech Stack:** Unity C# / VContainer(DI) / UniRx / uGUI / uloop CLI（execute-dynamic-codeでシーン編集）

**Spec:** `docs/superpowers/specs/2026-07-03-satisfactory-style-placement-design.md`
**申し送り:** `docs/superpowers/plans/2026-07-05-satisfactory-placement-handoff.md`（必読: 基盤API・技術的な罠）

## 合意済みの設計判断

- **通常ブロックのホットバー設置はクライアントから廃止**（`PlaceSystemSelector`の`IsBlock`フォールバック撤去）。特殊システム（UsePlaceItemsマッチング・歯車ポール分岐）は保持アイテム駆動のまま維持
- **カテゴリタブはプラン2でカテゴリデータが入ってから**。本プランはSortPriority順の単一グリッド
- **ビルドメニューのエントリは既存`ItemSlotView.Prefab`を流用**（新規プレハブのデザイン作業を回避）。コストはツールチップで表示。スペックの「所持数表示・不足素材の赤字」はメニューv1では省略し、設置プレビューの赤表示（賄えるセル数超過）で充足情報を担保する（スコープ縮小の合意事項）
- **選択→設置モードの受け渡しはTryConsumeポーリング**（コールバック逆流禁止のプロジェクト方針）
- **本番マスタ（v8 mod）へ暫定解放（initialUnlocked: true全ブロック、requiredItems無し）を投入**（ユーザー承認済み。無償設置＋アイテム返却の軽度増殖は開発期間中許容、プラン2で正式データに置換）

## Global Constraints

- 作業開始時に必ず`pwd`確認。作業ディレクトリは `/Users/katsumi/moorestech`
- .csファイル変更後は必ず `uloop compile --project-path ./moorestech_client`（エラー0件）
- テスト: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。Domain Reloadエラーは45秒待ってリトライ。一括180秒超はクラス分割
- partial絶対禁止 / 1ファイル200行以下 / 1ディレクトリ10ファイルまで / 単純getter/setter禁止（値のSetは`SetHoge`メソッド） / デフォルト引数禁止（追加引数は全呼び出し側を更新） / try-catch禁止 / イベントはUniRx
- `[SerializeField]`は_無しの小文字キャメルケース
- 日英2行セットコメント（各1行厳守、日本語目安20字・メソッド30字）を約3〜10行ごと。自明コメント禁止。根拠コメントは長くても可
- `#region Internal`はメソッド内ローカル関数まとめ用途のみ（クラス直下privateメソッド囲いは禁止）
- **Prefab・シーンのテキスト直編集は禁止。シーン/プレハブ変更は必ず`uloop execute-dynamic-code`（Unity Editor経由）で行う**。.meta手動作成禁止
- MessagePackの既存Key番号・enum順序の変更禁止
- クライアントテストで`Core.Inventory`等を参照する際は`global::`修飾が必要な場合がある（`Tests.*.Core`名前空間と衝突）
- moorestech_master編集時はmooreseditor.appを終了しておくこと。moorestech_masterへのコミット後は`.moorestech-external-revisions.json`のピン更新に注意（RepositorySyncによる巻き戻り対策）
- 各タスク完了時にコミット

---

### Task 1: アンロック同期のクライアント配線（Block/TrainCar）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/Responses.cs`（`UnlockStateResponse`: 142-171行付近）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`（`GetUnlockState`: 217-228行付近）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UnlockState/ClientGameUnlockStateDatastore.cs`（クラス名`ClientGameUnlockStateData`）

**Interfaces:**
- Consumes: サーバー応答`ResponseGameUnlockStateProtocolMessagePack.UnlockedBlockGuids/LockedBlockGuids/UnlockedTrainCarGuids/LockedTrainCarGuids`（Key10-13、プラン1で配信済み）、イベント`UnlockEventMessagePack.UnlockedBlockGuid/UnlockedTrainCarGuid`
- Produces: `UnlockStateResponse.LockedBlockGuids/UnlockedBlockGuids/LockedTrainCarGuids/UnlockedTrainCarGuids`（`List<Guid>`）、`ClientGameUnlockStateData.BlockUnlockStateInfos`が充填済みになる（Task 6のビルドメニューが解放フィルタに使う）

- [ ] **Step 1: UnlockStateResponseにBlock/TrainCarフィールドを追加**

`Responses.cs`の`UnlockStateResponse`クラスに、既存4ドメイン（CraftRecipe/Item/ChallengeCategory/MachineRecipe）と同じ形式でプロパティ4本とコンストラクタ引数4本を末尾追加する（既存の命名・プロパティ形式に完全に合わせること）:

```csharp
        public List<Guid> LockedBlockGuids { get; }
        public List<Guid> UnlockedBlockGuids { get; }
        public List<Guid> LockedTrainCarGuids { get; }
        public List<Guid> UnlockedTrainCarGuids { get; }
```

コンストラクタは既存8引数の後に`List<Guid> lockedBlockGuids, List<Guid> unlockedBlockGuids, List<Guid> lockedTrainCarGuids, List<Guid> unlockedTrainCarGuids`を追加し代入する。

- [ ] **Step 2: GetUnlockStateで新4リストを渡す**

`VanillaApiWithResponse.cs`の`GetUnlockState`内、`new UnlockStateResponse(...)`の引数末尾に以下を追加:

```csharp
                response.LockedBlockGuids, response.UnlockedBlockGuids,
                response.LockedTrainCarGuids, response.UnlockedTrainCarGuids
```

- [ ] **Step 3: ClientGameUnlockStateDataの充填とイベント反映**

`ClientGameUnlockStateDatastore.cs`を修正する:

(a) コンストラクタの既存4ドメイン充填ループの後に、同じ形式でBlock/TrainCarの充填を追加（「Task 5でハンドシェイクから充填」コメントは削除）:

```csharp
            // ブロックと列車車両の解放状態をハンドシェイク応答から充填する
            // Fill block and train car unlock states from the handshake response
            foreach (var locked in initialHandshakeResponse.UnlockState.LockedBlockGuids)
            {
                _blockUnlockStateInfos[locked] = new BlockUnlockStateInfo(locked, false);
            }
            foreach (var unlocked in initialHandshakeResponse.UnlockState.UnlockedBlockGuids)
            {
                _blockUnlockStateInfos[unlocked] = new BlockUnlockStateInfo(unlocked, true);
            }
            foreach (var locked in initialHandshakeResponse.UnlockState.LockedTrainCarGuids)
            {
                _trainCarUnlockStateInfos[locked] = new TrainCarUnlockStateInfo(locked, false);
            }
            foreach (var unlocked in initialHandshakeResponse.UnlockState.UnlockedTrainCarGuids)
            {
                _trainCarUnlockStateInfos[unlocked] = new TrainCarUnlockStateInfo(unlocked, true);
            }
```

（辞書フィールド名・`BlockUnlockStateInfo`/`TrainCarUnlockStateInfo`のコンストラクタ`(Guid, bool)`は既存定義に合わせる。実ファイルのフィールド名が異なる場合は現物に合わせること）

(b) `OnUpdateUnlock`のswitchで、安全弁`case UnlockEventType.Block: case UnlockEventType.TrainCar: break;`を実装に置き換える:

```csharp
                case UnlockEventType.Block:
                    _blockUnlockStateInfos[unlockEvent.UnlockedBlockGuid] = new BlockUnlockStateInfo(unlockEvent.UnlockedBlockGuid, true);
                    break;
                case UnlockEventType.TrainCar:
                    _trainCarUnlockStateInfos[unlockEvent.UnlockedTrainCarGuid] = new TrainCarUnlockStateInfo(unlockEvent.UnlockedTrainCarGuid, true);
                    break;
```

（メソッド内のイベント変数名は現物に合わせる。既存caseが`IsUnlocked=true`の新インスタンス代入以外の形式ならそれに合わせる）

- [ ] **Step 4: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/
git commit -m "feat: ブロック・列車車両の解放状態をクライアントへ配線"
```

---

### Task 2: 新プロトコル送信API

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs`（`PlaceHotBarBlock`の直後）

**Interfaces:**
- Consumes: `PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(int playerId, BlockId blockId, List<PlaceInfo> placeInfos)`（サーバー実装済み）
- Produces: `VanillaApiSendOnly.PlaceBlock(List<PlaceInfo> placePositions, BlockId blockId)`（Task 5が使用）

- [ ] **Step 1: 送信メソッドを追加**

`VanillaApiSendOnly.cs`の`PlaceHotBarBlock`メソッドの直後に追加（`using Core.Master;`が無ければ追加）:

```csharp
        public void PlaceBlock(List<PlaceInfo> placePositions, BlockId blockId)
        {
            var request = new PlaceBlockProtocol.SendPlaceBlockProtocolMessagePack(_playerId, blockId, placePositions);
            _packetSender.Send(request);
        }
```

- [ ] **Step 2: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 3: コミット**

```bash
git add moorestech_client/
git commit -m "feat: 新PlaceBlockProtocolのクライアント送信APIを追加"
```

---

### Task 3: BlockPlacementSelectionと設置コンテキスト拡張

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BlockPlacementSelection.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/IPlaceSystem.cs`（`PlaceSystemUpdateContext` struct: 14-29行付近）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs`（コンストラクタ＋`CreateContext()`: 47-61行付近）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（DI登録追加）
- Modify: `PlaceSystemUpdateContext`をnewしている全箇所（テスト含む。grepで特定）

**Interfaces:**
- Produces:
  - `BlockPlacementSelection`: `BlockId? SelectedBlockId { get; }` / `void SetSelectedBlock(BlockId blockId)` / `void ClearSelection()`（DIシングルトン）
  - `PlaceSystemUpdateContext.SelectedBlockId`（`BlockId?`、readonlyフィールド）
- Consumes: なし

- [ ] **Step 1: BlockPlacementSelectionを作成**

```csharp
using Core.Master;

namespace Client.Game.InGame.BlockSystem.PlaceSystem
{
    /// <summary>
    /// ビルドメニューで選択中の設置対象ブロック
    /// The block currently selected in the build menu for placement
    /// </summary>
    public class BlockPlacementSelection
    {
        public BlockId? SelectedBlockId { get; private set; }

        public void SetSelectedBlock(BlockId blockId)
        {
            SelectedBlockId = blockId;
        }

        public void ClearSelection()
        {
            SelectedBlockId = null;
        }
    }
}
```

- [ ] **Step 2: PlaceSystemUpdateContextにSelectedBlockIdを追加**

`IPlaceSystem.cs`の`PlaceSystemUpdateContext`にreadonlyフィールドとコンストラクタ引数（末尾）を追加:

```csharp
        public readonly BlockId? SelectedBlockId;
```

コンストラクタ末尾に`BlockId? selectedBlockId`を追加し代入（デフォルト引数禁止のため、全呼び出し側を更新する）。

- [ ] **Step 3: 呼び出し側を更新**

Run: `grep -rn "new PlaceSystemUpdateContext(" moorestech_client/Assets/Scripts`

- `PlaceSystemStateController.CreateContext()`: コンストラクタに`BlockPlacementSelection`を追加注入し、`_blockPlacementSelection.SelectedBlockId`を渡す
- テスト等の他の呼び出し箇所: `null`を渡す（特殊システムのテストは選択なしで正しい）

`PlaceSystemStateController`のコンストラクタ変更に伴い、その生成箇所（DI解決なら変更不要。手動newならgrepで特定して引数追加）を確認する。

- [ ] **Step 4: DI登録**

`MainGameStarter.cs`のシングルトン登録群（192-203行付近の`builder.Register<...>(Lifetime.Singleton)`が並ぶ場所）に追加:

```csharp
            builder.Register<BlockPlacementSelection>(Lifetime.Singleton);
```

- [ ] **Step 5: コンパイル＋既存テスト回帰**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceSystem|GearChainPole"`
Expected: エラー0件・全PASS

- [ ] **Step 6: コミット**

```bash
git add moorestech_client/
git commit -m "feat: ビルドメニュー選択を保持するBlockPlacementSelectionを追加"
```

---

### Task 4: ConstructionCostPreviewCalculator（純関数＋EditModeテスト）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/ConstructionCostPreviewCalculator.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/ConstructionCostPreviewCalculatorTest.cs`

**Interfaces:**
- Consumes: `ConstructionRequiredItemElement`（`.ItemGuid`/`.Count`、nullable配列）、`IItemStack`
- Produces: `static int ConstructionCostPreviewCalculator.CalculateAffordableCellCount(ConstructionRequiredItemElement[] requiredItems, IEnumerable<IItemStack> inventoryItems)`（コスト未定義なら`int.MaxValue`）

- [ ] **Step 1: 失敗するテストを書く**

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.BlockSystem.PlaceSystem.Util;
using Core.Master;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

// namespaceは既存の隣接テスト（CommonBlockPlacePointCalculatorTest等）に合わせること
// Match the namespace of sibling tests such as CommonBlockPlacePointCalculatorTest
namespace Client.Tests.PlaceSystem
{
    public class ConstructionCostPreviewCalculatorTest
    {
        private static readonly Guid Material1Guid = Guid.Parse("00000000-0000-0000-1234-000000000003"); // Test3(コスト×2)
        private static readonly Guid Material2Guid = Guid.Parse("00000000-0000-0000-1234-000000000004"); // Test4(コスト×1)

        [Test]
        public void 素材所持数から設置可能セル数を算出する()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;
            var factory = ServerContext.ItemStackFactory;

            // Test3=5個(2セル半) Test4=2個(2セル) → 賄えるのは2セル
            // Test3=5 (2.5 cells) and Test4=2 (2 cells) afford exactly 2 cells
            var inventory = new List<global::Core.Item.Interface.IItemStack>
            {
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 5),
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material2Guid), 2),
            };

            Assert.AreEqual(2, ConstructionCostPreviewCalculator.CalculateAffordableCellCount(requiredItems, inventory));
        }

        [Test]
        public void コスト未定義ならMaxValueを返す()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BeltConveyorId).RequiredItems;

            Assert.AreEqual(int.MaxValue, ConstructionCostPreviewCalculator.CalculateAffordableCellCount(requiredItems, new List<global::Core.Item.Interface.IItemStack>()));
        }

        [Test]
        public void 素材が1種でも足りなければ0セル()
        {
            CreateServer();
            var requiredItems = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.BlockId).RequiredItems;
            var factory = ServerContext.ItemStackFactory;

            // Test4を持っていないため0セル
            // Zero cells because no Test4 is held
            var inventory = new List<global::Core.Item.Interface.IItemStack>
            {
                factory.Create(MasterHolder.ItemMaster.GetItemId(Material1Guid), 10),
            };

            Assert.AreEqual(0, ConstructionCostPreviewCalculator.CalculateAffordableCellCount(requiredItems, inventory));
        }

        private static void CreateServer()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }
    }
}
```

- [ ] **Step 2: コンパイルエラー（RED）を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ConstructionCostPreviewCalculator`未定義のエラー

- [ ] **Step 3: 実装**

```csharp
using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Util
{
    /// <summary>
    /// 建設コストで賄える設置セル数を算出
    /// Calculates how many placement cells the held materials can afford
    /// </summary>
    public static class ConstructionCostPreviewCalculator
    {
        public static int CalculateAffordableCellCount(ConstructionRequiredItemElement[] requiredItems, IEnumerable<IItemStack> inventoryItems)
        {
            if (requiredItems == null || requiredItems.Length == 0) return int.MaxValue;

            // 素材ごとの所持数からセル数の最小値を取る
            // Take the minimum affordable cells across materials
            var affordableCellCount = int.MaxValue;
            foreach (var requiredItem in requiredItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(requiredItem.ItemGuid);
                var total = 0;
                foreach (var stack in inventoryItems)
                {
                    if (stack.Id != itemId) continue;
                    total += stack.Count;
                }
                affordableCellCount = Math.Min(affordableCellCount, total / requiredItem.Count);
            }

            return affordableCellCount;
        }
    }
}
```

- [ ] **Step 4: テストPASS確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "ConstructionCostPreviewCalculatorTest"`
Expected: 3件PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/
git commit -m "feat: 建設コストの設置可能セル数計算をクライアントに追加"
```

---

### Task 5: CommonBlockPlaceSystemのBlockId駆動化とセレクタ切替

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs`（HoldingItemId参照3箇所＋所持数判定＋送信）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemSelector.cs`（IsBlockフォールバック: 71-74行付近）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs`（送信ユーティリティ追加: 137-143行付近）

**Interfaces:**
- Consumes: Task 2の`VanillaApiSendOnly.PlaceBlock`、Task 3の`context.SelectedBlockId`、Task 4の`ConstructionCostPreviewCalculator`
- Produces: `PlaceSystemUtil.SendPlaceBlockProtocol(List<PlaceInfo> currentPlaceInfos, BlockId blockId)`。通常ブロックは「ビルドメニュー選択があるときだけ」CommonBlockPlaceSystemが発火する挙動

- [ ] **Step 1: PlaceSystemUtilに新送信メソッドを追加**

`SendPlaceProtocol`の直後に追加（既存メソッドは特殊システムが使うため残す）:

```csharp
        public static void SendPlaceBlockProtocol(List<PlaceInfo> currentPlaceInfos, BlockId blockId)
        {
            ClientContext.VanillaApi.SendOnly.PlaceBlock(currentPlaceInfos, blockId);
            SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.PlaceBlock);
        }
```

- [ ] **Step 2: PlaceSystemSelectorのフォールバックを選択駆動に変更**

`GetCurrentPlaceSystem`の`MasterHolder.BlockMaster.IsBlock(context.HoldingItemId)`分岐（71-74行付近）を以下に置き換える（UsePlaceItemsマッチング・歯車ポール分岐は**変更しない**）:

```csharp
            // ビルドメニューで選択中なら通常ブロック設置システムを使う
            // Use the common placement system while a build-menu selection exists
            if (context.SelectedBlockId.HasValue) return _commonBlockPlaceSystem;

            return _emptyPlaceSystem;
```

（`_emptyPlaceSystem`等のフィールド名は現物に合わせる）

- [ ] **Step 3: CommonBlockPlaceSystemのHoldingItemId依存を差し替え**

現物のファイルを読み、以下の3系統を置き換える（行番号は目安。プラン1以降の変更でずれている可能性があるためコードで探すこと）:

(a) プレビュー用ブロックマスタ取得（113行付近）:
`MasterHolder.BlockMaster.GetBlockMaster(context.HoldingItemId)` → `MasterHolder.BlockMaster.GetBlockMaster(context.SelectedBlockId.Value)`

(b) 自動接続プレビュー用ブロックID（152行付近）:
`MasterHolder.BlockMaster.GetBlockId(context.HoldingItemId)` → `context.SelectedBlockId.Value`

(c) 所持数判定`MarkInsufficientItemPreviewsAsNotPlaceable`（218-238行付近）を建設コスト方式に全置換:

```csharp
            void MarkInsufficientItemPreviewsAsNotPlaceable()
            {
                // 建設コストで賄えるセル数まで設置可にする
                // Allow placement up to the affordable cell count
                var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(context.SelectedBlockId.Value);
                var affordableCellCount = ConstructionCostPreviewCalculator.CalculateAffordableCellCount(blockMaster.RequiredItems, _localPlayerInventory);

                var placeableCount = 0;
                for (var i = 0; i < _currentPlaceInfos.Count; i++)
                {
                    if (!_currentPlaceInfos[i].Placeable) continue;
                    placeableCount++;
                    if (placeableCount > affordableCellCount)
                    {
                        _currentPlaceInfos[i].Placeable = false;
                    }
                }
            }
```

(d) 送信（211行付近）: `SendPlaceProtocol(_currentPlaceInfos, context)` → `PlaceSystemUtil.SendPlaceBlockProtocol(_currentPlaceInfos, context.SelectedBlockId.Value)`（呼び出し形式は現物のusing/クラス内配置に合わせる）

`CommonBlockPlaceSystem`内に他の`HoldingItemId`/`CurrentSelectHotbarSlotIndex`参照が残っていないかgrepで確認し、残っていれば同様にSelectedBlockId駆動へ置換（`IsSelectSlotChanged`によるプレビューリセット等は「選択変更」の意味なので、`SelectedBlockId`の変化検知に置き換えるか、影響がなければ残して報告）。

- [ ] **Step 4: コンパイル＋回帰**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceSystem|GearChainPole|ConveyorOverpass"`
Expected: エラー0件・全PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/
git commit -m "feat: 通常ブロック設置をビルドメニュー選択駆動＋新プロトコルに切替"
```

---

### Task 6: BuildMenuViewとBuildMenuState（コードのみ）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuView.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/UIStateEnum.cs`（`BuildMenu`追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/UIStateDictionary.cs`（登録追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/GameScreenState.cs`（46行付近: B→BuildMenu）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs`（54行付近: B→BuildMenu）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs`（Tab→BuildMenu追加、キー説明更新）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（SerializeField・RegisterComponent・State登録）

**Interfaces:**
- Consumes: Task 1の`IGameUnlockStateData.BlockUnlockStateInfos`、Task 3の`BlockPlacementSelection`、既存`ItemSlotView.Prefab`/`SetItem(ItemViewData, int, string)`/`OnLeftClickUp`、`ClientContext.ItemImageContainer.GetItemView(ItemId)`、`MasterHolder.BlockMaster.Blocks.Data`/`GetBlockId(Guid)`/`GetItemId(BlockId)`
- Produces: `BuildMenuView.SetActive(bool)` / `bool TryConsumeSelectedBlock(out BlockId selectedBlockId)`、`UIStateEnum.BuildMenu`、遷移: GameScreen/DeleteBar --B--> BuildMenu --クリック選択--> PlaceBlock --Tab--> BuildMenu

- [ ] **Step 1: BuildMenuViewを作成**

```csharp
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Core.Master;
using Game.UnlockState;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;
using VContainer;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    /// 解放済みブロックのグリッドを表示する設置メニュー
    /// Build menu grid showing unlocked placeable blocks
    /// </summary>
    public class BuildMenuView : MonoBehaviour
    {
        [SerializeField] private RectTransform blockListContainer;

        [Inject] private IGameUnlockStateData _gameUnlockStateData;

        private readonly List<ItemSlotView> _slotViews = new();
        private BlockId? _clickedBlockId;

        public void SetActive(bool active)
        {
            gameObject.SetActive(active);
            if (active) RebuildBlockList();
        }

        public bool TryConsumeSelectedBlock(out BlockId selectedBlockId)
        {
            // クリック済み選択を1回だけ消費する（一方通行フロー）
            // Consume the clicked selection once (one-way flow)
            if (_clickedBlockId.HasValue)
            {
                selectedBlockId = _clickedBlockId.Value;
                _clickedBlockId = null;
                return true;
            }

            selectedBlockId = default;
            return false;
        }

        private void RebuildBlockList()
        {
            foreach (var slotView in _slotViews) Destroy(slotView.gameObject);
            _slotViews.Clear();
            _clickedBlockId = null;

            // 解放済みブロックをソート順に列挙してスロット生成
            // Enumerate unlocked blocks in sort order and create slots
            var unlockedBlocks = MasterHolder.BlockMaster.Blocks.Data
                .Where(IsUnlocked)
                .OrderBy(b => b.SortPriority ?? 0)
                .ThenBy(b => b.Name)
                .ToList();
            foreach (var blockMaster in unlockedBlocks)
            {
                var blockId = MasterHolder.BlockMaster.GetBlockId(blockMaster.BlockGuid);
                var itemId = MasterHolder.BlockMaster.GetItemId(blockId);
                var itemView = ClientContext.ItemImageContainer.GetItemView(itemId);

                var slotView = Instantiate(ItemSlotView.Prefab, blockListContainer);
                slotView.SetItem(itemView, 0, CreateToolTipText(blockMaster));
                slotView.OnLeftClickUp.Subscribe(_ => _clickedBlockId = blockId).AddTo(slotView);
                _slotViews.Add(slotView);
            }
        }

        private bool IsUnlocked(BlockMasterElement blockMaster)
        {
            return _gameUnlockStateData.BlockUnlockStateInfos.TryGetValue(blockMaster.BlockGuid, out var state) && state.IsUnlocked;
        }

        private static string CreateToolTipText(BlockMasterElement blockMaster)
        {
            var builder = new StringBuilder(blockMaster.Name);
            if (blockMaster.RequiredItems == null || blockMaster.RequiredItems.Length == 0) return builder.ToString();

            // ツールチップに建設コストを列挙する
            // List the construction cost in the tooltip
            foreach (var requiredItem in blockMaster.RequiredItems)
            {
                var itemName = MasterHolder.ItemMaster.GetItemMaster(requiredItem.ItemGuid).Name;
                builder.Append($"\n{itemName} x{requiredItem.Count}");
            }

            return builder.ToString();
        }
    }
}
```

- [ ] **Step 2: UIStateEnumにBuildMenuを追加**

`UIStateEnum.cs`の列挙の末尾に`BuildMenu,`を追加する（既存値の順序は変更しない）。

- [ ] **Step 3: BuildMenuStateを作成**

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem;
using Client.Game.InGame.UI.BuildMenu;
using Client.Game.InGame.UI.KeyControl;
using Client.Input;
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State
{
    public class BuildMenuState : IUIState
    {
        private readonly BuildMenuView _buildMenuView;
        private readonly BlockPlacementSelection _blockPlacementSelection;

        public BuildMenuState(BuildMenuView buildMenuView, BlockPlacementSelection blockPlacementSelection)
        {
            _buildMenuView = buildMenuView;
            _blockPlacementSelection = blockPlacementSelection;
        }

        public void OnEnter(UITransitContext context)
        {
            _buildMenuView.SetActive(true);
            InputManager.MouseCursorVisible(true);
            KeyControlDescription.Instance.SetText("クリック: 設置ブロック選択  B: 閉じる");
        }

        public UITransitContext GetNextUpdate()
        {
            // 選択が確定したら設置モードへ遷移する
            // Transition to placement mode once a block is selected
            if (_buildMenuView.TryConsumeSelectedBlock(out var selectedBlockId))
            {
                _blockPlacementSelection.SetSelectedBlock(selectedBlockId);
                return new UITransitContext(UIStateEnum.PlaceBlock);
            }

            if (InputManager.UI.CloseUI.GetKeyDown || UnityEngine.Input.GetKeyDown(KeyCode.B)) return new UITransitContext(UIStateEnum.GameScreen);
            if (InputManager.UI.OpenInventory.GetKeyDown) return new UITransitContext(UIStateEnum.PlayerInventory);

            return null;
        }

        public void OnExit()
        {
            _buildMenuView.SetActive(false);
            InputManager.MouseCursorVisible(false);
        }
    }
}
```

- [ ] **Step 4: 遷移の変更**

- `GameScreenState.cs`（46行付近）: `UIStateEnum.PlaceBlock` → `UIStateEnum.BuildMenu`
- `DeleteObjectState.cs`（54行付近）: `UIStateEnum.PlaceBlock` → `UIStateEnum.BuildMenu`
- `PlaceBlockState.cs`の`GetNextUpdate`に追加（B→GameScreen判定の前）:

```csharp
            // Tabでビルドメニューを開き直す
            // Reopen the build menu with Tab
            if (UnityEngine.Input.GetKeyDown(KeyCode.Tab)) return new UITransitContext(UIStateEnum.BuildMenu);
```

- `PlaceBlockState.cs`のOnEnterのキー説明テキストを更新: 「1~9: 設置ブロック選択」を「Tab: ブロック選択」に差し替え（残りのキー説明は維持）

- [ ] **Step 5: UIStateDictionaryとMainGameStarterへの登録**

- `UIStateDictionary.cs`: コンストラクタ引数に`BuildMenuState buildMenuState`を追加し、`_stateDictionary.Add(UIStateEnum.BuildMenu, buildMenuState);`を追加
- `MainGameStarter.cs`:
  - SerializeField群（96-103行付近）に`[SerializeField] private BuildMenuView buildMenuView;`を追加
  - State登録群（192-203行付近）に`builder.Register<BuildMenuState>(Lifetime.Singleton);`を追加
  - コンポーネント登録群（263-269行付近）に`builder.RegisterComponent(buildMenuView);`を追加

- [ ] **Step 6: コンパイル確認（シーン未配線でもコンパイルは通る）**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [ ] **Step 7: コミット**

```bash
git add moorestech_client/
git commit -m "feat: ビルドメニューUIのViewとUIStateを追加"
```

---

### Task 7: シーン配線（uloop execute-dynamic-code）

**Files:**
- Modify: `moorestech_client/Assets/Scenes/Game/MainGame.unity`（**Unity Editor経由のみ。テキスト編集禁止**）

**Interfaces:**
- Consumes: Task 6の`BuildMenuView`（`blockListContainer`をSerializeFieldで要求）、`MainGameStarter.buildMenuView`フィールド
- Produces: MainGameシーンにBuildMenuViewパネル（初期非アクティブ）が常駐し、MainGameStarterに配線済み

- [ ] **Step 1: uloop execute-dynamic-codeでシーンにパネルを構築**

uloop-execute-dynamic-codeスキルを起動し、以下の処理を行うエディタコードを実行する（APIの細部はスキルの作法に従い、失敗時は分割実行してよい）:

```csharp
// MainGameシーンを開く
// Open the MainGame scene
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Game/MainGame.unity");

// MainGameStarterと既存UIのCanvasを特定する
// Locate MainGameStarter and the canvas hosting existing UI panels
var starter = UnityEngine.Object.FindFirstObjectByType<Client.Starter.MainGameStarter>(UnityEngine.FindObjectsInactive.Include);
var starterSo = new UnityEditor.SerializedObject(starter);
var challengeView = starterSo.FindProperty("challengeListView").objectReferenceValue as UnityEngine.Component;
var canvas = challengeView.GetComponentInParent<UnityEngine.Canvas>(true);

// パネル本体（全面・半透明背景・初期非アクティブ）
// Panel root: full-stretch, translucent background, initially inactive
var panel = new UnityEngine.GameObject("BuildMenuView", typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.Image));
panel.transform.SetParent(canvas.transform, false);
var panelRect = panel.GetComponent<UnityEngine.RectTransform>();
panelRect.anchorMin = UnityEngine.Vector2.zero; panelRect.anchorMax = UnityEngine.Vector2.one;
panelRect.offsetMin = UnityEngine.Vector2.zero; panelRect.offsetMax = UnityEngine.Vector2.zero;
panel.GetComponent<UnityEngine.UI.Image>().color = new UnityEngine.Color(0f, 0f, 0f, 0.6f);

// スクロール領域（Viewport + Content(GridLayoutGroup)）
// Scroll area: viewport + grid content
var scroll = new UnityEngine.GameObject("BlockListScroll", typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.ScrollRect));
scroll.transform.SetParent(panel.transform, false);
var scrollRect = scroll.GetComponent<UnityEngine.RectTransform>();
scrollRect.anchorMin = new UnityEngine.Vector2(0.15f, 0.1f); scrollRect.anchorMax = new UnityEngine.Vector2(0.85f, 0.9f);
scrollRect.offsetMin = UnityEngine.Vector2.zero; scrollRect.offsetMax = UnityEngine.Vector2.zero;

var viewport = new UnityEngine.GameObject("Viewport", typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.RectMask2D));
viewport.transform.SetParent(scroll.transform, false);
var viewportRect = viewport.GetComponent<UnityEngine.RectTransform>();
viewportRect.anchorMin = UnityEngine.Vector2.zero; viewportRect.anchorMax = UnityEngine.Vector2.one;
viewportRect.offsetMin = UnityEngine.Vector2.zero; viewportRect.offsetMax = UnityEngine.Vector2.zero;
viewport.GetComponent<UnityEngine.UI.Image>().color = new UnityEngine.Color(1f, 1f, 1f, 0.02f);

var content = new UnityEngine.GameObject("Content", typeof(UnityEngine.RectTransform), typeof(UnityEngine.UI.GridLayoutGroup), typeof(UnityEngine.UI.ContentSizeFitter));
content.transform.SetParent(viewport.transform, false);
var contentRect = content.GetComponent<UnityEngine.RectTransform>();
contentRect.anchorMin = new UnityEngine.Vector2(0f, 1f); contentRect.anchorMax = new UnityEngine.Vector2(1f, 1f);
contentRect.pivot = new UnityEngine.Vector2(0.5f, 1f);
contentRect.offsetMin = UnityEngine.Vector2.zero; contentRect.offsetMax = UnityEngine.Vector2.zero;
var grid = content.GetComponent<UnityEngine.UI.GridLayoutGroup>();
grid.cellSize = new UnityEngine.Vector2(80f, 80f); grid.spacing = new UnityEngine.Vector2(8f, 8f);
grid.padding = new UnityEngine.RectOffset(16, 16, 16, 16);
content.GetComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;

var sr = scroll.GetComponent<UnityEngine.UI.ScrollRect>();
sr.viewport = viewportRect; sr.content = contentRect; sr.horizontal = false;

// BuildMenuViewコンポーネントを付与しSerializeFieldを配線
// Attach BuildMenuView and wire its serialized field
var view = panel.AddComponent<Client.Game.InGame.UI.BuildMenu.BuildMenuView>();
var viewSo = new UnityEditor.SerializedObject(view);
viewSo.FindProperty("blockListContainer").objectReferenceValue = contentRect;
viewSo.ApplyModifiedProperties();

// MainGameStarterへ配線し、パネルは初期非アクティブ
// Wire into MainGameStarter and deactivate the panel initially
starterSo.FindProperty("buildMenuView").objectReferenceValue = view;
starterSo.ApplyModifiedProperties();
panel.SetActive(false);

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes();
```

注意: `MainGameStarter`の名前空間・`challengeListView`フィールド名は現物を確認して合わせる。パネルの兄弟順（描画順）は既存フルスクリーンパネル（ChallengeListView等）と同等の位置にする。

- [ ] **Step 2: シーン保存の確認とコミット**

Run: `git status --short moorestech_client/Assets/Scenes/`
Expected: `MainGame.unity`に変更あり

```bash
git add moorestech_client/Assets/Scenes/
git commit -m "feat: MainGameシーンにビルドメニューパネルを配線"
```

---

### Task 8: 本番マスタ暫定解放（moorestech_master）

**Files:**
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json`（別リポジトリ）

**Interfaces:**
- Produces: v8 modの全ブロックが`initialUnlocked: true`（requiredItemsは投入しない＝無償設置。プラン2で正式データに置換する暫定措置、ユーザー承認済み）

- [ ] **Step 1: mooreseditorが起動していないことを確認**

Run: `pgrep -fl mooreseditor || echo "not running"`
Expected: `not running`（起動中ならユーザーに終了を依頼して待つ）

- [ ] **Step 2: 暫定解放スクリプトを実行**

```bash
python3 - <<'EOF'
import json
path = '/Users/katsumi/moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/blocks.json'
data = json.load(open(path))
count = 0
for block in data['data']:
    if not block.get('initialUnlocked'):
        block['initialUnlocked'] = True
        count += 1
# 元のインデントを保って書き戻す（indent=2想定。実ファイルの形式を先に確認すること）
# Preserve original formatting (verify actual indent before writing)
json.dump(data, open(path, 'w'), ensure_ascii=False, indent=2)
print(f'unlocked {count} blocks')
EOF
```

実行前に`head -5`で実ファイルのインデントを確認し、`indent=`を合わせること。書き戻し後に`git -C /Users/katsumi/moorestech_master diff --stat`で差分が「initialUnlocked追加のみ」であることを確認する。

- [ ] **Step 3: moorestech_masterにコミット**

```bash
git -C /Users/katsumi/moorestech_master add server_v8/mods/moorestechAlphaMod_8/master/blocks.json
git -C /Users/katsumi/moorestech_master commit -m "feat: プラン3暫定措置として全ブロックを初期解放（プラン2で正式データに置換）"
```

コミット後、moorestechリポジトリ側の`.moorestech-external-revisions.json`のピン更新が必要か確認する（RepositorySyncがピンで巻き戻すため。ピンファイルが新コミットハッシュを指すよう更新し、moorestech側の変更として報告に含める）。

- [ ] **Step 4: 報告**

このタスクはテスト無し（データ投入）。ブロック数・コミットハッシュを報告する。

---

### Task 9: 全体回帰

**Files:** なし（検証のみ）

- [ ] **Step 1: クライアント・サーバー全回帰**

Run: `uloop compile --project-path ./moorestech_client`（エラー0件）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests"`（Client.Tests全体。180秒超ならディレクトリ単位に分割）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests.CombinedTest.Server"`
Expected: 全件PASS

- [ ] **Step 2: 未コミット差分の確認とコミット**

```bash
git status --short
git add moorestech_client/ moorestech_server/ .moorestech-external-revisions.json
git commit -m "chore: プラン3の残作業をコミット"
```

（差分がなければコミット不要。環境ドリフトファイルは含めない）

---

### Task 10: PlayMode実機検証（録画付き）

**Files:** なし（検証のみ）

- [ ] **Step 1: unity-playmode-recorded-playtestスキルで実機検証**

`unity-playmode-recorded-playtest`スキルを起動し、以下のシナリオを録画付きで検証する:

1. **起動**: 本番マスタ（v8 mod）でゲームが正常起動する（暫定解放によりビルドメニューに全ブロックが並ぶ）
2. **ビルドメニュー**: Bキーでビルドメニューが開き、ブロックグリッドが表示される。ブロックをクリックすると設置モード（PlaceBlockState）に遷移する
3. **設置**: 選択したブロックが設置できる（新プロトコル経由。requiredItems未定義のため無償）。ドラッグ複数設置も動作する
4. **破壊と再設置**: 設置したブロックを破壊するとアイテムが返却され（フォールバック経路）、Tabでメニューを開き直して別ブロックを選択・設置できる

注意（スキルの制約）: UIクリックはInputSystemのQueueStateEventで注入する。UIStateのBキーは`UnityEngine.Input`（legacy）直読みのため注入で駆動できない可能性が高い。その場合はUIStateControl経由の直接遷移（リフレクションまたはデバッグAPI）で代替し、その旨を報告する。

- [ ] **Step 2: 録画をユーザーに送付し、結果を報告**

検証4シナリオの成否と、legacy Input制約で自動化できなかった操作（手動確認が必要なもの）を明記する。
