# ベルト経路の出力口合流・直線限定・端点の坂 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ベルトの2クリック経路設置を、(1) 直線ベルト手持ちだけに限り、(2) 既存の出力口の前を避けず合流させ、(3) 出力口の面を狙ったらブロックから離れる向きにし、(4) 終点が下り坂になれるようにする。

**Architecture:** 変更はクライアントの設置システム（`Client.Game/InGame/BlockSystem/PlaceSystem/`）とそのテストだけ。サーバーの接続規則・プロトコル・マスタ・セーブ形式は変えない。探索の可否判定は今までどおり `BeltRouteTransitionRule` の1枚に集め、探索（`BeltRouteSearch`）・組み立て（`BeltRouteRecovery`）・テストの参照ソルバが同じ規則を通る。

**Tech Stack:** Unity / C#、NUnit（EditMode）、uloop、unityプレイ録画テスト。

## Requirements

設計は ADR 0102（`docs/adr/0102-belt-route-merges-into-outputs-and-straight-only-routing.md`）と `.decisions/2026-11-18-ベルト経路は出力口の前を避けず合流し経路設置は直線ベルトだけにする.md`。

- R1. ビルドメニューで選んだブロックが「ベルトファミリーの直線ブロックで分岐器でない」ときだけ経路設置に入る。分岐器・上り坂・下り坂は通常ブロックと同じ単発設置。受け入れ基準: 分岐器・上りベルトを選んで1クリックするとそのブロックが1個だけ置かれ、始点待ちにならない（Task 5 の録画テスト）。
- R2. 分岐器手持ちの経路に関するコード（`BeltRouteQuery.TurnByOutput`・`BeltConveyorHoldingBlock.IsSplitter`・`FeedsForeignInput`・`IBeltRouteWorld.InputFaceBits`）を削除する。受け入れ基準: リポジトリ内に `TurnByOutput` の参照が無い。
- R3. 経路の途中セルは既存の出力口の前を避けない。受け入れ基準: 出力口が向くセルを通る直線経路が最短で引け（Task 2 の単体テスト）、そのセルで合流して両方の荷物が終点側のチェストへ届く（Task 2 の搬送テスト）。
- R4. 終点自身の前のセルは途中で使わない（既存の `CellInFrontOfEndIsNeverUsed` が通り続ける）。Ctrl 分岐の「使わない出口の隣に受け手」不可（`BeltRouteBranchGeometry`）は触らない。
- R5. カーソルがブロックの側面に当たり、その面が出力口のとき、始点のベルトの向きは「ブロックから離れる向き」、終点は R の向き。出力口でない側面は、始点は R の向き、終点はブロックへ差し込む向き。天面・底面・地面は R の向き。受け入れ基準: Task 4 の単体テストと Task 5 の録画テスト。
- R6. 終点は直線か下り坂。下り坂の終点は、手前セルの出る向きが終点の向きと同じ・手前の出る高さが終点の1つ上・終点の真上が空き、のときだけ。始点は直線か上り坂のまま。始点の下り坂・終点の上り坂は作らない。受け入れ基準: Task 3 の単体テスト・搬送テスト・参照ソルバ突き合わせ。
- R7. 直線ベルト手持ちの経路の途中に坂が入る動き、Ctrl 分岐、建設コスト判定、確定時の世界読み直し検査は変えない。

やらないこと: サーバー側の変更、マスタ・スキーマの変更、流れを逆にして引く機能、始点の下り坂・終点の上り坂、分岐器を並べる経路。

## Global Constraints

- 1ファイル200行以下。`BeltConveyorPlaceSystem.cs` は現在ちょうど200行なので行を増やさない。
- 1ディレクトリの `.cs` は10ファイルまで。`Client.Tests/PlaceSystem/BeltRoute/` は既に10ファイルあるので、新しいテストは `Client.Tests/PlaceSystem/BeltRoute/Endpoint/` に置く。
- コメントは「// 日本語」「// English」の2行セット。各1行。自明なコメントは書かない。
- デフォルト引数禁止。`partial` 禁止。`Func<>` 禁止。try-catch 禁止。単純な getter/setter プロパティ禁止（`{ get; private set; }` は可）。
- `.meta` は手で作らない（Unity が作ったものはコミットしてよい）。
- `.cs` を変えたら `uloop compile --project-path ./moorestech_client` を実行する。「Unity is reloading」エラーは45秒待って再実行。
- fail-closed で何もしない経路を足したら、理由を開発者向けログへ出す。
- `.moorestech-external-revisions.json` の作業ツリー差分はこのplanの対象外。コミットに含めない。

## 機能パリティ（死活表）

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 直線手持ちの2クリック経路、途中の坂の自動挿入 | 生きる | R7 |
| Ctrl 分岐、R での出口切替 | 生きる | `BeltRouteBranchOrigin.Judge` は `IsSplitter` 条件が外れるだけ |
| 始点セル再クリックの単発設置 | 生きる | `BeltRouteRoutingFrame` は変えない |
| 右クリック・Esc の始点解除、Q/E の高さ、建設コスト判定、確定時の読み直し | 生きる | `BeltConveyorPlaceSystem` の該当箇所は変えない |
| 坂手持ち・分岐器手持ちの2クリック経路（スポイトで拾った場合を含む） | 死ぬ | ADR 0102 §1（ユーザー裁定） |
| 出力口の前を避ける迂回 | 死ぬ | ADR 0102 §2（ユーザー裁定） |
| 終点を出力口の面に当てたときの「差し込む向き」 | R の向きに変わる | ADR 0102 §3（ユーザー裁定） |
| 始点で出力口の面を狙っている間の R | プレビューの向きに効かない（面から外せば効く） | ADR 0102 §3 |

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反6件・修正6件 / 確認用grepの範囲・Files節の漏れ・テストの死に書き込み・録画シナリオのチェスト種別・死活表の欠落・テスト名を修正。層責務・前例・イディオム・機構選択は発火なし
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強4・弱5・第3バケツ2 / 強はADR 0050（坂のドラッグ）・0100・0055との食い違いと終点種別の型で、ADRへ注記と判断記録へ理由を追記。弱1（セレクタの重複）は修正、弱2〜5と第3バケツは据え置き（理由は判断記録）

---

## File Structure

パスは `moorestech_client/Assets/Scripts/` からの相対。`PS` = `Client.Game/InGame/BlockSystem/PlaceSystem`、`BT` = `Client.Tests/PlaceSystem/BeltRoute`。

| ファイル | 責務 | Task |
|---|---|---|
| `PS/PlaceSystemSelector.cs` | 直線ベルトだけ経路設置へ振る | 1 |
| `PS/BeltConveyor/Parts/BeltConveyorHoldingBlock.cs` | 経路設置に入るブロックかの判定を持つ。`IsSplitter` 削除 | 1 |
| `PS/BeltConveyor/Route/BeltRouteQuery.cs` | `TurnByOutput` 削除 | 1 |
| `PS/BeltConveyor/Route/BeltRoutePlan.cs` | `BeltRouteCell` の summary から分岐器の記述を消す | 1 |
| `PS/BeltConveyor/Route/Search/BeltRouteTransitionRule.cs` | 分岐器規則の削除(1)・出力口回避の削除(2)・終点の種別つき進入判定(3) | 1,2,3 |
| `PS/BeltConveyor/Route/Search/BeltRoutePlanner.cs` | 出発状態から分岐器を外す(1)・2つの終点状態で組む(3) | 1,3 |
| `PS/BeltConveyor/Route/Search/BeltRouteRecovery.cs` | 向きは常に出る向き(1)・終点状態を引数で受ける(3) | 1,3 |
| `PS/BeltConveyor/Route/Search/BeltRouteRecoveryCandidates.cs` | 終点の進入判定へ種別を渡す | 3 |
| `PS/BeltConveyor/Route/Search/BeltRouteSearch.cs` | 終点状態を直線・下りの2つ持つ | 3 |
| `PS/BeltConveyor/Route/Search/BeltRouteEndEnclosureProbe.cs` | 終点の手前の高さ制限を外す | 3 |
| `PS/BeltConveyor/World/IBeltRouteWorld.cs`・`BeltRouteLiveWorld.cs` | `InputFaceBits` 削除 | 1 |
| `PS/BeltConveyor/Session/BeltRouteSession.cs`・`Session/Branch/BeltRouteBranchOrigin.cs` | `IsSplitter` 参照の削除 | 1 |
| `PS/BeltConveyor/Session/BeltRouteEndpointFacing.cs`（新規） | 端点の向きを出力口の有無から決める | 4 |
| `PS/BeltConveyor/Session/BeltRouteCursorHit.cs` | 側面ヒットの向きを `BeltRouteEndpointFacing` で決める | 4 |
| `PS/BeltConveyor/BeltConveyorPlaceSystem.cs` | 始点の向きにカーソルの向きを使う | 1,4 |
| `BT/*.cs`・`BT/World/BeltRouteGrid.cs` | 規則の変更に追従 | 1,2,3 |
| `BT/Endpoint/BeltRouteEndpointTest.cs`（新規） | 終点の下り坂・端点の向きの単体テスト | 3,4 |
| `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-route-merge-and-ports-via-ui.cs`（新規） | 録画テスト | 5 |

