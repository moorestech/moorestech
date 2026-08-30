# Fキー「インタラクト」統合 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** ワールド内の「機械を開く・列車に乗る/開く・mapObject採掘・鉱脈手掘り」を単一の「インタラクト（F。乗車のみE）」概念に統合し、常に1件のインタラクト対象をアウトラインでハイライトして、カーソルツールチップに「[F] ○○を開く」等の操作ヒントを出す。

**Architecture:** 対象側が `IInteractable`（ハイライトON/OFF・可用性）を実装し、単押し系は `ITapInteractable`（アクション列: キー＋ヒント＋実行）、長押し系は既存の `IMiningTargetObject`（採掘FSM）として振る舞う。`GameScreenState` から毎フレーム `InteractController.ManualUpdate()` を駆動し、`InteractTargetSelector`（照準レイ優先→半径2m内で視線角度最小）で1件を選び、ハイライト差分・ツールチップ・F/E入力を一箇所で処理する。採掘FSM（`Mining*State`）は長押し実行の内側にそのまま残り、入力だけ左クリック→Fに差し替える。アウトラインは既存のステンシル方式（`Outline.mat`＋URP OutlinePass）を実行時にレンダラー複製で付与する。

**Tech Stack:** Unity C#（VContainer, UniRx, InputSystem）、localization.csv → Mooresmaster生成 `LocalizationKeys`（force-recompile必要）、webui `npm run gen:i18n`、moorestech_master（challenges.json / localization.csv）。

## Requirements

- R1. `Playable/Interact`（`<Keyboard>/f`）と `Playable/Ride`（`<Keyboard>/e`）を `.inputactions` に追加し、`InputManager.Playable.Interact` / `.Ride` で読める。`KeyCode.E` 直書き（`RideVehicleInputService`）は削除する（受け入れ: `grep -rn "KeyCode.E" moorestech_client/Assets/Scripts` が0件）
- R2. `IInteractable` を新設し、開けるブロック・列車車両・mapObject・露頭が実装する。ブロックは `IsBlockOpenable()` のものだけが対象（受け入れ: ベルトコンベアは選定されずハイライトもされない）
- R3. `InteractController` は `GameScreenState.GetNextUpdate()` から毎フレーム駆動され、`OnExit` で `Disable()` される。他UIステート中は選定・ハイライト・tooltipが一切出ない
- R4. 対象選定: ①照準レイ（Block＋MapObjectレイヤ、設置ゴースト貫通）の最前面ヒットが解決でき、かつプレイヤー距離≤2mならそれ ②無ければ半径2mのOverlapSphereで解決した候補のうちカメラ前方との角度が最小（同角度なら距離最小） ③無ければnull。全種別で2m共通（旧: 採掘1.5m/2.5m・乗車3m・ブロック100m）
- R5. ハイライトは選ばれた1件のみ。mapObjectは既存の焼き込み `outlineObject`、ブロック・列車・露頭は初回ハイライト時に `RuntimeOutlineFactory` が `Outline` レイヤの複製メッシュ子を生成してON/OFFする
- R6. tooltip: ブロック「[F] {ブロック名}を開く」、列車「[F] 車両インベントリを開く」＋「[E] 乗車」の2行、mapObject/露頭は既存採掘tooltipの左クリック文言をF表記に（`holdToGet`/`namedMineHold`/`namedMineClick` 文言変更、`pickUpLeftClick`→`pickUpInteract` 改名）。左下キーヒントHUDにはF/Eを載せない（ADR-0032維持）
- R7. F単押し: ブロック→`SubInventory`遷移、列車→`SubInventory`遷移、PickUp種mapObject→取得。E単押し: 列車→`TrainHUDScreen`遷移。F長押し: 採掘FSM進行（`MiningProgressState` の進捗・装備切替・対象変更の各挙動は現状維持）
- R8. `MiningController`(MonoBehaviour) / `GameScreenSubInventoryInteractService` / `RideVehicleInputService` を削除し、GameSystem.prefab から `MiningController` コンポーネントを外す（uloop経由）
- R9. 既存テスト（`MiningFocusState*`・`MiningEquipmentSwitchTest`・`MiningAimTest`・`OutcropMiningAimTest`・`MiningTargetFocusContextTest`・`UIStateCameraInteractionTest`・`UIStateFocusRestorationTest`）を新構造へ移し、選定規則・tap実行・ハイライト差分に新規テストを足す
- R10. moorestech_master の「左クリックで拾う」3箇所（challenges.json summary/pinText、localization.csv 2行）を「Fで拾う」に変え、別PR＋`.moorestech-external-revisions.json` のピン更新
- R11. プレイテストDSLに `PressInteract()` / `HoldInteract(seconds)` / `PressRide()` を追加し、スキル参照表に載せる
- やらないこと: 左下キーヒントHUDへのF/E追加、建築/破壊モード中のインタラクト、露頭以外のHPバー追加、TrainHUD側の車両インベントリ導線

## Global Constraints

- 作業は `moores-wt new feature/interact-key-unification` で切った新規worktreeで行い、PR作成直後に `moores-wt rm` で畳む
- 1ファイル200行以下・1ディレクトリ10ファイル以下・partial禁止・`Func<>`禁止・`event Action`禁止（UniRx）・try-catch禁止・デフォルト引数禁止・`#region Internal` はメソッド内ローカル関数のみ
- コメントは日本語→英語の2行セット（各1行）
- .cs変更後は必ず `uloop compile --project-path ./moorestech_client`。localization.csv変更後は `--force-recompile` と webui `npm run gen:i18n`
- `.meta` 手動作成禁止（Unityが生成したものをコミット）。Prefab/シーンの手編集禁止（`uloop execute-dynamic-code` 経由のみ）
- テストは `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "..."`（既定はPlayModeなので `--test-mode EditMode` 必須）
- ADR: `docs/adr/0046-interact-key-unifies-open-ride-and-mining.md`。裁定: `.decisions/2026-08-30-インタラクト*.md`, `.decisions/2026-08-30-列車はFで車両インベントリを開き*.md`
- bd: `moorestech-3cbt`（着手時 `bd update moorestech-3cbt --claim`）

---

## File Structure

新規（`moorestech_client/Assets/Scripts/Client.Game/InGame/Interact/`、7ファイル）:
- `IInteractable.cs` — 対象の共通契約（GameObject・可用性・ハイライト）
- `IInteractRayTarget.cs` — コライダ→対象の案内マーカー（旧 `IMiningRayTarget` の置換）
- `ITapInteractAction.cs` — 単押しアクション（キー・ヒント・実行）
- `ITapInteractable.cs` — 単押し対象（アクション列）
- `InteractableResolver.cs` — Collider から IInteractable を解決
- `InteractTargetSelector.cs` — 照準優先→近傍角度最小の1件選定
- `TapInteractionDriver.cs` — 単押し対象のtooltip表示とキー実行
- `InteractController.cs` — GameScreenState駆動の司令塔（選定・ハイライト・tap/mining振り分け）
- `Outline/RuntimeOutlineFactory.cs` — 実行時アウトライン生成

新規（対象側）:
- `Client.Game/InGame/Block/BlockInteractable.cs` — 開けるブロックの `ITapInteractable` コンポーネント
- `Client.Game/InGame/Block/BlockOpenInteractAction.cs`
- `Client.Game/InGame/Train/View/Object/Core/TrainCarInteractable.cs` — 列車の `ITapInteractable`（開く＋乗車）
- `Client.Game/InGame/Train/View/Object/Core/TrainCarInteractActions.cs` — 開く/乗車の2アクション

変更: `Mining/IMiningTargetObject.cs`（`IInteractable` 継承・`SetFocused`→`SetHighlighted`）、`Mining/MiningControllerContext.cs`（ハイライト呼び出し撤去）、`Mining/MiningFocusState.cs`・`MiningProgressState.cs`（入力をInteractへ）、`MapObject/MapObjectGameObject.cs`・`MapObject/MapObjectRayTarget.cs`、`Outcrop/OutcropGameObject.cs`・`Outcrop/OutcropRayTarget.cs`、`Block/BlockGameObject.cs`（`BlockInteractable` 付与）、`TrainCarObjectFactory.cs`、`UIState/State/GameScreenState.cs`、`Client.Starter/Registration/MainGameInteractionRegistration.cs`、`Client.Starter/MainGameStarter.cs`、`Client.Input/InputManager.cs`、`Asset/Common/moorestechInputSettings.inputactions`、`Client.Common/MaterialConst.cs`、`Editor/MapObjectWrapperGenerator/WrapperPrefabFactory.cs`、`Localization/localization.csv`、`Client.Playtest/Operations/PlaytestInteractOps.cs`（新規）

削除: `Mining/MiningController.cs`、`Mining/IMiningRayTarget.cs`、`UIState/State/SubInventory/GameScreenSubInventoryInteractService.cs`、`Train/Unit/RideVehicleInputService.cs`、`Client.Tests/Mining/MiningAimTest.cs`

---

### Task 1: InputSystem に Interact(F) / Ride(E) を追加する

**Files:**
- Modify: `moorestech_client/Assets/Asset/Common/moorestechInputSettings.inputactions`（Playableマップ）
- Modify: `moorestech_client/Assets/Scripts/Client.Input/InputManager.cs:76-89`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Input/PlayableInteractInputTest.cs`（新規）

**Interfaces:**
- Produces: `InputManager.Playable.Interact : InputKey`（`GetKeyDown` / `GetKey`）、`InputManager.Playable.Ride : InputKey`

- [x] **Step 1: 失敗するテストを書く**

```csharp
using Client.Input;
using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Client.Tests.Input
{
    public class PlayableInteractInputTest : InputTestFixture
    {
        [Test]
        public void FキーがInteractとして読める()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var interact = InputManager.Playable.Interact;
            InputSystem.Update();
            Press(keyboard.fKey);
            InputSystem.Update();
            Assert.IsTrue(interact.GetKey);
            Assert.IsTrue(interact.GetKeyDown);
        }

        [Test]
        public void EキーがRideとして読める()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var ride = InputManager.Playable.Ride;
            InputSystem.Update();
            Press(keyboard.eKey);
            InputSystem.Update();
            Assert.IsTrue(ride.GetKeyDown);
        }
    }
}
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `'PlayableInputManager' does not contain a definition for 'Interact'` のコンパイルエラー

- [x] **Step 3: .inputactions にアクションとバインドを追加する**

`moorestechInputSettings.inputactions` の `Playable` マップ `actions` 配列末尾に2要素、`bindings` 配列末尾に2要素を追加する（idは `uuidgen | tr A-Z a-z` で新規発行。既存idの複製禁止）:

