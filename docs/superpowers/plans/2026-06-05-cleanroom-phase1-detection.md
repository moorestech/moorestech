# クリーンルーム フェーズ1（境界ブロック＋3D密閉部屋検出）実装プラン

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **moorestech 固有の必須ルール:**
> - `.cs` 編集後は必ず `uloop compile --project-path ./moorestech_client` でコンパイル確認する。
> - テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"` で実行（クライアントプロジェクトからサーバーテストも走る）。
> - 新規サーバー `.cs`／新規 asmdef を認識させるには Unity の **再起動**が要る場合がある（Refresh では不足）。「型が見つからない」で失敗したら uloop で Unity 再起動してから再試行。
> - blockType スキーマ追加・SourceGenerator の手順は `edit-schema` スキルに従う。テスト作成は `creating-server-tests` スキルに従う。
> - 非ASCIIファイル編集時は AGENTS.md の「文字化け防止ワークフロー」を順守。
> - **APIシグネチャ確認の原則:** 本プランのコードは既存コードベースのパターンから書いているが、メソッド名・名前空間・引数順は推定を含む。各 `.cs` を書く前に、本文で「確認」と指示した既存ファイルを開いて実シグネチャに合わせること。コンパイル/テストのチェックポイントが安全網。

**Goal:** クリーンルーム境界ブロック（壁/ドア/ハッチ/パイプコネクタ）を定義し、それらで完全に囲まれた3D空間を「クリーンルーム」として検出・登録し、ブロックの設置/破壊に追従して部屋の成立/無効化を更新するサーバー側システムを実装する。フェーズ2以降（純度・クラス・清浄機・製造機・I/O）が乗る土台を作る。

**Architecture:** 新しい世界レベルのシングルトン `CleanRoomDetectionSystem`（DI singleton）が、`WorldBlockUpdateEvent` の設置/破壊のうち**境界ブロックの変更だけ**を購読して geometry-dirty を立て、`GameUpdater` の tick で再検出する。検出器 `CleanRoomDetector` は `BlockMasterDictionary` から構築した境界セル集合と**その境界AABB**を使い、AABB外縁に漏れない密閉空間を6近傍 flood-fill して `CleanRoom`（Id, セル集合, 体積V, 表面積S, 有効フラグ）を作る。部屋はブロックから導出可能な派生状態なのでフェーズ1ではセーブせず、ロード後の再検出で再構築する。フェーズ2で純度状態を部屋に持たせる際は、公開した `Cells` を使って再検出前後の部屋をセル重なりで対応付ける（本プランは Id を永続キーにしない）。

**Tech Stack:** C# (Unity, moorestech_server), R3/UniRx `IObservable`（`GameUpdater.UpdateObservable`）, NUnit (Server.Tests), Mooresmaster Source Generator (blocks.yml → BlocksModule)。

---

## 後続プランのロードマップ（このプランの対象外）

| フェーズ | 内容 | 主産物 |
|---|---|---|
| **1（本プラン）** | 境界ブロック＋3D密閉部屋検出＋部屋レジストリ/クエリ | 壁で囲うと部屋検出、壊すと無効化 |
| 2 | 純度シミュ（N/V/S、A_total、清浄機 n·q·C 除去、平衡、クラス閾値、ヒステリシス、ACH要求）＋**再検出時の部屋同一性対応付け（セル重なり）と純度状態の永続化** | 部屋にクラスが付き清浄機/汚染に応答 |
| 3 | 空気清浄機ブロック＋フィルター仕事量ベース消費＋電力＋汚染源4種（自然増加/機械/ドア/ハッチ）。境界種別 `CleanRoomBoundaryKind` を `a_connector`/ハッチ汚染/ドア汚染で参照 | 維持ループが回る |
| 4 | 製造機統合（binning歩留まり・最大グレード天井・Valid/Degraded/Invalid＋猶予で停止）。`TryGetRoomContainingBlock` で multi-block 機械の全占有セル所属を判定 | 半導体生産が部屋クラスに依存 |
| 5 | I/Oブロック挙動（ハッチ=アイテム・パイプコネクタ=流体・ドア=人）＋必要な部屋状態のセーブ/ロード | 完全な遊べる形 |

---

## File Structure（フェーズ1で作成/変更するファイル）

**スキーマ／マスタ（境界ブロック定義）**
- Modify: `VanillaSchema/blocks.yml` — `blockType` enum に4種追加＋blockParam の switch case 追加
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` — SourceGenerator トリガ文字列を変更
- Modify: テスト用mod `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json` と `.../master/blocks.json` — テスト用の4ブロックを追加
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs` — テスト用 BlockId アクセサ追加

**境界ブロックの実装**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomBoundaryComponent.cs` — 境界マーカー＋種別
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomBoundaryComponent.cs` — マーカー実装
- Create: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs` — 4種共通テンプレート（種別をコンストラクタで受ける）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs` — 4種を登録

**検出システム（新規アセンブリ Game.CleanRoom）**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Game.CleanRoom.asmdef` — Game.Gear.asmdef の参照を踏襲
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoom.cs` — 部屋データ
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetector.cs` — 検出ロジック（純関数）
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetectionSystem.cs` — tick/イベント駆動システム
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs` — DI登録＋eager（asmdef参照追加も）
- Modify: `moorestech_server/Assets/Scripts/Tests/Server.Tests.asmdef` — Game.CleanRoom 参照追加

**テスト**
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDetectionTest.cs`

