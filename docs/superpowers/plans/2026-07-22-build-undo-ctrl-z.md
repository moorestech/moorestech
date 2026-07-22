# 建築UI Ctrl+Z アンドゥ Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 建築UI（設置モード・破壊モード）で Ctrl+Z / Cmd+Z により、自分の直前の建築操作（設置バッチ／撤去バッチ）を1操作単位で取り消せるようにする。

**Architecture:** クライアント完結。設置送信の一本化点（`PlaceSystemUtil.SendPlaceBlockProtocol`）と撤去コミット点（`DragDeleteSelection.CommitDelete`）で操作を履歴スタックへ記録し、Ctrl+Zで逆操作（設置→`va:removeBlock`、撤去→`va:placeBlock`）を既存プロトコルで発行する。逆操作の発行前にクライアントワールドモデル（`BlockGameObjectDataStore`）と照合するガードで誤爆を防ぐ。サーバー変更なし。

**Tech Stack:** Unity C#（Client.Game asmdef）、VContainer、UniTask、既存 `VanillaApi`。スペック: `docs/superpowers/specs/2026-07-22-build-undo-ctrl-z-design.md`

## Global Constraints

- サーバー側コード・プロトコル・スキーマは一切変更しない
- 1ファイル200行以下・partial禁止・イベントはUniRx（本計画では新規イベント無し）
- 単純getter/setterプロパティ禁止（値SetはSetHogeメソッド。ただし読み取り専用プロパティは可）
- コメントは日本語→英語の2行セット、主要セクション毎
- 各タスク完了時に `uloop compile --project-path ./moorestech_client` を実行しエラー0を確認
- .metaファイルは手動作成しない（Unityが生成したものをコミットするのは可）
- 履歴上限は32エントリ。Redo無し。履歴はセッション内のみ

## File Structure

```
moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/
  IBuildOperationRecord.cs      … 履歴エントリのマーカーinterface
  PlaceOperationRecord.cs       … 設置バッチ記録＋Undo対象セル選別（純C#）
  RemoveOperationRecord.cs      … 撤去バッチ記録＋再設置対象セル選別（純C#）
  BuildOperationHistory.cs      … 上限付きLIFOスタック（純C#）
  BuildUndoService.cs           … Ctrl+Z検知・照合ガード・逆操作発行
moorestech_client/Assets/Scripts/Client.Tests/BuildUndo/
  BuildOperationHistoryTest.cs
  PlaceOperationRecordTest.cs
  RemoveOperationRecordTest.cs
```

変更ファイル:
- `Client.Input/HybridInput.cs` … `Z`/`LeftCommand` のInputSystemキーマッピング追加
- `Client.Game/InGame/Context/ClientDIContext.cs` … `BuildOperationHistory` のstatic公開
- `Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs` … 設置記録フック
- `Client.Game/InGame/UI/UIState/State/DragDelete/DragDeleteSelection.cs` … CommitDeleteが対象リストを返す
- `Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs` … 撤去記録フック
- `Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs` / `DeleteObjectState.cs` … BuildUndoService駆動＋キー説明
- `Client.Starter/MainGameStarter.cs` … DI登録
- `Client.Tests/UIState/UIStateCameraInteractionTest.cs` / `UIStateFocusRestorationTest.cs` … ctor変更追従

---