```json
{
 "name": "Interact",
 "type": "Button",
 "id": "<新規uuid-1>",
 "expectedControlType": "Button",
 "processors": "",
 "interactions": "",
 "initialStateCheck": true
},
{
 "name": "Ride",
 "type": "Button",
 "id": "<新規uuid-2>",
 "expectedControlType": "Button",
 "processors": "",
 "interactions": "",
 "initialStateCheck": true
}
```

```json
{
 "name": "",
 "id": "<新規uuid-3>",
 "path": "<Keyboard>/f",
 "interactions": "",
 "processors": "",
 "groups": "KeyboardMouse",
 "action": "Interact",
 "isComposite": false,
 "isPartOfComposite": false
},
{
 "name": "",
 "id": "<新規uuid-4>",
 "path": "<Keyboard>/e",
 "interactions": "",
 "processors": "",
 "groups": "KeyboardMouse",
 "action": "Ride",
 "isComposite": false,
 "isPartOfComposite": false
}
```

`.inputactions.meta` は `generateWrapperCode: 1` なので、Unityの再インポートで `Client.Input/moorestechInputSettings.cs` が自動再生成される（手編集しない）。再生成されない場合は `uloop execute-dynamic-code` で `UnityEditor.AssetDatabase.ImportAsset("Assets/Asset/Common/moorestechInputSettings.inputactions", UnityEditor.ImportAssetOptions.ForceUpdate);` を実行する。

- [x] **Step 4: InputManager に公開する**

`PlayableInputManager` を次に置き換える:

```csharp
    public class PlayableInputManager
    {
        public readonly InputKey BlockPlaceRotation;
        public readonly InputKey ClickPosition;
        public readonly InputKey ScreenLeftClick;
        public readonly InputKey ScreenRightClick;
        public readonly InputKey Interact;
        public readonly InputKey Ride;

        public PlayableInputManager(MoorestechInputSettings settings)
        {
            ScreenLeftClick = new InputKey(settings.Playable.ScreenLeftClick);
            ScreenRightClick = new InputKey(settings.Playable.ScreenRightClick);
            ClickPosition = new InputKey(settings.Playable.ClickPosition);
            BlockPlaceRotation = new InputKey(settings.Playable.BlockPlaceRotation, InputSuppressionScope.Keyboard);

            // Web UIのテキスト入力中に世界へ漏れないようキーボード抑止スコープに入れる
            // Keep both under the keyboard suppression scope so Web UI text input never leaks into the world
            Interact = new InputKey(settings.Playable.Interact, InputSuppressionScope.Keyboard);
            Ride = new InputKey(settings.Playable.Ride, InputSuppressionScope.Keyboard);
        }
    }
```

- [x] **Step 5: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → エラー0
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlayableInteractInputTest"`
Expected: 2件 PASS

- [x] **Step 6: コミット**

```bash
git add moorestech_client/Assets/Asset/Common/moorestechInputSettings.inputactions moorestech_client/Assets/Scripts/Client.Input/ moorestech_client/Assets/Scripts/Client.Tests/Input/
git commit -m "feat: InputSystemにInteract(F)とRide(E)アクションを追加"
```

---

### Task 2: IInteractable 契約と採掘対象の載せ替え

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Interact/IInteractable.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Interact/IInteractRayTarget.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/IMiningRayTarget.cs`（＋.meta）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/IMiningTargetObject.cs:37-54`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObject.cs:145-149`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectRayTarget.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGameObject.cs:99-103`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropRayTarget.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningControllerContext.cs:36-54`
- Modify（テスト追従）: `Client.Tests/Mining/MiningFocusStateTestFixture.cs`（`OutcomeStubMiningTarget`）、`Client.Tests/Mining/MiningTargetFocusContextTest.cs`（`FocusTrackingMiningTarget`）、`Client.Tests/Mining/MiningEquipmentSwitchTest.cs`（`AttackTrackingMiningTarget`）、`Client.Tests/Map/MapObjectRayTargetTest.cs`（`IMiningRayTarget`参照）

**Interfaces:**
- Produces:
  ```csharp
  public interface IInteractable { GameObject GameObject { get; } bool IsInteractAvailable { get; } void SetHighlighted(bool highlighted); }
  public interface IInteractRayTarget { IInteractable Interactable { get; } }
  public interface IMiningTargetObject : IInteractable { /* 既存メンバー。SetFocused は削除 */ }
  ```
- Consumes: なし

- [x] **Step 1: 契約ファイルを書く**

`Interact/IInteractable.cs`:
```csharp
using UnityEngine;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     Fキーで働きかけられる世界の物の共通契約。駆動側は種別を知らない
    ///     Shared contract for anything the interact key can act on; the driver never learns the concrete kind
    /// </summary>
    public interface IInteractable
    {
        GameObject GameObject { get; }

        // 破壊済み・マスタ欠損・開けないブロック等は候補にならない
        // Destroyed, master-less or non-openable things never become candidates
        bool IsInteractAvailable { get; }

        void SetHighlighted(bool highlighted);
    }
}
```

`Interact/IInteractRayTarget.cs`:
```csharp
namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     レイが当たるコライダに付ける、インタラクト対象への案内
    ///     Marker on a collider hit by the ray that points at its interactable
    /// </summary>
    public interface IInteractRayTarget
    {
        IInteractable Interactable { get; }
    }
}
```

- [x] **Step 2: IMiningTargetObject を IInteractable 継承に変える**

`IMiningTargetObject.cs` の interface 宣言を次に置換（`GameObject` と `SetFocused` を削除、`using Client.Game.InGame.Interact;` 追加）:

```csharp
    public interface IMiningTargetObject : IInteractable
    {
        SoundEffectType DestroySoundType { get; }

        // tooltipに出す取得物の識別
        // Identifies what this target yields for the tooltip
        IReadOnlyList<Guid> EarnItemGuids { get; }

        // 可否・種別・ツール解決を1回の問い合わせへ畳み、成立しない組み合わせを呼び出し側に作らせない
        // Fold availability, kind and tool resolution into one query so callers cannot build impossible combinations
        MiningStartOutcome TryBeginHandMining(ItemId equippedItemId, out MiningToolCandidate tool, out List<ItemId> recommendedToolItemIds);

        // ダメージ算出はサーバ権威のため、打撃対象だけを送る
        // Damage is computed by the server authority, so only the struck target is sent
        void SendAttack();
    }
```

- [x] **Step 3: MapObjectGameObject / MapObjectRayTarget を追従させる**

`MapObjectGameObject.cs`: `SetFocused` を `SetHighlighted` にリネームし、`IsInteractAvailable` を追加:
```csharp
        // 採掘の可用性と同じ条件で候補になる
        // Becomes a candidate under the same condition as mining availability
        public bool IsInteractAvailable => IsAvailable;

        public void SetHighlighted(bool highlighted)
        {
            if (outlineObject) outlineObject.SetActive(highlighted);
            if (hpBarView) hpBarView.SetActive(highlighted);
        }
```

`MapObjectRayTarget.cs`:
```csharp
using Client.Game.InGame.Interact;
using UnityEngine;

namespace Client.Game.InGame.Map.MapObject
{
    public class MapObjectRayTarget : MonoBehaviour, IInteractRayTarget
    {
        public MapObjectGameObject MapObjectGameObject { get; private set; }

        public IInteractable Interactable => MapObjectGameObject;

        public void Initialize(MapObjectGameObject mapObjectGameObject)
        {
            MapObjectGameObject = mapObjectGameObject;
        }
    }
}
```

- [x] **Step 4: OutcropGameObject / OutcropRayTarget を追従させる**

`OutcropGameObject.cs`: `SetFocused` を削除し、以下を追加（アウトライン生成はTask 4で `RuntimeOutlineFactory` を接続するまで空のままにせず、この時点では未実装のため `_outlineObject` フィールドとON/OFFだけ書く）:
```csharp
        private GameObject _outlineObject;

        // 露頭は無限資源で消えないので常に候補
        // An outcrop never disappears, so it is always a candidate
        public bool IsInteractAvailable => true;

        public void SetHighlighted(bool highlighted)
        {
            // 初回ハイライト時に複製メッシュを作る（Task 4でRuntimeOutlineFactoryを接続）
            // Build the duplicate mesh on first highlight (RuntimeOutlineFactory is wired in Task 4)
            if (highlighted && _outlineObject == null) _outlineObject = RuntimeOutlineFactory.Create(gameObject);
            if (_outlineObject != null) _outlineObject.SetActive(highlighted);
        }
```
※ Task 4 完了までコンパイルを通すため、本タスクでは `RuntimeOutlineFactory` を先に最小実装する（Task 4 の Step 3 のコードをそのまま先行作成してよい。Task 4 はテストと `MaterialConst`/Resources 移設を担う）。

`OutcropRayTarget.cs`:
```csharp
using Client.Game.InGame.Interact;
using UnityEngine;

namespace Client.Game.InGame.Map.Outcrop
{
    /// <summary>
    ///     露頭コライダに付与するレイキャスト用マーカー
    ///     Raycast marker attached to outcrop colliders
    /// </summary>
    public class OutcropRayTarget : MonoBehaviour, IInteractRayTarget
    {
        public OutcropGameObject OutcropGameObject { get; private set; }

        public IInteractable Interactable => OutcropGameObject;

        public void Initialize(OutcropGameObject outcropGameObject)
        {
            OutcropGameObject = outcropGameObject;
        }
    }
}
```

- [x] **Step 5: MiningControllerContext からハイライト呼び出しを外す**

ハイライトは `InteractController` が全種別に対して一箇所で行うため、`SetFocusTarget` を次に置換:
```csharp
        public void SetFocusTarget(IMiningTargetObject target)
        {
            // 同一対象なら再解決も要らない
            // The same target needs no re-resolution
            if (ReferenceEquals(CurrentFocusTarget, target)) return;

            CurrentFocusTarget = target;
            ResolveEarnItemNames();
        }
```

- [x] **Step 6: テストのスタブを追従させる**

- `MiningFocusStateTestFixture.OutcomeStubMiningTarget`: `SetFocused` → `SetHighlighted(bool highlighted) { }`、`public bool IsInteractAvailable => true;` を追加
- `MiningEquipmentSwitchTest.AttackTrackingMiningTarget`・`MiningTargetFocusContextTest.FocusTrackingMiningTarget`: 同様に追従。`FocusTrackingMiningTarget` は `SetHighlighted` で `FocusEnabledCount/FocusDisabledCount` を数える形に置換
- `MiningTargetFocusContextTest.SetFocusTargetPushesOnlyWhenTargetChanges` は「ハイライト通知」の検証を削除し、`CurrentFocusTarget` の差し替えと `同一対象なら再解決しない` だけを残す（ハイライト差分の検証は Task 6 の `InteractControllerHighlightTest` へ移す）:
```csharp
        [Test]
        public void SetFocusTargetは同一対象を再設定しない()
        {
            var context = new MiningControllerContext(null);
            var sharedGameObject = new GameObject("SharedTarget");
            var firstTarget = new FocusTrackingMiningTarget("first", sharedGameObject, new List<string>(), Array.Empty<Guid>());
            var secondTarget = new FocusTrackingMiningTarget("second", new GameObject("Second"), new List<string>(), Array.Empty<Guid>());
            context.SetFocusTarget(firstTarget);
            context.SetFocusTarget(firstTarget);
            Assert.AreSame(firstTarget, context.CurrentFocusTarget);
            context.SetFocusTarget(secondTarget);
            Assert.AreSame(secondTarget, context.CurrentFocusTarget);
            context.SetFocusTarget(null);
            Assert.IsNull(context.CurrentFocusTarget);
            UnityEngine.Object.DestroyImmediate(sharedGameObject);
            UnityEngine.Object.DestroyImmediate(secondTarget.GameObject);
        }