> 各 `.cs`／`.asmdef` 新規ファイルは Unity が `.meta` を自動生成する。`.meta` は手動作成禁止。

---

## Task 1: 境界ブロックの blockType をスキーマに追加

**Files:**
- Modify: `VanillaSchema/blocks.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`

このタスクはコード生成のみ。`edit-schema` スキルの手順に従うこと。

- [ ] **Step 1: blocks.yml の blockType enum に4種を追加**

`VanillaSchema/blocks.yml` の `blockType` の `options:` 配列に、既存値の末尾へ追加:

```yaml
      - CleanRoomWall
      - CleanRoomDoor
      - CleanRoomItemHatch
      - CleanRoomPipeConnector
```

- [ ] **Step 2: blockParam の switch/cases に4種を追加（または param 無し方針に合わせる）**

まず既存 blocks.yml を読み、「param を持たない blockType」（例 `Block`）が switch case をどう書いているか確認する。

- 既存に param 無し blockType の書き方があるなら、それに合わせて4種とも param 無しにする（フェーズ1では追加パラメータ不要）。
- switch が全 case 必須なら、4種それぞれ空オブジェクトの case を追加:

```yaml
      - when: CleanRoomWall
        type: object
        properties: []
      - when: CleanRoomDoor
        type: object
        properties: []
      - when: CleanRoomItemHatch
        type: object
        properties: []
      - when: CleanRoomPipeConnector
        type: object
        properties: []
```

- [ ] **Step 3: SourceGenerator をトリガ**

`moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` 定数の値を変更:

```csharp
private const string dummyText = "regenerate-cleanroom-phase1";
```

- [ ] **Step 4: 再生成を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。`Mooresmaster.Model.BlocksModule.BlockTypeConst` に `CleanRoomWall`/`CleanRoomDoor`/`CleanRoomItemHatch`/`CleanRoomPipeConnector` が生成される（Task 3 で参照確認）。

> 「Domain Reload in progress」なら45秒待って再試行。型未検出なら Unity 再起動。

- [ ] **Step 5: Commit**

```bash
git add VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git commit -m "feat(cleanroom): 境界ブロックの blockType をスキーマに追加"
```

---

## Task 2: 境界マーカー interface（種別付き）とコンポーネント

検出システムが「このブロックは密閉境界か」を判定するマーカー。フェーズ3で汚染計算（`a_connector`・ハッチ/ドア汚染）が境界の**種別**を要るので、最初から `CleanRoomBoundaryKind` を持たせておく。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomBoundaryComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomBoundaryComponent.cs`

検証は Task 6 のテストで行う（ここではコンパイルのみ）。

- [ ] **Step 1: マーカー interface ＋ 種別 enum を作成**

`Game.Block.Interface/Component/ICleanRoomBoundaryComponent.cs`:

```csharp
namespace Game.Block.Interface.Component
{
    // 境界ブロックの種別。フェーズ3の汚染計算で参照する。
    // Kind of boundary block; consumed by phase-3 pollution calc.
    public enum CleanRoomBoundaryKind
    {
        Wall,
        Door,
        ItemHatch,
        PipeConnector,
    }

