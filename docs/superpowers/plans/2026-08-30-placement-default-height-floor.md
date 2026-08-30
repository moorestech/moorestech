# ブロック設置の既定Yを地形最高点を含むセルにする Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** 通常ブロック設置の既定Yを「地形最高点を上回る最初のセル」から「地形最高点を含むセル」へ下げ、あわせて通常設置から地形重なりゲートを外す。

**Architecture:** 変更は2箇所に閉じる。(1) `PlacementGroundCellResolver.ResolveCellY` の丸めを切り上げから切り捨てへ変える。(2) 共有ヘルパ `PlacementCellReasonReporter` に「地形を見ない入口」を足し、`CommonBlockPlaceSystem` だけをそちらへ差し替える。地形プローブ（`GroundHeightProbe`）とドラッグの階段状追従（`PlacementGroundFollowStep`）は無変更。

**Tech Stack:** Unity 6000.3.8f1 / C# / NUnit（EditMode）/ uloop CLI

## Requirements

- R1: 既定（手動オフセット0）の設置セルYが「占有フットプリントの地形最高点を含むセル」になる。受け入れ基準: 地表 32.4 のセルは Y=32 に解決される（現在は 33）
- R2: 地表がちょうど整数のときは沈まない。受け入れ基準: 地表 32.0 のセルは Y=32 のまま（浮きも沈みもしない）
- R3: 通常ブロック設置（`CommonBlockPlaceSystem` の単体設置＋ドラッグ範囲設置）で、地形との重なりが設置不可の理由にならない。受け入れ基準: 地形に食い込むセルでも `PlaceInfo.Placeable` が `true` のまま、「地形に埋まっています」も出ない
- R4: 地表が見つからないセルは従来どおり設置不可のまま。受け入れ基準: `PlacementBlockCause.GroundNotFound` の経路が変わらない
- R5: 手動オフセット（Q/E の `HeightOffset`）は解決後の加算のまま。受け入れ基準: 地表 20.4・オフセット +3 で Y=23、-2 で Y=18
- R6: ベルト・レール・電柱ゴースト・歯車ポールの地形ゲートは現状維持。受け入れ基準: `BeltConveyorPlaceSystem` / `TrainRailPlaceService` / `ElectricWirePoleGhostEvaluation` / `GearChainPoleExtendPreviewObject` の挙動と既存テストが無変更で通る

**やらないこと（スコープ境界）**

- ベルトコンベア・レール専用経路・設計図(BP)貼り付けのY決定には触らない
- `GroundHeightProbe`（フットプリント4隅の最高点プローブ）のロジックは変えない
- `PlacementGroundFollowStep` の追従可否判断（ブロック面ヒット除外・Y軸列除外）は変えない
- `GroundCollisionDetector` のトリガー方式そのものは直さない（ADR 0037 の非目標のまま）
- 「地形に埋まっています」の localization キー・文言は消さない（他系統が使う）

## Global Constraints

- 正本ADR: `docs/adr/0047-placement-default-height-floor-of-terrain-max.md`。裁定記録: `.decisions/2026-08-30-ブロック設置の既定高さは地形最高点を含むセルにする.md`
- ADR 0037 の「フットプリント最高点プローブ」「ドラッグの階段状地形追従」は維持し、「上回る最初のセル」だけを上書きする
- コメントは日本語1行→英語1行の2行セット。日本語本文は処理・変数20字、メソッド30字が目安
- `try-catch` 禁止。`Func<>` 禁止。`partial` 禁止。1ファイル200行以下
- `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client` を実行する
- テストは `--filter-type regex` で対象を絞り、`--test-mode EditMode` を明示する（既定はPlayMode）
- タスク台帳のissueは `moorestech-41zp`。着手時に claim し、完了時に close する
- 作業はタスク専用worktreeで行う（`moores-wt new fix/placement-default-height-floor`）。メインワークツリーでのブランチ操作はhookが拒否する

## 配置と前例

