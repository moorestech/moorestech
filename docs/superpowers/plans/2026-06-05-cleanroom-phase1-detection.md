# クリーンルーム フェーズ1（境界ブロック＋3D密閉部屋検出）実装プラン

- **改訂: 2026-06-12 — codemap v2 整合＋批判的レビュー反映**（データストア化 `CleanRoomDatastore`／ブロック命名 DoorHatch・ItemHatch・PipeHatch／V・Cells規則／DI橋渡し／`BlockRemoveReason.ManualRemove`／斜め壁気密の訂正／分割・結合・上限・共有ハッチテスト追加／リーク済み領域の再探索防止）。
- **契約（正）**: `2026-06-06-cleanroom-phases2-5-codemap.md`（v2）と `2026-06-06-cleanroom-balance-parameters.md`。本プランはその TDD 展開。食い違いを見つけたら契約側を正としてこちらを直すこと。

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.
>
> **moorestech 固有の必須ルール:**
> - `.cs` 編集後は必ず `uloop compile --project-path ./moorestech_client` でコンパイル確認する。
> - テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"` で実行（クライアントプロジェクトからサーバーテストも走る）。
> - 新規サーバー `.cs`／新規 asmdef を認識させるには Unity の **再起動**が要る場合がある（Refresh では不足）。「型が見つからない」で失敗したら uloop で Unity 再起動してから再試行。
> - blockType スキーマ追加・SourceGenerator の手順は `edit-schema` スキルに従う。テスト作成は `creating-server-tests` スキルに従う。
> - 非ASCIIファイル編集時は AGENTS.md の「文字化け防止ワークフロー」を順守。
>
> **検証済みAPI（2026-06-12 実コード照合済み。本文スニペットはこれに合わせてある）:**
> - `IWorldBlockDatastore.TryAddBlock(BlockId, Vector3Int, BlockDirection, BlockCreateParam[], out IBlock)` / `RemoveBlock(Vector3Int, BlockRemoveReason)` / `BlockMasterDictionary: IReadOnlyDictionary<BlockInstanceId, WorldBlockData>`。
> - `BlockRemoveReason` は `Game.Block.Interface/BlockRemoveReason.cs` の **`Broken` / `ManualRemove` の2値のみ**（`Destroy` は存在しない）。
> - `BlockPositionInfo.MinPos` / `MaxPos`（occupy 範囲、両端含む）。
> - `IBlock.TryGetComponent<T>` は **`Game.Block.Interface.Extension.BlockExtension` の拡張メソッド**（using 必須）。`BlockComponentManager.TryGetComponent` は Destroy 後に例外を投げるが、**remove イベントは `block.Destroy()` より先に発火する**（`WorldBlockDatastore.RemoveBlock` で確認済み）ため購読側で安全に使える。
> - `IBlockTemplate.New/Load`・`BlockSystem(BlockInstanceId, Guid, List<IBlockComponent>, BlockPositionInfo)`・`IBlockComponent { bool IsDestroy; void Destroy(); }` は本文スニペットどおり。
> - `GameUpdater` は **UniRx**（`UpdateObservable: IObservable<Unit>`、テスト時間送りは `RunFrames(uint)`）。`Subscribe(Action)` は UniRx の拡張なので **asmdef 参照と `using UniRx;` が必須**。
> - `MoorestechServerDIContainerGenerator.Create(MoorestechServerDIContainerOptions)` は `(PacketResponseCreator, ServiceProvider)` を返す。**`IWorldBlockDatastore` は initializer 側コンテナにのみ登録されている**（Task 7 の DI 注意を必読）。
> - テスト用 BlockId ヘルパは `ForUnitTestModBlockId.GetBlock(string guid)`（`GetBlockId` ではない）。

**Goal:** クリーンルーム境界ブロック4種（壁/ドアハッチ/アイテムハッチ/パイプハッチ）を定義し、それらで完全に囲まれた3D空間を「クリーンルーム」として検出・登録し、ブロックの設置/破壊に追従して部屋の成立/無効化・V/S を更新するサーバー側システムを実装する。フェーズ2以降（純度・エアフィルター・専用機械・I/O）が乗る土台を作る。

**Architecture:** 世界システムは**データストア方式**の `CleanRoomDatastore`（`GearNetworkDatastore` 同型。codemap §1.1）。フェーズ1では**検出部分のみの骨格**を実装し、フェーズ2が純度tick・永続化・dirty分割（8192セル/tick）・触れた壁AABB+1局所化を**同じクラスに**追加する。`CleanRoomDatastore` は `WorldBlockUpdateEvent` の設置/破壊を購読して geometry-dirty を立て（境界ブロックは常に、非境界ブロックは既存部屋の `Cells` に重なる場合のみ）、`GameUpdater` の次tickで再検出する。検出器 `CleanRoomDetector` は純関数で、境界セル集合＋占有セル集合＋**境界AABB**を使い、AABB外縁に漏れない密閉空間を6近傍 flood-fill して `CleanRoom`（Id, Cells, Volume, SurfaceArea）を作る。**Cells＝機械占有セルを含む全内部セル（帰属判定用）、Volume＝Cells のうち空セル数**（バランス確定書§5）。部屋はブロックから導出可能な派生状態なのでフェーズ1ではセーブせず、ロード後の再検出で再構築する。フェーズ2は公開済み `Cells` のセル重なりで再検出前後の部屋を対応付ける（`Id` は永続キーにしない）。

**Tech Stack:** C# (Unity, moorestech_server), UniRx `IObservable`（`GameUpdater.UpdateObservable`）, NUnit (Server.Tests), Mooresmaster Source Generator (blocks.yml → BlocksModule)。

---

## 仕様§2「再検出の実装方針」との関係（逸脱の明示）

設計書は (1) 変更セルの dirty **area** 積み、(2) 次tick以降の**分割処理**、(3) 既知 room 境界からの**差分更新**、を前提とする。フェーズ1が満たすのは「**次tick遅延（dirtyフラグ）**」のみで、残りは以下の分担（フェーズ間で先送りループにしない。バランス確定書§5）:

| 項目 | フェーズ1 | フェーズ2（担当・DoDに含む） |
|---|---|---|
| dirty 粒度 | 単一フラグ | dirty area 積み＋**8192セル/tick** の分割処理（超過分は次tickへ繰越） |
| リーク判定境界 | グローバル境界AABB | **触れた壁AABB+1** に局所化 |
| 更新方式 | 全走査 `RebuildAll` | 差分更新基本 |

フェーズ1の暫定コストは「境界/室内ブロック変更があった tick の次tickに全走査1回」。リーク探索の暴走は (a) AABB外到達で即リーク、(b) **リーク確定済み探索域への接触で即リーク**（同一連結空間の再探索防止。Task 5）、(c) `MaxRoomVolume` 安全網、の3段で抑える。

---

## 後続プランのロードマップ（このプランの対象外）