### Task 1: 履歴スタックとレコード型（純C#）＋ユニットテスト

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/IBuildOperationRecord.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/PlaceOperationRecord.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/RemoveOperationRecord.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/BuildOperationHistory.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BuildUndo/BuildOperationHistoryTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BuildUndo/PlaceOperationRecordTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BuildUndo/RemoveOperationRecordTest.cs`

**Interfaces:**
- Consumes: `PlaceInfo`（`Server.Protocol.PacketResponse`）、`BlockId`（`Core.Master`）、`BlockDirection`/`BlockVerticalDirection`（`Game.Block.Interface`）
- Produces:
  - `interface IBuildOperationRecord {}`
  - `PlaceOperationRecord.CreateFrom(List<PlaceInfo>)` → `PlaceOperationRecord`（Placeableセルのみスナップショット）
  - `PlaceOperationRecord.HasCells` → `bool`（空バッチのPush抑止用）
  - `PlaceOperationRecord.SelectUndoableCells(Func<Vector3Int, BlockId?> blockIdAt)` → `List<Vector3Int>`
  - `RemoveOperationRecord(List<RemovedBlockInfo>)`、`struct RemovedBlockInfo { Vector3Int Position; BlockId BlockId; BlockDirection Direction; }`
  - `RemoveOperationRecord.SelectReplaceableCells(Func<RemovedBlockInfo, bool> isOccupied)` → `List<RemovedBlockInfo>`（占有判定はセル情報ごと渡す。マルチセルブロックの占有範囲照合に必要）
  - `BuildOperationHistory.Push(IBuildOperationRecord)` / `TryPop(out IBuildOperationRecord)`（上限32・最古破棄）

- [ ] **Step 1: 失敗するテストを書く**

`BuildOperationHistoryTest.cs`:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using NUnit.Framework;
using System.Collections.Generic;

namespace Client.Tests.BuildUndo
{
    public class BuildOperationHistoryTest
    {
        [Test]
        public void PushしたレコードがLIFO順でPopされる()
        {
            var history = new BuildOperationHistory();
            var first = new RemoveOperationRecord(new List<RemovedBlockInfo>());
            var second = new RemoveOperationRecord(new List<RemovedBlockInfo>());
            history.Push(first);
            history.Push(second);

            Assert.IsTrue(history.TryPop(out var popped1));
            Assert.AreSame(second, popped1);
            Assert.IsTrue(history.TryPop(out var popped2));
            Assert.AreSame(first, popped2);
            Assert.IsFalse(history.TryPop(out _));
        }

        [Test]
        public void 上限32を超えると最古のレコードが破棄される()
        {
            var history = new BuildOperationHistory();
            var records = new List<RemoveOperationRecord>();
            for (var i = 0; i < 33; i++)
            {
                var record = new RemoveOperationRecord(new List<RemovedBlockInfo>());
                records.Add(record);
                history.Push(record);
            }

            // 33件Pushすると最初の1件だけが落ち、32件がLIFOで取り出せる
            // After 33 pushes only the first record is dropped; 32 pop in LIFO order
            for (var i = 32; i >= 1; i--)
            {
                Assert.IsTrue(history.TryPop(out var popped));
                Assert.AreSame(records[i], popped);
            }
            Assert.IsFalse(history.TryPop(out _));
        }
    }
}
```

`PlaceOperationRecordTest.cs`:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Tests.BuildUndo
{
    public class PlaceOperationRecordTest
    {
        [Test]
        public void CreateFromはPlaceableなセルだけをスナップショットする()
        {
            var placeInfos = new List<PlaceInfo>
            {
                CreatePlaceInfo(new Vector3Int(0, 0, 0), 1, placeable: true),
                CreatePlaceInfo(new Vector3Int(1, 0, 0), 1, placeable: false),
            };
            var record = PlaceOperationRecord.CreateFrom(placeInfos);

            var cells = record.SelectUndoableCells(_ => new BlockId(1));
            CollectionAssert.AreEqual(new[] { new Vector3Int(0, 0, 0) }, cells);
        }

        [Test]
        public void SelectUndoableCellsは同座標同BlockIdのセルだけを返す()
        {
            var placeInfos = new List<PlaceInfo>
            {
                CreatePlaceInfo(new Vector3Int(0, 0, 0), 1, placeable: true), // 現存・ID一致 → 対象
                CreatePlaceInfo(new Vector3Int(1, 0, 0), 1, placeable: true), // 消滅 → 除外
                CreatePlaceInfo(new Vector3Int(2, 0, 0), 1, placeable: true), // 別ブロックに置換 → 除外
            };
            var record = PlaceOperationRecord.CreateFrom(placeInfos);

            var worldState = new Dictionary<Vector3Int, BlockId?>
            {
                [new Vector3Int(0, 0, 0)] = new BlockId(1),
                [new Vector3Int(1, 0, 0)] = null,
                [new Vector3Int(2, 0, 0)] = new BlockId(99),
            };
            var cells = record.SelectUndoableCells(pos => worldState[pos]);
            CollectionAssert.AreEqual(new[] { new Vector3Int(0, 0, 0) }, cells);
        }

        [Test]
        public void CreateFromは元リストのスナップショットであり後からの変更に影響されない()
        {
            var placeInfo = CreatePlaceInfo(new Vector3Int(0, 0, 0), 1, placeable: true);
            var placeInfos = new List<PlaceInfo> { placeInfo };
            var record = PlaceOperationRecord.CreateFrom(placeInfos);

            // 設置システムはPlaceInfoを使い回すため、記録後の変更が履歴を汚してはならない
            // Placement systems reuse PlaceInfo, so later mutation must not corrupt the record
            placeInfo.Position = new Vector3Int(9, 9, 9);
            var cells = record.SelectUndoableCells(_ => new BlockId(1));
            CollectionAssert.AreEqual(new[] { new Vector3Int(0, 0, 0) }, cells);
        }

        private static PlaceInfo CreatePlaceInfo(Vector3Int position, int blockId, bool placeable)
        {
            return new PlaceInfo
            {
                Position = position,
                Direction = BlockDirection.North,
                VerticalDirection = BlockVerticalDirection.Horizontal,
                BlockId = new BlockId(blockId),
                Placeable = placeable,
            };
        }
    }
}
```

`RemoveOperationRecordTest.cs`:

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Core.Master;
using Game.Block.Interface;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Client.Tests.BuildUndo
{
    public class RemoveOperationRecordTest
    {
        [Test]
        public void SelectReplaceableCellsは占有されていないセルだけを返す()
        {
            var record = new RemoveOperationRecord(new List<RemovedBlockInfo>
            {
                new(new Vector3Int(0, 0, 0), new BlockId(1), BlockDirection.North), // 空 → 再設置対象
                new(new Vector3Int(1, 0, 0), new BlockId(1), BlockDirection.East),  // 占有（撤去失敗 or 他者設置）→ 除外
            });

            var occupied = new HashSet<Vector3Int> { new(1, 0, 0) };
            var cells = record.SelectReplaceableCells(info => occupied.Contains(info.Position));

            Assert.AreEqual(1, cells.Count);
            Assert.AreEqual(new Vector3Int(0, 0, 0), cells[0].Position);
            Assert.AreEqual(BlockDirection.North, cells[0].Direction);
        }
    }
}
```