    // クリーンルームの密閉境界として機能するブロックが実装するマーカー。
    // Marker for blocks that act as a sealing boundary of a clean room.
    public interface ICleanRoomBoundaryComponent : IBlockComponent
    {
        CleanRoomBoundaryKind BoundaryKind { get; }
    }
}
```

> `IBlockComponent` の名前空間・必須メンバは `Game.Block.Interface/Component/IBlockComponent.cs` を開いて一致させること。

- [ ] **Step 2: マーカー実装コンポーネントを作成**

`Game.Block/Blocks/CleanRoom/CleanRoomBoundaryComponent.cs`:

```csharp
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom
{
    // 境界ブロック共通のマーカー。種別のみ保持し、可変状態は持たない。
    // Shared marker for boundary blocks; holds kind only, no mutable state.
    public class CleanRoomBoundaryComponent : ICleanRoomBoundaryComponent
    {
        public CleanRoomBoundaryKind BoundaryKind { get; }
        public bool IsDestroy { get; private set; }

        public CleanRoomBoundaryComponent(CleanRoomBoundaryKind kind)
        {
            BoundaryKind = kind;
        }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
```

> `IBlockComponent` が要求するメンバ（`IsDestroy`/`Destroy()` 等）は既存の単純コンポーネント（`Game.Block/Blocks/Chest/VanillaChestComponent.cs` 等）に合わせて過不足なく実装。

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 4: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomBoundaryComponent.cs moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomBoundaryComponent.cs
git commit -m "feat(cleanroom): 境界マーカー interface（種別付き）とコンポーネントを追加"
```

---

## Task 3: 境界ブロックのテンプレートと登録

4種とも挙動は同じ（種別付きマーカーを付けるだけ）なので、種別をコンストラクタで受ける1つの共通テンプレートを4インスタンス登録する。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs`

- [ ] **Step 1: 共通テンプレートを作成**

`Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs`:

```csharp
using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.CleanRoom;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    // 4種のクリーンルーム境界ブロック共通テンプレート。種別付きマーカーを付与。
    // Shared template for the 4 boundary block types; attaches a kinded marker.
    public class VanillaCleanRoomBoundaryTemplate : IBlockTemplate
    {
        private readonly CleanRoomBoundaryKind _kind;

        public VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind kind)
        {
            _kind = kind;
        }

        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId,
            BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return Build(blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement,
            BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            // 境界ブロックは保存状態を持たないので New と同じ。
            // Boundary blocks hold no save state, so identical to New.
            return Build(blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private IBlock Build(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId,
            BlockPositionInfo blockPositionInfo)
        {
            var components = new List<IBlockComponent>
            {
                new CleanRoomBoundaryComponent(_kind),
            };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
```

> `IBlockTemplate` の `New`/`Load` の正確なシグネチャは `Game.Block.Interface/IBlockTemplate.cs` と `VanillaChestTemplate.cs`/`VanillaGearTemplate.cs`、`BlockSystem` のコンストラクタ引数順は `Game.Block/Blocks/BlockSystem.cs` で確認して一致させる。

- [ ] **Step 2: VanillaIBlockTemplates に4種を登録**

`Game.Block/Factory/VanillaIBlockTemplates.cs` のコンストラクタの `BlockTypesDictionary.Add(...)` 群に追加:

```csharp
            BlockTypesDictionary.Add(BlockTypeConst.CleanRoomWall, new VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind.Wall));
            BlockTypesDictionary.Add(BlockTypeConst.CleanRoomDoor, new VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind.Door));
            BlockTypesDictionary.Add(BlockTypeConst.CleanRoomItemHatch, new VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind.ItemHatch));
            BlockTypesDictionary.Add(BlockTypeConst.CleanRoomPipeConnector, new VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind.PipeConnector));
```

> `using Mooresmaster.Model.BlocksModule;`（BlockTypeConst）と `using Game.Block.Interface.Component;`（CleanRoomBoundaryKind）が必要なら追加。

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。`BlockTypeConst.CleanRoomWall` 等が解決。

- [ ] **Step 4: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs
git commit -m "feat(cleanroom): 境界ブロックのテンプレートと登録を追加"
```

---

## Task 4: テスト用 mod に境界ブロックを追加

NUnit テストは `ForUnitTest` mod のマスタを使う。テスト用に4ブロックを mod の items.json / blocks.json に追加し、`ForUnitTestModBlockId` からアクセスできるようにする。`creating-server-tests` スキルのID規約に従う。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs`

- [ ] **Step 1: 既存のテスト mod とアクセサ定義を確認**

`.../master/blocks.json`・`items.json` の既存エントリ形式と、`ForUnitTestModBlockId.cs` での**実際のアクセサ定義方法**を読む。
**重要（Codex指摘）:** 既存ヘルパは `GetBlockId(...)` ではなく `GetBlock(...)` 等の別名の可能性が高い。必ず実ファイルのヘルパ名・GUID の渡し方をそのまま踏襲すること。GUID は既存と衝突しない新規値を割り当てる。

- [ ] **Step 2: items.json に4アイテムを追加**

`data` 配列に4種のテスト用アイテムを追加（既存形式に合わせる。`itemGuid` は新規GUID）。

- [ ] **Step 3: blocks.json に4ブロックを追加**

`data` 配列に4種を追加。例（既存の必須フィールドに合わせる。`blockSize` は `[1,1,1]`）:

```json
{
  "name": "TestCleanRoomWall",
  "blockType": "CleanRoomWall",
  "blockGuid": "<新規GUID-Wall>",
  "itemGuid": "<Step2のGUID-Wall>",
  "blockSize": [1, 1, 1],
  "blockParam": {}
}
```

`CleanRoomDoor`/`CleanRoomItemHatch`/`CleanRoomPipeConnector` も同様。`blockParam` を持たない方針にした場合はフィールドを省略するか既存の param 無しブロックに合わせる。

- [ ] **Step 4: ForUnitTestModBlockId に4アクセサを追加**

既存アクセサと同一パターンで4種を追加（ヘルパ名は Step 1 で確認した実名を使う。下は名称が `GetBlockId` だった場合の例。違えば合わせる）:

```csharp
        public static BlockId CleanRoomWall => GetBlockId("<TestCleanRoomWall の blockGuid>");
        public static BlockId CleanRoomDoor => GetBlockId("<同 Door>");
        public static BlockId CleanRoomItemHatch => GetBlockId("<同 Hatch>");
        public static BlockId CleanRoomPipeConnector => GetBlockId("<同 PipeConnector>");
```

- [ ] **Step 5: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 6: 設置スモークテストを書いて緑にする**

`Tests/CombinedTest/Core/CleanRoomDetectionTest.cs` を新規作成し、「境界ブロックを設置でき、種別付きマーカーを持つ」最小テストを追加:

```csharp
using System;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Context;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Tests.CombinedTest.Core
{
    public class CleanRoomDetectionTest
    {
        [Test]
        public void PlaceBoundaryBlock_HasKindedBoundaryComponent()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomItemHatch, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var hatch);

            Assert.True(hatch.TryGetComponent<ICleanRoomBoundaryComponent>(out var marker));
            Assert.AreEqual(CleanRoomBoundaryKind.ItemHatch, marker.BoundaryKind);
        }
    }
}
```

> `MoorestechServerDIContainerOptions`/`TestModDirectory.ForUnitTestModDirectory`/`IBlock.TryGetComponent<T>` の正確な名前は `GearBeltConveyorTest.cs` と `IBlock.cs` に合わせる。

- [ ] **Step 7: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceBoundaryBlock_HasKindedBoundaryComponent"`
Expected: PASS。

- [ ] **Step 8: Commit**

```bash
git add moorestech_server/Assets/Scripts/Tests.Module/ moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDetectionTest.cs
git commit -m "test(cleanroom): テスト用境界ブロックと設置スモークテストを追加"
```