| フェーズ | 内容 | 主産物 |
|---|---|---|
| **1（本プラン）** | 境界ブロック4種＋3D密閉部屋検出＋`CleanRoomDatastore` 骨格（検出のみ）＋部屋クエリ | 壁で囲うと部屋検出、壊すと無効化 |
| 2 | `CleanRoomDatastore` 拡張: 純度シミュ（N/V/S、A_total、n·q·C、平衡、閾値行＋ヒステリシス、ACH）＋セル重なり対応付け・N按分＋永続化＋**dirty分割（8192セル/tick）＋触れた壁AABB+1局所化** | 部屋に効果が付き汚染/除去に応答 |
| 3 | `CleanRoomAirFilter`（内部ブロック・単一コンポーネント: 電力＋フィルター仕事量＋q）＋汚染源4種。境界種別 `CleanRoomBoundaryKind` を `a_connector` 別集計で参照 | 維持ループが回る |
| 4 | `CleanRoomMachine`（専用機械・**Vanilla機械ファイル非改変**）＋プッシュ受信（`ICleanRoomStateReceiver`/`CleanRoomEffect`）＋binning。部屋帰属は `Cells` ベースの `TryGetCleanRoom` | 半導体生産が部屋純度に依存 |
| 5 | ハッチ3種の I/O 挙動（ドアハッチ=人・アイテムハッチ=アイテム・パイプハッチ=流体）＋`A_hatch`/`A_door` 計量＋境界ブロック用クエリ `GetAdjacentCleanRooms(IBlock)`＋セーブ仕上げ | 完全な遊べる形 |

---

## File Structure（フェーズ1で作成/変更するファイル）

**スキーマ／マスタ（境界ブロック定義）**
- Modify: `VanillaSchema/blocks.yml` — `blockType` enum に4種追加＋blockParam の switch case 追加
- Modify: `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` — SourceGenerator トリガ
- Modify: テスト用mod `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTest/mods/forUnitTest/master/items.json` と `.../master/blocks.json` — テスト用の4ブロックを追加
- Modify: `moorestech_server/Assets/Scripts/Tests.Module/TestMod/ForUnitTestModBlockId.cs` — テスト用 BlockId アクセサ追加

**境界ブロックの実装**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomBoundaryComponent.cs` — 境界マーカー＋種別
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomBoundaryComponent.cs` — マーカー実装
- Create: `moorestech_server/Assets/Scripts/Game.Block/Factory/BlockTemplate/VanillaCleanRoomBoundaryTemplate.cs` — 4種共通テンプレート（種別をコンストラクタで受ける）
- Modify: `moorestech_server/Assets/Scripts/Game.Block/Factory/VanillaIBlockTemplates.cs` — 4種を登録

**検出システム（新規アセンブリ Game.CleanRoom）**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/Game.CleanRoom.asmdef`
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoom.cs` — 部屋データ（フェーズ2が純度状態を同クラスに追加）
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDetector.cs` — 検出ロジック（純関数）
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs` — 世界データストア（フェーズ1は検出骨格のみ）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs` — **initializer 側登録＋main へのインスタンス橋渡し**（asmdef参照追加も）
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
（`CleanRoomAirFilter`／`CleanRoomMachine` は**内部ブロック**でありフェーズ3/4が追加する。フェーズ1は境界4種のみ。codemap §0）

- [ ] **Step 1: blocks.yml の blockType enum に4種を追加**

`VanillaSchema/blocks.yml` の `blockType`（`- key: blockType` / `type: enum`）の `options:` 配列に、既存値の末尾へ追加:

```yaml
      - CleanRoomWall
      - CleanRoomDoorHatch
      - CleanRoomItemHatch
      - CleanRoomPipeHatch
```

- [ ] **Step 2: blockParam の switch cases に4種を追加**

`blockParam` の `switch: ./blockType` の `cases:` は**全 blockType の case を持つ**（確認済み）。param 無しの既存例 `Block` は `properties: []` 形式。同形式で4種を追加:

```yaml
      - when: CleanRoomWall
        type: object
        properties: []
      - when: CleanRoomDoorHatch
        type: object
        properties: []
      - when: CleanRoomItemHatch
        type: object
        properties: []
      - when: CleanRoomPipeHatch
        type: object
        properties: []
```

- [ ] **Step 3: SourceGenerator をトリガ**

`Core.Master/_CompileRequester.cs` の `dummyText` 定数値を変更する（クラス名は `CompileRequester`、現値はタイムスタンプ文字列）。このファイルは Unity 内の SchemaWatcher が自動更新するため、**Unity 起動中に blocks.yml を保存した時点で自動書き換えされている場合は手動変更不要**（その差分をコミットに含めるだけでよい）:

```csharp
    private const string dummyText = "regenerate-cleanroom-phase1";
```

- [ ] **Step 4: 再生成を確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。`Mooresmaster.Model.BlocksModule.BlockTypeConst` に `CleanRoomWall`/`CleanRoomDoorHatch`/`CleanRoomItemHatch`/`CleanRoomPipeHatch` が生成される（Task 3 で参照確認）。

> 「Domain Reload in progress」なら45秒待って再試行。型未検出なら Unity 再起動。

- [ ] **Step 5: Commit**

```bash
git add VanillaSchema/blocks.yml moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs
git commit -m "feat(cleanroom): 境界ブロック4種の blockType をスキーマに追加"
```

---

## Task 2: 境界マーカー interface（種別付き）とコンポーネント

検出システムが「このブロックは気密境界か」を判定するマーカー。フェーズ3/5 で汚染計算（`a_connector` 別集計・ハッチ/ドア汚染）が境界の**種別**を要るので、最初から `CleanRoomBoundaryKind` を持たせておく。種別は codemap §0 の確定名（旧 `Door`/`PipeConnector` は廃名）。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.Block.Interface/Component/ICleanRoomBoundaryComponent.cs`
- Create: `moorestech_server/Assets/Scripts/Game.Block/Blocks/CleanRoom/CleanRoomBoundaryComponent.cs`

検証は Task 6 のテストで行う（ここではコンパイルのみ）。

- [ ] **Step 1: マーカー interface ＋ 種別 enum を作成**

`Game.Block.Interface/Component/ICleanRoomBoundaryComponent.cs`:

```csharp
namespace Game.Block.Interface.Component
{
    // 境界ブロックの種別。フェーズ3/5の汚染計算・I/Oで参照する。
    // Kind of boundary block; consumed by phase-3/5 pollution calc and I/O.
    public enum CleanRoomBoundaryKind
    {
        Wall,
        DoorHatch,
        ItemHatch,
        PipeHatch,
    }

    // クリーンルームの気密境界として機能するブロックが実装するマーカー。
    // Marker for blocks that act as an airtight boundary of a clean room.
    public interface ICleanRoomBoundaryComponent : IBlockComponent
    {
        CleanRoomBoundaryKind BoundaryKind { get; }
    }
}
```

- [ ] **Step 2: マーカー実装コンポーネントを作成**

`Game.Block/Blocks/CleanRoom/CleanRoomBoundaryComponent.cs`（`IBlockComponent` の要求メンバは `IsDestroy`/`Destroy()` のみ。確認済み）:

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