- [ ] **Step 2: テストが失敗（コンパイルエラー）することを確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `BuildOperationHistory` 等が存在しないためコンパイルエラー

- [ ] **Step 3: 最小実装を書く**

`IBuildOperationRecord.cs`:

```csharp
namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo
{
    /// <summary>
    ///     建築操作履歴の1エントリ（設置バッチ or 撤去バッチ）
    ///     One entry of build operation history (a place batch or a remove batch)
    /// </summary>
    public interface IBuildOperationRecord
    {
    }
}
```

`BuildOperationHistory.cs`:

```csharp
using System.Collections.Generic;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo
{
    /// <summary>
    ///     建築操作の履歴スタック。上限を超えたら最古を破棄するLIFO
    ///     Build operation history stack; LIFO that drops the oldest entry over capacity
    /// </summary>
    public class BuildOperationHistory
    {
        private const int MaxHistoryCount = 32;
        private readonly LinkedList<IBuildOperationRecord> _records = new();

        public void Push(IBuildOperationRecord record)
        {
            _records.AddLast(record);
            if (_records.Count > MaxHistoryCount) _records.RemoveFirst();
        }

        public bool TryPop(out IBuildOperationRecord record)
        {
            record = null;
            if (_records.Count == 0) return false;

            record = _records.Last.Value;
            _records.RemoveLast();
            return true;
        }
    }
}
```

`PlaceOperationRecord.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo
{
    /// <summary>
    ///     設置1バッチの履歴レコード。送信時のPlaceInfoをスナップショットとして保持する
    ///     History record of one place batch; holds a snapshot of the sent PlaceInfo list
    /// </summary>
    public class PlaceOperationRecord : IBuildOperationRecord
    {
        private readonly List<PlacedCell> _cells;

        private PlaceOperationRecord(List<PlacedCell> cells)
        {
            _cells = cells;
        }

        /// <summary>
        ///     有効セルが1件以上あるか（空バッチをPushしないためのガード）
        ///     Whether the record has any cells (guards against pushing an empty batch)
        /// </summary>
        public bool HasCells => _cells.Count > 0;

        public static PlaceOperationRecord CreateFrom(List<PlaceInfo> placeInfos)
        {
            // 設置システムはPlaceInfoを使い回すため値をコピーして保持する
            // Placement systems reuse PlaceInfo instances, so copy the values we need
            var cells = new List<PlacedCell>(placeInfos.Count);
            foreach (var info in placeInfos)
            {
                if (!info.Placeable) continue;
                cells.Add(new PlacedCell(info.Position, info.BlockId));
            }
            return new PlaceOperationRecord(cells);
        }

        /// <summary>
        ///     同座標に同BlockIdのブロックが現存するセルだけをUndo対象として返す
        ///     Return only cells whose position still holds a block with the same BlockId
        /// </summary>
        public List<Vector3Int> SelectUndoableCells(Func<Vector3Int, BlockId?> blockIdAt)
        {
            var result = new List<Vector3Int>();
            foreach (var cell in _cells)
            {
                var currentBlockId = blockIdAt(cell.Position);
                if (currentBlockId == null || !currentBlockId.Value.Equals(cell.BlockId)) continue;
                result.Add(cell.Position);
            }
            return result;
        }

        private readonly struct PlacedCell
        {
            public readonly Vector3Int Position;
            public readonly BlockId BlockId;

            public PlacedCell(Vector3Int position, BlockId blockId)
            {
                Position = position;
                BlockId = blockId;
            }
        }
    }
}
```