---

### Task 1: 経路設置を直線ベルトだけにし、分岐器手持ちの経路を消す

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemSelector.cs:60-71`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Parts/BeltConveyorHoldingBlock.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs:96-103`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/BeltRouteQuery.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/BeltRoutePlan.cs`（`BeltRouteCell` の summary だけ）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/Search/BeltRouteTransitionRule.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/Search/BeltRoutePlanner.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/Search/BeltRouteRecovery.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/Search/BeltRouteSearch.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/World/IBeltRouteWorld.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/World/BeltRouteLiveWorld.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/BeltRouteSession.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/Branch/BeltRouteBranchOrigin.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRoutePlannerBasicTest.cs`・`BeltRoutePlanVerifier.cs`・`BeltRouteRandomizedSearchTest.cs`・`BeltRouteReferenceCrossCheckTest.cs`・`BeltRouteReferenceSolver.cs`・`BeltRouteSearchOutcomeTest.cs`・`BeltRouteConveyanceTest.cs`・`World/BeltRouteGrid.cs`
- Modify: `docs/adr/0097-belt-two-click-shortest-route-placement.md`・`docs/adr/0100-belt-route-unbounded-weighted-search.md`・`docs/adr/0050-slope-belt-standalone-placement.md`（ヘッダへ注記1行ずつ）

**Interfaces:**
- Produces: `public static bool BeltConveyorHoldingBlock.IsRoutePlacementBlock(BlockId blockId)`
- Produces: `BeltRouteQuery(Vector3Int startCell, BlockDirection startDirection, bool startIsExistingBelt, Vector3Int endCell, BlockDirection endDirection, bool allowSlopes)`（引数6個。`TurnByOutput` 無し）
- Produces: `IBeltRouteWorld` は `IsBlockedByTerrain`・`IsBlockedByBlock`・`IsSlopeHead`・`OutputFaceBits` の4つ（`InputFaceBits` 無し）
- Produces: `BeltRouteTransitionRule.CanEnterGoalFrom` を削除（`CanEnterGoal(BeltRouteState previous)` は Task 3 まで残る）

- [ ] **Step 1: 経路設置に入るブロックの判定を `BeltConveyorHoldingBlock` に足し、`IsSplitter` と `SelectedBlockId` を消す**

`Resolve` の上に足す。手持ちは常に直線なので `SelectedBlockId` は `BlockId` と同じ値になり不要。

```csharp
        // 経路設置に入るのはファミリーの直線ブロックで分岐器でないものだけ。坂と分岐器は単発設置（ADR 0102）
        // Only a family's straight, non-splitter block enters route placement; slopes and splitters are placed singly (ADR 0102)
        public static bool IsRoutePlacementBlock(BlockId blockId)
        {
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(blockId, out var family)) return false;
            if (family.StraightBlockId != blockId) return false;
            return !BeltSplitterCounterpart.IsSplitter(MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockParam);
        }
```

コンストラクタを `private BeltConveyorHoldingBlock(BeltConveyorFamily family, BlockMasterElement blockMaster)` にし、`IsSplitter`・`SelectedBlockId` のフィールドと代入、クラス冒頭 summary の「坂を選んでいても直線を代表にし」の句を消す（summary は「選択ブロックから解決した手持ちファミリー。経路が各セルの種別を決める / The held family resolved from the build-menu selection; the route decides each cell's kind」）。`Resolve` の最後は `return new BeltConveyorHoldingBlock(family, master);`。

- [ ] **Step 2: `PlaceSystemSelector` の振り分けを変える**

```csharp
                    // 直線ベルトだけ経路設置へ。坂・分岐器と残りは通常ブロック（ADR 0102）
                    // Only straight belts go to route placement; slopes, splitters and the rest are common blocks (ADR 0102)
                    var blockMaster = MasterHolder.BlockMaster.GetBlockMaster(block.BlockId);
                    return blockMaster.BlockType switch
                    {
                        BlockMasterElement.BlockTypeConst.BeltConveyor or BlockMasterElement.BlockTypeConst.GearBeltConveyor when BeltConveyorHoldingBlock.IsRoutePlacementBlock(block.BlockId) => _beltConveyorPlaceSystem,
                        BlockMasterElement.BlockTypeConst.TrainRail => _trainRailPlaceSystem,
                        BlockMasterElement.BlockTypeConst.GearChainPole => _gearChainPoleConnectSystem,
                        _ => _commonBlockPlaceSystem,
                    };
```

`using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts;` を足す。

- [ ] **Step 3: `BeltConveyorPlaceSystem.ManualUpdate` の持ち替え検知を `BlockId` で行う**

```csharp
            if (isSelectionChanged || _holding == null || _holding.BlockId != target.BlockId)
```

- [ ] **Step 4: `BeltRouteQuery` から `TurnByOutput` を消す**

フィールド・コメント2行・コンストラクタ引数 `bool turnByOutput`・代入・`IsSameQuery` の `&& TurnByOutput == other.TurnByOutput` を消す。`AllowSlopes` のコメントは「手持ちファミリーに坂があるか / Whether the held family has slopes」。`StartDirection` のコメントは「始点ブロックの出る向き / The start block's exit heading」。

- [ ] **Step 5: `BeltRouteTransitionRule` から分岐器の規則を消す**

- `CanEnterGoalFrom` を消し、`CanEnterGoal` の最後の行を `return IsTurnAllowed(previous.Outgoing, _query.EndDirection);` にする。
- `IsIntermediatePlaceable` の最後の行 `return !FeedsForeignInput(...)` を消し、直前の行を `return (OutputFaceBits(cell) & EntryFaceBits(kind, incoming, outgoing)) == 0;` にする（この行の中身は Task 2 でさらに変える）。
- `FeedsForeignInput` をメソッドごと消す。
- `EntryFaceBits` の条件を `if (kind == BeltRouteCellKind.Straight)` にし、コメントの「分岐器と坂は」を「坂は / slopes only」に直す。
- `IsTurnAllowed` のコメント末尾「（分岐器も幾何は同じ）/ (splitters share the geometry)」を消す。

- [ ] **Step 6: 探索・組み立て・出発状態から分岐器を外す**

`BeltRouteSearch.Expand` の終点分岐:

```csharp
                        if (nextKind == BeltRouteCellKind.Straight && _rule.IsTurnAllowed(state.Outgoing, Goal.Outgoing)) Relax(Goal, g + BeltRouteCellKindExtension.StraightCost);
```

`BeltRoutePlanner.BuildStartStates` の `if (query.TurnByOutput) { ... }` ブロックを消し、コメントを「出発規則: 直線か上り。分岐は既存ベルトからコスト0 / Start rule: straight or up; a branch leaves the existing belt at cost 0」にする。

`BeltRouteRecovery.BuildPlan`:

```csharp
        // ベルトは出る向きが正面。総コストは置いたセルの和（分岐の始点は置かないので0）
        // A belt faces its exit heading; the total is the sum of placed cells (a branch start is not placed, so 0)
        private BeltRoutePlan BuildPlan()
        {
            var cells = new List<BeltRouteCell>(_reversedStates.Count);
            var totalCost = 0;
            for (var i = _reversedStates.Count - 1; 0 <= i; i--)
            {
                var state = _reversedStates[i];
                cells.Add(new BeltRouteCell(state.Cell, state.Outgoing, state.Kind));
                totalCost += state.Kind.Cost();
            }
            return new BeltRoutePlan(cells, totalCost);
        }
```

`BeltRoutePlan.cs` の `BeltRouteCell` の summary を「Facingは置くブロックの向き（出る向き）/ Facing is the placed block's heading (its exit)」に直す。

- [ ] **Step 7: `InputFaceBits` を世界から消す**

- `IBeltRouteWorld`: `int InputFaceBits(Vector3Int cell);` を消し、summary の「入出力辺 / edges」を「出力辺 / output edges」に直す。
- `BeltRouteLiveWorld`: `_inputFaceBits` フィールド・`Rebuild` の `_inputFaceBits.Clear()`・`InputFaceBits` メソッドを消す。`AddInputEdge` は `IBeltRouteEdgeSink`（分岐判定の `BeltRouteNeighbourEdges` が入力辺を読む）の実装として残し、中身を空にする:

```csharp
        // 探索は入力辺を読まない。入力辺を読むのは分岐判定の BeltRouteNeighbourEdges だけ（ADR 0102）
        // The search never reads input edges; only the branch judge's BeltRouteNeighbourEdges does (ADR 0102)
        public void AddInputEdge(BeltRouteEdge edge) { }
```