4種とも挙動は同じ（種別付きマーカーを付けるだけ）なので、種別をコンストラクタで受ける1つの共通テンプレートを4インスタンス登録する。`IBlockTemplate`/`BlockSystem` のシグネチャは検証済み（`VanillaChestTemplate.cs` と同形）。

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

- [ ] **Step 2: VanillaIBlockTemplates に4種を登録**

`Game.Block/Factory/VanillaIBlockTemplates.cs` のコンストラクタの `BlockTypesDictionary.Add(...)` 群に追加:

```csharp
            BlockTypesDictionary.Add(BlockTypeConst.CleanRoomWall, new VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind.Wall));
            BlockTypesDictionary.Add(BlockTypeConst.CleanRoomDoorHatch, new VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind.DoorHatch));
            BlockTypesDictionary.Add(BlockTypeConst.CleanRoomItemHatch, new VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind.ItemHatch));
            BlockTypesDictionary.Add(BlockTypeConst.CleanRoomPipeHatch, new VanillaCleanRoomBoundaryTemplate(CleanRoomBoundaryKind.PipeHatch));
```

> `using Mooresmaster.Model.BlocksModule;`（BlockTypeConst）と `using Game.Block.Interface.Component;`（CleanRoomBoundaryKind）が無ければ追加。

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

- [ ] **Step 1: items.json に4アイテムを追加**

`data` 配列に4種のテスト用アイテムを追加（既存エントリ形式に合わせる。`itemGuid` は `uuidgen` 等で新規生成し既存と衝突させない）。

- [ ] **Step 2: blocks.json に4ブロックを追加**

`data` 配列に4種を追加。param 無し blockType の既存エントリ（`TestBlock`, blockType `Block`）の実形式（確認済み）に合わせる:

```json
{
  "name": "TestCleanRoomWall",
  "blockType": "CleanRoomWall",
  "blockGuid": "<新規GUID-Wall>",
  "itemGuid": "<Step1のGUID-Wall>",
  "blockSize": [1, 1, 1],
  "blockParam": {},
  "modelTransform": {"scale": [1, 1, 1], "rotation": [0, 0, 0], "position": [0, 0, 0]},
  "overrideVerticalBlock": {}
}
```

`TestCleanRoomDoorHatch`（blockType `CleanRoomDoorHatch`）/`TestCleanRoomItemHatch`（`CleanRoomItemHatch`）/`TestCleanRoomPipeHatch`（`CleanRoomPipeHatch`）も同様。

- [ ] **Step 3: ForUnitTestModBlockId に4アクセサを追加**

ヘルパ名は `GetBlock(string guid)`（確認済み）。既存の命名規約（`...Id` サフィックス）に合わせる:

```csharp
        public static BlockId CleanRoomWallId => GetBlock("<TestCleanRoomWall の blockGuid>");
        public static BlockId CleanRoomDoorHatchId => GetBlock("<同 DoorHatch>");
        public static BlockId CleanRoomItemHatchId => GetBlock("<同 ItemHatch>");
        public static BlockId CleanRoomPipeHatchId => GetBlock("<同 PipeHatch>");
```

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: 成功。

- [ ] **Step 5: 設置スモークテストを書いて緑にする**

`Tests/CombinedTest/Core/CleanRoomDetectionTest.cs` を新規作成し、「境界ブロックを設置でき、種別付きマーカーを持つ」最小テストを追加。**using はこの一覧をそのまま使う**（`Game.Block.Interface.Extension`＝`TryGetComponent` 拡張、`Microsoft.Extensions.DependencyInjection`＝後続タスクの `GetService` 用）:

```csharp
using System;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using Microsoft.Extensions.DependencyInjection;
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

            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomItemHatchId, new Vector3Int(0, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var hatch);

            Assert.True(hatch.TryGetComponent<ICleanRoomBoundaryComponent>(out var marker));
            Assert.AreEqual(CleanRoomBoundaryKind.ItemHatch, marker.BoundaryKind);
        }
    }
}
```

- [ ] **Step 6: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceBoundaryBlock_HasKindedBoundaryComponent"`
Expected: PASS。

- [ ] **Step 7: Commit**

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

### アルゴリズム仕様（このフェーズで確定。契約: codemap §1.2／バランス確定書§0・§5）

- **境界ブロック** = `ICleanRoomBoundaryComponent` を持つブロック（壁/ドアハッチ/アイテムハッチ/パイプハッチ）。これだけが気密面になる。
- **通過セル（passable）** = 境界ブロックが存在しないセル。flood-fill は**内部の非境界ブロック（機械等）を通過する**。
- **Cells** = 部屋の全通過セル（**機械等の占有セルを含む**）。ブロックの「部屋内」帰属判定に使う。
- **Volume V** = Cells のうち**空セル数**（非境界ブロックの占有セルは V から除外。機械を詰めると実効 V が減り濃度が跳ねやすくなる＝詰め込みの代償。バランス確定書§5）。**V=0（全セル占有）の部屋も成立し得る**（フェーズ2の `C=N/V` は `V>0` ガード。codemap §1.2）。
- **表面積 S** = **空セル**の面のうち境界ブロックセルに接する面の数（バランス確定書§0）。内部に置いた境界ブロック柱の露出面も S に入る。占有セルの面は数えない。
- **境界AABB** = 全境界ブロックセルの最小〜最大座標で作る直方体（フェーズ1はグローバル。触れた壁AABB+1への局所化はフェーズ2）。
- **リーク判定:** seed から6近傍 flood-fill し、(a) 通過セルが境界AABBの外に出たら即リーク、(b) **過去にリーク確定した探索済みセルに触れたら即リーク**（同じ連結空間は全てリーク。同一リーク領域を複数シードから再探索する無駄も防ぐ）、(c) Cells 数が `MaxRoomVolume` を超えたら不成立（安全網）。
- **近傍は6近傍**（面接触）。**エッジのみ接触の斜め壁は気密＝リークにならない**（6近傍では空気は斜めに移動できない。設計書§2「コーナーだけで密閉した2壁片も自然に正しくなる」と整合。旧記述「斜めの隙間はリーク」は**誤り**なので注意。バランス確定書§5）。
- **ハッチが2部屋の共有壁にある場合**: ハッチは常に気密境界＝**2部屋を隔離**する（接続しない）。境界ブロックのセルはどの部屋の Cells にも含まれないため、帰属解決（面する部屋を引く）はフェーズ3/5 の `GetAdjacentCleanRooms(IBlock)` が担う。
- 同一通過セルは1部屋にのみ属する（visited で重複排除）。

定数（バランス確定書§5 の確定値・根拠ごと転記）:
```csharp
// 安全網。根拠: 大部屋例 V=500（10×10×5）の8倍超を許容しつつ、未密閉構造のリーク探索コストを抑える。
// 大部屋戦略を殺さないかはプレイテストで再評価（バランス確定書§5）。Cells 数（占有セル含む）に適用。
public const int MaxRoomVolume = 4096;
```

