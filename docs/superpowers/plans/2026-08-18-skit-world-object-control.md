# スキットの世界非表示を共通interfaceへ載せ替える Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** スキットの `inGameObjectControl` が消す「Environment 外の世界オブジェクト」を共通 interface `ISkitWorldObjectControl` で束ね、これまで消し残っていた鉱脈の露頭もスキットの世界非表示カットで一緒に消えるようにする。

**Architecture:** `Client.Skit` 側に `ISkitWorldObjectControl { void SetActive(bool) }` を1本置き、`MapObjectGameObjectDatastore` と `OutcropGameObjectDatastore` の両方に実装させる。SkitManager は VContainer の `IReadOnlyList<T>` 一括注入（`ITutorialWorldPin` と同型）で全実装を受け取り、composite `SkitWorldObjectControlGroup` として skit の DI コンテナへ登録する。コマンドは1フラグ `worldObjectEnable` を composite へ流すだけになる。

**Tech Stack:** Unity 6 / C# / VContainer / UniTask / CommandForgeGenerator（`commands.yaml` を additionalfile とする SourceGenerator） / NUnit（Unity Test Runner EditMode）

## Requirements

- スキットの世界非表示カット（`100_start_game` の `inGameObjectControl` id:70）で、露頭（vein の見た目オブジェクト）が mapObject と同時に非表示になる — 受け入れ基準: 録画実走で宇宙カットに露頭が1つも映らない
- 復帰コマンド（id:71）で露頭が mapObject と同時に再表示される — 受け入れ基準: 録画実走で復帰後に露頭が見える／`activeSelf == true` を assert
- 対象は共通 interface `ISkitWorldObjectControl` で表現し、mapObject datastore と露頭 datastore の両方が実装する — 受け入れ基準: 既存 `ISkitMapObjectControl` はリポジトリから消えている
- 束ねは composite `SkitWorldObjectControlGroup`（`IReadOnlyList<ISkitWorldObjectControl>` 注入）で行う — 受け入れ基準: composite が全要素へ `SetActive` を流すユニットテストが green
- 今後 Environment 外の表示物が増えたとき、interface 実装と DI 登録だけで乗る — 受け入れ基準: composite もコマンドも対象を名指ししていない
- コマンドのフラグ名を実処理に一致させる（`mapObjectEnable` → `worldObjectEnable`）。`commands.yaml` / `100_start_game.json` / i18n(japanese・english) / `commandListLabelFormat` を一括更新する — 受け入れ基準: リポジトリ内に `mapObjectEnable` / `MapObjectEnable` の参照が（ADR・裁定レコード等の文書を除き）残っていない
- entity は束に含めない — 受け入れ基準: `ISkitEntityObjectControl` と `entityEnable` が現状のまま残る
- デバッグシーン（`SkitTester`）が起動時に例外を出さない — 受け入れ基準: 露頭 datastore のダミーが `ISkitWorldObjectControl` として登録されている

**やらないこと（スコープ境界）**

- ビルドメニュー・インベントリ滞在中にスキットが発火しても `Story` ステートへ遷移しない件（bd `moorestech-h4j`）には触れない
- `MapVeinRangeViewService`（設置プレビュー中の鉱脈範囲ボックス）は本件と無関係。`PlaceBlockState.OnExit` で畳まれることを調査済みなので変更しない
- スキットが例外で中断したときに世界非表示が戻らない問題（既存の block/mapObject と共通の性質）は本 plan では扱わない

## Global Constraints