- `Client.Tests/PlaceSystem/BeltRoute/World/BeltRouteGrid.cs`: `_inputFaceBits` と `InputFaceBits` を消し、`AddInputEdge` を同じ空実装・同じコメントにする。

- [ ] **Step 8: `IsSplitter` の参照を消す**

- `BeltRouteSession.Plan`: `var query = new BeltRouteQuery(StartCell, StartDirection, IsBranch, endCell, endDirection, holding.AllowsSlopes);`
- `BeltRouteBranchOrigin.Judge`: `if (!IsStraightBeltOfFamily(belt, holding)) return BeltRouteBranchBlockReason.NotStraightBelt;`

- [ ] **Step 9: テストを追従させる**

- `BeltRoutePlannerBasicTest`: `SplitterHoldingTurnsOnTheExitSideAndStaysFlat` を消す。`new BeltRouteQuery(...)` の末尾引数（`, false)` / `, true)` の最後の1個）を全て外す。`Query` ヘルパは `return new BeltRouteQuery(start, startDirection, false, end, endDirection, true);`。
- `BeltRouteConveyanceTest`・`BeltRouteSearchOutcomeTest`: 同じく末尾引数を外す。
- `BeltRouteRandomizedSearchTest` の `WriteRandomBelt`: `grid.AddInputEdge(...)` の呼び出し5行を消す（`BeltRouteGrid.AddInputEdge` が空実装になり、誰も読まない書き込みになるため）。
- `BeltRouteRandomizedSearchTest`・`BeltRouteReferenceCrossCheckTest` の `TryPickQuery`: `var turnByOutput = ...` の行を消し、`var allowSlopes = random.Next(5) != 0;` にして、`new BeltRouteQuery(...)` の末尾引数を外す。ラベル文字列の ` splitter:{query.TurnByOutput}` を消す。
- `BeltRoutePlanVerifier.ToStates`: 分岐器の分岐を消す。

```csharp
        // 出る向きは次セルへの差分で、ベルトの向きと一致する
        // The exit heading is the delta to the next cell and equals the belt's facing
        private static List<BeltRouteState> ToStates(BeltRouteQuery query, IReadOnlyList<BeltRouteCell> cells)
        {
            var states = new List<BeltRouteState>(cells.Count);
            for (var i = 0; i < cells.Count; i++)
            {
                var outgoing = i + 1 < cells.Count ? BeltRouteDirection.FromStep(cells[i + 1].Cell - cells[i].Cell) : query.EndDirection;
                Assert.AreEqual(outgoing, cells[i].Facing, $"belt facing must equal its exit heading at {cells[i].Cell}");
                states.Add(new BeltRouteState(cells[i].Cell, outgoing, cells[i].Kind));
            }
            return states;
        }
```

- `BeltRouteReferenceSolver.StartStates`: `if (query.TurnByOutput) { ... }` ブロックを消し、コメントから「分岐器は3方向 / three headings for a splitter」を消す。

- [ ] **Step 10: ADR 0097・0100・0050 のヘッダへ注記を足す**

ADR 0100 のヘッダ（`- Supersedes:` の行の下）に1行:

```markdown
- Superseded in part by: ADR 0102（「変えないもの」のうち既存の出力口を横取りしない規則と分岐器手持ち、囲まれ判定の「終点の手前は同じ高さか1つ下だけ」、`IBeltRouteWorld` の入力辺の面ビットは 2026-11-18 に置き換え）
```

ADR 0050 のヘッダ部（状態の行の下）に1行:

```markdown
- Superseded in part by: ADR 0102（坂を手持ちにしたドラッグは通常ブロックと同じドラッグ列になる。「毎セル+1段の階段」は 2026-11-18 に置き換え）
```

ADR 0097 には次の1行。

`- Superseded in part by: ADR 0100（…）` の行の下に1行:

```markdown
- Superseded in part by: ADR 0102（既存の出力口の前を途中で使わない規則・分岐器手持ちの経路・手持ちが坂でも直線と同じ・終点は直線固定・終点の向きは 2026-11-18 に置き換え）
```

- [ ] **Step 11: 参照が残っていないことを確かめ、コンパイルしてテストを実行する**

Run: `grep -rn "TurnByOutput\|IsSplitter\b\|InputFaceBits\|CanEnterGoalFrom\|FeedsForeignInput\|SelectedBlockId" moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemSelector.cs moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute --include=*.cs`
Expected: ヒットは `BeltConveyorHoldingBlock.cs` の `BeltSplitterCounterpart.IsSplitter(` の1件だけ。それ以外が出たら消す。検索範囲は経路設置とそのテストに絞ってある（電線の `ElectricWirePoleSelection.SelectedBlockId` やサーバーの `BeltTopologyCell.IsSplitter` は無関係なので触らない）。

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.PlaceSystem\..*"`
Expected: ErrorCount 0 / 全て PASS。ランダム試験・参照ソルバ突き合わせは乱数の引き方が変わるので問題の並びが変わる。落ちたら、落ちた問い合わせ（ラベルに始点・終点が出る）を報告に書き、規則の取り違えが無いかを先に疑う（試験の回数や種を変えて通すのは不可）。

- [ ] **Step 12: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute docs/adr/0097-belt-two-click-shortest-route-placement.md
git commit -m "feat: ベルトの経路設置を直線ベルト手持ちだけにし、分岐器手持ちの経路を消す"
```

---

### Task 2: 既存の出力口の前を避けない

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/Search/BeltRouteTransitionRule.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRoutePlannerBasicTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRouteConveyanceTest.cs`

**Interfaces:**
- Consumes: Task 1 の `BeltRouteQuery`（引数6個）
- Produces: `BeltRouteTransitionRule.IsIntermediatePlaceable(Vector3Int cell, BeltRouteCellKind kind, BlockDirection incoming, BlockDirection outgoing)` は既存の出力口を見ない（シグネチャは同じ）

- [ ] **Step 1: 規則から世界の出力口を外し、終点自身の出口だけを残す**

`BeltRouteTransitionRule` のフィールドコメントと `OutputFaceBits` を置き換える。

```csharp
        // 終点自身の出口の辺。途中セルがこれを受けると終点が自分の経路へ流れ込む。辺に接するセルで途中になりうるのは、終点と終点の前それぞれの1つ下だけ
        // The end's own exit edge; a mid cell receiving it would make the end feed its own route. Of the cells touching it, only the ones below the end and below its front can be mid-route
```

```csharp
        // 終点自身の出口の辺の面ビット。既存の出力口は見ない（前を通れば合流になる。ADR 0102）
        // Face bits of the end's own exit edge; existing outputs are not consulted (passing in front of one merges, ADR 0102)
        private int EndExitFaceBits(Vector3Int cell)
        {
            var bits = 0;
            if (cell == _cellBelowEnd) bits |= _faceBitBelowEnd;
            if (cell == _cellBelowEndFront) bits |= _faceBitBelowEndFront;
            return bits;
        }
```

`IsIntermediatePlaceable` の最後の行を `return (EndExitFaceBits(cell) & EntryFaceBits(kind, incoming, outgoing)) == 0;` にする。`EntryFaceBits` の上のコメント2行（「入口の面が既存の出力口に向いていると…横取りする」）を次に置き換える。

```csharp
        // このセルが荷物を受ける面。終点自身の出口の辺と照合する
        // The faces this cell takes items on, matched against the end's own exit edge
```

クラス冒頭の summary はそのまま。`_world.OutputFaceBits` は規則から参照されなくなるが、`IBeltRouteWorld.OutputFaceBits` は Task 4 が使うので消さない。

- [ ] **Step 2: 単体テストを「避ける」から「通って合流する」へ書き換える**

`BeltRoutePlannerBasicTest.CellReceivingExistingOutputIsAvoided` を丸ごと置き換える。

```csharp
        [Test]
        public void CellReceivingExistingOutputIsUsedAndMerges()
        {
            var grid = FlatGrid();
            // (4,0,1)の北向きベルトが(4,0,2)へ流している
            // A north-facing belt at (4,0,1) flows into (4,0,2)
            grid.MarkBlockCell(new Vector3Int(4, 0, 1));
            grid.AddOutputEdge(BeltRouteEdge.FromFace(new Vector3Int(4, 0, 1), BlockDirection.North, 0));
            // 出力口の前を避けないので、迂回せず一直線に通る（そのセルは合流になる。ADR 0102）
            // Outputs are not avoided, so the route runs straight through without a detour (that cell becomes a merge, ADR 0102)
            var query = new BeltRouteQuery(new Vector3Int(2, 0, 2), BlockDirection.East, false, new Vector3Int(7, 0, 2), BlockDirection.East, false);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "existing output");
            Assert.IsTrue(result.Plan.Cells.Any(cell => cell.Cell == new Vector3Int(4, 0, 2)));
            Assert.AreEqual(6, result.Plan.Cells.Count);
            Assert.AreEqual(6 * BeltRouteCellKindExtension.StraightCost, result.Plan.TotalCost);
        }
```