- [ ] **Step 1: アセンブリ定義を作成**

`Game.CleanRoom/Game.CleanRoom.asmdef`。**`UniRx` 参照を忘れない**（`GameUpdater.UpdateObservable` の `Unit` と `Subscribe(Action)` 拡張に必須）:

```json
{
    "name": "Game.CleanRoom",
    "references": [
        "Game.Block.Interface",
        "Game.World.Interface",
        "Game.Context",
        "Core.Update",
        "Core.Master",
        "UniRx"
    ],
    "autoReferenced": true
}
```

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
            Assert.AreEqual(1, rooms[0].Cells.Count);
            Assert.AreEqual(6, rooms[0].SurfaceArea, "A single cell touches 6 wall faces");
        }

        // 1セルの壁を置くヘルパ。
        // Helper: place a single wall cell.
        private static void PlaceWall(IWorldBlockDatastore world, Vector3Int pos)
        {
            world.TryAddBlock(ForUnitTestModBlockId.CleanRoomWallId, pos,
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }

        // min..max の外殻だけ壁を置き、内部を空洞にするヘルパ。
        // Helper: place walls only on the shell of [min,max], leaving the interior hollow.
        private static void BuildWallShell(IWorldBlockDatastore world, Vector3Int min, Vector3Int max)
        {
            for (var x = min.x; x <= max.x; x++)
            for (var y = min.y; y <= max.y; y++)
            for (var z = min.z; z <= max.z; z++)
            {
                var onShell = x == min.x || x == max.x || y == min.y || y == max.y || z == min.z || z == max.z;
                if (!onShell) continue;
                PlaceWall(world, new Vector3Int(x, y, z));
            }
        }
```

- [ ] **Step 3: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Detect_SealedShell_ReturnsOneRoom"`
Expected: FAIL（`CleanRoomDetector` 未定義）。

- [ ] **Step 4: CleanRoom データ型を実装（Cells 公開・Volume は空セル数）**

`Game.CleanRoom/CleanRoom.cs`。`IsValid` フラグは**持たない**（検出器は成立部屋しか返さないため常に true の死にフィールドになる。Valid/Degraded/Invalid の状態列挙 `CleanRoomRoomStatus` と純度状態はフェーズ2が codemap §1.2 の形で本クラスに追加する）:

```csharp
using System.Collections.Generic;
using UnityEngine;

namespace Game.CleanRoom
{
    // 検出された1つのクリーンルーム。ブロックから導出される派生状態。
    // フェーズ2が純度状態（N/Status/ThresholdIndex）を本クラスに追加する（codemap §1.2）。
    // A single detected clean room; derived state computed from blocks.
    // Phase 2 adds purity state (N/Status/ThresholdIndex) to this class (codemap §1.2).
    public class CleanRoom
    {
        // Id は一時参照用。永続キーにしてはいけない（フェーズ2はCellsの重なりで同一性を取る）。
        // Id is an ephemeral handle, NOT a persistence key (phase 2 matches rooms by cell overlap).
        public int Id { get; }

        // Cells は機械等の占有セルを含む全内部セル。ブロックの「部屋内」帰属判定に使う。
        // Cells contains all interior cells incl. machine-occupied ones; used for room-membership checks.
        public IReadOnlyCollection<Vector3Int> Cells => _cells;

        // V = Cells のうち空セル数（占有セルは除外）。S = 空セルが境界に接する面の数。
        // V = empty-cell count within Cells (occupied excluded). S = empty-cell faces touching boundary.
        public int Volume { get; }
        public int SurfaceArea { get; }

        private readonly HashSet<Vector3Int> _cells;

        public CleanRoom(int id, HashSet<Vector3Int> cells, int volume, int surfaceArea)
        {
            Id = id;
            _cells = cells;
            Volume = volume;
            SurfaceArea = surfaceArea;
        }

        public bool Contains(Vector3Int cell) => _cells.Contains(cell);
    }
}
```

- [ ] **Step 5: CleanRoomDetector を実装（AABBリーク判定＋リーク済み領域接触で即リーク）**

`Game.CleanRoom/CleanRoomDetector.cs`:

```csharp
using System.Collections.Generic;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.CleanRoom
{
    public static class CleanRoomDetector
    {
        // 安全網。根拠: 大部屋例 V=500（10×10×5）の8倍超を許容しつつ、未密閉構造のリーク探索コストを抑える。
        // 大部屋戦略を殺さないかはプレイテストで再評価（バランス確定書§5）。Cells 数（占有セル含む）に適用。
        // Safety net (balance doc §5): allows >8x the large-room example V=500 while bounding leak-scan cost.
        public const int MaxRoomVolume = 4096;

        // ワールド全体を走査し、密閉されたクリーンルームをすべて返す。
        // Scan the whole world and return all sealed clean rooms.
        public static List<CleanRoom> DetectAllRooms(IWorldBlockDatastore world)
        {
            BuildCellSets(world, out var boundaryCells, out var occupiedCells);
            var rooms = new List<CleanRoom>();
            if (boundaryCells.Count == 0) return rooms;

            // 境界AABB。密閉された部屋の通過セルはこの内側に必ず収まる（フェーズ1はグローバル）。
            // Boundary AABB; passable cells of a sealed room must stay inside it (global in phase 1).
            ComputeAabb(boundaryCells, out var min, out var max);

            var globalVisited = new HashSet<Vector3Int>();
            var nextId = 0;

            foreach (var boundaryCell in boundaryCells)
            foreach (var seed in SixNeighbors(boundaryCell))
            {
                if (boundaryCells.Contains(seed)) continue;
                if (globalVisited.Contains(seed)) continue;
                if (IsOutsideAabb(seed, min, max)) continue; // AABB外の種は外部空間

                if (TryFloodFill(seed, out var cells, out var volume, out var surface))
                    rooms.Add(new CleanRoom(nextId++, cells, volume, surface));
            }

            return rooms;

            #region Internal

            // 種から通過セルを flood-fill。AABB外到達・リーク済み領域接触・上限超過で false。
            // Flood-fill passable cells; false on AABB exit, leaked-region contact, or cap overflow.
            bool TryFloodFill(Vector3Int start, out HashSet<Vector3Int> cells, out int volume, out int surfaceArea)
            {
                cells = new HashSet<Vector3Int>();
                volume = 0;
                surfaceArea = 0;
                var stack = new Stack<Vector3Int>();
                stack.Push(start);
                cells.Add(start);

                while (stack.Count > 0)
                {
                    var cur = stack.Pop();

                    if (cells.Count > MaxRoomVolume || IsOutsideAabb(cur, min, max)) return Fail(cells);

                    // 空セルのみ V/S に計上する（占有セルは Cells のみ。V・Cells規則）
                    // Only empty cells contribute to V/S (occupied cells are Cells-only)
                    var isEmpty = !occupiedCells.Contains(cur);
                    if (isEmpty) volume++;

                    foreach (var n in SixNeighbors(cur))
                    {
                        if (boundaryCells.Contains(n))
                        {
                            if (isEmpty) surfaceArea++;
                            continue;
                        }
                        // リーク確定済みの探索域に触れた＝この連結空間もリーク（再探索防止）
                        // Touching a previously failed region means this fill leaks too (no re-scan)
                        if (!cells.Contains(n) && globalVisited.Contains(n)) return Fail(cells);
                        if (cells.Add(n)) stack.Push(n);
                    }
                }

                foreach (var c in cells) globalVisited.Add(c);
                return true;
            }

            // 探索済みを visited 登録して不成立。以後この領域に触れた fill も即リークになる。
            // Register explored cells as visited and fail; later fills touching them leak instantly.
            bool Fail(HashSet<Vector3Int> cells)
            {
                foreach (var c in cells) globalVisited.Add(c);
                return false;
            }

            #endregion
        }

        // 全ブロックのセルを境界/占有（非境界）に分けて一括構築（GetBlockのO(n)を避ける）。
        // Build boundary/occupied cell sets in one pass over all blocks.
        private static void BuildCellSets(IWorldBlockDatastore world,
            out HashSet<Vector3Int> boundaryCells, out HashSet<Vector3Int> occupiedCells)
        {
            boundaryCells = new HashSet<Vector3Int>();
            occupiedCells = new HashSet<Vector3Int>();
            foreach (var kvp in world.BlockMasterDictionary)
            {
                var data = kvp.Value;
                var isBoundary = data.Block.TryGetComponent<ICleanRoomBoundaryComponent>(out _);
                var target = isBoundary ? boundaryCells : occupiedCells;

                var info = data.BlockPositionInfo;
                for (var x = info.MinPos.x; x <= info.MaxPos.x; x++)
                for (var y = info.MinPos.y; y <= info.MaxPos.y; y++)
                for (var z = info.MinPos.z; z <= info.MaxPos.z; z++)
                    target.Add(new Vector3Int(x, y, z));
            }
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

> 正しさのメモ: 成立した部屋の全セルは visited 登録されるため同一部屋の二重検出は無い（同部屋の別シードは visited でスキップ）。fill 中に「自分の cells に無い visited セル」へ触れるのは、同じ連結空間が過去にリーク確定している場合だけ（成立部屋とは連結し得ない）なので、接触＝即リークは正しい。

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

## Task 6: 検出ロジックの網羅テスト

密閉/リーク/複数部屋/境界種別/非境界ブロック/**V・Cells規則/斜め壁気密/内部パーティション分割/結合/共有ハッチ隔離/体積上限**を網羅する。各テストを1つずつ追加→実行で緑を確認していく。`PlaceWall`/`BuildWallShell` ヘルパ（Task 5）を再利用する。

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
            world.RemoveBlock(new Vector3Int(1, 1, 0), BlockRemoveReason.ManualRemove); // 面に穴

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(0, rooms.Count, "A shell with a hole must not form a sealed room");
        }
```

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Detect_ShellWithHole_ReturnsNoRoom"` → PASS（穴から (1,1,-1)＝AABB外へ到達して即リーク）。

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

- [ ] **Step 4: ドアハッチ/アイテムハッチ/パイプハッチも境界として密閉する**

```csharp
        [Test]
        public void Detect_DoorHatchItemHatchPipeHatch_AlsoSeal()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            // 壁3枚をドアハッチ/アイテムハッチ/パイプハッチに差し替える
            ReplaceWith(world, new Vector3Int(1, 1, 0), ForUnitTestModBlockId.CleanRoomDoorHatchId);
            ReplaceWith(world, new Vector3Int(1, 1, 2), ForUnitTestModBlockId.CleanRoomItemHatchId);
            ReplaceWith(world, new Vector3Int(0, 1, 1), ForUnitTestModBlockId.CleanRoomPipeHatchId);

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(1, rooms.Count, "DoorHatch/ItemHatch/PipeHatch must seal the room");
            Assert.AreEqual(1, rooms[0].Volume);
        }

        private static void ReplaceWith(IWorldBlockDatastore world, Vector3Int pos, BlockId blockId)
        {
            world.RemoveBlock(pos, BlockRemoveReason.ManualRemove);
            world.TryAddBlock(blockId, pos, BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
        }
```

Run → PASS。

- [ ] **Step 5: 壁穴に非境界ブロックを置いても密閉しない**

```csharp
        [Test]
        public void Detect_NonBoundaryBlockInHole_DoesNotSeal()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            world.RemoveBlock(new Vector3Int(1, 1, 0), BlockRemoveReason.ManualRemove);
            // 穴に非境界ブロックを置く（占有セル＝通過セルなので気密面にはならない）
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(1, 1, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(0, rooms.Count, "Only boundary blocks seal; a normal block does not");
        }
```

Run → PASS（fill は非境界ブロックを通過して穴から漏れる）。

- [ ] **Step 6: 室内の非境界ブロックは Cells に含まれ V/S から除外される（V・Cells規則）**

5x5x5 室内（Cells=27）の内部コーナー (1,1,1)（壁3面に接する）に機械相当の非境界ブロックを置く:

```csharp
        [Test]
        public void Detect_InteriorBlock_ExcludedFromVolume_IncludedInCells()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(1, 1, 1),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(1, rooms.Count);
            Assert.AreEqual(27, rooms[0].Cells.Count, "Cells includes occupied interior cells");
            Assert.AreEqual(26, rooms[0].Volume, "Occupied cells are excluded from V");
            Assert.AreEqual(51, rooms[0].SurfaceArea, "3 wall faces of the occupied corner cell leave S");
            Assert.True(rooms[0].Contains(new Vector3Int(1, 1, 1)), "Occupied cell still belongs to the room");
        }
```

Run → PASS。

- [ ] **Step 7: エッジのみ接触の斜め壁は気密（6近傍の確定挙動）**

z=1 の内部 3x3 を斜め壁3枚（互いにエッジ接触のみ）で対角に仕切る。6近傍では空気が斜めを通れないため両側とも密閉部屋になる:

```csharp
        [Test]
        public void Detect_DiagonalWalls_AreAirtight()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // 床(z=0)・天井(z=2)は全面壁、z=1 は外周リング＋斜め壁3枚。
            // Floor (z=0) and ceiling (z=2) are full walls; z=1 has the ring plus 3 diagonal walls.
            for (var x = 0; x <= 4; x++)
            for (var y = 0; y <= 4; y++)
            {
                PlaceWall(world, new Vector3Int(x, y, 0));
                PlaceWall(world, new Vector3Int(x, y, 2));
                if (x == 0 || x == 4 || y == 0 || y == 4) PlaceWall(world, new Vector3Int(x, y, 1));
            }
            // (1,3)-(2,2)-(3,1) は互いにエッジ接触のみ（面接触なし）
            PlaceWall(world, new Vector3Int(1, 3, 1));
            PlaceWall(world, new Vector3Int(2, 2, 1));
            PlaceWall(world, new Vector3Int(3, 1, 1));

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(2, rooms.Count, "Edge-only diagonal walls are airtight under 6-neighbor fill");
            Assert.AreEqual(3, rooms[0].Volume);
            Assert.AreEqual(3, rooms[1].Volume);
        }
```

Run → PASS。

- [ ] **Step 8: 内部パーティションで部屋が分割される**

```csharp
        [Test]
        public void Detect_InternalPartition_SplitsIntoTwoRooms()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            // x=2 の内部断面 3x3 を壁で完全に仕切る → 1x3x3 の部屋が2つ
            for (var y = 1; y <= 3; y++)
            for (var z = 1; z <= 3; z++)
                PlaceWall(world, new Vector3Int(2, y, z));

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(2, rooms.Count, "A full interior partition splits the room");
            Assert.AreEqual(9, rooms[0].Volume);
            Assert.AreEqual(9, rooms[1].Volume);
        }
```

Run → PASS。

- [ ] **Step 9: パーティション壁の撤去で部屋が結合される**

```csharp
        [Test]
        public void Detect_RemovePartitionWall_MergesIntoOneRoom()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            for (var y = 1; y <= 3; y++)
            for (var z = 1; z <= 3; z++)
                PlaceWall(world, new Vector3Int(2, y, z));
            // 仕切りの中央を撤去 → 開通して1部屋（9+9+開通セル1=19）
            world.RemoveBlock(new Vector3Int(2, 2, 2), BlockRemoveReason.ManualRemove);

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(1, rooms.Count, "Removing a partition cell merges the two rooms");
            Assert.AreEqual(19, rooms[0].Volume);
        }
```

Run → PASS。

- [ ] **Step 10: 2部屋の共有壁にあるハッチは部屋を隔離する（確定仕様）**

```csharp
        [Test]
        public void Detect_HatchOnSharedWall_KeepsRoomsIsolated()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // x=2 の壁面を共有する2シェル（共有面への重複設置は単に失敗して共有される）
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            BuildWallShell(world, new Vector3Int(2, 0, 0), new Vector3Int(4, 2, 2));
            // 共有壁セルをアイテムハッチに差し替えても気密境界のまま＝2部屋は接続しない
            ReplaceWith(world, new Vector3Int(2, 1, 1), ForUnitTestModBlockId.CleanRoomItemHatchId);

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(2, rooms.Count, "A hatch on a shared wall isolates (never connects) the rooms");
        }
```

Run → PASS。（ハッチの帰属＝「面する部屋」の解決はフェーズ3/5 の `GetAdjacentCleanRooms(IBlock)`。フェーズ1の部屋クエリは境界ブロックに対し常に失敗する仕様＝Task 8 でテスト。）

- [ ] **Step 11: `MaxRoomVolume` 超過は部屋不成立（安全網）**

```csharp
        [Test]
        public void Detect_VolumeOverMax_DoesNotFormRoom()
        {
            var (_, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;

            // 内寸 17x16x16 = 4352 セル > MaxRoomVolume(4096)
            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(18, 17, 17));

            var rooms = Game.CleanRoom.CleanRoomDetector.DetectAllRooms(world);
            Assert.AreEqual(0, rooms.Count, "Rooms exceeding MaxRoomVolume cells must not form");
        }
```

Run → PASS。（壁約1800個の設置と約4100セルの fill。遅い場合もテストは維持し、検出コスト自体はフェーズ2の dirty 分割・局所化で解決する。）

- [ ] **Step 12: Commit**

```bash
git add moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDetectionTest.cs
git commit -m "test(cleanroom): 密閉/リーク/分割/結合/斜め壁/共有ハッチ/上限の網羅テストを追加"
```

---

## Task 7: CleanRoomDatastore（dirty 制御・RebuildAll 公開・DI橋渡し）

検出ロジックを世界データストアに載せる（`GearNetworkDatastore` 同型。フェーズ2が純度tick・永続化を同じクラスに足す前提の骨格）。dirty 条件は2系統:
- **境界ブロック**の設置/破壊 → 常に dirty。
- **非境界ブロック**の設置/破壊 → 占有セルが既存部屋の `Cells` に重なる場合のみ dirty（室内の機械設置で V/S が変わるため。バランス確定書§5「内部ブロックの設置/削除でも V が変わるため dirty 対象」）。部屋外の非境界ブロックは部屋形状に影響しないので再検出しない。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`

- [ ] **Step 1: 失敗テスト（設置→tick→検出、破壊→tick→無効化、部屋外の非境界はdirtyにしない、室内の非境界はVを更新）**

```csharp
        [Test]
        public void Datastore_PlaceThenBreakBoundary_UpdatesRooms()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<Game.CleanRoom.CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(2, 2, 2));
            GameUpdater.RunFrames(1);
            Assert.AreEqual(1, datastore.Rooms.Count);

            world.RemoveBlock(new Vector3Int(1, 1, 0), BlockRemoveReason.ManualRemove);
            GameUpdater.RunFrames(1);
            Assert.AreEqual(0, datastore.Rooms.Count);
        }

        [Test]
        public void Datastore_FarNonBoundaryPlacement_DoesNotRebuild()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<Game.CleanRoom.CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            GameUpdater.RunFrames(1);
            var before = datastore.RebuildCount;

            // 既存部屋の Cells に重ならない非境界ブロック → 再検出は走らない
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(50, 0, 0),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            GameUpdater.RunFrames(1);

            Assert.AreEqual(before, datastore.RebuildCount, "Non-boundary placement outside rooms must not trigger re-detection");
        }

        [Test]
        public void Datastore_InteriorBlockPlacement_RebuildsAndUpdatesVolume()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<Game.CleanRoom.CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            GameUpdater.RunFrames(1);
            Assert.AreEqual(27, datastore.Rooms[0].Volume);

            // 室内（Cells 内）への非境界ブロック設置は V が変わるので dirty 対象
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(2, 2, 2),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out _);
            GameUpdater.RunFrames(1);

            Assert.AreEqual(1, datastore.Rooms.Count);
            Assert.AreEqual(26, datastore.Rooms[0].Volume, "Interior occupied cell is removed from V");
        }
