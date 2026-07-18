# クラフトツリー完全削除 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** クラフトツリーをサーバー状態、通信、保存、クライアントUI、Unityアセット、現行資料から完全に削除する。

**Architecture:** Unity YAMLは必ずEditor APIで先に整理し、その後にサーバーの保存・DI・通信登録、クライアントの通信・UI、最後に共有型と専用ファイルを削除する。途中の各コミットはUnityコンパイル可能な状態を保ち、削除後は保存JSONのキー不在、残存文字列・GUID不在、Prefabの参照整合性を独立に検証する。

**Tech Stack:** Unity 6.3、C#、uLoop、NUnit、Newtonsoft.Json、MessagePack、VContainer、Git

## Global Constraints

- 実装場所は `/Users/katsumi/moorestech-worktrees/tree3`、ブランチは `tree3` とする。
- `docs/superpowers/specs/2026-07-18-craft-tree-removal-design.md` を正本とし、互換DTO、空実装、無効化フラグ、旧セーブ移行、フォールバックを追加しない。
- Prefab・Scene・ScriptableObjectのYAMLを直接編集しない。Prefabの変更・削除・再保存は `uloop execute-dynamic-code` とUnity Editor APIだけで行う。
- `.meta` は手動作成しない。専用コードの削除には `git rm`、Prefabの削除には `AssetDatabase.DeleteAsset` を使う。
- `Library/` は削除しない。`partial`、デフォルト引数、不要なnull防御、汎用層へのドメイン語彙追加を行わない。
- C#変更後は `uloop compile --project-path ./moorestech_client` を実行する。Domain Reload中なら45秒待って再実行する。
- 実装subagentは各タスクごとに新規の `gpt-5.6-terra` セッションを使い、同じファイルを並行編集しない。
- 各タスクはコミットし、別subagentの仕様適合性・コード品質レビューでCritical・Importantが0件になってから次へ進む。

## 配置と前例

| # | 変更対象 | 所有層 | 使用機構・前例 |
|---|---|---|---|
| 1 | 共有・専用Prefab | `Client.Game` の表示層 | `PrefabUtility.LoadPrefabContents` / `SaveAsPrefabAsset` / `AssetDatabase.DeleteAsset` |
| 2 | `craftTreeInfo` と `CraftTreeManager` の保存統合 | `Game.SaveLoad` / `Server.Boot` | 既存の `WorldSaveAllInfoV1`、`AssembleSaveJsonText`、DI登録から削除 |
| 3 | get/apply通信と初期取得 | `Server.Protocol` / `Client.Network` | `PacketResponseCreator` と `UniTask.WhenAll` から登録・取得を対で削除 |
| 4 | クラフトツリーUI | `Client.Game` | `RecipeViewerView`、`PlayerInventoryState`、`MainGameStarter` から依存を削除 |

新しい型・public API・通知・永続化形式は追加しない。既存の各所有層からクラフトツリー固有責務を取り除くだけであり、`Core.*` やMaster生成物への配置変更はない。

---

### Task 1: Unity Prefabと専用アセットの安全な切り離し

**Files:**
- Modify through Unity Editor: `moorestech_client/Assets/Asset/UI/Prefab/Inventory/InventoryItems.prefab`
- Modify through Unity Editor: `moorestech_client/Assets/Asset/UI/Prefab/MainGameUI.prefab`
- Delete through Unity Editor: `moorestech_client/Assets/Asset/UI/Prefab/CraftTreeEditorViewItem.prefab`
- Delete through Unity Editor: `moorestech_client/Assets/Asset/UI/Prefab/CraftTreeListItem.prefab`
- Delete through Unity Editor: `moorestech_client/Assets/Asset/UI/Prefab/CraftTreeTarget.prefab`
- Delete through Unity Editor: `moorestech_client/Assets/Asset/UI/Prefab/CraftTreeTargetItem.prefab`

**Interfaces:**
- Consumes: 現在解決可能な `CraftTreeViewManager` と関連MonoBehaviour、UnityのPrefab API。
- Produces: `InventoryItems.prefab` と `MainGameUI.prefab` からクラフトツリーGameObjectが消え、後続タスクでスクリプトを削除できる状態。型の削除前は `RecipeViewerView` と `MainGameStarter` のserialized field名が残るため、Task 4でフィールド削除後に再シリアライズする。

