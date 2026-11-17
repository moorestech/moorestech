# 設置照準を占有セルの走査に置き換える Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** ブロックごとの設置照準の当たり判定 `BlockPlacementHitFaces`（GameObject 1個＋BoxCollider 6枚）を廃止し、占有セルの索引を照準レイに沿ってたどってブロックの面を求める。前後の読み込み時間・設置モードの出入り・GameObject 数を測って記録する。

**Architecture:** `BlockGameObjectDataStore` が占有セル→`BlockPositionInfo` の索引 `BlockOccupiedCellIndex` を辞書と同時に更新する。`PlacementAimCellTracer` がレイと索引から最初に入る占有セルの面（3D DDA）を返す。`PlaceSystemUtil.TryRaycastPlacementSurface` は物理レイ（地面など）の距離を上限に走査し、近い方を採る。走査は設置モード中だけ有効で、`PlaceBlockPreviewDisplay.OnEnter/OnExit` が静的な `PlacementAimTargetCells` に索引を渡す・外す。

**Tech Stack:** Unity 6000.3.8f1 / C# / NUnit（EditMode・EditModeInPlayingTest）/ uloop

**Spec:** `docs/adr/0100-placement-aim-hits-occupied-cells-by-traversal.md`（ADR 0099 決定2 を置き換える）

## Global Constraints

- AGENTS.md の規約をすべて守る（コメントは日本語1行＋英語1行の2行セット、1ファイル200行以下、partial・`Func<>`・デフォルト引数・try-catch 禁止、単純 getter/setter プロパティ禁止で `{ get; private set; }` は可、`#region Internal` はメソッド内ローカル関数だけ、fail-closed の経路は理由をログへ出すか `silent-ok:` コメントで理由を書く）。
- 照準の当たり方は占有マスの直方体の面（`BlockPositionInfo.MinPos`〜`MaxPos + 1`）。ブロック本体の形には当たらない（ADR 0100 決定2）。
- 走査は設置モード中（`PlaceBlockPreviewDisplay.OnEnter`〜`OnExit`）だけ。設置モードの外（削除ツールの箱選択）は今と同じく物理レイの結果だけで決める（決定6、ユーザー裁定）。
- 距離の上限は置かない。走査範囲は「物理レイの当たった距離まで」かつ「索引の外接直方体の中」（決定4）。
- 辺・角を同時にまたぐときは Y → X → Z の順に先の軸の面とする（決定5）。
- `BlockBoundingBox` レイヤー・`LayerConst` の定数とマスク・`RuntimeOutlineFactory` の除外は残す（辺のプレハブが使う。決定7）。
- `IsOverlapPositionInfo`・`GetOccupancy` は変えない（決定8）。
- `.cs` を変えたら必ず `uloop compile --project-path ./moorestech_client` を通す（ErrorCount 0）。`.meta` は手で作らない（削除するファイルの `.meta` は一緒に消す）。
- コミットはプレーンな `git commit`。`--author`・`Co-Authored-By` などの trailer を付けない。
- 計測は windows-desktop（i7-12700／RTX 3060 Ti、`G:\GitHub\moorestech`）で、ベースラインワールド `2026-10-22-world_1` を使う。変更前と変更後は同じ日に続けて測る。テスト・録画テストで `Saves/` に作ったワールドは作業の終わりに `list-work-saves.sh` で探して消す。

## Review Focus

1. カメラが占有セルの内側にある（三人称カメラが壁にめり込む）→ そのブロックの出る面に当たる。Task 2 の `InsideStart_HitsExitFace`。
2. レイがセルの辺・角をちょうど通る → Y → X → Z の順で決まる。Task 2 の `EdgeGraze_PrefersY`。
3. 軸に平行なレイ（方向成分 0）→ ゼロ除算せず当たる。Task 2 の6方向テストがすべて軸平行で通す。
4. 撤去中（破棄アニメーション中）のブロック → `RemoveBlock` 直後から当たらない。Task 3 の `PlaceBlockPreviewDisplayTest` に撤去直後の索引確認を足す。
5. 別サイズのブロックへの置き換え・重なる登録 → 古いセルが残らない／他ブロックのセルを消さない。Task 1 の `Remove_OnlyClearsCellsOwnedByThatBlock`・`ReRegisterSmaller_LeavesNoStaleCells`。