```

`RebuildCount` は再検出回数を数えるテスト用カウンタ（Step 3 で実装）。同tick内の複数変更は dirty フラグに集約され、次tickの再検出は1回だけになる（`Datastore_PlaceThenBreakBoundary_UpdatesRooms` の `BuildWallShell`＝26個設置→rebuild 1回、で間接的に検証される）。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Datastore_(PlaceThenBreakBoundary_UpdatesRooms|FarNonBoundaryPlacement_DoesNotRebuild|InteriorBlockPlacement_RebuildsAndUpdatesVolume)"`
Expected: FAIL（`CleanRoomDatastore` 未定義）。

- [ ] **Step 3: CleanRoomDatastore を実装**

`Game.CleanRoom/CleanRoomDatastore.cs`（クラス直下の private メソッドは `#region Internal` で囲まない＝AGENTS.md 準拠）:

```csharp
using System;
using System.Collections.Generic;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.World.Interface.DataStore;
using UniRx;
using UnityEngine;

namespace Game.CleanRoom
{
    // クリーンルーム系の中核データストア（GearNetworkDatastore 同型）。
    // フェーズ1は検出のみ。フェーズ2が純度tick・永続化・dirty分割を本クラスに追加する。
    // Core clean-room datastore (same shape as GearNetworkDatastore).
    // Phase 1 is detection only; phase 2 adds the purity tick, persistence and dirty slicing here.
    public class CleanRoomDatastore
    {
        public IReadOnlyList<CleanRoom> Rooms => _rooms;
        // テスト用: 再検出回数。dirty 制御の検証に使う。
        // Test-only: rebuild counter used to verify dirty gating.
        public int RebuildCount { get; private set; }

        private List<CleanRoom> _rooms = new();
        private bool _geometryDirty;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly List<IDisposable> _subscriptions = new();

        public CleanRoomDatastore(IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;

            _subscriptions.Add(GameUpdater.UpdateObservable.Subscribe(_ => Update()));

            // 設置/破壊イベント。remove は block.Destroy() より先に発火するため TryGetComponent は安全（検証済み）。
            // Place/remove events. Remove fires before block.Destroy(), so TryGetComponent is safe (verified).
            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(e => OnChanged(e.BlockData)));
            _subscriptions.Add(ServerContext.WorldBlockUpdateEvent.OnBlockRemoveEvent.Subscribe(e => OnChanged(e.BlockData)));

            _geometryDirty = true; // 起動/ロード直後に一度フル検出
        }

        // 全走査で部屋を再構築する。テスト/ロードから明示的にも呼べる。
        // Rebuild all rooms by full scan; callable from tests/load too.
        public void RebuildAll()
        {
            _rooms = CleanRoomDetector.DetectAllRooms(_worldBlockDatastore);
            RebuildCount++;
        }

        public bool TryGetCleanRoomAt(Vector3Int cell, out CleanRoom room)
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

        private void Update()
        {
            if (!_geometryDirty) return;
            _geometryDirty = false;
            // フェーズ1は全走査。dirty分割（8192セル/tick）・差分更新はフェーズ2が実装する。
            // Phase 1 does a full scan; dirty slicing (8192 cells/tick) and diffing land in phase 2.
            RebuildAll();
        }

        private void OnChanged(WorldBlockData blockData)
        {
            // 境界ブロックは常に部屋形状に影響する。
            // Boundary blocks always affect room geometry.
            if (blockData.Block.TryGetComponent<ICleanRoomBoundaryComponent>(out _))
            {
                _geometryDirty = true;
                return;
            }

            // 非境界ブロックも既存部屋の Cells に重なるなら V/S が変わる（V・Cells規則）。
            // Non-boundary blocks overlapping room Cells change V/S (V/Cells rule).
            if (OverlapsAnyRoomCells(blockData.BlockPositionInfo)) _geometryDirty = true;
        }

        private bool OverlapsAnyRoomCells(BlockPositionInfo info)
        {
            for (var x = info.MinPos.x; x <= info.MaxPos.x; x++)
            for (var y = info.MinPos.y; y <= info.MaxPos.y; y++)
            for (var z = info.MinPos.z; z <= info.MaxPos.z; z++)
                if (TryGetCleanRoomAt(new Vector3Int(x, y, z), out _)) return true;
            return false;
        }
    }
}
```