- [ ] **Step 1: 変更前の対象数をEditor APIで検証する**

Run:

```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
using System.Linq;
using UnityEditor;
using UnityEngine;
var inventory = PrefabUtility.LoadPrefabContents("Assets/Asset/UI/Prefab/Inventory/InventoryItems.prefab");
var inventoryNames = inventory.GetComponentsInChildren<Transform>(true).Count(x => x.name == "CraftTree" || x.name == "RecipeTreeView" || x.name == "show craft tree");
PrefabUtility.UnloadPrefabContents(inventory);
var mainUi = PrefabUtility.LoadPrefabContents("Assets/Asset/UI/Prefab/MainGameUI.prefab");
var targetCount = mainUi.GetComponentsInChildren<Transform>(true).Count(x => x.name == "CraftTreeTarget");
PrefabUtility.UnloadPrefabContents(mainUi);
return $"inventory={inventoryNames}, target={targetCount}";
'
```

Expected: `inventory=4, target=1`。値が異なる場合は破壊せず、実Hierarchyと表示テキストを調べて同じ責務のオブジェクトを特定する。

- [ ] **Step 2: 共有PrefabをUnity Editor経由で編集する**

Run:

```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

var inventoryPath = "Assets/Asset/UI/Prefab/Inventory/InventoryItems.prefab";
var inventory = PrefabUtility.LoadPrefabContents(inventoryPath);
var inventoryTargets = inventory.GetComponentsInChildren<Transform>(true)
    .Where(x => x.name == "CraftTree" || x.name == "RecipeTreeView" || x.name == "show craft tree")
    .Select(x => x.gameObject)
    .ToArray();
if (inventoryTargets.Length != 4) throw new InvalidOperationException($"Inventory target count was {inventoryTargets.Length}.");
foreach (var target in inventoryTargets) UnityEngine.Object.DestroyImmediate(target);
PrefabUtility.SaveAsPrefabAsset(inventory, inventoryPath);
PrefabUtility.UnloadPrefabContents(inventory);

var mainUiPath = "Assets/Asset/UI/Prefab/MainGameUI.prefab";
var mainUi = PrefabUtility.LoadPrefabContents(mainUiPath);
var targetObjects = mainUi.GetComponentsInChildren<Transform>(true)
    .Where(x => x.name == "CraftTreeTarget")
    .Select(x => x.gameObject)
    .ToArray();
if (targetObjects.Length != 1) throw new InvalidOperationException($"MainGameUI target count was {targetObjects.Length}.");
UnityEngine.Object.DestroyImmediate(targetObjects[0]);
PrefabUtility.SaveAsPrefabAsset(mainUi, mainUiPath);
PrefabUtility.UnloadPrefabContents(mainUi);

AssetDatabase.SaveAssets();
return inventoryTargets.Length + targetObjects.Length;
'
```

Expected: `5`。

- [ ] **Step 3: 専用PrefabをAssetDatabase経由で削除する**

Run:

```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
using System;
using UnityEditor;
var paths = new[]
{
    "Assets/Asset/UI/Prefab/CraftTreeEditorViewItem.prefab",
    "Assets/Asset/UI/Prefab/CraftTreeListItem.prefab",
    "Assets/Asset/UI/Prefab/CraftTreeTarget.prefab",
    "Assets/Asset/UI/Prefab/CraftTreeTargetItem.prefab",
};
foreach (var path in paths)
{
    if (!AssetDatabase.DeleteAsset(path)) throw new InvalidOperationException($"Failed to delete {path}.");
}
AssetDatabase.SaveAssets();
AssetDatabase.Refresh();
return paths.Length;
'
```

Expected: `4`。各Prefabと対応metaが `git status` で削除になる。

- [ ] **Step 4: Prefabの残存名・専用Prefab GUIDを検証する**

Run:

```bash
rg -n -i 'm_Name: CraftTree|m_Name: RecipeTreeView|m_Name: show craft tree|662286bf4e28a450bb63e74ae27083c3|4596607e76063443283c56cf70ec7f86|a0c5713ee75744be3a613a05d49448e3|0b72f2ba269524ecd85a96972d747a1c' \
  moorestech_client/Assets/Asset/UI/Prefab/Inventory/InventoryItems.prefab \
  moorestech_client/Assets/Asset/UI/Prefab/MainGameUI.prefab \
  moorestech_client/Assets/Asset/Common/Prefab/GameSystem.prefab
```