---

## Task 5: CleanRoom データ型と検出ロジック（純関数・AABBリーク判定）

検出ロジックを `IWorldBlockDatastore` を入力にとり部屋集合を返す純粋な静的メソッドとして実装する。tick/イベントから切り離してテストする。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Game.CleanRoom.asmdef`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoom.cs`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetector.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/Server.Tests.asmdef`

### アルゴリズム仕様（このフェーズで確定）

- **境界ブロック** = `ICleanRoomBoundaryComponent` を持つブロック。これだけが密閉面になる。
- **通過セル（passable）** = 境界ブロックが存在しないセル（空セル、または機械等の非境界ブロックのセル）。**機械占有セルも通過セル＝体積Vに数える**（空気として扱う簡略化。設計書の「Vが汚染/ACHに効く」と整合し、フェーズ2バランスはこの前提）。
- **境界AABB** = 全境界ブロックセルの最小〜最大座標で作る直方体。密閉された部屋の通過セルは必ずこのAABB内に収まる。
- **リーク判定（Codex指摘で `MaxRoomVolume` 依存から変更）:** seed から6近傍 flood-fill し、**通過セルが境界AABBの外に出たら即リーク**（その連結空間は外部）。`MaxRoomVolume` 超過は「大きすぎる/異常」としての安全網（別理由の不成立）。
- **部屋** = AABB外に漏れずに閉じた通過セル集合。
- **近傍は6近傍**（面接触）。斜めの隙間はリーク。
- **体積 V** = 部屋の通過セル数。**表面積 S** = 部屋セルの面のうち境界ブロックセルに接する面の数（内部に置いた境界ブロック柱の露出面もSに入る＝設計書 `a_surface·S` と整合）。
- 同一通過セルは1部屋にのみ属する（visited で重複排除）。

定数:
```csharp
public const int MaxRoomVolume = 4096; // 安全網。境界AABBが異常に巨大な場合の暴走防止。
```

- [ ] **Step 1: アセンブリ定義を作成**

`Game.CleanRoom/Game.CleanRoom.asmdef`。参照は `Game.Gear/Game.Gear.asmdef` の `references` をベースに踏襲する:

```json
{
    "name": "Game.CleanRoom",
    "references": [
        "Game.Block.Interface",
        "Game.World.Interface",
        "Game.Context",
        "Core.Update",
        "Core.Master"
    ],
    "autoReferenced": true
}
```

> 正確な参照名は Game.Gear.asmdef をコピーして調整。コンパイルが通るまで参照を足す。`.meta` は Unity 生成。

- [ ] **Step 2: 失敗するテストを書く（密閉立方体が1部屋・体積/表面積が正しい）**

`CleanRoomDetectionTest.cs` に追加:

```csharp
        [Test]
        public void Detect_SealedShell_ReturnsOneRoom()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // 3x3x3 の外殻を壁で作る。中心 (1,1,1) だけ空洞。
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);

            Assert.AreEqual(1, rooms.Count);
            Assert.AreEqual(1, rooms[0].Volume, "Inner volume should be 1 cell");
            Assert.AreEqual(6, rooms[0].SurfaceArea, "A single cell touches 6 wall faces");
        }

        // min..max の外殻だけ壁を置き、内部を空洞にするヘルパ。
        // Helper: place walls only on the shell of [min,max], leaving the interior hollow.
        private static void BuildWallShell(Game.World.Interface.DataStore.IWorldBlockDatastore world,
            Vector3Int min, Vector3Int max)
        {
            for (var x = min.x; x <= max.x; x++)
            for (var y = min.y; y <= max.y; y++)
            for (var z = min.z; z <= max.z; z++)
            {
                var onShell = x == min.x || x == max.x || y == min.y || y == max.y || z == min.z || z == max.z;
                if (!onShell) continue;
                world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWall, new Vector3Int(x, y, z),
                    BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            }
        }
```

- [ ] **Step 3: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Detect_SealedShell_ReturnsOneRoom"`
Expected: FAIL（`CleanRoomDetector` 未定義）。

- [ ] **Step 4: CleanRoom データ型を実装（Cells 公開）**

