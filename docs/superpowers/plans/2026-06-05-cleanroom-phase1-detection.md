# クリーンルーム フェーズ1（境界ブロック＋3D密閉部屋検出）実装プラン

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **moorestech 固有の必須ルール:**
> - `.cs` 編集後は必ず `uloop compile --project-path ./moorestech_client` でコンパイル確認する。
> - テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"` で実行（クライアントプロジェクトからサーバーテストも走る）。
> - 新規サーバー `.cs` を認識させるには Unity の **再起動**が要る場合がある（Refresh では不足）。コンパイルが「型が見つからない」で失敗したら uloop で Unity 再起動してから再試行。
> - blockType スキーマ追加・SourceGenerator の手順は `edit-schema` スキルに従う。テスト作成は `creating-server-tests` スキルに従う。
> - 非ASCIIファイル編集時は AGENTS.md の「文字化け防止ワークフロー」を順守。

**Goal:** クリーンルーム境界ブロック（壁/ドア/ハッチ/パイプコネクタ）を定義し、それらで完全に囲まれた3D空間を「クリーンルーム」として検出・登録し、ブロックの設置/破壊に追従して部屋の成立/無効化を更新するサーバー側システムを実装する。

**Architecture:** 新しい世界レベルのシングルトン `CleanRoomDetectionSystem`（DI登録）が、`WorldBlockUpdateEvent` の設置/破壊を購読して dirty 領域を積み、`GameUpdater` の tick で dirty 領域だけを再検出する。検出は `BlockMasterDictionary` から構築した `cellToBlockMap` を使い、境界ブロックに囲まれた空間を flood-fill して `CleanRoom`（Id, セル集合, 体積V, 表面積S, 有効フラグ）を作る。部屋はブロックから導出可能な派生状態なので、セーブはせずロード後の全走査で再構築する。

**Tech Stack:** C# (Unity 2022/6, moorestech_server), R3/UniRx `IObservable` (`GameUpdater.UpdateObservable`), NUnit (Server.Tests), Mooresmaster Source Generator (blocks.yml → BlocksModule)。

---

## 後続プランのロードマップ（このプランの対象外）

| フェーズ | 内容 | 主産物 |
|---|---|---|
| **1（本プラン）** | 境界ブロック＋3D密閉部屋検出＋部屋レジストリ/ライフサイクル | 壁で囲うと部屋検出、壊すと無効化 |
| 2 | 純度シミュ（N/V/S、A_total、清浄機 n·q·C 除去、平衡、クラス閾値、ヒステリシス、ACH要求） | 部屋にクラスが付き清浄機/汚染に応答 |
| 3 | 空気清浄機ブロック＋フィルター仕事量ベース消費＋電力＋汚染源4種（自然増加/機械/ドア/ハッチ） | 維持ループが回る |
| 4 | 製造機統合（binning歩留まり・最大グレード天井・Valid/Degraded/Invalid＋猶予で停止） | 半導体生産が部屋クラスに依存 |
| 5 | I/Oブロック挙動（ハッチ=アイテム・パイプコネクタ=流体・ドア=人）＋必要な部屋状態のセーブ/ロード | 完全な遊べる形 |

---

## File Structure（フェーズ1で作成/変更するファイル）

**スキーマ／マスタ（境界ブロック定義）**
- Modify: `VanillaSchema/blocks.yml` — `blockType` enum に4種追加＋blockParam の switch case 追加
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` — SourceGenerator トリガ文字列を変更
- Modify: テスト用mod `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json` と `.../master/blocks.json` — テスト用の4ブロックを追加
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs` — テスト用 BlockId アクセサ追加

**境界ブロックの実装**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomBoundaryComponent.cs` — 境界マーカーインターフェース
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomBoundaryComponent.cs` — マーカー実装
- Create: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs` — 4種共通テンプレート
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs` — 4種を登録

**検出システム（新規アセンブリ Game.CleanRoom）**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Game.CleanRoom.asmdef` — Game.Gear.asmdef の参照を踏襲
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoom.cs` — 部屋データ
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetectionSystem.cs` — 検出システム本体
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs` — DI登録＋eager materialize（asmdef参照追加も）