```
- `MapObjectRayTargetTest.cs`: `IMiningRayTarget` を `IInteractRayTarget`、`.MiningTargetObject` を `.Interactable` に置換

- [x] **Step 7: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → `MiningController.cs` の `rayTarget.MiningTargetObject` 参照エラーが出るので、その1行を `rayTarget.Interactable as IMiningTargetObject` に暫定置換（Task 6 で削除される）→ エラー0
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Mining(Focus|TargetFocus|Equipment)"`
Expected: すべて PASS

- [x] **Step 8: コミット**

```bash
git add -A moorestech_client/Assets/Scripts/Client.Game/InGame/Interact moorestech_client/Assets/Scripts/Client.Game/InGame/Mining moorestech_client/Assets/Scripts/Client.Game/InGame/Map moorestech_client/Assets/Scripts/Client.Tests/Mining moorestech_client/Assets/Scripts/Client.Tests/Map
git commit -m "refactor: IInteractable契約を新設し採掘対象を載せ替える"
```

---

### Task 3: 採掘FSMの入力をFへ差し替え、文言をF表記にする

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningFocusState.cs:44,60,67`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningProgressState.cs:56`
- Modify: `Localization/localization.csv:14,162,257,258`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Mining/MiningEquipmentSwitchTest.cs:171-183`（`PressLeftClick`）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Mining/MiningFocusStateTestFixture.cs:86`（左クリック前提のAssert）
- Test: `Client.Tests/Mining/MiningFocusStateTooltipTest.cs`等、`pickUpLeftClick` キー名を参照しているテストを `grep -rn "PickUpLeftClick" moorestech_client/Assets/Scripts/Client.Tests` で洗い出して `PickUpInteract` に置換

**Interfaces:**
- Consumes: `InputManager.Playable.Interact`（Task 1）
- Produces: `LocalizationKeys.Ui.Tooltip.PickUpInteract`（改名）

- [x] **Step 1: CSV を書き換える**

```csv
ui.tooltip.holdToGet,Hold F to get,Hold F to get,F長押しで取得する,F gedrückt halten zum Aufnehmen
ui.tooltip.pickUpInteract,Press F to pick up,Press F to pick up,Fで取得,F drücken zum Aufnehmen
ui.tooltip.namedMineHold,{p0} : Hold F to mine,{p0} : Hold F to mine,{p0} : F長押しで採掘,{p0} : Zum Abbauen F gedrückt halten
ui.tooltip.namedMineClick,{p0} : Press F to mine,{p0} : Press F to mine,{p0} : Fで採掘,{p0} : F drücken zum Abbauen
```
（14行目・162行目・257行目・258行目を上記でそれぞれ置換。`pickUpLeftClick` は改名）

- [x] **Step 2: 生成物を更新する**

Run: `cd moorestech_web/webui && npm run gen:i18n && cd -`
Run: `uloop compile --project-path ./moorestech_client --force-recompile`
Expected: `LocalizationKeys.Ui.Tooltip.PickUpLeftClick` 参照箇所（`MiningFocusState.cs:67`）で CS0117

- [x] **Step 3: 採掘ステートの入力とキーを差し替える**

`MiningFocusState.cs`:
- 44行目 `if (!InputManager.Playable.ScreenLeftClick.GetKey)` → `if (!InputManager.Playable.Interact.GetKey)`（直前コメントを「// Fが押されていない場合はフォーカスを維持 / // Keep focus while F is not held」に）
- 60行目 `if (InputManager.Playable.ScreenLeftClick.GetKeyDown)` → `if (InputManager.Playable.Interact.GetKeyDown)`
- 65-67行目のコメントと引数: `ShowEarnItemNamed(LocalizationKeys.Ui.Tooltip.NamedMineClick, LocalizationKeys.Ui.Tooltip.PickUpInteract);`（コメント「// Fが押されていなければ現状を維持 / // Keep the current state while F is not pressed」）

`MiningProgressState.cs:56`: `if (!InputManager.Playable.ScreenLeftClick.GetKey)` → `if (!InputManager.Playable.Interact.GetKey)`（コメント「// Fを離したらフォーカス状態に遷移 / // Releasing F returns to the focus state」）

- [x] **Step 4: テストの入力をFへ**

`MiningEquipmentSwitchTest.cs`: `_mouse` を `Keyboard _keyboard` に、`Setup` の `InputSystem.AddDevice<Mouse>()` を `AddDevice<Keyboard>()` に。`PressLeftClick` を次に置換:
```csharp
        private void PressInteract()
        {
            // 入力アセットの生成(Enable)を状態イベントより先に済ませないとバインドが解決されない
            // The input asset must be created (and enabled) before the state event, otherwise its bindings never resolve
            var interact = InputManager.Playable.Interact;
            InputSystem.Update();
            Press(_keyboard.fKey);
            InputSystem.Update();
            // 押下が届いていないと全遷移がフォーカス復帰に化けてテストが無意味になるため前提を固定する
            // Without the press landing every transition collapses into a focus fallback and the test proves nothing
            Assert.IsTrue(interact.GetKey, "Fの押下がInputSystemへ届いていない");
        }
```
呼び出し3箇所を `PressInteract()` に置換。

`MiningFocusStateTestFixture.cs:86`: `Assert.IsFalse(InputManager.Playable.Interact.GetKey, "Fが押されていない前提が崩れている");`。`Setup` の `InputSystem.AddDevice<Mouse>()` は `AddDevice<Keyboard>()` に。

`PickUpLeftClick` を参照するテストのキー名を `PickUpInteract` に置換。

- [x] **Step 5: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → エラー0
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Mining"`
Expected: すべて PASS（`MiningAimTest` はTask 6で削除するまで暫定置換のまま通る）

- [x] **Step 6: コミット**

```bash
git add Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated moorestech_client/Assets/Scripts/Client.Game/InGame/Mining moorestech_client/Assets/Scripts/Client.Tests/Mining
git commit -m "feat: 採掘の入力を左クリックからFへ差し替え文言をF表記にする"
```

---

### Task 4: 実行時アウトライン生成（RuntimeOutlineFactory）と材質のResources移設

**Files:**
- Move: `moorestech_client/Assets/Asset/Common/Shader/Outline/Outline.mat`（＋.meta）→ `moorestech_client/Assets/Resources/InteractOutline.mat`（`git mv` でguid維持）
- Modify: `moorestech_client/Assets/Scripts/Client.Common/MaterialConst.cs`
- Modify: `moorestech_client/Assets/Scripts/Editor/MapObjectWrapperGenerator/WrapperPrefabFactory.cs:16`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Interact/Outline/RuntimeOutlineFactory.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Interact/RuntimeOutlineFactoryTest.cs`

**Interfaces:**
- Produces: `static GameObject RuntimeOutlineFactory.Create(GameObject root)` — `root` 直下に `Outline` レイヤの非活性子 `Outline` を作り返す。`MaterialConst.GetInteractOutlineMaterial()`
- Consumes: `LayerConst.OutlineLayer`

- [x] **Step 1: 失敗するテストを書く**

```csharp
using Client.Common;
using Client.Game.InGame.Interact.Outline;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Interact
{
    public class RuntimeOutlineFactoryTest
    {
        [Test]
        public void 最近傍LODのメッシュがOutlineレイヤに複製され非活性で返る()
        {
            var root = new GameObject("Root");
            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(root.transform, false);
            visual.transform.localPosition = new Vector3(1f, 2f, 3f);
            visual.GetComponent<MeshRenderer>().sharedMaterials = new Material[2];

            var outline = RuntimeOutlineFactory.Create(root);

            Assert.AreEqual(root.transform, outline.transform.parent);
            Assert.IsFalse(outline.activeSelf);
            Assert.AreEqual(LayerConst.OutlineLayer, outline.layer);
            var copied = outline.GetComponentInChildren<MeshRenderer>(true);
            Assert.AreEqual(LayerConst.OutlineLayer, copied.gameObject.layer);
            Assert.AreEqual(visual.GetComponent<MeshFilter>().sharedMesh, copied.GetComponent<MeshFilter>().sharedMesh);
            Assert.AreEqual(2, copied.sharedMaterials.Length);
            Assert.AreSame(MaterialConst.GetInteractOutlineMaterial(), copied.sharedMaterials[0]);
            Assert.AreEqual(visual.transform.position, copied.transform.position);

            Object.DestroyImmediate(root);
        }
    }
}
```

- [x] **Step 2: コンパイルして失敗を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `RuntimeOutlineFactory` / `GetInteractOutlineMaterial` 未定義エラー

- [x] **Step 3: 材質を移設し、MaterialConst と WrapperPrefabFactory を更新**

```bash
git mv moorestech_client/Assets/Asset/Common/Shader/Outline/Outline.mat moorestech_client/Assets/Resources/InteractOutline.mat
git mv moorestech_client/Assets/Asset/Common/Shader/Outline/Outline.mat.meta moorestech_client/Assets/Resources/InteractOutline.mat.meta
```

`MaterialConst.cs` に追加:
```csharp
        // インタラクト対象のアウトライン材質（ステンシル方式。URPのOutlinePassが描く）
        // Outline material for interact targets (stencil based, drawn by the URP OutlinePass)
        public const string InteractOutlineMaterial = "InteractOutline";
        private static Material _interactOutlineMaterial;

        public static Material GetInteractOutlineMaterial()
        {
            _interactOutlineMaterial ??= Resources.Load<Material>(InteractOutlineMaterial);
            return _interactOutlineMaterial;
        }
```

`WrapperPrefabFactory.cs:16`: `private const string OutlineMaterialPath = "Assets/Resources/InteractOutline.mat";`

- [x] **Step 4: RuntimeOutlineFactory を書く**

```csharp
using System.Collections.Generic;
using Client.Common;
using UnityEngine;

