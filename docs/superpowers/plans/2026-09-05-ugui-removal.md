# uGUI残骸の全削除（論理モデル抽出PR + 削除PR） Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** Web UIへ移行済みで描画停止中の画面uGUI（Client.Game/InGame/UI 配下の残骸・prefab・MainGameStarter配線・未参照アセット）を全削除し、Webブリッジと UI 状態機械が uGUI ビューを論理状態の置き場として使っている依存を、Unity側の純ロジッククラスへ移す。

**Architecture:** 2本のPRに分ける。PR1（抽出）は挙動不変で、uGUIビューが抱えていた論理状態（サブインベントリ・ビルドメニュー選択・進捗バー・カーソルツールチップ・クロスヘア・BP名入力・切断状態・セーブ要求）を純C#モデルへ抽出し、UIState の各 State・Client.WebUiHost の Topic/Action・テストをそのモデル型へ差し替える。uGUI MonoBehaviour は参照されない孤児として残す。PR2（削除）は孤児になった .cs・prefab・シーン内オブジェクト・Addressable登録・未参照アセットを一括削除し、監査テストとdocsを縮小する。UI状態主権は Unity の `UIStateControl` のまま、Web側の topic/action 契約は不変。

**Tech Stack:** Unity 6000.3 / C# / VContainer / UniRx / UniTask / uloop（コンパイル・テスト・prefab編集） / moores-wt（worktree）

## Requirements

設計裁定の正本: `docs/adr/0052-ugui-removal-scope-and-exceptions.md`（以下ADR）と `.decisions/2026-09-05-*.md`。

- R1. 移行済み画面uGUIの残骸（Phase1マーク付き86ファイルのうち後述の残置対象を除く全て、`Asset/UI/Prefab` 配下、`AddressableResources/UI` 配下、MainGame.unity の uGUI オブジェクト）を削除する。受け入れ: PR2完了後、`grep -rl "uGUI廃止Phase1" moorestech_client/Assets/Scripts` が0件。
- R2. 例外4種はuGUIのまま残す（ADR）: ①CEF描画面（`MainGameUI.prefab` 直下の Canvas/CanvasScaler/GraphicRaycaster/CefUnity(RawImage)）②MainMenuシーン・GameInitialaizerのローディング表示・`TextMeshProLocalize` ③`MapObjectHpBar.prefab`/`MapObjectHpBarView` ④デバッグUI（`DebugObjects.prefab`/`ItemSelectModal`/`TrainUnitDebugOverlayPresenter`）。受け入れ: これらのファイル・prefabがPR2後も存在し、コンパイルが通る。
- R3. CutScene は `CutSceneManager.prefab` の `CutSceneCanvas` だけ除去し、`TimelinePlayer`/PlayableDirector/CutSceneCamera/playable/`GameStateController` の購読を残す。Skit の `SelectionButton.prefab` と `BackgroundSkitUI` の TMP 文字表示は削除（音声再生は残す）。
- R4. UI状態主権は `UIStateControl` のまま。uGUIビューが抱える論理状態は Client.Game 内の uGUI 非依存クラスへ抽出し、State と Web ブリッジの両方がそれを読み書きする。恒久非表示ビューへの `SetActive` 呼び出しは削除。`ProgressBarView.Instance` 等の静的所有は DI 登録へ置換。受け入れ: PR1後、Client.WebUiHost と UIState/State が `UnityEngine.UI`/`TMPro` 依存クラスを一切参照しない。
- R5. Web側の topic/action 契約（ui.progress / block_inventory / build_menu.select / pause_menu / crosshair / ui.visibility / tooltip / modal）は不変。受け入れ: `moorestech_web` に変更が無く、`Client.Tests/WebUi/*` の既存契約テストが通る。
- R6. テストは破棄でなく移植する。uGUI型を組み立てていたテストは新モデル型へ書き換える。uGUI prefab そのものの描画を検証していたテスト（EditModeInPlayingTest の6件・ItemSlotDefaultTooltipTest）は、サーバー往復の検証部分を action handler 経由へ移植したうえで削除する。
- R7. PR1は挙動不変（抽出と差し替えのみ）。PR2は削除のみ。順序は PR1 → PR2。
- R8. prefab・シーン・Addressable設定の変更は `uloop execute-dynamic-code` 経由でのみ行う（YAML直接編集禁止）。
- R9. `WebUiGateClassification`/`WebUiGateAuditTest` は削除せず、残置対象に合わせて Rules/ScanRoots を縮小して維持する。
- R10. `docs/webui/ugui-retirement-plan.md` のスコープ外リストをADRの例外で上書きし、Phase 2〜4 の完了を記録する。
- やらないこと: MainMenu/ローディングのUI Toolkit化、`com.unity.ugui` パッケージ削除、cef-unity の改変、HPバー・デバッグUIの置換、`blockUIAddressablesPath` マスタスキーマの改名（後続 bd タスク）、`WebUiScreenGate.IsWebUiMode` を参照する残置ファイル（世界空間ピン・ChainPlacementPreviewPart・SkitManager 等）の分岐簡約（後続 bd タスク）。

## Global Constraints

- 作業は `moores-wt new` で切った使い捨て worktree で行う（CLAUDE.local.md）。ベースは `origin/master`（`fix-compile-error` の修正は master に取り込み済み。`git cherry origin/master fix-compile-error` が空で確認済み）。
- .cs 変更後は必ず `uloop compile --project-path ./moorestech_client`。テストは `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<正規表現>"`。
- AGENTS.md 規約: partial禁止、`Func<>`禁止、try-catch原則禁止、1ファイル200行以下、1ディレクトリ10ファイル以下、`[SerializeField]` は小文字キャメル、単純 getter/setter 禁止（`{ get; private set; }` は可、値の設定は `SetHoge`）、初期化メソッド名は `Initialize`、イベントは UniRx、コメントは日本語→英語の2行セット、`#region Internal` はローカル関数用途のみ。
- 命名: 新モデルは役割で命名する（View を含めない）。`SubInventoryModel` / `BuildMenuSelection` / `ProgressBarState` / `MouseCursorTooltipState` / `CrosshairVisibility` / `BlueprintNameInputState` / `NetworkDisconnectState` / `GameSaveRequester` / `BackgroundSkitVoicePlayer`。
- .meta は手で作らない（Unity生成をコミット）。ファイル移動・改名は `.cs` と `.cs.meta` を一緒に `git mv` し GUID を保つ。
- `docs/webui/ugui-retirement-plan.md` の「Phase 1」で付与したヘッダコメント `// [uGUI廃止Phase1] ...` は、PR1で残す判断をしたファイル（純ロジック化したもの）からは削除する。
- bd: epic `moorestech-lnsf`。PR1 = `moorestech-lnsf.1`、PR2 = `moorestech-lnsf.2`。着手時 `bd update <id> --claim`、完了時 `bd close`。

---

## 事前調査で確定した事実（実装者向け）

| 事実 | 根拠 |
|---|---|
| CEF描画面は外部パッケージ `jp.juha.cefunity` の `CefUnityBrowserSample`（RawImage依存・asmdefが `UnityEngine.UI` 参照） | `Library/PackageCache/jp.juha.cefunity@*/Runtime/CefUnityBrowserSample.cs` |
| `com.unity.ugui` 2.0.0 は TMP 同梱。パッケージは残す | `moorestech_client/Packages/manifest.json` |
| サブインベントリの「モデル」は現在 uGUI prefab 実体そのもの（`CommonBlockInventoryViewBase`/`TrainInventoryView` が `ISubInventory` を実装し、`SubInventoryState` が Addressable prefab を生成して `LocalPlayerInventoryController.SetSubInventory` に渡す） | `SubInventoryState.cs:141-148`, `PlayerInventoryViewController.cs:61-72` |
| サーバーの `InventoryRequestProtocol` は全スロット（空含む）の `Items` を返す。よってスロット数は `response.Items.Count` で決まる | `moorestech_server/.../InventoryRequestProtocol.cs:63,78` |
| Web ブリッジの Tier B 依存: `ProgressBarView.Instance`(ProgressTopic)・`BuildMenuView`(BuildMenuSelectActionHandler)・`BlueprintNameInputView`(BlueprintNameInputWebBridge)・`CrosshairView.Instance`/`UIRoot.Instance`(CommonHudTopics)・`MouseCursorTooltip.Instance`(TooltipTopic)・`NetworkDisconnectPresenter`(PauseMenuTopic)・`SaveButton`/`SaveAndQuitPresenter`(PauseMenuActions)・`SubInventoryState.CurrentSubInventory`/`ITrainInventoryView`(BlockInventoryTopic/TrainInventoryDtoFactory/BlockInventoryActions) | `Client.WebUiHost/Game/WebUiGameBinder.cs` |
| `MouseCursorTooltip.Instance` の生きた呼び出し元: `MiningIdleState`/`MiningFocusState`/`MiningProgressState`（状態は `new` で遷移生成、DI外）・`TapInteractionDriver`（`InteractController` のフィールド初期化）・`DeleteObjectService`（`DeleteObjectState` が `new`）・`PlacementFeedbackTooltipPresenter`（DI）・`GameObjectTooltipTarget`（PineTree01/Stone prefab上、`GameObjectToolTipTargetController` が GameSystem.prefab 上で駆動） | grep 結果 |
| `ProgressBarView.Instance` の生きた呼び出し元: `MiningProgressState` のみ | grep 結果 |
| `CrosshairView.Instance.SetVisible` の呼び出し元: `PlayerViewApplier` のみ | grep 結果 |
| `BlueprintNameInputView` の呼び出し元: `BlueprintCopySystem`（`Open/Close/OnConfirm/OnCancel`、`AddTo(_nameInputView)` でMonoBehaviour寿命に束縛） | `BlueprintCopySystem.cs:23-149` |
| `ClientContext.ModalManager` の利用箇所は無い（生成と保持のみ） | grep 結果 |
| `ItemSelectModal`（デバッグ・Assembly-CSharp）は `ItemSlotView.Prefab`/`ItemViewData`/`OnRightClickUp` を使う。`ItemSlotView.prefab` は `CommonSlotView`（EventSystems/TMP/Image）と `UGuiTooltipTarget` を持つ | `Client.DebugSystem/ItemSelectModal.cs` |
| `MainGameUI.prefab` 直下: `CefUnity`(RawImage+CefUnityBrowserSample+WebUiCefNavigator)・`PauseMenu`・`Disconnected`・`ProgressBar`・`DeleteBar`・`ChallengeHudView`・`BacgkroundSkitUI`(BackgroundSkitUI+AudioSource)・`Loading`・`UICursorFollowControlRootCanvasRect`・`TutorialUI`(KeyControl/UIHighlight/ItemViewHighLight の3マネージャ、uGUI非依存)・ネスト prefab `SkitUI`(UI Toolkit)・`MouseCursorTooltip`・`ChallengeListUI`・`InventoryItems`・`ResearchTreeUI`。ルートに `CanvasScaler, GraphicRaycaster, UIStateControl, UIRoot, WebUiCefToggle`。`SaveAndQuitPresenter` は `PauseMenu/Buttons/'Back to MainMenu '` 上 | prefab解析 |
| `MainGame.unity` 直下の uGUI: `BuildMenuView`・`BlueprintNameInput`（TMP_InputField+TextMeshProLocalize）・`CrosshairView`・`EventSystem`（InputSystemUIInputModule。デバッグUI用に残す） | scene解析 |
| Addressable `Vanilla Asset Group` に `Vanilla/UI/*` が20件（Block 15・ItemSlotView・FluidSlotView・Modal 2・Train 1） | `AddressableAssetsData/AssetGroups/Vanilla Asset Group.asset` |
| 参照ゼロ prefab: `MissionBar`/`StoryUI`/`ChatlogEntry`/`Inventory/HotBarItem`/`Skit/SelectionButton` | guid grep |
| `Client.DebugSystem` に asmdef は無い（Assembly-CSharp） | find 結果 |
| `Client.Skit.asmdef` の TMP 参照は `BackgroundSkitUI` だけが使う。`Client.Game.asmdef` の TMP 参照は PR2後も `MapObjectHpBarView`/`TrainUnitDebugOverlayPresenter` が使うので残す | grep 結果 |

## File Structure（PR1で新規作成・変更）

新規（純ロジック。すべて MonoBehaviour 非依存、`UnityEngine.UI`/`TMPro` 非依存）:
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/SubInventoryModel.cs` — 開いているサブインベントリの真データ（スロット列・識別子・列車エラー種別）
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/TrainInventoryMessageType.cs` — `ITrainInventoryView.cs` から enum を分離
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuSelection.cs` — ビルドメニューで選ばれた設置ターゲットの1回消費キュー
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/ProgressBar/ProgressBarState.cs` — 画面進捗バーの表示状態と進捗値
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/IMouseCursorTooltip.cs` / `TooltipPresentation.cs` — `MouseCursorTooltip.cs` から分離（内容は移動のみ）
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/MouseCursorTooltipState.cs` — カーソルツールチップの所有者付き表示状態
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Crosshair/CrosshairVisibility.cs` — クロスヘア表示フラグ
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Blueprint/BlueprintNameInputState.cs` — BP名入力の開閉と確定/キャンセル通知
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/NetworkDisconnectState.cs` — 切断状態（`IInitializable`）
- `moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/GameSaveRequester.cs` — セーブ要求送信
- `moorestech_client/Assets/Scripts/Client.Skit/UI/BackgroundSkitVoicePlayer.cs` — `BackgroundSkitUI.cs` の改名（TMP文字表示を除去し音声再生だけ残す）

変更（State / ブリッジ / DI / テスト）: 各タスクの Files 節に列挙。

削除（PR2）: Task B1 の一覧を参照。

---

# Part A: PR1 — 論理状態の抽出と差し替え（挙動不変） `moorestech-lnsf.1`

### Task A0: worktree とブランチの準備

**Files:** なし（環境準備）

- [ ] **Step 1: bd を claim し worktree を切る**

```bash
cd <moorestech リポジトリのメインワークツリー>
bd update moorestech-lnsf.1 --claim
moores-wt new feature/ugui-removal-extract-models --from origin/master --fetch
cd <moores-wt が出力した worktree パス>
pwd
```
Expected: `git status` がクリーン、`uloop launch` 済み Editor が worktree を開いている。

- [ ] **Step 2: ベースが最新でコンパイルできることを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `errors: 0`

---

### Task A1: SubInventoryModel の新設と ISubInventory から uGUI 型を外す

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/SubInventoryModel.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/TrainInventoryMessageType.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/ISubInventory.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Train/ITrainInventoryView.cs`（enum を削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Main/PlayerInventoryViewController.cs:61-72`（`SubInventorySlotObjects` 参照を削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Main/Interaction/PlayerInventorySlotInteraction.cs:24-50`（`SubInventorySlotObjects` 参照を削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block/CommonBlockInventoryViewBase.cs:20` / `Train/TrainInventoryView.cs:20` / `Block/GearEnergyTransformerUIView.cs:96` / `Block/ElectricPoleNetworkInfoUIView.cs:47` / `Block/ElectricToGearGeneratorBlockInventoryView.cs:36` / `Block/FilterSplitterBlockInventoryView.cs:34`（`SubInventorySlotObjects` はインターフェース実装でなく各クラス固有メンバとして残す。コード変更なしで良い — インターフェースから消えるだけ）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Inventory/SubInventoryModelTest.cs`（新規）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Inventory/LocalPlayerInventoryControllerSwapMoveTest.cs:138-158`（`FakeSubInventory` を `SubInventoryModel` に置換）

**Interfaces:**
- Produces: `SubInventoryModel : ISubInventory` — `SubInventoryModel(ISubInventoryIdentifier identifier)`, `void SetItems(IReadOnlyList<IItemStack> items)`, `void SetItem(int slot, IItemStack item)`, `void SetTrainMessage(TrainInventoryMessageType messageType)`, `TrainInventoryMessageType? TrainMessage { get; }`
- Produces: `ISubInventory` = `{ List<IItemStack> SubInventory; int Count; ISubInventoryIdentifier ISubInventoryIdentifier; }`（`SubInventorySlotObjects` 削除）
- Produces: `enum TrainInventoryMessageType { ContainerMissing, TrainCarMissing, OpenFailed }`（namespace `Client.Game.InGame.UI.Inventory`）

- [ ] **Step 1: 失敗するテストを書く**

`Client.Tests/Inventory/SubInventoryModelTest.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory;
using Core.Item.Interface;
using Core.Master;
using Game.Context;
using Game.PlayerInventory.Interface.Subscription;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.Inventory
{
    public class SubInventoryModelTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void SetItemsでスロット数が応答のアイテム数になる()
        {
            var model = new SubInventoryModel(new BlockInventorySubInventoryIdentifier(Vector3Int.zero));
            var items = new List<IItemStack> { ServerContext.ItemStackFactory.CreatEmpty(), ServerContext.ItemStackFactory.CreatEmpty(), ServerContext.ItemStackFactory.CreatEmpty() };

            model.SetItems(items);

            Assert.AreEqual(3, model.Count);
            Assert.IsNull(model.TrainMessage);
        }

        [Test]
        public void SetItemは範囲内スロットだけを書き換える()
        {
            var model = new SubInventoryModel(new BlockInventorySubInventoryIdentifier(Vector3Int.zero));
            model.SetItems(new List<IItemStack> { ServerContext.ItemStackFactory.CreatEmpty(), ServerContext.ItemStackFactory.CreatEmpty() });
            var itemId = MasterHolder.ItemMaster.GetItemAllIds()[0];

            model.SetItem(1, ServerContext.ItemStackFactory.Create(itemId, 5));
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("インベントリのサイズを超えています"));
            model.SetItem(2, ServerContext.ItemStackFactory.Create(itemId, 1));

            Assert.AreEqual(5, model.SubInventory[1].Count);
            Assert.AreEqual(2, model.Count);
        }

        [Test]
        public void SetTrainMessageでスロットが空になりエラー種別が残る()
        {
            var model = new SubInventoryModel(new TrainInventorySubInventoryIdentifier(1));
            model.SetItems(new List<IItemStack> { ServerContext.ItemStackFactory.CreatEmpty() });

            model.SetTrainMessage(TrainInventoryMessageType.ContainerMissing);

            Assert.AreEqual(0, model.Count);
            Assert.AreEqual(TrainInventoryMessageType.ContainerMissing, model.TrainMessage);
        }
    }
}
```
（`LogAssert` は `UnityEngine.TestTools` の using が必要）

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `SubInventoryModel` 未定義のコンパイルエラー

- [ ] **Step 3: モデルと enum を実装し、ISubInventory から uGUI 型を外す**

`Client.Game/InGame/UI/Inventory/TrainInventoryMessageType.cs`:
```csharp
namespace Client.Game.InGame.UI.Inventory
{
    // 列車インベントリを開けなかった理由。Web側のエラー文言キーへ写す
    // Why a train inventory could not be opened; mapped onto the web-side error key
    public enum TrainInventoryMessageType
    {
        ContainerMissing,
        TrainCarMissing,
        OpenFailed,
    }
}
```

`Client.Game/InGame/UI/Inventory/SubInventoryModel.cs`:
```csharp
using System.Collections.Generic;
using Core.Item.Interface;
using Game.PlayerInventory.Interface.Subscription;
using UnityEngine;