**テスト**
- Create: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDetectionTest.cs`

> 各 `.cs` 新規ファイルは Unity が `.meta` を自動生成する。`.meta` は手動作成禁止。

---

## Task 1: 境界ブロックの blockType をスキーマに追加

**Files:**
- Modify: `VanillaSchema/blocks.yml`
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs`

このタスクはコード生成のみで、テストは Task 4 のコンパイルで間接検証する（スキーマ単体のNUnitテストは無い）。`edit-schema` スキルの手順に従うこと。

- [ ] **Step 1: blocks.yml の blockType enum に4種を追加**

`VanillaSchema/blocks.yml` の `blockType` の `options:` 配列に、既存値の末尾へ追加:

```yaml
      - CleanRoomWall
      - CleanRoomDoor
      - CleanRoomItemHatch
      - CleanRoomPipeConnector
```

- [ ] **Step 2: blockParam の switch/cases に4種を追加**

同ファイルの `blockParam` の `cases:` に、4種それぞれの最小 param を追加（フェーズ1では追加パラメータ不要なので空オブジェクト相当。スキーマが空 `properties` を許さない場合は他の `Block` 系 case の書き方を踏襲する）:

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

> もし既存スキーマで「paramを持たない blockType」（例: `Block`）が switch case を持たない書き方をしているなら、それに合わせて case を省略してよい。その場合 `blockParam` は null になり、テンプレート側で param を読まない実装にする。実際の blocks.yml の既存パターンを確認してから決めること。

- [ ] **Step 3: SourceGenerator をトリガ**

`moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の `dummyText` 定数の値を変更（任意の新しい文字列）:

```csharp
private const string dummyText = "regenerate-cleanroom-phase1";
```

- [ ] **Step 4: 再生成を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: コンパイル成功。`Mooresmaster.Model.BlocksModule.BlockTypeConst` に `CleanRoomWall` / `CleanRoomDoor` / `CleanRoomItemHatch` / `CleanRoomPipeConnector` が生成されている（Task 3 で参照して確認）。

> 「Domain Reload in progress」エラーが出たら45秒待って再試行。型が見つからない場合は uloop で Unity 再起動。

- [ ] **Step 5: Commit**

```bash
git add VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git commit -m "feat(cleanroom): 境界ブロックの blockType をスキーマに追加"
```

---

## Task 2: 境界マーカー interface とコンポーネント

検出システムが「このブロックは部屋を密閉する境界か」を判定するためのマーカー。4種の境界ブロックすべてがこれを実装したコンポーネントを持つ。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomBoundaryComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomBoundaryComponent.cs`

このタスクの検証は Task 5 のテストで行う（単体で動かす対象が無いため、ここではコンパイルのみ）。

- [ ] **Step 1: マーカー interface を作成**

`Game.Block.Interface/Component/ICleanRoomBoundaryComponent.cs`:

```csharp
using Game.Block.Interface.Component;

namespace Game.Block.Interface.Component
{
    /// クリーンルームの密閉境界として機能するブロックが実装するマーカー。
    /// Marker for blocks that act as a sealing boundary of a clean room.
    public interface ICleanRoomBoundaryComponent : IBlockComponent
    {
    }
}
```

> 既存の `IBlockComponent` のフルパス名前空間は `Game.Block.Interface/Component/IBlockComponent.cs` を開いて一致させること。

- [ ] **Step 2: マーカー実装コンポーネントを作成**

`Game.Block/Blocks/CleanRoom/CleanRoomBoundaryComponent.cs`:

```csharp
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom
{
    // 境界ブロック共通のマーカーコンポーネント。状態は持たない。
    // Shared marker component for clean-room boundary blocks. Holds no state.
    public class CleanRoomBoundaryComponent : ICleanRoomBoundaryComponent
    {
        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
```