namespace Client.Game.InGame.Interact.Outline
{
    /// <summary>
    ///     実行時に最近傍LODのメッシュをOutlineレイヤへ複製する（WrapperPrefabFactory.CreateOutlineの実行時版）
    ///     Duplicates the nearest-LOD meshes onto the Outline layer at runtime (runtime twin of WrapperPrefabFactory.CreateOutline)
    /// </summary>
    public static class RuntimeOutlineFactory
    {
        private const string OutlineObjectName = "Outline";

        public static GameObject Create(GameObject root)
        {
            var outlineMaterial = MaterialConst.GetInteractOutlineMaterial();
            var outlineRoot = new GameObject(OutlineObjectName) { layer = LayerConst.OutlineLayer };
            outlineRoot.transform.SetParent(root.transform, false);

            foreach (var sourceRenderer in CollectNearestLodRenderers(root))
            {
                var sourceFilter = sourceRenderer.GetComponent<MeshFilter>();
                if (sourceFilter == null || sourceFilter.sharedMesh == null) continue;

                var outlineMesh = new GameObject(sourceRenderer.name) { layer = LayerConst.OutlineLayer };
                outlineMesh.transform.SetParent(outlineRoot.transform, false);
                CopyWorldTransform(sourceRenderer.transform, outlineMesh.transform);
                outlineMesh.AddComponent<MeshFilter>().sharedMesh = sourceFilter.sharedMesh;
                outlineMesh.AddComponent<MeshRenderer>().sharedMaterials = FillOutlineMaterials(sourceRenderer.sharedMaterials.Length, outlineMaterial);
            }

            // ハイライト側が点ける
            // The highlight caller turns it on
            outlineRoot.SetActive(false);
            return outlineRoot;

            #region Internal

            static List<Renderer> CollectNearestLodRenderers(GameObject target)
            {
                var renderers = new List<Renderer>();
                var lodGroup = target.GetComponentInChildren<LODGroup>(true);
                if (lodGroup == null)
                {
                    foreach (var renderer in target.GetComponentsInChildren<MeshRenderer>(true))
                        if (renderer.gameObject.layer != LayerConst.OutlineLayer) renderers.Add(renderer);
                    return renderers;
                }

                foreach (var renderer in lodGroup.GetLODs()[0].renderers)
                    if (renderer != null) renderers.Add(renderer);
                return renderers;
            }

            static void CopyWorldTransform(Transform source, Transform target)
            {
                target.SetPositionAndRotation(source.position, source.rotation);

                // localScaleは親の合成拡縮を打ち消してから入れる
                // Cancel out the parent's accumulated scale before assigning localScale
                var parentScale = target.parent.lossyScale;
                var sourceScale = source.lossyScale;
                target.localScale = new Vector3(sourceScale.x / parentScale.x, sourceScale.y / parentScale.y, sourceScale.z / parentScale.z);
            }

            static Material[] FillOutlineMaterials(int sourceMaterialCount, Material outlineMaterial)
            {
                // サブメッシュごとにスロットが要るので、元と同数（最低1枚）を全部アウトラインで埋める
                // Every submesh needs a slot, so fill as many as the source had, never fewer than one
                var materials = new Material[Mathf.Max(1, sourceMaterialCount)];
                for (var index = 0; index < materials.Length; index++) materials[index] = outlineMaterial;
                return materials;
            }

            #endregion
        }
    }
}
```

- [x] **Step 5: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → エラー0
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "RuntimeOutlineFactoryTest"`
Expected: PASS

- [x] **Step 6: コミット**

```bash
git add -A moorestech_client/Assets/Resources moorestech_client/Assets/Asset/Common/Shader/Outline moorestech_client/Assets/Scripts/Client.Common/MaterialConst.cs moorestech_client/Assets/Scripts/Editor/MapObjectWrapperGenerator/WrapperPrefabFactory.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Interact moorestech_client/Assets/Scripts/Client.Tests/Interact
git commit -m "feat: 実行時アウトライン生成を追加しOutline材質をResourcesへ移設"
```

---

### Task 5: 単押しインタラクト（ブロック・列車）の対象側実装

**Files:**
- Create: `Client.Game/InGame/Interact/ITapInteractAction.cs`
- Create: `Client.Game/InGame/Interact/ITapInteractable.cs`
- Create: `Client.Game/InGame/Block/BlockInteractable.cs`
- Create: `Client.Game/InGame/Block/BlockOpenInteractAction.cs`
- Create: `Client.Game/InGame/Train/View/Object/Core/TrainCarInteractable.cs`
- Create: `Client.Game/InGame/Train/View/Object/Core/TrainCarInteractActions.cs`
- Modify: `Client.Game/InGame/Block/BlockGameObject.cs:58-70`（`Initialize` 内で付与）
- Modify: `Client.Game/InGame/Train/View/Object/Core/TrainCarObjectFactory.cs:66-67`
- Modify: `Localization/localization.csv`（3キー追加）
- Test: `Client.Tests/Interact/BlockInteractableTest.cs`

**Interfaces:**
- Produces:
  ```csharp
  public interface ITapInteractAction { InputKey Key { get; } LocalizationKey HintKey { get; } IReadOnlyList<string> HintParams { get; } UITransitContext Execute(); }
  public interface ITapInteractable : IInteractable { IReadOnlyList<ITapInteractAction> Actions { get; } }
  public class BlockInteractable : MonoBehaviour, ITapInteractable { void Initialize(BlockGameObject blockGameObject); }
  public class TrainCarInteractable : MonoBehaviour, ITapInteractable { void Initialize(TrainCarEntityObject trainCarEntityObject); }
  ```
  新キー: `LocalizationKeys.Ui.Tooltip.InteractOpenBlock`（{p0}=ブロック名）、`InteractOpenTrainInventory`、`InteractRideTrain`
- Consumes: `InputManager.Playable.Interact/Ride`（Task 1）、`RuntimeOutlineFactory`（Task 4）、既存 `BlockSubInventorySource` / `TrainSubInventorySource` / `RideTrainCarRequest`

- [x] **Step 1: CSV にヒントキーを追加**

`ui.tooltip.namedRequiredItems` 行の直後に追加:
```csv
ui.tooltip.interactOpenBlock,[F] Open {p0},[F] Open {p0},[F] {p0}を開く,[F] {p0} öffnen
ui.tooltip.interactOpenTrainInventory,[F] Open car inventory,[F] Open car inventory,[F] 車両インベントリを開く,[F] Wageninventar öffnen
ui.tooltip.interactRideTrain,[E] Ride,[E] Ride,[E] 乗車,[E] Einsteigen
```
Run: `cd moorestech_web/webui && npm run gen:i18n && cd -` / `uloop compile --project-path ./moorestech_client --force-recompile`

- [x] **Step 2: 失敗するテストを書く**

```csharp
using System.Linq;
using Client.Game.InGame.Block;
using Client.Game.InGame.Interact;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Client.Tests.Interact
{
    public class BlockInteractableTest : InputTestFixture
    {
        // ForUnitTestで機械UIを持つブロックと持たないブロック
        // A ForUnitTest block with a machine UI and one without
        private const string OpenableBlockName = "TestMachine";
        private const string PlainBlockName = "TestBeltConveyor";

        public override void Setup()
        {
            base.Setup();
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void 開けるブロックはFで開くアクションを1つ持ち開けないブロックは候補にならない()
        {
            var openable = CreateBlockInteractable(OpenableBlockName);
            Assert.IsTrue(openable.IsInteractAvailable);
            Assert.AreEqual(1, openable.Actions.Count);
            Assert.AreSame(InputManager.Playable.Interact, openable.Actions[0].Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.InteractOpenBlock, openable.Actions[0].HintKey);
            var transit = openable.Actions[0].Execute();
            Assert.AreEqual(UIStateEnum.SubInventory, transit.NextStateEnum);

            var plain = CreateBlockInteractable(PlainBlockName);
            Assert.IsFalse(plain.IsInteractAvailable);
            Assert.AreEqual(0, plain.Actions.Count);
        }

        private static BlockInteractable CreateBlockInteractable(string blockName)
        {
            var master = MasterHolder.BlockMaster.Blocks.Data.First(block => block.Name == blockName);
            var gameObject = new GameObject(blockName);
            var blockGameObject = gameObject.AddComponent<BlockGameObject>();
            blockGameObject.Initialize(master, new BlockPositionInfo(Vector3Int.zero, BlockDirection.North, Vector3Int.one), new BlockInstanceId(1));
            return gameObject.GetComponent<BlockInteractable>();
        }
    }
}
```
※ `OpenableBlockName` / `PlainBlockName` は `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/master/blocks.json` を開き、`blockUIAddressablesPath` が非空のブロック名と空のブロック名を実際に確認して置き換えること（名前が違えばテストは即失敗するので誤りは検出される）。`BlockGameObject.Initialize` は `ClientContext.VanillaApi` を触るため、テストで例外が出る場合は `BlockInteractable.Initialize(BlockGameObject)` を直接呼ぶ形に変え、`blockGameObject` は `SetField` で `BlockMasterElement` を差し込む（`MiningTestReflection.SetField` 利用）。

- [x] **Step 3: 契約2ファイルを書く**

`Interact/ITapInteractAction.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.UIState;
using Client.Input;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     単押しで実行するアクション。キー・ヒント・実行を対象側が定義し、駆動側は従うだけ
    ///     A tap action: the target defines key, hint and execution, the driver only follows
    /// </summary>
    public interface ITapInteractAction
    {
        InputKey Key { get; }
        LocalizationKey HintKey { get; }
        IReadOnlyList<string> HintParams { get; }

        // UI遷移を伴わないアクションはnullを返す
        // Actions without a UI transition return null
        UITransitContext Execute();
    }
}
```

`Interact/ITapInteractable.cs`:
```csharp
using System.Collections.Generic;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     単押しアクションを1つ以上持つインタラクト対象
    ///     An interactable exposing one or more tap actions
    /// </summary>
    public interface ITapInteractable : IInteractable
    {
        IReadOnlyList<ITapInteractAction> Actions { get; }
    }
}
```

- [x] **Step 4: ブロック側を書く**

`Block/BlockOpenInteractAction.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Input;
using Client.Localization;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.Block
{
    /// <summary>
    ///     Fで機械UIを開く
    ///     Opens the machine UI with F
    /// </summary>
    public class BlockOpenInteractAction : ITapInteractAction
    {
        private readonly BlockGameObject _blockGameObject;
        private readonly string[] _hintParams;

        public InputKey Key => InputManager.Playable.Interact;
        public LocalizationKey HintKey => LocalizationKeys.Ui.Tooltip.InteractOpenBlock;
        public IReadOnlyList<string> HintParams => _hintParams;

        public BlockOpenInteractAction(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            _hintParams = new[] { Localize.GetContent(ContentLocalizationKeys.BlockName(blockGameObject.BlockMasterElement.BlockGuid)) };
        }

        public UITransitContext Execute()
        {
            var container = UITransitContextContainer.Create<ISubInventorySource>(new BlockSubInventorySource(_blockGameObject));
            return new UITransitContext(UIStateEnum.SubInventory, container);
        }
    }
}
```