| # | 項目 | 配置先 | 前例 |
|---|---|---|---|
| 1 | `ResolveCellY` の丸め変更 | `Client.Game/.../PlaceSystem/Ground/PlacementGroundCellResolver.cs` | 既存の同ファイル。地表探査の所有者は `GroundHeightProbe` のまま |
| 2 | 地形を見ない理由集約の入口 | `Client.Game/.../PlaceSystem/Feedback/PlacementCellReasonReporter.cs` | 同ファイルが既に「カーソル解決＋共有理由の積み上げ」の単一所有者。地形を見るかどうかの判断だけを具体側（各設置システム）へ寄せる（AGENTS.md「判断は具体側で行い、基盤にはプッシュする」） |
| 3 | プレビュー配置と地形検出の分離 | `.../PreviewController/PlacementPreviewBlockGameObjectController.cs` | 既存 `SetPreviewAndGroundDetect` を `SetPreview` + 検出に割る。戻り値を捨てる呼び出しを作らないため |

**データフロー地図（新規コンポーネントの立ち位置）**

```
レイヒット → CalcPlacePoint → PlacementRun(セル列)
  → [IPlacementGroundFollower] 地形追従でYを解決   ← ここの丸めだけ変える
  → SetPreview / SetPreviewAndGroundDetect（プレビュー配置）
  → PlacementCellReasonReporter（Placeable と理由）  ← 通常設置だけ地形を見ない入口へ
  → Vein制限 → 素材不足 → 自動接続 → UpdatePlaceableColors
```

新規コンポーネントは無い。既存の駅で丸めと入口を差し替えるだけで、分岐・逆流・並行経路は足さない。

**機能パリティ死活表（地形重なりゲートにぶら下がる全操作）**

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 通常設置の「地形に埋まっています」表示 | **意図的に死ぬ** | ADR 0047 のユーザー裁定「通常設置からゲートを外す」 |
| ベルト設置の同表示 | 生きる | `BeltConveyorPlaceSystem:111` は `ApplyGroundOverlapsAndReport` のまま |
| レール設置の同表示 | 生きる | `TrainRailPlaceService:58` 無変更 |
| 電柱ゴーストの同表示 | 生きる | `ElectricWirePoleGhostEvaluation:51` 別経路・無変更 |
| 歯車ポールの地形検出 | 生きる | `GearChainPoleExtendPreviewObject:45` 無変更 |
| 地表なしセルの設置不可 | 生きる | `PlacementGroundFollowStep` の `GroundNotFound` を触らない |
| Q/E の高さオフセット | 生きる | 解決後の加算のまま |
| ブロック面ヒットの積み重ね | 生きる | `surfaceKind != Ground` で追従をスキップする分岐は無変更 |

死ぬ操作は1つだけで、それは裁定済みの当該仕様である。

---

### Task 1: 既定Yを地形最高点を含むセルにする

**Files:**

- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/PlacementGroundCellResolver.cs:13-32`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/PlacementGroundCellApplyTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/PlacementGroundFollowStepTest.cs:44-46,60,91`

**Interfaces:**

- Consumes: `GroundHeightProbe.TryGetFootprintMaxGroundHeight(Vector3Int blockPos, BlockDirection blockDirection, Vector3Int blockSize, out float maxHeight)` — 変更しない
- Produces: `PlacementGroundCellResolver.TryResolveCellFromGround(Vector3Int cellPosition, BlockDirection blockDirection, Vector3Int blockSize, int heightOffset, out Vector3Int resolvedPosition)` — シグネチャ不変、返すYだけが1段下がる

- [ ] **Step 1: 既存テストの期待値を新しい規約へ書き換える**

`PlacementGroundCellApplyTest.cs` の4つのテストを差し替える。スラブは `position.y` が中心・`scale.y = 1` なので地表 = `position.y + 0.5`。