Expected: no output。

- [ ] **Step 5: コンパイルしてコミットする**

Run:

```bash
uloop compile --project-path ./moorestech_client
git add moorestech_client/Assets/Asset
git commit -m "refactor: クラフトツリーPrefabを削除"
```

Expected: compile `Success: true`, `ErrorCount: 0`。コミット後にtracked変更なし。

---

### Task 2: サーバー保存・DI・プロトコル登録からクラフトツリーを外す

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/SaveLoad/AssembleSaveJsonTextTest.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/AssembleSaveJsonText.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldLoaderFromJson.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Json/WorldVersions/WorldSaveAllInfoV1.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.SaveLoad/Game.SaveLoad.asmdef`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Server.Boot.asmdef`
- Modify: `moorestech_server/Assets/Scripts/Server.Event/Server.Event.asmdef`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`

**Interfaces:**
- Consumes: `WorldSaveAllInfoV1` の既存コンストラクタ順、Microsoft DI、Newtonsoft JSON。
- Produces: 保存JSONに `craftTreeInfo` がなく、ロード・DI・パケットディスパッチが `CraftTreeManager` に依存しないサーバー。
- Note: Task 3までクライアント通信型が参照するため、`Game.CraftTree` と2プロトコルのファイル自体はこのタスクでは残す。

- [ ] **Step 1: 保存JSONの削除を要求する失敗テストを追加する**

`AssembleSaveJsonTextTest.cs` に `using Newtonsoft.Json.Linq;` を追加し、`var json` の直後へ次を追加する。

```csharp
// 廃止したクラフトツリー状態を保存形式から除外する
// Exclude the retired craft-tree state from the save format
var saveJson = JObject.Parse(json);
Assert.IsNull(saveJson["craftTreeInfo"]);
```

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'Tests\.UnitTest\.Game\.SaveLoad\.AssembleSaveJsonTextTest'
```

Expected: FAIL because `craftTreeInfo` exists。

- [ ] **Step 2: 保存集約と復元からクラフトツリー引数・フィールドを削除する**

`AssembleSaveJsonText` から `using Game.CraftTree`、`_craftTreeManager`、コンストラクタ引数・代入、`WorldSaveAllInfoV1` に渡す `_craftTreeManager.GetSaveJsonObject()` を削除する。

`WorldLoaderFromJson` から同じusing、field、コンストラクタ引数・代入、`_craftTreeManager.LoadCraftTreeInfo(load.CraftTreeInfo)` を削除する。

`WorldSaveAllInfoV1` から `Game.CraftTree*` using、`List<PlayerCraftTreeJsonObject> craftTreeInfo` 引数、代入、次のプロパティを削除する。

```csharp
[JsonProperty("craftTreeInfo")] public List<PlayerCraftTreeJsonObject> CraftTreeInfo { get; set; }
```

残る引数順は `gameUnlockStateJsonObject` の直後が `research` になる。

- [ ] **Step 3: サーバーDI・通信登録・不要asmdef参照を削除する**

`MoorestechServerDIContainerGenerator` から `using Game.CraftTree` と `services.AddSingleton<CraftTreeManager>();` を削除する。

`PacketResponseCreator` から次の2行を削除する。

```csharp
_packetResponseDictionary.Add(ApplyCraftTreeProtocol.ProtocolTag, new ApplyCraftTreeProtocol(serviceProvider));
_packetResponseDictionary.Add(GetCraftTreeProtocol.ProtocolTag, new GetCraftTreeProtocol(serviceProvider));
```

`Game.SaveLoad.asmdef`、`Server.Boot.asmdef`、`Server.Event.asmdef` から `"Game.CraftTree"` 参照を削除する。`Server.Protocol.asmdef` はTask 3でプロトコル本体と同時に外す。

- [ ] **Step 4: テストとコンパイルを通す**

