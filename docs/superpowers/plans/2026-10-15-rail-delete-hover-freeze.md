# 破壊モードのレールホバーNREで抜けられなくなる不具合 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 破壊モードで赤プレビューを戻す処理が例外を出しても、毎フレーム同じ例外を繰り返さず破壊モードを抜けられるようにし、あわせてテスターのセーブで NRE の原因（何が null か）と新版の「破壊だけ効かない」症状を再現・特定する。

**Architecture:** 単体ホバーの赤プレビュー対象の保持を新クラス `SingleHoverDeletePreview` へ切り出し、「参照を先に手放してから戻す」順序をクラス内で固定する。ドラッグ選択（`DragDeleteSelection`）のキャンセル・確定も同じく「選択を先に空にしてから戻す」順へ揃える。原因本体はテスターのセーブを Editor で読み込んで再現し、行番号付きスタックで確定してから直す（推測で直さない）。

**Tech Stack:** Unity (C#), NUnit (EditMode), uloop, unity-playmode-recorded-playtest

## Requirements

- R1: 赤プレビューを戻す処理（`IDeleteTarget.ResetMaterial`）が例外を出しても、同じ対象への戻し処理は以後呼ばれない（例外は最大1回）。受け入れ基準: Task 1 のテストで ResetCount が 1 のまま、2回目の呼び出しが例外を出さない
- R2: 戻し処理が例外を出した次のフレーム以降、G / ESC / 右短押し / B / Tab で破壊モードを抜けられる（例外と同じフレームの離脱入力は失われ、次の押下で抜ける）。受け入れ基準: Task 1 のテストで (a) `SingleHoverDeletePreview.Current == null` かつ `DragDeleteSelection.HasSelection() == false` になること、(b) `UIStateControl` で `OnExit` が1回投げた遷移は `CurrentState` 不変で、次の遷移要求で抜けられることを確認。Task 2 で再現できた場合は実プレイでも G で抜けられることを録画で確認
- R3: ドラッグ選択のキャンセル・確定で戻し処理が例外を出しても、選択は空になり、同じ削除を二重送信しない。受け入れ基準: Task 1 のテストで `HasSelection()==false`、`DeleteCount==1`
- R4: テスターのセーブ（`5/save.json`）を現行 master で読み込み、(a) 該当橋脚のレールへのホバーで NRE が出るか、(b) 新版で報告された「レール・橋脚が破壊できない」が再現するかを確認し、観測した証拠（ログ行・スクショ・値）を報告ファイルに残す
- R5: R4 で null の正体が証拠付きで特定できたら、その原因を直す（Task 3）。特定できなければ直さずに報告して止める
- やらないこと: 原因未確定のまま候補箇所（`BezierRailMeshGpu.ApplyPropertyBlock` 等）へ null ガードを足すこと／橋脚が「本来置けない座標」に建った経緯（設置判定の変更）の修正（別課題。Task 2 で事実だけ記録する）／橋脚撤去時にレール素材が戻らない件（別スレッドの別課題）

## Global Constraints

- try-catch は使わない（AGENTS.md。UI 層は外部境界ではない）
- `Func<>`・partial・デフォルト引数は使わない。1ファイル200行以下・1ディレクトリ10ファイル以下
- 日本語→英語の2行コメントを約3〜10行ごと
- `.meta` は手で作らない。Prefab・シーンを手で編集しない
- `.cs` を変えたら必ず `uloop compile --project-path ./moorestech_client`（ErrorCount 0）
- 作業場所: worktree `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/rail-delete-hover-freeze`（ブランチ `fix/rail-delete-hover-freeze`）。Editor はこの worktree のものだけ使う（`uloop launch /Users/sakastudio/hermes-agent/data/worktrees/moorestech/rail-delete-hover-freeze/moorestech_client`）
- bd: `moorestech-cdddu`。区切りごとに `bd note moorestech-cdddu "..."`

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反0件・修正0件 / `SingleHoverDeletePreview` は同役割の前例 `DragDeleteSelection`（同ディレクトリ・サービス所有の素のクラス）に合わせた。新規機構なし。死活表: G/ESC/右短押し/B/Tab/中クリック/Ctrl+Z/ドラッグ削除は全て存続（順序変更のみ）
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強0・弱2・第3バケツ1 / 弱: 新設 Release に破棄済み判定が無い（5-F）・破棄済み判定が Selection 内で2回重複（検査6）。第3バケツ: 赤プレビューの要求者方式 `RailChainRemovePreview`/`IRemovePreviewable` と単体ホバー保持の役割重なり

---

## 背景（コードで確認済みの事実）

- `DeleteObjectService.UpdateSingleHoverPreview`（`moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs:91-116`）は、ホバー対象が変わると `_deleteTargetObject.ResetMaterial()` を呼んでから `_deleteTargetObject = hovered` に差し替える。戻し処理が例外を出すと差し替えが走らず、次フレームも同じ対象で同じ例外が出る（動画でエラー件数 0→18→1063）
- モード離脱は `UIStateControl`（`.../UI/UIState/UIStateControl.cs:65`）が `OnExit()` を呼んでから `CurrentState` を書き換える。`DeleteObjectState.OnExit` → `DeleteObjectService.CancelSelection` が同じ戻し処理を呼ぶので、そこで例外が出ると遷移が中断され、どこへも抜けられない
- `DragDeleteSelection.CancelSelection` / `CommitDelete`（`.../DragDelete/DragDeleteSelection.cs:84-134`）も「全件を戻す → 最後に `_selectedTargets.Clear()`」の順で、1件でも例外を出すと選択が残り続ける
- 報告者のスタック: `BezierRailChain.ResetMaterial` ← `DeleteTargetRail.ResetMaterial` ← `UpdateSingleHoverPreview`。ビルド版は途中フレームがインライン化されており、null の正体はコードからは確定できない（推測の候補: GPU 変形経路 `BezierRailMeshGpu.ApplyPropertyBlock` が破棄済みメッシュに触れる。未検証）
- Discord: https://discord.com/channels/888315641709330433/1559935844129968149 （本件）、https://discord.com/channels/888315641709330433/1559209564363161740 （同症状の先行報告）。新版（10/15 0:52 配信）以降は「固まらないが破壊だけ効かない・橋脚も壊せない」（lanQube）、問題の橋脚は本来置けない座標にあった（maitan）

---

### Task 1: 戻し処理の例外で破壊モードが詰まないようにする

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/SingleHoverDeletePreview.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteTargetLiveness.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DeleteObjectService.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/DragDeleteSelection.cs:84-134`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/UIState/Fakes/FakeDeleteTarget.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/UIState/DragDelete/ResetFailureRecoveryTest.cs`
- Modify (Test): `moorestech_client/Assets/Scripts/Client.Tests/UIState/UIStateControlTest.cs`

**Interfaces:**
- Consumes: `IDeleteTarget`（既存。`SetRemovePreviewing()` / `ResetMaterial()` / `IsRemovable(out LocalizationKey?)`）
- Produces: `public static class DeleteTargetLiveness` — `static bool IsAliveOrWarn(IDeleteTarget target, string destroyedWarning)`（破棄済みなら警告ログを出して false）／ `public class SingleHoverDeletePreview` — `IDeleteTarget Current { get; private set; }` / `void Hover(IDeleteTarget hovered)`（hovered は非 null）/ `void Release()`

- [ ] **Step 1: テスト用 fake に「戻し処理で例外」を足す**

`FakeDeleteTarget.cs` の `ResetMaterial` を次に置き換え、フィールドを1つ足す:

```csharp
        // trueなら戻し処理で例外を投げる（実機の破棄済みレールでのNREを模す）
        // When true, the reset throws (mimics the NRE seen on a broken rail in the player build)
        public bool ThrowOnReset;

        public void ResetMaterial()
        {
            ResetCount++;
            if (ThrowOnReset) throw new System.InvalidOperationException("reset failed");
        }
```

- [ ] **Step 2: テストを書く**

`ResetFailureRecoveryTest.cs`:

```csharp
using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.Undo;
using Client.Game.InGame.UI.UIState.State.DragDelete;
using Client.Tests.BuildUndo;
using Client.Tests.UIState.Fakes;
using NUnit.Framework;

namespace Client.Tests.UIState
{
    public class ResetFailureRecoveryTest
    {
        [Test]
        public void HoverChangeDoesNotRetryFailingReset()
        {
            var preview = new SingleHoverDeletePreview();
            var broken = new FakeDeleteTarget { ThrowOnReset = true };
            var next = new FakeDeleteTarget();
            preview.Hover(broken);

            // 1回目の差し替えは例外になるが参照は手放されている
            // The first swap throws, but the reference has already been released
            Assert.Throws<InvalidOperationException>(() => preview.Hover(next));
            Assert.IsNull(preview.Current);

            // 次フレームの同じ差し替えは成功し、壊れた対象へ再び触れない
            // The same swap on the next frame succeeds without touching the broken target again
            preview.Hover(next);
            Assert.AreSame(next, preview.Current);
            Assert.AreEqual(1, broken.ResetCount);
            Assert.AreEqual(1, next.SetPreviewCount);
        }

        [Test]
        public void ReleaseAfterFailingResetIsNoOp()
        {
            var preview = new SingleHoverDeletePreview();
            var broken = new FakeDeleteTarget { ThrowOnReset = true };
            preview.Hover(broken);

            // モード離脱の片付けは2回目で必ず通る
            // Mode-exit cleanup always passes on the second attempt
            Assert.Throws<InvalidOperationException>(() => preview.Release());
            Assert.DoesNotThrow(() => preview.Release());
            Assert.AreEqual(1, broken.ResetCount);
        }

        [Test]
        public void HoveringSameTargetDoesNotReapply()
        {
            var preview = new SingleHoverDeletePreview();
            var target = new FakeDeleteTarget();
            preview.Hover(target);
            preview.Hover(target);
            Assert.AreEqual(1, target.SetPreviewCount);
            Assert.AreEqual(0, target.ResetCount);
        }

        [Test]
        public void CancelSelectionClearsEvenWhenResetThrows()
        {
            var selection = new DragDeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender());
            var broken = new FakeDeleteTarget { Removable = true, ThrowOnReset = true };
            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(broken, out _));

            // 例外が出ても選択は空になり、2回目のキャンセルは触れない
            // The selection is emptied despite the throw, and a second cancel touches nothing
            Assert.Throws<InvalidOperationException>(() => selection.CancelSelection());
            Assert.IsFalse(selection.HasSelection());
            Assert.IsFalse(selection.CanCommit());
            Assert.DoesNotThrow(() => selection.CancelSelection());
            Assert.AreEqual(1, broken.ResetCount);
        }

        [Test]
        public void CommitDeleteDoesNotResendWhenResetThrows()
        {
            var selection = new DragDeleteSelection(new BuildOperationHistory(), new FakeRemovalRestoreSender());
            var broken = new FakeDeleteTarget { Removable = true, ThrowOnReset = true };
            selection.BeginDrag();
            Assert.IsTrue(selection.TryAddTarget(broken, out _));

            // 確定で例外が出ても選択は空になり、再確定で二重送信しない
            // A throw during commit still empties the selection, so a retry never resends
            Assert.Throws<InvalidOperationException>(() => selection.CommitDelete());
            Assert.IsFalse(selection.HasSelection());
            selection.CommitDelete();
            Assert.AreEqual(1, broken.DeleteCount);
        }
    }
}
```

（`FakeRemovalRestoreSender` は既存の `Client.Tests.BuildUndo` のもの。`DestroyedSelectionTest.cs` と同じ使い方）

- [ ] **Step 2b: 離脱処理が1回投げても次の要求で抜けられることを `UIStateControlTest` に足す**

`StubUIState` にフィールドを1つ足し、`OnExit` を次に置き換える:

```csharp
            // trueなら最初のOnExitだけ例外を投げる（戻し処理の失敗を模す）
            // When true, only the first OnExit throws (mimics a failing preview reset)
            public bool ThrowOnFirstExit;

            public void OnExit()
            {
                ExitCount++;
                if (ThrowOnFirstExit && ExitCount == 1) throw new System.InvalidOperationException("exit failed");
            }
```

テストを1本足す:

```csharp
        [Test]
        public void 離脱処理が1回失敗しても次の要求で抜けられる()
        {
            var deleteLikeState = new StubUIState(UIStateEnum.BuildMenu, false) { ThrowOnFirstExit = true };
            var nextState = new StubUIState(null, false);
            var control = _controlObject.AddComponent<UIStateControl>();
            control.Construct(CreateDictionary(deleteLikeState, nextState), new FakePlayerObjectController());
            control.Initialize(UIStateEnum.GameScreen, new UITransitContext(UIStateEnum.GameScreen));

            // 投げたフレームは遷移しない
            // The frame that throws does not transition
            Assert.Throws<TargetInvocationException>(() => InvokeUpdate(control));
            Assert.AreEqual(UIStateEnum.GameScreen, control.CurrentState);

            // 次の要求で抜けられる
            // The next request leaves the state
            InvokeUpdate(control);
            Assert.AreEqual(UIStateEnum.BuildMenu, control.CurrentState);
            Assert.AreEqual(1, nextState.EnterCount);
        }
```

（`InvokeUpdate` は reflection 経由なので、`OnExit` の例外は `TargetInvocationException` に包まれて出る）

- [ ] **Step 3: 破棄済み判定ヘルパーと `SingleHoverDeletePreview` を作る**

`DeleteTargetLiveness.cs`（現在 `DragDeleteSelection` の2か所に同形で書かれている判定を1つにまとめる）:

```csharp
using UnityEngine;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    // 削除対象が破棄済みかを判定し、破棄済みなら理由をログに残す
    // Checks whether a delete target was destroyed and logs why it is skipped
    public static class DeleteTargetLiveness
    {
        public static bool IsAliveOrWarn(IDeleteTarget target, string destroyedWarning)
        {
            if (target is Object unityTarget && unityTarget == null)
            {
                Debug.LogWarning(destroyedWarning);
                return false;
            }
            return true;
        }
    }
}
```

`SingleHoverDeletePreview.cs`:

```csharp
using Client.Game.InGame.UI.UIState.State;

namespace Client.Game.InGame.UI.UIState.State.DragDelete
{
    // 単体ホバーの赤プレビュー対象を1件持つ。戻しが例外でも毎フレーム再発しないよう参照を先に手放す
    // Holds the single-hover preview target; drops the reference before resetting so a failing reset never repeats every frame
    public class SingleHoverDeletePreview
    {
        public IDeleteTarget Current { get; private set; }

        public void Hover(IDeleteTarget hovered)
        {
            if (Current == hovered) return;

            // 旧対象を戻してから新対象を赤くする
            // Reset the previous target, then paint the new one red
            Release();
            Current = hovered;
            hovered.SetRemovePreviewing();
        }

        public void Release()
        {
            if (Current == null) return;

            // 戻し処理の失敗で参照が残ると次フレームも同じ例外になるため先に外す
            // Detach first: a reference left behind by a failing reset would throw again next frame
            var previous = Current;
            Current = null;
            if (!DeleteTargetLiveness.IsAliveOrWarn(previous, "[DragDelete] hovered target was destroyed before reset")) return;
            previous.ResetMaterial();
        }
    }
}
```

- [ ] **Step 4: `DeleteObjectService` を `SingleHoverDeletePreview` へ置き換える**

フィールド `private IDeleteTarget _deleteTargetObject;` を `private readonly SingleHoverDeletePreview _hoverPreview = new();` に置き換え、3か所を次のとおり変える。

`HandleDragStart` の所有権受け渡し（現 57-63 行）:

```csharp
                // 単体プレビューの所有権を選択側へ渡す
                // Hand off preview ownership from single-hover to the selection
                _hoverPreview.Release();
```

`UpdateSingleHoverPreview`（現 91-116 行）:

```csharp
            void UpdateSingleHoverPreview()
            {
                // ホバー対象が変われば旧を戻し新を表示する
                // Swap preview when the hovered target changes
                if (hovered != null) _hoverPreview.Hover(hovered);
                else _hoverPreview.Release();

                // 削除不可な対象は理由ツールチップだけ表示する
                // For a non-removable target only show the denial tooltip
                var current = _hoverPreview.Current;
                if (current != null && !current.IsRemovable(out var reason))
                {
                    ShowDenyReason(reason);
                }
            }
```

`HandleRelease`（現 118-124 行）— 確定が例外でもドラッグ状態を残さない:

```csharp
            void HandleRelease()
            {
                if (!InputManager.Playable.ScreenLeftClick.GetKeyUp) return;

                // 確定が例外でもドラッグ状態を残さないよう先に下ろす
                // Lower the drag flag first so a throwing commit cannot leave the drag alive
                var shouldCommit = _isDragging && _selection.CanCommit();
                _isDragging = false;
                if (shouldCommit) _selection.CommitDelete();
            }
```

`CancelSelection`（現 150-164 行）— ツールチップ・ドラッグ状態の片付けを、例外が出得る戻し処理より前に置く:

```csharp
        public void CancelSelection()
        {
            // 例外が出得る戻し処理より先に自分の状態を片付ける
            // Clear own state before the resets that may throw
            _tooltip.Hide(_tooltipOwner);
            _isDragging = false;

            // 選択とホバー中の赤プレビューを戻す
            // Reset the selection and any single-hover red preview
            _selection.CancelSelection();
            _hoverPreview.Release();
        }
```

- [ ] **Step 5: `DragDeleteSelection` のキャンセル・確定を「先に空にする」順へ変える**

`CancelSelection`（現 84-99 行）:

```csharp
        public void CancelSelection()
        {
            // 戻し処理が例外でも選択が残らないよう、先に取り出して空にする
            // Take and empty the selection first so a throwing reset cannot leave it behind
            var targets = new List<IDeleteTarget>(_selectedTargets.Values);
            _selectedTargets.Clear();
            _canceled = true;
            AimFilter = DeleteAimFilter.Frontmost;

            foreach (var target in targets)
            {
                if (!DeleteTargetLiveness.IsAliveOrWarn(target, "[DragDelete] selected target was destroyed before cancel")) continue;
                target.ResetMaterial();
            }
        }
```

`CommitDelete`（現 103-134 行）— `committed` を作った直後に選択を空にし、「全件 `Delete()` → Undo 記録を積む → 全件 `ResetMaterial()`」の順に分ける:

```csharp
        public void CommitDelete()
        {
            if (_canceled) return;

            // 削除送信後に端点が消え得るため撤去物を先に記録する
            // Record removed objects before sends can remove their endpoints
            var committed = new List<IDeleteTarget>();
            foreach (var target in _selectedTargets.Values)
            {
                if (!DeleteTargetLiveness.IsAliveOrWarn(target, "[DragDelete] selected target was destroyed before commit")) continue;
                committed.Add(target);
            }
            var record = RemoveOperationRecord.CreateFrom(committed, _restoreSender);

            // 戻しが例外でも再確定で二重送信しないよう先に空にする
            // Empty the selection first so a throwing reset cannot cause a duplicate send on retry
            _selectedTargets.Clear();
            AimFilter = DeleteAimFilter.Frontmost;

            // 全件を送ってからUndo履歴を積み、記録と送信済み集合を一致させる
            // Send all deletions, then push the undo record so it matches exactly what was sent
            foreach (var target in committed) target.Delete();
            if (record.HasRemovedObjects) _buildOperationHistory.Push(record);

            // Delete はサーバー往復の非同期なので即座に赤プレビューだけ戻す（例外を出し得る処理を最後に置く）
            // Delete is async over the server, so clear the red preview now (the throwable step goes last)
            foreach (var target in committed) target.ResetMaterial();
        }
```

- [ ] **Step 6: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.UIState|Client.Tests.BuildUndo"`
Expected: ErrorCount 0 / 全 PASS（既存の DragDelete・Undo 系テストが退行しないこと）

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/DragDelete/ moorestech_client/Assets/Scripts/Client.Tests/UIState/
git commit -m "fix: 破壊モードで赤プレビューの戻しが例外を出しても毎フレーム再発せず抜けられるようにする"
```

---

### Task 2: テスターのセーブで現行 master の挙動を再現・観測する

**Files:**
- Create: `docs/research/2026-10-15-rail-delete-hover-nre/report.md`（観測記録。スクショは同ディレクトリへ）

**Interfaces:**
- Consumes: Task 1 の修正（例外が出ても抜けられるので、再現中に Editor が詰まない）
- Produces: `report.md` の「## 結論」節に、次の3つを証拠付きで書く — (A) ホバーで NRE が出るか・出たなら行番号付きスタックと null だった値、(B) レール／橋脚が破壊できない症状が出るか・出たならどの判定で拒否されたか（`IsRemovable` の `DeleteDeniedReason` 値やサーバー側ログ）、(C) 該当橋脚の座標と、その位置が現行の設置判定で置けない理由

- [ ] **Step 1: セーブを用意する**

NRE が出ていた時点のセーブは残っていない（1本目の zip は `map.json`・`world.json` だけで `save.json` を含まないことを確認済み）。テスターのセーブは Google Drive から取得済み（`https://drive.google.com/file/d/1eIeRXyIQ2_yr_DbmONivrGJOUYFedWGG/view`、中身は `5/save.json`。10/15 01:06 保存・再現しなくなった後のもの）。地形の `map.json` / `world.json` は1本目（`https://drive.google.com/file/d/10Jv0kh9l7mp6VY_FK8w8XZl9wFFDefd5/view`）に入っている。

```bash
D=/Users/sakastudio/hermes-agent/data/worktrees/moorestech/rail-delete-hover-freeze/docs/research/2026-10-15-rail-delete-hover-nre
mkdir -p $D/save && cd $D/save
curl -sL -o save.zip "https://drive.usercontent.google.com/download?id=1eIeRXyIQ2_yr_DbmONivrGJOUYFedWGG&export=download&confirm=t"
curl -sL -o map.zip "https://drive.usercontent.google.com/download?id=10Jv0kh9l7mp6VY_FK8w8XZl9wFFDefd5&export=download&confirm=t"
unzip -o save.zip && unzip -o map.zip
```

`save/` はコミットしない（`.gitignore` に追加しない。`git add` 時に report.md とスクショだけを指定する）。

- [ ] **Step 2: セーブを読み込んで起動する**

先に master データの互換を確かめる。テスターのビルドは 10/15 0:52 配信版なので、その配信ビルドの元コミット（Steam の playtest ブランチ。`bd show moorestech-j8t6x.1` や配布ビルドの記録で確認）の `.moorestech-external-revisions.json` のピンと、この worktree のピンを比べ、異なる場合は差分にセーブが参照するブロック・アイテム GUID の削除が無いかを見る。スキーマ不整合は `MooresmasterLoaderException` で初期化が止まるので、ロード失敗時は migration より先にこれを疑う。

`GeneratedWorldPlayModeSettings.WorldDirectoryPath`（`moorestech_client/Assets/Scripts/Client.Starter/Editor/GeneratedWorldPlayModeSettings.cs:24`、`world_generated`）へ `save.json`・`map.json`・`world.json` を置き、生成ワールドの再生で起動する。置き場の実パスは `uloop execute-dynamic-code` で `Client.Starter.Editor.GeneratedWorldPlayModeSettings.WorldDirectoryPath` を返して確かめる。既存の `world_generated` があれば `world_generated.bak-20261015` へ退避してから置く（消さない）。旧版セーブからの変換は `SaveMigrationChain` がロード時に行うので、ロード時のログ（`uloop get-logs --project-path ./moorestech_client`）に migration の失敗が無いことを先に確かめる。ロードに失敗したら、そのログ行を report.md に書いて Task 2 を止め、`bd note moorestech-cdddu` に残す。

- [ ] **Step 3: 該当橋脚へ移動し、破壊モードでホバー・破壊を試す**

場所はスクショ `https://i.gyazo.com/0bc802345cfd3158188a5c33165b88e6.jpg` の「左のレールの橋脚」。unity-playmode-recorded-playtest スキルのプレイテストDSLで、(1) プレイヤーを橋脚の近くへ移動、(2) G で破壊モード、(3) 橋脚に載ったレールの端・中央・橋脚本体へ順にカーソルを合わせ、(4) 左クリックで破壊、(5) G で抜ける、を録画付きで通す。DSL のワールド対象へのクリックは `ClickPlace`（設置用）と `SemanticInput.Click` しか確認できていないので、破壊モードの単クリックが打てなければ、DSL に op を足すのではなく `uloop execute-dynamic-code` で `DeleteTargetRail.IsRemovable` を直接評価して拒否理由を取り、クリック経路は手動録画（`uloop screenshot`）で補う。座標は `save.json` の橋脚ブロック（blockId を master の橋脚 GUID で引く）から求め、report.md に書く。

- [ ] **Step 4: 観測して記録する**

- `uloop get-logs --project-path ./moorestech_client --log-type Error` で NRE の有無と行番号付きスタックを取る。NRE が出たら、該当フレームで `uloop execute-dynamic-code` を使い `BezierRailChain` の `_useGpuDeform`、セグメント数、各 `BezierRailMesh` の生存（`== null`）、`RailChainRemovePreview._chain` を読んで、null の正体を値として記録する
- 破壊できなかった場合は、`DeleteTargetRail.IsRemovable` の拒否理由（`DeleteDeniedReason`）と、送信後のサーバー側ログ（`[RailDelete]` や切断拒否の行）を記録する
- 「本来置けない座標」については、同じ位置に橋脚を置こうとしたときの拒否理由（「地形に埋まっています」等）を記録する
- Task 1 の修正後なので、NRE が出ても G で抜けられることを録画で確認する（R2 の実プレイ確認）

- [ ] **Step 5: 結論を書いてコミットする**

report.md の `## 結論` に (A)(B)(C) を「観測した事実（ログ行・値・スクショ）」と「推測」を分けて書く。原因を言い切るのは証拠がある場合だけ。

```bash
git add docs/research/2026-10-15-rail-delete-hover-nre/report.md docs/research/2026-10-15-rail-delete-hover-nre/*.png
git commit -m "docs: 破壊モードのレールホバーNREをテスターのセーブで再現・観測した記録"
bd note moorestech-cdddu "Task2 観測: <結論の要約1行>"
```

---

### Task 3: 特定できた原因を直す（Task 2 で証拠付きで特定できた場合のみ）

**Files:**（Task 2 の結論で決まる。原因が出たファイルと、その挙動を固定するテスト）

**Interfaces:**
- Consumes: Task 2 の `report.md` の結論
- Produces: 原因の修正と、その入力状態を再現するテスト

- [ ] **Step 1: 実行判定**

report.md の (A) または (B) に、観測した証拠（スタックの行番号と null だった値、または拒否理由の値）があるか確認する。無ければ Task 3 は実行せず、`bd note moorestech-cdddu "Task3 見送り: 原因未確定（Task2 で再現せず）"` を残して Task 4 へ進む。PR 本文に「原因本体は未確定・詰みのみ解消」と明記する。

- [ ] **Step 2: 証拠がある場合は、修正方針を判断記録に足してから直す**

修正は「null を握りつぶすガード」ではなく、null を生んだ状態遷移を直す。方針を本 plan の `## 判断記録（ADR）` に1行で追記し（出所: Task 2 report.md）、原因の入力状態を作るテスト（`moorestech_client/Assets/Scripts/Client.Tests/RailGraph/` 配下、既存の `DeleteTargetRailPreviewTest.cs` の作りに倣う）を書いてから修正する。挙動の変更（破壊可否の判定を変える等）に及ぶ場合は、moores-grill-with-docs の対象なので実装せず、report.md に方針案を書いて bd note に残し、PR 本文の「残課題」に載せる。

- [ ] **Step 3: コンパイル・テスト・Task 2 の再現手順で消えたことを確認してコミットする**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.(RailGraph|UIState)"` → Task 2 Step 3 のシナリオを再実行し、NRE（または破壊不可）が出ないことを録画で確認
Expected: ErrorCount 0 / 全 PASS / 録画で破壊・離脱が成功

```bash
git add <修正したファイル> <テスト>
git commit -m "fix: <特定した原因>"
```

---

### Task 4: moores-code-review で全ブランチレビュー

- [ ] 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー反映が判定経路・条件式・評価時点に触れたら、Task 2 Step 3 の録画シナリオを反映後のバイナリで再実施してから完了とする

### Task 5: PR 作成とセッション終了可能状態

- [ ] セッション終了可能状態にすること: pr-create スキルで PR を作成し、master とのコンフリクトがあれば master をマージして解消・コンパイル確認のうえ push する（解消の実作業は pr-create 経由で opus subagent に委譲される）。全作業がコミット・push 済みで、このセッションをそのまま閉じても PR がマージ可能な状態になっていることを確認して終える。PR 本文に Discord スレッド（上記2件）と bd `moorestech-cdddu` を張る

---

## 判断記録（ADR）

- 設計 ADR: 該当なし（不具合修正。AGENTS.md「明白なクラッシュ修正」として grill 対象外）
- 詰みの解消（Task 1）と原因の修正（Task 3）を分けた。原因未確定のまま候補箇所へ null ガードを足すと、外れていても症状が消えて見え、原因が埋もれるため。Task 1 は原因が何であっても有効
- 単体ホバーの保持を `SingleHoverDeletePreview` に切り出した。「参照を先に手放してから戻す」順序を呼び出し側3か所で守らせるより、1か所に閉じた方が再発しにくく、テストも `DeleteObjectService` の入力・レイキャスト依存なしで書けるため
- try-catch で戻し処理を囲む案は採らない。AGENTS.md が UI 層での try-catch を禁止しており、順序の変更だけで「例外は最大1回」を満たせるため
- `CommitDelete` は「全件 `Delete()` → Undo 記録を積む → 全件 `ResetMaterial()`」に分けた。4つの `Delete()` 実装（`DeleteTargetRail.cs`・`BlockGameObjectChild.cs`・`TrainCarEntityChildrenObject.cs`・`ConnectionLineDeleteTarget.cs`）はいずれもサーバーへ送るだけで同期的な破棄をしないので、交互に並べる理由が無い。例外を出し得る戻し処理を最後に置くことで、Undo 記録＝送信済み集合が常に成り立つ（出所: シミュレーター予測→plan へ適用 2026-10-15）
- unityプレイ録画テスト: Task 2・Task 3 で実施する。Task 1 単独では実プレイで戻し処理の例外を起こす手段が無いため、ユニットテストで状態を固定し、R2 の実プレイ確認は Task 2 Step 4 で再現できた場合に行う
- 破棄済み判定＋警告ログを `DeleteTargetLiveness.IsAliveOrWarn` 1本へまとめ、単体ホバーの戻し・選択のキャンセル・確定の3か所で共用する。既存2か所の重複と、新設の単体ホバー戻しに判定が無い経路を同時に消す（Phase 2.6 弱2件。出所: シミュレーター予測・確信高→前提宣言〈拒否権つき〉2026-10-15）。警告文言は既存テスト（`DestroyedSelectionTest`）が照合する文字列をそのまま保つ
- Phase 2.6 第3バケツ（本PR外のリファクタ提案）: 赤プレビューの要求者方式 `RailChainRemovePreview`/`RemovePreviewRequests`/`IRemovePreviewable` と、単体ホバー保持 `SingleHoverDeletePreview` の役割が重なる。どちらを正本にするかは本PRでは扱わず、PR本文の「本PR外のリファクタ提案」節に1行で載せる

- Task 1 再レビュー対応: Releaseで参照を外すだけでは入力なしフレームの同対象再HoverでG離脱失敗が再発する。SingleHoverDeletePreviewはReset直前に未完了対象集合へ登録し、正常終了した対象だけ除く。未完了対象はサービス寿命中に再保持せず、対象ごとの初回抑止を警告する。複数対象の連続失敗も保持し、正常終了した対象の再Hoverは許可する。根拠: `.superpowers/sdd/task-1-review.md` Important 1 の静的故障シナリオ。追加テストで離脱失敗→入力なし同対象Hover→再離脱とA/B連続失敗を固定する（Unity実行は本体担当）。

- Task 1 共有回復状態: 選択CancelSelection/CommitDeleteのReset失敗から単体Hoverへ渡る経路もR1/R2の対象となるため、未完了集合・抑止警告・Reset実行をDeletePreviewResetRecoveryへ集約する。DeleteObjectServiceが1インスタンスを作りSingleHoverDeletePreviewとDragDeleteSelectionへ必須constructor注入する。選択へ追加すること自体は維持し、失敗対象の赤表示再要求とReset再実行だけを抑止する。根拠: task-1-review.md再レビューImportant 2。