`RemoveOperationRecord.cs`:

```csharp
using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo
{
    /// <summary>
    ///     撤去1バッチの履歴レコード。コミット時点の選択対象を楽観的に記録する
    ///     History record of one remove batch; optimistically captured at commit time
    /// </summary>
    public class RemoveOperationRecord : IBuildOperationRecord
    {
        private readonly List<RemovedBlockInfo> _removedBlocks;

        public RemoveOperationRecord(List<RemovedBlockInfo> removedBlocks)
        {
            _removedBlocks = removedBlocks;
        }

        /// <summary>
        ///     占有されていないセルだけを再設置対象として返す（撤去失敗・他者設置セルを除外）
        ///     Return only unoccupied cells (excludes failed removals and rebuilt cells)
        /// </summary>
        public List<RemovedBlockInfo> SelectReplaceableCells(Func<RemovedBlockInfo, bool> isOccupied)
        {
            var result = new List<RemovedBlockInfo>();
            foreach (var removed in _removedBlocks)
            {
                if (isOccupied(removed)) continue;
                result.Add(removed);
            }
            return result;
        }
    }

    public readonly struct RemovedBlockInfo
    {
        public readonly Vector3Int Position;
        public readonly BlockId BlockId;
        public readonly BlockDirection Direction;

        public RemovedBlockInfo(Vector3Int position, BlockId blockId, BlockDirection direction)
        {
            Position = position;
            BlockId = blockId;
            Direction = direction;
        }
    }
}
```

- [ ] **Step 4: コンパイルとテスト実行**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.BuildUndo"`
Expected: 6テスト全PASS

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo moorestech_client/Assets/Scripts/Client.Tests/BuildUndo
git commit -m "feat(client): 建築Undo履歴スタックとレコード型を追加"
```

（Unity生成の.metaは `git status` で確認して同コミットに含める）

---

### Task 2: BuildUndoService（Ctrl+Z検知・逆操作発行）とHybridInputマッピング

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/BuildUndoService.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Input/HybridInput.cs`（`ToInputSystemKey` のswitchに2行追加）

**Interfaces:**
- Consumes: Task 1の `BuildOperationHistory` / `PlaceOperationRecord.SelectUndoableCells` / `RemoveOperationRecord.SelectReplaceableCells`、`BlockGameObjectDataStore.TryGetBlockGameObject(Vector3Int, out BlockGameObject)`、`ClientContext.VanillaApi.Response.BlockRemove(Vector3Int, CancellationToken)`、`ClientContext.VanillaApi.SendOnly.PlaceBlock(List<PlaceInfo>)`
- Produces: `BuildUndoService.ManualUpdate()`（UIステートから毎フレーム呼ぶ。Ctrl+Z判定はサービス内部）

- [ ] **Step 1: HybridInputにキーマッピングを追加**

`HybridInput.cs` の `ToInputSystemKey` switch（`KeyCode.LeftControl => Key.LeftCtrl,` の下）に追加:

```csharp
                KeyCode.LeftCommand => Key.LeftCommand,
                KeyCode.Z => Key.Z,
```

（プレイテストDSLの `QueueStateEvent` 注入はInputSystem経路のみ通るため、このマッピングが無いとE2Eテスト不能）

- [ ] **Step 2: BuildUndoServiceを実装**