> `IBlockComponent` が要求するメンバ（`IsDestroy` / `Destroy()` 等）は、既存の単純コンポーネント（例 `Game.Block/Blocks/Chest/VanillaChestComponent.cs` や Gear 系）を開いて、実際のインターフェース定義に合わせて過不足なく実装すること。

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 4: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomBoundaryComponent.cs moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomBoundaryComponent.cs
git commit -m "feat(cleanroom): 境界マーカー interface とコンポーネントを追加"
```

---

## Task 3: 境界ブロックのテンプレートと登録

4種の境界ブロックを生成するテンプレート。フェーズ1では4種とも挙動は同じ（マーカーを付けるだけ）なので1つの共通テンプレートを使い回す。

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
    // 4種のクリーンルーム境界ブロック共通テンプレート。マーカーのみ付与。
    // Shared template for the 4 clean-room boundary block types. Attaches only the marker.
    public class VanillaCleanRoomBoundaryTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId,
            BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            var components = new List<IBlockComponent>
            {
                new CleanRoomBoundaryComponent(),
            };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement,
            BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            // 境界ブロックは保存状態を持たないので New と同じ。
            // Boundary blocks hold no save state, so identical to New.
            var components = new List<IBlockComponent>
            {
                new CleanRoomBoundaryComponent(),
            };
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
```

> `IBlockTemplate` の `New` / `Load` の正確なシグネチャは `Game.Block.Interface/IBlockTemplate.cs` と既存の `VanillaChestTemplate.cs` / `VanillaGearTemplate.cs` を開いて一致させること（引数名・型・順序）。`BlockSystem` のコンストラクタ引数順も `Game.Block/Blocks/BlockSystem.cs` で確認。

- [ ] **Step 2: VanillaIBlockTemplates に4種を登録**

`Game.Block/Factory/VanillaIBlockTemplates.cs` のコンストラクタ内、`BlockTypesDictionary.Add(...)` が並ぶ箇所に追加:

```csharp
            var cleanRoomBoundaryTemplate = new VanillaCleanRoomBoundaryTemplate();
            BlockTypesDictionary.Add(BlockTypeConst.CleanRoomWall, cleanRoomBoundaryTemplate);
            BlockTypesDictionary.Add(BlockTypeConst.CleanRoomDoor, cleanRoomBoundaryTemplate);
            BlockTypesDictionary.Add(BlockTypeConst.CleanRoomItemHatch, cleanRoomBoundaryTemplate);
            BlockTypesDictionary.Add(BlockTypeConst.CleanRoomPipeConnector, cleanRoomBoundaryTemplate);
```

> `BlockTypeConst.CleanRoomWall` 等が解決できれば Task 1 の SourceGenerator が成功している証拠。`using Mooresmaster.Model.BlocksModule;` が必要なら追加。

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。`BlockTypeConst.CleanRoomWall` 等が解決される。

- [ ] **Step 4: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs
git commit -m "feat(cleanroom): 境界ブロックのテンプレートと登録を追加"
```

---

## Task 4: テスト用 mod に境界ブロックを追加

NUnit テストは `ForUnitTest` mod のマスタを使う。テスト用に4ブロック（最低限 Wall）を mod の items.json / blocks.json に追加し、`ForUnitTestModBlockId` からアクセスできるようにする。`creating-server-tests` スキルのテスト用ID規約に従うこと。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/blocks.json`
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs`

- [ ] **Step 1: 既存のテスト mod の blocks.json / items.json と ForUnitTestModBlockId.cs を確認**

Run: `ls moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/`
既存ブロック（例 GearBeltConveyor）の items.json / blocks.json エントリと、`ForUnitTestModBlockId.cs` でのアクセサ定義方法（GUID→BlockId 解決の書き方）を読んで、同じ形式で追記する。GUID は既存と衝突しない新規値を割り当てる。

- [ ] **Step 2: items.json に4アイテムを追加**

`.../master/items.json` の `data` 配列に、テスト用クリーンルームアイテム4種を追加（既存エントリの形式に合わせる。`itemGuid` は新規GUID）。最低限 `CleanRoomWall` 用のアイテムは必須。

- [ ] **Step 3: blocks.json に4ブロックを追加**

`.../master/blocks.json` の `data` 配列に、4種の blockType エントリを追加。例（既存エントリの必須フィールドに合わせること。`blockSize` は `[1,1,1]`）:

```json
{
  "name": "TestCleanRoomWall",
  "blockType": "CleanRoomWall",
  "blockGuid": "<新規GUID>",
  "itemGuid": "<Step2で割り当てたGUID>",
  "blockSize": [1, 1, 1],
  "blockParam": {}
}
```

`CleanRoomDoor` / `CleanRoomItemHatch` / `CleanRoomPipeConnector` も同様に追加。

> `blockParam` を持たない設計（Task 1 Step 2 の注記）にした場合は `blockParam` フィールド自体を省略するか、既存の param 無しブロックの書き方に合わせる。

- [ ] **Step 4: ForUnitTestModBlockId にアクセサを追加**

`ForUnitTestModBlockId.cs` に、既存アクセサ（例 `GearBeltConveyor`）と同じパターンで4種のアクセサを追加:

```csharp
        public static BlockId CleanRoomWall => GetBlockId("<TestCleanRoomWall の blockGuid>");
        public static BlockId CleanRoomDoor => GetBlockId("<同 Door>");
        public static BlockId CleanRoomItemHatch => GetBlockId("<同 Hatch>");
        public static BlockId CleanRoomPipeConnector => GetBlockId("<同 PipeConnector>");