Run:

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'Tests\.UnitTest\.Game\.SaveLoad\.AssembleSaveJsonTextTest'
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'Tests\.CombinedTest\.Server\.PacketTest\.InitialHandshakeProtocolTest'
```

Expected: compile succeeds; both suites have `FailedCount: 0`。

- [ ] **Step 5: コミットする**

Run:

```bash
git add moorestech_server/Assets/Scripts
git commit -m "refactor: サーバーからクラフトツリー状態を削除"
```

Expected: commit succeeds。

---

### Task 3: クライアント通信・UIと共有クラフトツリー型を削除する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/Responses.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiSendOnly.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Network/Client.Network.asmdef`
- Modify: `moorestech_client/Assets/Scripts/Client.DebugSystem/CharacterTestDebug.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/Inventory/RecipeViewer/RecipeViewerView.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlayerInventoryState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Client.Game.asmdef`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/Server.Protocol.asmdef`
- Delete: `moorestech_client/Assets/Scripts/Client.Game/InGame/CraftTree`
- Delete: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/ApplyCraftTreeProtocol.cs`
- Delete: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetCraftTreeProtocol.cs`
- Delete: `moorestech_server/Assets/Scripts/Game.CraftTree`

**Interfaces:**
- Consumes: Task 2でクラフトツリーを除いたサーバー保存・DI・packet registration。
- Produces: 初期取得タプルとUIの依存グラフにクラフトツリー型がなく、専用アセンブリ・プロトコル・クライアントUIコードが存在しない状態。

- [ ] **Step 1: 初期取得・レスポンス・送信APIからクラフトツリーを削除する**

`Responses.cs` から `using Game.CraftTree.Models`、`InitialHandshakeResponse.CraftTree`、タプルの `CraftTreeResponse craftTree`、コンストラクタ代入、`CraftTreeResponse` クラス全体を削除する。

結果のタプルは次の9要素とする。

```csharp
(
    List<MapObjectsInfoMessagePack> mapObjects,
    WorldDataResponse worldData,
    PlayerInventoryResponse inventory,
    List<ChallengeCategoryResponse> challenges,
    UnlockStateResponse unlockState,
    List<string> playedSkitIds,
    Dictionary<Guid, ResearchNodeState> researchNodeStates,
    RailGraphSnapshotMessagePack railGraphSnapshot,
    TrainUnitSnapshotResponse trainUnitSnapshots
) responses
```

`VanillaApiWithResponse` からusing、`GetCraftTree(playerId, ct)` のWhenAll要素、`GetCraftTree` メソッド全体を削除する。

`VanillaApiSendOnly` からusingと `SendCraftTreeNode(Guid target, List<CraftTreeNode> craftTree)` を削除する。

`CharacterTestDebug` からusing、`craftTree` ローカル、テスト用タプル要素を削除し、上記9要素と一致させる。

- [ ] **Step 2: UI消費側とDI登録を削除する**

`RecipeViewerView` から `System.Collections.Generic`、CraftTree namespace、`UnityEngine.UI` の不要using、2つのSerializeField、Awake内の `foreach (var craftTreeView in createCraftTreeView)` ブロックを削除する。

`PlayerInventoryState` からCraftTree namespace、field、コンストラクタ引数・代入、`OnExit` の `_craftTreeViewManager.Hide()` を削除する。コンストラクタは次の5引数とする。

```csharp
public PlayerInventoryState(
    RecipeViewerView recipeViewerView,
    PlayerInventoryViewController playerInventoryViewController,
    LocalPlayerInventoryController localPlayerInventoryController,
    InitialHandshakeResponse handshakeResponse)
```

`MainGameStarter` からCraftTree namespace、SerializeField、`builder.RegisterComponent(craftTreeViewManager);` を削除する。

- [ ] **Step 3: asmdef参照と専用ソースを削除する**

3つのasmdefから `"Game.CraftTree"` を削除する。

```text
moorestech_client/Assets/Scripts/Client.Network/Client.Network.asmdef
moorestech_client/Assets/Scripts/Client.Game/Client.Game.asmdef
moorestech_server/Assets/Scripts/Server.Protocol/Server.Protocol.asmdef
```

Task 2ですでに他のサーバーasmdefから削除済みであることを確認する。次を `git rm -r` で削除する。

```bash
git rm -r moorestech_client/Assets/Scripts/Client.Game/InGame/CraftTree
git rm moorestech_client/Assets/Scripts/Client.Game/InGame/CraftTree.meta
git rm \
  moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/ApplyCraftTreeProtocol.cs \
  moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/ApplyCraftTreeProtocol.cs.meta \
  moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetCraftTreeProtocol.cs \
  moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetCraftTreeProtocol.cs.meta