namespace Client.Game.InGame.UI.Inventory
{
    /// <summary>
    ///     開いているブロック/列車インベントリの真データ。スロット数はサーバー応答のアイテム数で決まる
    ///     Authoritative data of the open block/train inventory; the slot count comes from the server response
    /// </summary>
    public class SubInventoryModel : ISubInventory
    {
        public List<IItemStack> SubInventory { get; } = new();
        public int Count => SubInventory.Count;
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; }

        // 列車のみ。null なら正常に開けている
        // Train only; null means the inventory opened normally
        public TrainInventoryMessageType? TrainMessage { get; private set; }

        public SubInventoryModel(ISubInventoryIdentifier identifier)
        {
            ISubInventoryIdentifier = identifier;
        }

        public void SetItems(IReadOnlyList<IItemStack> items)
        {
            SubInventory.Clear();
            SubInventory.AddRange(items);
        }

        public void SetItem(int slot, IItemStack item)
        {
            if (SubInventory.Count <= slot)
            {
                Debug.LogError($"インベントリのサイズを超えています。item:{item} slot:{slot}");
                return;
            }

            SubInventory[slot] = item;
        }

        // 開けなかった列車はスロットを持たない
        // A train that failed to open exposes no slots
        public void SetTrainMessage(TrainInventoryMessageType messageType)
        {
            SubInventory.Clear();
            TrainMessage = messageType;
        }
    }
}
```

`ISubInventory.cs` を次に置換（Phase1ヘッダ削除・`SubInventorySlotObjects` 削除・`using Client.Game.InGame.UI.Inventory.Common` 削除）:
```csharp
using System.Collections.Generic;
using Core.Item.Interface;
using Game.PlayerInventory.Interface.Subscription;

namespace Client.Game.InGame.UI.Inventory
{
    /// <summary>
    /// プレイヤーのインベントリとは別に、ブロックや列車など「他のインベントリ」を表すインターフェース
    /// Represents an inventory other than the player's own, such as a block or train inventory
    /// </summary>
    public interface ISubInventory
    {
        public List<IItemStack> SubInventory { get; }
        public int Count { get; }
        public ISubInventoryIdentifier ISubInventoryIdentifier { get; }
    }

    public static class ISubInventoryExtension
    {
        public static bool IsEnableSubInventory(this ISubInventory subInventory) => subInventory.Count > 0;
    }

    public class EmptySubInventory : ISubInventory
    {
        public List<IItemStack> SubInventory { get; } = new();
        public int Count => 0;
        public ISubInventoryIdentifier ISubInventoryIdentifier => null;
    }
}
```

`ITrainInventoryView.cs` から `enum TrainInventoryMessageType {...}` ブロックを削除（インターフェース本体は残す）。

`PlayerInventoryViewController.SetSubInventory` を次に置換（サブスロットのポインタ購読は uGUI 専用で恒久非表示のため削除）:
```csharp
        public void SetSubInventory(ISubInventory subInventory)
        {
            foreach (var disposable in _subInventorySlotUIEventUnsubscriber) disposable.Dispose();
            _subInventorySlotUIEventUnsubscriber.Clear();
            _subInventory = subInventory;
            _interaction.SetSubInventory(subInventory);
            _playerInventory.SetSubInventory(subInventory);
        }
```
`InventoryViewUpdate` 内の `else _subInventory.SubInventorySlotObjects[...]` 分岐は `else break;` に置換。

`PlayerInventorySlotInteraction.cs` の `index = _mainInventorySlotObjects.Count + _subInventory.SubInventorySlotObjects.IndexOf(slotObject);` 行を含む分岐は、メインスロットに無ければ `return;` に置換（サブ側uGUIスロットは生成されなくなるため）。

`LocalPlayerInventoryControllerSwapMoveTest.cs` の `FakeSubInventory` クラスと `using Client.Game.InGame.UI.Inventory.Common;` を削除し、`new FakeSubInventory(1)` を次に置換:
```csharp
            var subInventory = new SubInventoryModel(null);
            subInventory.SetItems(new List<IItemStack> { ServerContext.ItemStackFactory.CreatEmpty() });
```

- [ ] **Step 4: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `errors: 0`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "SubInventoryModelTest|LocalPlayerInventoryControllerSwapMoveTest"`
Expected: 全件 PASS

- [ ] **Step 5: コミット**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory moorestech_client/Assets/Scripts/Client.Tests/Inventory
git commit -m "refactor: サブインベントリの真データを SubInventoryModel へ抽出し ISubInventory から uGUI 型を外す"
```

---

### Task A2: SubInventoryState を prefab 非依存にする（ソースがモデルを生成）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventory/ISubInventorySource.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventory/BlockSubInventorySource.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventory/TrainSubInventorySource.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventoryState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/TrainInventoryDtoFactory.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/SubInventorySourceModelTest.cs`（新規）

**Interfaces:**
- Consumes: `SubInventoryModel`, `TrainInventoryMessageType`（Task A1）
- Produces: `ISubInventorySource { InventoryIdentifierMessagePack InventoryIdentifier { get; } SubInventoryModel CreateModel(InventoryResponse inventoryResponse); }`
- Produces: `SubInventoryState.CurrentSubInventory` の型は `SubInventoryModel`（`ISubInventory` として読む既存呼び出しはそのまま動く）。`SubInventoryState(LocalPlayerInventoryController localPlayerInventoryController, RightShortPressInputService rightShortPressInputService)`

- [ ] **Step 1: 失敗するテストを書く**

`Client.Tests/UIState/SubInventorySourceModelTest.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.Inventory;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Network.API;
using Core.Item.Interface;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.Module.TestMod;

namespace Client.Tests.UIState
{
    public class SubInventorySourceModelTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 列車ソースはコンテナ無し応答をエラー種別へ写す()
        {
            var identifier = InventoryIdentifierMessagePack.CreateTrainMessage(7);
            var response = new InventoryResponse(identifier, new List<IItemStack>(), InventoryRequestResult.ContainerNotFound);
            var source = new TrainSubInventorySourceForTest(7);

            var model = source.CreateModel(response);

            Assert.AreEqual(TrainInventoryMessageType.ContainerMissing, model.TrainMessage);
            Assert.AreEqual(0, model.Count);
        }

        [Test]
        public void 列車ソースは成功応答のアイテムをそのまま載せる()
        {
            var identifier = InventoryIdentifierMessagePack.CreateTrainMessage(7);
            var items = new List<IItemStack> { ServerContext.ItemStackFactory.CreatEmpty(), ServerContext.ItemStackFactory.CreatEmpty() };
            var source = new TrainSubInventorySourceForTest(7);

            var model = source.CreateModel(new InventoryResponse(identifier, items, InventoryRequestResult.Success));

            Assert.IsNull(model.TrainMessage);
            Assert.AreEqual(2, model.Count);
        }

        // TrainCarEntityObject は MonoBehaviour なので識別子だけを差し替える最小の派生で組む
        // TrainCarEntityObject is a MonoBehaviour, so build the source with the minimal identifier-only derivation
        private class TrainSubInventorySourceForTest : TrainSubInventorySource
        {
            public TrainSubInventorySourceForTest(long trainCarInstanceId) : base(trainCarInstanceId) { }
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `CreateModel` 未定義・`TrainSubInventorySource(long)` 未定義のコンパイルエラー

- [ ] **Step 3: 実装する**

`ISubInventorySource.cs`:
```csharp
using Client.Game.InGame.UI.Inventory;
using Client.Network.API;
using Server.Util.MessagePack;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    public interface ISubInventorySource
    {
        /// <summary>
        /// ブロックや列車を共通で扱えるインベントリ識別子
        /// Common inventory identifier that can handle blocks and trains
        /// </summary>
        InventoryIdentifierMessagePack InventoryIdentifier { get; }

        /// <summary>
        /// サーバー応答から開いているインベントリの真データを組み立てる
        /// Build the authoritative open-inventory data from the server response
        /// </summary>
        SubInventoryModel CreateModel(InventoryResponse inventoryResponse);
    }
}
```

`BlockSubInventorySource.cs`（`UIPrefabAddressablePath`・`ExecuteInitialize` を削除し、次を追加。`using Client.Game.InGame.UI.Inventory.Block;` と `using Core.Item.Interface;`・`using System.Collections.Generic;` は不要になるので削除。`using Game.PlayerInventory.Interface.Subscription;` を追加）:
```csharp
        public SubInventoryModel CreateModel(InventoryResponse inventoryResponse)
        {
            var model = new SubInventoryModel(new BlockInventorySubInventoryIdentifier(_blockGameObject.BlockPosInfo.OriginalPos));
            if (inventoryResponse.Result != InventoryRequestResult.Success)
            {
                Debug.Log($"ブロックインベントリの取得に失敗しました。結果:{inventoryResponse.Result} 位置:{InventoryIdentifier.BlockPosition.Vector3Int}");
                return model;
            }

            model.SetItems(inventoryResponse.Items);
            return model;
        }
```

`TrainSubInventorySource.cs` を次に置換:
```csharp
using Client.Game.InGame.Train.View.Object.Core;
using Client.Game.InGame.UI.Inventory;
using Client.Network.API;
using Game.PlayerInventory.Interface.Subscription;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;

namespace Client.Game.InGame.UI.UIState.State.SubInventory
{
    public class TrainSubInventorySource : ISubInventorySource
    {
        public InventoryIdentifierMessagePack InventoryIdentifier { get; }
        public long TrainCarInstanceId { get; }

        public TrainSubInventorySource(TrainCarEntityObject trainCarEntityObject) : this(trainCarEntityObject.TrainCarInstanceId.AsPrimitive())
        {
        }

        // 識別子だけで組める経路。テストと本番の両方が同じ変換を通る
        // Identifier-only construction path shared by tests and production
        protected TrainSubInventorySource(long trainCarInstanceId)
        {
            TrainCarInstanceId = trainCarInstanceId;
            InventoryIdentifier = InventoryIdentifierMessagePack.CreateTrainMessage(trainCarInstanceId);
        }

        public SubInventoryModel CreateModel(InventoryResponse inventoryResponse)
        {
            var model = new SubInventoryModel(new TrainInventorySubInventoryIdentifier(TrainCarInstanceId));
            switch (inventoryResponse.Result)
            {
                case InventoryRequestResult.Success:
                    model.SetItems(inventoryResponse.Items);
                    return model;
                case InventoryRequestResult.ContainerNotFound:
                    model.SetTrainMessage(TrainInventoryMessageType.ContainerMissing);
                    return model;
                case InventoryRequestResult.TrainCarNotFound:
                    model.SetTrainMessage(TrainInventoryMessageType.TrainCarMissing);
                    return model;
                default:
                    model.SetTrainMessage(TrainInventoryMessageType.OpenFailed);
                    return model;
            }
        }
    }
}
```

`SubInventoryState.cs` の変更点:
- フィールド `PlayerInventoryViewController _playerInventoryViewController` → `LocalPlayerInventoryController _localPlayerInventoryController`、ctor 引数も同様。`ISubInventoryView _currentView` → `SubInventoryModel _currentModel`。`CurrentSubInventory` の型を `SubInventoryModel` に。
- `OnUnifiedInventoryEvent`: `_currentView.UpdateInventorySlot(packet.Slot, item)` → `_currentModel.SetItem(packet.Slot, item)`。
- `LoadInventory()` を次に置換:
```csharp
            async UniTask LoadInventory()
            {
                _loadInventoryCts = new CancellationTokenSource();
                var ct = _loadInventoryCts.Token;

                // カーソルを表示
                // Show cursor
                InputManager.MouseCursorVisible(true);

                // インベントリデータを取得し真データを組み立てる
                // Fetch inventory data and build the authoritative model
                var inventoryResponse = await ClientContext.VanillaApi.Response.GetInventory(_subInventorySource.InventoryIdentifier, ct);
                _currentModel = _subInventorySource.CreateModel(inventoryResponse);
                _localPlayerInventoryController.SetSubInventory(_currentModel);

                // インベントリの更新を購読
                // Subscribe to inventory updates
                ClientContext.VanillaApi.SendOnly.SubscribeInventory(_subInventorySource.InventoryIdentifier, true);

                // ロード完了を外部購読者（Web UI など）へ通知する
                // Notify external subscribers (e.g. Web UI) that loading has finished
                _onSubInventoryUpdated.OnNext(Unit.Default);
            }
```
- `OnExit()`: `_playerInventoryViewController.SetSubInventory(new EmptySubInventory()); _playerInventoryViewController.SetActive(false); _currentView?.DestroyUI(); _currentView = null;` → `_localPlayerInventoryController.SetSubInventory(new EmptySubInventory()); _currentModel = null;`
- 不要 using（`Client.Common.Asset`, `Client.Game.InGame.UI.Inventory.Main` は `LocalPlayerInventoryController` の名前空間なので残す）を整理。

`TrainInventoryDtoFactory.cs`: 引数型を `SubInventoryModel inventory` に変え、`ResolveError` を次に置換:
```csharp
        private static string ResolveError(SubInventoryModel inventory)
        {
            if (inventory.TrainMessage == null) return null;
            return inventory.TrainMessage.Value switch
            {
                TrainInventoryMessageType.ContainerMissing => "containerMissing",
                TrainInventoryMessageType.TrainCarMissing => "trainCarMissing",
                _ => "openFailed",
            };
        }
```
（`using Client.Game.InGame.UI.Inventory.Train;` を削除）

- [ ] **Step 4: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `errors: 0`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "SubInventorySourceModelTest|InventoryAreaMapperTest|CollectActionTest|WireContractC2Test"`
Expected: 全件 PASS