`BuildUndoService.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Input;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Block.Interface;
using Server.Protocol.PacketResponse;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Undo
{
    /// <summary>
    ///     Ctrl+Zで直前の建築操作を取り消す。UIステートからManualUpdateで毎フレーム駆動される
    ///     Undo the latest build operation on Ctrl+Z; driven every frame from UI states via ManualUpdate
    /// </summary>
    public class BuildUndoService
    {
        private readonly BuildOperationHistory _history;
        private readonly BlockGameObjectDataStore _blockGameObjectDataStore;
        private bool _isUndoing;

        public BuildUndoService(BuildOperationHistory history, BlockGameObjectDataStore blockGameObjectDataStore)
        {
            _history = history;
            _blockGameObjectDataStore = blockGameObjectDataStore;
        }

        public void ManualUpdate()
        {
            if (!IsUndoKeyPressed()) return;
            if (_isUndoing) return;
            if (!_history.TryPop(out var record)) return;

            UndoAsync(record).Forget();

            #region Internal

            static bool IsUndoKeyPressed()
            {
                var modifierHeld = HybridInput.GetKey(KeyCode.LeftControl) || HybridInput.GetKey(KeyCode.LeftCommand);
                return modifierHeld && HybridInput.GetKeyDown(KeyCode.Z);
            }

            #endregion
        }

        private async UniTask UndoAsync(IBuildOperationRecord record)
        {
            _isUndoing = true;
            // ネットワーク送受信（外部境界）の例外でも再入フラグを必ず復帰させる（try-catch原則禁止の境界例外条項）
            // Guarantee the re-entrancy flag resets even on network-boundary exceptions (boundary exemption of the no-try-catch rule)
            try
            {
                switch (record)
                {
                    case PlaceOperationRecord placeRecord:
                        await UndoPlaceOperation(placeRecord);
                        break;
                    case RemoveOperationRecord removeRecord:
                        UndoRemoveOperation(removeRecord);
                        break;
                }
            }
            finally
            {
                _isUndoing = false;
            }

            #region Internal

            async UniTask UndoPlaceOperation(PlaceOperationRecord placeRecord)
            {
                // 同座標同BlockIdの現存セルだけを撤去する（設置失敗・他者変更セルの誤爆防止）
                // Remove only cells still holding the same BlockId (avoids nuking failed or replaced cells)
                var cells = placeRecord.SelectUndoableCells(GetBlockIdAt);
                foreach (var position in cells)
                {
                    await ClientContext.VanillaApi.Response.BlockRemove(position, CancellationToken.None);
                }
            }

            void UndoRemoveOperation(RemoveOperationRecord removeRecord)
            {
                // 占有範囲が空いているセルだけを1バッチで再設置する（CreateParamsは復元不可のため空）
                // Re-place only cells whose footprint is unoccupied, in one batch (CreateParams cannot be restored, so empty)
                var cells = removeRecord.SelectReplaceableCells(IsFootprintOccupied);
                if (cells.Count == 0) return;

                var placeInfos = new List<PlaceInfo>(cells.Count);
                foreach (var cell in cells)
                {
                    placeInfos.Add(new PlaceInfo
                    {
                        Position = cell.Position,
                        Direction = cell.Direction,
                        VerticalDirection = ToVerticalDirection(cell.Direction),
                        BlockId = cell.BlockId,
                        Placeable = true,
                    });
                }
                ClientContext.VanillaApi.SendOnly.PlaceBlock(placeInfos);
            }

            BlockId? GetBlockIdAt(Vector3Int position)
            {
                if (!_blockGameObjectDataStore.TryGetBlockGameObject(position, out var blockGameObject)) return null;
                return blockGameObject.BlockId;
            }

            bool IsFootprintOccupied(RemovedBlockInfo removed)
            {
                // 辞書キーはオリジン座標のみのため、マルチセルブロックとの重なりは占有範囲同士で判定する
                // The dictionary keys origins only, so overlap with multi-cell blocks needs a footprint check
                var blockSize = MasterHolder.BlockMaster.GetBlockMaster(removed.BlockId).BlockSize;
                var positionInfo = new BlockPositionInfo(removed.Position, removed.Direction, blockSize);
                return _blockGameObjectDataStore.IsOverlapPositionInfo(positionInfo);
            }

            static BlockVerticalDirection ToVerticalDirection(BlockDirection direction)
            {
                return direction switch
                {
                    BlockDirection.UpNorth or BlockDirection.UpEast or BlockDirection.UpSouth or BlockDirection.UpWest => BlockVerticalDirection.Up,
                    BlockDirection.DownNorth or BlockDirection.DownEast or BlockDirection.DownSouth or BlockDirection.DownWest => BlockVerticalDirection.Down,
                    _ => BlockVerticalDirection.Horizontal,
                };
            }

            #endregion
        }
    }
}
```

- [ ] **Step 3: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Undo/BuildUndoService.cs moorestech_client/Assets/Scripts/Client.Input/HybridInput.cs
git commit -m "feat(client): BuildUndoService実装とCtrl+Z用キーマッピング追加"
```

---

### Task 3: DI登録と設置側の記録フック

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`（State登録群 216-227行付近）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Context/ClientDIContext.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs:149-155`

**Interfaces:**
- Consumes: Task 1の `BuildOperationHistory` / `PlaceOperationRecord.CreateFrom`、Task 2の `BuildUndoService`
- Produces: `ClientDIContext.BuildOperationHistory`（static。静的ユーティリティからの履歴アクセス用）、VContainer解決可能な `BuildUndoService`

- [ ] **Step 1: MainGameStarterにDI登録を追加**

`builder.Register<BuildMenuState>(Lifetime.Singleton);`（227行）の下に追加:

```csharp
            builder.Register<BuildOperationHistory>(Lifetime.Singleton);
            builder.Register<BuildUndoService>(Lifetime.Singleton);