- **作業ツリー**: `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/skit-outcrop-hide`（ブランチ `fix/skit-hide-outcrop-with-world-objects`、master `ce10cea49` から分岐）。メインクローンでは作業しない
- **Unity Editor**: この worktree は `moores-wt new --no-editor` で作られている（作成時に他セッションの Editor が上限超過だったため）。最初の `.cs` 変更前に `uloop launch /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/skit-outcrop-hide/moorestech_client` で自分用 Editor を起動する。他セッションの Editor は絶対に kill しない
- **コンパイル必須ゲート**: `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client` を実行し、エラー0を確認してからコミットする
- **テスト実行**: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "<正規表現>"`。`--test-mode` を省くと PlayMode が既定になるので必ず付ける。「Unity is reloading (Domain Reload in progress)」が出たら45秒待ってリトライ
- **コメント規約**: 主要な処理セクションに日本語1行→英語1行の2行セットコメント。自明なコメントは書かない。日本語本文の目安は処理・変数20字、メソッド30字
- **禁止事項**: `partial`（如何なる条件でも）、`Func<>`、try-catch（外部境界の隔離を除く）、単純 getter/setter プロパティ、`.meta` の手動作成、Prefab/シーンのテキスト直接編集
- **ファイル規約**: 1ファイル200行以下、1ディレクトリ10ファイルまで
- **`.meta` ファイル**: 新規 `.cs` を作ったら Unity が `.meta` を生成する。生成された `.meta` は一緒にコミットしてよい（手書きは禁止）
- 後方互換・パフォーマンス最適化・将来拡張性は考慮しない。正しい設計のために全 JSON・全呼び出し側を一括更新する

---

### Task 1: 共通interfaceとcompositeを作る

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Skit/Commands/InGameObjectControlCommand.cs:16-19`（`ISkitMapObjectControl` の宣言を `ISkitWorldObjectControl` へ置換。この時点では `ExecuteAsync` は旧 interface を使ったままなのでコンパイルが通るよう両方の作業を同一タスクで完結させる → 下記 Step 3 参照）
- Create: `moorestech_client/Assets/Scripts/Client.Game/Skit/SkitWorldObjectControlGroup.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Skit/SkitWorldObjectControlGroupTest.cs`

**Interfaces:**
- Consumes: なし（このタスクが起点）
- Produces:
  - `CommandForgeGenerator.Command.ISkitWorldObjectControl`（`void SetActive(bool enable)`）
  - `Client.Game.Skit.SkitWorldObjectControlGroup`（ctor: `SkitWorldObjectControlGroup(IReadOnlyList<ISkitWorldObjectControl> worldObjectControls)` / `void SetActive(bool enable)`）

- [x] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Skit/SkitWorldObjectControlGroupTest.cs` を新規作成する:

```csharp
using System.Collections.Generic;
using Client.Game.Skit;
using CommandForgeGenerator.Command;
using NUnit.Framework;

namespace Client.Tests.Skit
{
    public class SkitWorldObjectControlGroupTest
    {
        [Test]
        public void SetActiveReachesEveryRegisteredWorldObject()
        {
            var mapObjects = new RecordingWorldObjectControl();
            var outcrops = new RecordingWorldObjectControl();
            var group = new SkitWorldObjectControlGroup(
                new List<ISkitWorldObjectControl> { mapObjects, outcrops });

            group.SetActive(false);
            group.SetActive(true);

            CollectionAssert.AreEqual(new[] { false, true }, mapObjects.ReceivedValues);
            CollectionAssert.AreEqual(new[] { false, true }, outcrops.ReceivedValues);
        }

        [Test]
        public void SetActiveOnEmptyGroupDoesNotThrow()
        {
            var group = new SkitWorldObjectControlGroup(new List<ISkitWorldObjectControl>());

            Assert.DoesNotThrow(() => group.SetActive(false));
        }

        private sealed class RecordingWorldObjectControl : ISkitWorldObjectControl
        {
            public readonly List<bool> ReceivedValues = new();

            public void SetActive(bool enable)
            {
                ReceivedValues.Add(enable);
            }
        }
    }
}
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "SkitWorldObjectControlGroupTest"`
Expected: コンパイルエラー（`ISkitWorldObjectControl` / `SkitWorldObjectControlGroup` が存在しない）で実行に至らない

- [x] **Step 3: interfaceを置換する**

`InGameObjectControlCommand.cs` の `ISkitMapObjectControl` 宣言（16-19行）を差し替え、`ExecuteAsync` の該当行も同時に新 interface へ向ける:

```csharp
    // mapObjectと露頭のようにEnvironment外へ置かれる世界オブジェクトの表示窓口
    // Visibility entry point for world objects placed outside Environment, such as map objects and outcrops
    public interface ISkitWorldObjectControl
    {
        void SetActive(bool enable);
    }
```

`ExecuteAsync` 内の該当1行:

```csharp
            // mapObject・露頭・エンティティはEnvironment外に生成されるため個別に消す
            // Map objects, outcrops, and entities live outside Environment, so hide them individually
            storyContext.GetService<ISkitWorldObjectControl>().SetActive(MapObjectEnable);