---

## File Structure

| ファイル | 種別 | 責務 |
|---|---|---|
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/Index/BlockOccupiedCellIndex.cs` | Create | 占有セル→`BlockPositionInfo` の索引と、登録されたセルの外接直方体（拡大のみ） |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObjectDataStore.cs` | Modify | 索引を持ち、設置・撤去・置き換えで更新する |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlacementAimCellTracer.cs` | Create | レイと索引から、最初に入る占有セルの面と距離を返す |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlacementAimTargetCells.cs` | Create | 設置モード中だけ照準が引く索引（静的） |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlaceSystemUtil.cs` | Modify | `TryRaycastPlacementSurface` を物理レイ＋走査へ |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PlaceBlock/PlaceBlockPreviewDisplay.cs` | Modify | `OnEnter`/`OnExit` で索引を照準へ渡す・外す |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/PreviewObject/BlockPlacementHitFaces.cs` | Delete | 旧当たり判定 |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObject.cs` | Modify | `BlockPlacementHitFaces.Create` の呼び出し（89〜91行）を消す |
| `moorestech_client/Assets/Scripts/Client.Game/InGame/ColliderStreaming/Block/BlockColliderCullingRegisterService.cs` | Modify | `BlockBoundingBox` レイヤーの除外（30〜32行）を消し、全コライダーを対象に戻す |
| `moorestech_client/Assets/Scripts/Client.Tests/BlockPreview/BlockOccupiedCellIndexTest.cs` | Create | 索引（EditMode） |
| `moorestech_client/Assets/Scripts/Client.Tests/BlockPreview/PlacementAimCellTracerTest.cs` | Create | 走査（EditMode） |
| `moorestech_client/Assets/Scripts/Client.Tests/BlockPreview/BlockPlacementHitFacesTest.cs`・`BlockColliderCullingExcludesHitFacesTest.cs` | Delete | 旧当たり判定のテスト |
| `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/Ground/Entry/FootprintGroundedEntryFixture.cs` | Modify | `CreateFace` を索引への登録に替える |
| `moorestech_client/Assets/Scripts/Client.Tests/BlockPreview/PlaceBlockPreviewDisplayTest.cs` | Modify | 当たり判定の確認を、索引と照準の有効期間の確認に替える |
| `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/BlockInventory/SpaceElevator/SpaceElevatorConnectActionTest.cs` | Modify | 当たり判定の存在確認を消す |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/build-preview-radius.cs` | Modify | 当たり判定の待ちをブロックの存在待ちに替える |
| `docs/performance/benchmark/snippets/placement-aim-trace-cost.cs` | Create | 照準1本の最悪時間の計測 |
| `docs/performance/2026-11-12-placement-aim-cell-traversal/README.md` | Create | 前後比較の記録 |

---

### Task 1: 占有セルの索引 `BlockOccupiedCellIndex`

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/Index/BlockOccupiedCellIndex.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Block/BlockGameObjectDataStore.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BlockPreview/BlockOccupiedCellIndexTest.cs`

**Interfaces:**
- Produces（namespace `Client.Game.InGame.Block.Index`、`public sealed class BlockOccupiedCellIndex`）:
  - `public void Register(BlockPositionInfo posInfo)` — `MinPos`〜`MaxPos` の全セルをその `posInfo` に結び、外接直方体を広げる
  - `public void Remove(BlockPositionInfo posInfo)` — その `posInfo`（参照が同じもの）に結ばれたセルだけを外す。外接直方体は縮めない
  - `public bool TryGetBlockAt(Vector3Int cell, out BlockPositionInfo posInfo)`
  - `public bool TryGetBounds(out Vector3Int minCell, out Vector3Int maxCell)` — 一度も登録が無ければ false
- Produces（`BlockGameObjectDataStore`）: `public BlockOccupiedCellIndex OccupiedCells { get; private set; } = new();`