- [ ] **Step 4: DI に登録（initializer 側＋main へのインスタンス橋渡し）**

**⚠ 重要（起動時例外の罠）:** `IWorldBlockDatastore` は `MoorestechServerDIContainerGenerator` の **initializer 側コンテナにのみ登録**されており、main `services` には無い。`services.AddSingleton<CleanRoomDatastore>()`（型登録）にすると**コンパイルは通るが起動時に解決例外で必ず落ちる**。`GearNetworkDatastore` と同じ**インスタンス橋渡し**にする（codemap §1.1 の注意どおり）。

`Server.Boot/MoorestechServerDIContainerGenerator.cs` の initializer 登録群（`initializerCollection.AddSingleton<GearNetworkDatastore>();` の近く）に追加:

```csharp
            initializerCollection.AddSingleton<CleanRoomDatastore>();
```

main `services` のインスタンス橋渡し群（`services.AddSingleton(initializerProvider.GetService<GearNetworkDatastore>());` の近く）に追加:

```csharp
            // initializer 側で生成して橋渡し。この GetService が生成タイミング＝ServerContext 構築後なので、
            // コンストラクタ内の ServerContext.WorldBlockUpdateEvent 購読も安全。生成済みのため別途 eager 不要。
            // Created via the initializer provider after ServerContext exists; no extra eager call needed.
            services.AddSingleton(initializerProvider.GetService<CleanRoomDatastore>());
```