- [ ] **Step 5: コミット**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/TrainInventoryDtoFactory.cs moorestech_client/Assets/Scripts/Client.Tests/UIState/SubInventorySourceModelTest.cs
git commit -m "refactor: SubInventoryState が uGUI prefab を生成せず SubInventoryModel を組み立てる"
```

---

### Task A3: PlayerInventoryState / SkitState から uGUI ビュー依存を外す

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlayerInventoryState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SkitState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/ChallengeListState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/ResearchTreeState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PauseMenu/PauseMenuStateService.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Inventory/PlayerInventoryStateEquipmentApplyTest.cs:80-92`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/RightShortPressTransitionTest.cs:56-98`

**Interfaces:**
- Produces: `PlayerInventoryState(LocalPlayerInventoryController, LocalPlayerEquipment, InitialHandshakeResponse, RightShortPressInputService)`
- Produces: `SkitState(SkitManager)`（既存の他引数があればそれは維持し、`PlayerInventoryViewController` だけ外す）
- Produces: `ChallengeListState(RightShortPressInputService)`、`ResearchTreeState(RightShortPressInputService)`
- Produces: `DeleteObjectState(RailGraphClientCache, UiStateCameraPolicyService, BuildOperationHistory, BuildUndoService, PlacementTargetPickService, RightShortPressInputService)`（`DeleteBarObject` を外す）
- Produces: `PauseMenuStateService()`（引数なし）

- [ ] **Step 1: 既存テストを新シグネチャへ書き換え失敗させる**

`PlayerInventoryStateEquipmentApplyTest.cs` の `CreatePlayerInventoryState` を次に置換（RecipeViewerView/PlayerInventoryViewController の生成と `SetPrivateField` 2行を削除）:
```csharp
            void CreatePlayerInventoryState(LocalPlayerEquipment playerEquipment, InitialHandshakeResponse initialHandshake)
            {
                new PlayerInventoryState(
                    new LocalPlayerInventoryController(new LocalPlayerInventory(), playerEquipment),
                    playerEquipment, initialHandshake, new RightShortPressInputService(new RightShortPressInput()));
            }
```
`RightShortPressTransitionTest.cs`:
- `new DeleteObjectState(deleteBarObject, null, ...)` → `new DeleteObjectState(null, CreateCameraPolicy(...), new BuildOperationHistory(), ...)`（`deleteBarObject` 生成行も削除）
- `var challengeListView = CreateComponent<ChallengeListView>("ChallengeList"); ... new ChallengeListState(challengeListView, rightShortPressInputService)` → `new ChallengeListState(rightShortPressInputService)`
- `var researchTreeViewManager = ...; new ResearchTreeState(researchTreeViewManager, rightShortPressInputService)` → `new ResearchTreeState(rightShortPressInputService)`
- 不要になった `using Client.Game.InGame.UI.Challenge;` 等を削除

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 上記 ctor が無いためエラー

- [ ] **Step 3: State を書き換える**

`PlayerInventoryState.cs`: フィールド `_recipeViewerView`/`_playerInventoryViewController` と ctor 引数、`OnEnter`/`OnExit`/ctor 内の `SetActive(...)` 4行と `_playerInventoryViewController.SetSubInventory(new EmptySubInventory())` を削除し、代わりに `OnEnter` で `_localPlayerInventoryController.SetSubInventory(new EmptySubInventory());` を呼ぶ。`using Client.Game.InGame.UI.Inventory.RecipeViewer;` を削除、`using Client.Game.InGame.UI.Inventory;` は `EmptySubInventory` のため残す。

`SkitState.cs`: `_playerInventoryViewController` フィールド・ctor 引数・`OnEnter` 内の `if (context.LastStateEnum == ...) { _playerInventoryViewController.SetActive(false); }` ブロック（コメント含む）を削除。

`ChallengeListState.cs` / `ResearchTreeState.cs`: ビューのフィールド・ctor 引数・`SetActive(true/false)` 行を削除。

`DeleteObjectState.cs`: `_deleteBarObject` フィールド・ctor 引数・ctor 末尾 `deleteBarObject.gameObject.SetActive(false);`・`OnEnter` の `_deleteBarObject.gameObject.SetActive(!WebUiScreenGate.IsWebUiMode);`・`OnExit` の `_deleteBarObject.gameObject.SetActive(false);`・`using Client.Game.InGame.UI.UIState.UIObject;` を削除。

`PauseMenuStateService.cs` を次に置換:
```csharp
using Client.Input;

namespace Client.Game.InGame.UI.UIState.State.PauseMenu
{
    public class PauseMenuStateService
    {
        public bool IsClosePause()
        {
            return InputManager.UI.CloseUI.GetKeyDown;
        }

        public void OnEnter()
        {
            InputManager.MouseCursorVisible(true);
        }

        public void OnExit()
        {
        }
    }
}
```

- [ ] **Step 4: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `errors: 0`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlayerInventoryStateEquipmentApplyTest|RightShortPressTransitionTest|UIStateControlTest|UIStateKeyHintCatalogTest|UIStateFocusRestorationTest"`
Expected: 全件 PASS

- [ ] **Step 5: コミット**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState moorestech_client/Assets/Scripts/Client.Tests
git commit -m "refactor: UIState の各 State から恒久非表示 uGUI ビューへの SetActive と依存を外す"
```

---

### Task A4: BuildMenuSelection（ビルドメニュー選択の1回消費キュー）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuSelection.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/BuildMenuState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/BuildMenuActions.cs:16-50`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:153,191`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuView.cs:24`（`, IBuildMenuView` を外すだけ。孤児化）
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/IBuildMenuView.cs`（+ .meta）
- Delete: `moorestech_client/Assets/Scripts/Client.Tests/UIState/Fakes/FakeBuildMenuView.cs`（+ .meta）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs:146`（`builder.RegisterComponent(buildMenuView).AsSelf().As<IBuildMenuView>();` → `builder.RegisterComponent(buildMenuView);`）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs`（`builder.Register<BuildMenuSelection>(Lifetime.Singleton);` を `RegisterUiAndPlayer` に追加）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/BuildMenuSelectionTest.cs`（新規）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/RightShortPressTransitionTest.cs:71`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/BuildMenuEntryDtoFactoryTest.cs:159-176`

**Interfaces:**
- Produces: `BuildMenuSelection` — `void SetSelectedTarget(IPlacementTarget target)`, `bool TryConsumeSelectedTarget(out IPlacementTarget target)`, `void Clear()`
- Produces: `BuildMenuState(BuildMenuSelection, UiStateCameraPolicyService, RightShortPressInputService)`
- Produces: `BuildMenuSelectActionHandler(UIStateControl, PlacementTargetResolver, BuildMenuSelection)`

- [ ] **Step 1: 失敗するテストを書く**

`Client.Tests/UIState/BuildMenuSelectionTest.cs`:
```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Client.Game.InGame.UI.BuildMenu;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    public class BuildMenuSelectionTest
    {
        [Test]
        public void 選択は一度だけ消費される()
        {
            var selection = new BuildMenuSelection();
            var target = new BlueprintPlacementTarget(System.Guid.NewGuid());

            selection.SetSelectedTarget(target);

            Assert.IsTrue(selection.TryConsumeSelectedTarget(out var first));
            Assert.AreSame(target, first);
            Assert.IsFalse(selection.TryConsumeSelectedTarget(out _));
        }

        [Test]
        public void Clearで未消費の選択が捨てられる()
        {
            var selection = new BuildMenuSelection();
            selection.SetSelectedTarget(new BlueprintPlacementTarget(System.Guid.NewGuid()));

            selection.Clear();

            Assert.IsFalse(selection.TryConsumeSelectedTarget(out _));
        }
    }
}
```
（`BlueprintPlacementTarget` の ctor シグネチャは `Client.Game/InGame/BlockSystem/PlaceSystem/Targets/BlueprintPlacementTarget.cs` を読んで合わせる。Guid 1引数で無ければ `PlacementTargetCatalog` から取る `BuildMenuEntryDtoFactoryTest.cs:169` と同じ取り方にする）

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `BuildMenuSelection` 未定義

- [ ] **Step 3: 実装する**

`BuildMenuSelection.cs`:
```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;

namespace Client.Game.InGame.UI.BuildMenu
{
    /// <summary>
    ///     Webのビルドメニューで選ばれた設置ターゲットを、BuildMenuState が1回だけ消費するキュー
    ///     Holds the placement target chosen on the web build menu until BuildMenuState consumes it once
    /// </summary>
    public class BuildMenuSelection
    {
        private IPlacementTarget _selectedTarget;

        public void SetSelectedTarget(IPlacementTarget target)
        {
            _selectedTarget = target;
        }

        // 消費は一方通行。同じ選択が二度設置モードへ入らない
        // Consumption is one-way so the same selection never enters placement twice
        public bool TryConsumeSelectedTarget(out IPlacementTarget target)
        {
            target = _selectedTarget;
            _selectedTarget = null;
            return target != null;
        }

        // メニューを開き直したときに前回の未消費選択を捨てる
        // Discard a stale unconsumed selection when the menu is reopened
        public void Clear()
        {
            _selectedTarget = null;
        }
    }
}
```

`BuildMenuState.cs`: `IBuildMenuView _buildMenuView` → `BuildMenuSelection _buildMenuSelection`（ctor も）。`OnEnter` の `_buildMenuView.SetActive(true);` → `_buildMenuSelection.Clear();`。`GetNextUpdate` の
```csharp
            if (_buildMenuView.TryConsumeSelectedEntry(out var entry))
                return new UITransitContext(UIStateEnum.PlaceBlock, UITransitContextContainer.Create(new PlacementSelection(entry.Target, PlacementOrigin.NonHotbar)));
```
→
```csharp
            if (_buildMenuSelection.TryConsumeSelectedTarget(out var target))
                return new UITransitContext(UIStateEnum.PlaceBlock, UITransitContextContainer.Create(new PlacementSelection(target, PlacementOrigin.NonHotbar)));
```
`OnExit` の `_buildMenuView.SetActive(false);` を削除（空メソッドとして残す）。

`BuildMenuActions.cs`: `BuildMenuView _buildMenuView` → `BuildMenuSelection _buildMenuSelection`（ctor も）。`_buildMenuView.SetSelectedEntry(new BuildMenuEntry(target, null, string.Empty));` → `_buildMenuSelection.SetSelectedTarget(target);`。コメント「uGUIの消費キューへ…」は「BuildMenuState の消費キューへ渡す / Hand the target to BuildMenuState's consume queue」に書き換え。

`WebUiGameBinder.cs`: `var buildMenuView = resolver.Resolve<BuildMenuView>();` → `var buildMenuSelection = resolver.Resolve<BuildMenuSelection>();`、`new BuildMenuSelectActionHandler(uiStateControl, placementTargetResolver, buildMenuView)` → `(..., buildMenuSelection)`。

`RightShortPressTransitionTest.cs:71`: `new BuildMenuState(new FakeBuildMenuView(), ...)` → `new BuildMenuState(new BuildMenuSelection(), ...)`。`using Client.Tests.UIState.Fakes;` は `FakeDeleteTarget` が残るなら維持。

`BuildMenuEntryDtoFactoryTest.cs:159-176`: `viewObject`/`view` を `var selection = new BuildMenuSelection();` に置換し、handler へ渡す。`Assert.IsTrue(view.TryConsumeSelectedEntry(out var selected)); Assert.AreEqual(entry.Id, selected.Target.Id);` → `Assert.IsTrue(selection.TryConsumeSelectedTarget(out var selected)); Assert.AreEqual(entry.Id, selected.Id);`。`viewObject` の Destroy 行も削除。

- [ ] **Step 4: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `errors: 0`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "BuildMenuSelectionTest|BuildMenuEntryDtoFactoryTest|BuildMenuTopicRepublishTest|RightShortPressTransitionTest"`
Expected: 全件 PASS

- [ ] **Step 5: コミット**

```bash
git add -A moorestech_client/Assets/Scripts
git commit -m "refactor: ビルドメニュー選択を BuildMenuSelection へ抽出し BuildMenuView 依存を State/Action から外す"
```

---

### Task A5: ProgressBarState と採掘FSMのDI化

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/ProgressBar/ProgressBarState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningControllerContext.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningProgressState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningFocusState.cs:15`（`new MiningProgressState(currentTarget, usableMiningTool)` → `new MiningProgressState(context, currentTarget, usableMiningTool)`）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Interact/InteractController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/ProgressTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:70-73`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs`（`builder.Register<ProgressBarState>(Lifetime.Singleton);`）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Interact/InteractControllerDisableTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Mining/MiningEquipmentSwitchTest.cs`

本タスクはツールチップ（Task A6）と同じ経路（`MiningControllerContext`）を触る。A5 で `ProgressBar` を、A6 で `Tooltip` を同じコンテキストに足す。

**Interfaces:**
- Produces: `ProgressBarState` — `bool IsShown { get; }`, `float CurrentProgress { get; }`, `IObservable<Unit> OnProgressChanged`, `void Show()`, `void Hide()`, `void SetProgress(float progress)`
- Produces: `MiningControllerContext(LocalPlayerEquipment localPlayerEquipment, ProgressBarState progressBar)` と `ProgressBarState ProgressBar { get; }`（A6 で `IMouseCursorTooltip tooltip` 引数と `Tooltip` プロパティを追加）
- Produces: `MiningProgressState(MiningControllerContext context, IMiningTargetObject startedMiningTarget, MiningToolCandidate miningToolCandidate)`
- Produces: `InteractController(LocalPlayerEquipment, IInteractTargetSelector, ProgressBarState)`（A6 で `IMouseCursorTooltip` を追加）
- Produces: `ProgressTopic(WebSocketHub hub, ProgressBarState state)`

- [ ] **Step 1: 既存テストを新型へ移植して失敗させる**

`InteractControllerDisableTest.cs`:
- `CreateProgressBarView()` ローカル関数と `_progressBarObject` フィールド、`ProgressBarView.Instance = null;`、`using UnityEngine.UI;`（Scrollbar）を削除。
- フィールド `private ProgressBarState _progressBar;` を追加し SetUp で `_progressBar = new ProgressBarState();`。
- `new InteractController(equipment, selector)` 形の生成箇所を `new InteractController(equipment, selector, _progressBar)` に（A6 完了後は第4引数 `_tooltip` を追加）。
- `ProgressBarView.Instance.IsShown` → `_progressBar.IsShown`。

`MiningEquipmentSwitchTest.cs`:
- 同様に `CreateProgressBarView()`/`_progressBarObject`/`ProgressBarView.Instance = null;` を削除し `_progressBar = new ProgressBarState();`。
- `new MiningControllerContext(equipment)` → `new MiningControllerContext(equipment, _progressBar)`（A6 後は `, _tooltip`）。
- `new MiningProgressState(target, miningTool)` → `new MiningProgressState(context, target, miningTool)`。

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ProgressBarState` 未定義

- [ ] **Step 3: 実装する**

`ProgressBarState.cs`:
```csharp
using System;
using UniRx;

namespace Client.Game.InGame.UI.ProgressBar
{
    /// <summary>
    ///     画面固定の進捗バーの論理状態。表示は Web UI が ui.progress topic 経由で描く
    ///     Logical state of the screen progress bar; the Web UI renders it through the ui.progress topic
    /// </summary>
    public class ProgressBarState
    {
        public bool IsShown { get; private set; }
        public float CurrentProgress { get; private set; }

        // Show/Hide/SetProgress いずれかで状態が変化したら発火する
        // Fires whenever Show/Hide/SetProgress changes the state
        public IObservable<Unit> OnProgressChanged => _onProgressChanged;
        private readonly Subject<Unit> _onProgressChanged = new();

        public void Show()
        {
            IsShown = true;
            _onProgressChanged.OnNext(Unit.Default);
        }

        public void Hide()
        {
            IsShown = false;
            _onProgressChanged.OnNext(Unit.Default);
        }

        public void SetProgress(float progress)
        {
            CurrentProgress = progress;
            _onProgressChanged.OnNext(Unit.Default);
        }
    }
}
```

`MiningControllerContext.cs`: `using Client.Game.InGame.UI.ProgressBar;` を追加し、
```csharp
        public readonly LocalPlayerEquipment LocalPlayerEquipment;
        public ProgressBarState ProgressBar { get; }

        public MiningControllerContext(LocalPlayerEquipment localPlayerEquipment, ProgressBarState progressBar)
        {
            LocalPlayerEquipment = localPlayerEquipment;
            ProgressBar = progressBar;
            ...
```

`MiningProgressState.cs`: フィールド `private readonly ProgressBarState _progressBar;` を追加。ctor を `MiningProgressState(MiningControllerContext context, IMiningTargetObject startedMiningTarget, MiningToolCandidate miningToolCandidate)` にし `_progressBar = context.ProgressBar;`。`ProgressBarView.Instance.Show()/Hide()/SetProgress(...)` → `_progressBar.Show()/Hide()/SetProgress(...)`。`using Client.Game.InGame.UI.ProgressBar;` は残す（型が同名前空間）。

`InteractController.cs`: ctor を `InteractController(LocalPlayerEquipment localPlayerEquipment, IInteractTargetSelector selector, ProgressBarState progressBar)` にし `_miningContext = new MiningControllerContext(localPlayerEquipment, progressBar);`。

`ProgressTopic.cs`: `ProgressBarView _view` → `ProgressBarState _state`（ctor・`BuildJson` の `_view.IsShown/CurrentProgress` も）。コメント「uGUI バーに label 源が無いため null」→「進捗バーに label 源が無いため null / The progress bar has no label source, so emit null」。

`WebUiGameBinder.cs:70-73`: 
```csharp
            // 進捗バートピックを登録
            // Register the progress-bar topic
            var progressTopic = new ProgressTopic(hub, resolver.Resolve<ProgressBarState>());
```
`using Client.Game.InGame.UI.ProgressBar;` は維持。

