# ブロックスポイト機能（ミドルクリック）設計

日付: 2026-07-08
ブランチ: feature/replace-palce-system-with-electric

## 概要

ワールドに設置済みのブロックをミドルクリックでピックし、そのブロック（と向き）を設置選択状態にする「スポイト」機能。FactorioのパイプットやMinecraftのピックブロックに相当する。完全クライアントサイドで、サーバー通信・スキーマ変更は不要。

## 発動条件と挙動

### GameScreenState（通常プレイ）
- ミドルクリックでカーソル下のブロックをピックし、`PlacementSelection` にセットして `UIStateEnum.PlaceBlock` へ遷移する
- `PlaceBlockState.OnEnter` → `BuildViewModeController.OnEnterBuildState` がセッション未開始時に自動でカメラ保存・セッション開始するため、追加のセッション処理は不要（調査で確認済み）

### PlaceBlockState（配置モード）
- ミドルクリックで選択ブロックがピック対象へ切り替わる。ステート遷移は起きない
- `PlacementSelection` を書き換えるだけで、`PlaceSystemStateController` が毎フレーム選択変化を検知し PlaceSystem（Common/ベルト/レール/歯車ポール）を自動で切り替える
- テキスト入力中ガード（`!isTextInputFocused`）の内側に置き、BP名入力中の誤爆を防ぐ

### 照準
既存の `BlockClickDetectUtil.TryGetCursorOnBlock`（`AimPointProvider` 経由、`BlockOnlyLayerMask`）を使用。GameScreenでのブロックインタラクト（`GameScreenSubInventoryInteractService`）と同じ照準セマンティクスになる。

## コンポーネント構成

### BlockPickService（新規）
両ステートに注入する1クラス。`bool TryPickBlockUnderCursor()` が以下を行い、ピック成立時 true を返す:
1. `HybridInput.GetMouseButtonDown(2)` でミドルクリック検知（既存のゲーム内ミドルクリック使用は無く衝突しない）
2. `BlockClickDetectUtil.TryGetCursorOnBlock` でカーソル下の `BlockGameObject` を取得
3. `BlockPickResolver` でピック可否と最終BlockIdを解決
4. `BlockGameObject.BlockPosInfo.BlockDirection` を取得
5. `PlacementSelection.SetSelectedBlock(blockId, direction)` を呼ぶ

### BlockPickResolver（新規・純粋ロジック）
`BlockId + IGameUnlockStateData → 最終BlockId or 失敗` の解決のみを担う静的クラス:
1. ベルト隠しバリアントなら `BeltConveyorPlaceFamilyUtil.GetRepresentativeBlockId` で代表ブロックへ変換（ビルドメニューが隠しバリアントを除外するのと整合）
2. 未解放ブロックなら失敗（スポイトで解放システムを迂回できてはならない）

## 向きのコピー

- `PlacementSelection.SetSelectedBlock(BlockId blockId, BlockDirection? direction)` に引数を追加する。デフォルト引数は禁止のため、既存呼び出し元（`BuildMenuState`）は `null` を明示する
- `PlaceSystemUpdateContext` に `SelectedBlockDirection` を追加し、選択変化検知（IsSelectionChanged）の比較対象に含める。同一ブロックを別向きでピックし直したケースを変化として拾うため
- `CommonBlockPlaceSystem` は `IsSelectionChanged` かつ direction 有りのとき `_currentBlockDirection` へ適用する
- ベルト・レール等、ドラッグ操作で向きが決まる PlaceSystem は direction を単に無視する（壊れない）

## エッジケース

| ケース | 挙動 |
|---|---|
| 空振り（ブロック無し・地形のみ） | 何もしない。現在の選択を維持（クリアしない） |
| 未解放ブロック | 無反応 |
| 多セルブロックの端をクリック | 子コライダー → `BlockGameObjectChild.BlockGameObject` で親解決（既存ユーティリティの挙動） |
| 列車・電線 | `BlockOnlyLayerMask` のためヒットせず対象外 |
| 縦向き設置ブロック（UpNorth等） | `BlockDirection` をそのままコピー |
| ベルト隠しバリアント | 代表ブロックへ変換して選択 |
| テキスト入力中（PlaceBlockState） | 既存ガードにより無効 |

## UI

キー操作説明文（`KeyControlDescription`）を更新し、GameScreen と PlaceBlock の両方へ「ミドルクリック: ブロックをスポイト」を追記する。

## テスト

- `BlockPickResolver` をユニットテスト（隠しバリアント→代表ブロック変換、未解放ブロックの拒否）
- 入力・レイキャスト・ステート遷移はプレイモード/実機で確認（`BlockPickService` は Unity の入力とカメラに依存するためユニットテスト対象外）
- 注: `PlaceSystemStateController` の選択変化検知フローは既存機能として動作しているものに乗る形であり、本機能で新規テストは追加しない

## 実装上の注意

作業ツリーが `origin/master-fable-tmp` とのマージ途中（本スペック作成時点）。本設計は HEAD 側の `BuildViewModeController` 経路を前提とするため、実装はマージ解決後に着手すること。