同じファイルに、避け続ける例外（出力口が1段下の下り坂へ繋がっている配置）のテストも足す。

```csharp
        // 出力口が1段下の下り坂へ繋がっているとき、出力口の正面は坂の真上なので経路に使わない。使うと坂への接続を奪う（ADR 0102）
        // When an output feeds a down slope one level below, the cell in front of it is the slope's head and stays unused; using it would steal the slope's connection (ADR 0102)
        [Test]
        public void FrontOfAnOutputFeedingADownSlopeBelowStaysUnused()
        {
            var grid = FlatGrid();
            grid.MarkBlockCell(new Vector3Int(4, 1, 1));
            grid.AddOutputEdge(BeltRouteEdge.FromFace(new Vector3Int(4, 1, 1), BlockDirection.North, 1));
            grid.MarkBlockCell(new Vector3Int(4, 0, 2));
            grid.MarkSlopeHead(new Vector3Int(4, 1, 2));
            var query = new BeltRouteQuery(new Vector3Int(2, 1, 2), BlockDirection.East, false, new Vector3Int(7, 1, 2), BlockDirection.East, false);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.DefaultRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "slope head in front of an output");
            Assert.IsFalse(result.Plan.Cells.Any(cell => cell.Cell == new Vector3Int(4, 1, 2)));
        }
```

- [ ] **Step 3: 搬送テストを足す（合流して両方の荷物が届く）**

`BeltRouteConveyanceTest` の末尾に足す。

```csharp
        // 既存ベルトの末端が向くセルを新しい経路が通り、そこで合流して両方の荷物が同じ終点へ届く（ADR 0102）
        // A new route passes the cell an existing belt's end points at, merges there, and items from both reach the same end (ADR 0102)
        [Test]
        public void RoutePassingAnExistingOutputMergesBothFlows()
        {
            var world = BootWorld();
            var straight = ForUnitTestModBlockId.GetBlock("7b3e1a10-0001-4000-8000-000000000009");
            Assert.IsTrue(world.TryAddBlock(straight, new Vector3Int(3, 0, -2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(straight, new Vector3Int(3, 0, -1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));

            var query = new BeltRouteQuery(new Vector3Int(0, 0, 0), BlockDirection.East, false, new Vector3Int(6, 0, 0), BlockDirection.East, false);
            var result = new BeltRoutePlanner(BuildGridFromWorld(world), BeltRoutePlanner.NoRecoverySlack).Plan(query);
            Assert.IsTrue(result.HasPlan, "no route");
            BeltRoutePlanVerifier.AssertValid(BuildGridFromWorld(world), query, result.Plan, "merge route");
            Assert.IsTrue(result.Plan.Cells.Any(cell => cell.Cell == new Vector3Int(3, 0, 0)), "route should pass in front of the existing belt end");
            foreach (var cell in result.Plan.Cells)
                Assert.IsTrue(world.TryAddBlock(straight, cell.Cell, cell.Facing, Array.Empty<BlockCreateParam>(), out _), $"placement failed at {cell}");

            var routeSource = PlaceChest(world, new Vector3Int(-1, 0, 0));
            var sideSource = PlaceChest(world, new Vector3Int(3, 0, -3));
            var sink = PlaceChest(world, new Vector3Int(7, 0, 0));
            routeSource.SetItem(0, ServerContext.ItemStackFactory.Create(TestItemId, 1));
            sideSource.SetItem(0, ServerContext.ItemStackFactory.Create(TestItemId, 1));

            Assert.IsTrue(TickUntilDelivered(sink, 2, null, 0), $"both flows did not reach the sink: {CountOf(sink)}");
        }
```

続けて、既に別のラインへ流している分岐器の、空いた出口の前を経路が通る配置も足す。ADR 0102 の「既存の接続は付け替わらない」はコードの読みだけで未実測なので、この1配置で実測する。

```csharp
        // 分岐器の西の出口は既存ラインへ繋がり、北の出口は空いている。北の出口の前を新しい経路が通っても、既存ラインへ荷物が届き続ける（ADR 0102）
        // The splitter's west exit feeds an existing line and its north exit is free; a new route passing the north exit must leave the existing line fed (ADR 0102)
        [Test]
        public void RoutePassingASplitterExitKeepsTheExistingLineFed()
        {
            var world = BootWorld();
            var straight = ForUnitTestModBlockId.GetBlock("7b3e1a10-0001-4000-8000-000000000009");
            var splitter = ForUnitTestModBlockId.GetBlock("7b3e1a10-0001-4000-8000-000000000012");
            Assert.IsTrue(world.TryAddBlock(straight, new Vector3Int(3, 0, -2), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(splitter, new Vector3Int(3, 0, -1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(straight, new Vector3Int(2, 0, -1), BlockDirection.West, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(straight, new Vector3Int(1, 0, -1), BlockDirection.South, Array.Empty<BlockCreateParam>(), out _));
            Assert.IsTrue(world.TryAddBlock(straight, new Vector3Int(1, 0, -2), BlockDirection.South, Array.Empty<BlockCreateParam>(), out _));

            var query = new BeltRouteQuery(new Vector3Int(0, 0, 0), BlockDirection.East, false, new Vector3Int(6, 0, 0), BlockDirection.East, false);
            var result = new BeltRoutePlanner(BuildGridFromWorld(world), BeltRoutePlanner.NoRecoverySlack).Plan(query);
            Assert.IsTrue(result.HasPlan, "no route");
            Assert.IsTrue(result.Plan.Cells.Any(cell => cell.Cell == new Vector3Int(3, 0, 0)), "route should pass in front of the splitter's free exit");
            foreach (var cell in result.Plan.Cells)
                Assert.IsTrue(world.TryAddBlock(straight, cell.Cell, cell.Facing, Array.Empty<BlockCreateParam>(), out _), $"placement failed at {cell}");

            var source = PlaceChest(world, new Vector3Int(3, 0, -3));
            var existingSink = PlaceChest(world, new Vector3Int(1, 0, -3));
            var routeSink = PlaceChest(world, new Vector3Int(7, 0, 0));
            source.SetItem(0, ServerContext.ItemStackFactory.Create(TestItemId, 6));

            Assert.IsTrue(TickUntilDelivered(existingSink, 1, routeSink, 1), $"existing:{CountOf(existingSink)} route:{CountOf(routeSink)}");
        }
```

このテストが `existing:0` で落ちたら、新しい経路が既存の接続を奪っている。規則を戻さず、その結果をそのまま報告して止まる（ADR 0102 の前提が崩れるので、ユーザーの裁定が要る）。

- [ ] **Step 4: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.PlaceSystem\.BeltRoute\..*"`
Expected: ErrorCount 0 / 全て PASS。`RoutePassingAnExistingOutputMergesBothFlows` が落ちて `CountOf(sink)` が 1 なら、合流がサーバーで成立していない。そのときは規則を戻さず、どちらの荷物が届かなかったか（`CountOf(routeSource)`・`CountOf(sideSource)`）を測って報告する。

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/Search/BeltRouteTransitionRule.cs moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute
git commit -m "feat: ベルト経路が既存の出力口の前を避けず合流するようにする"
```

---

### Task 3: 終点が下り坂になれるようにする

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/Search/BeltRouteTransitionRule.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/Search/BeltRouteSearch.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/Search/BeltRouteRecovery.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/Search/BeltRouteRecoveryCandidates.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/Search/BeltRoutePlanner.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/Search/BeltRouteEndEnclosureProbe.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/BeltRoutePlanVerifier.cs`・`BeltRouteReferenceSolver.cs`・`BeltRouteConveyanceTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/Endpoint/BeltRouteEndpointTest.cs`

**Interfaces:**
- Produces: `public bool BeltRouteTransitionRule.CanEnterGoal(BeltRouteState previous, BeltRouteCellKind goalKind)`
- Produces: `BeltRouteSearch.Goal`（直線の終点状態）・`BeltRouteSearch.DownGoal`（下りの終点状態）・`BeltRouteSearch.BestGoal`（コストの小さい方。同じなら直線）
- Produces: `public BeltRoutePlan BeltRouteRecovery.Recover(BeltRouteTransitionRule rule, IReadOnlyList<(BeltRouteState state, int cost)> startStates, BeltRouteState goal, int budget)`

- [ ] **Step 1: 終点への進入判定に種別を渡す**

`BeltRouteTransitionRule.CanEnterGoal` を置き換える。