`Game.CleanRoom/CleanRoom.cs`:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Game.CleanRoom
{
    // 検出された1つのクリーンルーム。ブロックから導出される派生状態。
    // A single detected clean room; derived state computed from blocks.
    public class CleanRoom
    {
        // Id は一時参照用。永続キーにしてはいけない（フェーズ2はCellsの重なりで同一性を取る）。
        // Id is an ephemeral handle, NOT a persistence key (phase 2 matches rooms by cell overlap).
        public int Id { get; }
        public int Volume => _cells.Count;
        public int SurfaceArea { get; }
        public bool IsValid { get; }

        // フェーズ2の再検出時の部屋対応付け（セル重なり）に使うため公開する。
        // Exposed so phase 2 can re-map rooms across re-detection by cell overlap.
        public IReadOnlyCollection<Vector3Int> Cells => _cells;

        private readonly HashSet<Vector3Int> _cells;

        public CleanRoom(int id, HashSet<Vector3Int> cells, int surfaceArea, bool isValid)
        {
            Id = id;
            _cells = cells;
            SurfaceArea = surfaceArea;
            IsValid = isValid;
        }

        public bool Contains(Vector3Int cell) => _cells.Contains(cell);
    }
}
```

- [ ] **Step 5: CleanRoomDetector を実装（AABBリーク判定）**

`Game.CleanRoom/CleanRoomDetector.cs`:

```csharp
using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom
{
    public static class CleanRoomDetector
    {
        public const int MaxRoomVolume = 4096;

        // ワールド全体を走査し、密閉されたクリーンルームをすべて返す。
        // Scan the whole world and return all sealed clean rooms.
        public static List<CleanRoom> DetectAllRooms(IWorldBlockDatastore world)
        {
            var boundaryCells = BuildBoundaryCellSet(world);
            var rooms = new List<CleanRoom>();
            if (boundaryCells.Count == 0) return rooms;

            // 境界AABB。密閉された部屋の通過セルはこの内側に必ず収まる。
            // Boundary AABB; passable cells of a sealed room must stay inside it.
            ComputeAabb(boundaryCells, out var min, out var max);

            var globalVisited = new HashSet<Vector3Int>();
            var nextId = 0;

            foreach (var boundary in boundaryCells)
            foreach (var seed in SixNeighbors(boundary))
            {
                if (boundaryCells.Contains(seed)) continue;
                if (globalVisited.Contains(seed)) continue;
                if (IsOutsideAabb(seed, min, max)) continue; // AABB外の種は外部空間

                if (TryFloodFill(seed, boundaryCells, min, max, globalVisited, out var cells, out var surface))
                    rooms.Add(new CleanRoom(nextId++, cells, surface, true));
            }

            return rooms;

            #region Internal

            // 種から通過セルを flood-fill。AABB外に出たら（=漏れ）false。
            // Flood-fill passable cells; false if it leaves the AABB (leak).
            bool TryFloodFill(Vector3Int start, HashSet<Vector3Int> boundary, Vector3Int aabbMin, Vector3Int aabbMax,
                HashSet<Vector3Int> visitedAll, out HashSet<Vector3Int> cells, out int surfaceArea)
            {
                cells = new HashSet<Vector3Int>();
                surfaceArea = 0;
                var stack = new Stack<Vector3Int>();
                stack.Push(start);
                cells.Add(start);

                while (stack.Count > 0)
                {
                    var cur = stack.Pop();

                    if (cells.Count > MaxRoomVolume || IsOutsideAabb(cur, aabbMin, aabbMax))
                    {
                        // 漏れ/暴走。探索済みを除外登録して不成立。
                        // Leak/runaway. Mark explored as visited and fail.
                        foreach (var c in cells) visitedAll.Add(c);
                        return false;
                    }

                    foreach (var n in SixNeighbors(cur))
                    {
                        if (boundary.Contains(n)) { surfaceArea++; continue; } // 境界面に接触
                        if (cells.Add(n)) stack.Push(n);
                    }
                }

                foreach (var c in cells) visitedAll.Add(c);
                return true;
            }

            #endregion
        }

        // 全境界ブロックが占有するセル集合（GetBlockのO(n)を避け一括構築）。
        private static HashSet<Vector3Int> BuildBoundaryCellSet(IWorldBlockDatastore world)
        {
            var set = new HashSet<Vector3Int>();
            foreach (var kvp in world.BlockMasterDictionary)
            {
                var data = kvp.Value;
                if (!data.Block.TryGetComponent<ICleanRoomBoundaryComponent>(out _)) continue;

                var info = data.BlockPositionInfo;
                for (var x = info.MinPos.x; x <= info.MaxPos.x; x++)
                for (var y = info.MinPos.y; y <= info.MaxPos.y; y++)
                for (var z = info.MinPos.z; z <= info.MaxPos.z; z++)
                    set.Add(new Vector3Int(x, y, z));
            }
            return set;
        }

        private static void ComputeAabb(HashSet<Vector3Int> cells, out Vector3Int min, out Vector3Int max)
        {
            min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
            foreach (var c in cells)
            {
                min = Vector3Int.Min(min, c);
                max = Vector3Int.Max(max, c);
            }
        }

        private static bool IsOutsideAabb(Vector3Int p, Vector3Int min, Vector3Int max)
        {
            return p.x < min.x || p.x > max.x || p.y < min.y || p.y > max.y || p.z < min.z || p.z > max.z;
        }

        private static IEnumerable<Vector3Int> SixNeighbors(Vector3Int p)
        {
            yield return new Vector3Int(p.x + 1, p.y, p.z);
            yield return new Vector3Int(p.x - 1, p.y, p.z);
            yield return new Vector3Int(p.x, p.y + 1, p.z);
            yield return new Vector3Int(p.x, p.y - 1, p.z);
            yield return new Vector3Int(p.x, p.y, p.z + 1);
            yield return new Vector3Int(p.x, p.y, p.z - 1);
        }
    }
}
```

> `world.BlockMasterDictionary` の要素型・`data.Block.TryGetComponent<T>`・`IWorldBlockDatastore` の名前空間は `WorldBlockDatastore.cs`/`IBlock.cs`/`IWorldBlockDatastore.cs` で確認。`Vector3Int.Min/Max` は Unity 標準。

- [ ] **Step 6: Server.Tests.asmdef に Game.CleanRoom 参照を追加**

`Tests/Server.Tests.asmdef` の `references` に `"Game.CleanRoom"` を追加。

- [ ] **Step 7: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Detect_SealedShell_ReturnsOneRoom"`
Expected: PASS。型未検出なら asmdef 認識のため Unity 再起動。

