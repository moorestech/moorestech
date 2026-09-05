# 上下の坂ベルトコンベアの単体設置 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 上り・下り坂ベルトコンベアをビルドメニュー・ホットバーの個別エントリとして選べるようにし、1マス単体でも、ドラッグで一定勾配の坂列としても設置できるようにする。

**Architecture:** サーバー側 `PlacementTargetCatalog` の坂除外を撤廃し、解放判定だけをファミリー直線へ委譲する共通utilへ寄せる。クライアント側 `BeltConveyorPlaceSystem` は「選択が直線か坂か」で経路計算を分岐し、坂選択時は新設の純粋計算 `BeltConveyorSlopePathBuilder`（XZ経路＋一定勾配Y）を使う。立体交差の自動持ち上げ・自動坂割り当ては直線選択時の既存経路にだけ残る。

**Tech Stack:** Unity 2022 / C# / NUnit（EditModeテスト）/ uloop CLI

## Requirements

設計対話（2026-09-05）で確定した要件。正本は `docs/adr/0050-slope-belt-standalone-placement.md`。

1. 上り・下りベルトがビルドメニュー・ホットバー割当の対象として列挙される。受け入れ基準: `PlacementTargetCatalog.CreateEntries` の Block 群に坂の BlockGuid が含まれる。
2. 坂エントリの並び順は既存の `sortPriority` → `name` 規則に従う（直線→上り→下りになる）。受け入れ基準: カタログの Block 並び順が `MasterHolder.BlockMaster.Blocks.Data` を除外なしで `SortPriority ?? 0` → `Name` で整列した順と一致する。
3. 坂の解放判定はファミリー直線ブロックの解放状態に従う。受け入れ基準: 直線だけを解放した状態で、坂が `UnlockedEntries` に現れる。直線が未解放なら坂も現れない。
4. 建設コストの財布は直線代表のまま（`ConstructionWalletUtil.ResolveWalletBlockId`）で、research.json・コストマスタは変更しない。受け入れ基準: マスタ（`../moorestech_master`）に一切の差分が出ない。
5. 坂を選んで1クリックすると、その1マスに選択した坂ブロックが置かれる。受け入れ基準: 単セル経路の `PlaceInfo.BlockId` が選択した坂の BlockId になる。
6. 坂を選んでドラッグすると経路の全セルが選択中の坂になる（中途に直線を混ぜない）。受け入れ基準: 3セル経路で3セルとも坂の BlockId。
7. ドラッグ中の高さは選択ブロックだけで決まる。起点から毎セル +1（上り）／ -1（下り）で進み、カーソル先の地形高さは参照しない。受け入れ基準: 起点 `y=0`・終点 `y` が何であっても、i番目セルの `Position.y == 0 + step * i`。
8. L字ドラッグの角のセルも坂にする（`VerticalDirection` は全セル Up または Down）。受け入れ基準: 曲がる経路でも角セルの `VerticalDirection` が選択した坂の向きのまま。
9. 坂選択中は立体交差の自動持ち上げ（`ConveyorOverpassRaiser`）を通さない。受け入れ基準: 障害物があっても高さプロファイルは一定勾配のまま変わらず、障害物セルは `ExistingBlock` 原因で設置不可になる。
10. 地面と重なるセルは従来どおり設置不可（共通の地面重なり判定 `PlacementCellReasonReporter.ApplyGroundOverlapsAndReport` をそのまま通す）。受け入れ基準: 坂経路も他の設置系と同じ地面重なり経路を通る（分岐で迂回しない）。
11. スポイトで坂を拾うと坂そのものが手持ちになる。受け入れ基準: 坂ブロックをピックした結果の `BlockPlacementTarget.BlockGuid` が坂の Guid。未解放（直線が未解放）ならピックできない。
12. 直線を選んでいるときの既存の自動坂挿入・立体交差は挙動を変えない。受け入れ基準: `ConveyorOverpassConveyanceTest` と `BeltConveyorCellBlockResolverTest` が無変更で通る。

**やらないこと（スコープ境界）:**

- research.json・blocks.json・コストマスタなどマスタデータの変更
- 坂ブロック自体の搬送ロジック・プレハブ・当たり判定の変更
- 直線選択時の経路計算（`BeltConveyorPathBuilder` / `ConveyorOverpassRaiser` / `BeltConveyorCellBlockResolver`）の挙動変更
- 分岐器（坂を持たないファミリー）の扱いの変更
- 角で搬送の接続が繋がるかの保証（ADR 0050 で「破綻したら裁定を更新する」と明記済み。今回は繋がらなくても実装を止めない）

## Global Constraints

`AGENTS.md` の全規約が全タスクに適用される。特に本planで踏みやすいもの:

- `partial` は如何なる条件でも禁止。`Func<>` の使用禁止（新規に述語を引数で渡さない）。
- try-catch 禁止（外部境界を除く）。null チェックは外部データと非同期ロード結果のみ。
- 1ファイル200行以下。1ディレクトリ10ファイルまで。
- コメントは主要な処理セクションに「// 日本語 → // English」の2行セット、3〜10行ごと。各言語1行に収める。自明なコメントは書かない。
- メソッド内のローカル関数は `#region Internal` にまとめる。クラス直下のprivateメソッドを `#region Internal` で囲うのは禁止。
- デフォルト引数は禁止。引数を増やすときは呼び出し側を全て直す。
- イベント発火に `Action` を使わない（本planでは新規イベントを作らないため該当なし）。
- `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client` を実行する。
- `.meta` ファイルは手動作成しない。新規 `.cs` を追加した後、Unity が生成した `.meta` はコミットしてよい。
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"` で対象を絞って実行する。
- 作業前に `pwd` で現在ディレクトリを確認する。タスク終了前に必ずコミットする。