```

（`MapObjectEnable` は Task 3 で `WorldObjectEnable` へリネームする。この時点では `commands.yaml` が旧名のままなので生成プロパティ名も旧名である）

- [x] **Step 4: compositeを実装する**

`moorestech_client/Assets/Scripts/Client.Game/Skit/SkitWorldObjectControlGroup.cs` を新規作成する:

```csharp
using System.Collections.Generic;
using CommandForgeGenerator.Command;

namespace Client.Game.Skit
{
    /// <summary>
    ///     Environment外に置かれた世界オブジェクトを1単位で表示切替する
    ///     Toggles every world object placed outside Environment as a single unit
    /// </summary>
    public class SkitWorldObjectControlGroup : ISkitWorldObjectControl
    {
        private readonly IReadOnlyList<ISkitWorldObjectControl> _worldObjectControls;

        public SkitWorldObjectControlGroup(IReadOnlyList<ISkitWorldObjectControl> worldObjectControls)
        {
            _worldObjectControls = worldObjectControls;
        }

        public void SetActive(bool enable)
        {
            foreach (var worldObjectControl in _worldObjectControls) worldObjectControl.SetActive(enable);
        }
    }
}
```

- [x] **Step 5: SkitManagerとMapObjectGameObjectDatastoreの参照を暫定で合わせてコンパイルを通す**

`ISkitMapObjectControl` が消えたことで2箇所が壊れる。Task 2 で本実装するが、このタスクをコンパイルグリーンで閉じるため、ここで最小の追従を行う:

- `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs:22` のクラス宣言を `ISkitMapObjectControl` → `ISkitWorldObjectControl` へ置換する
- `moorestech_client/Assets/Scripts/Client.Game/Skit/SkitManager.cs:156` を次へ置換する:

```csharp
                builder.RegisterInstance<ISkitWorldObjectControl>(mapObjectGameObjectDatastore);
```

- [x] **Step 6: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

- [x] **Step 7: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "SkitWorldObjectControlGroupTest"`
Expected: 2 tests PASS

- [x] **Step 8: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Skit/Commands/InGameObjectControlCommand.cs \
        moorestech_client/Assets/Scripts/Client.Game/Skit/SkitWorldObjectControlGroup.cs \
        moorestech_client/Assets/Scripts/Client.Game/Skit/SkitWorldObjectControlGroup.cs.meta \
        moorestech_client/Assets/Scripts/Client.Tests/Skit \
        moorestech_client/Assets/Scripts/Client.Game/InGame/Map/MapObject/MapObjectGameObjectDatastore.cs \
        moorestech_client/Assets/Scripts/Client.Game/Skit/SkitManager.cs
git commit -m "feat(skit): Environment外の世界オブジェクトを束ねるISkitWorldObjectControlを追加"
```

---

### Task 2: 露頭を束へ載せてDIを配線する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGameObjectDatastore.cs:21`（クラス宣言に interface 追加）＋末尾に `SetActive` 追加
- Modify: `moorestech_client/Assets/Scripts/Client.Game/Skit/SkitManager.cs:37, 156`（concrete注入 → `IReadOnlyList` 注入、composite 登録）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs:307-308`（DI に `As<ISkitWorldObjectControl>()` を追加）
- Modify: `moorestech_client/Assets/Scripts/Client.DebugSystem/Skit/SkitTester.cs`（露頭ダミーの追加登録）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Skit/OutcropGameObjectDatastoreSkitVisibilityTest.cs`

**Interfaces:**
- Consumes: `CommandForgeGenerator.Command.ISkitWorldObjectControl` / `Client.Game.Skit.SkitWorldObjectControlGroup`（Task 1）
- Produces: `OutcropGameObjectDatastore.SetActive(bool enable)`（`ISkitWorldObjectControl` 実装）