`MainGameInteractionRegistration.RegisterUiAndPlayer`: `builder.Register<InteractController>(Lifetime.Singleton);` の直前に `builder.Register<ProgressBarState>(Lifetime.Singleton);`。

- [ ] **Step 4: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `errors: 0`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "InteractControllerDisableTest|MiningEquipmentSwitchTest|MiningFocusStateTest"`
Expected: 全件 PASS（MiningFocusStateTest は A6 で再度触る）

- [ ] **Step 5: コミット**

```bash
git add -A moorestech_client/Assets/Scripts
git commit -m "refactor: 進捗バーの論理状態を ProgressBarState へ抽出し採掘FSMとProgressTopicをDI経由にする"
```

---

### Task A6: MouseCursorTooltipState（カーソルツールチップの純ロジック化）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/IMouseCursorTooltip.cs`（`MouseCursorTooltip.cs` からインターフェースを移動）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/TooltipPresentation.cs`（同 struct を移動）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/MouseCursorTooltipState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/MouseCursorTooltip.cs`（移動した2型を削除。MonoBehaviour本体は孤児として残す）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/GameObjectTooltipTarget.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Tooltip/GameObjectToolTipTargetController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningControllerContext.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningIdleState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningFocusState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningCompleteState.cs:22-24`（`new MiningIdleState()` → `new MiningIdleState(context)`）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningProgressState.cs`（`new MiningFocusState()` はそのまま。`MiningIdleState` 生成箇所があれば `context` を渡す）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Interact/InteractController.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Interact/TapInteractionDriver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Feedback/PlacementFeedbackTooltipPresenter.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/TooltipTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:114`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（`GameObjectToolTipTargetController` の `[SerializeField]` と `RegisterComponent` 追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs`
- Test: `Client.Tests/UIState/UIStateTestFixtureBase.cs:82-88`、`Client.Tests/Mining/MiningFocusStateTestFixture.cs`、`Client.Tests/Mining/MiningFocusStateTest.cs`、`Client.Tests/Interact/InteractControllerHighlightTest.cs`、`Client.Tests/Interact/TapInteractionDriverTest.cs`、`Client.Tests/Interact/InteractControllerDisableTest.cs`、`Client.Tests/PlaceSystem/Feedback/PlacementFeedbackTooltipPresenterTest.cs`、`Client.Tests/UIState/RightShortPressTransitionTest.cs`、`Client.Tests/Mining/MiningEquipmentSwitchTest.cs`

**Interfaces:**
- Produces: `MouseCursorTooltipState : IMouseCursorTooltip` — `IObservable<TooltipPresentation> OnPresentationChanged`, `TooltipPresentation GetPresentation()`, `Show(TooltipOwner, LocalizationKey)`, `Show(TooltipOwner, LocalizationKey, IReadOnlyList<string>)`, `Show(TooltipOwner, IReadOnlyList<TooltipLine>)`, `Hide(TooltipOwner)`。private フィールド名 `_currentOwner`（テストがリフレクションで読む）
- Produces: `MiningControllerContext(LocalPlayerEquipment, ProgressBarState, IMouseCursorTooltip)` と `IMouseCursorTooltip Tooltip { get; }`
- Produces: `MiningIdleState(MiningControllerContext context)`
- Produces: `InteractController(LocalPlayerEquipment, IInteractTargetSelector, ProgressBarState, IMouseCursorTooltip)`
- Produces: `TapInteractionDriver(IMouseCursorTooltip tooltip)`
- Produces: `DeleteObjectService(BuildOperationHistory, IMouseCursorTooltip)`; `DeleteObjectState(RailGraphClientCache, UiStateCameraPolicyService, BuildOperationHistory, BuildUndoService, PlacementTargetPickService, RightShortPressInputService, IMouseCursorTooltip)`
- Produces: `PlacementFeedbackTooltipPresenter(IMouseCursorTooltip tooltip)`
- Produces: `GameObjectTooltipTarget.OnCursorEnter(IMouseCursorTooltip tooltip)` / `OnCursorExit(IMouseCursorTooltip tooltip)`; `GameObjectToolTipTargetController` は `[Inject] IMouseCursorTooltip` を受ける
- Produces: `TooltipTopic(WebSocketHub hub, MouseCursorTooltipState tooltip)`

- [ ] **Step 1: テストを新型へ移植して失敗させる**

共通パターン（全テスト）: 
- `new GameObject("MouseCursorTooltip")` + `AddComponent<MouseCursorTooltip>()` + `SetField(..."canvasGroup"...)` + `SetField(..."itemName"...)` の4〜5行 → `_tooltip = new MouseCursorTooltipState();`（フィールド `private MouseCursorTooltipState _tooltip;`）
- `TestReflection.SetStaticProperty(typeof(MouseCursorTooltip), "Instance", null);` → 削除
- `MouseCursorTooltip.Instance.GetPresentation()` → `_tooltip.GetPresentation()`
- `using TMPro;` / `CanvasGroup` 参照を削除

個別:
- `UIStateTestFixtureBase.SetUpMouseCursorTooltip()` → `protected MouseCursorTooltipState CreateMouseCursorTooltip() => new();` に置換し、呼び出し側（`RightShortPressTransitionTest.cs:56`）で `var tooltip = CreateMouseCursorTooltip();` として `new DeleteObjectState(..., rightShortPressInputService, tooltip)` に渡す。
- `MiningFocusStateTestFixture.cs`: `new MiningControllerContext(equipment, _progressBar)` → `new MiningControllerContext(equipment, _progressBar, _tooltip)`（`_progressBar` が無ければ `new ProgressBarState()` を足す）。`MouseCursorTooltip.Instance.GetPresentation().Lines[0]...` → `_tooltip.GetPresentation().Lines[0]...`。`new MiningIdleState()` → `new MiningIdleState(context)`。
- `InteractControllerDisableTest.cs` / `InteractControllerHighlightTest.cs`: `new InteractController(equipment, selector, _progressBar)` → `new InteractController(equipment, selector, _progressBar, _tooltip)`。
- `TapInteractionDriverTest.cs`: `new TapInteractionDriver()` → `new TapInteractionDriver(_tooltip)`。
- `PlacementFeedbackTooltipPresenterTest.cs`: `_tooltip` を `MouseCursorTooltipState` に、`new PlacementFeedbackTooltipPresenter()` → `new PlacementFeedbackTooltipPresenter(_tooltip)`、`typeof(MouseCursorTooltip).GetField("_currentOwner", ...)` → `typeof(MouseCursorTooltipState).GetField("_currentOwner", ...)`、`MouseCursorTooltip.Instance.X` → `_tooltip.X`。`Instance == null` 分岐に対応するテストがあれば削除（新実装に null 経路は無い）。
- `MiningEquipmentSwitchTest.cs`: `new MiningControllerContext(equipment, _progressBar, _tooltip)`。

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `MouseCursorTooltipState` 未定義

- [ ] **Step 3: 実装する**

`IMouseCursorTooltip.cs`（`MouseCursorTooltip.cs` 15-23行を移動）:
```csharp
using System.Collections.Generic;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.UI.Tooltip
{
    public interface IMouseCursorTooltip
    {
        // 表示も非表示も所有者トークン付きで呼ぶ（現所有者以外のHideは他者の表示を消さない）
        // Both show and hide carry an owner token, so a Hide from anyone else never clears the current tooltip
        public void Hide(TooltipOwner owner);
        public void Show(TooltipOwner owner, LocalizationKey key);
        public void Show(TooltipOwner owner, LocalizationKey key, IReadOnlyList<string> textParams);
        public void Show(TooltipOwner owner, IReadOnlyList<TooltipLine> lines);
    }
}
```
`TooltipPresentation.cs`（同 100-135行を移動。using は `System`, `System.Collections.Generic`, `System.Linq`）。

`MouseCursorTooltipState.cs`:
```csharp
using System;
using System.Collections.Generic;
using Mooresmaster.Localization.Generated;
using UniRx;

namespace Client.Game.InGame.UI.Tooltip
{
    /// <summary>
    ///     カーソル付近に出す文言の所有者付き表示状態。描画は Web UI が tooltip topic 経由で行う
    ///     Owner-tracked state of the cursor tooltip; the Web UI renders it through the tooltip topic
    /// </summary>
    public class MouseCursorTooltipState : IMouseCursorTooltip
    {
        private readonly ReactiveProperty<TooltipPresentation> _presentation = new(TooltipPresentation.Hidden);
        private TooltipOwner _currentOwner;

        public IObservable<TooltipPresentation> OnPresentationChanged => _presentation;
        public TooltipPresentation GetPresentation() => _presentation.Value;

        public void Show(TooltipOwner owner, LocalizationKey key)
        {
            Show(owner, key, Array.Empty<string>());
        }

        public void Show(TooltipOwner owner, LocalizationKey key, IReadOnlyList<string> textParams)
        {
            Show(owner, new[] { new TooltipLine(key, textParams) });
        }

        // 表示したものは最後に呼んだ主体のものになる（所有権は毎回Showした側へ移る）
        // What is shown belongs to the last caller; ownership moves to whoever showed it
        public void Show(TooltipOwner owner, IReadOnlyList<TooltipLine> lines)
        {
            // 行が無い表示要求は非表示と同義（表示状態は行の有無から導出されるため）
            // A show request without lines means hidden, because visibility is derived from the lines
            if (lines.Count == 0)
            {
                Hide(owner);
                return;
            }

            _currentOwner = owner;
            _presentation.Value = new TooltipPresentation(lines);
        }

        // 自分が出していない表示は消さない（毎フレームHideする書き手が他者の表示を潰さないため）
        // Never clear a tooltip shown by someone else, so writers that hide every frame cannot stomp on others
        public void Hide(TooltipOwner owner)
        {
            if (_currentOwner != owner) return;

            _currentOwner = null;
            _presentation.Value = TooltipPresentation.Hidden;
        }
    }
}
```

`MouseCursorTooltip.cs`: インターフェースと struct のブロックを削除し、class 宣言を `public class MouseCursorTooltip : MonoBehaviour, IMouseCursorTooltip` のまま残す（孤児。PR2で削除）。

`MiningControllerContext.cs`: `using Client.Game.InGame.UI.Tooltip;` は既存。`public IMouseCursorTooltip Tooltip { get; }` を追加し ctor 引数 `IMouseCursorTooltip tooltip` を受けて代入。

`MiningIdleState.cs`:
```csharp
namespace Client.Game.InGame.Mining
{
    public class MiningIdleState : IMiningState
    {
        public MiningIdleState(MiningControllerContext context)
        {
            context.Tooltip.Hide(MiningControllerContext.TooltipOwner);
        }

        public IMiningState GetNextUpdate(MiningControllerContext context, float dt)
        {
            return context.CurrentFocusTarget != null ? new MiningFocusState() : this;
        }
    }
}
```
`MiningFocusState.cs`: `MouseCursorTooltip.Instance.` → `context.Tooltip.`（ローカル関数内は外側の `context` を捕捉）。`new MiningIdleState()` → `new MiningIdleState(context)`。`MiningCompleteState.cs` / `MiningProgressState.cs` の `new MiningIdleState()` も `context` を渡す。

`InteractController.cs`:
```csharp
        public InteractController(LocalPlayerEquipment localPlayerEquipment, IInteractTargetSelector selector, ProgressBarState progressBar, IMouseCursorTooltip tooltip)
        {
            _selector = selector;
            _miningContext = new MiningControllerContext(localPlayerEquipment, progressBar, tooltip);
            _tapDriver = new TapInteractionDriver(tooltip);
            _miningState = new MiningIdleState(_miningContext);
        }
```
（フィールド初期化子 `= new()` / `= new MiningIdleState()` を外し `readonly` は維持）

`TapInteractionDriver.cs`: `private readonly IMouseCursorTooltip _tooltip;` と ctor を追加、`MouseCursorTooltip.Instance.` → `_tooltip.`。

`DeleteObjectService.cs`: ctor を `DeleteObjectService(BuildOperationHistory buildOperationHistory, IMouseCursorTooltip tooltip)`、`MouseCursorTooltip.Instance.` → `_tooltip.`。`DeleteObjectState.cs`: ctor 末尾に `IMouseCursorTooltip tooltip` を追加し `new DeleteObjectService(buildOperationHistory, tooltip)`。

`PlacementFeedbackTooltipPresenter.cs`: ctor で `IMouseCursorTooltip tooltip` を受け `_tooltip` に保持。`if (MouseCursorTooltip.Instance == null) return;` の2箇所とそのコメントを削除、`MouseCursorTooltip.Instance.` → `_tooltip.`。

`GameObjectTooltipTarget.cs`: `OnCursorEnter()`/`OnCursorExit()` を `OnCursorEnter(IMouseCursorTooltip tooltip)`/`OnCursorExit(IMouseCursorTooltip tooltip)` にし `MouseCursorTooltip.Instance` → `tooltip`。`GameObjectToolTipTargetController.cs`: `[Inject] private IMouseCursorTooltip _tooltip;` を追加（`using VContainer;`）、呼び出しに `_tooltip` を渡す。Phase1ヘッダを削除（世界オブジェクトのツールチップ駆動で uGUI 非依存）。`MainGameStarter.cs`: `[SerializeField] private GameObjectToolTipTargetController gameObjectToolTipTargetController;` を追加し `builder.RegisterComponent(gameObjectToolTipTargetController);`。参照の配線は Task A9 の uloop ステップで行う。

`TooltipTopic.cs`: `MouseCursorTooltip _tooltip` → `MouseCursorTooltipState _tooltip`（ctor も）。`WebUiGameBinder.cs:114`: `new TooltipTopic(hub, resolver.Resolve<MouseCursorTooltipState>())`。コメント「uGUI/3D由来のツールチップ…」→「ゲーム内ツールチップ状態を Web へ接続する / Connect the in-game tooltip state to the web」。

`MainGameInteractionRegistration.RegisterUiAndPlayer`: `builder.Register<MouseCursorTooltipState>(Lifetime.Singleton).AsSelf().As<IMouseCursorTooltip>();`

- [ ] **Step 4: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `errors: 0`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Mining|Interact|PlacementFeedbackTooltipPresenterTest|RightShortPressTransitionTest|TooltipPresentationEqualityTest|WireContractC2Test"`
Expected: 全件 PASS

- [ ] **Step 5: コミット**

```bash
git add -A moorestech_client/Assets/Scripts
git commit -m "refactor: カーソルツールチップを MouseCursorTooltipState へ抽出し静的 Instance を DI に置換する"
```

---

### Task A7: CrosshairVisibility / UIRoot / BlueprintNameInputState

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Crosshair/CrosshairVisibility.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Blueprint/BlueprintNameInputState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Control/ViewMode/PlayerViewApplier.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/UIRoot.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/BlueprintCopySystem.cs:23-149`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/BlueprintNameInputWebBridge.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/CommonHudTopics.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:105-106,154,158`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（`[SerializeField] private UIRoot uiRoot;` 追加、`RegisterComponent(uiRoot)`、`blueprintNameInputView` の `RegisterComponent` 削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs`（`CrosshairVisibility`, `BlueprintNameInputState` を Singleton 登録）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/BlueprintNameInputStateTest.cs`（新規）

**Interfaces:**
- Produces: `CrosshairVisibility` — `void SetVisible(bool visible)`, `bool IsVisible()`, `IObservable<bool> OnVisibleChanged`
- Produces: `UIRoot`（MonoBehaviour維持、`Instance`・`canvasGroup` 削除、`IsVisible()`/`OnVisibilityChanged` はそのまま）
- Produces: `BlueprintNameInputState` — `bool IsOpen { get; }`, `IObservable<bool> OnOpenChanged`, `IObservable<string> OnConfirm`, `IObservable<Unit> OnCancel`, `void Open()`, `void Close()`, `void Confirm(string name)`, `void Cancel()`
- Produces: `PlayerViewApplier(InGameCameraController, CrosshairVisibility)`; `BlueprintCopySystem(Camera, ClientBlueprintLibrary, BlueprintNameInputState)`; `BlueprintNameInputWebBridge(BlueprintNameInputState, WebUiModalService)`; `CrosshairTopic(WebSocketHub, CrosshairVisibility)`; `UiVisibilityTopic(WebSocketHub, UIRoot)`（型不変）

- [ ] **Step 1: 失敗するテストを書く**