## 配置と前例（spec-architecture-review 結果）

| 項目 | 配置先 | 前例 |
|---|---|---|
| 解放判定用Guidの正規化 `ResolveUnlockBlockGuid` | `Game.Block.Interface/Extension/BeltConveyorPlaceFamilyUtil.cs`（server, ドメインutil） | 同ファイルの `IsSlopeBlock`。現在 `PlaceBlockProtocol.IsUnlocked` と `PlacementTargetCatalog` の2箇所に同じ規則が散るため、utilへ集約する（「同種の条件分岐は一箇所へ揃える」） |
| 坂の向き解決 `TryGetSlopeDirection` | `Game.Block.Interface/Extension/BeltConveyorFamily.cs` | 同クラスの `IsSlopeBlock`（ファミリー内のメンバー判定はこのクラスの責務） |
| 坂経路の純粋計算 `BeltConveyorSlopePathBuilder` | `Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Path/` | 同ディレクトリの `BeltConveyorPathBuilder` / `BeltConveyorPositionListBuilder`（座標列組み立ては Path/ の責務） |
| 占有判定込みの坂設置点計算 `CalculateSlopePoint` | `Client.Game/.../BeltConveyor/Parts/BeltConveyorPlacePointCalculator.cs` | 同クラスの `CalculatePoint`（`BlockGameObjectDataStore` を持つ唯一の設置点計算クラス） |

新規パターン（レビュー注目点）: 坂選択時に `ConveyorOverpassRaiser` を通さない第2経路を `BeltConveyorPlaceSystem` 内に持つこと。既存の直線経路は無傷のまま残し、坂経路は同じ後段（地面重なり判定・建設コスト・プレビュー色）へ合流させる受動的な追加とする。

**データフロー地図:**

```
ビルドメニュー/ホットバー選択 → BlockPlacementTarget → PlaceSystemSelector → BeltConveyorPlaceSystem
  → [直線] BeltConveyorPathBuilder → ConveyorOverpassRaiser → BeltConveyorCellBlockResolver ┐
  → [坂]   BeltConveyorSlopePathBuilder ──────────────────────────────────────────────────┴→ List<PlaceInfo>
  → 地面重なり判定 → 建設コスト判定 → プレビュー色 → PlaceBlockProtocolSender
```

坂経路は「`List<PlaceInfo>` を作る書き手」が1人増えるだけで、下流の駅は一切増やさない。

---

### Task 1: 坂をカタログへ載せ、解放判定を直線へ委譲する

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorPlaceFamilyUtil.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.PlacementTarget/PlacementTargetCatalog.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs:126-134`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementTargetCatalogTest.cs:52-69`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementTargetCatalogUnlockTest.cs`

**Interfaces:**
- Produces: `BeltConveyorPlaceFamilyUtil.ResolveUnlockBlockGuid(Guid blockGuid) -> Guid`（坂ならファミリー直線のGuid、それ以外は引数そのまま）
- Consumes: 既存の `BeltConveyorPlaceFamilyUtil.TryGetFamilyByGuid(Guid, out BeltConveyorFamily)`

- [x] **Step 1: 失敗するテストを書く（カタログ列挙と解放委譲）**

`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementTargetCatalogUnlockTest.cs` の末尾（`ブループリント未解放なら…` テストの後、`}` の前）に追加する。ファイル先頭の using に `using Core.Master;`・`using Microsoft.Extensions.DependencyInjection;`・`using Tests.CombinedTest.Server.PacketTest;` を足す。

このテストだけ SetUp の戻り値（ServiceProvider）が要るので、テスト内で自前にサーバーを起動する（`PlaceBlockProtocolTestSupport.LockBlock` が ServiceProvider を要求し、直線の初期解放状態に依存せず「未解放から始まる」ことを保証するため）。

```csharp
        [Test]
        public void 坂ベルトはカタログに載り直線の解放状態に従う()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var catalog = new PlacementTargetCatalog();
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            // 直線の初期解放状態に依存しないよう、明示的にロックしてから始める
            // Start from an explicitly locked state so the initial unlock flag cannot affect the result
            PlaceBlockProtocolTestSupport.LockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);

            var straightGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.GearBeltConveyor).BlockGuid;
            var upGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearBeltConveyorUp).BlockGuid;
            var downGuid = MasterHolder.BlockMaster.GetBlockMaster(ForUnitTestModBlockId.TestGearBeltConveyorDown).BlockGuid;

            // 坂もマスタ由来エントリとして列挙される
            // Slopes are enumerated as master-derived entries
            var blockIds = catalog.CreateEntries(NoBlueprints)
                .Where(entry => entry.Kind == PlacementTargetKind.Block)
                .Select(entry => entry.Id)
                .ToHashSet();
            Assert.IsTrue(blockIds.Contains(upGuid));
            Assert.IsTrue(blockIds.Contains(downGuid));

            // 直線が未解放なら坂も出ない
            // Slopes stay hidden while the straight block is locked
            var lockedIds = catalog.UnlockedEntries(unlockState, false, NoBlueprints).Select(entry => entry.Id).ToHashSet();
            Assert.IsFalse(lockedIds.Contains(straightGuid));
            Assert.IsFalse(lockedIds.Contains(upGuid));
            Assert.IsFalse(lockedIds.Contains(downGuid));

            // 直線を解放すると坂も同時に現れる
            // Unlocking the straight block reveals the slopes together
            unlockState.UnlockBlock(straightGuid);
            var unlockedIds = catalog.UnlockedEntries(unlockState, false, NoBlueprints).Select(entry => entry.Id).ToHashSet();
            Assert.IsTrue(unlockedIds.Contains(straightGuid));
            Assert.IsTrue(unlockedIds.Contains(upGuid));
            Assert.IsTrue(unlockedIds.Contains(downGuid));
        }