```

using追加: `using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;`

- [ ] **Step 2: ClientDIContextにstatic公開を追加**

```csharp
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using VContainer;

namespace Client.Game.InGame.Context
{
    public class ClientDIContext
    {
        public static DIContainer DIContainer { get; private set; }
        public static BlockGameObjectDataStore BlockGameObjectDataStore { get; set; }
        public static BuildOperationHistory BuildOperationHistory { get; private set; }
        
        public ClientDIContext(DIContainer diContainer)
        {
            DIContainer = diContainer;
            BlockGameObjectDataStore = diContainer.DIContainerResolver.Resolve<BlockGameObjectDataStore>();
            BuildOperationHistory = diContainer.DIContainerResolver.Resolve<BuildOperationHistory>();
        }
    }
}
```

- [ ] **Step 3: PlaceSystemUtilに記録フックを追加**

`SendPlaceBlockProtocol` を以下に変更:

```csharp
        public static void SendPlaceBlockProtocol(List<PlaceInfo> currentPlaceInfos)
        {
            // セル毎BlockId付きでPlaceInfoをサーバーに送信
            // Send PlaceInfo to server; each cell already carries its own BlockId
            ClientContext.VanillaApi.SendOnly.PlaceBlock(currentPlaceInfos);

            // Ctrl+Z用に設置バッチを履歴へ記録する（全セル設置不能の空バッチは積まない）
            // Record the place batch into the undo history for Ctrl+Z (skip empty batches where no cell was placeable)
            var record = PlaceOperationRecord.CreateFrom(currentPlaceInfos);
            if (record.HasCells) ClientDIContext.BuildOperationHistory.Push(record);

            SoundEffectManager.Instance.PlaySoundEffect(SoundEffectType.PlaceBlock);
        }
```

using追加: `using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;`（`Client.Game.InGame.Context` は既存参照を確認、無ければ追加）

- [ ] **Step 4: コンパイルとコミット**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

```bash
git add moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Context/ClientDIContext.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs
git commit -m "feat(client): 設置バッチのUndo履歴記録とDI登録"
```

---

### Task 4: 撤去側の記録フック

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DragDeleteSelection.cs:87-103`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs`（ctor経由でhistoryを渡す）

**Interfaces:**
- Consumes: Task 1の `BuildOperationHistory` / `RemoveOperationRecord` / `RemovedBlockInfo`、`BlockGameObjectChild.BlockGameObject`（`BlockId` / `BlockPosInfo.OriginalPos` / `BlockPosInfo.BlockDirection`）
- Produces: `DragDeleteSelection.CommitDelete()` → `List<IDeleteTarget>`（コミットした対象。従来voidから変更）、`DeleteObjectService(BuildOperationHistory)` ctor、`DeleteObjectState(DeleteBarObject, RailGraphClientCache, IPlayerCameraInteractionApplier, BuildOperationHistory)` ctor（4引数。Task 5で `BuildUndoService` を足して5引数になる）

- [ ] **Step 1: CommitDeleteがコミット対象を返すよう変更**

`DragDeleteSelection.CommitDelete` を以下に変更:

```csharp
        // 選択対象を全て削除しMaterialを戻して選択を空にする。コミットした対象を履歴記録用に返す
        // Delete all selected targets, reset materials, clear the selection, and return the committed targets for history recording
        public List<IDeleteTarget> CommitDelete()
        {
            if (_canceled) return new List<IDeleteTarget>();

            var committed = new List<IDeleteTarget>(_selectedTargets.Values);
            foreach (var target in committed)
            {
                // Delete はサーバー往復の非同期なので即座に赤プレビューだけ戻す
                // Delete is async over the server, so we just clear the red preview immediately
                target.Delete();
                target.ResetMaterial();
            }

            _selectedTargets.Clear();
            _sessionCategory = null;
            return committed;
        }
```

（`using System.Collections.Generic;` は既存。既存テストは戻り値を無視して呼んでいるため互換）

- [ ] **Step 2: DeleteObjectServiceに履歴記録を追加**

ctorと `HandleRelease` を変更:

```csharp
        private readonly BuildOperationHistory _buildOperationHistory;

        public DeleteObjectService(BuildOperationHistory buildOperationHistory)
        {
            _buildOperationHistory = buildOperationHistory;
        }
```

`HandleRelease` 内の `if (_isDragging && _selection.CanCommit()) _selection.CommitDelete();` を:

```csharp
                if (_isDragging && _selection.CanCommit()) RecordAndCommitDelete();
```

に変更し、`#region Internal` 内へローカル関数を追加:

```csharp
            void RecordAndCommitDelete()
            {
                var committed = _selection.CommitDelete();

                // ブロック対象だけをCtrl+Z用に楽観的記録（撤去失敗セルはUndo時の空き座標ガードで自然に無効化）
                // Optimistically record block targets for Ctrl+Z (failed removals are neutralized by the empty-cell guard on undo)
                var removedBlocks = new List<RemovedBlockInfo>();
                foreach (var target in committed)
                {
                    if (target is not BlockGameObjectChild blockChild) continue;
                    var blockGameObject = blockChild.BlockGameObject;
                    removedBlocks.Add(new RemovedBlockInfo(
                        blockGameObject.BlockPosInfo.OriginalPos,
                        blockGameObject.BlockId,
                        blockGameObject.BlockPosInfo.BlockDirection));
                }
                if (removedBlocks.Count != 0) _buildOperationHistory.Push(new RemoveOperationRecord(removedBlocks));
            }
```

using追加: `using Client.Game.InGame.Block;` `using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;` `using System.Collections.Generic;`

- [ ] **Step 3: DeleteObjectStateからhistoryを渡す**

`DeleteObjectState` のフィールド初期化 `private readonly DeleteObjectService _deleteObjectService = new();` をctor内初期化に変更し、ctorへ `BuildOperationHistory buildOperationHistory` を追加:

```csharp
        public DeleteObjectState(DeleteBarObject deleteBarObject, RailGraphClientCache cache, IPlayerCameraInteractionApplier cameraInteractionApplier, BuildOperationHistory buildOperationHistory)
        {
            _deleteBarObject = deleteBarObject;
            _cameraInteractionApplier = cameraInteractionApplier;
            _deleteObjectService = new DeleteObjectService(buildOperationHistory);
            deleteBarObject.gameObject.SetActive(false);
        }
```

フィールドは `private readonly DeleteObjectService _deleteObjectService;` に変更。using追加: `using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;`

- [ ] **Step 4: 既存テストのctor呼び出しを追従**

`Client.Tests/UIState/UIStateCameraInteractionTest.cs:90` と `UIStateFocusRestorationTest.cs:78` の
`new DeleteObjectState(deleteObject, null, applier)` を
`new DeleteObjectState(deleteObject, null, applier, new BuildOperationHistory())` に変更。
両ファイルにusing追加: `using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;`

- [ ] **Step 5: コンパイル・テスト・コミット**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "DragDeleteSelection|UIStateCameraInteraction|UIStateFocusRestoration"`
Expected: 既存テスト全PASS

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs moorestech_client/Assets/Scripts/Client.Tests/UIState
git commit -m "feat(client): 撤去バッチのUndo履歴記録"
```

---

### Task 5: UIステートへのCtrl+Z配線とキー説明更新

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DeleteObjectState.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateCameraInteractionTest.cs` / `UIStateFocusRestorationTest.cs`

**Interfaces:**
- Consumes: Task 2の `BuildUndoService.ManualUpdate()`
- Produces: 建築UI中（設置モード・破壊モード）のCtrl+Z有効化

- [ ] **Step 1: PlaceBlockStateに配線**

ctorへ `BuildUndoService buildUndoService` を追加しフィールド保持:

```csharp
        private readonly BuildUndoService _buildUndoService;

        public PlaceBlockState(SkitManager skitManager, BlockGameObjectDataStore blockGameObjectDataStore, PlaceSystemStateController placeSystemStateController, PlacementTargetPickService placementTargetPickService, IPlayerCameraInteractionApplier cameraInteractionApplier, BuildUndoService buildUndoService)
```

`GetNextUpdate()` の `_placeSystemStateController.ManualUpdate();` の直後に追加:

```csharp
            // Ctrl+Zで直前の建築操作を取り消す（判定はサービス内部）
            // Undo the latest build operation on Ctrl+Z (detection lives inside the service)
            _buildUndoService.ManualUpdate();
```

`OnEnter` のキー説明文字列末尾に `\nCtrl+Z: 元に戻す` を追加。
using追加: `using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;`

- [ ] **Step 2: DeleteObjectStateに配線**

ctorへ `BuildUndoService buildUndoService` を追加（Task 4で追加した `BuildOperationHistory` と並ぶ）しフィールド保持。
`GetNextUpdate()` の `_deleteObjectService.Update();` の直後に追加:

```csharp
            // Ctrl+Zで直前の建築操作を取り消す（判定はサービス内部）
            // Undo the latest build operation on Ctrl+Z (detection lives inside the service)
            _buildUndoService.ManualUpdate();