`Client.Tests/UIState/BlueprintNameInputStateTest.cs`:
```csharp
using Client.Game.InGame.UI.Blueprint;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.UIState
{
    public class BlueprintNameInputStateTest
    {
        [Test]
        public void 空白だけの名前は確定されない()
        {
            var state = new BlueprintNameInputState();
            string confirmed = null;
            using var subscription = state.OnConfirm.Subscribe(name => confirmed = name);
            state.Open();

            state.Confirm("   ");

            Assert.IsNull(confirmed);
            Assert.IsTrue(state.IsOpen);
        }

        [Test]
        public void 確定でTrimされた名前が流れ閉じる()
        {
            var state = new BlueprintNameInputState();
            string confirmed = null;
            var openLog = new System.Collections.Generic.List<bool>();
            using var s1 = state.OnConfirm.Subscribe(name => confirmed = name);
            using var s2 = state.OnOpenChanged.Subscribe(openLog.Add);
            state.Open();

            state.Confirm("  base  ");

            Assert.AreEqual("base", confirmed);
            Assert.IsFalse(state.IsOpen);
            CollectionAssert.AreEqual(new[] { true, false }, openLog);
        }

        [Test]
        public void 閉じているときの確定とキャンセルは無視される()
        {
            var state = new BlueprintNameInputState();
            var count = 0;
            using var s1 = state.OnConfirm.Subscribe(_ => count++);
            using var s2 = state.OnCancel.Subscribe(_ => count++);

            state.Confirm("x");
            state.Cancel();

            Assert.AreEqual(0, count);
        }
    }
}
```

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `BlueprintNameInputState` 未定義

- [ ] **Step 3: 実装する**

`CrosshairVisibility.cs`:
```csharp
using System;
using UniRx;

namespace Client.Game.InGame.UI.Crosshair
{
    /// <summary>
    ///     一人称視点の画面中央クロスヘアの表示フラグ。描画は Web UI が担う
    ///     Visibility flag of the first-person center crosshair; the Web UI renders it
    /// </summary>
    public class CrosshairVisibility
    {
        private readonly ReactiveProperty<bool> _visible = new(false);

        public IObservable<bool> OnVisibleChanged => _visible;
        public bool IsVisible() => _visible.Value;

        public void SetVisible(bool visible)
        {
            _visible.Value = visible;
        }
    }
}
```

`UIRoot.cs`: `Instance` プロパティと `Awake`、`canvasGroup` フィールドと `canvasGroup.alpha = ...` 行を削除（Ctrl+U のトグルと `_isActive` は維持）。

`BlueprintNameInputState.cs`:
```csharp
using System;
using UniRx;

namespace Client.Game.InGame.UI.Blueprint
{
    /// <summary>
    ///     BP名入力の開閉と確定/キャンセルの通知。入力欄そのものは Web のモーダルが担う
    ///     Open/close state and confirm/cancel notifications of the blueprint-name input; the web modal owns the field
    /// </summary>
    public class BlueprintNameInputState
    {
        public bool IsOpen { get; private set; }
        public IObservable<bool> OnOpenChanged => _onOpenChanged;
        public IObservable<string> OnConfirm => _onConfirm;
        public IObservable<Unit> OnCancel => _onCancel;

        private readonly Subject<bool> _onOpenChanged = new();
        private readonly Subject<string> _onConfirm = new();
        private readonly Subject<Unit> _onCancel = new();

        public void Open()
        {
            IsOpen = true;
            _onOpenChanged.OnNext(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            _onOpenChanged.OnNext(false);
        }

        // 空白のみの名前は確定させない
        // Reject whitespace-only names on confirm
        public void Confirm(string name)
        {
            if (!IsOpen) return;
            if (string.IsNullOrWhiteSpace(name)) return;
            _onConfirm.OnNext(name.Trim());
            Close();
        }

        public void Cancel()
        {
            if (!IsOpen) return;
            _onCancel.OnNext(Unit.Default);
            Close();
        }
    }
}
```

`BlueprintCopySystem.cs`: 型を `BlueprintNameInputState` に（フィールド名 `_nameInputState`）。`.AddTo(_nameInputView)` はMonoBehaviour寿命依存なので、`CompositeDisposable _subscriptions = new();` フィールドを追加して `.AddTo(_subscriptions)` に置換（`BlueprintCopySystem` は Singleton なのでゲーム寿命。AGENTS.md「dispose漏れは考慮不要」）。`Open()/Close()` はそのまま。

`BlueprintNameInputWebBridge.cs`: `BlueprintNameInputView _view` → `BlueprintNameInputState _state`。`if (!WebUiScreenGate.IsWebUiMode) return;` とコメントを削除（`using Client.Game.InGame.UI.UIState;` も）。`_view.SetConfirmFromWeb(text)` → `_state.Confirm(text)`、`_view.SetCancelFromWeb()` → `_state.Cancel()`。クラスコメントの「uGUIモード時は何もしない」を削除。

`PlayerViewApplier.cs`: ctor で `CrosshairVisibility crosshairVisibility` を受け、`CrosshairView.Instance.SetVisible(isFirstPerson)` → `_crosshairVisibility.SetVisible(isFirstPerson)`。`using Client.Game.InGame.UI.Crosshair;` 維持。

`CommonHudTopics.cs`: `CrosshairView _view` → `CrosshairVisibility _visibility`（ctor・`view.OnVisibleChanged`・`_view.IsVisible()`）。`UiVisibilityTopic` は `UIRoot` 型のまま。

`WebUiGameBinder.cs`:
```csharp
            hub.RegisterTopic(CrosshairTopic.TopicName, new CrosshairTopic(hub, resolver.Resolve<CrosshairVisibility>()));
            hub.RegisterTopic(UiVisibilityTopic.TopicName, new UiVisibilityTopic(hub, resolver.Resolve<UIRoot>()));
            ...
            var blueprintNameInputState = resolver.Resolve<BlueprintNameInputState>();
            ...
            new BlueprintNameInputWebBridge(blueprintNameInputState, modalService);
```

`MainGameStarter.cs`: `[SerializeField] private UIRoot uiRoot;` を `uIStateControl` の隣に追加、`builder.RegisterComponent(uiRoot);`。`builder.RegisterComponent(blueprintNameInputView);` を削除し、フィールド `blueprintNameInputView` も削除（MainGame.unity 側の参照は Task A9 で外す）。

`MainGameInteractionRegistration.RegisterUiAndPlayer`: `builder.Register<CrosshairVisibility>(Lifetime.Singleton); builder.Register<BlueprintNameInputState>(Lifetime.Singleton);`

- [ ] **Step 4: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `errors: 0`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "BlueprintNameInputStateTest|WireContractC2Test|UIStateCameraInteractionTest|UiStateCameraPolicy"`
Expected: 全件 PASS

- [ ] **Step 5: コミット**

```bash
git add -A moorestech_client/Assets/Scripts
git commit -m "refactor: クロスヘア表示・UIRoot・BP名入力を純ロジック状態へ抽出し Web ブリッジを DI 経由にする"
```

---

### Task A8: ポーズメニュー（切断状態・セーブ要求）と ChallengeManager / GameStateController / BackgroundSkit の uGUI 依存除去

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/NetworkDisconnectState.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/GameSaveRequester.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/PauseMenuTopic.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/PauseMenuActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:93-95,193`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Challenge/ChallengeManager.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Common/GameStateController.cs`
- Rename: `moorestech_client/Assets/Scripts/Client.Skit/UI/BackgroundSkitUI.cs` → `BackgroundSkitVoicePlayer.cs`（.meta も `git mv`）
- Modify: `moorestech_client/Assets/Scripts/Client.Skit/Context/StoryContextExtension.cs:14`
- Modify: `moorestech_client/Assets/Scripts/Client.Skit/Commands/BackgroundSkitTextCommand.cs:24-32`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BackgroundSkit/BackgroundSkitManager.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（`saveButton`/`networkDisconnectPresenter`/`pauseMenuObject`/`deleteBarObject`/`challengeListView`/`researchTreeViewManager`/`playerInventoryViewController`/`craftInventoryView`/`machineRecipeView`/`recipeViewerView`/`itemListView`/`recipeTabView`/`buildMenuView` のフィールドと `RegisterComponent` を削除。`saveAndQuitPresenter` は残す）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`（`builder.RegisterEntryPoint<NetworkDisconnectState>().AsSelf(); builder.Register<GameSaveRequester>(Lifetime.Singleton);`）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateTestFixtureBase.cs:98-114`（`CurrentChallengeHudView` 生成と `SetField(gameState, "currentChallengeHudView", ...)` を削除）

**Interfaces:**
- Produces: `NetworkDisconnectState : IInitializable` — `bool IsDisconnected { get; }`, `IObservable<bool> OnDisconnectedChanged`, `void Initialize()`（`ClientContext.VanillaApi.OnDisconnect` を購読）
- Produces: `GameSaveRequester` — `void Save()`
- Produces: `PauseMenuTopic(WebSocketHub, NetworkDisconnectState)`; `PauseMenuSaveActionHandler(GameSaveRequester)`
- Produces: `ChallengeManager`（`currentChallengeHudView`・`_challengeListView`・`Construct` の `ChallengeListView` 引数を削除。`Construct(InitialHandshakeResponse)`）
- Produces: `BackgroundSkitVoicePlayer : MonoBehaviour` — `void SetActive(bool)`, `UniTask PlayVoiceAndWait(AudioClip)`（`SetText`/`SetTextVisible` 削除）
- Produces: `StoryContextExtension.GetBackgroundSkitVoicePlayer(this StoryContext)`

- [ ] **Step 1: 既存テストを新型へ寄せて失敗させる**

`UIStateTestFixtureBase.SetUpGameStateController()` から `var challengeHud = CreateComponent<CurrentChallengeHudView>("ChallengeHud");` と `SetField(gameState, "currentChallengeHudView", challengeHud);` を削除、`using Client.Game.InGame.UI.Challenge;` を削除。

- [ ] **Step 2: コンパイルして失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: この時点ではテストは通るがフィールド削除で `GameStateController` は変更前なので `errors: 0`（次のステップで実装）

- [ ] **Step 3: 実装する**

`NetworkDisconnectState.cs`:
```csharp
using System;
using Client.Game.InGame.Context;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.Presenter.PauseMenu
{
    /// <summary>
    ///     サーバー切断の論理状態。表示は Web のポーズメニューが pause_menu topic 経由で行う
    ///     Logical disconnect state; the web pause menu renders it through the pause_menu topic
    /// </summary>
    public class NetworkDisconnectState : IInitializable
    {
        private readonly ReactiveProperty<bool> _isDisconnected = new(false);

        public bool IsDisconnected => _isDisconnected.Value;
        public IObservable<bool> OnDisconnectedChanged => _isDisconnected;

        public void Initialize()
        {
            ClientContext.VanillaApi.OnDisconnect.Subscribe(_ => _isDisconnected.Value = true);
        }
    }
}
```

`GameSaveRequester.cs`:
```csharp
using System;
using Client.Game.InGame.Context;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.Presenter.PauseMenu
{
    /// <summary>
    ///     セーブ要求の送信口。応答は要求番号のみで待ち合わせ先が無いため失敗のログだけ観測する
    ///     Entry point for save requests; the response carries only the generation, so only failures are logged
    /// </summary>
    public class GameSaveRequester
    {
        public void Save()
        {
            ClientContext.VanillaApi.Response.Save(default).Forget(LogSaveFailure);
        }

        private static void LogSaveFailure(Exception exception)
        {
            Debug.LogError($"セーブ要求に失敗しました: {exception.GetType()} {exception.Message}\n{exception.StackTrace}");
        }
    }
}
```

`PauseMenuTopic.cs`: `NetworkDisconnectPresenter _presenter` → `NetworkDisconnectState _state`（ctor・購読・`IsDisconnected`）。`PauseMenuActions.cs`: `SaveButton _saveButton` → `GameSaveRequester _saveRequester`、`_saveButton.Save()` → `_saveRequester.Save()`。`WebUiGameBinder.cs`: `resolver.Resolve<NetworkDisconnectPresenter>()` → `resolver.Resolve<NetworkDisconnectState>()`、`new PauseMenuSaveActionHandler(resolver.Resolve<SaveButton>())` → `(resolver.Resolve<GameSaveRequester>())`。

`ChallengeManager.cs`: Phase1ヘッダ削除。`currentChallengeHudView` フィールド・`_challengeListView` フィールド・`Construct` の `ChallengeListView challengeListView` 引数・`currentChallengeHudView.SetCurrentChallenge(...)`・`_challengeListView.SetUI(...)`・`_challengeListView.UpdateUI(...)`・`ProcessChallengeCompletion` 内の `currentChallengeHudView.SetCurrentChallenge(nextList)` と `await currentChallengeHudView.OnChallengeCompleted(completedChallengeGuid);` を削除。`ProcessChallengeCompletion` は `await` が無くなるので `void` の通常ローカル関数 `ApplyNextTutorials(List<ChallengeMasterElement> nextList)` に改名し `.Forget()` 呼び出しを直接呼び出しに変える。`backgroundSkitManager` フィールドは未使用なら削除（`grep -n backgroundSkitManager` で本ファイル内の使用が宣言のみなら削除し、prefab側参照は Task A9 で Missing にならないよう確認）。`using Cysharp.Threading.Tasks;` が不要になれば削除。

`GameStateController.cs`: `currentChallengeHudView` フィールドと `SetActive` 3行、`using Client.Game.InGame.UI.Challenge;` を削除。

`BackgroundSkitVoicePlayer.cs`（改名後の全文）:
```csharp
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Skit.UI
{
    /// <summary>
    ///     背景スキットの音声再生。文字表示は Web が SkitPresentationStateStore 経由で描く
    ///     Voice playback for background skits; the web renders the text through SkitPresentationStateStore
    /// </summary>
    public class BackgroundSkitVoicePlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource voiceSource;

        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
        }

        public async UniTask PlayVoiceAndWait(AudioClip voice)
        {
            if (voice == null)
            {
                await UniTask.Delay(3000);
                return;
            }

            voiceSource.clip = voice;
            voiceSource.Play();

            await UniTask.Delay((int)(voiceSource.clip.length * 1000));
        }
    }
}
```
`StoryContextExtension.cs:14`: `GetBackgroundSkitUI` → `GetBackgroundSkitVoicePlayer`（戻り型 `BackgroundSkitVoicePlayer`）。`BackgroundSkitTextCommand.cs`: `var skitUi = storyContext.GetBackgroundSkitUI();` → `var voicePlayer = storyContext.GetBackgroundSkitVoicePlayer();`、`skitUi.SetText(...)` 行を削除、`await skitUi.PlayVoiceAndWait(voiceClip)` → `await voicePlayer.PlayVoiceAndWait(voiceClip)`。コメント「解決文をWeb・uGUIへ共有」→「解決文をWebへ共有 / Share the resolved text with the web」。
`BackgroundSkitManager.cs`: フィールド型を `BackgroundSkitVoicePlayer backgroundSkitVoicePlayer` に、`backgroundSkitUI.SetTextVisible(!WebUiScreenGate.IsWebUiMode);` とそのコメント2行を削除、他の `backgroundSkitUI` → `backgroundSkitVoicePlayer`。`using Client.Game.InGame.UI.UIState;` は `uiStateControl`/`UIStateEnum` で使うので残す。

`MainGameStarter.cs`: 上記フィールドと `RegisterComponent` 行を削除。対応する `using`（`Client.Game.InGame.UI.Inventory.Block.Research` / `.RecipeViewer` / `.Craft` / `.Main` / `.Blueprint` / `.BuildMenu` / `.UIState.UIObject` / `.Presenter.PauseMenu`）のうち未使用になったものを削除。

`MainGameModelRegistration.cs`: `builder.RegisterEntryPoint<NetworkEventInventoryUpdater>();` の下に
```csharp
            // 切断状態とセーブ要求はWebのポーズメニューが読む論理モデル
            // Disconnect state and save requests are logical models read by the web pause menu
            builder.RegisterEntryPoint<NetworkDisconnectState>().AsSelf();
            builder.Register<GameSaveRequester>(Lifetime.Singleton);
```
（`using Client.Game.InGame.Presenter.PauseMenu;` 追加）

- [ ] **Step 4: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `errors: 0`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "UIState|WebUi|SkitPresentationStateStoreTest"`
Expected: 全件 PASS

- [ ] **Step 5: コミット**

```bash
git add -A moorestech_client/Assets/Scripts
git commit -m "refactor: 切断状態・セーブ要求・チャレンジ進行・背景スキット音声から uGUI ビュー依存を外す"
```

---

### Task A9: prefab / シーンの参照配線（uloop）と PlayMode スモーク

**Files:**
- Modify（uloop経由）: `moorestech_client/Assets/Asset/Common/Prefab/GameSystem.prefab`（`MainGameStarter` の新フィールド `uiRoot`・`gameObjectToolTipTargetController` を配線、削除したフィールドの残骸は Unity が自動で捨てる）
- Modify（uloop経由）: `moorestech_client/Assets/Asset/UI/Prefab/MainGameUI.prefab`（`BacgkroundSkitUI` の `BackgroundSkitVoicePlayer` は改名で GUID 維持のため配線不変。確認のみ）

- [ ] **Step 1: 参照を配線する**