```

- [x] **Step 2: 並び順テストから坂除外を外す**

`PlacementTargetCatalogTest.cs:57-62` の期待値式から `Where` 行を削除する。

```csharp
            var expected = MasterHolder.BlockMaster.Blocks.Data
                .OrderBy(block => block.SortPriority ?? 0)
                .ThenBy(block => block.Name)
                .Select(block => block.BlockGuid)
                .ToList();
```

同ファイル先頭の `using Game.Block.Interface.Extension;` は他で使っていなければ削除する。

- [x] **Step 3: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementTargetCatalog"`
Expected: FAIL（`坂ベルトはカタログに載り直線の解放状態に従う` が `upGuid` 不在で落ち、`Blockの並び順が…` が期待値と実際の件数差で落ちる）

- [x] **Step 4: 解放Guid正規化をutilへ追加する**

`BeltConveyorPlaceFamilyUtil.cs` の `IsSlopeBlock` の直後へ追加する。

```csharp
        // 解放判定に使うGuid。坂ベルトはファミリー直線へ寄せる
        // The guid used for unlock checks; belt slopes normalize to their family's straight block
        public static Guid ResolveUnlockBlockGuid(Guid blockGuid)
        {
            if (!TryGetFamilyByGuid(blockGuid, out var family)) return blockGuid;
            return MasterHolder.BlockMaster.GetBlockMaster(family.StraightBlockId).BlockGuid;
        }
```

- [x] **Step 5: カタログの坂除外を撤廃し解放判定を委譲する**

`PlacementTargetCatalog.cs` の `CreateMasterEntries` 内、ブロック整列を次に置き換える。

```csharp
                // 表示優先度と名前で整列（坂ベルトも単体設置対象として載せる）
                // Sort by display priority and name; belt slopes are placeable targets too
                var blocks = MasterHolder.BlockMaster.Blocks.Data
                    .OrderBy(block => block.SortPriority ?? 0)
                    .ThenBy(block => block.Name);
```

`IsEntryUnlocked` の `PlacementTargetKind.Block` ケースを次に置き換える。

```csharp
                case PlacementTargetKind.Block:
                    // 坂ベルトは直線ブロックの解放状態に従う
                    // Belt slopes follow their family straight block's unlock state
                    var unlockGuid = BeltConveyorPlaceFamilyUtil.ResolveUnlockBlockGuid(entry.Id);
                    return showAllPlaceable || (unlockState.BlockUnlockStateInfos.TryGetValue(unlockGuid, out var blockInfo) && blockInfo.IsUnlocked);
```

- [x] **Step 6: PlaceBlockProtocol を同じutilへ寄せる**

`PlaceBlockProtocol.cs` の `IsUnlocked` を置き換える（引数 `blockId` が未使用になるため削除し、呼び出し側 `if (!IsUnlocked(placeBlockId, blockMaster.BlockGuid))` を `if (!IsUnlocked(blockMaster.BlockGuid))` に直す）。

```csharp
            bool IsUnlocked(Guid blockGuid)
            {
                // ベルトファミリーは直線ブロックのunlock状態を参照する
                // Belt families resolve unlock state through their straight block
                var unlockGuid = BeltConveyorPlaceFamilyUtil.ResolveUnlockBlockGuid(blockGuid);
                return _gameUnlockStateDataController.BlockUnlockStateInfos[unlockGuid].IsUnlocked;
            }
```

- [x] **Step 7: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

- [x] **Step 8: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlacementTargetCatalog|HotbarAssignmentDatastore|HotbarProtocol|HotbarSaveLoad|PlaceBlockProtocol"`
Expected: PASS（ホットバー系テストは坂以外のブロックを選ぶ実装のため無変更で通る）

- [x] **Step 9: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorPlaceFamilyUtil.cs \
        moorestech_server/Assets/Scripts/Game.PlacementTarget/PlacementTargetCatalog.cs \
        moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/PlaceBlockProtocol.cs \
        moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementTargetCatalogTest.cs \
        moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/PlacementTargetCatalogUnlockTest.cs
git commit -m "feat: 坂ベルトを設置対象カタログへ載せ解放判定を直線へ委譲する"
```

---

### Task 2: スポイトが坂を坂のまま手持ちにする

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlacementPick/BlockPickResolver.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlacementPick/PlacementTargetPickService.cs:70-76`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlockPickResolverTest.cs`

**Interfaces:**
- Produces: `BlockPickResolver.IsPickable(BlockId blockId, IGameUnlockStateData unlockState) -> bool`（`TryResolvePickTarget` を置き換える。正規化をやめたので out 引数は無くなる）
- Consumes: Task 1 の `BeltConveyorPlaceFamilyUtil.ResolveUnlockBlockGuid`

- [ ] **Step 1: 失敗するテストを書く**

`BlockPickResolverTest.cs` の3テストを次で全置換する（クラス本体のみ差し替え、`CreateServer` はそのまま残す）。

```csharp
        [Test]
        public void 解放済み通常ブロックはピックできる()
        {
            var serviceProvider = CreateServer();
            PlaceBlockProtocolTestSupport.UnlockBlock(serviceProvider, ForUnitTestModBlockId.MachineId);
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            Assert.IsTrue(BlockPickResolver.IsPickable(ForUnitTestModBlockId.MachineId, unlockState));
        }

        [Test]
        public void ベルト坂ブロックは直線の解放状態でピックできる()
        {
            var serviceProvider = CreateServer();
            PlaceBlockProtocolTestSupport.UnlockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            // 坂は直線の解放状態を借りてピックできる（手持ちは坂そのもの）
            // A slope is pickable through its straight block's unlock state, and stays a slope
            Assert.IsTrue(BlockPickResolver.IsPickable(ForUnitTestModBlockId.TestGearBeltConveyorUp, unlockState));
        }

        [Test]
        public void 直線が未解放なら坂もピックできない()
        {
            var serviceProvider = CreateServer();
            PlaceBlockProtocolTestSupport.LockBlock(serviceProvider, ForUnitTestModBlockId.GearBeltConveyor);
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            Assert.IsFalse(BlockPickResolver.IsPickable(ForUnitTestModBlockId.TestGearBeltConveyorUp, unlockState));
        }

        [Test]
        public void 未解放ブロックはピックできない()
        {
            var serviceProvider = CreateServer();
            PlaceBlockProtocolTestSupport.LockBlock(serviceProvider, ForUnitTestModBlockId.MachineId);
            var unlockState = serviceProvider.GetService<IGameUnlockStateDataController>();

            Assert.IsFalse(BlockPickResolver.IsPickable(ForUnitTestModBlockId.MachineId, unlockState));
        }
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlockPickResolver"`
Expected: FAIL（コンパイルエラー: `IsPickable` が存在しない）