`Block/BlockInteractable.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.Common;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Interact.Outline;
using UnityEngine;

namespace Client.Game.InGame.Block
{
    /// <summary>
    ///     開けるブロックのインタラクト面。開けないブロックは候補にならない
    ///     Interact face of an openable block; non-openable blocks never become candidates
    /// </summary>
    public class BlockInteractable : MonoBehaviour, ITapInteractable
    {
        private static readonly IReadOnlyList<ITapInteractAction> NoActions = Array.Empty<ITapInteractAction>();

        private BlockGameObject _blockGameObject;
        private GameObject _outlineObject;

        public GameObject GameObject => gameObject;
        public IReadOnlyList<ITapInteractAction> Actions { get; private set; } = NoActions;

        // 撤去済み（索引の墓標）は候補から外す
        // A removed block (index tombstone) leaves the candidates
        public bool IsInteractAvailable => Actions.Count > 0 && _blockGameObject.IsSearchable;

        public void Initialize(BlockGameObject blockGameObject)
        {
            _blockGameObject = blockGameObject;
            if (blockGameObject.BlockMasterElement.IsBlockOpenable())
                Actions = new ITapInteractAction[] { new BlockOpenInteractAction(blockGameObject) };
        }

        public void SetHighlighted(bool highlighted)
        {
            // 初回ハイライト時だけ複製メッシュを作る
            // Build the duplicate mesh only on the first highlight
            if (highlighted && _outlineObject == null) _outlineObject = RuntimeOutlineFactory.Create(gameObject);
            if (_outlineObject != null) _outlineObject.SetActive(highlighted);
        }
    }
}
```

`BlockGameObject.Initialize` の `foreach (var child in gameObject.GetComponentsInChildren<BlockGameObjectChild>(true)) child.Init(this);` の直後に追加:
```csharp
            // インタラクト面を付ける（開けるかは面側が判断する）
            // Attach the interact face; whether it opens is decided there
            gameObject.AddComponent<BlockInteractable>().Initialize(this);
```
BlockGameObject.cs は204行なので、この追加と引き換えに `SubscribeBlockState` 内の例外ログ文字列を1行に畳むか、`LoadBoundingBox` を `BlockPreviewBoundingBoxLoader` として分離して200行以下にする（分離する場合は `Block/BlockPreviewBoundingBoxLoader.cs` を新設し `static async UniTask<IPreviewOnlyObject> LoadAsync(BlockGameObject owner, BlockMasterElement master, BlockPositionInfo posInfo)` を持たせる）。

- [x] **Step 5: 列車側を書く**

`Train/View/Object/Core/TrainCarInteractActions.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.UI.UIState;
using Client.Game.InGame.UI.UIState.State.SubInventory;
using Client.Input;
using Mooresmaster.Localization.Generated;

namespace Client.Game.InGame.Train.View.Object.Core
{
    /// <summary>
    ///     Fで車両インベントリを開く
    ///     Opens the car inventory with F
    /// </summary>
    public class TrainCarOpenInventoryInteractAction : ITapInteractAction
    {
        private readonly TrainCarEntityObject _trainCar;
        public InputKey Key => InputManager.Playable.Interact;
        public LocalizationKey HintKey => LocalizationKeys.Ui.Tooltip.InteractOpenTrainInventory;
        public IReadOnlyList<string> HintParams => Array.Empty<string>();

        public TrainCarOpenInventoryInteractAction(TrainCarEntityObject trainCar)
        {
            _trainCar = trainCar;
        }

        public UITransitContext Execute()
        {
            var container = UITransitContextContainer.Create<ISubInventorySource>(new TrainSubInventorySource(_trainCar));
            return new UITransitContext(UIStateEnum.SubInventory, container);
        }
    }

    /// <summary>
    ///     Eで乗車する
    ///     Boards the car with E
    /// </summary>
    public class TrainCarRideInteractAction : ITapInteractAction
    {
        private readonly TrainCarEntityObject _trainCar;
        public InputKey Key => InputManager.Playable.Ride;
        public LocalizationKey HintKey => LocalizationKeys.Ui.Tooltip.InteractRideTrain;
        public IReadOnlyList<string> HintParams => Array.Empty<string>();

        public TrainCarRideInteractAction(TrainCarEntityObject trainCar)
        {
            _trainCar = trainCar;
        }

        public UITransitContext Execute()
        {
            // TODO ほかプレイヤーが列車に乗っているかどうかをチェックする（旧RideVehicleInputServiceから継承）
            var container = UITransitContextContainer.Create(new RideTrainCarRequest(_trainCar.TrainCarInstanceId));
            return new UITransitContext(UIStateEnum.TrainHUDScreen, container);
        }
    }
}
```

`Train/View/Object/Core/TrainCarInteractable.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.Interact;
using Client.Game.InGame.Interact.Outline;
using UnityEngine;

namespace Client.Game.InGame.Train.View.Object.Core
{
    /// <summary>
    ///     列車車両のインタラクト面。F=車両インベントリ、E=乗車の2アクション
    ///     Interact face of a train car: F opens the car inventory, E boards it
    /// </summary>
    public class TrainCarInteractable : MonoBehaviour, ITapInteractable
    {
        private GameObject _outlineObject;

        public GameObject GameObject => gameObject;
        public IReadOnlyList<ITapInteractAction> Actions { get; private set; }
        public bool IsInteractAvailable => true;

        public void Initialize(TrainCarEntityObject trainCarEntityObject)
        {
            Actions = new ITapInteractAction[]
            {
                new TrainCarOpenInventoryInteractAction(trainCarEntityObject),
                new TrainCarRideInteractAction(trainCarEntityObject),
            };
        }

        public void SetHighlighted(bool highlighted)
        {
            if (highlighted && _outlineObject == null) _outlineObject = RuntimeOutlineFactory.Create(gameObject);
            if (_outlineObject != null) _outlineObject.SetActive(highlighted);
        }
    }
}
```

`TrainCarObjectFactory.cs:67` の `trainEntityObject.Initialize(...)` 直後に追加:
```csharp
                // インタラクト面（開く・乗車）を付ける
                // Attach the interact face (open / ride)
                trainObject.AddComponent<TrainCarInteractable>().Initialize(trainEntityObject);
```

- [x] **Step 6: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → エラー0
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "BlockInteractableTest"`
Expected: PASS

- [x] **Step 7: コミット**

```bash
git add -A Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated moorestech_client/Assets/Scripts/Client.Game/InGame/Interact moorestech_client/Assets/Scripts/Client.Game/InGame/Block moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/Object/Core moorestech_client/Assets/Scripts/Client.Tests/Interact
git commit -m "feat: ブロックと列車にITapInteractableを実装"
```

---

### Task 6: 対象選定・tap駆動・InteractController と GameScreenState 接続、旧サービス削除

**Files:**
- Create: `Client.Game/InGame/Interact/InteractableResolver.cs`
- Create: `Client.Game/InGame/Interact/InteractTargetSelector.cs`
- Create: `Client.Game/InGame/Interact/TapInteractionDriver.cs`
- Create: `Client.Game/InGame/Interact/InteractController.cs`
- Modify: `Client.Game/InGame/UI/UIState/State/GameScreenState.cs`
- Modify: `Client.Starter/Registration/MainGameInteractionRegistration.cs:118-120`
- Modify: `Client.Starter/MainGameStarter.cs:68-71`（`miningController` フィールド削除）＋ GameSystem.prefab のコンポーネント除去（uloop）
- Delete: `Client.Game/InGame/Mining/MiningController.cs`、`UIState/State/SubInventory/GameScreenSubInventoryInteractService.cs`、`Train/Unit/RideVehicleInputService.cs`、`Client.Tests/Mining/MiningAimTest.cs`（各.metaも）
- Modify: `Client.Tests/UIState/UIStateCameraInteractionTest.cs:127-134`、`Client.Tests/UIState/UIStateFocusRestorationTest.cs:28`、`Client.Tests/Mining/Outcrop/OutcropMiningAimTest.cs`（`MiningController`→`InteractTargetSelector`）
- Test: `Client.Tests/Interact/InteractTargetSelectorTest.cs`、`Client.Tests/Interact/TapInteractionDriverTest.cs`、`Client.Tests/Interact/InteractControllerHighlightTest.cs`

**Interfaces:**
- Produces:
  ```csharp
  public static class InteractableResolver { public static bool TryResolve(Collider collider, out IInteractable interactable); }
  public class InteractTargetSelector { public const float InteractDistance = 2f; public IInteractable Select(); }
  public class TapInteractionDriver { public UITransitContext Step(ITapInteractable target); public void Clear(); }
  public class InteractController { public InteractController(LocalPlayerEquipment equipment, InteractTargetSelector selector); public UITransitContext ManualUpdate(); public void Disable(); }
  ```
- Consumes: Task 2〜5 のすべて、`MiningControllerContext` / `Mining*State`、`BlockClickDetectUtil` と同じ `AimPointProvider.GetAimScreenPoint()` / `UiPointerHitTest.IsPointerOverAnyUi()`、`PlayerSystemContainer.Instance.PlayerObjectController.Position`

- [x] **Step 1: 選定テストを書く（`MiningAimTest` / `OutcropMiningAimTest` の土台を流用）**

`Client.Tests/Interact/InteractTargetSelectorTest.cs`（Setup/TearDown は `MiningAimTest.cs:26-101` のカメラ・EventSystem・PlayerSystem 生成をそのままコピーし、`_miningObject` を除く）:
```csharp
        [Test]
        public void 照準レイのヒットが2m以内なら選ばれ2mを超えると選ばれない()
        {
            var camera = _cameraObject.GetComponent<Camera>();
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var target = CreateMapObjectTarget(camera.ScreenPointToRay(center).GetPoint(1f));
            _playerObject.transform.position = target.transform.position;
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);

            var selector = new InteractTargetSelector();
            Assert.AreSame(target, selector.Select());

            // 2mを超えると照準ヒットでも候補にならず、近傍にも無いのでnull
            // Beyond 2m the aim hit is discarded and nothing is nearby, so null
            _playerObject.transform.position = target.transform.position + new Vector3(0f, 0f, InteractTargetSelector.InteractDistance + 0.5f);
            Assert.IsNull(selector.Select());
        }

        [Test]
        public void 照準に何も無ければ半径2m内で視線角度が最小の候補が選ばれる()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            _cameraObject.transform.position = new Vector3(0f, 1f, -5f);
            _cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            _playerObject.transform.position = Vector3.zero;

            // 前方1.5m（角度0）と、より近い右横1.0m（角度90）
            // One 1.5m ahead (angle 0) and a closer one 1.0m to the right (angle 90)
            var ahead = CreateMapObjectTarget(new Vector3(0f, 0f, 1.5f));
            var right = CreateMapObjectTarget(new Vector3(1.0f, 0f, 0f));

            var selector = new InteractTargetSelector();
            Assert.AreSame(ahead, selector.Select());
            Assert.AreNotSame(right, selector.Select());
        }

        [Test]
        public void 開けないブロックは照準に当たっても選ばれない()
        {
            AimPointProvider.SetViewMode(PlayerViewMode.FirstPerson);
            var camera = _cameraObject.GetComponent<Camera>();
            var center = new Vector2(Screen.width / 2f, Screen.height / 2f);
            var blockObject = new GameObject("PlainBlock") { layer = LayerConst.BlockLayer };
            blockObject.transform.position = camera.ScreenPointToRay(center).GetPoint(1f);
            blockObject.AddComponent<BoxCollider>();
            var child = blockObject.AddComponent<BlockGameObjectChild>();
            var interactable = blockObject.AddComponent<BlockInteractable>();
            // Initializeを呼ばない＝Actionsが空＝IsInteractAvailable false
            // Never initialized = no actions = unavailable
            _playerObject.transform.position = blockObject.transform.position;
            Physics.SyncTransforms();

            Assert.IsNull(new InteractTargetSelector().Select());
            Object.DestroyImmediate(blockObject);
        }

        private MapObjectGameObject CreateMapObjectTarget(Vector3 position)
        {
            var targetObject = new GameObject("MapObjectTarget") { layer = LayerConst.MapObjectLayer };
            targetObject.transform.position = position;
            targetObject.AddComponent<SphereCollider>().radius = 0.05f;
            var mapObject = targetObject.AddComponent<MapObjectGameObject>();
            targetObject.AddComponent<MapObjectRayTarget>().Initialize(mapObject);
            _targetObjects.Add(targetObject);
            Physics.SyncTransforms();
            return mapObject;
        }