`uloop execute-dynamic-code` で次を実行:
```csharp
using UnityEditor;
using UnityEngine;
using Client.Starter;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.Tooltip;

var gameSystemPath = "Assets/Asset/Common/Prefab/GameSystem.prefab";
var root = PrefabUtility.LoadPrefabContents(gameSystemPath);
var starter = root.GetComponentInChildren<MainGameStarter>(true);
var so = new SerializedObject(starter);
var tooltipController = root.GetComponentInChildren<GameObjectToolTipTargetController>(true);
so.FindProperty("gameObjectToolTipTargetController").objectReferenceValue = tooltipController;
so.ApplyModifiedPropertiesWithoutUndo();
PrefabUtility.SaveAsPrefabAsset(root, gameSystemPath);
PrefabUtility.UnloadPrefabContents(root);
Debug.Log($"wired tooltip controller: {tooltipController != null}");
```
`uiRoot` は `MainGameUI.prefab` 側にあり `GameSystem.prefab` からは同一シーン内参照になるため、`Assets/Scenes/Game/MainGame.unity` を開いて `MainGameStarter.uiRoot` に `MainGameUI` ルートの `UIRoot` を配線し `EditorSceneManager.SaveScene` する（`EditorSceneManager.OpenScene` → `Object.FindFirstObjectByType<MainGameStarter>()` → `FindFirstObjectByType<UIRoot>()` → `SerializedObject` で設定）。MainGameStarter が prefab インスタンスなら `PrefabUtility.RecordPrefabInstancePropertyModifications` を呼ぶ。

- [ ] **Step 2: Missing 参照が無いことを確認する**

`uloop execute-dynamic-code`:
```csharp
var starter = Object.FindFirstObjectByType<Client.Starter.MainGameStarter>();
var so = new SerializedObject(starter);
var p = so.GetIterator();
var missing = new System.Collections.Generic.List<string>();
while (p.NextVisible(true))
    if (p.propertyType == SerializedPropertyType.ObjectReference && p.objectReferenceValue == null && p.objectReferenceInstanceIDValue != 0) missing.Add(p.propertyPath);
Debug.Log("missing: " + string.Join(", ", missing));
```
Expected: `missing:` が空

- [ ] **Step 3: PlayMode スモーク（プレイテストDSL）**

unity-playmode-recorded-playtest スキルの `scripts/run-scenario.sh` で「インベントリを開く→ブロックを設置→機械UIを開く→採掘で進捗バーが出る」シナリオ（同スキルの references のインベントリ/ビルド/採掘サンプルを組み合わせる）を実行。
Expected: `result.json` が success、`uloop get-logs --log-type Error` にエラー無し。Web 側の block inventory パネルにスロットが表示され、ui.progress で採掘バーが動く。

- [ ] **Step 4: コミット**

```bash
git add -A moorestech_client/Assets
git commit -m "chore: MainGameStarter の新規参照（UIRoot・ツールチップ駆動）を配線する"
```

---

### Task A10: Phase1 ヘッダ整理と PR1 の全ブランチレビュー

**Files:**
- Modify: PR1で純ロジック化して残すファイルから `// [uGUI廃止Phase1] ...` ヘッダ2行を削除: `ISubInventory.cs`（済）、`LocalPlayerInventory.cs`、`LocalPlayerInventoryController.cs`、`NetworkEventInventoryUpdater.cs`、`ItemRecipeViewerDataContainer.cs`、`ChallengeManager.cs`（済）、`GameObjectToolTipTargetController.cs`（済）、`WebUiScreenGate.cs`（コメント本文の「置換済みuGUIビューの表示抑止にだけ使う」は残す）
- Modify: `docs/webui/ugui-retirement-plan.md` に「PR1（抽出）完了」の1行を Phase 2 見出し直下へ追記

- [ ] **Step 1: ヘッダを削除しコンパイル**

Run: `uloop compile --project-path ./moorestech_client` → `errors: 0`

- [ ] **Step 2: 関連テスト全走**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.(UIState|WebUi|Inventory|Interact|Mining|PlaceSystem|Tooltip|Localization)"`
Expected: 全件 PASS

- [ ] **Step 3: コミットして push、PR作成**

```bash
git add -A
git commit -m "docs: uGUI退役計画に PR1（論理モデル抽出）の完了を記録する"
```
pr-create スキルで PR を作る（タイトル「uGUI退役 PR1: 論理状態の純ロジック抽出とState/WebUiHost/テストの差し替え」、本文に ADR 0052 と本 plan へのリンク、bd `moorestech-lnsf.1`）。

- [ ] **Step 4: 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

moores-code-review スキルを起動し、指摘の機械的修正を適用してから push。完了後 `bd close moorestech-lnsf.1 --reason="PR作成・レビュー適用済み"`。PR作成後は `moores-wt rm feature/ugui-removal-extract-models`。

---

# Part B: PR2 — 残骸の一括削除 `moorestech-lnsf.2`

PR1 のマージ後に着手する。

### Task B0: worktree 準備

- [ ] **Step 1**
```bash
bd update moorestech-lnsf.2 --claim
moores-wt new feature/ugui-removal-delete --from origin/master --fetch
```
Run: `uloop compile --project-path ./moorestech_client` → `errors: 0`

---

### Task B1: 孤児 .cs の削除

**Files（Delete、すべて `.meta` 込み）:**

`Client.Game/InGame/UI/`:
- `Blueprint/BlueprintNameInputView.cs`
- `BuildMenu/BuildMenuView.cs`, `BuildMenu/BuildMenuEntry.cs`, `BuildMenu/BuildMenuEntryCatalog.cs`
- `Challenge/ChallengeListView.cs`, `ChallengeListViewCategoryElement.cs`, `ChallengeTreeView.cs`, `ChallengeTreeViewElement.cs`, `CurrentChallengeHudView.cs`, `CurrentChallengeHudViewElement.cs`, `ITreeViewElement.cs`, `TreeViewAdjuster.cs`（`ChallengeManager.cs` は残す）
- `Crosshair/CrosshairView.cs`
- `Inventory/Block/*`（`Research/` 含む全ファイル。`IBlockInventoryView.cs` 含む）
- `Inventory/Common/FluidSlotView.cs`, `ProgressArrowView.cs`（`ItemSlotView.cs`/`CommonSlotView.cs`/`CommonSlotViewOption.cs`/`CommonSlotViewExtension.cs` は Task B2 でデバッグ側へ移動）
- `Inventory/Craft/*`（4ファイル）
- `Inventory/Main/Interaction/*`（3ファイル）, `Inventory/Main/PlayerInventoryMainSlotsView.cs`, `Inventory/Main/PlayerInventoryViewController.cs`
- `Inventory/RecipeViewer/MachineRecipeView.cs`, `RecipeTabView.cs`, `RecipeViewerTabElement.cs`, `RecipeViewerView.cs`（`ItemRecipeViewerDataContainer.cs` は残す）
- `Inventory/Train/*`（2ファイル）
- `Inventory/ISubInventoryView.cs`, `Inventory/IInventorySource.cs`（実装が残っていないことを `grep -rn IInventorySource` で確認してから）
- `Modal/*`（`ModalManager.cs` と `ModalObject/*` 全部）
- `ProgressBar/ProgressBarView.cs`
- `Tooltip/MouseCursorTooltip.cs`, `Tooltip/UGuiTooltipTarget.cs`
- `UIState/UIObject/PauseMenuObject.cs`, `UIState/UIObject/DeleteBarObject.cs`, `UIState/WebUiCefToggle.cs`

`Client.Game/`:
- `InGame/Presenter/PauseMenu/SaveButton.cs`, `NetworkDisconnectPresenter.cs`
- `InGame/Control/UICursorFollowControl.cs`, `UICursorFollowControlRootCanvasRect.cs`
- `Common/UIRaycastTarget.cs`
- `InGame/Context/ClientContext.cs`: `ModalManager` プロパティと ctor 引数を削除。`Client.Starter/InitializeScenePipeline.cs:117,152`: `var modalManager = new ModalManager();` と ctor の最後の引数を削除。

`Client.Tests/`:
- `UIState/Fakes/FakeBuildMenuView.cs`（PR1で削除済みなら不要）
- `Localization/Display/ItemSlotDefaultTooltipTest.cs`
- `EditModeInPlayingTest/ChallengeListUITest.cs`, `ElectricToGearModeSelectUITest.cs`, `MachineModuleSlotUITest.cs`, `MachineRecipeSelectionGearUITest.cs`, `MachineRecipeSelectionUITest.cs`, `MachineRecipeSelectionTestHelper.cs`（サーバー往復の移植は Task B6）
- `WebUi/Gate/WebUiScreenGateTest.cs`（`IsWebUiMode` 恒久 true の検証は無意味になるため削除。`WebUiScreenGate` 自体は残す — 世界空間ピン等が参照）

`Client.Playtest/Operations/PlaytestUiOps.cs`: `TryClickBuildMenuSlot` と非CEF経路（`useWebUi == false` 分岐）、`using UnityEngine.EventSystems; using Client.Game.InGame.UI.BuildMenu; using Client.Game.InGame.UI.Inventory.Common;` を削除。`CefScreenMapper.cs` は CEF 例外として無変更。

- [ ] **Step 1: 削除前に参照を機械確認する**

```bash
cd moorestech_client/Assets/Scripts
for t in BuildMenuView BlueprintNameInputView CrosshairView ProgressBarView MouseCursorTooltip UGuiTooltipTarget PlayerInventoryViewController RecipeViewerView ChallengeListView ResearchTreeViewManager CurrentChallengeHudView SaveButton NetworkDisconnectPresenter PauseMenuObject DeleteBarObject ModalManager ISubInventoryView ITrainInventoryView IBlockInventoryView CommonBlockInventoryViewBase FluidSlotView ProgressArrowView UICursorFollowControl UIRaycastTarget WebUiCefToggle; do echo "$t: $(grep -rlw --include='*.cs' "$t" . | grep -vE "/$t\.cs$" | tr '\n' ' ')"; done
```
Expected: 各行の参照が「削除対象ファイル同士」か「Tests/WebUi/Gate/WebUiGateClassification.cs（Task B5で更新）」のみ。それ以外が出たら PR1 の抽出漏れなので、そのタスクの手順で先に差し替える。

- [ ] **Step 2: 削除する**

```bash
git rm -r moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Craft moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Main/Interaction moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Train moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Modal
git rm moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Block.meta moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Craft.meta moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Main/Interaction.meta moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Train.meta moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Modal.meta
# 個別ファイル（.cs と .cs.meta のペア）
for f in Blueprint/BlueprintNameInputView BuildMenu/BuildMenuView BuildMenu/BuildMenuEntry BuildMenu/BuildMenuEntryCatalog Challenge/ChallengeListView Challenge/ChallengeListViewCategoryElement Challenge/ChallengeTreeView Challenge/ChallengeTreeViewElement Challenge/CurrentChallengeHudView Challenge/CurrentChallengeHudViewElement Challenge/ITreeViewElement Challenge/TreeViewAdjuster Crosshair/CrosshairView Inventory/Common/FluidSlotView Inventory/Common/ProgressArrowView Inventory/Main/PlayerInventoryMainSlotsView Inventory/Main/PlayerInventoryViewController Inventory/RecipeViewer/MachineRecipeView Inventory/RecipeViewer/RecipeTabView Inventory/RecipeViewer/RecipeViewerTabElement Inventory/RecipeViewer/RecipeViewerView Inventory/ISubInventoryView Inventory/IInventorySource ProgressBar/ProgressBarView Tooltip/MouseCursorTooltip Tooltip/UGuiTooltipTarget UIState/UIObject/PauseMenuObject UIState/UIObject/DeleteBarObject UIState/WebUiCefToggle; do git rm "moorestech_client/Assets/Scripts/Client.Game/InGame/UI/$f.cs" "moorestech_client/Assets/Scripts/Client.Game/InGame/UI/$f.cs.meta"; done
git rm moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/UIObject.meta
git rm moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/SaveButton.cs* moorestech_client/Assets/Scripts/Client.Game/InGame/Presenter/PauseMenu/NetworkDisconnectPresenter.cs* moorestech_client/Assets/Scripts/Client.Game/InGame/Control/UICursorFollowControl.cs* moorestech_client/Assets/Scripts/Client.Game/InGame/Control/UICursorFollowControlRootCanvasRect.cs* moorestech_client/Assets/Scripts/Client.Game/Common/UIRaycastTarget.cs*
git rm moorestech_client/Assets/Scripts/Client.Tests/Localization/Display/ItemSlotDefaultTooltipTest.cs* moorestech_client/Assets/Scripts/Client.Tests/WebUi/Gate/WebUiScreenGateTest.cs*
git rm moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ChallengeListUITest.cs* moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/ElectricToGearModeSelectUITest.cs* moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/MachineModuleSlotUITest.cs* moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/MachineRecipeSelectionGearUITest.cs* moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/MachineRecipeSelectionUITest.cs* moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/MachineRecipeSelectionTestHelper.cs*
```
`ClientContext.cs` / `InitializeScenePipeline.cs` / `PlaytestUiOps.cs` を上記どおり編集。`Client.Tests/Localization/Resolution/LocalizeContentTest.cs:136` の `ItemSlotView.GetToolTipText(itemView)` は `Localize.GetContent(ContentLocalizationKeys.ItemName(itemMaster.ItemGuid))` に置換（`ItemSlotView.GetToolTipText` の実装が別ロジックなら、その実装本文をテスト側の期待値計算にインライン化する）。`SerializedLocalizedTooltipKeyTest.cs` から `AssertUGuiTooltipKeys` とその呼び出しを削除。

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `errors: 0`。エラーが出たら、そのシンボルの生きた参照が PR1 の抽出漏れ。Step 1 の表に戻る。

- [ ] **Step 4: コミット**

```bash
git add -A
git commit -m "chore: 移行済み画面uGUIの孤児スクリプトとテストを削除する"
```

---

### Task B2: デバッグ用 ItemSlotView をデバッグ側へ移し、Starter の事前ロードを外す

**Files:**
- Rename（`git mv`、.meta 込み）: `Client.Game/InGame/UI/Inventory/Common/ItemSlotView.cs` → `Client.DebugSystem/ItemSlot/ItemSlotView.cs`、同 `CommonSlotView.cs` / `CommonSlotViewOption.cs` / `CommonSlotViewExtension.cs`（namespace は `Client.DebugSystem.ItemSlot` に変更。`Client.Game/InGame/UI/Inventory/Common` ディレクトリは空になるので `.meta` ごと削除）
- Modify: `Client.DebugSystem/ItemSelectModal.cs`（`using Client.DebugSystem.ItemSlot;`、`Initialize()` を `async UniTask` にして先頭で `await ItemSlotView.LoadItemSlotViewPrefab();`、`SelectItem` から `await Initialize();`）
- Modify: `CommonSlotView.cs`（`UGuiTooltipTarget` フィールド `uGuiTooltipTarget` と、それを使う `SetView` のツールチップ設定・`IPointerEnter/Exit/MoveHandler` のツールチップ転送コードを削除。`using Client.Game.InGame.UI.Tooltip;` と `Mooresmaster.Localization.Generated` の tooltip キー参照を削除。EventSystems の pointer handler 自体は右クリック選択に必要なので残す）
- Modify: `ItemSlotView.cs`（`SetItem(ItemViewData, int, string toolTipText)` の tooltip 引数経路・`GetToolTipText`・`SetFluid`・`SetTextOnly`・`Client.Localization` 依存を削除。デバッグモーダルが使う `Prefab`/`LoadItemSlotViewPrefab`/`SetItem(ItemViewData, int)`/`ItemViewData`/`OnRightClickUp`/`OnLeftClickUp` だけ残す）
- Modify: `Client.Starter/Initialization/ModAssetLoader.cs:40-57`（`PreloadCriticalAssetsAsync` を削除。呼び出し元 `InitializeScenePipeline` の該当 await 行も削除。「ChestBlockInventory」事前ロードはその prefab を消すため不要）
- Modify（uloop）: `Assets/AddressableResources/UI/ItemSlotView.prefab` から `UGuiTooltipTarget` コンポーネントを除去（スクリプト削除により Missing になるため、`GameObjectUtility.RemoveMonoBehavioursWithMissingScript` で掃除）

- [ ] **Step 1: 移動と編集**

```bash
mkdir -p moorestech_client/Assets/Scripts/Client.DebugSystem/ItemSlot
for f in ItemSlotView CommonSlotView CommonSlotViewOption CommonSlotViewExtension; do git mv moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Common/$f.cs moorestech_client/Assets/Scripts/Client.DebugSystem/ItemSlot/$f.cs; git mv moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Common/$f.cs.meta moorestech_client/Assets/Scripts/Client.DebugSystem/ItemSlot/$f.cs.meta; done
git rm moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/Common.meta
```
（`ItemSlot.meta` は Unity 起動で生成されるので、生成後にコミットに含める）

- [ ] **Step 2: Missing スクリプト掃除（uloop execute-dynamic-code）**