```csharp
        // 手前の状態から終点セルへ入れるか。終点は向きが固定で、直線か下り坂
        // Whether the end cell can be entered from the previous state; the end has a fixed facing and is straight or a down slope
        // 下り坂の終点は1つ上の高さから入り、曲がれず、真上を空ける。上り坂の終点は直線より高くつくだけで出口が1つ上へずれるので作らない（ADR 0102）
        // A down-slope end is entered one level up, cannot turn, and keeps its head clear; an up-slope end only costs more and lifts the exit a level, so it is never built (ADR 0102)
        public bool CanEnterGoal(BeltRouteState previous, BeltRouteCellKind goalKind)
        {
            var next = previous.Cell + BeltRouteDirection.Forward(previous.Outgoing);
            if (next.x != _query.EndCell.x || next.z != _query.EndCell.z) return false;
            if (previous.ExitHeight != _query.EndCell.y + goalKind.EntryHeightOffset()) return false;
            if (goalKind == BeltRouteCellKind.Straight) return IsTurnAllowed(previous.Outgoing, _query.EndDirection);
            return goalKind == BeltRouteCellKind.Down && previous.Outgoing == _query.EndDirection && IsSlopeHeadFree(_query.EndCell);
        }
```

`IsSlopeHeadFree(_query.EndCell)` は「終点の真上が始点でない・既存ブロックが無い」を見る。自分の経路が終点の真上を通らないことは組み立ての作業地図（`BeltRouteRecoveryMap`。終点を深さ0で置くとき坂なら真上も登録する）が守る。

- [ ] **Step 2: 探索が終点状態を2つ持つ**

`BeltRouteSearch` の `Goal` プロパティ周りを置き換える。

```csharp
        // 終点状態は直線と下り坂の2つ。Runごとに決まり呼び手も共用
        // Two goal states, straight and down slope; fixed per Run and shared with callers
        public BeltRouteState Goal { get; private set; }
        public BeltRouteState DownGoal { get; private set; }

        // コストの小さい方の終点状態。同じなら直線
        // The cheaper goal state; straight on a tie
        public BeltRouteState BestGoal => BestCost(DownGoal) < BestCost(Goal) ? DownGoal : Goal;
```

`Run` の `Goal = ...` の下に `DownGoal = new BeltRouteState(query.EndCell, query.EndDirection, BeltRouteCellKind.Down);` を足す。

`ExpandUntil` の3か所を変える。

```csharp
                if (expansionLimit <= Expansions) return BestCost(BestGoal) == Unreached ? BeltRouteSearchOutcome.CapReached : BeltRouteSearchOutcome.Reached;
```

```csharp
                if (state.Equals(Goal) || state.Equals(DownGoal))
```

```csharp
            return BestCost(BestGoal) == Unreached ? BeltRouteSearchOutcome.Exhausted : BeltRouteSearchOutcome.Reached;
```

`Expand` の終点分岐を置き換える。

```csharp
                    // 終点セルそのものへ入るときは終点状態（固定向きの直線か下り坂）だけを生成する。終点の列の他の高さは普通のセル
                    // Entering the end cell itself generates only goal states (fixed facing, straight or down slope); other heights of that column are ordinary cells
                    if (nextCell == Goal.Cell)
                    {
                        if (nextKind != BeltRouteCellKind.Up && _rule.CanEnterGoal(state, nextKind)) Relax(nextKind == BeltRouteCellKind.Straight ? Goal : DownGoal, g + nextKind.Cost());
                        continue;
                    }
```

坂を使わないファミリーでは、この分岐の上にある `if (nextKind.IsSlope() && !_rule.Query.AllowSlopes) continue;` が下り坂の終点も止める。

- [ ] **Step 3: 組み立てが終点状態を引数で受ける**

`BeltRouteRecovery.Recover` のシグネチャを `public BeltRoutePlan Recover(BeltRouteTransitionRule rule, IReadOnlyList<(BeltRouteState state, int cost)> startStates, BeltRouteState goal, int budget)` にし、中の `var goal = _search.Goal;` を消す。コメントは「探索が終点へ届いた後に呼ぶ。goalは組み立てる終点状態、budgetは経路の総コストの上限。組めなければnull / Called after the search reached the goal; goal is the goal state to assemble from and budget caps the route's total cost; null when nothing assembles within it」。

`BeltRouteRecoveryCandidates.IsReachableTransition`:

```csharp
            if (isGoal) return rule.CanEnterGoal(previous, next.Kind);
```

- [ ] **Step 4: 計画の入口が2つの終点状態で組む**

`BeltRoutePlanner.Plan` の「届いたコストで組み…」以降を置き換える。

```csharp
            // 届いた終点状態のコストで組み、失敗時は追加展開して直線・下り坂の順に組み直す
            // Assemble at the reached goal state's cost; on failure expand more and retry the straight goal, then the down-slope one
            var goal = _search.BestGoal;
            var reached = _search.BestCost(goal);
            var plan = _recovery.Recover(rule, startStates, goal, reached);
            if (plan == null && _recoverySlack != NoRecoverySlack)
            {
                _search.ExpandMore(RecoveryExtraExpansions);
                plan = _recovery.Recover(rule, startStates, _search.Goal, reached + _recoverySlack) ?? _recovery.Recover(rule, startStates, _search.DownGoal, reached + _recoverySlack);
            }
            return plan == null ? NotFound(query, "recovery failed", _search.Expansions) : BeltRoutePlanResult.Found(plan);
```

届いていない終点状態を渡した `Recover` は、既存の `if (_search.BestCost(goal) == BeltRouteSearch.Unreached) return null;` で null を返す。

自分の経路が下り坂の終点の真上を通らないことは、探索ではなく組み立ての作業地図が守る（探索の状態は自分の経路を知らない。自分の坂の真上を空ける既存の規則と同じ分担）。最短の下り坂の終点がその形でしか組めない配置は、余裕つきの組み直し（上の `if` の中。直線の終点を先に試す）が拾う。余裕なし（`NoRecoverySlack`。テストだけが使う）では経路なしになり得る。

- [ ] **Step 5: 囲まれ判定の、終点の手前の高さ制限を外す**

`BeltRouteEndEnclosureProbe.IsOpen` の `var maxRise = cell == endCell ? 0 : 1;` とその上のコメント2行を消し、内側のループを `for (var dy = -1; dy <= 1; dy++)` にする。下り坂の終点は1つ上の高さから入るので、終点の手前も高さ差±1。塗りつぶしは経路があるための必要条件を見るだけなので、広げても「開いている」と誤る方向にしか動かない。

- [ ] **Step 6: 検査器と参照ソルバを追従させる**

`BeltRoutePlanVerifier.AssertValid`:

```csharp
            Assert.AreNotEqual(BeltRouteCellKind.Up, cells[^1].Kind, $"{label}: last kind");
```

```csharp
                if (isGoal) Assert.IsTrue(rule.CanEnterGoal(previous, next.Kind), $"{label}: goal not enterable from {previous}");
```

`BeltRouteReferenceSolver.Step` の終点分岐:

```csharp
            // 終点セルへ入るのは終点状態（固定向きの直線か下り坂）だけ。下り坂は真上が置き済みでないこと
            // Only goal states (fixed facing, straight or down slope) may enter the end cell; a down slope needs its head unused
            if (next.Cell == _query.EndCell)
            {
                if (next.Kind == BeltRouteCellKind.Up || next.Outgoing != _query.EndDirection || !_rule.CanEnterGoal(current, next.Kind)) return;
                if (next.Kind.IsSlope() && _usedCells.Contains(next.Cell + Vector3Int.up)) return;
                if (_best == NotFound || nextCost < _best) _best = nextCost;
                return;
            }
```

- [ ] **Step 7: 単体テストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/Endpoint/BeltRouteEndpointTest.cs` を新規作成する（`.meta` とフォルダの `.meta` は Unity に作らせる）。

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route.Search;
using Game.Block.Interface;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.PlaceSystem.BeltRoute.Endpoint
{
    // 端点（始点・終点）の種別と向きの規則を確かめる（ADR 0102）
    // Checks the rules for the endpoints' kinds and facings (ADR 0102)
    public class BeltRouteEndpointTest
    {
        // 始点の隣で1つ下の終点。終点が下り坂になれないと遠回りするしかない
        // An end beside the start and one level down; without a down-slope end the only option is a detour
        [Test]
        public void EndOneLevelBelowTheAdjacentStartBecomesADownSlope()
        {
            var grid = BeltRoutePlannerBasicTest.FlatGrid(16);
            var query = new BeltRouteQuery(new Vector3Int(3, 1, 2), BlockDirection.East, false, new Vector3Int(4, 0, 2), BlockDirection.East, true);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.NoRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "down end");
            Assert.AreEqual(2, result.Plan.Cells.Count);
            Assert.AreEqual(BeltRouteCellKind.Down, result.Plan.Cells[1].Kind);
            Assert.AreEqual(BeltRouteCellKindExtension.StraightCost + BeltRouteCellKindExtension.SlopeCost, result.Plan.TotalCost);
        }

        // 終点の真上に既存ブロックがあると下り坂にできず、終点は直線のまま
        // With a block right above the end it cannot be a down slope, so the end stays straight
        [Test]
        public void EndUnderABlockStaysStraight()
        {
            var grid = BeltRoutePlannerBasicTest.FlatGrid(16);
            grid.MarkBlockCell(new Vector3Int(6, 1, 2));
            var query = new BeltRouteQuery(new Vector3Int(2, 1, 2), BlockDirection.East, false, new Vector3Int(6, 0, 2), BlockDirection.East, true);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.DefaultRecoverySlack).Plan(query);

            Assert.IsTrue(result.HasPlan);
            BeltRoutePlanVerifier.AssertValid(grid, query, result.Plan, "straight end under a block");
            Assert.AreEqual(BeltRouteCellKind.Straight, result.Plan.Cells[^1].Kind);
        }

        // 坂の無いファミリーでは終点も下り坂になれず、1つ下の終点へは経路なし
        // A family without slopes cannot make the end a down slope either, so there is no route to an end one level down
        [Test]
        public void NoRouteToTheLowerEndWithoutSlopes()
        {
            var grid = BeltRoutePlannerBasicTest.FlatGrid(16);
            var query = new BeltRouteQuery(new Vector3Int(3, 1, 2), BlockDirection.East, false, new Vector3Int(4, 0, 2), BlockDirection.East, false);
            var result = new BeltRoutePlanner(grid, BeltRoutePlanner.DefaultRecoverySlack).Plan(query);

            Assert.IsFalse(result.HasPlan);
        }
    }
}
```