> `using Game.CleanRoom;` を追加し、`Server.Boot.asmdef` の `references` に `"Game.CleanRoom"` を追加する。

- [ ] **Step 5: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Datastore_(PlaceThenBreakBoundary_UpdatesRooms|FarNonBoundaryPlacement_DoesNotRebuild|InteriorBlockPlacement_RebuildsAndUpdatesVolume)"`
Expected: PASS。

- [ ] **Step 6: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs
git commit -m "feat(cleanroom): CleanRoomDatastore（検出骨格・dirty制御・DI橋渡し）を追加"
```

---

## Task 8: 部屋クエリ（座標・ブロック帰属）と全テスト緑化

フェーズ4（専用機械）が使うクエリを足す。`TryGetCleanRoomAt` は単一セル、`TryGetCleanRoom(IBlock)` はブロックの**全占有セルが同一部屋の Cells に含まれる**かを判定する（codemap §1.1/§4 の命名・規則）。境界ブロックのセルは Cells に含まれないため、境界ブロックに対しては常に false（境界用の `GetAdjacentCleanRooms(IBlock)` はフェーズ3/5 が追加）。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs`
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDetectionTest.cs`

- [ ] **Step 1: 失敗テスト（座標クエリ＋ブロック帰属）**

```csharp
        [Test]
        public void Datastore_TryGetCleanRoomAt_ReturnsContainingRoom()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<Game.CleanRoom.CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            GameUpdater.RunFrames(1);

            Assert.True(datastore.TryGetCleanRoomAt(new Vector3Int(2, 2, 2), out _));
            Assert.False(datastore.TryGetCleanRoomAt(new Vector3Int(50, 50, 50), out _));
            Assert.False(datastore.TryGetCleanRoomAt(new Vector3Int(0, 0, 0), out _), "Boundary cells belong to no room");
        }

        [Test]
        public void Datastore_TryGetCleanRoom_InteriorBlockBelongs_BoundaryDoesNot()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var world = ServerContext.WorldBlockDatastore;
            var datastore = serviceProvider.GetService<Game.CleanRoom.CleanRoomDatastore>();

            BuildWallShell(world, new Vector3Int(0, 0, 0), new Vector3Int(4, 4, 4));
            world.TryAddBlock(ForUnitTestModBlockId.GearBeltConveyor, new Vector3Int(2, 2, 2),
                BlockDirection.North, Array.Empty<BlockCreateParam>(), out var inside);
            GameUpdater.RunFrames(1);

            // 占有セルは V から除外されるが Cells には含まれるため、室内ブロックは部屋に帰属する。
            // Occupied cells are excluded from V but kept in Cells, so interior blocks belong to the room.
            Assert.True(datastore.TryGetCleanRoom(inside, out _), "A block fully inside the room is contained");

            // 境界ブロック（壁）は Cells 外＝帰属しない。境界用クエリはフェーズ3/5 の GetAdjacentCleanRooms。
            var wall = world.GetBlock(new Vector3Int(0, 0, 0));
            Assert.False(datastore.TryGetCleanRoom(wall, out _), "Boundary blocks never belong to a room's Cells");
        }
