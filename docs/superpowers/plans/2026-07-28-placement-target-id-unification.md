---
spec: docs/plans/hotbar-build-shortcut-and-equipment-slot-design.md
---

# Plan A: 設置対象IDのGuid統一 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** ビルドメニューに並ぶ全設置対象（ブロック・列車車両・接続ツール・ビルドツール・ブループリント）の識別子を Guid 1本に統一し、Guid→設置対象を解決する共有カタログを1本置く。**ゲームの見た目・挙動は変えない**（Web契約のキーとBP識別子が変わるだけ）。

**Architecture:** ①`buildMenu.yml` に `buildTools` 配列を新設しBPコピーツールをマスタ化（前例: 同ファイルの `connectTools`）②共有アセンブリ `Game.PlacementTarget` に設置対象カタログを新設し、マスタ由来エントリは共有コードで列挙、ブループリント供給だけ `IBlueprintCatalogSource` で差し替え ③ブループリントにGUIDを発行し名前を表示名へ格下げ ④Web契約 `entryType+entryKey` を `id`（Guid文字列）+`kind`（表示用・識別子ではない）へ差し替え。

**Tech Stack:** C#（Unity / MessagePack / VContainer・Microsoft.Extensions.DependencyInjection）/ Mooresmaster SourceGenerator（YAMLスキーマ→ローダー自動生成）/ TypeScript+React+zod（moorestech_web/webui）

**確認すべきドキュメント（着手前に必読）:**
- spec: `docs/plans/hotbar-build-shortcut-and-equipment-slot-design.md`（用語は `CONTEXT.md`、決定は `docs/adr/0001`）
- スキーマ編集: `/edit-schema` スキル（SourceGeneratorのトリガー方法を含む）
- テスト作成: `/creating-server-tests` スキル

## Global Constraints

- 後方互換は考慮しない。スキーマ追加は必須プロパティとし、テスト用・実運用の全JSONを一括更新する（`optional: true`・`?? Default`フォールバック・ローダー欠損補完は禁止）
- 設置対象IDは**生 `Guid`**を使う（ラッパー型は作らない。既存マスタ識別子 `connectToolGuid` 等が生Guidである前例に合わせる）
- 実行時 `BlockId` は永続・通信に使わない。永続キーは `BlockGuid`
- `Func<>` 禁止・partial 禁止・try-catch 原則禁止・単純getter/setterプロパティ禁止（値のSetは `SetHoge` メソッド）・1ファイル200行以下・イベントはUniRx
- コメントは日本語・英語の2行セット（主要処理セクションごと）
- .cs 変更後は必ず `uloop compile --project-path ./moorestech_client` を実行する
- .metaファイルは手動作成しない（Unity起動時の自動生成に任せ、生成後にコミット）
- コミットは頻繁に。各タスク末尾で必ずコミットする

## File Structure（このplanで触るファイルの全体像）

新規:
- `VanillaSchema/buildMenu.yml` … `buildTools` 配列追記（新規ファイルではなく追記）
- `moorestech_server/Assets/Scripts/Core.Master/BuildToolMaster.cs` … buildTools配列だけを読むラッパーMaster（前例: `ConnectToolMaster.cs`）
- `moorestech_server/Assets/Scripts/Game.PlacementTarget/Game.PlacementTarget.asmdef`（前例: `Game.UnlockState.asmdef`）
- `moorestech_server/Assets/Scripts/Game.PlacementTarget/PlacementTargetKind.cs`
- `moorestech_server/Assets/Scripts/Game.PlacementTarget/PlacementTargetEntry.cs`
- `moorestech_server/Assets/Scripts/Game.PlacementTarget/IBlueprintCatalogSource.cs`
- `moorestech_server/Assets/Scripts/Game.PlacementTarget/PlacementTargetCatalog.cs`
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Targets/BuildToolPlacementTarget.cs`（`BlueprintCopyToolPlacementTarget` の置き換え）
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Targets/PlacementTargetFactory.cs`

変更（主要のみ・行番号は現状）:
- `moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs:42-61` … BuildToolMaster登録
- `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintJsonObject.cs` / `IBlueprintDatastore.cs` / `BlueprintDatastore.cs:12-27` … GUID化・連番廃止
- `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintProtocol.cs` … Delete対象をGUIDへ
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Targets/IPlacementTarget.cs` ほか全実装 … `Guid Id` 追加
- `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/ClientBlueprintLibrary.cs` … Delete引数GUID化・`IBlueprintCatalogSource`実装
- `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuEntryCatalog.cs` / `Client.WebUiHost/Game/Topics/BuildMenu/WebBuildMenuEntryCatalog.cs` … 共有カタログ列挙ベースへ
- `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuEntryDtoFactory.cs:51-66` / `BuildMenuDtos.cs:21` / `Game/Actions/BuildMenuActions.cs:17,63` … id/kind契約へ
- `moorestech_web/webui/src/bridge/contract/schemas/buildMenu.ts` / `src/bridge/transport/actionContract.ts` / `src/features/buildMenu/BuildMenuSlot.tsx` / `BuildMenuPanel.tsx` / `BuildMenuCategoryGrid.tsx`
- テストJSON: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/buildMenu.json`
- 実運用JSON: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/buildMenu.json`（別リポジトリ。コミットも忘れない）

テスト:
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/BuildToolMasterTest.cs`（新規）
- `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/PlacementTargetCatalogTest.cs`（新規）
- `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BlueprintProtocolTest.cs`（既存修正）

---

### Task 1: `buildMenu.yml` に `buildTools` 配列を新設し、BuildToolMaster で読む