`BeltRoutePlannerBasicTest.FlatGrid(int size)` は高さ3の箱（y=0..2）で、既に `internal static`。`NoRouteToTheLowerEndWithoutSlopes` は坂なしでは高さを変えられないので経路なしになる。

- [ ] **Step 8: 搬送テストを足す（下り坂の終点から荷物が出る）**

`BeltRouteConveyanceTest` の末尾に足す。

```csharp
        // 始点の隣で1つ下の終点は下り坂になり、荷物が終点の先のチェストへ届く（ADR 0102）
        // An end beside the start and one level down becomes a down slope, and the item reaches the chest beyond it (ADR 0102)
        [Test]
        public void DownSlopeEndConveysItemToTheLowerChest()
        {
            var world = BootWorld();
            var query = new BeltRouteQuery(new Vector3Int(0, 1, 0), BlockDirection.East, false, new Vector3Int(1, 0, 0), BlockDirection.East, true);
            var result = new BeltRoutePlanner(BuildGridFromWorld(world), BeltRoutePlanner.NoRecoverySlack).Plan(query);
            Assert.IsTrue(result.HasPlan, "no route");
            BeltRoutePlanVerifier.AssertValid(BuildGridFromWorld(world), query, result.Plan, "down end route");
            Assert.AreEqual(BeltRouteCellKind.Down, result.Plan.Cells[^1].Kind);

            var family = ForUnitTestModBlockId.GearBeltConveyor;
            foreach (var cell in result.Plan.Cells)
                Assert.IsTrue(world.TryAddBlock(ResolveFamilyBlock(family, cell.Kind), cell.Cell, cell.Facing, Array.Empty<BlockCreateParam>(), out _), $"placement failed at {cell}");
            // 歯車ベルトは回転が要るので、水平の始点の横に発電機を置く
            // Gear belts need rotation, so a generator goes beside the flat start
            Assert.IsTrue(world.TryAddBlock(ForUnitTestModBlockId.SimpleGearGenerator, new Vector3Int(0, 1, -1), BlockDirection.North, Array.Empty<BlockCreateParam>(), out _));

            var source = PlaceChest(world, new Vector3Int(-1, 1, 0));
            var sink = PlaceChest(world, new Vector3Int(2, 0, 0));
            source.SetItem(0, ServerContext.ItemStackFactory.Create(TestItemId, 1));

            Assert.IsTrue(TickUntilDelivered(sink, 1, null, 0), "item did not reach the lower chest");
        }
```

- [ ] **Step 9: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.PlaceSystem\.BeltRoute\..*"`
Expected: ErrorCount 0 / 全て PASS。参照ソルバ突き合わせ（`BeltRouteReferenceCrossCheckTest`）が「参照は見つけたが本番は経路なし」で落ちたら、下り坂の終点が探索と参照で食い違っている。落ちた問い合わせのラベルを報告に書き、`CanEnterGoal` の条件と `Expand` の終点分岐を見直す（試験の回数や種を変えて通すのは不可）。

- [ ] **Step 10: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Route/Search moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute
git commit -m "feat: ベルト経路の終点が下り坂になれるようにする"
```

---

### Task 4: 出力口の面を狙ったらブロックから離れる向きにする

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/BeltRouteEndpointFacing.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/Session/BeltRouteCursorHit.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor/BeltConveyorPlaceSystem.cs:120-172`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute/Endpoint/BeltRouteEndpointTest.cs`

**Interfaces:**
- Consumes: `IBeltRouteWorld.OutputFaceBits(Vector3Int cell)`、`BeltRouteFaceBits.FaceBit(BlockDirection face, int heightOffset)`
- Produces: `public enum BeltRouteEndpointRole { Start, End }`
- Produces: `public static BlockDirection BeltRouteEndpointFacing.Resolve(IBeltRouteWorld world, Vector3Int cell, BlockDirection faceInward, BlockDirection keyDirection, BeltRouteEndpointRole role)`
- Produces: `public static bool BeltRouteCursorHit.TryResolve(Camera camera, int heightOffset, BlockDirection keyDirection, BlockMasterElement holdingMaster, BlockGameObjectDataStore blocks, IBeltRouteWorld world, BeltRouteEndpointRole role, PlacementFeedback feedback, out BeltRouteCursorHit hit)`

- [ ] **Step 1: 端点の向きの規則を1ファイルに書く**

`BeltRouteEndpointFacing.cs` を新規作成する。

```csharp
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;
using Game.Block.Interface;
using UnityEngine;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session
{
    /// <summary>
    ///     経路の端点が始点か終点か
    ///     Whether a route endpoint is the start or the end
    /// </summary>
    public enum BeltRouteEndpointRole
    {
        Start,
        End,
    }

    /// <summary>
    ///     ブロックの側面を狙った端点の向きを決める（ADR 0102）
    ///     Decides the facing of an endpoint aimed at a block's side face (ADR 0102)
    /// </summary>
    public static class BeltRouteEndpointFacing
    {
        // faceInward は狙った面からブロックへ向かう向き
        // faceInward heads from the aimed face into the block
        // 始点は、面が出力口ならブロックから離れる向き（その出力を受ける）、それ以外はキーの向き
        // The start heads away from the block on an output port (taking that output), and keeps the key's heading otherwise
        // 終点は、面が出力口ならキーの向き（差し込んでも繋がらない）、それ以外はブロックへ差し込む向き
        // The end keeps the key's heading on an output port (heading in would connect nothing), and heads into the block otherwise
        public static BlockDirection Resolve(IBeltRouteWorld world, Vector3Int cell, BlockDirection faceInward, BlockDirection keyDirection, BeltRouteEndpointRole role)
        {
            var facesOutputPort = (world.OutputFaceBits(cell) & BeltRouteFaceBits.FaceBit(faceInward, 0)) != 0;
            if (role == BeltRouteEndpointRole.Start) return facesOutputPort ? BeltRouteDirection.Opposite(faceInward) : keyDirection;
            return facesOutputPort ? keyDirection : faceInward;
        }
    }
}
```

`world.OutputFaceBits(cell)` の高さ差0のビットは「cell の高さにある、その面の出力の辺」を表す（`BeltRouteFaceBits.Mark` が辺に接する両側のセルへ、辺の方を向く面のビットを立てる）。cell は空きセルなので、その辺は狙ったブロックの出力口。

- [ ] **Step 2: `BeltRouteCursorHit` が側面ヒットの向きをこの規則で決める**

`TryResolve` を置き換え、`ResolveFaceDirection` を「側面ならブロックへ向かう向きを返す」形に変える。

```csharp
        public static bool TryResolve(Camera camera, int heightOffset, BlockDirection keyDirection, BlockMasterElement holdingMaster, BlockGameObjectDataStore blocks, IBeltRouteWorld world, BeltRouteEndpointRole role, PlacementFeedback feedback, out BeltRouteCursorHit hit)
        {
            hit = default;
            if (!PlaceSystemUtil.TryGetFootprintGroundedRayHitBlockPosition(camera, heightOffset, keyDirection, holdingMaster, feedback, out var cell, out var surfaceType)) return false;

            // 地面はキーの向き。天面・底面は面の向きが決まらないのでキーの向きのまま
            // Ground keeps the key's heading; top and bottom faces fix no heading, so the key's stays
            if (surfaceType == null)
            {
                hit = new BeltRouteCursorHit(cell, keyDirection, null);
                return true;
            }
            var isSideFace = TryGetFaceInward(surfaceType.Value, out var faceInward);
            var direction = isSideFace ? BeltRouteEndpointFacing.Resolve(world, cell, faceInward, keyDirection, role) : keyDirection;
            hit = new BeltRouteCursorHit(cell, direction, ResolveHitBlock(blocks, cell, surfaceType.Value, faceInward));
            return true;
        }

        // 側面なら、面の手前セルからブロックへ向かう向きを返す
        // For a side face, returns the heading from the cell in front of the face into the block
        private static bool TryGetFaceInward(PreviewSurfaceType surfaceType, out BlockDirection faceInward)
        {
            faceInward = BlockDirection.North;
            switch (surfaceType)
            {
                case PreviewSurfaceType.YX_Origin: faceInward = BlockDirection.North; return true;
                case PreviewSurfaceType.YX_Z: faceInward = BlockDirection.South; return true;
                case PreviewSurfaceType.YZ_Origin: faceInward = BlockDirection.East; return true;
                case PreviewSurfaceType.YZ_X: faceInward = BlockDirection.West; return true;
                default: return false;
            }
        }