- [ ] **Step 8: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/ moorestech_server/Assets/Scripts/Tests/Server.Tests.asmdef moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDetectionTest.cs
git commit -m "feat(cleanroom): CleanRoom型とAABBリーク判定の密閉検出器を追加"
```

---

## Task 6: 検出ロジックの網羅テスト（Codex指摘の最小セット）

密閉/リーク/複数部屋/分割/境界種別/非境界ブロック/体積上限を網羅する。各テストを1つずつ追加→実行で緑を確認していく。`BuildWallShell` ヘルパ（Task 5）を再利用する。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDetectionTest.cs`

- [ ] **Step 1: リーク（穴あきシェル）→ 部屋なし**

```csharp
        [Test]
        public void Detect_ShellWithHole_ReturnsNoRoom()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            world.RemoveBlock(new Vector3Int(1, 1, 0), BlockRemoveReason.Destroy); // 面に穴

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(0, rooms.Count, "A shell with a hole must not form a sealed room");
        }
```

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Detect_ShellWithHole_ReturnsNoRoom"` → PASS（穴から (1,1,-1)＝AABB外へ到達して即リーク）。
`BlockRemoveReason.Destroy` の正確な名前は `IWorldBlockDatastore.cs` で確認。

- [ ] **Step 2: 5x5x5 シェル → 体積27・表面積54**

```csharp
        [Test]
        public void Detect_5x5x5Shell_HasVolume27Surface54()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4)); // 内部 3x3x3

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(1, rooms.Count);
            Assert.AreEqual(27, rooms[0].Volume);
            Assert.AreEqual(54, rooms[0].SurfaceArea); // 3x3面 x6 = 54
        }
```

Run → PASS。

- [ ] **Step 3: 離れた2つの密閉シェル → 2部屋**

```csharp
        [Test]
        public void Detect_TwoSeparateShells_ReturnsTwoRooms()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            BuildWallShell(world, new Vector3Int(10, 0, 0), new Vector3Int(12, 2, 2));

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(2, rooms.Count);
        }
```

Run → PASS。

- [ ] **Step 4: ドア/ハッチ/コネクタも境界として密閉する**

3x3x3 シェルの壁1個をドアに、もう1個をハッチに、もう1個をコネクタに置き換えても1部屋のまま:

```csharp
        [Test]
        public void Detect_DoorHatchConnector_AlsoSeal()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            // 壁3枚をドア/ハッチ/コネクタに差し替える
            ReplaceWith(world, new Vector3Int(1, 1, 0), ForUnitTestModBlockId.CleanRoomDoor);
            ReplaceWith(world, new Vector3Int(1, 1, 2), ForUnitTestModBlockId.CleanRoomItemHatch);
            ReplaceWith(world, new Vector3Int(0, 1, 1), ForUnitTestModBlockId.CleanRoomPipeConnector);

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(1, rooms.Count, "Door/Hatch/Connector must seal the room");
            Assert.AreEqual(1, rooms[0].Volume);
        }

        private static void ReplaceWith(Game.World.Interface.DataStore.IWorldBlockDatastore world,
            Vector3Int pos, BlockId blockId)
        {
            world.RemoveBlock(pos, BlockRemoveReason.Destroy);
            world.TryAddBlock(blockId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }
```

Run → PASS。`BlockId` の using（`Core.Master`）を追加。

- [ ] **Step 5: 壁穴に非境界ブロックを置いても密閉しない**

穴あきシェルの穴に通常ブロック（テスト mod の既存非境界ブロック、例 GearBeltConveyor 等）を置いても部屋にならない:

```csharp
        [Test]
        public void Detect_NonBoundaryBlockInHole_DoesNotSeal()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            world.RemoveBlock(new Vector3Int(1, 1, 0), BlockRemoveReason.Destroy);
            // 穴に非境界ブロックを置く（密閉面にはならない）
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(1, 1, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(0, rooms.Count, "Only boundary blocks seal; a normal block does not");
        }
```

Run → PASS。使用する非境界ブロックIDは `ForUnitTestModBlockId` の既存値に合わせる。

- [ ] **Step 6: 室内に非境界ブロックを置いても部屋は維持・Vは不変**

5x5x5 室内（体積27）に機械相当の非境界ブロックを置いても1部屋・体積27のまま（機械セルも通過セル＝Vに数える方針の明示テスト）:

```csharp
        [Test]
        public void Detect_NonBoundaryBlockInside_RoomPersists_VolumeUnchanged()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(2, 2, 2),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(1, rooms.Count);
            Assert.AreEqual(27, rooms[0].Volume, "Machine cells count as passable air volume");
        }
```

Run → PASS。

- [ ] **Step 7: Commit**

```bash
git add moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDetectionTest.cs
git commit -m "test(cleanroom): 密閉/リーク/複数/種別/非境界の網羅テストを追加"
```

---

## Task 7: CleanRoomDetectionSystem（境界変更のみ dirty・RebuildAll 公開）