- [ ] **Step 1: テストを書く**（`BlockOccupiedCellIndexTest`、EditMode、`new BlockPositionInfo(origin, direction, size)` で作る）
  - `Register_MapsEveryFootprintCell`: origin (10,5,20)・North・size (2,1,3) を登録 → (10,5,20)〜(11,5,22) の6セルすべてで `TryGetBlockAt` が true かつ `AreSame(posInfo)`、(12,5,20) と (10,6,20) は false。East で同じ origin・size → `posInfo.MinPos`〜`MaxPos` の全セルが true。
  - `Remove_OnlyClearsCellsOwnedByThatBlock`: A=(0,0,0)/size(2,1,1) を登録 → 別インスタンス B=(1,0,0)/size(1,1,1) を `Remove` → (1,0,0) は A のまま。A を `Remove` → 両セル false。
  - `ReRegisterSmaller_LeavesNoStaleCells`: 大 (0,0,0)/size(3,3,3) 登録 → 大を `Remove` → 小 (0,0,0)/size(1,1,1) 登録 → (2,2,2) は false、(0,0,0) は小。
  - `Bounds_GrowOnlyAndFalseWhenEmpty`: 空で `TryGetBounds` false。(0,0,0)/1 と (5,2,-3)/1 を登録 → min (0,0,-3)・max (5,2,0)。(5,2,-3) を `Remove` しても同じ値。

- [ ] **Step 2: 失敗を確認**
  Run: `uloop compile --project-path ./moorestech_client`
  Expected: `BlockOccupiedCellIndex` が無いコンパイルエラー

- [ ] **Step 3: `BlockOccupiedCellIndex` を実装する**
  `Dictionary<Vector3Int, BlockPositionInfo>` と外接直方体の min/max・登録有無フラグ。`Remove` は `ReferenceEquals` で一致するセルだけ消す。

- [ ] **Step 4: `BlockGameObjectDataStore` に組み込む**
  - `PlaceBlock` の新規設置の後（`_blockTypeIndex.Register(block)` の隣）で `OccupiedCells.Register(block.BlockPosInfo)`。
  - 置き換え経路（`Destroy(oldBlock.gameObject)` の前後）と `RemoveBlock` で `OccupiedCells.Remove(block.BlockPosInfo)`。辞書から消すのと同じ瞬間に行う（撤去アニメ中は当たらない。ADR 0100 決定3）。
  - 今193行なので200行を超える。置き換え経路と `RemoveBlock` に重複している「InstanceId 辞書から外す・`DropFromNearestIndex`・`_blockTypeIndex.Remove`」を、索引の `Remove` も含めた1つの private メソッド（例 `ForgetFromIndexes(BlockGameObject block)`）へまとめて200行以下に収める。

- [ ] **Step 5: テストを通す**
  Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.BlockOccupiedCellIndexTest"`
  Expected: 全件 PASS

- [ ] **Step 6: コミット**
  `git commit -m "feat(block): 占有セルからブロックを引く索引を、ブロック置き場の辞書と同時に更新する（ADR 0100）"`

---