```

`ResolveHitBlock` の第4引数名を `faceInward` に変える（中身は同じ。天面・底面では使われない）。構造体冒頭の summary を「カーソルが指すセルと、そこに置くベルトの向き。ブロックの側面に当たったときの向きは BeltRouteEndpointFacing が決める / The cell under the cursor and the heading a belt there takes; a hit on a block's side face is resolved by BeltRouteEndpointFacing」にする。`using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.World;` を足す。

- [ ] **Step 3: 設置システムが始点にもカーソルの向きを使う**

`BeltConveyorPlaceSystem` を次のとおり変える（行数は増やさない）。

`UpdateStartSelection`:

```csharp
            if (!BeltRouteCursorHit.TryResolve(_mainCamera, _placementHeightOffset.Value, _currentDirection, _holding.BlockMaster, _blocks, _world, BeltRouteEndpointRole.Start, feedback, out var hit)) return;
```

```csharp
            ShowPreview(new List<PlaceInfo> { BeltRoutePlaceInfoBuilder.Single(hit.Cell, hit.Direction, _holding.BlockId, placeable) });

            if (placeable && BeltRouteKeyInput.IsConfirmPressed) _session = BeltRouteSession.CreateAtCell(hit.Cell, hit.Direction, _world);
```

`UpdateRouting`:

```csharp
            if (!BeltRouteCursorHit.TryResolve(_mainCamera, _placementHeightOffset.Value, _currentDirection, _holding.BlockMaster, _blocks, _world, BeltRouteEndpointRole.End, feedback, out var hit)) return;
```

終点が出力口の面に当たったときは `hit.Direction` が R の向き（`_currentDirection`）になる（ADR 0102 §3）。始点選び中の R は今までどおり `_currentDirection` を回す。出力口の面を狙っている間はプレビューの向きが変わらないだけで、面から外せば回した向きになる。

- [ ] **Step 4: 単体テストを足す**

`BeltRouteEndpointTest` に足す（`using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Session;` を足す）。

```csharp
        // (4,0,3)のブロックが南の面から(4,0,2)へ出力している。(4,0,2)から北の面（ブロックへ向かう向き=北）を狙う
        // A block at (4,0,3) outputs through its south face into (4,0,2); the north face is aimed from (4,0,2) (the heading into the block is north)
        private static BeltRouteGrid GridWithOutputPortFacingSouth()
        {
            var grid = BeltRoutePlannerBasicTest.FlatGrid(16);
            grid.MarkBlockCell(new Vector3Int(4, 0, 3));
            grid.AddOutputEdge(BeltRouteEdge.FromFace(new Vector3Int(4, 0, 3), BlockDirection.South, 0));
            return grid;
        }

        // 出力口の面。始点はブロックから離れる向き（南）、終点はキーの向き（東）
        // An output port face: the start heads away from the block (south), the end keeps the key's heading (east)
        [TestCase(BeltRouteEndpointRole.Start, BlockDirection.South)]
        [TestCase(BeltRouteEndpointRole.End, BlockDirection.East)]
        public void OutputPortFaceTurnsTheStartAwayAndLeavesTheEndToTheKey(BeltRouteEndpointRole role, BlockDirection expected)
        {
            var facing = BeltRouteEndpointFacing.Resolve(GridWithOutputPortFacingSouth(), new Vector3Int(4, 0, 2), BlockDirection.North, BlockDirection.East, role);
            Assert.AreEqual(expected, facing);
        }

        // 出力口でない面。始点はキーの向き、終点はブロックへ差し込む向き
        // A face without an output port: the start keeps the key's heading, the end heads into the block
        [TestCase(BeltRouteEndpointRole.Start, BlockDirection.East)]
        [TestCase(BeltRouteEndpointRole.End, BlockDirection.West)]
        public void PlainFaceKeepsTheFormerHeading(BeltRouteEndpointRole role, BlockDirection expected)
        {
            // (5,0,3)から西の面（ブロックへ向かう向き=西）を狙う。この面に出力口は無い
            // Aim the west-heading face from (5,0,3); there is no output port on it
            var facing = BeltRouteEndpointFacing.Resolve(GridWithOutputPortFacingSouth(), new Vector3Int(5, 0, 3), BlockDirection.West, BlockDirection.East, role);
            Assert.AreEqual(expected, facing);
        }

        // 1つ上の高さの出力口は、その下のセルの向きを変えない
        // An output port one level up does not change the heading of the cell below it
        [Test]
        public void OutputPortOneLevelUpIsIgnored()
        {
            var grid = BeltRoutePlannerBasicTest.FlatGrid(16);
            grid.MarkBlockCell(new Vector3Int(4, 0, 3));
            grid.MarkBlockCell(new Vector3Int(4, 1, 3));
            grid.AddOutputEdge(BeltRouteEdge.FromFace(new Vector3Int(4, 1, 3), BlockDirection.South, 1));
            var facing = BeltRouteEndpointFacing.Resolve(grid, new Vector3Int(4, 0, 2), BlockDirection.North, BlockDirection.East, BeltRouteEndpointRole.End);
            Assert.AreEqual(BlockDirection.North, facing);
        }
```

`using Client.Tests.PlaceSystem.BeltRoute.World;`（`BeltRouteGrid` の名前空間。既存テストの using に合わせる）を足す。

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.PlaceSystem\..*"`
Expected: ErrorCount 0 / 全て PASS（`BeltRouteWiringTest` は設置システムを実際に回すので、ここで通ること）。`wc -l` で `BeltConveyorPlaceSystem.cs` が200行以下であることを確かめる。

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/BeltConveyor moorestech_client/Assets/Scripts/Client.Tests/PlaceSystem/BeltRoute
git commit -m "feat: ブロックの出力口の面を狙ったベルトをブロックから離れる向きにする"
```

---

### Task 5: unityプレイ録画テスト

**Files:**
- Create: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-route-merge-and-ports-via-ui.cs`
- Modify（必要なら）: `.agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-route-two-click-via-ui.cs`・`belt-route-branch-via-ui.cs`

**Interfaces:**
- Consumes: プレイテストDSL（`p.RoutePlaceViaUi`・`p.PlaceBlockDirect`・`p.OpenBuildMenuAndSelectBlock`・`p.AimAt`・`p.ClickPlace`・`p.GetBlock`・`p.Assert`・`p.Screenshot`・`p.WarpPlayer`・`p.HideMapObjects`）。書き方は `unity-playmode-recorded-playtest` スキルと既存の `belt-route-two-click-via-ui.cs` に従う。

- [ ] **Step 1: 既存の2シナリオが新しい規則で意味を保つか確かめる**

Run: `grep -n "RoutePlaceViaUi\|BranchPlaceViaUi\|OpenBuildMenuAndSelectBlock" .agents/skills/unity-playmode-recorded-playtest/scenarios/building/belt-route-*.cs`
経路設置の手持ちが直線ベルト以外（「上りベルトコンベア」「ベルトコンベア分岐器」等）の呼び出しがあれば、直線ベルトへ直す。出力口の前を避ける遠回りをセル数で確かめている Assert があれば、避けない前提の数へ直す。無ければ変更しない。

- [ ] **Step 2: 新しいシナリオを書く**

`belt-route-two-click-via-ui.cs` の冒頭（using・`PlaytestRunOptions { Record = true }`・`SetupDebugEnvironment`・`SkipOpeningSkit`・`FocusGameViewAndEnableInput`・`HideMapObjects`）と、同ファイルのローカル関数 `CountBelts`・`HasBlockNamed` の書き方をそのまま使い、足場 y=32 の上で次の4フェーズを確かめる。座標は足場の内側で、フェーズごとに重ならない場所を選ぶ。各フェーズの最後に `await p.ExitToGameScreen(); await p.Screenshot("<番号>-<名前>");`。

