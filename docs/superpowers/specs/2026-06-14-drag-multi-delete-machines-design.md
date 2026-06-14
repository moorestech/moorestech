# ドラッグまとめ削除（破壊モードの複数選択）設計

## 概要 / Overview

破壊モード中に、左ドラッグでカーソルが触れた機械（ブロック）を「なぞって」まとめて選択し、
マウスを離すと選択した全ブロックを一括削除する。ドラッグ中／後に ESC を押すと、
削除選択だけをキャンセルし（プレビューを解除し、離しても削除しない）、破壊モード自体は継続する。

Lets the player paint-select multiple machines by dragging in destroy mode, delete them all on
mouse-release, and press ESC to cancel only the deletion selection while staying in destroy mode.

## 対象 / Scope

- クライアント側のみ。サーバープロトコルは既存の単体 `RemoveBlockProtocol` を流用する。
- 破壊モードのステート `DeleteObjectState`（`UIStateEnum.DeleteBar`）を拡張する。

## 既存システム / Existing system

- `DeleteObjectState.GetNextUpdate()` が破壊モードのメインループ。現状は単体削除のみ。
  - 検出: `BlockClickDetectUtil.TryGetCursorOnComponent<IDeleteTarget>(out target)`（Camera ray）。
  - プレビュー: `IDeleteTarget.SetRemovePreviewing()` / `ResetMaterial()`（赤ハイライト）。
  - 削除: `IDeleteTarget.Delete()` → `BlockGameObjectChild` が単体 `RemoveBlockProtocol` を送信。
  - 削除可否: `IDeleteTarget.IsRemovable(out reason)`。
- 入力: `InputManager.Playable.ScreenLeftClick` は `GetKeyDown`/`GetKey`(押下中)/`GetKeyUp` を持つ。
  `InputManager.UI.CloseUI` が ESC、`InputManager.UI.BlockDelete` が破壊モードのトグル(G)。
- ドラッグ先行例: `CommonBlockPlaceSystem` が `GetKeyDown`→蓄積→`GetKeyUp` でまとめ設置を実装済み。

## 仕様 / Behavior

破壊モード（`DeleteObjectState`）の入力処理を次のように変更する。

1. **ホバー（非ドラッグ）**: 従来通り、カーソル下の 1 ブロックを赤プレビュー表示。
   削除不可なら拒否理由ツールチップを表示（既存挙動を維持）。
2. **ドラッグ開始**: `ScreenLeftClick.GetKeyDown`（かつ UI 上でない）で選択を開始。選択集合をクリアし、
   `_dragCanceled = false` にする。
3. **ドラッグ中**: `ScreenLeftClick.GetKey` の各フレームで、カーソル下の `IDeleteTarget` を取得。
   削除可能かつ未選択なら選択集合に追加し `SetRemovePreviewing()` で赤ハイライト。
   削除不可なら追加せず拒否理由ツールチップを表示。HashSet 同等で重複は無視。
4. **離す（削除実行）**: `ScreenLeftClick.GetKeyUp` 時、`_dragCanceled` が false なら選択集合の全 `IDeleteTarget`
   に対し `Delete()` を呼ぶ（＝各ブロックが既存の単体削除プロトコルを送る）。実行後に選択集合・ドラッグ状態をリセット。
5. **ESC（選択キャンセル）**: `CloseUI.GetKeyDown` で、選択集合の全要素を `ResetMaterial()` で戻して集合をクリアし、
   `_dragCanceled = true` にする（押下中の指を離しても手順4で削除しない）。**破壊モードは継続**（ステート遷移しない）。
   選択が無いときに押しても無害（何もしない）。
6. **モード終了/遷移**: 破壊モードの終了は破壊トグル（`BlockDelete` / G）で行う。B で設置モード、Tab でインベントリ。
   **ESC ではモードを抜けない**（要件: 破壊ステートはそのまま）。