```csharp
        // 地表32.4のセルは、その最高点を含むY32へ収まる
        // A cell over ground at 32.4 lands on Y 32, the cell containing that max
        [Test]
        public void 端数のある地表は最高点を含むセルへ収める()
        {
            CreateGroundSlab(new Vector3(100.5f, 31.9f, 200.5f), new Vector3(6f, 1f, 6f));

            Assert.IsTrue(TryResolve(new Vector3Int(100, 0, 200), 0, out var resolved));
            Assert.AreEqual(new Vector3Int(100, 32, 200), resolved);
        }

        // 整数ちょうどの地表は沈めない
        // Ground exactly on an integer must not sink
        [Test]
        public void 整数ちょうどの地表は沈めない()
        {
            CreateGroundSlab(new Vector3(300.5f, 31.5f, 400.5f), new Vector3(6f, 1f, 6f));

            Assert.IsTrue(TryResolve(new Vector3Int(300, 0, 400), 0, out var resolved));
            Assert.AreEqual(32, resolved.y);
        }

        // 手動オフセットは地形解決後に加算される
        // The manual offset is added after the terrain resolution
        [Test]
        public void 手動オフセットが加算される()
        {
            CreateGroundSlab(new Vector3(500.5f, 19.9f, 600.5f), new Vector3(6f, 1f, 6f));

            Assert.IsTrue(TryResolve(new Vector3Int(500, 0, 600), 3, out var resolvedUp));
            Assert.AreEqual(23, resolvedUp.y);

            Assert.IsTrue(TryResolve(new Vector3Int(500, 0, 600), -2, out var resolvedDown));
            Assert.AreEqual(18, resolvedDown.y);
        }

        // 負の高さでも切り捨て規約は変わらない
        // The round-down convention is unchanged for negative heights
        [Test]
        public void 負の高さでも切り捨てる()
        {
            CreateGroundSlab(new Vector3(700.5f, -3.9f, 800.5f), new Vector3(6f, 1f, 6f));

            Assert.IsTrue(TryResolve(new Vector3Int(700, 0, 800), 0, out var resolved));
            Assert.AreEqual(-4, resolved.y);
        }
```

`XZは書き換えない` と `地表が無いセルは失敗を返す` は無変更。

`PlacementGroundFollowStepTest.cs` の期待値も書き換える（`CreateCellSlab` は `scale = Vector3.one` なので地表 = `centerY + 0.5`）。`地面ヒットの横方向列は各セルが地形へ追従する`:

```csharp
            // 最高点→Y: 10/14/18
            // Maxima → Y: 10/14/18
            Assert.AreEqual(10, run.Cells[0].Position.y);
            Assert.AreEqual(14, run.Cells[1].Position.y);
            Assert.AreEqual(18, run.Cells[2].Position.y);
```

`地表の無いセルは設置不可になり他セルは解決される` の1行:

```csharp
            Assert.AreEqual(6, run.Cells[0].Position.y);
```

`ブロック面ヒットの列は追従しない`（`AreEqual(4, ...)`）と `縦積み列は追従しない`（20/21/22）は追従しない経路なので無変更。

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlacementGroundCellApplyTest|PlacementGroundFollowStepTest"`
Expected: FAIL（`Expected: 32 But was: 33` など、丸めの1段差で落ちる）

- [ ] **Step 3: 丸めを切り捨てへ変える**

`PlacementGroundCellResolver.cs` の定数コメントと `ResolveCellY` を置き換える。許容誤差は符号が反転する（整数の地表が誤差で1段**沈む**のを防ぐ側になる）。

```csharp
        // 整数の地表が誤差で1段沈むのを防ぐ
        // Keeps ground exactly on an integer from sinking one cell
        private const float IntegerGroundTolerance = 0.001f;
```

```csharp
        // 地形最高点を含むセルを返す（ADR 0047）
        // Returns the cell containing the terrain max height (ADR 0047)
        private static int ResolveCellY(float groundMaxHeight, int heightOffset)
        {
            return Mathf.FloorToInt(groundMaxHeight + IntegerGroundTolerance) + heightOffset;
        }
```

クラスのXMLコメントも実処理に合わせる。