```
※ `MapObjectGameObject.IsAvailable` は `MapObjectMasterElement != null` を要求するため、テストでは `MiningTestReflection.SetField`（`BindingFlags`で自動プロパティのバッキングフィールド `<MapObjectMasterElement>k__BackingField`）に `MasterHolder.MapObjectMaster` の任意要素を差し込む。`Setup` で `new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));` を呼び、`MasterHolder.MapObjectMaster.MapObjects.Data[0]` を使う。

- [x] **Step 2: tap駆動とハイライト差分のテストを書く**

`Client.Tests/Interact/TapInteractionDriverTest.cs`（`MiningFocusStateTestFixture` と同じ tooltip 生成を `Setup` にコピー）:
```csharp
        [Test]
        public void アクションのヒントが行として出てキー押下で遷移が返る()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var target = new StubTapInteractable(new StubAction(InputManager.Playable.Interact, LocalizationKeys.Ui.Tooltip.InteractOpenTrainInventory, UIStateEnum.SubInventory), new StubAction(InputManager.Playable.Ride, LocalizationKeys.Ui.Tooltip.InteractRideTrain, UIStateEnum.TrainHUDScreen));
            var driver = new TapInteractionDriver();
            InputSystem.Update();

            Assert.IsNull(driver.Step(target));
            var lines = MouseCursorTooltip.Instance.GetPresentation().Lines;
            Assert.AreEqual(2, lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.InteractOpenTrainInventory.Key, lines[0].Key.Key);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.InteractRideTrain.Key, lines[1].Key.Key);

            Press(keyboard.eKey);
            InputSystem.Update();
            var transit = driver.Step(target);
            Assert.AreEqual(UIStateEnum.TrainHUDScreen, transit.NextStateEnum);
            Assert.IsFalse(MouseCursorTooltip.Instance.GetPresentation().Visible);
        }
```
（`StubTapInteractable` は `ITapInteractable` を `new GameObject` で実装、`StubAction` は `ITapInteractAction` を実装し `Execute` で `new UITransitContext(nextState)` を返す。テストクラス内 private sealed class として書く）

`Client.Tests/Interact/InteractControllerHighlightTest.cs`（旧 `MiningTargetFocusContextTest.SetFocusTargetPushesOnlyWhenTargetChanges` の意図を `InteractController` へ移す）:
```csharp
        [Test]
        public void ハイライトは対象が変わった時だけ切り替わり消失時は一度だけ消える()
        {
            var log = new List<string>();
            var first = new HighlightTrackingInteractable("first", log);
            var second = new HighlightTrackingInteractable("second", log);
            var selector = new ScriptedSelector();
            var controller = new InteractController(null, selector);

            selector.Next = first; controller.ManualUpdate(); controller.ManualUpdate();
            CollectionAssert.AreEqual(new[] { "first:true" }, log);

            selector.Next = second; controller.ManualUpdate();
            CollectionAssert.AreEqual(new[] { "first:true", "first:false", "second:true" }, log);

            selector.Next = null; controller.ManualUpdate(); controller.ManualUpdate();
            CollectionAssert.AreEqual(new[] { "first:true", "first:false", "second:true", "second:false" }, log);

            selector.Next = first; controller.ManualUpdate();
            controller.Disable();
            Assert.AreEqual("first:false", log[^1]);
        }
```
※ このテストのために `InteractTargetSelector.Select()` は `virtual` にし、`ScriptedSelector : InteractTargetSelector` が `Next` を返す。`HighlightTrackingInteractable` は `ITapInteractable` で `Actions` 空（tapもminingも走らない）。`InteractController` のコンストラクタ第1引数 `LocalPlayerEquipment` は `MiningControllerContext` へ渡すだけなので null 可（`MiningControllerContext(null)` は既存テストで実績あり）。

- [x] **Step 3: コンパイルして失敗を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `InteractTargetSelector` 等の未定義エラー

- [x] **Step 4: InteractableResolver を書く**

```csharp
using Client.Game.InGame.Block;
using Client.Game.InGame.Entity.Object;
using UnityEngine;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     当たったコライダからインタラクト対象を解決する。種別ごとの探し方はここに閉じる
    ///     Resolves the interactable behind a hit collider; per-kind lookup lives only here
    /// </summary>
    public static class InteractableResolver
    {
        public static bool TryResolve(Collider collider, out IInteractable interactable)
        {
            interactable = null;

            // mapObject・露頭はコライダ上のマーカーで案内される
            // Map objects and outcrops are pointed at by a marker on the collider
            if (collider.TryGetComponent(out IInteractRayTarget rayTarget)) interactable = rayTarget.Interactable;

            // ブロックはBlockGameObjectChildから親のインタラクト面へ
            // Blocks climb from BlockGameObjectChild to the parent's interact face
            else if (collider.GetComponentInParent<BlockGameObjectChild>() is { } blockChild)
                interactable = blockChild.BlockGameObject.GetComponent<BlockInteractable>();

            // 列車はレンダラー子から車両本体のインタラクト面へ
            // Train cars climb from the renderer child to the car's interact face
            else if (collider.GetComponentInParent<TrainCarEntityChildrenObject>() is { } trainChild)
                interactable = trainChild.TrainCarEntityObject.GetComponent<TrainCarInteractable>();

            return interactable != null && interactable.IsInteractAvailable;
        }
    }
}
```

- [x] **Step 5: InteractTargetSelector を書く**

```csharp
using System;
using Client.Common;
using Client.Game.InGame.BlockSystem.PlaceSystem.Common.PreviewController;
using Client.Game.InGame.Control;
using Client.Game.InGame.Control.ViewMode;
using Client.Game.InGame.Player;
using UnityEngine;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     インタラクト対象を常に1件だけ選ぶ。照準レイのヒットを優先し、無ければ半径2m内で視線角度が最小のもの（ADR 0046）
    ///     Picks exactly one interactable: the aim-ray hit first, else the smallest view angle within 2m (ADR 0046)
    /// </summary>
    public class InteractTargetSelector
    {
        public const float InteractDistance = 2f;
        private const float RayLength = 100f;
        private const int OverlapBufferSize = 64;

        private static readonly int InteractLayerMask = LayerConst.BlockOnlyLayerMask | LayerConst.MapObjectOnlyLayerMask;
        private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];

        public virtual IInteractable Select()
        {
            var camera = Camera.main;
            if (camera == null) return null;
            if (UiPointerHitTest.IsPointerOverAnyUi()) return null;

            var playerPosition = PlayerSystemContainer.Instance.PlayerObjectController.Position;
            var aimed = SelectByAimRay(camera, playerPosition);
            return aimed ?? SelectNearbyByViewAngle(camera, playerPosition);

            #region Internal

            IInteractable SelectByAimRay(Camera aimCamera, Vector3 playerPos)
            {
                var ray = aimCamera.ScreenPointToRay(AimPointProvider.GetAimScreenPoint());
                var hits = Physics.RaycastAll(ray, RayLength, InteractLayerMask);
                if (hits.Length == 0) return null;
                Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));

                foreach (var hit in hits)
                {
                    // 手前の設置ゴーストだけ貫通する（BlockClickDetectUtilと同じ規則）
                    // Only the placement ghost in front is see-through (same rule as BlockClickDetectUtil)
                    if (hit.collider.GetComponentInParent<BlockPreviewObject>() != null) continue;

                    // 最前面の実体が対象でなければ照準ヒット無しとして近傍探索へ回す
                    // If the frontmost solid is not interactable, fall through to the nearby search
                    if (!InteractableResolver.TryResolve(hit.collider, out var interactable)) return null;
                    return IsWithinReach(interactable, playerPos) ? interactable : null;
                }

                return null;
            }

            IInteractable SelectNearbyByViewAngle(Camera aimCamera, Vector3 playerPos)
            {
                var hitCount = Physics.OverlapSphereNonAlloc(playerPos, InteractDistance, _overlapBuffer, InteractLayerMask);
                IInteractable best = null;
                var bestAngle = float.PositiveInfinity;
                var bestSqrDistance = float.PositiveInfinity;
                var forward = aimCamera.transform.forward;

                for (var index = 0; index < hitCount; index++)
                {
                    if (!InteractableResolver.TryResolve(_overlapBuffer[index], out var candidate)) continue;
                    if (best != null && candidate.GameObject == best.GameObject) continue;

                    var toCandidate = candidate.GameObject.transform.position - playerPos;
                    var angle = Vector3.Angle(forward, toCandidate);
                    var sqrDistance = toCandidate.sqrMagnitude;

                    // 角度が小さい方を優先し、同角度なら近い方
                    // Prefer the smaller angle, and the closer one on a tie
                    var better = angle < bestAngle || (Mathf.Approximately(angle, bestAngle) && sqrDistance < bestSqrDistance);
                    if (!better) continue;
                    best = candidate;
                    bestAngle = angle;
                    bestSqrDistance = sqrDistance;
                }

                return best;
            }

            static bool IsWithinReach(IInteractable interactable, Vector3 playerPos)
            {
                return Vector3.Distance(playerPos, interactable.GameObject.transform.position) <= InteractDistance;
            }

            #endregion
        }
    }
}
```
（`OverlapSphere` の重複コライダ除外は `best` との比較だけでは不十分なので、実装時は `HashSet<GameObject>` をフィールドに持ち `Clear()` してから使う。上記の `if (best != null && ...)` 行はその `HashSet.Add` 判定に置き換える。）

- [x] **Step 6: TapInteractionDriver を書く**

```csharp
using System.Collections.Generic;
using Client.Game.InGame.UI.Tooltip;
using Client.Game.InGame.UI.UIState;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     単押し対象のヒント表示とキー実行。アクションの中身は知らない
    ///     Shows tap hints and executes on key press without knowing what the actions do
    /// </summary>
    public class TapInteractionDriver
    {
        private static readonly TooltipOwner TooltipOwner = new();
        private readonly List<TooltipLine> _lines = new();

        public UITransitContext Step(ITapInteractable target)
        {
            _lines.Clear();
            foreach (var action in target.Actions)
            {
                if (action.Key.GetKeyDown)
                {
                    Clear();
                    return action.Execute();
                }
                _lines.Add(new TooltipLine(action.HintKey, action.HintParams));
            }

            MouseCursorTooltip.Instance.Show(TooltipOwner, _lines);
            return null;
        }

        public void Clear()
        {
            MouseCursorTooltip.Instance.Hide(TooltipOwner);
        }
    }
}
```
（`TooltipLine` のコンストラクタ形は `MouseCursorTooltip.cs` の `TooltipLine` 定義を読んで合わせる。`Show(owner, lines)` は `IReadOnlyList<TooltipLine>` を受ける既存API）

- [x] **Step 7: InteractController を書く**

```csharp
using Client.Game.InGame.Mining;
using Client.Game.InGame.UI.Inventory.Equipment;
using Client.Game.InGame.UI.UIState;
using UnityEngine;