git rm -r moorestech_server/Assets/Scripts/Game.CraftTree
git rm moorestech_server/Assets/Scripts/Game.CraftTree.meta
```

- [ ] **Step 4: 製品コード参照の不在を確認してコンパイルする**

Run:

```bash
rg -n -i 'CraftTree|craftTree|show[[:space:]_-]*craft[[:space:]_-]*tree|RecipeTreeView|va:getCraftTree|va:applyCraftTree' \
  moorestech_server/Assets moorestech_client/Assets \
  --glob '*.cs' --glob '*.asmdef'
uloop compile --project-path ./moorestech_client
```

Expected: `rg` has no output; compile `Success: true`, `ErrorCount: 0`。

- [ ] **Step 5: 関連テストを通してコミットする**

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'Tests\.UnitTest\.Game\.SaveLoad\.AssembleSaveJsonTextTest'
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'Tests\.CombinedTest\.Server\.PacketTest\.InitialHandshakeProtocolTest'
git add moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts
git commit -m "refactor: クラフトツリー実装を完全削除"
```

Expected: both suites have `FailedCount: 0`; commit succeeds。

---

### Task 4: 現行資料・Unity残存参照・最終QAを完了する

**Files:**
- Modify: `docs/ugui-screenshot/screen-list.md`
- Delete: `docs/ugui-screenshot/screenshots/25-craft-tree-planner.png`
- Re-save through Unity Editor: `moorestech_client/Assets/Asset/UI/Prefab/Inventory/InventoryItems.prefab`
- Re-save through Unity Editor: `moorestech_client/Assets/Asset/UI/Prefab/MainGameUI.prefab`
- Re-save through Unity Editor: `moorestech_client/Assets/Asset/Common/Prefab/GameSystem.prefab`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/Removal/CraftTreeRemovalTest.cs`

**Interfaces:**
- Consumes: Task 1〜3でクラフトツリー型とアセットが削除済みのUnityプロジェクト。
- Produces: 現行資料・Unity YAML・GUID・Consoleに削除機能の残骸や破損参照がない、最終コミット済みのtree3。

- [ ] **Step 1: 現行スクリーン資料を削除する**

`screen-list.md` の次の行を削除する。

```markdown
| 20 | クラフトツリー（レシピチェーン設計） | ✅ | `25-craft-tree-planner.png` | `CraftTreeViewManager.CreateNewCraftTree(itemId)` を直接呼び出し（トレイテムのマスターGuidから`MasterHolder.ItemMaster.GetItemId`で変換） |
```

Run:

```bash
git rm docs/ugui-screenshot/screenshots/25-craft-tree-planner.png
```

tree3には `docs/webui/TODO.md`、`docs/webui/cef-webui-migration-todo.md`、`docs/webui/2026-07-07-parity-audit-verification-handoff.md` が存在しないため、これらは変更しない。

- [ ] **Step 2: 共有Prefabを再保存し、missing参照を検査する**

`InventoryItems.prefab` と `MainGameUI.prefab` は通常どおりUnity Editorから再保存する。`GameSystem.prefab` はprivate avatar asset不在に由来する既存のmissing script 18件が保存を妨げるため、`Chr001.prefab` のmissing componentをUnity APIで一時除去してから保存し、直後に`Chr001.prefab`をGitから復元してUnityへ再インポートする。最終差分に`Chr001.prefab`を残してはならない。

検査Run:

```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code '
using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
var paths = new[]
{
    "Assets/Asset/UI/Prefab/Inventory/InventoryItems.prefab",
    "Assets/Asset/UI/Prefab/MainGameUI.prefab",
    "Assets/Asset/Common/Prefab/GameSystem.prefab",
};
var failures = new List<string>();
foreach (var path in paths)
{
    var root = PrefabUtility.LoadPrefabContents(path);
    var obsoleteUiPaths = root.GetComponentsInChildren<Transform>(true)
        .Where(transform =>
            transform.name.Equals("CraftTree", StringComparison.OrdinalIgnoreCase) ||
            transform.name.Equals("RecipeTreeView", StringComparison.OrdinalIgnoreCase) ||
            transform.name.Equals("show craft tree", StringComparison.OrdinalIgnoreCase) ||
            transform.GetComponents<TMP_Text>().Any(text => text.text.Contains("クラフトツリー", StringComparison.Ordinal)))
        .Select(transform => AnimationUtility.CalculateTransformPath(transform, root.transform))
        .Distinct()
        .ToArray();
    var missingPaths = root.GetComponentsInChildren<Transform>(true)
        .Select(transform => transform.gameObject)
        .Where(gameObject => GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(gameObject) > 0)
        .Select(gameObject => AnimationUtility.CalculateTransformPath(gameObject.transform, root.transform))
        .ToArray();
    var missingPrefabPaths = root.GetComponentsInChildren<Transform>(true)
        .Select(transform => transform.gameObject)
        .Where(gameObject => PrefabUtility.GetPrefabInstanceStatus(gameObject) == PrefabInstanceStatus.MissingAsset)
        .Select(gameObject => AnimationUtility.CalculateTransformPath(gameObject.transform, root.transform))
        .Distinct()
        .ToArray();
    var brokenReferences = new List<string>();
    foreach (var component in root.GetComponentsInChildren<Component>(true))
    {
        if (component == null) continue;
        var serialized = new SerializedObject(component);
        var property = serialized.GetIterator();
        while (property.NextVisible(true))
        {
            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                property.objectReferenceValue == null &&
                property.objectReferenceInstanceIDValue != 0)
            {
                brokenReferences.Add($"{AnimationUtility.CalculateTransformPath(component.transform, root.transform)} | {component.GetType().FullName}.{property.propertyPath}");
            }
        }
    }
    if (missingPrefabPaths.Length != 0)
    {
        failures.Add($"{path}: missing prefabs: {string.Join(", ", missingPrefabPaths)}");
    }
    if (obsoleteUiPaths.Length != 0)
    {
        failures.Add($"{path}: obsolete craft-tree UI: {string.Join(", ", obsoleteUiPaths)}");
    }
    if (path.EndsWith("GameSystem.prefab", StringComparison.Ordinal))
    {
        if (missingPaths.Length != 18 || missingPaths.Any(missingPath => !missingPath.StartsWith("Player/PlayerAvater/Chr001/", StringComparison.Ordinal)))
        {
            failures.Add($"{path}: unexpected baseline missing scripts: {string.Join(", ", missingPaths)}");
        }
        var expectedBrokenReferences = new[]
        {
            "Player/PlayerAvater/Chr001/hair_05 | UnityEngine.SkinnedMeshRenderer.m_Mesh",
            "CutSceneManager | UnityEngine.Playables.PlayableDirector.m_SceneBindings.Array.data[0].key",
        };
        if (!brokenReferences.OrderBy(value => value).SequenceEqual(expectedBrokenReferences.OrderBy(value => value)))
        {
            failures.Add($"{path}: unexpected broken references: {string.Join(", ", brokenReferences)}");
        }
    }
    else
    {
        if (missingPaths.Length != 0)
        {
            failures.Add($"{path}: {string.Join(", ", missingPaths)}");
        }
        if (brokenReferences.Count != 0)
        {
            failures.Add($"{path}: broken references: {string.Join(", ", brokenReferences)}");
        }
    }
    PrefabUtility.UnloadPrefabContents(root);
}
if (failures.Count != 0) throw new InvalidOperationException(string.Join("\n", failures));
return "MissingPrefabs=0, InventoryItems/MainGameUI clean, GameSystem=18 missing scripts + 2 broken-reference baseline";
'
```

Expected: `MissingPrefabs=0, InventoryItems/MainGameUI clean, GameSystem=18 missing scripts + 2 broken-reference baseline` and no exception。`GameSystem`の18 missing scriptsと2 broken referencesはprivate asset不在に由来する変更前ベースラインであり、増減を失敗にする。さらに`GameSystem.prefab`から削除対象GUID `61f42a36e8ea4515850d3bce341f3f35` と `craftTreeViewManager` が消え、`Chr001.prefab`に差分がないこと。

- [ ] **Step 3: Prefab残骸の回帰テストを追加する**

`CraftTreeRemovalTest.cs` に `InventoryPrefabDoesNotContainCraftTreeUi` を追加する。`AssetDatabase.LoadAssetAtPath<GameObject>` で実際の `InventoryItems.prefab` を読み、inactiveを含む全Transformについて次を検査する。

- 名前が `CraftTree`、`RecipeTreeView`、`show craft tree` のいずれでもない。
- 全Componentのserialized `m_text` に `クラフトツリー` が含まれない。
- 違反時はHierarchy pathを列挙し、どの残骸が戻ったか分かるようにする。

`TMPro` をテストasmdefへ追加せず、`SerializedObject.FindProperty("m_text")` で表示文言を検査する。フォルダとファイルのmetaはUnityのインポートで生成させ、手動作成しない。

Run:

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'Client\.Tests\.Removal\.CraftTreeRemovalTest'
```