```

> 実際のヘルパ名（`GetBlockId` 相当）と GUID 文字列の渡し方は既存コードに一致させること。

- [ ] **Step 5: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 6: 設置スモークテストを書いて緑にする（最小テスト）**

`Tests/CombinedTest/Core/CleanRoomDetectionTest.cs` を新規作成し、まず「境界ブロックを設置でき、マーカーコンポーネントを持つ」ことだけ確認する最小テストを追加:

```csharp
using System;
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
        public void PlaceBoundaryBlock_HasBoundaryComponent()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWall, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var wall);

            Assert.True(wall.TryGetComponent<ICleanRoomBoundaryComponent>(out _),
                "Clean room wall must have ICleanRoomBoundaryComponent");
        }
    }
}
```

> `MoorestechServerDIContainerOptions` / `TestModDirectory.ForUnitTestModDirectory` / `IBlock.TryGetComponent<T>` の正確な名前は `Tests/CombinedTest/Core/GearBeltConveyorTest.cs` と `Game.Block.Interface/IBlock.cs` に一致させること。`using` も実ファイルに合わせる。

- [ ] **Step 7: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoomDetectionTest"`
Expected: `PlaceBoundaryBlock_HasBoundaryComponent` が PASS。

- [ ] **Step 8: Commit**

```bash
git add moorestech_server/Assets/Scripts/Tests.Module/ moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDetectionTest.cs
git commit -m "test(cleanroom): テスト用境界ブロックと設置スモークテストを追加"
```

---

## Task 5: CleanRoom データ型と検出ロジック（純関数）