- [x] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/Skit/OutcropGameObjectDatastoreSkitVisibilityTest.cs` を新規作成する:

```csharp
using Client.Game.InGame.Map.Outcrop;
using CommandForgeGenerator.Command;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Skit
{
    public class OutcropGameObjectDatastoreSkitVisibilityTest
    {
        private GameObject _datastoreObject;

        [TearDown]
        public void TearDown()
        {
            if (_datastoreObject != null) Object.DestroyImmediate(_datastoreObject);
        }

        [Test]
        public void SetActiveFalseHidesEveryOutcropUnderTheDatastore()
        {
            _datastoreObject = new GameObject(nameof(OutcropGameObjectDatastore));
            var datastore = _datastoreObject.AddComponent<OutcropGameObjectDatastore>();
            var outcrop = new GameObject("VeinOutcrop_test");
            outcrop.transform.SetParent(_datastoreObject.transform);

            datastore.SetActive(false);
            Assert.IsFalse(outcrop.activeInHierarchy);

            datastore.SetActive(true);
            Assert.IsTrue(outcrop.activeInHierarchy);
        }

        [Test]
        public void DatastoreIsPartOfTheSkitWorldObjectContract()
        {
            _datastoreObject = new GameObject(nameof(OutcropGameObjectDatastore));
            var datastore = _datastoreObject.AddComponent<OutcropGameObjectDatastore>();

            Assert.IsInstanceOf<ISkitWorldObjectControl>(datastore);
        }
    }
}
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "OutcropGameObjectDatastoreSkitVisibilityTest"`
Expected: コンパイルエラー（`OutcropGameObjectDatastore` に `SetActive` が無い）

- [x] **Step 3: 露頭datastoreにinterfaceを実装する**

`OutcropGameObjectDatastore.cs:21` のクラス宣言を置換する:

```csharp
    public class OutcropGameObjectDatastore : MonoBehaviour, IInitialEventApplyWaitTarget, ISkitWorldObjectControl
```

`using CommandForgeGenerator.Command;` を using 群へ追加し、`SearchNearestOutcrop` の下（クラス末尾）へ追加する:

```csharp
        public void SetActive(bool enable)
        {
            gameObject.SetActive(enable);
        }
```

- [x] **Step 4: SkitManagerをcomposite登録へ切り替える**

`SkitManager.cs:37` の `[Inject] private MapObjectGameObjectDatastore mapObjectGameObjectDatastore;` を削除し、代わりに `worldPins` の隣へ追加する:

```csharp
        [Inject] private IReadOnlyList<ISkitWorldObjectControl> worldObjectControls;
```

`PreProcess` 内の登録行（Task 1 で書き換えた156行付近）を置換する:

```csharp
                builder.RegisterInstance<ISkitWorldObjectControl>(new SkitWorldObjectControlGroup(worldObjectControls));
```

`using Client.Game.InGame.Map.MapObject;` が他で使われていなければ削除する（コンパイル警告ではなく未使用using整理として。使われていれば残す）。

- [x] **Step 5: MainGameStarterのDI登録に新interfaceを足す**

`MainGameStarter.cs:307-308` を置換する:

```csharp
            builder.RegisterComponent(mapObjectGameObjectDatastore).AsSelf().As<IInitialEventApplyWaitTarget>().As<ISkitWorldObjectControl>();
            builder.RegisterComponent(outcropGameObjectDatastore).AsSelf().As<IInitialEventApplyWaitTarget>().As<ISkitWorldObjectControl>();
```

`using CommandForgeGenerator.Command;` が無ければ using 群へ追加する。

- [x] **Step 6: SkitTesterへ露頭ダミーを足す**

`SkitTester.cs` の「テストシーンにmapObject/エンティティは存在しないので〜」ブロックを置換する:

```csharp
            // テストシーンにmapObject/露頭/エンティティは存在しないのでSetActive先の空オブジェクトだけ用意する
            // The test scene has no map objects, outcrops, or entities, so provide empty objects purely as SetActive targets
            var mapObjectDatastore = CreateChildComponent<MapObjectGameObjectDatastore>();
            var outcropDatastore = CreateChildComponent<OutcropGameObjectDatastore>();
            var entityObjectDatastore = CreateChildComponent<EntityObjectDatastore>();

            // RegisterComponentはビルド時に強制Resolveしサーバ応答必須のConstructを走らせるためRegisterInstanceを使う
            // RegisterComponent force-resolves at build time and would run Construct, which needs a server response, so use RegisterInstance
            builder.RegisterInstance(mapObjectDatastore).AsSelf().As<ISkitWorldObjectControl>();
            builder.RegisterInstance(outcropDatastore).AsSelf().As<ISkitWorldObjectControl>();
            builder.RegisterInstance(entityObjectDatastore);
```

using へ `using Client.Game.InGame.Map.Outcrop;` と `using CommandForgeGenerator.Command;` を追加する。

- [x] **Step 7: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0

- [x] **Step 8: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "OutcropGameObjectDatastoreSkitVisibilityTest|SkitWorldObjectControlGroupTest|SkitFailureCleanupTest"`
Expected: 全 PASS