**Files:**
- Modify: `VanillaSchema/buildMenu.yml`（`connectTools` 配列の直後・ファイル末尾）
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/buildMenu.json`
- Modify: `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/buildMenu.json`
- Create: `moorestech_server/Assets/Scripts/Core.Master/BuildToolMaster.cs`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/MasterHolder.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Core/BuildToolMasterTest.cs`

**Interfaces:**
- Produces: `MasterHolder.BuildToolMaster`（`IReadOnlyList<BuildToolMasterElement> All`, `BuildToolMasterElement GetBuildTool(Guid buildToolGuid)`）。`BuildToolMasterElement` はSourceGenerator生成（`BuildToolGuid: Guid`, `Name: string`, `ToolType: string`）。Task 2 のカタログと Task 5 のクライアントが参照する

- [ ] **Step 1: スキーマに `buildTools` を追記する**

`/edit-schema` スキルを読んでから、`VanillaSchema/buildMenu.yml` の `connectTools` 配列定義の後（同じインデントレベル）に追記:

```yaml
- key: buildTools
  type: array
  openedByDefault: true
  overrideCodeGeneratePropertyName: BuildToolMasterElement
  items:
    type: object
    properties:
    - key: buildToolGuid
      type: uuid
      autoGenerated: true
    - key: name
      type: string
    - key: toolType
      type: enum
      options:
      - blueprintCopy
```

`optional: true` は付けない（必須化＋全JSON一括更新が正規手順）。

- [ ] **Step 2: テスト用・実運用の全 buildMenu.json に `buildTools` を追加する**

`moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/buildMenu.json` と `../moorestech_master/server_v8/mods/moorestechAlphaMod_8/master/buildMenu.json` の両方に、トップレベルキーとして追加（既存の `categories`/`connectTools` と同階層）:

```json
"buildTools": [
  {
    "buildToolGuid": "3f8f6de0-0000-4000-8000-000000000001",
    "name": "ブループリントコピー",
    "toolType": "blueprintCopy"
  }
]
```

GUIDは新規発行してよい（既存データと重複しなければ何でもよい）。テスト用と実運用で同じGUIDにする必要はない。

- [ ] **Step 3: コンパイルしてSourceGeneratorに `BuildToolMasterElement` を生成させる**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0。生成型は `Mooresmaster.Model.BuildMenuModule.BuildToolMasterElement`（`ConnectToolMasterElement` と同モジュール）

- [ ] **Step 4: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests/UnitTest/Core/BuildToolMasterTest.cs`（初期化パターンは既存 `Tests/CombinedTest/Server/PacketTest/SortInventoryProtocolTest.cs:30` を踏襲）:

```csharp
using System.Linq;
using Core.Master;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Core
{
    public class BuildToolMasterTest
    {
        [Test]
        public void BuildToolsをマスタからロードできる()
        {
            // DIコンテナ生成でMasterHolderがロードされる
            // Building the DI container loads MasterHolder
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            Assert.AreEqual(1, MasterHolder.BuildToolMaster.All.Count);
            var tool = MasterHolder.BuildToolMaster.All[0];
            Assert.AreEqual("blueprintCopy", tool.ToolType);
            Assert.AreEqual(tool.BuildToolGuid, MasterHolder.BuildToolMaster.GetBuildTool(tool.BuildToolGuid).BuildToolGuid);
        }
    }
}
```

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BuildToolMasterTest"`
Expected: FAIL（`MasterHolder.BuildToolMaster` が存在せずコンパイルエラー）

- [ ] **Step 5: BuildToolMaster を実装する**

`moorestech_server/Assets/Scripts/Core.Master/BuildToolMaster.cs`（前例 `ConnectToolMaster.cs:20-22` の「同じbuildMenu.jsonから自分の配列だけ読む」形をそのまま踏襲。コンストラクタ・Validate・Initializeのシグネチャは `ConnectToolMaster` を開いて完全に同じ形にする）:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Mooresmaster.Loader.BuildMenuModule;
using Mooresmaster.Model.BuildMenuModule;
using Newtonsoft.Json.Linq;

namespace Core.Master
{
    public class BuildToolMaster : IMaster
    {
        public IReadOnlyList<BuildToolMasterElement> All => _buildTools;
        private readonly List<BuildToolMasterElement> _buildTools;
        private readonly Dictionary<Guid, BuildToolMasterElement> _byGuid = new();

        public BuildToolMaster(JToken buildMenuJToken)
        {
            // buildMenu.jsonのbuildTools配列だけを読む
            // Load only the buildTools array from buildMenu.json
            _buildTools = BuildMenuLoader.Load(buildMenuJToken).BuildTools.ToList();
        }

        public BuildToolMasterElement GetBuildTool(Guid buildToolGuid)
        {
            return _byGuid[buildToolGuid];
        }

        // ConnectToolMasterと同じIMasterライフサイクル（Validate/Initialize）を実装する
        // Implement the same IMaster lifecycle (Validate/Initialize) as ConnectToolMaster
    }
}
```

`IMaster` の `Validate`/`Initialize` メソッドシグネチャは `ConnectToolMaster.cs` を開いて同じ形で実装（`Initialize` で `_byGuid` を構築、`Validate` でGuid重複を検出）。`MasterHolder.cs` には `ConnectToolMaster` の登録行（42行・57行付近）の直後に同じ形で `BuildToolMaster` を追加する（同じ `JsonFileName("buildMenu")` を渡す）。

- [ ] **Step 6: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BuildToolMasterTest"`
Expected: PASS

- [ ] **Step 7: コミットする**