Expected: 1 passed, 0 failed。

- [ ] **Step 4: 削除対象GUIDと製品・現行資料の参照を全検索する**

Run:

```bash
rg -n \
  '662286bf4e28a450bb63e74ae27083c3|4596607e76063443283c56cf70ec7f86|a0c5713ee75744be3a613a05d49448e3|0b72f2ba269524ecd85a96972d747a1c|058220796c814f608b0d1b94a6f2dfd5|ee3261436a374ca393cca5bb22485a8f|36b72ae7cc6b4b73a4767fe5b4ddb3f1|1c64e53f6322408d9b376d095e2bad6c|a7e915c9672e4fdbb752d84fbad91529|7bc4ecb2349b47398a4c892145f3aac8|0e111dc75b0e409f85558606cf4912cc|0eb9491ba2804e2aa738d53e718a1ca4|61f42a36e8ea4515850d3bce341f3f35|7867aeb59d3046e8973df223e3187154|c9cd2765530440c4925308171448a53d|2d4e3a54b66a44b798bcba9760b88e43|3908aa65463394990878052ecc1ef88c|2a640c771c80473eb174b15fe9be8809|2e74e2c896c74f7b875c836606fd7670' \
  moorestech_client/Assets moorestech_server/Assets

rg -n -i 'CraftTree|craftTree|show[[:space:]_-]*craft[[:space:]_-]*tree|クラフトツリー|RecipeTreeView|va:getCraftTree|va:applyCraftTree' \
  moorestech_client/Assets moorestech_server/Assets \
  --glob '!**/*Tests/**' \
  --glob '!Library/**' --glob '!Temp/**' --glob '!Logs/**'

rg -n -i 'CraftTree|craftTree|show[[:space:]_-]*craft[[:space:]_-]*tree|クラフトツリー|RecipeTreeView|va:getCraftTree|va:applyCraftTree' \
  docs \
  --glob '!docs/superpowers/**' \
  --glob '!Library/**' --glob '!Temp/**' --glob '!Logs/**'

rg -n -i 'CraftTree|craftTree|show[[:space:]_-]*craft[[:space:]_-]*tree|RecipeTreeView|va:getCraftTree|va:applyCraftTree' \
  moorestech_client/Assets moorestech_server/Assets \
  --glob '**/*Tests/**'
```