- [x] **Step 9: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Map/Outcrop/OutcropGameObjectDatastore.cs \
        moorestech_client/Assets/Scripts/Client.Game/Skit/SkitManager.cs \
        moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs \
        moorestech_client/Assets/Scripts/Client.DebugSystem/Skit/SkitTester.cs \
        moorestech_client/Assets/Scripts/Client.Tests/Skit
git commit -m "fix(skit): 露頭をISkitWorldObjectControlへ載せスキット中も一緒に消す"
```

---

### Task 3: コマンドのフラグをworldObjectEnableへ改名する

**Files:**
- Modify: `moorestech_client/Assets/AddressableResources/Skit/commands.yaml:189, 199`
- Modify: `moorestech_client/Assets/AddressableResources/Skit/skits/100_start_game.json:132, 329`
- Modify: `moorestech_client/Assets/AddressableResources/Skit/i18n/japanese.json:188, 193-194`
- Modify: `moorestech_client/Assets/AddressableResources/Skit/i18n/english.json:133, 138-139`
- Modify: `moorestech_client/Assets/Scripts/Client.Skit/Commands/InGameObjectControlCommand.cs`（`MapObjectEnable` → `WorldObjectEnable`）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Localization/Skit/SkitLocalizationDictionaryCompletenessTest.cs:25-26`（baseline hash の更新）

**Interfaces:**
- Consumes: Task 1 で置いた `ISkitWorldObjectControl`
- Produces: 生成プロパティ `InGameObjectControlCommand.WorldObjectEnable`（`commands.yaml` から SourceGenerator が生成。`Assets/Scripts/Client.Skit/csc.rsp` の `/additionalfile:Assets/AddressableResources/Skit/commands.yaml` 経由）

- [x] **Step 1: commands.yaml を書き換える**

189行の `commandListLabelFormat` と 199行のプロパティ名を置換する:

```yaml
    commandListLabelFormat: "背景:{backgroundEnable} ブロック:{blockEnable} 世界オブジェクト:{worldObjectEnable} エンティティ:{entityEnable}"
```

```yaml
      worldObjectEnable:
        type: boolean
        required: true
```

- [x] **Step 2: スキットJSONを書き換える**

`skits/100_start_game.json` の2箇所（132行 `"mapObjectEnable": false,` / 329行 `"mapObjectEnable": true,`）をキー名だけ `"worldObjectEnable"` へ置換する。値は変えない。

```bash
sed -i '' 's/"mapObjectEnable"/"worldObjectEnable"/g' moorestech_client/Assets/AddressableResources/Skit/skits/100_start_game.json
grep -n "worldObjectEnable" moorestech_client/Assets/AddressableResources/Skit/skits/100_start_game.json
```

Expected: 2行ヒットする

- [x] **Step 3: i18n の2ファイルを書き換える**

`i18n/japanese.json`:

```json
    "command.inGameObjectControl.description": "ゲーム内の背景・ブロック・世界オブジェクト（mapObject/露頭）・エンティティの表示を制御",
    "command.inGameObjectControl.property.worldObjectEnable.name": "世界オブジェクト表示",
    "command.inGameObjectControl.property.worldObjectEnable.description": "mapObjectと露頭の表示状態",
```

`i18n/english.json`:

```json
    "command.inGameObjectControl.description": "Control in-game background, block, world object (map object / outcrop) and entity visibility",
    "command.inGameObjectControl.property.worldObjectEnable.name": "World Object Enable",
    "command.inGameObjectControl.property.worldObjectEnable.description": "Map object and outcrop visibility state",
```

キーの並び順は既存位置（`blockEnable` の次）を維持する。

- [x] **Step 4: コマンドの参照プロパティ名を変える**

`InGameObjectControlCommand.cs` の `ExecuteAsync` 内の該当行:

```csharp
            storyContext.GetService<ISkitWorldObjectControl>().SetActive(WorldObjectEnable);
```

- [x] **Step 5: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: errors 0（SourceGenerator が `WorldObjectEnable` を再生成する。`MapObjectEnable` が見つからないというエラーが出た場合は `commands.yaml` の保存漏れか Editor のリフレッシュ待ちなので、45秒待って再実行する）