```bash
git add VanillaSchema/buildMenu.yml moorestech_server/Assets/Scripts/Core.Master/ moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/buildMenu.json moorestech_server/Assets/Scripts/Tests/UnitTest/Core/BuildToolMasterTest.cs
git commit -m "feat: buildMenu.ymlにbuildTools配列を新設しBuildToolMasterで読む"
cd ../moorestech_master && git add server_v8/mods/moorestechAlphaMod_8/master/buildMenu.json && git commit -m "feat: buildToolsにブループリントコピーを追加" && cd -
```

---

### Task 2: 共有アセンブリ `Game.PlacementTarget` に設置対象カタログを新設する

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.PlacementTarget/Game.PlacementTarget.asmdef`
- Create: `moorestech_server/Assets/Scripts/Game.PlacementTarget/PlacementTargetKind.cs`
- Create: `moorestech_server/Assets/Scripts/Game.PlacementTarget/PlacementTargetEntry.cs`
- Create: `moorestech_server/Assets/Scripts/Game.PlacementTarget/IBlueprintCatalogSource.cs`
- Create: `moorestech_server/Assets/Scripts/Game.PlacementTarget/PlacementTargetCatalog.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/PlacementTargetCatalogTest.cs`

**Interfaces:**
- Consumes: Task 1 の `MasterHolder.BuildToolMaster`
- Produces:
  - `enum PlacementTargetKind { Block, TrainCar, ConnectTool, BuildTool, Blueprint }`
  - `readonly struct PlacementTargetEntry { Guid Id; PlacementTargetKind Kind; string DisplayName; }`
  - `interface IBlueprintCatalogSource { IReadOnlyList<(Guid id, string name)> BlueprintEntries { get; } }`
  - `class PlacementTargetCatalog { PlacementTargetCatalog(IBlueprintCatalogSource blueprintSource); IReadOnlyList<PlacementTargetEntry> Entries { get; } bool TryGetEntry(Guid id, out PlacementTargetEntry entry); }`
  - Task 4 でサーバDI登録、Task 5 でクライアントから参照、plan C のホットバー割当検証が使う

- [ ] **Step 1: asmdef を作る**

`moorestech_server/Assets/Scripts/Game.PlacementTarget/Game.PlacementTarget.asmdef`（前例 `Game.UnlockState.asmdef` と同形式）:

```json
{
    "name": "Game.PlacementTarget",
    "rootNamespace": "",
    "references": [
        "Core.Master"
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

.metaは作らない（後でUnityが生成したものをコミット）。

- [ ] **Step 2: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests/UnitTest/Game/PlacementTargetCatalogTest.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.PlacementTarget;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game
{
    public class PlacementTargetCatalogTest
    {
        private class EmptyBlueprintSource : IBlueprintCatalogSource
        {
            public IReadOnlyList<(Guid id, string name)> BlueprintEntries => new List<(Guid, string)>();
        }

        [Test]
        public void マスタ由来の設置対象がGuidで解決できる()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var catalog = new PlacementTargetCatalog(new EmptyBlueprintSource());

            // ブロック・車両・接続ツール・ビルドツールが全部エントリに入っている
            // Blocks, train cars, connect tools, and build tools are all present
            Assert.IsTrue(catalog.Entries.Any(e => e.Kind == PlacementTargetKind.Block));
            Assert.IsTrue(catalog.Entries.Any(e => e.Kind == PlacementTargetKind.ConnectTool));
            Assert.IsTrue(catalog.Entries.Any(e => e.Kind == PlacementTargetKind.BuildTool));

            // 任意のエントリはTryGetEntryで往復できる
            // Every entry round-trips through TryGetEntry
            foreach (var entry in catalog.Entries)
            {
                Assert.IsTrue(catalog.TryGetEntry(entry.Id, out var resolved));
                Assert.AreEqual(entry.Kind, resolved.Kind);
            }

            // 未知のGuidは解決できない
            // Unknown GUIDs do not resolve
            Assert.IsFalse(catalog.TryGetEntry(Guid.NewGuid(), out _));
        }
    }
}
```

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementTargetCatalogTest"`
Expected: FAIL（型が存在せずコンパイルエラー）

- [ ] **Step 3: 型とカタログを実装する**

`PlacementTargetKind.cs`:

```csharp
namespace Game.PlacementTarget
{
    public enum PlacementTargetKind
    {
        Block,
        TrainCar,
        ConnectTool,
        BuildTool,
        Blueprint,
    }
}
```

`PlacementTargetEntry.cs`:

```csharp
using System;

namespace Game.PlacementTarget
{
    public readonly struct PlacementTargetEntry
    {
        public readonly Guid Id;
        public readonly PlacementTargetKind Kind;
        public readonly string DisplayName;

        public PlacementTargetEntry(Guid id, PlacementTargetKind kind, string displayName)
        {
            Id = id;
            Kind = kind;
            DisplayName = displayName;
        }
    }
}
```

`IBlueprintCatalogSource.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Game.PlacementTarget
{
    // ブループリントの供給元だけサーバ/クライアントで差し替える
    // Only the blueprint source differs between server and client
    public interface IBlueprintCatalogSource
    {
        IReadOnlyList<(Guid id, string name)> BlueprintEntries { get; }
    }
}
```

`PlacementTargetCatalog.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Master;

namespace Game.PlacementTarget
{
    public class PlacementTargetCatalog
    {
        private readonly IBlueprintCatalogSource _blueprintSource;

        public PlacementTargetCatalog(IBlueprintCatalogSource blueprintSource)
        {
            _blueprintSource = blueprintSource;
        }

        public IReadOnlyList<PlacementTargetEntry> Entries
        {
            get
            {
                // マスタ由来のエントリを列挙し、末尾に現在のブループリントを足す
                // Enumerate master-derived entries, then append current blueprints
                var entries = new List<PlacementTargetEntry>();
                foreach (var block in MasterHolder.BlockMaster.Blocks.Data)
                    entries.Add(new PlacementTargetEntry(block.BlockGuid, PlacementTargetKind.Block, block.Name));
                foreach (var trainCar in MasterHolder.TrainUnitMaster.Train.TrainCars)
                    entries.Add(new PlacementTargetEntry(trainCar.TrainCarGuid, PlacementTargetKind.TrainCar, trainCar.Name));
                foreach (var connectTool in MasterHolder.ConnectToolMaster.All)
                    entries.Add(new PlacementTargetEntry(connectTool.ConnectToolGuid, PlacementTargetKind.ConnectTool, connectTool.Name));
                foreach (var buildTool in MasterHolder.BuildToolMaster.All)
                    entries.Add(new PlacementTargetEntry(buildTool.BuildToolGuid, PlacementTargetKind.BuildTool, buildTool.Name));
                foreach (var (id, name) in _blueprintSource.BlueprintEntries)
                    entries.Add(new PlacementTargetEntry(id, PlacementTargetKind.Blueprint, name));
                return entries;
            }
        }

        public bool TryGetEntry(Guid id, out PlacementTargetEntry entry)
        {
            foreach (var e in Entries)
            {
                if (e.Id != id) continue;
                entry = e;
                return true;
            }
            entry = default;
            return false;
        }
    }
}
```

**注意（実装時に必ず確認）:** ブロックの列挙条件は、既存の `WebBuildMenuEntryCatalog.CreateEntries`（`Client.WebUiHost/Game/Topics/BuildMenu/WebBuildMenuEntryCatalog.cs:23`）と `BuildMenuEntryCatalog.CreateEntries`（`Client.Game/InGame/UI/BuildMenu/BuildMenuEntryCatalog.cs:25`）を両方開き、`MasterHolder.BlockMaster.Blocks.Data` に対するフィルタ（あれば）を**そのまま**カタログへ移すこと。ベルトの坂のようにビルドメニューに出ないブロックが既存実装で除外されているなら、その条件がカタログの「含まない」の定義になる。フィルタが存在しない（全ブロック列挙）ならカタログも全ブロックでよい。この確認結果をカタログのコメントに1行残す。プロパティ名（`TrainCarGuid`/`Name` 等）はコンパイルエラーが出たら生成コード（`Mooresmaster.Model.*`）の実名に合わせる。

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementTargetCatalogTest"`
Expected: PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.PlacementTarget/ moorestech_server/Assets/Scripts/Tests/UnitTest/Game/PlacementTargetCatalogTest.cs
git commit -m "feat: 設置対象カタログGame.PlacementTargetを新設"
```

---

### Task 3: ブループリントのGUID化（連番付与の廃止・削除/参照のGUID化）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintJsonObject.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Blueprint/IBlueprintDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintDatastore.cs:12-27`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintProtocol.cs`（Delete分岐と`BlueprintMessagePack`）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/ClientBlueprintLibrary.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BlueprintProtocolTest.cs`（既存修正＋追加）

**Interfaces:**
- Produces:
  - `BlueprintJsonObject.BlueprintGuid: Guid`（`"guid"` JsonProperty、文字列保持＋Guidプロパティの `BlockGuidStr` と同形式）
  - `IBlueprintDatastore.Register(BlueprintJsonObject)` は**登録したGuidを返す**（`Guid Register(...)`）。名前は加工しない
  - `IBlueprintDatastore.Delete(Guid blueprintGuid)`、`TryGet(Guid, out BlueprintJsonObject)`
  - `ClientBlueprintLibrary.DeleteBlueprint(Guid blueprintGuid, CancellationToken ct)`
  - BlueprintのMessagePackにGuid文字列フィールド追加（Task 6のWeb契約とplan Cの割当検証が使う）

- [ ] **Step 1: 失敗するテストを書く**

`BlueprintProtocolTest.cs` に追加（既存テストの初期化・Create操作の呼び出しパターンをそのまま流用する）:

```csharp
[Test]
public void 同名ブループリントは連番なしでそのまま登録されGuidで区別される()
{
    // 既存テストと同じ初期化でdatastoreを取得する
    // Use the same initialization as existing tests to get the datastore
    var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
    var datastore = serviceProvider.GetService<IBlueprintDatastore>();

    var guid1 = datastore.Register(new BlueprintJsonObject("同じ名前", new List<BlueprintBlockJsonObject>()));
    var guid2 = datastore.Register(new BlueprintJsonObject("同じ名前", new List<BlueprintBlockJsonObject>()));

    // 名前は加工されず同名2件が共存し、Guidは異なる
    // Names are untouched; two same-name entries coexist with distinct GUIDs
    Assert.AreEqual(2, datastore.Blueprints.Count(b => b.Name == "同じ名前"));
    Assert.AreNotEqual(guid1, guid2);

    // Guidで片方だけ削除できる
    // Deleting by GUID removes exactly one
    Assert.IsTrue(datastore.Delete(guid1));
    Assert.AreEqual(1, datastore.Blueprints.Count(b => b.Name == "同じ名前"));
}

[Test]
public void Guid無しの旧セーブはロード時にGuidが発行される()
{
    var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
    var datastore = serviceProvider.GetService<IBlueprintDatastore>();

    // Guid未設定（旧セーブ相当）のオブジェクトをロードする
    // Load an object without a GUID (legacy save)
    var legacy = new BlueprintJsonObject("旧BP", new List<BlueprintBlockJsonObject>());
    datastore.LoadBlueprints(new List<BlueprintJsonObject> { legacy });

    Assert.AreNotEqual(Guid.Empty, datastore.Blueprints[0].BlueprintGuid);
}
```

`BlueprintJsonObject` の既存コンストラクタ引数が上記と違う場合は実物に合わせる（テストの意図は変えない）。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintProtocolTest"`
Expected: FAIL（`Register` の戻り値型・`Delete(Guid)`・`BlueprintGuid` が無くコンパイルエラー）

- [ ] **Step 2: サーバ側を実装する**

`BlueprintJsonObject.cs` — `BlockGuidStr`/`BlockGuid`（同ファイル25行付近）と同じ「文字列で保持しGuidプロパティで読む」形式でGUIDを追加。`SetBlueprintGuid(Guid)` メソッドを持たせる（単純setter禁止のため）:

```csharp
[JsonProperty("guid")] public string BlueprintGuidStr { get; private set; }
public Guid BlueprintGuid => string.IsNullOrEmpty(BlueprintGuidStr) ? Guid.Empty : Guid.Parse(BlueprintGuidStr);

public void SetBlueprintGuid(Guid guid)
{
    BlueprintGuidStr = guid.ToString();
}
```

`BlueprintDatastore.cs` — `Register` の連番whileループ（12-27行）を**削除**し、GUID発行に置き換え:

```csharp
public Guid Register(BlueprintJsonObject blueprint)
{
    // 名前は加工せずGuidを発行して登録する
    // Register without renaming; issue a GUID as the identity
    var guid = Guid.NewGuid();
    blueprint.SetBlueprintGuid(guid);
    _blueprints.Add(blueprint);
    return guid;
}

public bool Delete(Guid blueprintGuid)
{
    var index = _blueprints.FindIndex(b => b.BlueprintGuid == blueprintGuid);
    if (index < 0) return false;
    _blueprints.RemoveAt(index);
    return true;
}
```

`LoadBlueprints` — ロード時、`BlueprintGuid == Guid.Empty` のものにだけ `SetBlueprintGuid(Guid.NewGuid())` で発行する（ユーザー生成データの補完であり、マスタ由来値のフォールバック禁止とは別物。spec判断記録に裁定済み）。

`IBlueprintDatastore.cs` — シグネチャを `Guid Register(...)` / `bool Delete(Guid)` に変更し、`bool TryGet(Guid blueprintGuid, out BlueprintJsonObject blueprint)` を追加。

`BlueprintProtocol.cs` — Delete分岐のリクエストフィールドを名前文字列からGuid文字列に変更し、`_blueprintDatastore.Delete(Guid.Parse(...))` を呼ぶ。応答・`BlueprintMessagePack` にGuid文字列フィールド（`[Key(n)] public string BlueprintGuidStr`）を追加。Createの応答は「登録された名前」ではなく「発行されたGuid」を返す。

- [ ] **Step 3: サーバテストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlueprintProtocolTest"`
Expected: PASS（既存のCreate/GetAll/Deleteテストが名前ベース前提で落ちる場合は、テストの検証をGuidベースに書き換える。「同名で登録すると ` (2)` が付く」ことを検証する既存テストがあれば**削除**する — 仕様自体が廃止のため）

- [ ] **Step 4: クライアント側を追随させる**

`ClientBlueprintLibrary.cs`:
- `DeleteBlueprint(string name, ...)` → `DeleteBlueprint(Guid blueprintGuid, ...)`
- `CreateBlueprint` の戻り値 `(bool success, string registeredName)` → `(bool success, Guid blueprintGuid)`
- 呼び出し側（コンパイルエラーになった箇所すべて。BPコピーUI・`BlueprintPlacementTarget` 生成箇所・`BuildMenuActions.BlueprintDeleteActionHandler`）を追随。`BuildMenuActions` のpayload変更はTask 6でまとめて行うため、ここでは一時的に `Guid.Parse(name)` 等の橋渡しはせず、**Task 6と同時にコンパイルが通ればよい順序で作業してもよい**（その場合Step 5のコンパイル確認はTask 6完了時に行う）

- [ ] **Step 5: コンパイル確認とコミット**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

```bash
git add moorestech_server/Assets/Scripts/Game.Blueprint/ moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/BlueprintProtocol.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/ moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/BlueprintProtocolTest.cs
git commit -m "feat: ブループリントをGUID識別に統一し同名連番付与を廃止"
```

---

### Task 4: サーバ側カタログのDI配線（BlueprintDatastoreを供給元にする）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Blueprint/BlueprintDatastore.cs`（`IBlueprintCatalogSource` 実装追加）
- Modify: `moorestech_server/Assets/Scripts/Game.Blueprint/Game.Blueprint.asmdef`（`Game.PlacementTarget` 参照追加）
- Modify: サーバDI登録（`MoorestechServerDIContainerGenerator` — `grep -rn "IBlueprintDatastore" moorestech_server/Assets/Scripts/Server.Boot/` で登録箇所を特定し、同じ場所に追加）
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/PlacementTargetCatalogTest.cs`（追加）

**Interfaces:**
- Consumes: Task 2 の `PlacementTargetCatalog` / `IBlueprintCatalogSource`、Task 3 の `BlueprintDatastore`
- Produces: DIから `PlacementTargetCatalog` が取得可能になる（plan Cのホットバー割当検証が消費）

- [ ] **Step 1: 失敗するテストを書く**

`PlacementTargetCatalogTest.cs` に追加:

```csharp
[Test]
public void サーバDIのカタログはBlueprintDatastoreのBPを含む()
{
    var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
    var datastore = serviceProvider.GetService<IBlueprintDatastore>();
    var catalog = serviceProvider.GetService<PlacementTargetCatalog>();

    var guid = datastore.Register(new BlueprintJsonObject("カタログ確認用", new List<BlueprintBlockJsonObject>()));

    Assert.IsTrue(catalog.TryGetEntry(guid, out var entry));
    Assert.AreEqual(PlacementTargetKind.Blueprint, entry.Kind);
}
```

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementTargetCatalogTest"`
Expected: FAIL（DIに `PlacementTargetCatalog` が未登録で null）

- [ ] **Step 2: 実装する**

- `Game.Blueprint.asmdef` の `references` に `"Game.PlacementTarget"` を追加
- `BlueprintDatastore` に `IBlueprintCatalogSource` を実装:

```csharp
public IReadOnlyList<(Guid id, string name)> BlueprintEntries =>
    _blueprints.Select(b => (b.BlueprintGuid, b.Name)).ToList();
```

- DI登録箇所（`IBlueprintDatastore` を `AddSingleton` している行の近く）に追加。登録形式は同ファイルの既存行に完全に合わせる:

```csharp
services.AddSingleton<IBlueprintCatalogSource>(sp => (BlueprintDatastore)sp.GetService<IBlueprintDatastore>());
services.AddSingleton<PlacementTargetCatalog>();
```

（`IBlueprintDatastore` の登録が具象 `BlueprintDatastore` の `AddSingleton` 経由なら、キャストではなくその形に合わせる）

- [ ] **Step 3: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementTargetCatalogTest"`
Expected: PASS（Task 2のテストも含め全件）

- [ ] **Step 4: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Blueprint/ moorestech_server/Assets/Scripts/Server.Boot/ moorestech_server/Assets/Scripts/Tests/UnitTest/Game/PlacementTargetCatalogTest.cs
git commit -m "feat: サーバDIにPlacementTargetCatalogを配線しBP供給元をBlueprintDatastoreに"
```

---

### Task 5: クライアント側の統一 — `IPlacementTarget.Id`・PlacementTargetFactory・カタログ列挙への一本化

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Targets/IPlacementTarget.cs`
- Modify: 同 `Targets/BlockPlacementTarget.cs` / `ConnectToolPlacementTarget.cs` / `TrainCarPlacementTarget.cs` / `BlueprintPlacementTarget.cs`
- Delete: 同 `Targets/BlueprintCopyToolPlacementTarget.cs` → Create: `Targets/BuildToolPlacementTarget.cs`
- Create: 同 `Targets/PlacementTargetFactory.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Blueprint/ClientBlueprintLibrary.cs`（`IBlueprintCatalogSource` 実装）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/BuildMenu/BuildMenuEntryCatalog.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/WebBuildMenuEntryCatalog.cs`
- Modify: `Client.Game` / `Client.WebUiHost` の asmdef（`Game.PlacementTarget` 参照追加）

**Interfaces:**
- Consumes: Task 2 の `PlacementTargetCatalog`/`PlacementTargetEntry`/`PlacementTargetKind`、Task 3 のGuid化済みBP
- Produces:
  - `IPlacementTarget` に `Guid Id { get; }` 追加（全実装が設置対象IDを返す。plan Cの割当保存が使う）
  - `BlueprintPlacementTarget(Guid blueprintGuid, string displayName)`（名前は表示用に格下げ）
  - `BuildToolPlacementTarget(Guid buildToolGuid)`
  - `static class PlacementTargetFactory { static bool TryCreate(PlacementTargetEntry entry, out IPlacementTarget target); }`（Guid→設置対象の唯一のクライアント側解決点。plan Cのホットバー呼び出しが使う）

- [ ] **Step 1: IPlacementTargetにIdを追加し全実装を更新する**

`IPlacementTarget.cs`:

```csharp
public interface IPlacementTarget : IEquatable<IPlacementTarget>
{
    // 設置対象ID。種別を問わずGuid1本で識別する（ADR-0001）
    // Placement target id: a single GUID regardless of kind (ADR-0001)
    Guid Id { get; }
}
```

各実装:
- `BlockPlacementTarget` — `public Guid Id => MasterHolder.BlockMaster.GetBlockMaster(BlockId).BlockGuid;`（`GetBlockMaster` の実名はBlockMasterを開いて確認。実行時BlockIdは保持したままでよいが、**IdはBlockGuid**）。`PickedDirection` はそのまま（設置操作中の一時状態。Idには含めない）
- `ConnectToolPlacementTarget` — `public Guid Id => ConnectToolGuid;`
- `TrainCarPlacementTarget` — `public Guid Id => TrainCarGuid;`
- `BlueprintPlacementTarget` — フィールドを `BlueprintGuid`＋`DisplayName` に変更。`Id => BlueprintGuid`。`Equals` はGuid比較に変更。生成箇所（コンパイルエラー箇所）を追随
- `BuildToolPlacementTarget`（新規、旧 `BlueprintCopyToolPlacementTarget` を置換）:

```csharp
using System;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class BuildToolPlacementTarget : IPlacementTarget
    {
        public Guid Id { get; }

        public BuildToolPlacementTarget(Guid buildToolGuid)
        {
            Id = buildToolGuid;
        }

        public bool Equals(IPlacementTarget other)
        {
            return other is BuildToolPlacementTarget o && o.Id == Id;
        }
    }
}
```

`BlueprintCopyToolPlacementTarget` の参照箇所（`PlaceSystemSelector` のBPコピーシステム選択分岐・`BuildMenuEntryCatalog` の固定エントリ等、コンパイルエラーになった全箇所）を `BuildToolPlacementTarget` に置換。BPコピーのGuidは `MasterHolder.BuildToolMaster` から `ToolType == "blueprintCopy"` の要素を引く。

- [ ] **Step 2: PlacementTargetFactoryを実装する**

`Targets/PlacementTargetFactory.cs`:

```csharp
using System;
using Game.PlacementTarget;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public static class PlacementTargetFactory
    {
        // カタログエントリからIPlacementTargetを生成する唯一の解決点
        // The single resolution point from catalog entry to IPlacementTarget
        public static bool TryCreate(PlacementTargetEntry entry, out IPlacementTarget target)
        {
            switch (entry.Kind)
            {
                case PlacementTargetKind.Block:
                    target = new BlockPlacementTarget(MasterHolder.BlockMaster.GetBlockId(entry.Id), null);
                    return true;
                case PlacementTargetKind.TrainCar:
                    target = new TrainCarPlacementTarget(entry.Id);
                    return true;
                case PlacementTargetKind.ConnectTool:
                    target = new ConnectToolPlacementTarget(entry.Id);
                    return true;
                case PlacementTargetKind.BuildTool:
                    target = new BuildToolPlacementTarget(entry.Id);
                    return true;
                case PlacementTargetKind.Blueprint:
                    target = new BlueprintPlacementTarget(entry.Id, entry.DisplayName);
                    return true;
                default:
                    target = null;
                    return false;
            }
        }
    }
}
```

（`MasterHolder.BlockMaster.GetBlockId(Guid)` の実名は `BlockMaster` を開いて確認。BlockGuid→BlockId変換APIが無ければ `BlockMaster` に追加せず、既存の変換箇所を検索してその方法に合わせる）

- [ ] **Step 3: 2つのビルドメニューカタログを共有カタログ列挙ベースに書き換える**

- `ClientBlueprintLibrary` に `IBlueprintCatalogSource` を実装（`Blueprints` キャッシュから `(BlueprintGuid, Name)` を返す）
- クライアント側DIに `PlacementTargetCatalog` を登録する（`MainGameStarter` の `ClientBlueprintLibrary` 登録行（200行付近）の隣で、`ClientBlueprintLibrary` を供給元にして生成・登録。plan Cの `_placementTargetCatalog` 注入が前提とする — シミュレーター指摘 2026-07-28）
- `BuildMenuEntryCatalog.CreateEntries` / `WebBuildMenuEntryCatalog.CreateEntries` の「5種を固定順で列挙する」部分を、`new PlacementTargetCatalog(clientBlueprintLibrary).Entries` → `PlacementTargetFactory.TryCreate` の列挙に置き換える。アイコン解決・アンロック判定・カテゴリ解決の各処理は現行ロジックを維持（見た目は不変）
- `Client.Game.asmdef` / `Client.WebUiHost.asmdef` の `references` に `"Game.PlacementTarget"` を追加

- [ ] **Step 4: コンパイルとリグレッション確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BuildMenu|Blueprint|PlacementTarget"`
Expected: PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/ moorestech_client/Assets/Scripts/Client.WebUiHost/
git commit -m "feat: IPlacementTargetにGuid Idを導入しビルドメニュー列挙を共有カタログに一本化"
```

---

### Task 6: Web契約の差し替え — `entryType+entryKey` → `id`+`kind`

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuEntryDtoFactory.cs:51-66`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/BuildMenu/BuildMenuDtos.cs:21`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/BuildMenuActions.cs`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/buildMenu.ts`
- Modify: `moorestech_web/webui/src/bridge/transport/actionContract.ts:26,68`
- Modify: `moorestech_web/webui/src/features/buildMenu/BuildMenuSlot.tsx` / `BuildMenuPanel.tsx` / `BuildMenuCategoryGrid.tsx`

**Interfaces:**
- Consumes: Task 5 の `IPlacementTarget.Id`
- Produces: Web契約 `BuildMenuEntryDto { Id: string(Guid), Kind: "block"|"trainCar"|"connectTool"|"buildTool"|"blueprint", Label, Category, SubCategory, RequiredItems, IconUrl }`。アクション `build_menu.select {id: string}`・`blueprint.delete {id: string}`（plan CのD&D割当が `id` を再利用する）

- [ ] **Step 1: webui側の失敗するテストを書く**

`moorestech_web/webui/src/bridge/contract/schemas/buildMenu.test.ts`（新規。既存のスキーマテストがあればそのファイルに追加）:

```typescript
import { describe, expect, it } from "vitest";
import { BuildMenuEntryDataSchema } from "./buildMenu";