```csharp
    /// <summary>
    ///     地形の高さから設置セルYを決める（ADR 0047。ADR 0037の「上回る最初のセル」を上書き）
    ///     Decides the placement cell Y from the terrain height (ADR 0047, superseding ADR 0037's "first cell above")
    /// </summary>
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlacementGroundCellApplyTest|PlacementGroundFollowStepTest"`
Expected: PASS（全件）

- [ ] **Step 5: コンパイルを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `"Success": true`, `"ErrorCount": 0`

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Ground/PlacementGroundCellResolver.cs \
        moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/PlacementGroundCellApplyTest.cs \
        moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/PlacementGroundFollowStepTest.cs
git commit -m "fix(place): 設置の既定Yを地形最高点を含むセルにする (ADR 0047)"
```

---

### Task 2: 通常ブロック設置から地形重なりゲートを外す

**Files:**

- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Feedback/PlacementCellReasonReporter.cs:18-58`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/IPlacementPreviewBlockGameObjectController.cs:16`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/PlacementPreviewBlockGameObjectController.cs:24-56`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs:160-167`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Feedback/PlacementCellReasonReporterTest.cs`

**Interfaces:**

- Consumes: `PlacementCursorCellResolver.Resolve(List<PlaceInfo> placeInfos, Vector3Int cursorCell)` → `int`（見つからなければ負値）
- Produces:
  - `PlacementCellReasonReporter.ResolveCursorAndReportCauses(List<PlaceInfo> placeInfos, IReadOnlyList<PlacementBlockCause> cellCauses, Vector3Int cursorCell, PlacementFeedback feedback)` → `int cursorIndex`
  - `PlacementCellReasonReporter.ReportCause(PlacementBlockCause cursorCause, PlacementFeedback feedback)`（internal）
  - `IPlacementPreviewBlockGameObjectController.SetPreview(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster)` → `void`
  - `ApplyGroundOverlapsAndReport` と `Report` は既存シグネチャのまま残す（ベルト・レールと既存テストが使う）

- [ ] **Step 1: 失敗するテストを書く**

`PlacementCellReasonReporterTest.cs` に、地形を見ない入口のテストを2本追加する。

既存の `BuildDragCells(int cellCount)`（同ファイル `:109-114`。`new PlaceInfo { Position = new Vector3Int(i, 0, 0), Placeable = true }` を並べる）をそのまま使う。

```csharp
        // 地形を見ない入口は地形の理由を積まず、どのセルも落とさない
        // The terrain-blind entry pushes no terrain line and drops no cell
        [Test]
        public void 地形を見ない入口は地形の理由を積まない()
        {
            var placeInfos = BuildDragCells(3);
            var cellCauses = new List<PlacementBlockCause> { PlacementBlockCause.None, PlacementBlockCause.None, PlacementBlockCause.None };
            var feedback = new PlacementFeedback();

            var cursorIndex = PlacementCellReasonReporter.ResolveCursorAndReportCauses(placeInfos, cellCauses, new Vector3Int(1, 0, 0), feedback);

            Assert.AreEqual(1, cursorIndex);
            Assert.IsTrue(placeInfos[0].Placeable);
            Assert.IsTrue(placeInfos[1].Placeable);
            Assert.IsTrue(placeInfos[2].Placeable);
            Assert.IsEmpty(feedback.Lines);
        }

        // 地形以外の共有原因は地形を見ない入口でも積まれる
        // Non-terrain shared causes are still reported by the terrain-blind entry
        [Test]
        public void 地形を見ない入口でも既存ブロックの理由は積む()
        {
            var placeInfos = BuildDragCells(3);
            var cellCauses = new List<PlacementBlockCause> { PlacementBlockCause.None, PlacementBlockCause.ExistingBlock, PlacementBlockCause.None };
            var feedback = new PlacementFeedback();

            PlacementCellReasonReporter.ResolveCursorAndReportCauses(placeInfos, cellCauses, new Vector3Int(1, 0, 0), feedback);

            Assert.AreEqual(1, feedback.Lines.Count);
            Assert.AreEqual(LocalizationKeys.Ui.Tooltip.PlaceBlockedByExistingBlock.Key, feedback.Lines[0].Key.Key);
        }
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlacementCellReasonReporterTest"`
Expected: FAIL（`ResolveCursorAndReportCauses` が存在せずコンパイルエラー）