検出ロジックを世界システムに載せる。**境界ブロックの設置/破壊だけ** geometry-dirty を立て、tick で再検出。テスト・ロードから呼べる `RebuildAll()` を公開する。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetectionSystem.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`

- [ ] **Step 1: 失敗テスト（設置→tick→検出、破壊→tick→無効化、非境界設置はdirtyにしない）**

```csharp
        [Test]
        public void System_PlaceThenBreakBoundary_UpdatesRooms()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var system = serviceProvider.GetService<Game.CleanRoom.CleanRoomDetectionSystem>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, system.Rooms.Count);

            world.RemoveBlock(new Vector3Int(1, 1, 0), BlockRemoveReason.Destroy);
            GameUpdater.RunFrames(1);
            Assert.AreEqual(0, system.Rooms.Count);
        }

        [Test]
        public void System_NonBoundaryPlacement_DoesNotMarkGeometryDirty()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var system = serviceProvider.GetService<Game.CleanRoom.CleanRoomDetectionSystem>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            GameUpdater.RunFrames(1);
            var before = system.RebuildCount;

            // 非境界ブロックを室内に置く → geometry は変わらない → 再検出は走らない
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(2, 2, 2),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            GameUpdater.RunFrames(1);

            Assert.AreEqual(before, system.RebuildCount, "Non-boundary placement must not trigger re-detection");
        }
```

`RebuildCount` は再検出回数を数えるテスト用カウンタ（Step 3 で実装）。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "System_(PlaceThenBreakBoundary_UpdatesRooms|NonBoundaryPlacement_DoesNotMarkGeometryDirty)"`
Expected: FAIL（`CleanRoomDetectionSystem` 未定義）。

- [ ] **Step 3: CleanRoomDetectionSystem を実装**

`Game.CleanRoom/CleanRoomDetectionSystem.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Update;
using Game.Block.Interface.Component;
using Game.Context;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom
{
    // 世界レベルのクリーンルーム検出。境界ブロック変更のみ再検出をトリガする。
    // World-level clean room detection. Only boundary-block changes trigger re-detection.
    public class CleanRoomDetectionSystem
    {
        public IReadOnlyList<CleanRoom> Rooms => _rooms;
        public int RebuildCount { get; private set; } // テスト用: 再検出回数

        private List<CleanRoom> _rooms = new();
        private bool _geometryDirty;
        private readonly IWorldBlockDatastore _world;
        private readonly List<IDisposable> _subscriptions = new();

        public CleanRoomDetectionSystem(IWorldBlockDatastore world)
        {
            _world = world;

            _subscriptions.Add(GameUpdater.UpdateObservable.Subscribe(_ => Update()));

            // 境界ブロックの設置/破壊だけ geometry-dirty を立てる。
            // Only boundary-block place/remove marks geometry dirty.
            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(e => OnChanged(e.BlockData)));
            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(e => OnChanged(e.BlockData)));

            _geometryDirty = true; // 起動/ロード直後に一度フル検出
        }

        // 全走査で部屋を再構築する。テスト/ロードから明示的にも呼べる。
        // Rebuild all rooms by full scan; callable from tests/load too.
        public void RebuildAll()
        {
            _rooms = CleanRoomDetector.DetectAllRooms(_world);
            RebuildCount++;
        }

        public bool TryGetRoomAt(Vector3Int cell, out CleanRoom room)
        {
            foreach (var r in _rooms)
                if (r.Contains(cell)) { room = r; return true; }
            room = null;
            return false;
        }

        public void Destroy()
        {
            foreach (var s in _subscriptions) s.Dispose();
            _subscriptions.Clear();
        }

        #region Internal

        private void Update()
        {
            if (!_geometryDirty) return;
            _geometryDirty = false;
            // フェーズ1は全走査。dirty領域の差分更新はフェーズ2以降で最適化。
            RebuildAll();
        }

        private void OnChanged(WorldBlockData blockData)
        {
            // 境界ブロックでなければ部屋形状は変わらないので無視。
            if (blockData.Block.TryGetComponent<ICleanRoomBoundaryComponent>(out _))
                _geometryDirty = true;
        }

        #endregion
    }
}
```

> `OnBlockPlaceEvent` が流す型（`BlockPlaceProperties`）と `.BlockData`/`.BlockData.Block`、`WorldBlockData` の名前空間は `Game.World/WorldBlockUpdateEvent.cs`・`WorldBlockData.cs` で確認。`Subscribe` の戻り `IDisposable` を確認。`Game.Context`/`Game.World.Interface` 参照を asmdef に追加。

- [ ] **Step 4: DI に登録して eager materialize**

`Server.Boot/MoorestechServerDIContainerGenerator.cs` の他の世界システム登録箇所に追加:

```csharp
            services.AddSingleton<Game.CleanRoom.CleanRoomDetectionSystem>();
```
eager 実体化箇所に追加:
```csharp
            serviceProvider.GetService<Game.CleanRoom.CleanRoomDetectionSystem>();
```

> `Server.Boot` の asmdef に `Game.CleanRoom` 参照を追加（コンパイルエラーで判明したら）。

- [ ] **Step 5: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "System_(PlaceThenBreakBoundary_UpdatesRooms|NonBoundaryPlacement_DoesNotMarkGeometryDirty)"`
Expected: PASS。

- [ ] **Step 6: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetectionSystem.cs moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs
git commit -m "feat(cleanroom): 境界変更のみ再検出する世界システムを追加"
```

---

## Task 8: 部屋クエリ（座標・multi-blockブロック）と全テスト緑化