describe("BuildMenuEntryDataSchema", () => {
  it("id+kind契約をパースできる", () => {
    const entry = BuildMenuEntryDataSchema.parse({
      id: "3f8f6de0-0000-4000-8000-000000000001",
      kind: "buildTool",
      label: "ブループリントコピー",
      category: "ツール",
      subCategory: "ツール",
      requiredItems: [],
    });
    expect(entry.id).toBe("3f8f6de0-0000-4000-8000-000000000001");
  });

  it("旧entryType/entryKey契約は拒否する", () => {
    expect(() =>
      BuildMenuEntryDataSchema.parse({
        entryType: "block",
        entryKey: "1",
        label: "x",
        category: "c",
        subCategory: "s",
        requiredItems: [],
      })
    ).toThrow();
  });
});
```

Run: `cd moorestech_web/webui && npm test`
Expected: FAIL（スキーマがまだ entryType/entryKey）

- [ ] **Step 2: webuiスキーマとコンポーネントを書き換える**

`buildMenu.ts`:

```typescript
export const BuildMenuEntryKindSchema = z.enum([
  "block",
  "trainCar",
  "connectTool",
  "buildTool",
  "blueprint",
]);

export const BuildMenuEntryDataSchema = z.object({
  id: z.string(),
  kind: BuildMenuEntryKindSchema,
  label: z.string(),
  category: z.string(),
  subCategory: z.string(),
  requiredItems: z.array(RequiredItemSchema),
  iconUrl: z.string().optional(),
});
```

（`RequiredItemSchema`・`BuildMenuCategorySchema`・`BuildMenuDataSchema` は現行のまま）

- `actionContract.ts` — `"build_menu.select": { entryType; entryKey }` → `{ id: string }`、`"blueprint.delete": { name: string }` → `{ id: string }`
- `BuildMenuSlot.tsx` — testId・tutorialAnchor の組み立てを `entry.kind`+`entry.id` ベースへ（`entryType`/`entryKey` の参照を全置換）
- `BuildMenuPanel.tsx` — hover一致判定と `dispatchAction("build_menu.select", { id: entry.id })`
- `BuildMenuCategoryGrid.tsx` — Reactのkeyを `entry.id` へ、blueprint右クリック削除を `dispatchAction("blueprint.delete", { id: entry.id })` へ

Run: `cd moorestech_web/webui && npm test`
Expected: PASS

- [ ] **Step 3: Unity側のDto・アクションを書き換える**

- `BuildMenuDtos.cs` — `EntryType`/`EntryKey` プロパティを `Id`/`Kind` に変更
- `BuildMenuEntryDtoFactory.cs` — `GetEntryTypeName`/`GetEntryKey` を削除し、次の2つに置換:

```csharp
// 設置対象IDはGuid文字列1本。kindは表示・振る舞い用で識別子ではない
// The id is a single GUID string; kind is for display/behavior, not identity
public static string GetId(IPlacementTarget target)
{
    return target.Id.ToString();
}