部屋の検出ロジックを、`IWorldBlockDatastore` を入力にとり部屋集合を返す純粋な静的メソッドとして実装する。tick やイベントから切り離してテストしやすくする。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Game.CleanRoom.asmdef`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoom.cs`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetector.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/Server.Tests.asmdef`（Game.CleanRoom 参照追加）

### アルゴリズム仕様（このフェーズで確定）

- **境界ブロック** = `ICleanRoomBoundaryComponent` を持つブロック。これだけが密閉面になる。
- **通過セル（passable）** = 境界ブロックが存在しないセル（空セル、または機械等の非境界ブロックのセル）。
- **部屋** = ある通過セルから6近傍 flood-fill して到達できる通過セル集合で、外部へ漏れない（= 集合サイズが `MaxRoomVolume` 以下で、集合の全フロンティア面が境界ブロックに接している）もの。
  - flood-fill が `MaxRoomVolume` を超えたら「密閉していない」とみなし、その種の部屋は不成立。
  - 近傍は **6近傍**（面接触）。斜めの隙間はリーク（=面で塞がれていなければ漏れる）。
- **体積 V** = 部屋の通過セル数。
- **表面積 S** = 部屋セルの面のうち、境界ブロックセルに接している面の数。
- 同一の通過セルは1つの部屋にしか属さない（flood-fill の visited で重複排除）。

定数:
```csharp
public const int MaxRoomVolume = 4096; // 密閉判定の上限。これを超える連結通過空間は「外部」とみなす。
```

- [ ] **Step 1: アセンブリ定義を作成**

`Game.CleanRoom/Game.CleanRoom.asmdef` を作成する。参照は `Game.Gear/Game.Gear.asmdef` を開いて、その `references` をベースに踏襲する（最低限 `Game.Block.Interface`, `Game.World.Interface`, `Game.Context`, `Core.Update`, `Core.Master`, および UniTask/R3 等の共通参照）。

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

> 正確な参照名は Game.Gear.asmdef の `references` をコピーして調整すること。コンパイルが通るまで参照を足す。`.meta` は Unity が生成する。

- [ ] **Step 2: 失敗するテストを書く（密閉した立方体が1部屋として検出される）**

`Tests/CombinedTest/Core/CleanRoomDetectionTest.cs` に追加。3×3×3 の壁シェル（内部に1×1×1の空洞）を作り、検出器が体積1の部屋を1つ返すことを確認:

```csharp
        [Test]
        public void Detect_SealedShell_ReturnsOneRoom()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // 3x3x3 の外殻を壁で作る。中心 (1,1,1) だけ空洞。
            // Build a 3x3x3 wall shell with a single hollow cell at the center.
            for (var x = 0; x < 3; x++)
            for (var y = 0; y < 3; y++)
            for (var z = 0; z < 3; z++)
            {
                if (x == 1 && y == 1 && z == 1) continue; // 中心は空ける
                world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWall, new Vector3Int(x, y, z),
                    BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            }

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);

            Assert.AreEqual(1, rooms.Count, "Exactly one sealed room expected");
            Assert.AreEqual(1, rooms[0].Volume, "Inner volume should be 1 cell");
            Assert.AreEqual(6, rooms[0].SurfaceArea, "A single cell touches 6 wall faces");
        }
```

- [ ] **Step 3: テストを実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Detect_SealedShell_ReturnsOneRoom"`
Expected: FAIL（`CleanRoomDetector` が未定義）。

- [ ] **Step 4: CleanRoom データ型を実装**

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
        public int Id { get; }
        public int Volume => _cells.Count;
        public int SurfaceArea { get; }
        public bool IsValid { get; }

        // セルは座標包含判定のため HashSet で保持する。
        // Cells are stored as a HashSet for O(1) containment queries.
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

- [ ] **Step 5: CleanRoomDetector を実装**

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
            var globalVisited = new HashSet<Vector3Int>();
            var nextId = 0;

            // 境界セルの6近傍にある未訪問の通過セルを種にして flood-fill する。
            // Seed flood-fill from passable cells adjacent to boundary cells.
            foreach (var boundary in boundaryCells)
            foreach (var seed in SixNeighbors(boundary))
            {
                if (boundaryCells.Contains(seed)) continue;
                if (globalVisited.Contains(seed)) continue;

                if (TryFloodFill(seed, boundaryCells, globalVisited, out var cells, out var surface))
                {
                    rooms.Add(new CleanRoom(nextId++, cells, surface, true));
                }
            }

            return rooms;

            #region Internal

            // 種セルから通過セルを flood-fill。外部に漏れたら false。
            // Flood-fill passable cells from a seed; false if it leaks outside.
            bool TryFloodFill(Vector3Int start, HashSet<Vector3Int> boundary, HashSet<Vector3Int> visitedAll,
                out HashSet<Vector3Int> cells, out int surfaceArea)
            {
                cells = new HashSet<Vector3Int>();
                surfaceArea = 0;
                var localVisited = new HashSet<Vector3Int>();
                var stack = new Stack<Vector3Int>();
                stack.Push(start);
                localVisited.Add(start);

                while (stack.Count > 0)
                {
                    var cur = stack.Pop();
                    cells.Add(cur);

                    if (cells.Count > MaxRoomVolume)
                    {
                        // 上限超過 = 密閉していない（外部）。このセル群は訪問済みにして除外。
                        // Over the cap = not sealed. Mark visited to skip later.
                        foreach (var c in localVisited) visitedAll.Add(c);
                        return false;
                    }

                    foreach (var n in SixNeighbors(cur))
                    {
                        if (boundary.Contains(n))
                        {
                            surfaceArea++; // 境界面に接している = 表面積1
                            continue;
                        }
                        if (localVisited.Add(n)) stack.Push(n);
                    }
                }

                foreach (var c in cells) visitedAll.Add(c);
                return true;
            }

            #endregion
        }

        // 全境界ブロックが占有するセルの集合を作る。GetBlock の O(n) を避けるため一括構築。
        // Build the set of all cells occupied by boundary blocks (avoids O(n) GetBlock per cell).
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