- [x] **Step 6: ローカライズ辞書テストを実行して baseline のズレを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "SkitLocalizationDictionaryCompletenessTest"`
Expected: `CommandForgeDictionaryKeepsRootFlatTranslationsAndBaselineValues` が hash 不一致で FAIL する（キー名が変わったため）。失敗メッセージの Actual 値（english / japanese それぞれの64桁hex）を控える。件数（english 143 / japanese 208）は1:1リネームなので変わらない — 件数側が変わっていたらキーの追加・削除ミスなので先にそちらを直す

- [x] **Step 7: baseline hash を更新する**

`SkitLocalizationDictionaryCompletenessTest.cs:25-26` の `TestCase` 属性2行の hash を Step 6 で控えた Actual 値へ置換し、直上のコメントを実態に合わせる:

```csharp
        // count/hashはworldObjectEnableへの改名後のroot値とソート済みCommandForge key/valueを正本とする
        // Baseline is the post-worldObjectEnable-rename root values and sorted CommandForge key/value pairs
        [TestCase("english", 143, "<Step 6 で控えたenglishのhash>")]
        [TestCase("japanese", 208, "<Step 6 で控えたjapaneseのhash>")]
```

- [x] **Step 8: スキット系テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Skit"`
Expected: 全 PASS（`SkitLocalizationDictionaryCompletenessTest` の `RuntimeSkitKeysMatchAssetBasenamesAndSchemaFields` と `AllTranslationValuesAreNonEmpty` を含む）

- [x] **Step 9: 旧名の残骸が無いことを確認する**

Run: `grep -rn "mapObjectEnable\|MapObjectEnable\|ISkitMapObjectControl" --include='*.cs' --include='*.json' --include='*.yaml' . | grep -v '/Library/' | grep -v '^./docs/' | grep -v '^./.decisions/' | grep -v '^./.superpowers/'`
Expected: 0件

- [x] **Step 10: コミットする**

```bash
git add moorestech_client/Assets/AddressableResources/Skit \
        moorestech_client/Assets/Scripts/Client.Skit/Commands/InGameObjectControlCommand.cs \
        moorestech_client/Assets/Scripts/Client.Tests/Localization/Skit/SkitLocalizationDictionaryCompletenessTest.cs
git commit -m "refactor(skit): inGameObjectControlのフラグをworldObjectEnableへ改名"
```

---

### Task 4: 開幕スキットを実走して露頭が消えることを録画で確認する

**Files:**
- Create: `.claude/skills/unity-playmode-recorded-playtest/scenarios/misc/skit-opening-world-hidden.cs`

**Interfaces:**
- Consumes: Task 2・Task 3 の実装一式
- Produces: 実行成果物（`result.json` / mp4 / スクリーンショット）。コードは生まない

- [x] **Step 1: シナリオを書く**

`.claude/skills/unity-playmode-recorded-playtest/scenarios/misc/skit-opening-world-hidden.cs` を新規作成する。**開幕スキットを Skip しない**のがこのシナリオの要点:

```csharp
using Client.Game.InGame.Map.Outcrop;
using Client.Playtest;
using Client.Skit.UI;
using Cysharp.Threading.Tasks;
using UnityEngine;

var options = new PlaytestRunOptions { Record = true };
return PlaytestRunner.Run("skit-opening-world-hidden", options, async p =>
{
    await p.SetupDebugEnvironment(new PlaytestEnvironmentConfig());

    // 検証対象が開幕スキットそのものなのでSkipOpeningSkitは呼ばない
    // The opening skit itself is under test, so SkipOpeningSkit is deliberately not called
    p.Note("開幕スキットをオート送りで宇宙カットまで進める");

    var skitStore = SkitPresentationStateStore.Instance;
    await p.Until(() => skitStore.GetCurrent() != null, 60f, "スキット開始待ち");

    var started = skitStore.GetCurrent();
    skitStore.TrySetAuto(started.SessionId, started.SceneRevision, true);

    var outcropDatastore = Object.FindObjectOfType<OutcropGameObjectDatastore>(true);
    p.Assert(outcropDatastore != null, "露頭datastoreがシーンに存在する");

    // オートが効かない場合に備え、poll毎にAdvanceインテントも打つ
    // Also fire an advance intent on every poll in case auto mode does not take
    await p.Until(() =>
    {
        var current = skitStore.GetCurrent();
        if (current != null) skitStore.TryAdvance(current.SessionId, current.SceneRevision);
        return !outcropDatastore.gameObject.activeSelf;
    }, 180f, "世界非表示カットで露頭rootが非表示になる");

    await p.Screenshot("01-space-cut-without-outcrop");

    await p.Until(() =>
    {
        var current = skitStore.GetCurrent();
        if (current != null) skitStore.TryAdvance(current.SessionId, current.SceneRevision);
        return outcropDatastore.gameObject.activeSelf;
    }, 180f, "復帰カットで露頭rootが再表示される");

    await p.Screenshot("02-world-restored-with-outcrop");
});
```