namespace Client.Game.InGame.Interact
{
    /// <summary>
    ///     GameScreenStateから毎フレーム駆動される司令塔。選定・ハイライト・単押し/長押しの振り分けを一箇所で行う
    ///     Driven every frame by GameScreenState: selection, highlight and tap/hold dispatch in one place
    /// </summary>
    public class InteractController
    {
        private readonly InteractTargetSelector _selector;
        private readonly TapInteractionDriver _tapDriver = new();
        private readonly MiningControllerContext _miningContext;
        private IMiningState _miningState = new MiningIdleState();
        private IInteractable _highlighted;

        public InteractController(LocalPlayerEquipment localPlayerEquipment, InteractTargetSelector selector)
        {
            _selector = selector;
            _miningContext = new MiningControllerContext(localPlayerEquipment);
        }

        public UITransitContext ManualUpdate()
        {
            var target = _selector.Select();
            ApplyHighlight(target);

            // 長押し系は採掘FSMがそのまま担う（対象でなければnullでIdleへ戻る）
            // Hold interactions stay with the mining FSM; a non-mining target passes null and it idles
            _miningContext.SetFocusTarget(target as IMiningTargetObject);
            _miningState = _miningState.GetNextUpdate(_miningContext, Time.deltaTime);

            if (target is ITapInteractable tapTarget) return _tapDriver.Step(tapTarget);
            _tapDriver.Clear();
            return null;
        }

        public void Disable()
        {
            ApplyHighlight(null);
            _tapDriver.Clear();
            _miningContext.SetFocusTarget(null);
            _miningState = new MiningIdleState();
        }

        private void ApplyHighlight(IInteractable target)
        {
            // 同一実体なら切り替えない
            // The same object never toggles
            if (ReferenceEquals(_highlighted, target)) return;
            if (_highlighted != null && (target == null || _highlighted.GameObject != target.GameObject)) _highlighted.SetHighlighted(false);
            if (target != null && (_highlighted == null || _highlighted.GameObject != target.GameObject)) target.SetHighlighted(true);
            _highlighted = target;
        }
    }
}
```
`MiningIdleState` のコンストラクタは `MouseCursorTooltip.Instance.Hide(...)` を呼ぶため、テストで `MouseCursorTooltip.Instance` が無い場合は `InteractControllerHighlightTest` の Setup で `MiningFocusStateTestFixture` と同じ tooltip 生成を行う。

- [x] **Step 8: GameScreenState を接続し、旧サービスを削除する**

`GameScreenState.cs`: フィールド `_subInventoryInteractService` / `_rideVehicleInputService` を `private readonly InteractController _interactController;` に置換し、コンストラクタ引数も `InteractController interactController` に。`GetNextUpdate` の乗車・ブロック判定2行を次に置換:
```csharp
            // インタラクト対象の選定・ハイライト・F/Eの実行を1箇所で駆動し、遷移があれば返す
            // Drive target selection, highlight and F/E execution in one place and return any transition
            var interactTransit = _interactController.ManualUpdate();
            if (interactTransit != null) return interactTransit;
```
`OnExit` の先頭に `_interactController.Disable();` を追加。不要になった `using Client.Game.InGame.Train.Unit;` / `...SubInventory;` を削除。

`MainGameInteractionRegistration.cs`: `GameScreenSubInventoryInteractService` と `RideVehicleInputService` の登録を削除し、`builder.Register<InteractTargetSelector>(Lifetime.Singleton); builder.Register<InteractController>(Lifetime.Singleton);` を追加（`using Client.Game.InGame.Interact;`）。

削除:
```bash
git rm moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningController.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Mining/MiningController.cs.meta
git rm moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/SubInventory/GameScreenSubInventoryInteractService.cs*
git rm moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/RideVehicleInputService.cs*
git rm moorestech_client/Assets/Scripts/Client.Tests/Mining/MiningAimTest.cs*
```

`MainGameStarter.cs:68-71` の `[FormerlySerializedAs("mapObjectMiningController")] [SerializeField] private MiningController miningController;` を削除（`using` 整理）。

GameSystem.prefab から `MiningController` コンポーネントを外す（手編集禁止。uloop経由）:
```
uloop execute-dynamic-code --project-path ./moorestech_client --code '
var path = "Assets/Asset/Common/Prefab/GameSystem.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
var removed = 0;
foreach (var component in root.GetComponentsInChildren<Component>(true))
{
    if (component == null) continue;
    var so = new UnityEditor.SerializedObject(component);
    var script = so.FindProperty("m_Script");
    if (script != null && script.objectReferenceValue == null) { UnityEngine.Object.DestroyImmediate(component, true); removed++; }
}
UnityEditor.PrefabUtility.SaveAsPrefabAsset(root, path);
UnityEditor.PrefabUtility.UnloadPrefabContents(root);
return "removed missing scripts: " + removed;'
```
（`MiningController.cs` 削除後のコンパイル完了を待ってから実行。Missing script になったコンポーネントを除去する。`removed` が1でなければ `GameObjectUtility.RemoveMonoBehavioursWithMissingScript` で再試行）

`UIStateCameraInteractionTest.cs:127-134` / `UIStateFocusRestorationTest.cs:28`: `new GameScreenState(skitManager, new InteractController(null, new InteractTargetSelector()), placementTargetPickService, CreateCameraPolicy(applier), CreateHotbarTapInputService(null))` の形に更新（null引数の個数も1つ減る）。

`OutcropMiningAimTest.cs`: `MiningController` の生成・`Update` 呼び出しを `new InteractTargetSelector().Select()` の戻り値検証に置き換える（露頭とmapObjectの優先はレイの最前面に従う、という既存の意図を維持）。

- [x] **Step 9: コンパイルと全関連テスト**

Run: `uloop compile --project-path ./moorestech_client` → エラー0、`grep -rn "KeyCode.E\b\|ScreenLeftClick" moorestech_client/Assets/Scripts/Client.Game/InGame/Mining` が0件
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Interact|Mining|UIState(CameraInteraction|FocusRestoration|KeyHintCatalog)"`
Expected: すべて PASS

- [x] **Step 10: コミット**

```bash
git add -A moorestech_client/Assets/Scripts moorestech_client/Assets/Asset/Common/Prefab/GameSystem.prefab
git commit -m "feat: InteractControllerをGameScreenStateから駆動し旧クリック/Eサービスを撤去"
```

---

### Task 7: moorestech_master の「左クリックで拾う」を「Fで拾う」へ（別PR＋ピン更新）

**Files:**
- Modify（別repo）: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/challenges.json:14,26`
- Modify（別repo）: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/localization/localization.csv:250,337`
- Modify: `.moorestech-external-revisions.json`（`moorestech_master.commitHash`）

**Interfaces:** なし（データのみ）

- [x] **Step 1: masterリポジトリを現ピンから分岐する**

```bash
cd ../moorestech_master && git fetch origin && git checkout -b feature/interact-key-pickup-text 6fdf04d978543f9c40074ba1281fdaf45a843f9f
```

- [x] **Step 2: 文言を置換する**

- `challenges.json:14` `"summary": "地面の小石を左クリックで1個拾おう"` → `"summary": "地面の小石をFで1個拾おう"`
- `challenges.json:26` `"pinText": "左クリックで拾う"` → `"pinText": "Fで拾う"`
- `localization.csv:250` → `challenge.bd5262ed-fbd4-51e0-a75d-2944f366e10a.summary,地面の小石をFで3個拾おう,Press F to pick up 3 Pebbles from the ground.,地面の小石をFで3個拾おう,Drücken Sie F um 3 Kieselsteine vom Boden aufzuheben.`
- `localization.csv:337` → `challengeTutorial.0426a3b7-8c17-542b-a804-4aacd472d38c.text,Fで拾う,Pick Up with F,Fで拾う,Mit F aufheben`

`grep -rn "左クリック" server_v8/mods` が0件になることを確認。

- [x] **Step 3: コミット・push・PR作成**

```bash
git add -A && git commit -m "master: 小石チャレンジの左クリック文言をFに変更（moorestech ADR 0046）" && git push -u origin feature/interact-key-pickup-text
gh pr create --title "小石チャレンジの左クリック文言をFに変更" --body "moorestech ADR 0046（Fキーインタラクト統合）に伴う文言変更。summary/pinText/localization 4箇所。"
git rev-parse HEAD
```

- [x] **Step 4: 本repoのピンを更新する**

`.moorestech-external-revisions.json` の `moorestech_master.commitHash` を Step 3 の `git rev-parse HEAD` の値に置換（Unityがピンを書き戻すことがあるため `git diff` で当該1行だけの差分であることを確認 — [[unity-rewrites-master-pin-file]]）。

```bash
cd ../moorestech && git add .moorestech-external-revisions.json && git commit -m "chore: master dataピンをFキー文言版へ更新"
```

---