Expected: 最初の3コマンドは出力なし。テスト検索は保存JSONから削除済みであることを検証する`AssembleSaveJsonTextTest.cs`の`craftTreeInfo`アサーションと、旧Prefab要素の再混入を防ぐ`CraftTreeRemovalTest.cs`だけ。`docs/superpowers/` は承認済み履歴資料なので検索対象外。

- [ ] **Step 5: 最終コンパイル・テスト・Errorログを検証する**

Run:

```bash
uloop clear-console --project-path ./moorestech_client
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'Client\.Tests\.Removal\.CraftTreeRemovalTest'
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'Tests\.UnitTest\.Game\.SaveLoad\.AssembleSaveJsonTextTest'
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'Tests\.CombinedTest\.Server\.PacketTest\.InitialHandshakeProtocolTest'
uloop get-logs --project-path ./moorestech_client --log-type Error
```

Expected: compile `Success: true`, Prefab removal test 1/1 and both server suites `FailedCount: 0`, Error logs empty。

- [ ] **Step 6: 差分監査を行い最終タスクをコミットする**

Run:

```bash
git diff --check -- . ':(glob,exclude)moorestech_client/Assets/**/*.prefab'
git status --short
git add docs moorestech_client/Assets/Asset moorestech_client/Assets/Scripts/Client.Tests/Removal
git commit -m "refactor: クラフトツリーの残存参照を削除"
git status --short
```

Expected: Unity自身が生成したPrefab末尾空白を除く`git diff --check`に出力がなく、最終statusがclean。