- [ ] **Step 3: BlockPickResolver を書き換える**

`BlockPickResolver.cs` のクラス本体を次に置き換える。

```csharp
    public static class BlockPickResolver
    {
        // 拾ったブロックはそのまま手持ちにする（坂ベルトも坂のまま）
        // The picked block is held as-is, slopes included
        public static bool IsPickable(BlockId blockId, IGameUnlockStateData unlockState)
        {
            // 未解放ブロックはピック不可（スポイトで解放システムを迂回させない）
            // Locked blocks are not pickable; the eyedropper must not bypass the unlock system
            var blockGuid = MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockGuid;
            var unlockGuid = BeltConveyorPlaceFamilyUtil.ResolveUnlockBlockGuid(blockGuid);
            return unlockState.BlockUnlockStateInfos.TryGetValue(unlockGuid, out var info) && info.IsUnlocked;
        }
    }
```

- [ ] **Step 4: 呼び出し側を直す**

`PlacementTargetPickService.cs` の `TryPickBlock` を次に置き換える。

```csharp
            bool TryPickBlock(out IPlacementTarget target)
            {
                target = null;
                if (!BlockClickDetectUtil.TryGetCursorOnBlock(out var blockObject)) return false;
                if (!BlockPickResolver.IsPickable(blockObject.BlockId, _gameUnlockStateData)) return false;

                target = new BlockPlacementTarget(MasterHolder.BlockMaster.GetBlockMaster(blockObject.BlockId).BlockGuid, blockObject.BlockPosInfo.BlockDirection);
                return true;
            }
```

- [ ] **Step 5: コンパイルしてテストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlockPickResolver"`
Expected: PASS（4テスト）

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlacementPick/BlockPickResolver.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlacementPick/PlacementTargetPickService.cs \
        moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BlockPickResolverTest.cs
git commit -m "feat: スポイトで拾った坂ベルトを坂のまま手持ちにする"
```

---

### Task 3: ファミリーから坂の向きを引けるようにする

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorFamily.cs`
- Test: `moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltConveyorFamilyTest.cs`

**Interfaces:**
- Produces: `BeltConveyorFamily.TryGetSlopeDirection(BlockId blockId, out BlockVerticalDirection verticalDirection) -> bool`（上りなら `Up`、下りなら `Down` を返して true。直線・分岐器は `Horizontal` を返して false）
- 既存の `IsSlopeBlock(BlockId)` はこのメソッドへ委譲する（規則の二重定義を避ける）

- [ ] **Step 1: 失敗するテストを書く**

`BeltConveyorFamilyTest.cs` の `坂ブロックだけを非直線メンバーとして判定する` テストの直後に追加する。ファイル先頭の using に `using Game.Block.Interface;` が無ければ足す。

```csharp
        [Test]
        public void 坂ブロックから上下の向きを引ける()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.GearBeltConveyor, out var family);

            Assert.IsTrue(family.TryGetSlopeDirection(ForUnitTestModBlockId.TestGearBeltConveyorUp, out var up));
            Assert.AreEqual(BlockVerticalDirection.Up, up);
            Assert.IsTrue(family.TryGetSlopeDirection(ForUnitTestModBlockId.TestGearBeltConveyorDown, out var down));
            Assert.AreEqual(BlockVerticalDirection.Down, down);

            // 直線は坂でないのでfalseとHorizontalを返す
            // The straight block is not a slope, so it returns false with Horizontal
            Assert.IsFalse(family.TryGetSlopeDirection(ForUnitTestModBlockId.GearBeltConveyor, out var straight));
            Assert.AreEqual(BlockVerticalDirection.Horizontal, straight);
        }
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltConveyorFamilyTest"`
Expected: FAIL（コンパイルエラー: `TryGetSlopeDirection` が存在しない）

- [ ] **Step 3: BeltConveyorFamily へ実装する**

`BeltConveyorFamily.cs` の `IsSlopeBlock` を次の2メソッドに置き換える。

```csharp
        public bool IsSlopeBlock(BlockId blockId)
        {
            return TryGetSlopeDirection(blockId, out _);
        }

        // 坂ブロックなら上下どちらの坂かを返す
        // Returns which way the slope goes when the block is a slope
        public bool TryGetSlopeDirection(BlockId blockId, out BlockVerticalDirection verticalDirection)
        {
            if (UpBlockId.HasValue && blockId == UpBlockId.Value)
            {
                verticalDirection = BlockVerticalDirection.Up;
                return true;
            }

            if (DownBlockId.HasValue && blockId == DownBlockId.Value)
            {
                verticalDirection = BlockVerticalDirection.Down;
                return true;
            }

            verticalDirection = BlockVerticalDirection.Horizontal;
            return false;
        }
```