### Task 2: 占有セルの走査 `PlacementAimCellTracer`

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlacementAimCellTracer.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BlockPreview/PlacementAimCellTracerTest.cs`

**Interfaces:**
- Consumes: Task 1 の `BlockOccupiedCellIndex.TryGetBlockAt`・`TryGetBounds`、既存の `PreviewSurfaceType`
- Produces: `public static class PlacementAimCellTracer`（namespace `Client.Game.InGame.BlockSystem.PlaceSystem.Util`）
  - `public static bool TryTrace(BlockOccupiedCellIndex cells, Ray ray, float maxDistance, out float distance, out PreviewSurfaceType surfaceType)` — `ray.direction` は正規化済み前提。`distance` はレイ上の距離（`ray.GetPoint(distance)` が当たった位置）

面の対応（入る向き → 面）: +X へ進んで入る＝`YZ_Origin`、−X へ＝`YZ_X`、+Y へ＝`XZ_Origin`、−Y へ＝`XZ_Y`、+Z へ＝`YX_Origin`、−Z へ＝`YX_Z`。始点が内側のときは出る面: +X へ出る＝`YZ_X`、−X＝`YZ_Origin`、+Y＝`XZ_Y`、−Y＝`XZ_Origin`、+Z＝`YX_Z`、−Z＝`YX_Origin`（`PlaceSystemUtil.CalcPlacePointBySize` の対応と同じ）。

- [ ] **Step 1: テストを書く**（ブロック origin (10,5,20)・size (2,1,3)。箱は `MinPos`〜`MaxPos + 1`、中心 c）
  - `[TestCase(North)][TestCase(East)] OutsideRays_ResolveWorldFaces`: c から各軸 ±20 の点から c へ撃つ → +X 側から `YZ_X`、−X 側から `YZ_Origin`、上から `XZ_Y`、下から `XZ_Origin`、+Z 側から `YX_Z`、−Z 側から `YX_Origin`。上からのとき `distance` は `(c.y + 20) - (MaxPos.y + 1)` に 1e-4 以内で一致。
  - `InsideStart_HitsExitFace`: c から +X へ → `YZ_X`、`distance` = `MaxPos.x + 1 - c.x`。
  - `MaxDistanceBeforeBlock_Misses`: 上から撃ち、`maxDistance` を箱上面までの距離 −0.01 にする → false。
  - `NearerBlockWins`: 同じ X 列に (10,5,20) と (20,5,20) の1マスブロック。(30.5,5.5,20.5) から −X へ → `distance` = 30.5 − 21。
  - `EdgeGraze_PrefersY`: 1マスブロック (0,0,0)。始点 (3,3,0.5)、方向 (−1,−1,0) 正規化（点 (1,1,0.5) で上面と +X 面の辺を通る）→ `XZ_Y`。
  - `MissOrEmpty_ReturnsFalse`: 空の索引で false。箱から外れたレイ（上空を水平に）で false。
  - `NoMaxDistance_StopsAtBounds`: `maxDistance = float.PositiveInfinity` で箱から外れた水平レイが false を返して終わる（無限ループしない）。

- [ ] **Step 2: 失敗を確認**
  Run: `uloop compile --project-path ./moorestech_client`
  Expected: `PlacementAimCellTracer` が無いコンパイルエラー

- [ ] **Step 3: `TryTrace` を実装する**
  1. `TryGetBounds` で外接直方体（ワールドでは `min`〜`max + 1`）を得て、スラブ法でレイとの区間 [tEnter, tExit] を求める。外れれば false。`tEnd = min(tExit, maxDistance)`。
  2. 始点のセル（`Vector3Int.FloorToInt(ray.origin)`）が占有されていれば、そのブロックの箱から出る距離と軸をスラブ法で求め、`tEnd` 以下なら出る面を返す。
  3. そうでなければ `max(0, tEnter)` の点から 3D DDA（Amanatides–Woo）でセルを進む。方向成分 0 の軸は次の境界までの距離を無限大にする。外接直方体の外から入ったときの最初のセルの入る軸は tEnter を決めた軸。次に進む軸は `tMax` が最小の軸で、同値は Y → X → Z を優先する。入ったセルが占有されていれば、そのときの t と入る向きの面を返す。t が `tEnd` を超えたら false。

- [ ] **Step 4: テストを通す**
  Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.PlacementAimCellTracerTest"`
  Expected: 全件 PASS

- [ ] **Step 5: コミット**
  `git commit -m "feat(place): 照準レイに沿って占有セルをたどり、最初に入るブロックの面を求める（ADR 0100）"`

---