- [ ] **Step 3: 地形を見ない入口を足す**

`PlacementCellReasonReporter.cs` の `Report` から原因スイッチを `ReportCause` へ切り出し、新しい入口を足す。`Report` と `ApplyGroundOverlapsAndReport` のシグネチャは変えない。

```csharp
        internal static void Report(int cursorIndex, PlacementBlockCause cursorCause, IReadOnlyList<bool> groundOverlaps, PlacementFeedback feedback)
        {
            if (cursorIndex < 0) return;
            if (groundOverlaps[cursorIndex]) feedback.AddBlockedByTerrain();

            ReportCause(cursorCause, feedback);
        }

        // 地形以外の共有原因だけを積む。地形を見る入口と見ない入口で共用する
        // Pushes only the non-terrain shared causes, shared by the terrain-aware and terrain-blind entries
        internal static void ReportCause(PlacementBlockCause cursorCause, PlacementFeedback feedback)
        {
            // 原因を取り違えると空セルに「埋まっています」と誤案内するため、原因ごとに文言を分ける
            // Confusing the causes would mis-report "occupied" on an empty cell, so each cause gets its own wording
            switch (cursorCause)
            {
                case PlacementBlockCause.None:
                    break;
                case PlacementBlockCause.ExistingBlock:
                    feedback.AddBlockedByExistingBlock();
                    break;
                case PlacementBlockCause.GroundNotFound:
                    feedback.AddGroundNotFound();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(cursorCause), cursorCause, null);
            }
        }

        // 地形の重なりを設置不可の理由にしない入口（ADR 0047）。通常設置は地形へ食い込む前提のためこちらを使う
        // The entry that never blocks on terrain overlap (ADR 0047); normal placement digs into the terrain by design
        public static int ResolveCursorAndReportCauses(List<PlaceInfo> placeInfos, IReadOnlyList<PlacementBlockCause> cellCauses, Vector3Int cursorCell, PlacementFeedback feedback)
        {
            var cursorIndex = PlacementCursorCellResolver.Resolve(placeInfos, cursorCell);
            if (cursorIndex < 0) return cursorIndex;

            ReportCause(cellCauses[cursorIndex], feedback);
            return cursorIndex;
        }
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "PlacementCellReasonReporterTest"`
Expected: PASS（既存テストも含め全件）

- [ ] **Step 5: プレビュー配置と地形検出を分ける**

`IPlacementPreviewBlockGameObjectController.cs` に `SetPreview` を足す。

```csharp
        /// <summary>
        /// プレビューブロックを配置する。地形との接触は見ない
        /// Places the preview blocks without looking at terrain contact
        /// </summary>
        public void SetPreview(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster);

        public List<bool> SetPreviewAndGroundDetect(List<PlaceInfo> currentPlaceInfos, BlockMasterElement holdingBlockMaster);
```

`PlacementPreviewBlockGameObjectController.cs` の `SetPreviewAndGroundDetect` を2つへ割る。