製造機統合（フェーズ4）が使う2つのクエリを足す。`TryGetRoomAt` は単一セル、`TryGetRoomContainingBlock` は multi-block の**全占有セルが同一部屋**かを判定する。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetectionSystem.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDetectionTest.cs`

- [ ] **Step 1: 失敗テスト（座標クエリ＋multi-block包含）**

```csharp
        [Test]
        public void System_GetRoomAt_ReturnsContainingRoom()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var system = serviceProvider.GetService<Game.CleanRoom.CleanRoomDetectionSystem>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            GameUpdater.RunFrames(1);

            Assert.True(system.TryGetRoomAt(new Vector3Int(2, 2, 2), out _));
            Assert.False(system.TryGetRoomAt(new Vector3Int(50, 50, 50), out _));
        }

        [Test]
        public void System_GetRoomContainingBlock_RequiresAllCellsInside()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var system = serviceProvider.GetService<Game.CleanRoom.CleanRoomDetectionSystem>();

            // 内部 3x3x3 の部屋に、室内へ完全に収まる単一セルブロックを置く。
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(2, 2, 2),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var inside);
            GameUpdater.RunFrames(1);

            Assert.True(system.TryGetRoomContainingBlock(inside, out _),
                "A block fully inside the room is contained");
        }
```

> multi-block（2x2x2等）テストはテスト mod に該当ブロックがあれば追加する。無ければ単一セルで `TryGetRoomContainingBlock` の正常系のみ検証し、複数セル判定はフェーズ4で multi-block 機械を使って拡充する旨コメントを残す。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "System_(GetRoomAt_ReturnsContainingRoom|GetRoomContainingBlock_RequiresAllCellsInside)"`
Expected: FAIL（`TryGetRoomContainingBlock` 未定義。`TryGetRoomAt` は Task 7 で実装済みなので前者は通る場合あり）。

- [ ] **Step 3: TryGetRoomContainingBlock を実装**

`CleanRoomDetectionSystem` に追加:

```csharp
        // ブロックの全占有セルが同一部屋に含まれるとき true。multi-block 機械の所属判定用。
        // True iff every occupied cell of the block lies in the SAME room. For multi-block machines.
        public bool TryGetRoomContainingBlock(Game.Block.Interface.IBlock block, out CleanRoom room)
        {
            var info = block.BlockPositionInfo;
            CleanRoom found = null;
            for (var x = info.MinPos.x; x <= info.MaxPos.x; x++)
            for (var y = info.MinPos.y; y <= info.MaxPos.y; y++)
            for (var z = info.MinPos.z; z <= info.MaxPos.z; z++)
            {
                if (!TryGetRoomAt(new Vector3Int(x, y, z), out var r)) { room = null; return false; }
                if (found == null) found = r;
                else if (!ReferenceEquals(found, r)) { room = null; return false; } // 別部屋にまたがる
            }
            room = found;
            return found != null;
        }
```

> `Game.Block.Interface.IBlock`・`BlockPositionInfo.MinPos/MaxPos` は実ファイルで確認。asmdef に `Game.Block.Interface` 参照済み。

- [ ] **Step 4: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "System_(GetRoomAt_ReturnsContainingRoom|GetRoomContainingBlock_RequiresAllCellsInside)"`
Expected: PASS。

- [ ] **Step 5: フェーズ1の全テストを実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"`
Expected: 全 PASS。

- [ ] **Step 6: 既存テストの非回帰を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(GearBeltConveyor|MachineIO|Fluid)Test"`
Expected: 従来どおり PASS（境界ブロック・DI追加が既存を壊していない）。

- [ ] **Step 7: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetectionSystem.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDetectionTest.cs
git commit -m "feat(cleanroom): 座標/multi-block の部屋クエリを追加しフェーズ1完了"
```

---

## フェーズ1 完了の定義（Definition of Done）

- 4種の境界 blockType がスキーマ・テンプレート（種別付き）・登録まで通り、テスト mod で設置できる。
- `CleanRoomDetector.DetectAllRooms` が密閉空間を部屋として検出し、体積V・表面積Sを正しく計算する。**機械等の非境界ブロックのセルは通過セル＝Vに含める。**
- リーク判定は境界AABB外縁到達で即不成立（`MaxRoomVolume` は安全網）。穴あきシェルは部屋にならない。
- 複数部屋・ドア/ハッチ/コネクタ密閉・室内非境界ブロック維持の網羅テストが緑。
- `CleanRoomDetectionSystem` は**境界ブロック変更のみ**で再検出し、`TryGetRoomAt`／`TryGetRoomContainingBlock`／`RebuildAll`／`Rooms` を公開する。
- 既存テストが非回帰。

## フェーズ1で意図的に先送りした事項（後続プラン）

- **部屋の同一性と純度状態の永続化** → フェーズ2。再検出前後の部屋を公開済み `Cells` のセル重なりで対応付け、merge は N 合算・split は濃度/重なり比で配分する。`CleanRoom.Id` は永続キーにしない（本プランで担保済み）。
- 純度（不純物濃度・クラス・ヒステリシス・ACH） → フェーズ2
- dirty 領域の差分更新・リーク判定境界の局所化（連結成分AABB、または推奨の「触れた壁AABB+1」＝内部fillが触れた境界セルのbboxで縛る）・非同期分割処理（フェーズ1は境界変更時に全走査でグローバルAABB） → フェーズ2以降の最適化。詳細は設計書「リーク判定境界の局所化」節を参照
- 空気清浄機・フィルター・電力・汚染源（境界種別 `CleanRoomBoundaryKind` を参照） → フェーズ3
- 製造機の binning 歩留まり・Valid/Degraded/Invalid＋猶予停止（`TryGetRoomContainingBlock` を使用） → フェーズ4
- ハッチ/コネクタ/ドアの I/O 挙動・必要な部屋状態のセーブ/ロード → フェーズ5
- 本番 mod（moorestech_master）の blocks.json 配線・モデル/画像アセット → 各フェーズで playable 化する際に対応
```