### Task 3: 照準を走査へ切り替え、当たり判定を廃止する

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Util/PlacementAimTargetCells.cs`
- Modify: `PlaceSystemUtil.cs`（`TryGetRayHitPlacePointBySize`・`TryGetRayHitPosition`・`TryRaycastPlacementSurface`）、`PlaceBlockPreviewDisplay.cs`、`BlockGameObject.cs:89-91`、`BlockColliderCullingRegisterService.cs:30-32`
- Delete: `BlockPlacementHitFaces.cs`、`Client.Tests/BlockPreview/BlockPlacementHitFacesTest.cs`、`Client.Tests/BlockPreview/BlockColliderCullingExcludesHitFacesTest.cs`（各 `.meta` も）
- Test（Modify）: `FootprintGroundedEntryFixture.cs`、`PlaceBlockPreviewDisplayTest.cs`、`SpaceElevatorConnectActionTest.cs`

**Interfaces:**
- Consumes: Task 1 の `BlockGameObjectDataStore.OccupiedCells`、Task 2 の `PlacementAimCellTracer.TryTrace`
- Produces: `public static class PlacementAimTargetCells`（namespace `Client.Game.InGame.BlockSystem.PlaceSystem.Util`）
  - `public static BlockOccupiedCellIndex Current { get; private set; }` — 設置モード外は null
  - `public static void Enable(BlockOccupiedCellIndex cells)` / `public static void Disable()`
- `PlaceSystemUtil`: `internal static bool TryRaycastPlacementSurface(Camera mainCamera, out Vector3 hitPoint, out PreviewSurfaceType? surfaceType)`（`RaycastHit` の出力をやめる。public の入口のシグネチャは変えない）

- [ ] **Step 1: テストを書き換える**
  - `FootprintGroundedEntryFixture.CreateFace()`: GameObject と `BlockPlacementHitFaces.Create` をやめ、`Blocks.OccupiedCells.Register(new BlockPositionInfo(new Vector3Int(496, 15, 595), BlockDirection.North, new Vector3Int(10, 10, 10)))` と `PlacementAimTargetCells.Enable(Blocks.OccupiedCells)` にする（上面 y=25、カメラ (501.5,40,600.5) の真下。地面 20.2 より手前）。戻り値は `XZ_Y` のまま。`FootprintGroundedEntryFixture` で `public override void TearDown()` を足し、`PlacementAimTargetCells.Disable()` の後に `base.TearDown()` を呼ぶ。`FootprintGroundedRayEntryTest` の期待値は変えない。
  - `PlaceBlockPreviewDisplayTest`: `AssertPreview` の当たり判定の確認を消し、次を足す。`display.OnEnter()` の直後に `Assert.AreSame(store.OccupiedCells, PlacementAimTargetCells.Current)`、各 `OnExit()` の直後に `Assert.IsNull(PlacementAimTargetCells.Current)`。半径外の `far` について `store.OccupiedCells.TryGetBlockAt(farPosition, out _)` が true（半径外にも当たる。ADR 0083 決定4）。最後に `store.RemoveBlock(newFarPosition)` の直後に `TryGetBlockAt(newFarPosition, out _)` が false（撤去アニメ中は当たらない）。
  - `SpaceElevatorConnectActionTest`: `BlockPlacementHitFaces` の `Assert.IsNotNull` と、`Assert.Greater(colliders.Length, 1, …)` を消す（当たり判定のコライダーを含めるための確認だった）。

- [ ] **Step 2: 失敗を確認**
  Run: `uloop compile --project-path ./moorestech_client`
  Expected: `PlacementAimTargetCells` が無いコンパイルエラー

- [ ] **Step 3: `PlacementAimTargetCells` を作り、`PlaceBlockPreviewDisplay.OnEnter` の先頭で `Enable(_blockGameObjectDataStore.OccupiedCells)`、`OnExit` の先頭で `Disable()` を呼ぶ**

- [ ] **Step 4: `TryRaycastPlacementSurface` を書き換える**
  1. 今と同じ `ScreenPointToRay(AimPointProvider.GetAimScreenPoint())` と `Physics.Raycast(…, LayerConst.Without_Player_MapObject_Block_LayerMask)`。当たれば `hit.distance`、当たらなければ `float.PositiveInfinity` を上限にする。
  2. `PlacementAimTargetCells.Current` が null でなく `PlacementAimCellTracer.TryTrace` が true なら、`hitPoint = ray.GetPoint(distance)`・`surfaceType = 面` で true。
  3. それ以外は今と同じ（物理レイが無ければ false、`GroundGameObject` なら `surfaceType = null` で true、他は false）。`BlockPlacementHitFaces` の分岐は消す。
  呼び出し側の `TryGetRayHitPlacePointBySize`・`TryGetRayHitPosition` は `hit.point` を `hitPoint` に替える。

- [ ] **Step 5: 当たり判定を消す**
  `BlockPlacementHitFaces.cs`(+meta) と旧テスト2本(+meta) を削除。`BlockGameObject.cs:89-91` の生成とコメントを削除。`BlockColliderCullingRegisterService.CreateEntry` を `block.GetComponentsInChildren<Collider>(true)` 全件に戻し、除外のコメントと不要になった using を消す。`grep -rn "BlockPlacementHitFaces" moorestech_client/Assets .agents docs/performance/benchmark/snippets` の結果が `placement-frame-count.cs`（型名の文字列）と Task 4 で直すシナリオだけになることを確かめる。

- [ ] **Step 6: テストを通す**
  Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BlockOccupiedCellIndexTest|PlacementAimCellTracerTest|FootprintGrounded|DeleteBoxInput|BlockPreviewOnlyObjectsViewTest|PlaceBlockPreviewDisplayTest|SpaceElevatorConnectActionTest|BlockClickColliderTest" --timeout-seconds 1500`
  Expected: 全件 PASS。`uloop get-logs --project-path ./moorestech_client --log-type Error` に新しいエラーが無い