- [ ] **Step 4: コンパイルしてテストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltConveyorFamilyTest"`
Expected: PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Block.Interface/Extension/BeltConveyorFamily.cs \
        moorestech_server/Assets/Scripts/Tests/UnitTest/Game/BeltConveyorFamilyTest.cs
git commit -m "feat: ベルトファミリーから坂の上下向きを引けるようにする"
```

---

### Task 4: 坂経路の純粋計算を新設する

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Path/BeltConveyorSlopePathBuilder.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorSlopePathBuilderTest.cs`

**Interfaces:**
- Produces: `BeltConveyorSlopePathBuilder.Build(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection, BlockVerticalDirection slopeDirection) -> List<PlaceInfo>`
  - `Position.y` は `startPoint.y + step * index`（Up なら step=+1、Down なら step=-1）。`endPoint.y` は無視する。
  - `Direction` は次セルへの進行方向（末尾セルは前セルからの方向）。単セルのときだけ引数の `blockDirection`。
  - `VerticalDirection` は全セル `slopeDirection`。`Placeable` は全セル true（占有・地面判定は後段）。`BlockId` は未設定（後段で埋める）。
- Consumes: 既存の `BeltConveyorPositionListBuilder.Build(Vector3Int, Vector3Int, bool) -> (List<Vector3Int>, int)`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorSlopePathBuilderTest.cs` を新規作成する。

```csharp
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Path;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    public class BeltConveyorSlopePathBuilderTest
    {
        // 単セルはキー回転の向きをそのまま使う
        // A single cell keeps the key-rotated direction as-is
        [Test]
        public void 単セルは選択した坂と回転方向で1個だけ返る()
        {
            var placeInfos = BeltConveyorSlopePathBuilder.Build(
                new Vector3Int(3, 5, 7), new Vector3Int(3, 5, 7), true, BlockDirection.West, BlockVerticalDirection.Up);

            Assert.AreEqual(1, placeInfos.Count);
            Assert.AreEqual(new Vector3Int(3, 5, 7), placeInfos[0].Position);
            Assert.AreEqual(BlockDirection.West, placeInfos[0].Direction);
            Assert.AreEqual(BlockVerticalDirection.Up, placeInfos[0].VerticalDirection);
            Assert.IsTrue(placeInfos[0].Placeable);
        }

        // 上りは終点の高さを無視して毎セル+1で伸びる
        // Up ignores the end height and climbs one per cell
        [Test]
        public void 上りは終点の高さを無視して毎セル1段上がる()
        {
            var placeInfos = BeltConveyorSlopePathBuilder.Build(
                new Vector3Int(0, 0, 0), new Vector3Int(3, -10, 0), false, BlockDirection.North, BlockVerticalDirection.Up);

            CollectionAssert.AreEqual(
                new[] { new Vector3Int(0, 0, 0), new Vector3Int(1, 1, 0), new Vector3Int(2, 2, 0), new Vector3Int(3, 3, 0) },
                placeInfos.Select(info => info.Position).ToList());
            Assert.IsTrue(placeInfos.All(info => info.VerticalDirection == BlockVerticalDirection.Up));
            Assert.IsTrue(placeInfos.All(info => info.Direction == BlockDirection.East));
        }

        // 下りは毎セル-1で潜る
        // Down descends one per cell
        [Test]
        public void 下りは毎セル1段下がる()
        {
            var placeInfos = BeltConveyorSlopePathBuilder.Build(
                new Vector3Int(0, 0, 0), new Vector3Int(0, 0, 2), true, BlockDirection.North, BlockVerticalDirection.Down);

            CollectionAssert.AreEqual(
                new[] { new Vector3Int(0, 0, 0), new Vector3Int(0, -1, 1), new Vector3Int(0, -2, 2) },
                placeInfos.Select(info => info.Position).ToList());
            Assert.IsTrue(placeInfos.All(info => info.VerticalDirection == BlockVerticalDirection.Down));
        }

        // L字でも角のセルが坂のまま一定勾配で続く
        // An L-shaped run keeps the corner cell sloped at the same constant grade
        [Test]
        public void L字の角も坂のまま一定勾配で続く()
        {
            var placeInfos = BeltConveyorSlopePathBuilder.Build(
                new Vector3Int(0, 0, 0), new Vector3Int(2, 0, 2), true, BlockDirection.North, BlockVerticalDirection.Up);

            // Z方向へ2マス進んでからX方向へ2マス曲がる経路（角は index 2）
            // The path runs two cells along Z, then turns two cells along X (corner at index 2)
            CollectionAssert.AreEqual(
                new[]
                {
                    new Vector3Int(0, 0, 0), new Vector3Int(0, 1, 1), new Vector3Int(0, 2, 2),
                    new Vector3Int(1, 3, 2), new Vector3Int(2, 4, 2),
                },
                placeInfos.Select(info => info.Position).ToList());
            Assert.IsTrue(placeInfos.All(info => info.VerticalDirection == BlockVerticalDirection.Up));

            // 角のセルは次セルへ向く（East）、末尾は前セルからの方向を引き継ぐ
            // The corner cell faces the next cell (East) and the tail inherits the previous direction
            Assert.AreEqual(BlockDirection.North, placeInfos[1].Direction);
            Assert.AreEqual(BlockDirection.East, placeInfos[2].Direction);
            Assert.AreEqual(BlockDirection.East, placeInfos[4].Direction);
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltConveyorSlopePathBuilder"`
Expected: FAIL（コンパイルエラー: `BeltConveyorSlopePathBuilder` が存在しない）

- [ ] **Step 3: BeltConveyorSlopePathBuilder を実装する**

`moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Path/BeltConveyorSlopePathBuilder.cs` を新規作成する。

```csharp
using System.Collections.Generic;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Path
{
    /// <summary>
    /// 坂ブロック選択時の経路計算（全セル坂・一定勾配・地形非依存）
    /// Path calculation while a slope block is selected (all cells sloped, constant grade, terrain-independent)
    /// </summary>
    public static class BeltConveyorSlopePathBuilder
    {
        public static List<PlaceInfo> Build(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection, BlockVerticalDirection slopeDirection)
        {
            // XZ経路だけを既存ビルダーで組み、Yは終点の高さを見ずに一定勾配で決める
            // Build only the XZ path with the existing builder; Y follows a constant grade ignoring the end height
            var flatEndPoint = new Vector3Int(endPoint.x, startPoint.y, endPoint.z);
            var (positions, _) = BeltConveyorPositionListBuilder.Build(startPoint, flatEndPoint, isStartDirectionZ);

            var yStep = slopeDirection == BlockVerticalDirection.Up ? 1 : -1;
            var placeInfos = new List<PlaceInfo>(positions.Count);
            for (var i = 0; i < positions.Count; i++)
            {
                var position = positions[i];
                position.y = startPoint.y + yStep * i;
                placeInfos.Add(new PlaceInfo
                {
                    Position = position,
                    Direction = ResolveDirection(i),
                    VerticalDirection = slopeDirection,
                    Placeable = true,
                });
            }

            return placeInfos;

            #region Internal

            // 進行方向は次セルへの差分。末尾セルだけ前セルからの差分を引き継ぐ
            // The facing comes from the delta to the next cell; the tail inherits the delta from the previous cell
            BlockDirection ResolveDirection(int index)
            {
                if (positions.Count == 1) return blockDirection;

                var isTail = index == positions.Count - 1;
                var from = isTail ? positions[index - 1] : positions[index];
                var to = isTail ? positions[index] : positions[index + 1];

                if (from.x == to.x) return to.z > from.z ? BlockDirection.North : BlockDirection.South;
                return to.x > from.x ? BlockDirection.East : BlockDirection.West;
            }

            #endregion
        }
    }
}
```

- [ ] **Step 4: コンパイルしてテストを実行する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltConveyorSlopePathBuilder"`
Expected: PASS（4テスト）

- [ ] **Step 5: コミットする**

`.meta` はUnityが生成したものをそのまま含める（手動作成しない）。

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Path/BeltConveyorSlopePathBuilder.cs* \
        moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorSlopePathBuilderTest.cs*
git commit -m "feat: 坂選択時の一定勾配経路ビルダーを追加する"
```

---

### Task 5: 坂選択時の設置点計算を設置システムへ繋ぐ

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorPlacePointCalculator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorSlopePlacementTest.cs`（新規）

**Interfaces:**
- Produces: `BeltConveyorPlacePointCalculator.CalculateSlopePoint(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection, BlockMasterElement holdingBlockMaster, BlockVerticalDirection slopeDirection, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons) -> List<PlaceInfo>`（全セルの `BlockId` は `holdingBlockMaster` のBlockId。既存ブロックと重なるセルは `Placeable=false` かつ `blockCauses[i] = PlacementBlockCause.ExistingBlock`。`beltReasons` は全セル `None`）
- Consumes: Task 3 の `BeltConveyorFamily.TryGetSlopeDirection`、Task 4 の `BeltConveyorSlopePathBuilder.Build`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorSlopePlacementTest.cs` を新規作成する。設置点計算はUnityの `BlockGameObjectDataStore` を要求するため、ここでは「ファミリーからの向き解決＋経路ビルダー＋BlockId割当」というシステムの決定規則だけを検証する。

```csharp
using System.Linq;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Path;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltConveyor
{
    public class BeltConveyorSlopePlacementTest
    {
        [SetUp]
        public void SetUp()
        {
            new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        // 上りを選ぶと全セルが上りブロックになり中途に直線が混ざらない
        // Selecting the up slope fills every cell with the up block and never mixes in a straight block
        [Test]
        public void 坂を選ぶと経路の全セルがその坂ブロックになる()
        {
            var holdingBlockId = ForUnitTestModBlockId.TestGearBeltConveyorUp;
            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(holdingBlockId, out var family));
            Assert.IsTrue(family.TryGetSlopeDirection(holdingBlockId, out var slopeDirection));

            var placeInfos = BeltConveyorSlopePathBuilder.Build(
                new Vector3Int(0, 0, 0), new Vector3Int(2, 0, 0), false, BlockDirection.East, slopeDirection);
            foreach (var placeInfo in placeInfos) placeInfo.BlockId = holdingBlockId;

            Assert.AreEqual(3, placeInfos.Count);
            Assert.IsTrue(placeInfos.All(info => info.BlockId == holdingBlockId));
            Assert.IsFalse(placeInfos.Any(info => info.BlockId == family.StraightBlockId));
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, placeInfos.Select(info => info.Position.y).ToList());
        }

        // 下りを選ぶと同じ経路が毎セル1段下がる
        // Selecting the down slope makes the same path descend one per cell
        [Test]
        public void 下りを選ぶと経路が毎セル下がる()
        {
            var holdingBlockId = ForUnitTestModBlockId.TestGearBeltConveyorDown;
            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(holdingBlockId, out var family));
            Assert.IsTrue(family.TryGetSlopeDirection(holdingBlockId, out var slopeDirection));

            var placeInfos = BeltConveyorSlopePathBuilder.Build(
                new Vector3Int(0, 0, 0), new Vector3Int(2, 0, 0), false, BlockDirection.East, slopeDirection);

            CollectionAssert.AreEqual(new[] { 0, -1, -2 }, placeInfos.Select(info => info.Position.y).ToList());
        }

        // 直線を選んだときは坂の向きが引けず既存の自動判定経路に落ちる
        // Selecting the straight block yields no slope direction, so the existing auto path is used
        [Test]
        public void 直線を選んだときは坂の向きが引けない()
        {
            Assert.IsTrue(BeltConveyorPlaceFamilyUtil.TryGetFamily(ForUnitTestModBlockId.GearBeltConveyor, out var family));
            Assert.IsFalse(family.TryGetSlopeDirection(ForUnitTestModBlockId.GearBeltConveyor, out _));
        }
    }
}
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltConveyorSlopePlacement"`
Expected: PASS（Task 3・4 の成果だけで通る。ここは後続の実装が壊さないための回帰網）