1. **出力口の前を通って合流する。** `p.PlaceBlockDirect("ベルトコンベア", (5,32,0), North)` と `(5,32,1), North` を置く（末端が (5,32,2) を向く）。`p.RoutePlaceViaUi("ベルトコンベア", (2,32,2), East, (8,32,2), East)`。Assert: `(5,32,2)` にブロックがあり東向き／z=2 の x=2..8 の7セルだけにベルトが増えた（迂回していない）。続けて供給と受けに「鉄のミニコンベアチェスト」（入口は背面、出口は正面。「木のチェスト」は入出力口が無くベルトと繋がらないので使わない）を置く: 供給 (5,32,-1) 北向き・供給 (1,32,2) 東向き・受け (9,32,2) 東向き。供給2個にアイテムを1個ずつ入れ、受けに2個届くまで待つ（アイテムの入れ方・待ち方は `belt-route-two-click-via-ui.cs` の荷物が流れるフェーズと同じ手順）。
2. **始点で出力口の面を狙うと離れる向きになり、終点で狙うと R の向きのままになる。**（終点側: 同じ出力口の面を、別の経路の終点として狙い、R で南向きにしてから確定。Assert: 終点のベルトが南向きで、チェストへ差し込む向き＝西ではない。）始点側は次のとおり。 「木のコンベアチェスト」（3×2×4。北向きで置くと出力口はローカル (1,0,3) の北の面）を `p.PlaceBlockDirect` で置く。「ベルトコンベア」を選び、R を押さずに（向きは北のまま。これが離れる向きと同じにならないよう、チェストは東向きで置いて出力口が東の面になるようにする）、出力口の面の中心の少し手前の点を `p.AimAt` で狙って `p.ClickPlace`、4セル先の地面を狙って `p.ClickPlace`。Assert: 出力口の前のセルのベルトが東向き（`ServerContext.WorldBlockDatastore.GetBlockDirection`）。チェストの出力スロット側にアイテムを入れ、経路の終点の先に、経路と同じ向きで置いた「鉄のミニコンベアチェスト」へ届くことも確かめる。
3. **終点が下り坂になる。** 「木のチェスト」を1個置き、その上面のセル (x,33,z) を始点、隣の地面のセル (x+1,32,z) を終点にして東向きで `p.RoutePlaceViaUi`（始点はチェストの天面を狙う形になる。`RoutePlaceViaUi` の狙い方で始点が (x,33,z) にならなければ、`p.AimAt` で天面の中心を直接狙う手順に置き換える）。Assert: `(x+1,32,z)` のブロック名が「下りベルトコンベア」で東向き、`CountBelts()` の増分が2。
4. **分岐器と坂は単発設置。** `p.OpenBuildMenuAndSelectBlock("ベルトコンベア分岐器")` → 地面を狙って `p.ClickPlace` を1回。Assert: そのセルに「ベルトコンベア分岐器」が1個あり、ベルト系ブロックの総数の増分が1（始点待ちにならず、1クリックで置かれた）。同じことを「上りベルトコンベア」でも行う。

ブロック名は `../moorestech_master/server_v8/mods` の `blocks.json` の `name` と一致させる（実行前に `grep` で確かめる）。

- [ ] **Step 3: シナリオを実行して録画と結果を確かめる**

`unity-playmode-recorded-playtest` スキルの手順で `belt-route-merge-and-ports-via-ui` を実行し、続けて `belt-route-two-click-via-ui` と `belt-route-branch-via-ui` も実行する。
Expected: 3本とも全 Assert が通る。録画・スクショは worktree の gitignore 下に置き、`../moorestech_logs` へはスクショ3枚までと報告だけを書く。落ちた Assert は、テスト側を緩めず、観測した値（ブロック名・向き・個数）を報告に書いて原因を切り分ける。

- [ ] **Step 4: コミットする**

```bash
git add .agents/skills/unity-playmode-recorded-playtest/scenarios/building
git commit -m "test: ベルト経路の出力口合流・出力口の面の向き・下り坂の終点・単発設置の録画シナリオを足す"
```

---

### Task 6: 全ブランチレビュー

- [ ] 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー指摘の反映がソースの判定経路・条件式・その評価時点に触れたら、Task 5 の録画シナリオ3本を反映後のバイナリで再実施してから完了とする。

### Task 7: セッション終了可能状態にする

- [ ] pr-create スキルで PR を作成し、master とのコンフリクトがあれば master をマージして解消・コンパイル確認のうえ push する。全作業がコミット・push 済みで、このセッションをそのまま閉じても PR がマージ可能な状態になっていることを確認して終える。PR 未作成のまま終わるのは plan 未完了。マージは通常のマージコミット（Squash 不可）。

---

## 判断記録（ADR）

- 設計の裁定: `docs/adr/0102-belt-route-merges-into-outputs-and-straight-only-routing.md`、`.decisions/2026-11-18-ベルト経路は出力口の前を避けず合流し経路設置は直線ベルトだけにする.md`。ユーザー裁定と agent前提の区別はADRの出所欄が正。
- ランタイム挙動に触れるので、unityプレイ録画テスト（Task 5）を含める。
- `PlaceSystemSelector` の振り分け判定を `BeltConveyorHoldingBlock.IsRoutePlacementBlock` に置く（agent前提）。理由: 「どのブロックが経路設置の手持ちになれるか」は手持ちの解決（`Resolve`）と同じ知識で、ファミリーの直線・分岐器判定を既に同クラスが使っている。サーバー側の `BeltConveyorPlaceFamilyUtil` に置かないのは、経路設置がクライアントだけの概念だから。
- `BeltRouteLiveWorld.AddInputEdge` を空実装で残す（agent前提）。理由: 辺の書き込み（`BeltRouteBlockEdgeScanner.Write`）は分岐判定の `BeltRouteNeighbourEdges` と同じ `IBeltRouteEdgeSink` を使い、そちらは入力辺を読む。書き込み先を2つの interface に割ると走査側に分岐が要る。
- 終点状態を `Goal`・`DownGoal` の2つの固定プロパティで持つ（agent前提）。理由: 終点になれる種別は直線と下りの2つに決まっており（ADR 0102 §4）、リストにする利点が無い。
- 端点の向きの規則を `BeltRouteEndpointFacing` に切り出す（agent前提）。理由: カメラの光線に依らない純粋な規則として単体テストでき、`BeltRouteCursorHit` は光線ヒットの解釈だけを持てる。
- 終点の種別は `BeltRouteCellKind` をそのまま引数に使い、終点専用の型は作らない（agent前提）。理由: 「上りは終点にならない」の判定は `BeltRouteTransitionRule.CanEnterGoal` が正で、探索の `nextKind != Up` はその呼び出しを省く早期の枝刈り、検査器と参照ソルバはテスト側の独立した確認。型を分けると探索の種別ループ（0..2 の添字）に変換が要る。
- 始点選び中、R と確定クリックを同じフレームに押したときだけ、始点の向きが回転前になる（agent前提・既知の割り切り）。理由: `BeltRouteCursorHit.TryResolve` は Ctrl 分岐の判定（R が出口切替になる）より前に要り、R の回転はその後に置く必要がある。`BeltConveyorPlaceSystem.cs` は200行ちょうどで、順序を直す行を足せない。人の操作で同フレームになることは実質無い。
- テストを足すことと `.decisions/2026-11-07-ベルト経路のテストは利用者が見る挙動を守るものだけ残す.md` の関係（agent前提）: 足すのは利用者が見る挙動（出力口の前で合流して荷物が届く・終点が下り坂になる・出力口の面で向きが決まる）を守るものだけで、同裁定の趣旨に沿う。壁時計の閾値は置かない。
- 坂・分岐器を手持ちにした左ドラッグは、通常ブロックと同じドラッグ列になる（同じ高さに横一列）。出所: ユーザー裁定 2026-11-18 原文「他ブロックと同じように単発設置で良い」の字義（他ブロックと同じ）。ADR 0050 の「毎セル+1段の階段」は置き換え（ADR 0102 §1）。
- このセッションで本体が直接実装する。出所: ユーザー裁定 2026-11-18「2 ok」（質問「実装をこのセッションで私が進めてよいですか」への回答）。
- セーブ形式は変えない（設置時のクライアント判定だけの変更）。マイグレーションは不要。
- 反証検査（fresh-context・opus、2026-11-18）: Critical 1件（録画シナリオの荷物の受け渡しに入出力口の無い「木のチェスト」を使っていた → 「鉄のミニコンベアチェスト」へ修正済み）、Warning 1件（Task 1 Step 11 の grep の期待値 → 検索範囲を絞って修正済み）、設計判断 0件。反証できなかった主張（証明ではない。コードの読みで反例が出なかっただけで、未実行・未網羅）: 出力口の前に置いても既存の接続が付け替わらない（サーバーは辺と同じ高さのセルを先に選び、1つ下が受け手になり得るのは坂のときだけで、その真上は坂の真上の規則で置けない）／テスト用ベルトの横入力で合流が成立する／下り坂の終点の探索・組み立て・参照ソルバの条件が一致する／出力口の面ビットの読み方。終点が出力口の面のとき R の向きにする変更（ユーザー裁定）は検査開始後に入れたもので、単体テストの期待値（終点=キーの向き）は本体が手で追って確認した。