```csharp
        public void SetPreview(List<PlaceInfo> placePointInfos, BlockMasterElement holdingBlockMaster)
        {
            // さっきと違うブロックだったら削除する
            // Destroy the pooled previews when the held block changed
            if (_previewBlockMasterElement == null || _previewBlockMasterElement.BlockGuid != holdingBlockMaster.BlockGuid)
            {
                _previewBlockMasterElement = holdingBlockMaster;
                _blockPlacePreviewObjectPool.AllDestroy();
            }

            _blockPlacePreviewObjectPool.AllUnUse();
            _activePreviewBlocks.Clear();

            // プレビューブロックの位置を設定
            // Set preview block positions
            foreach (var placeInfo in placePointInfos)
            {
                var blockId = placeInfo.BlockId;

                var pos = SlopeBlockPlaceSystem.GetBlockPositionToPlacePosition(placeInfo.Position, placeInfo.Direction, blockId);
                var rot = placeInfo.Direction.GetRotation();

                var previewBlock = _blockPlacePreviewObjectPool.GetObject(blockId);
                _activePreviewBlocks.Add(previewBlock);
                previewBlock.SetTransform(pos, rot);

                previewBlock.SetPlaceableColor(placeInfo.Placeable);
                previewBlock.SetPreviewStateDetail(placeInfo);
            }
        }

        public List<bool> SetPreviewAndGroundDetect(List<PlaceInfo> placePointInfos, BlockMasterElement holdingBlockMaster)
        {
            SetPreview(placePointInfos, holdingBlockMaster);

            // 地形接触を見る系統だけが初期色にも接触を織り込む
            // Only the terrain-aware systems fold contact into the initial color as well
            var isGroundDetectedList = new List<bool>();
            for (var i = 0; i < _activePreviewBlocks.Count; i++)
            {
                var isGroundDetected = _activePreviewBlocks[i].IsCollisionGround;
                isGroundDetectedList.Add(isGroundDetected);
                _activePreviewBlocks[i].SetPlaceableColor(!isGroundDetected && placePointInfos[i].Placeable);
            }

            return isGroundDetectedList;
        }
```

- [ ] **Step 6: 通常設置を地形を見ない入口へ差し替える**

`CommonBlockPlaceSystem.cs:160-167` を置き換える。

```csharp
                //プレビューを表示する。通常設置では地形との重なりを設置不可の理由にしない（ADR 0047）
                //Display the preview; normal placement never blocks on terrain overlap (ADR 0047)
                _previewBlockController.SetPreview(_currentPlaceInfos, holdingBlockMaster);

                // この時点の不可原因は既存ブロックと地表欠落のみ。カーソルセルの理由集約だけを行う
                // Existing blocks and missing ground are the only causes set by this point; only the cursor cell's reasons are reported
                var cursorIndex = PlacementCellReasonReporter.ResolveCursorAndReportCauses(_currentPlaceInfos, placeCauses, placePoint, feedback);
```

`blockGroundOverlapList` は使われなくなるので変数ごと削除する。下段の「地面フィルタ後にアイテム数チェック」コメントは実態と合わなくなるため、`ConstructionMaterialShortageReporter.ReportShortages` 直上のコメントを次へ差し替える。

```csharp
                // 鉱脈・既存ブロックで落ちたセルがアイテム枠を消費しないよう、フィルタ後にチェックする
                // Check after filtering so cells dropped by veins or existing blocks don't consume item quota
```

- [ ] **Step 7: コンパイルを確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `"Success": true`, `"ErrorCount": 0`

`IPlacementPreviewBlockGameObjectController` の実装は `PlacementPreviewBlockGameObjectController` 1つだけで、テスト用フェイクは存在しない（`Client.Tests` / `Client.Playtest` に参照なし）。インターフェースへメンバーを足しても他の実装は壊れない。

- [ ] **Step 8: 設置系のテストをまとめて実行する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client.Tests.PlaceSystem"`
Expected: PASS（全件。ベルト・レール・電柱・歯車ポールの既存テストが無変更で通ること＝R6）

- [ ] **Step 9: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Feedback/PlacementCellReasonReporter.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/IPlacementPreviewBlockGameObjectController.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewController/PlacementPreviewBlockGameObjectController.cs \
        moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs \
        moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Feedback/PlacementCellReasonReporterTest.cs