このテストが FAIL する場合は Task 3・4 の実装が不完全なので、先にそちらへ戻る。

- [ ] **Step 3: CalculateSlopePoint を実装する**

`BeltConveyorPlacePointCalculator.cs` の `CalculatePoint`（static版）の直後、`IsNotExistBlock` の前に追加する。ファイル先頭の using に `using Core.Master;` を足す。

```csharp
        // 坂選択時の設置点計算（一定勾配・立体交差なし・全セル同一ブロック）
        // Placement-point calculation while a slope is selected (constant grade, no overpass, one block for every cell)
        public List<PlaceInfo> CalculateSlopePoint(Vector3Int startPoint, Vector3Int endPoint, bool isStartDirectionZ, BlockDirection blockDirection, BlockMasterElement holdingBlockMaster, BlockVerticalDirection slopeDirection, out List<PlacementBlockCause> blockCauses, out List<BeltConveyorPlacementBlockReason> beltReasons)
        {
            var placeInfos = BeltConveyorSlopePathBuilder.Build(startPoint, endPoint, isStartDirectionZ, blockDirection, slopeDirection);
            var holdingBlockId = MasterHolder.BlockMaster.GetBlockId(holdingBlockMaster.BlockGuid);

            blockCauses = new List<PlacementBlockCause>(placeInfos.Count);
            beltReasons = new List<BeltConveyorPlacementBlockReason>(placeInfos.Count);

            // 坂は立体交差も坂欠落も起こらないためベルト固有理由は立たない
            // A slope run raises neither an overpass nor a missing-slope reason, so the belt column stays None
            for (var i = 0; i < placeInfos.Count; i++)
            {
                var info = placeInfos[i];
                info.BlockId = holdingBlockId;
                blockCauses.Add(PlacementBlockCause.None);
                beltReasons.Add(BeltConveyorPlacementBlockReason.None);

                if (IsNotExistBlock(info, holdingBlockMaster)) continue;

                info.Placeable = false;
                blockCauses[i] = PlacementBlockCause.ExistingBlock;
            }

            return placeInfos;
        }
```