> `world.BlockMasterDictionary` の要素型・`data.Block.TryGetComponent<T>` の正確なシグネチャは `Game.World/DataStore/WorldBlockDatastore.cs` と `IBlock.cs` で確認して一致させること。`IWorldBlockDatastore` の名前空間も実ファイルに合わせる。

- [ ] **Step 6: Server.Tests.asmdef に Game.CleanRoom 参照を追加**

`Tests/Server.Tests.asmdef` の `references` に `"Game.CleanRoom"` を追加（テストから `CleanRoomDetector` を参照するため）。

- [ ] **Step 7: テストを実行して成功を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Detect_SealedShell_ReturnsOneRoom"`
Expected: PASS。

> 「型が見つからない」で失敗する場合、新規 asmdef を認識させるため uloop で Unity 再起動してから再試行。

- [ ] **Step 8: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/ moorestech_server/Assets/Scripts/Tests/Server.Tests.asmdef moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDetectionTest.cs
git commit -m "feat(cleanroom): CleanRoom データ型と密閉検出ロジックを追加"
```

---

## Task 6: リーク（未密閉）検出のテストと修正

穴の開いたシェルは部屋として検出されないことを保証する。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDetectionTest.cs`

- [ ] **Step 1: 失敗（または未カバー）のテストを書く**

```csharp
        [Test]
        public void Detect_ShellWithHole_ReturnsNoRoom()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // 3x3x3 の外殻だが、面の1ブロックを欠けさせて穴を開ける。
            // 3x3x3 shell but leave one face block missing -> leak.
            for (var x = 0; x < 3; x++)
            for (var y = 0; y < 3; y++)
            for (var z = 0; z < 3; z++)
            {
                if (x == 1 && y == 1 && z == 1) continue;       // 中心の空洞
                if (x == 1 && y == 1 && z == 0) continue;       // 面に穴を1つ開ける
                world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWall, new Vector3Int(x, y, z),
                    BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            }

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);

            Assert.AreEqual(0, rooms.Count, "A shell with a hole must not form a sealed room");
        }
```

- [ ] **Step 2: 実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Detect_ShellWithHole_ReturnsNoRoom"`
Expected: PASS（穴があると flood-fill が MaxRoomVolume まで広がり不成立になる。`MaxRoomVolume=4096` だと穴から外へ広がり最終的に上限超過 → false）。

> もし外部に他のブロックが無く flood-fill が無限に広がらず途中で自然に閉じてしまう恐れがある場合は、テストの穴サイズ・配置を見直す。穴から外部空間へ通じていれば 4096 セルに達して不成立になる。FAIL する場合はアルゴリズム（漏れ判定）を点検し修正する。

- [ ] **Step 3: Commit**

```bash
git add moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDetectionTest.cs
git commit -m "test(cleanroom): 穴あきシェルが部屋にならないことを検証"
```

---

## Task 7: CleanRoomDetectionSystem（世界システム・tick/イベント駆動）