- [x] **Step 2: シナリオを実行する**

Run: `.claude/skills/unity-playmode-recorded-playtest/scripts/run-scenario.sh .claude/skills/unity-playmode-recorded-playtest/scenarios/misc/skit-opening-world-hidden.cs`
（引数の渡し方が異なる場合は `.claude/skills/unity-playmode-recorded-playtest/references/run-scenario.md` に従う）
Expected: `result.json` の assert / until がすべて ok。タイムアウトで落ちる場合は `references/troubleshooting.md` を参照する

- [x] **Step 3: 録画とスクリーンショットを目視する**

`01-space-cut-without-outcrop` に露頭（岩の露出）が1つも写っていないこと、`02-world-restored-with-outcrop` で世界が戻っていることを確認する。mp4 も宇宙カット前後を再生して確認する。

- [x] **Step 4: 結果を記録してコミットする**

```bash
bd note moorestech-kvl "録画実走で確認: 宇宙カットに露頭なし / 復帰後に再表示。scenario=misc/skit-opening-world-hidden.cs"
git add .claude/skills/unity-playmode-recorded-playtest/scenarios/misc/skit-opening-world-hidden.cs
git commit -m "test(playtest): 開幕スキットの世界非表示で露頭が消えることを実走検証するシナリオを追加"
```

---

### Task 5: 全ブランチレビュー（省略不可）

**Files:** なし（レビュー実行のみ）

- [x] **Step 1: moores-code-review を実行する**

`moores-code-review` スキルを起動し、`master...fix/skit-hide-outcrop-with-world-objects` の全差分をレビューする。ゴール達成を理由に省略してはならない。

- [x] **Step 2: 指摘へ対応する**

機械的修正は適用し、設計判断は AskUserQuestion で裁定を仰ぐ。修正後は `uloop compile` と Task 1〜3 のテストを再実行する。

- [x] **Step 3: 作業を全てコミットする**

```bash
git status   # 未コミットの変更が無いことを確認
```

---

## 判断記録（ADR）

- 設計 ADR: [docs/adr/0016-skit-hides-world-objects-through-shared-interface.md](../../adr/0016-skit-hides-world-objects-through-shared-interface.md)
- 裁定レコード: `.decisions/2026-08-18-スキットの世界非表示は共通interfaceへ載せ替える.md` / `.decisions/2026-08-18-inGameObjectControlのフラグをworldObjectEnableへ改名する.md` / `.decisions/2026-08-18-スキット非表示の束にentityは含めない.md`
- 調査・タスク: bd `moorestech-kvl`（本体）/ bd `moorestech-h4j`（スコープ外に切り出したUIステートの穴）

planning中に生じた判断:

- **Task 1 でクラス宣言と SkitManager を暫定追従させる**: `ISkitMapObjectControl` を削除するとコンパイルが割れるため、interface 置換と最小追従を同一タスクに畳んだ。出所: agent前提（各タスクをコンパイルグリーンで閉じるという writing-plans の原則）
- **yaml リネームを C# 載せ替えと別タスクにした**: 名前変更は生成プロパティ経由で必ず一斉に効くため分割不能に見えるが、Task 2 までは旧プロパティ名 `MapObjectEnable` を新 interface へ流すことで両タスクとも green のまま閉じられる。出所: agent前提
- **検証シナリオで `SkipOpeningSkit()` を呼ばない**: プレイテストDSLの定型2行目だが、本件は開幕スキット自体が検証対象。出所: agent前提（ユーザー裁定「unityプレイ録画テストで実走確認する」の実現手段）
- **ローカライズ baseline hash はテスト失敗の Actual から取る**: hash は root 値＋ソート済み key/value から計算されるため手計算しない。出所: agent前提（`SkitLocalizationDictionaryCompletenessTest` の実装）