ファイル先頭の using に `using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Path;` が無ければ足す。

- [ ] **Step 4: BeltConveyorPlaceSystem を分岐させる**

`BeltConveyorPlaceSystem.cs` の `GroundClickControl` を次のとおり書き換える。

(1) ファミリー解決の直後を置き換える。

```csharp
            // ファミリー定義を解決し、非ファミリーブロックは対象外にする
            // Resolve the family definition and ignore non-family blocks
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(target.BlockId, out var family)) return;

            // 坂を選んでいるならその坂を手持ちにし、直線選択時は従来どおり直線を手持ちにする
            // Hold the selected slope when one is selected; otherwise hold the straight block as before
            var isSlopeSelected = family.TryGetSlopeDirection(target.BlockId, out var slopeDirection);
            var holdingBlockId = isSlopeSelected ? target.BlockId : family.StraightBlockId;
            var holdingBlockMaster = MasterHolder.BlockMaster.GetBlockMaster(holdingBlockId);
```

(2) ローカル関数 `UpdateCurrentPlaceInfos` の本体を置き換える。

```csharp
            (List<PlacementBlockCause> placeCauses, List<BeltConveyorPlacementBlockReason> beltReasons) UpdateCurrentPlaceInfos()
            {
                var dragStartPoint = _dragState.ResolveDragStartCell(placePoint);
                if (dragStartPoint == placePoint)
                {
                    _isStartZDirection = null;
                }
                else if (!_isStartZDirection.HasValue)
                {
                    _isStartZDirection = Mathf.Abs(placePoint.x - dragStartPoint.x) < Mathf.Abs(placePoint.z - dragStartPoint.z);
                }

                // 坂選択中は一定勾配の専用経路。立体交差も坂の自動割り当ても通さない
                // A selected slope uses the constant-grade path: neither the overpass nor the auto slope assignment runs
                if (isSlopeSelected)
                {
                    _currentPlaceInfos = _blockPlacePointCalculator.CalculateSlopePoint(dragStartPoint, placePoint, _isStartZDirection ?? true, _currentBlockDirection, holdingBlockMaster, slopeDirection, out var slopeCauses, out var slopeBeltReasons);
                    return (slopeCauses, slopeBeltReasons);
                }

                var cellInfos = _blockPlacePointCalculator.CalculatePoint(dragStartPoint, placePoint, _isStartZDirection ?? true, _currentBlockDirection, holdingBlockMaster, out var cellCauses, out var cellBeltReasons);

                // セル列へ直線・坂ブロックを1対1で割り当てる（坂欠落はベルト固有理由の列へ書き戻される）
                // Assign straight and slope blocks to cells one-to-one (a missing slope is written back into the belt reason column)
                _currentPlaceInfos = BeltConveyorCellBlockResolver.Resolve(cellInfos, family, cellBeltReasons);

                return (cellCauses, cellBeltReasons);
            }
```