> **入力bindの注意（実機playtestで判明）**: `<Keyboard>/escape` は `OpenMenu` と `CloseUI` の両アクションに bind されている。
> そのため破壊モードでは `HandleTransition` で `OpenMenu`→`PauseMenu` を拾ってはならない（拾うと ESC が常にポーズへ遷移し、
> `CloseUI` による選択キャンセルが到達不能＝デッドコードになる）。破壊モード中の ESC は選択キャンセル専用とし、ポーズは開かない。

単体クリック削除は「1 要素のドラッグ（押下で1個選択→離して削除）」として自然に成立するため、専用経路は設けない。

## 構成 / Architecture

責務を分離し `DeleteObjectState` を肥大化させないため、選択モデルを別クラスに切り出す。

### 新規 `DragDeleteSelection`（選択モデル, Client.Game）

ファイル: `.../UI/UIState/State/DragDelete/DragDeleteSelection.cs`

純粋な選択ロジック（raycast を持たない＝テスト可能）。`IDeleteTarget` を直接受け取る。

- `bool IsCanceled { get; }` … は使わず内部フラグで管理（getter/setter禁止規約のため `SetXxx` で操作）。
- `void AddTarget(IDeleteTarget target)` … 削除可能(`IsRemovable`)かつ未選択なら追加し `SetRemovePreviewing()`。
- `void CancelSelection()` … 全要素 `ResetMaterial()` → クリア → キャンセル済みフラグを立てる。
- `void CommitDelete()` … 全要素 `Delete()` を呼び、`ResetMaterial()` で見た目も戻し、クリア。
- `void BeginDrag()` … 集合クリア＋キャンセルフラグ解除（ドラッグ開始時）。
- `bool CanCommit()` … キャンセルされていなければ true。
- `int SelectedCount()` … 選択数（テスト・デバッグ用）。
- 重複判定は `HashSet<IDeleteTarget>`（参照同一性）で行う。

### 改修 `DeleteObjectState`

ドラッグ入力を解釈し `DragDeleteSelection` に委譲する。ホバープレビューは非ドラッグ時のみ動かし、
ドラッグ中は選択集合のプレビューと競合しないようにする。ESC は選択キャンセルに振り替え、
キー説明テキスト（`KeyControlDescription`）を更新する。

### サーバー / Server

変更なし。一括削除は `Delete()` を選択数ぶんループするだけ（既存の検証済みサーバー処理を再利用）。
部分的な失敗（列車が乗ったレール等）は各ブロック独立に拒否理由が出る。後方互換・性能は考慮不要の方針に従う。

## エラー処理 / Error handling

- 削除不可ターゲットは選択集合に入れない（拒否理由はツールチップ表示）。
- `Camera.main` が無い／ray が外れたフレームは何も選択しない（既存 util がガード済み）。
- ドラッグ開始時に `EventSystem.current.IsPointerOverGameObject()` なら開始しない（UI 誤クリック防止、設置系と同様）。

## テスト / Testing

`Client.Tests`（EditMode, `Client.Game` 参照可）に `DragDeleteSelection` の単体テストを追加。
`IDeleteTarget` のフェイク実装で `SetRemovePreviewing`/`ResetMaterial`/`Delete`/`IsRemovable` の呼び出しを記録し検証する。

- 追加で赤プレビューが付く／重複追加されない
- 削除不可ターゲットは追加されない
- `CancelSelection` で全要素がリセットされ集合が空になり、`CanCommit()` が false
- `CommitDelete` で全要素 `Delete()` が呼ばれ集合が空
- `BeginDrag` でキャンセルフラグが解除され再び `CanCommit()` が true

## 証拠動画 / Proof video

PlayMode 不要の手法（execute-dynamic-code + RenderTexture + PNG + ffmpeg, demo-video-via-dynamic-code 参照）で、
実ブロックを複数設置 → `DragDeleteSelection` になぞり選択（赤化）→ ESC でキャンセル（赤が戻る・モード継続）→
再選択して離す（ブロック消滅）の一連を録画し、要件3点（まとめ選択／まとめ削除／ESCキャンセルでモード継続）を可視化する。