git commit -m "fix(place): 通常ブロック設置から地形重なりゲートを外す (ADR 0047)"
```

---

### Task 3: 実プレイで見た目を確認する

**Files:**

- Create: なし（検証のみ。録画とログは成果物としてPR本文へ添付する）

**Interfaces:**

- Consumes: Task 1・Task 2 の変更が入ったブランチ

- [ ] **Step 1: プレイ録画テストで設置の見た目を確認する**

`unity-playmode-recorded-playtest` スキルを起動し、プレイテストDSLで次を通す。

1. 自動生成マップでゲーム開始
2. 平地でブロックを1つ設置し、地面と面が揃っていることを録画で確認
3. 斜面でブロックを1つ設置し、最高角が沈み低い側の隙間が縮んでいることを確認
4. 斜面を横断するドラッグ設置を行い、階段状追従が保たれていることを確認
5. 崖際でカーソルを地形へ寄せ、「地形に埋まっています」が出ず設置できることを確認

Expected: 手動オフセット0の状態が、変更前に `-1` を押した時と同じ見た目になる（R1・R3の受け入れ）

- [ ] **Step 2: エラーログが出ていないことを確認する**

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: 本変更に由来するエラーが0件

- [ ] **Step 3: 確認結果を残す**

録画から静止画を1〜2枚切り出し、PR本文へ「平地」「斜面」の比較として添付する。リポジトリへ追加するファイルが無ければコミットは作らない（空コミットは作らない）。

---

### Task 4: 全ブランチレビュー（省略不可）

**Files:**

- Modify: レビュー指摘に応じた修正のみ

- [ ] **Step 1: moores-code-review を実行する**

`moores-code-review` スキルを起動し、`master` からの全ブランチ差分をレビューする。**このタスクは自動実行であり、ゴール文言による省略はできない。**

- [ ] **Step 2: 指摘を反映しコンパイルとテストを再実行する**

Run: `uloop compile --project-path ./moorestech_client`
Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client.Tests.PlaceSystem"`
Expected: いずれも PASS

- [ ] **Step 3: タスク台帳を閉じてコミットする**

`moorestech-41zp` を「ADR 0047 実装完了（切り上げ→切り捨て＋通常設置の地形ゲート撤去）」の理由で close する。

```bash
git add -A
git commit -m "fix(place): レビュー指摘を反映する"
```

---

## 判断記録（ADR）

- 正本: `docs/adr/0047-placement-default-height-floor-of-terrain-max.md`（ADR 0037 の「上回る最初のセル」決定を上書き）
- 裁定記録: `.decisions/2026-08-30-ブロック設置の既定高さは地形最高点を含むセルにする.md`

planning 中に生じた判断:

- **許容誤差の符号を反転する**（`CeilToInt(h - tol)` → `FloorToInt(h + tol)`）。切り捨てへ変えると、地表がちょうど整数のとき浮動小数の誤差（31.9999998 等）で1段沈みうる。R2 を守るには誤差を上向きに足す必要がある。出所: agent前提（既存 `IntegerGroundTolerance` の意図「整数の地表で1段ずれない」を切り捨て側へ移した）
- **`PlacementCellReasonReporter` に第2の入口を足す**（呼び出し側で全 false の `groundOverlaps` を合成しない）。合成は共有ヘルパへ嘘のデータを渡す形で意図が読めず、「地形を見るかどうか」の判断を具体側へ置く AGENTS.md の方針にも反する。出所: agent前提（AGENTS.md「判断は具体側で行い、基盤には `SetHoge(値)` でプッシュする」）
- **`SetPreview` を新設して戻り値を捨てる呼び出しを作らない**。`SetPreviewAndGroundDetect` の戻り値を通常設置で無視すると、地形検出が残っているのか外したのか読めなくなる。出所: agent前提（前例なし・新規パターンとしてレビュー注目点に挙げる）
- **`Report` / `ApplyGroundOverlapsAndReport` のシグネチャは維持する**。ベルト・レールに加え `PlacementCellReasonReporterTest` が `Report` を直接呼んでおり、変えると本件と無関係な差分が増える。出所: agent前提（既存テストの直接呼び出し `PlacementCellReasonReporterTest.cs:21,33,48,53,66,77`）

**レビュー注目点（前例のない新規パターン）**

- `IPlacementPreviewBlockGameObjectController` に `SetPreview` を足し、プレビュー配置と地形検出を分離した点。同種の分割の前例はコードベースに無い