public static string GetKind(IPlacementTarget target)
{
    return target switch
    {
        BlockPlacementTarget => "block",
        TrainCarPlacementTarget => "trainCar",
        ConnectToolPlacementTarget => "connectTool",
        BuildToolPlacementTarget => "buildTool",
        BlueprintPlacementTarget => "blueprint",
        _ => target.GetType().Name,
    };
}
```

- `BuildMenuActions.cs` — `BuildMenuSelectActionHandler` はpayloadの `id` をGuidパースし、現在カタログのエントリと `entry.Target.Id` 一致で照合（stale拒否ロジックは維持）。`BlueprintDeleteActionHandler` は `{id}` を受けて `ClientBlueprintLibrary.DeleteBlueprint(Guid.Parse(id))` へ

- [ ] **Step 4: コンパイル・全面リグレッション**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0
Run: `cd moorestech_web/webui && npm run build && npm test`
Expected: PASS（tscの型エラー0。`entryType`/`entryKey` の残存参照はここで全部露出する）
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BuildMenu|Blueprint"`
Expected: PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost/ moorestech_web/webui/src/
git commit -m "feat: Web契約をentryType+entryKeyからid(Guid)+kindへ差し替え"
```

---

### Task 7: 最終確認 — moores-code-review

- [ ] **Step 1: 全テスト＋コンパイルの最終確認**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BuildTool|PlacementTarget|Blueprint|BuildMenu"`
Run: `cd moorestech_web/webui && npm run build && npm test`
Expected: すべてPASS・エラー0