```csharp
var path = "Assets/AddressableResources/UI/ItemSlotView.prefab";
var root = PrefabUtility.LoadPrefabContents(path);
var removed = 0;
foreach (var t in root.GetComponentsInChildren<Transform>(true)) removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
PrefabUtility.SaveAsPrefabAsset(root, path);
PrefabUtility.UnloadPrefabContents(root);
Debug.Log($"removed missing scripts: {removed}");
```
Expected: `removed missing scripts: 1`（UGuiTooltipTarget）

- [ ] **Step 3: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → `errors: 0`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "LocalizeContentTest|SerializedLocalizedTooltipKeyTest"` → PASS

- [ ] **Step 4: コミット**

```bash
git add -A
git commit -m "chore: デバッグ用 ItemSlotView を Client.DebugSystem へ移し Starter の uGUI 事前ロードを外す"
```

---

### Task B3: prefab・シーンの uGUI オブジェクト除去（uloop）

**Files（uloop execute-dynamic-code 経由でのみ変更）:**
- `Assets/Asset/UI/Prefab/MainGameUI.prefab`
- `Assets/Scenes/Game/MainGame.unity`
- `Assets/Asset/CutScene/CutSceneManager.prefab`
- `Assets/Asset/Common/Prefab/GameSystem.prefab`（Missing 掃除）

- [ ] **Step 1: MainGameUI.prefab を CEF ルート中心に縮小する**

```csharp
using System.Linq;
using UnityEditor;
using UnityEngine;
var path = "Assets/Asset/UI/Prefab/MainGameUI.prefab";
var root = PrefabUtility.LoadPrefabContents(path);
// SaveAndQuitPresenter はポーズメニュー配下にあるためルートへ移す（uGUI非依存・DI登録あり）
// SaveAndQuitPresenter lives under the pause menu; move it to the root (uGUI-free, DI-registered)
var saveAndQuit = root.GetComponentInChildren<Client.Game.InGame.Presenter.PauseMenu.SaveAndQuitPresenter>(true);
var saveAndQuitHost = new GameObject("SaveAndQuitPresenter");
saveAndQuitHost.transform.SetParent(root.transform, false);
UnityEditorInternal.ComponentUtility.CopyComponent(saveAndQuit);
UnityEditorInternal.ComponentUtility.PasteComponentAsNew(saveAndQuitHost);
// 削除対象の直下子。TutorialUI / CefUnity / SkitUI(ネスト) は残す
// Direct children to delete; keep TutorialUI, CefUnity and the nested SkitUI
var deleteNames = new[] { "PauseMenu", "Disconnected", "ProgressBar", "DeleteBar", "ChallengeHudView", "Loading", "UICursorFollowControlRootCanvasRect", "MouseCursorTooltip", "ChallengeListUI", "InventoryItems", "ResearchTreeUI" };
foreach (Transform child in root.transform.Cast<Transform>().ToArray())
{
    if (deleteNames.Contains(child.name)) { Object.DestroyImmediate(child.gameObject); continue; }
    if (child.name == "BacgkroundSkitUI")
    {
        // 文字表示の子だけ消し AudioSource を持つルートは残す
        // Remove only the text child; keep the root that owns the AudioSource
        var text = child.Find("BackgroundText");
        if (text != null) Object.DestroyImmediate(text.gameObject);
    }
}
var removed = 0;
foreach (var t in root.GetComponentsInChildren<Transform>(true)) removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
PrefabUtility.SaveAsPrefabAsset(root, path);
PrefabUtility.UnloadPrefabContents(root);
Debug.Log($"MainGameUI trimmed. missing removed={removed}. children now: {string.Join(",", PrefabUtility.LoadPrefabContents(path).transform.Cast<Transform>().Select(c => c.name))}");
```
Expected: children が `CefUnity, TutorialUI, BacgkroundSkitUI, SkitUI, SaveAndQuitPresenter` のみ。ルートの `UIRoot`/`UIStateControl`/`CanvasScaler`/`GraphicRaycaster`/`Canvas` は残る（`WebUiCefToggle` は Missing 掃除で消える。`CefUnity` ルートは prefab 上で `activeSelf = true` にしておく: `root.transform.Find("CefUnity").gameObject.SetActive(true)` を保存前に追加）。
その後 `MainGameStarter` の `saveAndQuitPresenter` 参照を新ホスト上のコンポーネントへ再配線する（Task A9 と同じ SerializedObject 手順、対象は `Assets/Scenes/Game/MainGame.unity` を開いて `MainGameStarter` に設定）。

- [ ] **Step 2: MainGame.unity の直下 uGUI オブジェクトを削除する**

```csharp
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Game/MainGame.unity");
foreach (var name in new[] { "BuildMenuView", "BlueprintNameInput", "CrosshairView" })
{
    var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
    if (go != null) Object.DestroyImmediate(go);
}
// EventSystem はデバッグUI（DebugSheet）のクリックに必要なので残す
// Keep EventSystem: the debug sheet still needs it for clicks
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
```

- [ ] **Step 3: CutSceneManager.prefab の Canvas を削除する**

```csharp
var path = "Assets/Asset/CutScene/CutSceneManager.prefab";
var root = PrefabUtility.LoadPrefabContents(path);
var canvas = root.transform.Find("CutSceneCanvas");
Object.DestroyImmediate(canvas.gameObject);
PrefabUtility.SaveAsPrefabAsset(root, path);
PrefabUtility.UnloadPrefabContents(root);
```
その後 `Assets/Asset/CutScene/*.playable` を開き、Missing バインディング（`PlayableDirector` の `m_SceneBindings`）が無いことを `director.playableAsset.outputs` で確認する（事前調査でバインド無しを確認済み）。

- [ ] **Step 4: GameSystem.prefab / 全 prefab の Missing スクリプト掃除**

```csharp
var guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Asset", "Assets/AddressableResources" });
var total = 0;
foreach (var guid in guids)
{
    var path = AssetDatabase.GUIDToAssetPath(guid);
    var root = PrefabUtility.LoadPrefabContents(path);
    var removed = 0;
    foreach (var t in root.GetComponentsInChildren<Transform>(true)) removed += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
    if (removed > 0) { PrefabUtility.SaveAsPrefabAsset(root, path); Debug.Log($"{path}: removed {removed}"); total += removed; }
    PrefabUtility.UnloadPrefabContents(root);
}
Debug.Log($"total missing removed: {total}");
```
ログに出た prefab が「削除予定 prefab（Task B4）」以外なら、そのコンポーネントが生きた依存だった可能性があるので個別に判断する（例: `GameSystem.prefab` の `ChallengeManager` から `currentChallengeHudView`/`challengeListView` 参照が消えるのは正常）。

- [ ] **Step 5: コミット**

```bash
git add -A moorestech_client/Assets
git commit -m "chore: MainGameUI/MainGame/CutSceneManager から uGUI オブジェクトを除去しCEFルート中心に縮小する"
```

---

### Task B4: 孤児 prefab・Addressable 登録・未参照アセットの削除

**Files:**
- Delete（uloop `AssetDatabase.DeleteAsset`）: `Assets/AddressableResources/UI/Block/*`（15）, `Assets/AddressableResources/UI/Modal/*`（2）, `Assets/AddressableResources/UI/Train/CommonTrainInventoryView.prefab`, `Assets/AddressableResources/UI/FluidSlotView.prefab`
- Delete: `Assets/Asset/UI/Prefab/` 配下の `MainGameUI.prefab` 以外すべて（`Challenge/`、`Inventory/`、`Research/`、`Craft Recipe Item Element.prefab`、`MissionBar.prefab`、`StoryUI.prefab`、`ChatlogEntry.prefab`、`MouseCursorTooltip.prefab`、`ProgressArrow.prefab`、`Recipe viwer *.prefab`）
- Delete: `Assets/Asset/Skit/SelectionButton.prefab`、`Assets/Asset/UI/ChallengeHudViewElement_*.anim`、`ChallengeHudViewElement.controller`
- Modify: Addressable グループ `Vanilla Asset Group` から `Vanilla/UI/*` のうち `Vanilla/UI/ItemSlotView` 以外を除去（`AssetDatabase.DeleteAsset` で実体を消すと Addressables の entry は `Remove Missing Entries` 相当の処理で消える: `AddressableAssetSettingsDefaultObject.Settings.RemoveMissingReferences()` ではなく `settings.RemoveAssetEntry(guid)` を各 entry に対して呼ぶ）
- Delete: `Assets/Asset/UI/NewUI`・`Assets/Asset/UI/OldUI` 配下で未参照になったスプライト・フォント・アトラス

- [ ] **Step 1: prefab と Addressable entry を削除する（uloop）**

```csharp
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
var settings = AddressableAssetSettingsDefaultObject.Settings;
var keep = "Assets/AddressableResources/UI/ItemSlotView.prefab";
var targets = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/AddressableResources/UI", "Assets/Asset/UI/Prefab" })
    .Select(AssetDatabase.GUIDToAssetPath)
    .Where(p => p != keep && p != "Assets/Asset/UI/Prefab/MainGameUI.prefab")
    .Concat(new[] { "Assets/Asset/Skit/SelectionButton.prefab" })
    .ToList();
foreach (var path in targets)
{
    var guid = AssetDatabase.AssetPathToGUID(path);
    var entry = settings.FindAssetEntry(guid);
    if (entry != null) settings.RemoveAssetEntry(guid);
    AssetDatabase.DeleteAsset(path);
}
AssetDatabase.SaveAssets();
Debug.Log($"deleted {targets.Count} prefabs");
```
Expected: `deleted 36 prefabs`（AddressableResources/UI 19 + Asset/UI/Prefab 19 − MainGameUI − ItemSlotView ＋ SelectionButton ＝ 37 前後。実数をログで確認）。空になったディレクトリ（`Assets/AddressableResources/UI/Block` 等）は `AssetDatabase.DeleteAsset(dir)` で消す。

- [ ] **Step 2: 未参照アセットを検出して削除する（uloop）**

```csharp
using System.Linq;
using UnityEditor;
using UnityEngine;
// Assets 全体の依存を逆引きし、Asset/UI 配下で誰からも参照されないものを列挙する
// Reverse-map dependencies across Assets and list Asset/UI files nothing references
var all = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") && !p.StartsWith("Assets/Asset/UI/")).ToArray();
var referenced = new System.Collections.Generic.HashSet<string>();
foreach (var p in all) foreach (var d in AssetDatabase.GetDependencies(p, true)) referenced.Add(d);
var candidates = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/Asset/UI/") && !AssetDatabase.IsValidFolder(p) && !referenced.Contains(p) && p != "Assets/Asset/UI/Prefab/MainGameUI.prefab").ToList();
// MainGameUI.prefab 自身の依存は残す
// Keep everything MainGameUI.prefab still depends on
var keep = new System.Collections.Generic.HashSet<string>(AssetDatabase.GetDependencies("Assets/Asset/UI/Prefab/MainGameUI.prefab", true));
candidates = candidates.Where(c => !keep.Contains(c)).ToList();
System.IO.File.WriteAllLines("/tmp/ugui-unreferenced.txt", candidates);
Debug.Log($"unreferenced under Asset/UI: {candidates.Count}");
```
`/tmp/ugui-unreferenced.txt` を目視し、MainMenu シーン・DebugObjects・HPバー・`ItemSlotView.prefab` の依存（スプライト等）が含まれていないことを確認（`AssetDatabase.GetDependencies` はシーンも `all` に含むので原則含まれない）。確認後 `foreach (var p in File.ReadAllLines(...)) AssetDatabase.DeleteAsset(p);` で削除。

- [ ] **Step 3: コンパイルと Addressable ビルド確認**

Run: `uloop compile --project-path ./moorestech_client` → `errors: 0`
`uloop execute-dynamic-code` で `UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.CleanPlayerContent(); UnityEditor.AddressableAssets.Settings.AddressableAssetSettings.BuildPlayerContent();` を実行し、エラーが無いことを `uloop get-logs --log-type Error` で確認。

- [ ] **Step 4: コミット**

```bash
git add -A moorestech_client/Assets
git commit -m "chore: 孤児 uGUI prefab・Addressable 登録・未参照UIアセットを削除する"
```

---

### Task B5: 監査テストの縮小と asmdef 整理

**Files:**
- Modify: `Client.Tests/WebUi/Gate/WebUiGateClassification.cs`
- Modify: `Client.Tests/WebUi/Gate/WebUiGateAuditTest.cs`（`GatedRootsContainGateToken` は残す。ルールが減るだけ）
- Modify: `Client.Skit/Client.Skit.asmdef`（`"Unity.TextMeshPro"` を references から削除。`SkitUITools.cs` 等が DOTween を使うなら `DOTween.Modules`/`UniTask.DOTween` は残す）

- [ ] **Step 1: 分類ルールを残置対象に合わせる**

`WebUiGateClassification.Rules` を次に置換:
```csharp
        public static readonly IReadOnlyList<Rule> Rules = new List<Rule>
        {
            // --- ゲートルート（ゲート参照必須） / Gated roots (gate reference required)
            new Rule("Client.Game/InGame/BackgroundSkit/BackgroundSkitManager.cs", Category.GatedRoot, "背景スキット（Web文字表示・音声はUnity）"),
            new Rule("Client.Game/Skit/SkitManager.cs", Category.GatedRoot, "通常スキット UI Toolkit 抑止"),

            // --- 基盤 / Infra
            new Rule("Client.Game/InGame/UI", Category.Infra, "状態機械・論理モデル・ゲート本体（uGUIビューは全削除済み: ADR 0052）"),
            new Rule("Client.Game/Skit/Localization", Category.Infra, "通常スキットの辞書読込・合成・解決基盤（画面表示なし）"),
            new Rule("Client.Game/InGame/Tutorial", Category.Infra, "challenge lifecycle・presentation state・interface"),

            // --- 移行対象外 / Excluded
            new Rule("Client.Game/InGame/Mining", Category.Excluded, "採掘FSM（進捗は ProgressBarState→ui.progress）"),
            new Rule("Client.Game/InGame/Tutorial/MapObjectPin.cs", Category.Excluded, "ワールド座標ピンのためUnity残置"),
            new Rule("Client.Game/InGame/Tutorial/VeinPin.cs", Category.Excluded, "鉱脈露頭を指すワールド座標ピンのためUnity残置"),
            new Rule("Client.Game/InGame/Tutorial/BlockPlacePreviewTutorialManager.cs", Category.Excluded, "3D配置previewのためUnity残置"),
            new Rule("Client.Game/InGame/Tutorial/TutorialBlock", Category.Excluded, "3D配置preview配下"),
            new Rule("Client.Game/Skit/SkitWorldObjectControlGroup.cs", Category.Excluded, "ワールド表示物の切替でスクリーンUIを持たない"),
            new Rule("Client.Game/Skit/SkitVisibilityLedger.cs", Category.Excluded, "ワールド表示の復元台帳でスクリーンUIを持たない"),
            new Rule("Client.Skit", Category.CoveredByRoot, "SkitManagerがUI Toolkit rootをWebモード時に抑止"),
            new Rule("Client.CutScene", Category.Excluded, "TimelinePlayerのみ（Canvasは削除済み: ADR 0052）"),
        };
```
`ScanRoots` から `"Client.Game/InGame/Presenter/PauseMenu"` を削除（残る `SaveAndQuitPresenter.cs` は uGUI 非依存だが分類は要るので、削除しない場合は `new Rule("Client.Game/InGame/Presenter/PauseMenu", Category.Infra, "終了経路・切断状態・セーブ要求")` を足す — どちらかに統一。推奨: ルール追加）。`Category.Pending` の enum 値は使用箇所ゼロなら削除。クラスコメントの「Pendingは…」文を削除。

- [ ] **Step 2: テスト**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "WebUiGateAuditTest"`
Expected: 3件 PASS（未分類ファイルが出たら Rules に追加する。`Client.Game/InGame/UI` 配下は Infra 一括で吸収される）

- [ ] **Step 3: asmdef から TMP を外してコンパイル**

`Client.Skit.asmdef` の references から `"Unity.TextMeshPro"` を削除。
Run: `uloop compile --project-path ./moorestech_client` → `errors: 0`（エラーなら TMP を使う残存クラスがあるので元に戻し、そのクラスを本planの範囲外として記録）

- [ ] **Step 4: コミット**

```bash
git add -A
git commit -m "chore: uGUIゲート監査の分類を残置対象へ縮小し Client.Skit の TMP 参照を外す"
```

---

### Task B6: 削除した PlayMode テストのサーバー往復検証を action handler 経由へ移植する

**Files:**
- Create: `Client.Tests/EditModeInPlayingTest/MachineRecipeSelectActionTest.cs`（`MachineRecipeSelectionUITest.RecipeSlotsRenderAndHighlightSelectedRecipe` のうち「選択がサーバーの選択レシピへ届く」部分）
- Create: `Client.Tests/EditModeInPlayingTest/ElectricToGearOutputModeActionTest.cs`（`ElectricToGearModeSelectUITest.RowSelectChangesServerSelectedIndex` の移植）
- Create: `Client.Tests/EditModeInPlayingTest/MachineModuleSlotRoundTripTest.cs`（`MachineModuleSlotUITest.ModuleSlotRenderAndEquipRoundTrip` のうち「統合スロット数 = 入力+出力+モジュール」と「モジュール装着がサーバーへ届く」部分。スロット数は `SubInventoryState.CurrentSubInventory.Count` で検証）

各テストは既存の EditModeInPlayingTest 基盤（`EnterPlayMode` → 起動待ち → `ClientDIContext.DIContainer.DIContainerResolver` から `SubInventoryState`/`UIStateControl` を取得）を使い、ブロック設置後に `uiStateControl.RequestTransition(UIStateEnum.SubInventory)` 相当の経路（`BlockOpenInteractAction` が使う `UITransitContext` 生成）でサブインベントリを開き、`MachineRecipeSelectActionHandler`/`ElectricToGearSetOutputModeActionHandler`/`BlockMoveItemActionHandler` を `ExecuteAsync(JObject)` で叩いてサーバー側コンポーネントの状態を assert する。

- [ ] **Step 1: 既存の Web action テスト前例を確認する**

```bash
grep -rln "MachineRecipeSelectActionHandler\|ElectricToGearSetOutputModeActionHandler\|BlockMoveItemActionHandler" moorestech_client/Assets/Scripts/Client.Tests
```
既に同等の検証があるなら、そのテストに「サブインベントリ経由でスロット数が一致する」assert を1本足すだけに留める。

- [ ] **Step 2: テストを書く（前例のフィクスチャをコピーし、view の `GetComponent<...View>()` を `subInventoryState.CurrentSubInventory` と action handler へ置換）**

各テストの骨子（`MachineModuleSlotRoundTripTest` の例）:
```csharp
        [UnityTest]
        public IEnumerator 統合スロット数が入力出力モジュールの合計と一致しモジュール装着がサーバーへ届く()
        {
            yield return new EnterPlayMode();
            // 既存 MachineModuleSlotUITest と同じ起動待ち・ブロック設置・モジュール定数
            // Same boot wait, block placement and module constants as the deleted MachineModuleSlotUITest
            ...
            var resolver = ClientDIContext.DIContainer.DIContainerResolver;
            var subInventoryState = resolver.Resolve<SubInventoryState>();
            var uiStateControl = resolver.Resolve<UIStateControl>();
            // ブロックUIを開く（BlockOpenInteractAction と同じ遷移）
            // Open the block UI through the same transition BlockOpenInteractAction uses
            ...
            yield return new WaitUntil(() => subInventoryState.CurrentSubInventory != null && subInventoryState.CurrentSubInventory.Count > 0);
            Assert.AreEqual(InputSlotCount + OutputSlotCount + ModuleSlotCount, subInventoryState.CurrentSubInventory.Count, "unified slot count mismatch");
            var handler = new BlockMoveItemActionHandler(resolver.Resolve<LocalPlayerInventoryController>(), subInventoryState);
            var result = handler.ExecuteAsync(new JObject { ["from"] = new JObject { ["area"] = "grab" }, ["to"] = new JObject { ["area"] = "block", ["slot"] = InputSlotCount + OutputSlotCount }, ["count"] = 1 }).GetAwaiter().GetResult();
            Assert.IsTrue(result.Ok);
            // サーバー側モジュールスロットに届いたことを確認
            // Confirm the module reached the server-side module slot
            ...
        }
```
payload の形は `BlockInventoryActions.cs` の `TryParseBlockSlot` / `InventoryAreaMapperTest` に合わせる。

- [ ] **Step 3: 実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "MachineRecipeSelectActionTest|ElectricToGearOutputModeActionTest|MachineModuleSlotRoundTripTest"`（PlayMode遷移テストなので `--test-mode` は既定。Domain Reload エラーは45秒待ってリトライ）
Expected: PASS

- [ ] **Step 4: コミット**

```bash
git add -A
git commit -m "test: uGUI prefab 依存だった PlayMode テストのサーバー往復検証を action handler 経由へ移植する"
```

---

### Task B7: docs 更新・最終検証・PR

**Files:**
- Modify: `docs/webui/ugui-retirement-plan.md`（「スコープ外」節を ADR 0052 の例外4種＋CutScene残部で置換。Phase 2/3/4 を「完了（PR1 #xxxx / PR2 #yyyy）」に）
- Modify: `docs/webui/disposition.md`（SYS-2 等の「除外: 旧D3維持」を ADR 0052 参照へ。`CutScene` 行を「Canvas削除・TimelinePlayer残置」に）
- Modify: `docs/webui/TODO.md`（uGUI退役の項目をクローズ）
- Modify: `.agents/skills/moores-code-review/references/lens-digest.md` は変更不要（uGUI関連レンズがあれば「削除済み」注記）

- [ ] **Step 1: docs を更新しコミット**

```bash
git add -A docs
git commit -m "docs: uGUI退役計画をADR 0052の例外で上書きしPhase 2〜4の完了を記録する"
```

- [ ] **Step 2: 最終検証**

```bash
grep -rl "uGUI廃止Phase1" moorestech_client/Assets/Scripts | wc -l   # → 0
grep -rlE "using UnityEngine\.UI;|using TMPro" --include='*.cs' moorestech_client/Assets/Scripts | sort
```
Expected: 後者が `Client.MainMenu/*`、`Client.Starter/InitializeScenePipeline.cs`、`Client.Starter/Initialization/LoadingProgressLog.cs`、`Client.Localization/TextMeshProLocalize.cs`、`Client.Game/InGame/Map/MapObject/MapObjectHpBarView.cs`、`Client.Game/InGame/Train/Debug/TrainUnitDebugOverlayPresenter.cs`、`Client.DebugSystem/ItemSelectModal.cs`、`Client.DebugSystem/ItemSlot/*`、`Client.Playtest/WebUi/CefScreenMapper.cs`、`Client.Tests/Map/MapObjectHpBarScaleTest.cs` のみ。
Run: `uloop compile --project-path ./moorestech_client` → `errors: 0`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests"` → 全件 PASS
PlayMode スモーク: Task A9 Step 3 と同じシナリオ＋「ポーズメニューでセーブ」「列車インベントリを開く（列車がある地図なら）」。
Release ビルド確認（任意だが推奨）: `daily-build-repair` スキルのビルド手順で Mac ビルドが通ることを確認（Addressable の欠落アドレスはビルド時に露見する）。

- [ ] **Step 3: PR 作成**

pr-create スキルで PR（タイトル「uGUI退役 PR2: 移行済み画面uGUIの残骸・prefab・アセットの一括削除」、ADR 0052・本plan・PR1 へのリンク、bd `moorestech-lnsf.2`）。

- [ ] **Step 4: 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

moores-code-review スキルを起動し、指摘の機械的修正を適用してから push。完了後 `bd close moorestech-lnsf.2` と `bd close moorestech-lnsf`（epic）。`moores-wt rm feature/ugui-removal-delete`。

---

## Self-Review 記録

- Requirements coverage: R1→B1/B3/B4、R2→B1（削除対象から除外）/B2/B3（EventSystem・CefUnity 残置）、R3→B3 Step3・B1（SelectionButton は B4、BackgroundSkitUI は A8）、R4→A1〜A8、R5→A2/A4/A5/A6/A7/A8（Topic/Action の型差し替えのみ、DTO不変）、R6→A3/A5/A6/A7/B1/B6、R7→Part A/B の分割、R8→A9/B2/B3/B4、R9→B5、R10→B7。
- Placeholder scan: 「TBD/後で」無し。各ステップにコードまたは具体コマンド。B6 のテスト骨子は「…」で既存フィクスチャの流用箇所を示しているが、流用元（削除前の同名テスト）は git 履歴 `git show origin/master:moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/MachineModuleSlotUITest.cs` で参照可能と明記した。
- Type consistency: `SubInventoryModel.SetItems/SetItem/SetTrainMessage`（A1）→ A2 で使用。`BuildMenuSelection.SetSelectedTarget/TryConsumeSelectedTarget/Clear`（A4）→ A4 Action/State/テスト。`ProgressBarState`（A5）→ A6 のテストで `_progressBar`。`MiningControllerContext(equipment, progressBar, tooltip)` は A5 で2引数→A6 で3引数に増える順序を明記。`IMouseCursorTooltip` は A6 で別ファイルへ移動。

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置 | 機構 | 前例 | 判定 |
|---|---|---|---|---|---|
| 1 | `SubInventoryModel` | Client.Game / `UI/Inventory`（`ISubInventory` と同居） | 純C#、`ISubInventory` 実装 | `LocalPlayerInventory`（同ディレクトリの純モデル） | ok |
| 2 | `ISubInventorySource.CreateModel` | Client.Game / `UIState/State/SubInventory` | ソースがモデルを組む | 旧 `ExecuteInitialize` と同じ責務位置 | ok（置換対象の駆動方式を維持） |
| 3 | `BuildMenuSelection` | Client.Game / `UI/BuildMenu` | 1回消費キュー、DI Singleton | 旧 `BuildMenuView._clickedEntry` の一方通行フロー | ok |
| 4 | `ProgressBarState` / `CrosshairVisibility` / `BlueprintNameInputState` / `NetworkDisconnectState` / `MouseCursorTooltipState` | Client.Game 各UIサブディレクトリ | UniRx `Subject`/`ReactiveProperty`、DI Singleton | `LocalPlayerEquipment`（純モデル+`OnSlotsOrSelectionChanged`）、`ClientHotbarDatastore` | ok |
| 5 | `NetworkDisconnectState : IInitializable` を `RegisterEntryPoint().AsSelf()` | Client.Starter Registration | VContainer EntryPoint | `NetworkEventInventoryUpdater`、`PlayerPositionSender().AsSelf()` | ok |
| 6 | 採掘FSMへの `ProgressBar`/`Tooltip` 注入 | `MiningControllerContext` | 状態が `context` 経由で読む | 既存 `context.LocalPlayerEquipment` | ok |
| 7 | `GameObjectToolTipTargetController` の `[Inject]` | MonoBehaviour を `RegisterComponent` | VContainer `[Inject]` | `BuildMenuView` の `[Inject]`（RegisterComponent 済み Mono） | ok |
| 8 | `GameSaveRequester` | Client.Game / `Presenter/PauseMenu` | 純C#、DI | `IBlueprintDeleteService` を Action が使う形 | ok |
| 9 | `UIRoot` の `RegisterComponent` | MainGameStarter | Mono 登録 | `uIStateControl` | ok |
| 10 | `ItemSlotView` を `Client.DebugSystem/ItemSlot` へ移動 | Assembly-CSharp | ファイル移動（GUID維持） | ADR 0052 agent前提3 | 新規パターン（デバッグ専用へ降格） |
| 11 | `WebUiScreenGate` は残す | Client.Game | 静的ゲート | 世界空間ピン等の参照が残る | 注目点（分岐簡約は後続タスク） |

データフロー（サブインベントリ）: `BlockOpenInteractAction → UITransitContext(ISubInventorySource) → SubInventoryState.OnEnter → [SubInventoryModel] → LocalPlayerInventoryController / BlockInventoryTopic / BlockInventoryActions`。新規コンポーネントは「書き手」（`SubInventoryModel` への書き手は `SubInventoryState` のみ）。交差点なし。

死活表（機能パリティ）:
| 操作 | 計画後 | 根拠 |
|---|---|---|
| ブロック/列車インベントリを開く・アイテム移動 | 生きる | `SubInventoryModel` が同じ `ISubInventory` 契約で `LocalPlayerInventoryController` に載る |
| ビルドメニューで選択→設置 | 生きる | `BuildMenuSelection` を `BuildMenuState` が消費 |
| 採掘の進捗バー | 生きる | `ProgressBarState` → `ProgressTopic` |
| カーソルツールチップ（採掘/タップ/設置失敗/削除拒否/世界オブジェクト） | 生きる | `MouseCursorTooltipState` → `TooltipTopic` |
| 一人称クロスヘア | 生きる | `CrosshairVisibility` → `CrosshairTopic` |
| Ctrl+U で UI 非表示 | 生きる | `UIRoot` 維持 → `UiVisibilityTopic` |
| BP名入力（Webモーダル） | 生きる | `BlueprintNameInputState` → `BlueprintNameInputWebBridge` |
| ポーズメニューのセーブ / セーブして終了 / 切断表示 | 生きる | `GameSaveRequester` / `SaveAndQuitPresenter`（移設） / `NetworkDisconnectState` |
| 背景スキットの音声 | 生きる | `BackgroundSkitVoicePlayer` |
| 背景スキットの文字（Web） | 生きる | `SkitPresentationStateStore.SetBackgroundText` は不変 |
| チュートリアル誘導（キーヒント・ハイライト・ドラッグ誘導・ワールドピン） | 生きる | `TutorialUI` 配下マネージャは残置、uGUI非依存 |
| デバッグシート・アイテム選択モーダル | 生きる | `ItemSlotView.prefab`/`EventSystem` 残置 |
| 切断時の「メインメニューへ戻る」uGUIボタン | 消える（Web側にも無い） | Phase1 で描画停止済みの経路。ADR「起動後メインメニューへ戻らない」設計と整合 |
| 非CEF環境でのプレイテストDSLのビルドメニュークリック | 消える | CEF 恒久ON（Phase1裁定）。`CefScreenMapper.IsWebUiAvailable()` false の環境は想定外 |

## 判断記録（ADR）

- 設計裁定: `docs/adr/0052-ugui-removal-scope-and-exceptions.md`、`.decisions/2026-09-05-uGUIはパッケージごと完全撤去する.md`、`.decisions/2026-09-05-uGUI撤去の唯一の例外はCEF描画面.md`、`.decisions/2026-09-05-メインメニューとロード画面はuGUI現状維持.md`、`.decisions/2026-09-05-mapObjectのHPバーはuGUI現状維持.md`、`.decisions/2026-09-05-CutSceneはuGUI-Canvasだけ消しTimelinePlayerは残す.md`、`.decisions/2026-09-05-デバッグUIはuGUI現状維持.md`、`.decisions/2026-09-05-uGUIビューの論理状態はUnity側の純ロジッククラスへ抽出する.md`、`.decisions/2026-09-05-uGUI撤去は抽出PRと削除PRの2本に分ける.md`
- planning 中の判断（すべて agent前提。ユーザー裁定ではない）:
  1. サブインベントリのスロット数はサーバー応答 `Items.Count` から決める（旧実装は prefab のスロット生成数）。根拠: `InventoryRequestProtocol` が全スロットを返す。取得失敗時は Count=0（旧実装は prefab スロット数のまま空データという不整合だった）
  2. `BuildMenuSelection` は `BuildMenuEntry` でなく `IPlacementTarget` を運ぶ（アイコン・ツールチップ文字列は Web 側が持つため）。`BuildMenuEntry`/`BuildMenuEntryCatalog` は PR2 で削除
  3. `MouseCursorTooltip.Instance` 等の静的所有は DI へ置換（裁定）。採掘FSMは `new` で遷移するため `MiningControllerContext` 経由で配る。`GameObjectTooltipTarget`（世界オブジェクト）は `GameObjectToolTipTargetController` を `RegisterComponent` して `[Inject]` で受ける
  4. `NetworkDisconnectPresenter` の「メインメニューへ戻る」ボタン経路は Web 側にも存在せず Phase1 で描画停止済みのため削除（死活表参照）
  5. `ItemSlotView`/`CommonSlotView` はデバッグ用に `Client.DebugSystem/ItemSlot` へ移し、ツールチップ（`UGuiTooltipTarget`）・液体・テキスト表示は落とす。`ModAssetLoader.PreloadCriticalAssetsAsync` のハング回避事前ロードは対象アセットが消えるため削除
  6. `WebUiScreenGate` は削除しない（`IsWebUiMode` 恒久 true のまま）。世界空間ピン・`ChainPlacementPreviewPart`・`SkitManager` 等の残置ファイルが参照する分岐の簡約は後続 bd タスクへ
  7. `UIStateControl` の webモード両エッジ正規化コードは触らない（恒久 true で不活性。削除は後続）
  8. マスタ `blockUIAddressablesPath` は `IsBlockOpenable()` の判定材料としてそのまま使う（値が指す prefab は消えるが文字列の有無だけを見る）。`openable: bool` への改名は master repo を跨ぐため後続 bd タスク `moorestech-lnsf` 配下に積む
  9. `Client.Game.asmdef` の `Unity.TextMeshPro` 参照は `MapObjectHpBarView`/`TrainUnitDebugOverlayPresenter` が使うため残す。`Client.Skit.asmdef` からは外す
  10. 削除する PlayMode テスト6件のうち「サーバー往復」の検証は action handler 経由で移植し、「uGUI描画・ハイライト」の検証は移植しない（描画主体が Web に移っており、Web 側は vitest/e2e が担う）
