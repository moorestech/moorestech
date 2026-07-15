# ブロックスポイト（Pick Block）機能 設計

## 概要
建設中およびゲーム画面中に、カーソル先のブロックをミドルクリックすると、そのブロックの種類（BlockId）と向き（BlockDirection）を建設メニューの選択（`PlacementSelection`）にコピーする。Minecraft のクリエイティブモードの「ブロック選択（pick block）」に相当する。

- 有効範囲: 配置モード（`PlaceBlock`）中と通常プレイ（`GameScreen`）中の両方。
- ピック時にブロックの向き（回転）もコピーする。
- サーバー通信なし。純クライアント UI 操作で、選択モデルを書き換えるだけ。

## データフロー
```
UIStateControl.Update（毎フレーム）
  → 各 UIState.GetNextUpdate
    → BlockPickService.ManualUpdate  ← ミドルクリック検知＋レイキャスト解決を内包
      → PlacementSelection（共有選択モデル）へ書き込み（BlockId ＋ BlockDirection）
        → PlaceSystemStateController が IsSelectionChanged を検知
          → CommonBlockPlaceSystem が向き/BlockId を反映
            → 設置プレビュー / 設置挙動
```
本機能の実体は「共有選択モデル（`PlacementSelection`）への書き手が1人増える」だけであり、既存の一方向配置パイプラインに相乗りする。下流（PlaceSystem）へ制御を戻す `bool` 戻り値・選択モデルを迂回する第2の書き込み経路は設けない。

## コンポーネント

### 1. `BlockPickService`（新規, Client.Game）
- 何をするか: 毎フレーム `ManualUpdate()` で駆動され、ミドルクリック検知・レイキャスト解決・選択モデル書き込みを内部で完結する。
- 使い方: `PlaceBlockState` と `GameScreenState` の両方が毎フレーム `ManualUpdate()` を呼ぶ。`TryXxx() bool` 戻り値でステート側にクリック判定・分岐を書かせない（先行同族: `PlaceSystemStateController.ManualUpdate()` / `BuildViewModeController.ManualUpdate()`）。
- 依存: `PlacementSelection`（書き込み先）、`BlockClickDetectUtil`（レイキャスト）、`HybridInput`（入力）。VContainer で注入。
- 挙動:
  1. `HybridInput.GetMouseButtonDown(2)`（ミドルクリック）でない、または `EventSystem.current.IsPointerOverGameObject()` なら早期 return。
  2. `BlockClickDetectUtil.TryGetCursorOnBlock(out BlockGameObject block)` が false（空・非ブロック）なら何もしない（選択を消さない・遷移しない）。
  3. `block.BlockId` と `block.BlockPosInfo.BlockDirection` を読み、`PlacementSelection.SetSelectedBlock(blockId, direction)` を呼ぶ。
  4. ピック成立を UniRx イベント `IObservable<Unit> OnPicked` で通知する（GameScreen の遷移用。C# 標準 event ではなく UniRx）。

### 2. `PlacementSelection`（既存を拡張）
- `BlockDirection SelectedBlockDirection`（`{ get; private set; }`）を追加。
- `SetSelectedBlock(BlockId)` を `SetSelectedBlock(BlockId, BlockDirection)` へ変更（デフォルト引数は使わない）。通常のメニュー選択（`BuildMenuState`）は呼び出し側で `BlockDirection.North` を渡すよう変更（コンパイルエラー駆動で漏れ検出）。
- 他の選択種別（TrainCar/ConnectTool/Blueprint 等）を選んだ際は `SelectedBlockDirection` を `North` にリセットする。

### 3. `PlaceSystemUpdateContext` / `PlaceSystemStateController`（既存を拡張）
- `PlaceSystemUpdateContext` に readonly フィールド `BlockDirection SelectedBlockDirection` を追加。
- `PlaceSystemStateController.CreateContext()` の `IsSelectionChanged` 比較に `SelectedBlockDirection` を含める。前フレームキャッシュ `_lastSelectedBlockDirection` を追加。
  - これにより「同一 BlockId・異なる向き」の再ピックでも変化検知が発火する（後述の致命ケース対策）。