### Task 8: プレイテストDSLにインタラクト操作を追加する

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Playtest/Operations/PlaytestInteractOps.cs`
- Modify: `.agents/skills/unity-playmode-recorded-playtest/references/write-scenario.md:65` 付近の操作表

**Interfaces:**
- Produces: `p.PressInteract()`, `p.HoldInteract(float seconds)`, `p.PressRide()`
- Consumes: `SemanticInput.KeyDown/KeyUp/TapKey`（既存）

- [x] **Step 1: DSLを書く**

```csharp
using Client.Playtest.Input;
using Cysharp.Threading.Tasks;
using UnityEngine.InputSystem;

namespace Client.Playtest.Operations
{
    /// <summary>
    ///     インタラクト（F/E）の共有操作。録画のアクションログに残す
    ///     Shared interact operations (F/E) recorded in the action log
    /// </summary>
    public static class PlaytestInteractOps
    {
        public static async UniTask PressInteract(this PlaytestDriver p)
        {
            p.Note("インタラクト(F)");
            await SemanticInput.TapKey(Key.F);
        }

        public static async UniTask PressRide(this PlaytestDriver p)
        {
            p.Note("乗車(E)");
            await SemanticInput.TapKey(Key.E);
        }

        // 採掘はF長押しで進捗が溜まるので、指定秒だけ押し続ける
        // Mining accumulates while F is held, so keep it down for the given seconds
        public static async UniTask HoldInteract(this PlaytestDriver p, float seconds)
        {
            p.Note($"インタラクト長押し {seconds}s");
            SemanticInput.KeyDown(Key.F);
            await UniTask.Delay(System.TimeSpan.FromSeconds(seconds));
            SemanticInput.KeyUp(Key.F);
            await UniTask.DelayFrame(2);
        }
    }
}
```

- [x] **Step 2: スキル参照表に3行追加**

`write-scenario.md` の操作表（`ClickPlace()` 行の直後）に:
```
| `PressInteract()` | Fを単押し（機械を開く・小石を拾う） |
| `HoldInteract(seconds)` | Fを指定秒押し続ける（採掘・手掘り） |
| `PressRide()` | Eを単押し（列車に乗る） |
```

- [x] **Step 3: コンパイルとコミット**

Run: `uloop compile --project-path ./moorestech_client` → エラー0
```bash
git add moorestech_client/Assets/Scripts/Client.Playtest/Operations/PlaytestInteractOps.cs* .agents/skills/unity-playmode-recorded-playtest/references/write-scenario.md
git commit -m "feat: プレイテストDSLにインタラクト操作(F/E)を追加"
```

---

### Task 9: unityプレイ録画テストで通しを確認する

**Files:**
- Create（一時。結果確認後に削除するか `Client.Playtest/Scenarios/` の既存配置規約に従う）: `moorestech_client/Assets/Scripts/Client.Playtest/Scenarios/InteractKeyUnificationScenario.cs`

- [x] **Step 1: シナリオを書く**

`unity-playmode-recorded-playtest` スキルの `references/write-scenario.md` の雛形に従い:
1. `SetupFlatGround()` → 小石mapObjectの最寄りへ `WarpPlayer` → `AimAt(小石)` → `PressInteract()` → `CountItem("小石")` が1増えること
2. 石窯を `PlaceBlockDirect` → 隣へワープ → `AimAt(石窯)` → 2フレーム待って `MouseCursorTooltip.Instance.GetPresentation().Lines[0].Key.Key == "ui.tooltip.interactOpenBlock"` → `PressInteract()` → `WaitUiState(UIStateEnum.SubInventory, 5f)`
3. `ExitToGameScreen()` → 石窯から3m離れてAim → tooltipが `Hidden` であること（2m制限）

- [x] **Step 2: 実行**

Run: `.agents/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh InteractKeyUnificationScenario`（スキル本文の手順どおり）
Expected: `result.json` が success。録画で石窯にアウトラインが出ていることを目視確認し、スクリーンショットをPR本文に添付する

- [x] **Step 3: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Playtest/Scenarios/InteractKeyUnificationScenario.cs*
git commit -m "test: Fキーインタラクトの通しプレイテストシナリオを追加"
```

---

### Task 10: 全ブランチレビュー（必須・省略不可）

- [x] **Step 1: 必ず最後にコードレビュースキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）**

`moores-code-review` スキルを起動し、ブランチ全体（Task 1〜9）をレビュー対象にする。指摘の機械的修正を適用し、設計判断だけ AskUserQuestion で仰ぐ。

- [ ] **Step 2: PR作成と撤収**

`pr-create` スキルでPRを作る（本文にADR 0046・裁定4件・master側PRのURL・録画スクリーンショットを記載）。作成直後に `moores-wt rm feature/interact-key-unification`。`bd close moorestech-3cbt --reason="PR #<番号>"`。

---

## 判断記録（ADR）

- 設計ADR: `docs/adr/0046-interact-key-unifies-open-ride-and-mining.md`（ユーザー裁定6件の出所つき）
- 裁定: `.decisions/2026-08-30-インタラクトはFキーに統合し採掘も含める.md`、`.decisions/2026-08-30-インタラクト対象は照準優先で無ければ近傍最寄りを1件選ぶ.md`、`.decisions/2026-08-30-インタラクト方法の表示はカーソル近傍ツールチップに統合する.md`、`.decisions/2026-08-30-インタラクトはIInteractableを対象側に持たせ単一コントローラで駆動する.md`、`.decisions/2026-08-30-列車はFで車両インベントリを開きEで乗車しインタラクトはGameScreen限定にする.md`

planning中の判断（すべて agent前提）:
- **単押し/長押しの振り分けは `ITapInteractable` / `IMiningTargetObject` の2サブ契約で行う**（`InteractController` は `is` 判定2つだけ持ち、開く/採掘の語彙は持たない）。出所: agent前提（ユーザー裁定「基盤は開く/採掘を知らない」の最小実現。押し方は種別でなく入力の性質）
- **ブロック・列車のインタラクト面は別コンポーネント（`BlockInteractable` / `TrainCarInteractable`）にする**。出所: agent前提（`BlockGameObject` が204行で200行規約に抵触するため。`TrainCarEntityChildrenObject` が `IDeleteTarget` を別責務として持つ前例と同型）
- **近傍探索は k-d tree 索引でなく `Physics.OverlapSphereNonAlloc`**。出所: agent前提（`NearestTargetIndex.TrySearchNearest` は種別GUIDキー必須で「任意種別の2m内」を引けない。旧 `RideVehicleInputService.TryFindNearbyTrainCar` と同機構）
- **`MiningControllerContext` からハイライト呼び出しを撤去し `InteractController` に一本化**。出所: agent前提（ハイライトは全種別共通の責務。旧 `SetFocusTarget` の同一GameObject判定はそのまま `ApplyHighlight` に移植）
- **`Outline.mat` を `Assets/Resources/InteractOutline.mat` へ移設**。出所: agent前提（実行時ロードには Resources か Addressables が要り、`MaterialConst` の既存3材質が Resources 前例。`git mv` でguidを保ちプレハブ参照を壊さない）
- **`ui.keyHint.key.f` は追加しない**。出所: agent前提（tooltipのみで左下HUDに載せない裁定の帰結。ADR 0046 Consequences に反映済み）
- **`pickUpLeftClick` は `pickUpInteract` へ改名**。出所: agent前提（AGENTS.md「変更の波及を恐れない」。キー名が実態と矛盾するのを残さない）
- **インタラクト選定の距離基準は対象 `GameObject.transform.position`**。出所: agent前提（旧 `MiningController` と同じ。マルチブロックの原点基準になる帰結は受容。実プレイで届かないブロックが出たらコライダ最近点へ切り替えるのは後続）
- **建築モード中の採掘喪失は既知の制限でなく裁定済み**（`.decisions/2026-08-30-列車は…GameScreen限定にする.md`）

## 配置と前例（spec-architecture-review）

| 項目 | 配置 | 前例 |
|---|---|---|
| `InteractController`（ステート駆動・`ManualUpdate`/`Disable`） | `Client.Game/InGame/Interact` | `PlaceSystemStateController.ManualUpdate()`（`PlaceBlockState` から駆動）。置換対象 `MiningController` は MonoBehaviour `Update` だったが、ユーザー裁定で駆動方式を変更（新規パターンとしてADRに記載） |
| 遷移コンテキストを返す `ManualUpdate()` | 同上 | `GameScreenSubInventoryInteractService.TryGetSubInventoryInteractObject`（共有状態を書かない遷移判定）。`InteractController` はハイライト・tooltip という表示状態を書くが、共有選択モデルは書かないため Try-bool ではなく戻り値 null/遷移の1本で返す |
| `IInteractable` を対象GameObject側が実装 | `Client.Game/InGame/Interact` ＋ 各対象 | `IMiningTargetObject` / `IDeleteTarget` / `INearestSearchTarget`（対象側interface前例） |
| `ITapInteractAction`（キー＋ヒント＋実行を対象が定義） | 同上 | `KeyHint`（キー名＋文言のペアをステートが宣言、ADR-0032） |
| tooltip表示 | `MouseCursorTooltip.Show(owner, lines)` | `MiningFocusState` / `PlacementFeedbackTooltipPresenter` |
| 実行時アウトライン | `Interact/Outline/RuntimeOutlineFactory` | `WrapperPrefabFactory.CreateOutline`（エディタ焼き込み） |
| 材質ロード | `MaterialConst` Resources | `GetPreviewPlaceBlockMaterial()` |
| DI登録 | `MainGameInteractionRegistration.RegisterUiAndPlayer` | 同ファイルの `PlacementTargetPickService` 登録 |
| 入力 | `InputManager.Playable` に `InputKey` 追加（Keyboard抑止スコープ） | `BlockPlaceRotation` |

データフロー: `GameScreenState.GetNextUpdate → InteractController.ManualUpdate →［選定1件］→ ハイライト/tooltip（表示）→ F/E → UITransitContext を返す`。共有選択モデルへの書き込みは無く、既存パイプラインへの交差点（bool戻り・直接セッター）は足していない。

死活表（Phase 2.5）:
| 操作 | 計画後 | 根拠 |
|---|---|---|
| 左クリックで機械UIを開く | 廃止→F | ユーザー裁定 |
| 左クリック長押しで採掘/手掘り | 廃止→F長押し | ユーザー裁定 |
| 左クリックで小石を拾う | 廃止→F | ユーザー裁定 |
| Eで列車に乗る | 維持（InputSystem化） | ユーザー裁定 |
| 左クリックで列車インベントリを開く | 廃止→F | ユーザー裁定 |
| 建築/破壊モード中の採掘 | 廃止 | ユーザー裁定 |
| 中クリックスポイト・B/G/T/R/Tab・設置・電線接続・破壊 | 維持 | 触らない |
| チュートリアル mapObjectPin「左クリックで拾う」 | 文言のみFへ | Task 7 |