```

> multi-block（2x2x2等）の「全占有セルが同一部屋」「2部屋またがりで false」の検証は、テスト mod に適切なサイズの非境界ブロックが無ければフェーズ4（`CleanRoomMachine` 追加時）で拡充する。その旨をテストコメントに残すこと。

- [ ] **Step 2: 実行して失敗を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Datastore_(TryGetCleanRoomAt_ReturnsContainingRoom|TryGetCleanRoom_InteriorBlockBelongs_BoundaryDoesNot)"`
Expected: FAIL（`TryGetCleanRoom` 未定義。`TryGetCleanRoomAt` は Task 7 で実装済みなので前者は通る場合あり）。

- [ ] **Step 3: TryGetCleanRoom を実装**

`CleanRoomDatastore` に追加:

```csharp
        // ブロックの全占有セルが同一部屋の Cells に含まれるとき true。内部ブロック（機械等）の帰属判定用。
        // 境界ブロックのセルは Cells に含まれないため常に false（境界用はフェーズ3/5 の GetAdjacentCleanRooms）。
        // True iff every occupied cell lies in the SAME room's Cells. Boundary blocks always return false.
        public bool TryGetCleanRoom(IBlock block, out CleanRoom room)
        {
            var info = block.BlockPositionInfo;
            CleanRoom found = null;
            for (var x = info.MinPos.x; x <= info.MaxPos.x; x++)
            for (var y = info.MinPos.y; y <= info.MaxPos.y; y++)
            for (var z = info.MinPos.z; z <= info.MaxPos.z; z++)
            {
                if (!TryGetCleanRoomAt(new Vector3Int(x, y, z), out var r)) { room = null; return false; }
                if (found == null) found = r;
                else if (!ReferenceEquals(found, r)) { room = null; return false; } // 別部屋にまたがる
            }
            room = found;
            return found != null;
        }
```

- [ ] **Step 4: テスト実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Datastore_(TryGetCleanRoomAt_ReturnsContainingRoom|TryGetCleanRoom_InteriorBlockBelongs_BoundaryDoesNot)"`
Expected: PASS。

- [ ] **Step 5: フェーズ1の全テストを実行**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "CleanRoom"`
Expected: 全 PASS。

- [ ] **Step 6: 既存テストの非回帰を確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "(GearBeltConveyor|MachineIO|Fluid)Test"`
Expected: 従来どおり PASS（境界ブロック・DI追加が既存を壊していない）。

- [ ] **Step 7: Commit**

```bash
git add moorestech_server/Assets/Scripts/Game.CleanRoom/CleanRoomDatastore.cs moorestech_server/Assets/Scripts/Tests/CombinedTest/Core/CleanRoomDetectionTest.cs
git commit -m "feat(cleanroom): 座標/ブロック帰属の部屋クエリを追加しフェーズ1完了"
```

---

## フェーズ1 完了の定義（Definition of Done）

- 4種の境界 blockType（`CleanRoomWall`/`CleanRoomDoorHatch`/`CleanRoomItemHatch`/`CleanRoomPipeHatch`）がスキーマ・テンプレート（種別付き `CleanRoomBoundaryKind`）・登録まで通り、テスト mod で設置できる。
- `CleanRoomDetector.DetectAllRooms` が密閉空間を部屋として検出し、**Cells（占有セル含む全内部セル）・Volume（空セル数）・SurfaceArea（空セルの境界接触面数）** を正しく計算する。
- リーク判定は「境界AABB外縁到達」「リーク確定済み領域への接触」「`MaxRoomVolume`（=4096、バランス確定書§5）超過」のいずれかで即不成立。穴あきシェルは部屋にならない。
- **エッジのみ接触の斜め壁は気密**（6近傍の確定挙動）・内部パーティション分割・撤去による結合・**共有壁ハッチによる2部屋隔離**・室内非境界ブロックの V 除外/Cells 帰属、の網羅テストが緑。
- `CleanRoomDatastore` は「境界ブロック変更」＋「既存部屋 Cells に重なる非境界ブロック変更」のみで再検出し、`Rooms`／`TryGetCleanRoomAt`／`TryGetCleanRoom`／`RebuildAll` を公開する。DI は initializer 登録＋main へのインスタンス橋渡し（型登録は起動時例外になるため禁止）。
- 既存テストが非回帰。

## フェーズ1で意図的に先送りした事項（後続プラン・担当確定済み）

- **部屋の同一性と純度状態の永続化** → フェーズ2。再検出前後の部屋を公開済み `Cells` のセル重なりで対応付け、merge は N 合算・split/形状変更は `N_new = Σ C_old·overlap`（縮小=濃度保存/拡張=N保存。バランス確定書§5）。`CleanRoom.Id` は永続キーにしない（本プランで担保済み）。
- 純度（N/C・閾値行＋ヒステリシス・ACH・Valid/Degraded/Invalid＋猶予）→ フェーズ2（`CleanRoomDatastore`/`CleanRoom` に追加。codemap §1.2/§2）
- **dirty area 分割処理（8192セル/tick）・触れた壁AABB+1 局所化・差分更新** → **フェーズ2担当・フェーズ2 DoD に含む**（バランス確定書§5。フェーズ1は dirtyフラグ＋次tick全走査の暫定。本文「仕様§2との関係」参照）
- エアフィルター（`CleanRoomAirFilter`・電力・フィルター仕事量）・汚染源（境界種別 `CleanRoomBoundaryKind` 別集計）→ フェーズ3
- 専用機械 `CleanRoomMachine` の binning・`ICleanRoomStateReceiver` プッシュ・Valid/Degraded/Invalid 停止（`TryGetCleanRoom` を使用）→ フェーズ4
- ハッチ3種の I/O 挙動・`A_hatch`/`A_door` 計量・**境界ブロックの帰属クエリ `GetAdjacentCleanRooms(IBlock)`**（共有壁ハッチの「面する各部屋に計上」規則＝バランス確定書§2 はここで使う）・必要な部屋状態のセーブ/ロード → フェーズ5
- 本番 mod（moorestech_master）の blocks.json 配線・モデル/画像アセット → 各フェーズで playable 化する際に対応