他の箇所（プレビュー・地面重なり・建設コスト・送信）は `holdingBlockMaster` を通して既に坂対応になるため変更しない。

- [ ] **Step 4.5: 要件9・10を構造で確認する**

要件9（坂選択中は立体交差を通さない）と要件10（地面重なり判定は共通経路のまま）は、Unityの `BlockGameObjectDataStore` 依存のためユニットテストで直接検証できない。次のgrepで構造として確認する。

```bash
grep -n "ConveyorOverpassRaiser" moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorPlacePointCalculator.cs
```
Expected: `CalculatePoint`（直線経路）の中の1箇所だけにヒットし、`CalculateSlopePoint` の内側には一切現れないこと。

```bash
grep -n "ApplyGroundOverlapsAndReport\|DetectGroundOverlaps" moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs
```
Expected: それぞれ1箇所のみ（`isSlopeSelected` の分岐の外＝坂も直線も同じ地面重なり経路を通る）。

- [ ] **Step 5: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件

`BeltConveyorPlaceSystem.cs` が200行を超えていないことを確認する。

Run: `wc -l moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs`
Expected: 200以下。超えた場合は `GroundClickControl` のローカル関数群を `Parts/` の新クラスへ切り出す（`partial` は禁止）。

- [ ] **Step 6: 関連テストを実行する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BeltConveyor|ConveyorOverpass"`
Expected: PASS（既存の `BeltConveyorCellBlockResolverTest` / `BeltConveyorPlacePointCalculatorTest` / `ConveyorOverpassConveyanceTest` が無変更で通ること＝要件12）

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorPlacePointCalculator.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs \
        moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltConveyor/BeltConveyorSlopePlacementTest.cs*
git commit -m "feat: 坂選択時は一定勾配の専用経路で設置する"
```

---

### Task 6: 全体回帰とログ確認

**Files:**
- Modify: なし（確認のみ。失敗が出た場合は該当タスクへ戻る）

**Interfaces:**
- Consumes: Task 1〜5 の全成果

- [ ] **Step 1: フルコンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0件・新規警告0件

- [ ] **Step 2: 設置系テストを一括実行する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaceSystem|PlacementTarget|BeltConveyor|ConveyorOverpass|Hotbar|PlaceBlockProtocol"`
Expected: PASS（失敗ゼロ）

「Unity is reloading (Domain Reload in progress)」が出た場合は45秒待ってリトライする。

- [ ] **Step 3: エラーログを確認する**

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: 本作業に起因するエラーが無いこと

- [ ] **Step 4: マスタに差分が無いことを確認する（要件4）**

```bash
git -C ../moorestech_master status --porcelain
```
Expected: 出力が空

- [ ] **Step 5: コミットする**

差分が無ければコミット不要。ステップ1〜4で修正が入った場合のみ:

```bash
git add -A
git commit -m "fix: 坂ベルト単体設置の回帰修正"
```

---

### Task 7: ブランチ全体のコードレビュー（必須・省略不可）

- [ ] **Step 1: moores-code-review スキルでブランチ全体をレビューする**

`moores-code-review` スキルを起動し、`master` からの全差分をレビュー対象にする。指摘のうち機械的修正は適用し、設計判断が要るものだけユーザーへ諮る。

このタスクは自動実行であり、ゴール文言やplanの都合で省略してはならない。

- [ ] **Step 2: レビュー指摘の修正をコミットする**

```bash
git add -A
git commit -m "fix: コードレビュー指摘の修正"
```

---

## 判断記録（ADR）

- 設計裁定の正本: `docs/adr/0050-slope-belt-standalone-placement.md`（2026-09-05・ユーザー裁定）
- 裁定台帳:
  - `.decisions/2026-09-05-坂ベルトは個別エントリとして単体設置できるようにする.md`
  - `.decisions/2026-09-05-坂選択中のドラッグは全セル坂にし角も坂にする.md`
  - `.decisions/2026-09-05-坂の解放と建設コストは直線代表を維持する.md`
- 関連: `docs/adr/0026-belt-construction-cost-remaining-placement-count.md`（財布3個1セット。今回は変更しない）

planning中に新たに生じた判断:

- **解放判定のGuid正規化を `BeltConveyorPlaceFamilyUtil.ResolveUnlockBlockGuid` へ集約する。** 出所: agent前提（AGENTS.md「同種の条件分岐は文脈が集まっている側の一箇所へ揃える」。現在 `PlaceBlockProtocol.IsUnlocked` に同じ規則がありカタログ側で二重定義になるため）
- **坂の上下向き解決は `BeltConveyorFamily.TryGetSlopeDirection` に置き、`IsSlopeBlock` をそれへ委譲する。** 出所: agent前提（同クラスの `IsSlopeBlock` が既にファミリー内メンバー判定を持つ前例）
- **`BlockPickResolver.TryResolvePickTarget` を `IsPickable` へ改名する。** 正規化をやめると out 引数が常に入力と同値になり、名前が実処理と一致しなくなるため。出所: agent前提（AGENTS.md「名前は実処理と一致させる」）
- **坂経路の純粋計算とUnity依存の占有判定を分ける（`BeltConveyorSlopePathBuilder` と `CalculateSlopePoint`）。** 既存の `BeltConveyorPathBuilder` / `BeltConveyorPlacePointCalculator` の分担に合わせ、幾何のテストをUnity非依存に保つため。出所: agent前提（前例一致）
- **坂選択時に述語の注入（`Func<>`）で経路を切り替えない。** AGENTS.md で `Func<>` 禁止のため、`CalculateSlopePoint` を専用メソッドとして生やし、占有判定はクラス内の既存privateを使う。出所: agent前提（AGENTS.md）