検出ロジックを世界レベルのシングルトンに載せ、ブロック設置/破壊を購読して dirty を立て、tick で再検出する。現在の部屋集合を公開し、座標から部屋を引けるようにする。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetectionSystem.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`

- [ ] **Step 1: 失敗するテストを書く（設置→tick→部屋が検出され、破壊→tick→無効化）**

`CleanRoomDetectionTest.cs` に追加。DI から `CleanRoomDetectionSystem` を取得する手段は、既存の世界システム取得方法（`serviceProvider.GetService<T>()` か `ServerContext`）に合わせる。ここでは `serviceProvider` 経由で取得する:

```csharp
        [Test]
        public void System_PlaceThenBreak_UpdatesRoomValidity()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var system = serviceProvider.GetService<Game.CleanRoom.CleanRoomDetectionSystem>();

            // 密閉シェルを作る
            for (var x = 0; x < 3; x++)
            for (var y = 0; y < 3; y++)
            for (var z = 0; z < 3; z++)
            {
                if (x == 1 && y == 1 && z == 1) continue;
                world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWall, new Vector3Int(x, y, z),
                    BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            }

            GameUpdater.RunFrames(1); // dirty 処理を1tick回す
            Assert.AreEqual(1, system.Rooms.Count, "Room should be detected after placing the shell");

            // 壁を1つ壊して穴を開ける
            world.RemoveBlock(new Vector3Int(1, 1, 0), BlockRemoveReason.Destroy);

            GameUpdater.RunFrames(1);
            Assert.AreEqual(0, system.Rooms.Count, "Room should be invalidated after breaking a wall");
        }
```

> `serviceProvider.GetService<T>` / `GameUpdater.RunFrames` / `BlockRemoveReason.Destroy` の正確な名前は既存テスト（`GearBeltConveyorTest.cs`）と `IWorldBlockDatastore.cs` で確認。`using Core.Update;` を追加。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "System_PlaceThenBreak_UpdatesRoomValidity"`
Expected: FAIL（`CleanRoomDetectionSystem` 未定義）。

- [ ] **Step 3: CleanRoomDetectionSystem を実装**

`Game.CleanRoom/CleanRoomDetectionSystem.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Update;
using Game.Context;
using Game.World.Interface.DataStore;

namespace Game.CleanRoom
{
    // 世界レベルのクリーンルーム検出システム。ブロック変更を購読し、tick で再検出する。
    // World-level clean room detection. Subscribes to block changes and re-detects on tick.
    public class CleanRoomDetectionSystem
    {
        public IReadOnlyList<CleanRoom> Rooms => _rooms;

        private List<CleanRoom> _rooms = new();
        private bool _dirty;
        private readonly IWorldBlockDatastore _world;
        private readonly List<IDisposable> _subscriptions = new();

        public CleanRoomDetectionSystem(IWorldBlockDatastore world)
        {
            _world = world;

            // tick ごとに dirty なら再検出
            // Re-detect on tick when dirty.
            _subscriptions.Add(GameUpdater.UpdateObservable.Subscribe(_ => Update()));

            // ブロック設置/破壊で dirty を立てる
            // Mark dirty on block place/remove.
            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(_ => _dirty = true));
            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(_ => _dirty = true));

            _dirty = true; // 起動/ロード直後に一度フル検出
        }

        private void Update()
        {
            if (!_dirty) return;
            _dirty = false;

            // フェーズ1は全走査で再構築（dirty領域差分更新はフェーズ2以降で最適化）。
            // Phase 1 recomputes from full scan; incremental dirty-region update is deferred.
            _rooms = CleanRoomDetector.DetectAllRooms(_world);
        }

        public void Destroy()
        {
            foreach (var s in _subscriptions) s.Dispose();
            _subscriptions.Clear();
        }
    }
}
```

> `GameUpdater.UpdateObservable.Subscribe` / `ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent` の戻り値が `IDisposable` であることを `Core.Update/GameUpdater.cs` と `Game.World/WorldBlockUpdateEvent.cs` で確認。asmdef に `Game.Context` 参照が要る。

- [ ] **Step 4: DI に登録して eager materialize**

`Server.Boot/MoorestechServerDIContainerGenerator.cs` の `Create(...)` 内、他の世界システム（`GearNetworkDatastore` 等）が `services.AddSingleton<...>()` されている箇所に追加:

```csharp
            services.AddSingleton<Game.CleanRoom.CleanRoomDetectionSystem>();
```

そして `serviceProvider.GetService<...>()` で eager に実体化している箇所に追加:

```csharp
            serviceProvider.GetService<Game.CleanRoom.CleanRoomDetectionSystem>();
```

> `Server.Boot` の asmdef に `Game.CleanRoom` 参照を追加する必要がある。コンパイルエラーで判明したら追加。