- [ ] **Step 7: コミット**
  `git commit -m "perf(place): 設置照準を占有セルの走査へ切り替え、ブロックごとの当たり判定を廃止する（ADR 0100）"`

---

### Task 4: 録画テストと前後計測

**Files:**
- Modify: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/build-preview-radius.cs`
- Create: `docs/performance/benchmark/snippets/placement-aim-trace-cost.cs`、`docs/performance/2026-11-12-placement-aim-cell-traversal/README.md`
- Modify: `docs/performance/README.md`（一覧への1行）

**Interfaces:**
- Consumes: Task 1〜3 の全部

- [ ] **Step 1: 録画シナリオを直す**
  `HitFacesAt(pos) != null` の待ち（3か所）を `ClientDIContext.BlockGameObjectDataStore.GetBlockGameObject(pos) != null` に替え、`HitFacesAt` を消す。「半径外のチェスト上面へ照準して設置」の手順と期待（`farTarget + Vector3Int.up` に設置できる）は変えない。コメントの「面コライダー経由」を「占有セルの走査で」に直す。

- [ ] **Step 2: 録画シナリオを流す**
  unity-playmode-recorded-playtest スキルの手順で `building/build-preview-radius.cs` を実行。
  Expected: すべての `p.Assert` が通り、`03-place-on-far-block` のスクショで半径外ブロックの上に設置されている

- [ ] **Step 3: 照準1本の最悪時間のスニペットを書く**
  `placement-aim-trace-cost.cs`（`uloop execute-dynamic-code` 用）: `ClientDIContext.BlockGameObjectDataStore.OccupiedCells` の外接直方体を `TryGetBounds` で取り、その X 方向の端の外 1 マスから中心の高さ・中心の Z で +X へ水平に撃つレイと、Z 方向に同様のレイを、それぞれ1000回 `PlacementAimCellTracer.TryTrace(cells, ray, float.PositiveInfinity, …)` して1本あたりの平均 ms と、たどったセル数の目安（外接直方体の辺の長さ）を JSON で返す。

- [ ] **Step 4: 前後を測る**（windows-desktop、同じ日に変更前→変更後）
  変更前 = master 110ccc5674、変更後 = このブランチ。手順は `docs/performance/2026-11-12-placement-preview-frame/README.md` の「計測方法」と同じ（`load-measure.sh timeline`・`load-measure.sh subtree`・`run-benchmark.sh <label>`・`run-buildmode-entry.sh <label>`）。変更後だけ Step 3 のスニペットを定常後に実行する。
  Expected: 照準1本の平均が 0.1ms 以下（ADR 0100 決定9）。超えたら照準の結果を変えずに速くする手当て（空チャンクを飛ばす2段の索引）を別タスクとして起こし、ここで止めてユーザーへ報告する

- [ ] **Step 5: 記録を書く**
  `README.md` に前後の表（Transform 総数・`Collider` 数・`BlockPlacementHitFaces` 数、ブロック生成フレームの ms と `AddComponent` 回数、開始→定常の秒、設置モードに入る遷移の処理時間 1回目／2回目以降と `ActivateAwakeRecursively`、出る遷移、定常 fps、照準1本の平均 ms）と、計測環境・生データへのリンク。`docs/performance/README.md` の一覧へ1行足す。

- [ ] **Step 6: 後片付けとコミット**
  `list-work-saves.sh` でテスト・録画で作ったワールドを探して消す。
  `git commit -m "docs(perf): 設置照準を占有セルの走査に替えた前後で、読み込み・設置モードの出入り・GameObject 数・照準1本の時間を測って記録する"`