```

`OnEnter` のキー説明文字列末尾に `\nCtrl+Z: 元に戻す` を追加。

- [ ] **Step 3: 既存テストのctor呼び出しを追従**

- `UIStateCameraInteractionTest.cs:90` / `UIStateFocusRestorationTest.cs:78`:
  `new DeleteObjectState(deleteObject, null, applier, new BuildOperationHistory(), new BuildUndoService(new BuildOperationHistory(), null))`
- `UIStateCameraInteractionTest.cs:116` / `UIStateFocusRestorationTest.cs:96`:
  `new PlaceBlockState(skitManager, dataStore, placeStateController, pickService, applier, new BuildUndoService(new BuildOperationHistory(), dataStore))`

（EditModeテストではCtrl+Zを押さないため `ManualUpdate` はキー判定で即returnし、null datastoreにも触れない）

- [ ] **Step 4: コンパイル・テスト・コミット**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "UIStateCameraInteraction|UIStateFocusRestoration|Client.Tests.BuildUndo"`
Expected: 全PASS

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State moorestech_client/Assets/Scripts/Client.Tests/UIState
git commit -m "feat(client): 建築UIにCtrl+Zアンドゥを配線"
```

---

### Task 6: E2E検証（プレイテストDSL）

**Files:**
- Create: プレイテストDSLシナリオ（unity-playmode-recorded-playtest スキルの `references/` に従い、既存シナリオと同じ置き場に追加）

**Interfaces:**
- Consumes: Task 1-5の全実装、プレイテストDSL（`Client.Playtest` asmdef + `scripts/run-scenario.sh`）

- [ ] **Step 1: unity-playmode-recorded-playtest スキルを読み、既存シナリオの形式・置き場を確認する**

- [ ] **Step 2: シナリオ追加**

**必須前提**: 設置は必ずUI経路（ビルドメニュー→PlaceBlockState→クリック設置）で行うこと。過去のE2E回避策である `SendOnly.PlaceBlock` 直送は `PlaceSystemUtil` の記録フックを通らず履歴が積まれないため、Ctrl+Z検証が偽陰性になる。

内容（DSLの実アクション名はスキルreferencesに従う）:
1. ホットバーにブロックを持ち設置モードへ → 1ブロック設置 → ブロック存在を確認
2. Ctrl+Z（LeftCtrl押下→Z押下→両解放を `QueueStateEvent` で注入）→ ブロック消滅を確認
3. 再度設置 → 破壊モードでドラッグ撤去 → ブロック消滅を確認
4. Ctrl+Z → ブロック再出現を確認

- [ ] **Step 3: シナリオ実行**

Run: スキル同梱の `scripts/run-scenario.sh`（実行コマンドはスキル参照）
Expected: result.json が全ステップ成功。失敗時は録画とログで原因を特定して修正

- [ ] **Step 4: 全体テストとコミット**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests"`
Expected: 全PASS（ドメインリロードエラー時は45秒待ってリトライ）

```bash
git add <シナリオファイル>
git commit -m "test(playtest): Ctrl+ZアンドゥのE2Eシナリオを追加"
```

---

## Self-Review 済み事項

- **Spec coverage**: spec §3（記録フック2箇所・Undoフロー・ガード）→ Task 1-4、§4（入力・キー説明）→ Task 2/5、§5（エラー処理）→ Task 2のガードとスキップ、§7（テスト）→ Task 1/6。§6の既知の制限はコード対応不要
- **型整合**: `RemovedBlockInfo(Vector3Int, BlockId, BlockDirection)` / `SelectUndoableCells(Func<Vector3Int, BlockId?>)` / `SelectReplaceableCells(Func<RemovedBlockInfo, bool>)` をTask 1定義・Task 2/4消費で統一
- **Fableレビュー反映済み**: ①`_isUndoing` のtry-finally復帰保証（外部境界例外条項）②空バッチのPush抑止（`HasCells`）③撤去Undoの占有ガードを `IsOverlapPositionInfo` によるフットプリント判定へ変更（辞書はオリジンキーのみのため）④橋脚設置（`PlaceRailWithPier`）はUndo対象外としてspec既知の制限に明記⑤修飾キーはLeft系のみにspec側を統一⑥E2EはUI経路設置必須を明記
- **配置と前例**: spec §9 の通り（ステート駆動ManualUpdate・ClientDIContext static・VContainer登録・HybridInput直接キー参照）
- **注意点**: `BlockId?` の比較は `Equals` を使用（`BlockId` はUnitGenerator型のため `==` のnullable挙動を避ける）。`PlaceBlockState` を `WebUiGameBinder` / `PlacementModeTopic` もresolveしているがctor変更はVContainer解決のため影響なし