- [ ] **Step 5: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "System_PlaceThenBreak_UpdatesRoomValidity"`
Expected: PASS。

- [ ] **Step 6: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetectionSystem.cs moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs
git commit -m "feat(cleanroom): tick/イベント駆動の CleanRoomDetectionSystem を追加"
```

---

## Task 8: 座標→部屋クエリ と フェーズ1の全テスト緑化

製造機やフェーズ2が「このセルはどの部屋か」を引けるAPIを足し、フェーズ1のテストを全実行して緑を確認する。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetectionSystem.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDetectionTest.cs`

- [ ] **Step 1: 失敗するテストを書く（座標から部屋が引ける）**

```csharp
        [Test]
        public void System_GetRoomAt_ReturnsContainingRoom()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var system = serviceProvider.GetService<Game.CleanRoom.CleanRoomDetectionSystem>();

            for (var x = 0; x < 3; x++)
            for (var y = 0; y < 3; y++)
            for (var z = 0; z < 3; z++)
            {
                if (x == 1 && y == 1 && z == 1) continue;
                world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWall, new Vector3Int(x, y, z),
                    BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            }
            GameUpdater.RunFrames(1);

            Assert.True(system.TryGetRoomAt(new Vector3Int(1, 1, 1), out var room), "Center cell is inside the room");
            Assert.AreEqual(1, room.Volume);
            Assert.False(system.TryGetRoomAt(new Vector3Int(10, 10, 10), out _), "Far cell is in no room");
        }
```

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "System_GetRoomAt_ReturnsContainingRoom"`
Expected: FAIL（`TryGetRoomAt` 未定義）。

- [ ] **Step 3: TryGetRoomAt を実装**

`CleanRoomDetectionSystem` に追加:

```csharp
        // 指定セルを含む部屋を返す。線形探索で十分（部屋数は少ない）。
        // Return the room containing the cell. Linear scan is fine (rooms are few).
        public bool TryGetRoomAt(UnityEngine.Vector3Int cell, out CleanRoom room)
        {
            foreach (var r in _rooms)
            {
                if (r.Contains(cell))
                {
                    room = r;
                    return true;
                }
            }
            room = null;
            return false;
        }
```

> `CleanRoom.Contains` は Task 5 で定義済み（内部 `HashSet<Vector3Int>` で O(1) 判定）。

- [ ] **Step 4: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "System_GetRoomAt_ReturnsContainingRoom"`
Expected: PASS。

- [ ] **Step 5: フェーズ1の全テストを実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"`
Expected: `CleanRoomDetectionTest` の全テスト PASS。

- [ ] **Step 6: 既存テストの非回帰を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(GearBeltConveyor|MachineIO|Fluid)Test"`
Expected: 既存テストが従来どおり PASS（境界ブロック追加・DI追加が既存を壊していないこと）。

- [ ] **Step 7: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/ moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDetectionTest.cs
git commit -m "feat(cleanroom): 座標→部屋クエリ TryGetRoomAt を追加しフェーズ1完了"
```

---

## フェーズ1 完了の定義（Definition of Done）

- 4種の境界 blockType がスキーマ・テンプレート・登録まで通り、テスト mod で設置できる。
- `CleanRoomDetector.DetectAllRooms` が密閉空間を1部屋として検出し、体積V・表面積Sを計算する。
- 穴あきシェルは部屋として検出されない。
- `CleanRoomDetectionSystem` が設置/破壊に追従して部屋集合を更新し、`TryGetRoomAt` で座標から部屋を引ける。
- 既存テストが非回帰。

## フェーズ1で意図的に先送りした事項（後続プラン）

- 純度（不純物濃度・クラス・ヒステリシス・ACH）→ フェーズ2
- dirty 領域の差分更新・非同期分割処理（フェーズ1は全走査）→ フェーズ2以降の最適化
- 空気清浄機・フィルター・電力・汚染源 → フェーズ3
- 製造機の binning 歩留まり・Valid/Degraded/Invalid＋猶予停止 → フェーズ4
- ハッチ/コネクタ/ドアの I/O 挙動・必要な部屋状態のセーブ/ロード → フェーズ5
- 本番 mod（moorestech_master）の blocks.json 配線・モデル/画像アセット → 各フェーズで playable 化する際に対応
```