- [ ] **Step 2: 未コミット作業が無いことを確認してコミットする**

```bash
git status --short   # 空であること（.metaの追加があれば git add してコミット）
```

- [ ] **Step 3: moores-code-reviewスキルで全ブランチレビューを実行する**

必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。

---

## 判断記録（ADR）

specの台帳: `docs/plans/hotbar-build-shortcut-and-equipment-slot-design.md` の「判断記録（ADR）」を参照（設置対象ID統一・buildToolsマスタ化・カタログ共有アセンブリ・BP名格下げはすべて裁定済み）。

planning中に新たに生じた判断:
- **設置対象IDは生 `Guid` を使いラッパー型を作らない**（出所: agent前提（拒否権つき））。既存マスタ識別子（`connectToolGuid` 等）が生Guidで扱われている前例に合わせた。UnitGenerator型（`ItemId`/`BlockId`）は実行時intの前例でありGuidの前例ではない
- **`IPlacementTarget` に `Guid Id` プロパティを追加する**（出所: agent前提（拒否権つき））。割当保存（plan C）・Web契約・スポイト後の割当がすべて「現在の設置対象のID」を必要とするため、各実装が自分のIDを知る形が最小。カタログ側だけにマッピングを持つ案は逆引き（target→Guid）の二重管理になるため不採用
- **カタログの `Entries` は毎回列挙で構築する**（出所: agent前提（拒否権つき））。BPの増減に自動追従し、キャッシュ無効化の状態管理を持たない。ビルドメニュー表示・割当検証の頻度では性能問題にならない（パフォーマンス最適化は考慮不要の方針）
- **旧 `BlueprintCopyToolPlacementTarget` は `BuildToolPlacementTarget` に改名・置換**（出所: agent前提（拒否権つき））。CONTEXT.mdの用語「ビルドツール」に一致させる
- **クライアント側DIにも `PlacementTargetCatalog` を登録する（`MainGameStarter`、供給元 `ClientBlueprintLibrary`）**（出所: シミュレーター予測 2026-07-28 → 適用。plan Cの注入前提を明示化）