### 4. `CommonBlockPlaceSystem`（既存を拡張）
- `IsSelectionChanged` が true のフレームで、`context.SelectedBlockDirection` を `_currentBlockDirection` の初期値として採用する。以降の回転キー操作はそこから継続する。
- 向きを持たない/自前で整列するブロック（ベルト・レール・歯車チェーンポール・列車・ブループリント等）は各 PlaceSystem の既存ロジックに委ね、本フィールドを参照しない（スコープ外）。

### 5. UI ステート統合
- `PlaceBlockState.GetNextUpdate()`: 既存の `_placeSystemStateController.ManualUpdate()` 呼び出しと並べて `_blockPickService.ManualUpdate()` を呼ぶ。既に PlaceBlock なので遷移は不要（選択更新だけでプレビューが更新される）。
- `GameScreenState`:
  - `GetNextUpdate()` で `_blockPickService.ManualUpdate()` を呼ぶ。
  - `OnEnter` で `_blockPickService.OnPicked` を購読し、ピック成立時に「PlaceBlock へ遷移する」フラグ（保留 `UITransitContext`）を立てる。`GetNextUpdate()` はそのフラグがあれば `new UITransitContext(UIStateEnum.PlaceBlock)` を返す。`OnExit` で購読を破棄する。
  - 遷移判定はステートの責務だが、クリック検知・対象解決・選択書き込みはサービス内で完結しており、L41 の「ステート側でクリック判定・分岐」は犯していない。

## エッジケース
- **同一 BlockId・異なる向きの再ピック（致命ケース）**: 北向きのブロックAを選択中に東向きのAをピック。`SelectedBlockId` が同一のため、変化検知に向きを含めないと `IsSelectionChanged` が発火せずプレビューが北のまま。→ `PlacementSelection` と `PlaceSystemUpdateContext` の両方の変化検知比較に `BlockDirection` を含めて解決する。
- **縦向き（`Up*`/`Down*`）**: `BlockDirection` は12値（水平4・上4・下4）。水平4値に落とさず完全な `BlockDirection` を保持・コピーし、`UpNorth` に置かれたブロックのピックが `UpNorth` を再現する。
- **空・非ブロックをピック**: レイキャスト miss。何もしない（選択維持・遷移なし）。
- **UI 上でのミドルクリック**: `EventSystem.current.IsPointerOverGameObject()` で早期 return。
- **自動整列ブロック（ベルト/レール等）**: BlockId はコピーするが向きは採用しない（各 PlaceSystem の自前ロジック）。

## エラーハンドリング
- try-catch は使わない。レイキャスト miss・非ブロックは条件分岐で早期 return。
- ピック対象のブロックは既にクライアントに存在する前提（`BlockGameObjectDataStore` 経由で解決済み）のため、非同期ロード・外部データ由来の null チェックは不要。

## テスト
- クライアント側の統合検証は `unity-playmode-recorded-playtest`（プレイテスト DSL）で行う: PlaceBlock/GameScreen 各ステートでミドルクリック → 選択 BlockId・向きが更新されること、GameScreen ではピックで PlaceBlock へ遷移することを確認。
- ロジック単体では `PlacementSelection` の向き付き選択と `PlaceSystemStateController.IsSelectionChanged`（同一 BlockId・異向きで true）を検証する。

## 非スコープ
- 後方互換・パフォーマンス最適化・将来拡張性（AGENTS.md 既定）。
- ベルト/レール等の自動整列ブロックへの向き適用。
- サーバー側の変更（プロトコル・セーブ）。
- `.inputactions` アセットへの新 InputAction 追加（`HybridInput.GetMouseButtonDown(2)` で足りる）。
