# ブロックスポイト（Eyedropper）機能 設計書

- 日付: 2026-07-08
- 対象: moorestech_client
- 概要: ワールド内のブロックをマウスのミドルクリックでピックし、そのブロック種別と向き（回転）を配置選択にコピーする機能。

## 1. 目的とスコープ

### 目的
建設中に「今見えているブロックと同じものを、同じ向きで置きたい」を1操作で実現する。Minecraft のブロックピック（middle click）に相当する。

### 有効範囲
- `GameScreen`（通常プレイ中）
- `PlaceBlock`（配置モード中）

の両ステートで有効にする。

### コピーする情報
- ブロック種別（`BlockId`）
- ブロックの向き（`BlockDirection`、水平＋上下回転を包含する全12値）

### スコープ外（YAGNI）
- 列車車両（`TrainCarEntityChildrenObject`）・接続ツール・ブループリントのピック。ミドルクリックの対象はワールドに設置済みの通常ブロックのみとする。理由: `BlockClickDetectUtil.TryGetCursorOnBlock` はブロックの GameObject のみを解決し、車両は別経路（`TryGetCursorOnComponent<TrainCarEntityChildrenObject>`）で扱われる。将来必要になったら別途拡張する。
- 後方互換・パフォーマンス最適化・将来拡張性は本設計では考慮しない。

## 2. 現状（調査結果）

| 要素 | 実体 | 備考 |
|---|---|---|
| 中ボタン入力 | `HybridInput.GetMouseButtonDown(2)`（`Client.Input/HybridInput.cs`） | 既にマップ済み。既存ゲーム機能では未使用 |
| カーソル下ブロック解決 | `BlockClickDetectUtil.TryGetCursorOnBlock(out BlockGameObject)`（`Client.Game/InGame/Control/BlockClickDetectUtil.cs`） | `AimPointProvider` 経由で視点モードに応じた照準を使用。`BlockOnlyLayerMask`、距離100 |
| ブロックの種別・向き | `BlockGameObject.BlockId` / `BlockGameObject.BlockPosInfo.BlockDirection`（`BlockPositionInfo.cs`） | `BlockDirection` は全12値（`Up/Down×4` + 水平4） |
| 配置選択の共有モデル | `PlacementSelection`（`.../PlaceSystem/PlacementSelection.cs`、Singleton） | 現状**向きフィールドを持たない** |
| 配置時の向き保持 | `CommonBlockPlaceSystem._currentBlockDirection`（`:34`、初期値 `North`） | R キー等でローカルに回転。選択モデルからは受け取っていない |
| 選択変化検知 | `PlaceSystemStateController.CreateContext()`（`:65-69`） | 前フレーム値との**5フィールド比較**で `IsSelectionChanged` を算出 |
| 毎フレーム駆動ループ | `UIStateControl.Update()` → 現ステートの `GetNextUpdate()` | `PlaceBlockState` が `PlaceSystemStateController.ManualUpdate()` を毎フレーム呼ぶ |
| UI遷移機構 | `UIStateControl.Update()` が `GetNextUpdate()` の戻り値 `UITransitContext` でのみ遷移 | サービスから push はできない（pull 型） |

## 3. 設計原則との照合（B判定）

moorestech-principles の「UI入力・選択モデル」表に本機能そのものが規定されている:

- **サービス形状**: `TryXxx()` bool を返してステート側にクリック判定・分岐を書く形は不可。UIステートから毎フレーム `ManualUpdate()` 型で駆動し、入力検知（ミドルクリック）も対象検知（レイキャスト・解決）もサービス内部で行う。駆動同族は `PlaceSystemStateController.ManualUpdate()` / `BuildViewModeController.ManualUpdate()`。`GameScreenSubInventoryInteractService` の TryGet 型は「共有状態を書かない遷移判定サービス限定」であり、スポイトは `PlacementSelection` を書くため該当しない。
- **反映経路**: ピック結果（向き等の付帯情報を含む）は `PlacementSelection` への書き込み一本。向きは**フィールドを追加**し、変化検知（`IsSelectionChanged` 比較）にも含める。選択モデルを迂回して各 PlaceSystem へ直接セッターを呼ばない（SSOT）。

その他: イベントは UniRx、partial 禁止、1ファイル200行以下、default 引数禁止。

## 4. アーキテクチャ

### 4.1 データフロー

```
[中ボタン押下]
  BlockEyedropperService.ManualUpdate()   ← GameScreenState / PlaceBlockState から毎フレーム駆動
    1. HybridInput.GetMouseButtonDown(2) でないなら return
    2. EventSystem.current.IsPointerOverGameObject() なら return（UI上を除外）
    3. BlockClickDetectUtil.TryGetCursorOnBlock(out block) が false なら return
    4. PlacementSelection.SetSelectedBlock(block.BlockId, block.BlockPosInfo.BlockDirection)
    5. _onPicked.OnNext(Unit.Default)
         │
         │ (GameScreen 中のみ)
         └→ GameScreenState が OnPicked を購読 → 次の GetNextUpdate で UITransitContext(PlaceBlock) を返す
         │
         ▼ (PlaceBlock 中)
  PlaceSystemStateController.ManualUpdate() → CreateContext()
    SelectedBlockDirection を6番目の比較に追加 → IsSelectionChanged を算出
    PlaceSystemUpdateContext に SelectedBlockDirection を載せる
         ▼
  CommonBlockPlaceSystem.GroundClickControl(context)
    IsSelectionChanged && context.SelectedBlockDirection.HasValue のとき
      _currentBlockDirection = context.SelectedBlockDirection.Value
    （以降のプレビュー・設置は既存どおり _currentBlockDirection を使用）
```

### 4.2 各コンポーネント

#### (新規) `BlockEyedropperService`
- 責務: 毎フレーム自走し、中ボタンでカーソル下ブロックを検知して `PlacementSelection` にピック結果を書き込む。ピック成立を UniRx で通知する。
- 配置: `Client.Game.InGame.BlockSystem.PlaceSystem`（`PlacementSelection` と同名前空間）。
- 依存: `PlacementSelection`（コンストラクタ注入、書き込み先）。検知は静的 `BlockClickDetectUtil` / `HybridInput` / `EventSystem` を使用するため追加注入は不要。
- 公開メンバー:
  - `void ManualUpdate()`
  - `IObservable<Unit> OnPicked`（内部 `Subject<Unit>` を公開）
- DI: `MainGameStarter` で `Lifetime.Singleton` 登録。

#### (変更) `PlacementSelection`
- `BlockDirection? SelectedBlockDirection { get; private set; }` フィールドを追加。
- `SetSelectedBlock(BlockId blockId, BlockDirection blockDirection)` オーバーロードを追加（スポイト用、向きを設定）。
- 既存 `SetSelectedBlock(BlockId blockId)`（ビルドメニュー用）は `SelectedBlockDirection = null` を設定する。ビルドメニュー選択は向きを持ち込まない（配置時は `CommonBlockPlaceSystem` の現在の向きを維持）。
- `ClearSelection()` で `SelectedBlockDirection = null` にリセット。

#### (変更) `PlaceSystemUpdateContext`（`IPlaceSystem.cs` 内）
- `BlockDirection? SelectedBlockDirection` を追加（readonly フィールド＋コンストラクタ引数）。

#### (変更) `PlaceSystemStateController`
- `_lastSelectedBlockDirection` を追加し `Disable()` で `null` リセット。
- `CreateContext()` の `isSelectionChanged` 比較に `_lastSelectedBlockDirection != _placementSelection.SelectedBlockDirection` を追加（5→6フィールド）。
- `PlaceSystemUpdateContext` 生成時に `SelectedBlockDirection` を渡し、前フレーム値を更新する。

#### (変更) `CommonBlockPlaceSystem`
- `GroundClickControl(context)` の冒頭（連続設置リセット判定の近傍）で、ピック由来の向きを適用する:
  - `if (context.IsSelectionChanged && context.SelectedBlockDirection.HasValue) _currentBlockDirection = context.SelectedBlockDirection.Value;`
- 適用は「選択が変化したフレームのみ」。以降ユーザーが R キーで回転しても `IsSelectionChanged=false` なので上書きされない（ピック直後の1回だけ向きを反映）。

#### (変更) `GameScreenState`
- `BlockEyedropperService` を注入。
- コンストラクタで `_eyedropperService.OnPicked.Subscribe(...)` を購読し、ピック発生フラグを立てる（`CompositeDisposable` で保持）。
- `GetNextUpdate()` 内で `_eyedropperService.ManualUpdate()` を呼び、直後にフラグが立っていれば `new UITransitContext(UIStateEnum.PlaceBlock)` を返す（`OnPicked` は `ManualUpdate` 内で同期発火するため同フレームで遷移でき、遅延なし）。フラグは返す直前にクリアする。

#### (変更) `PlaceBlockState`
- `BlockEyedropperService` を注入。
- `GetNextUpdate()` の `!isTextInputFocused` ブロック内（`_buildViewModeController.ManualUpdate()` の近傍、`_placeSystemStateController.ManualUpdate()` より前）で `_eyedropperService.ManualUpdate()` を呼ぶ。PlaceBlock 中は遷移不要（既に配置モード）。テキスト入力フォーカス中はピックしない（BP名入力等の誤爆防止）。

#### (変更) `MainGameStarter`
- `BlockEyedropperService` を `Lifetime.Singleton` 登録。

## 5. エッジケースと処理

| ケース | 挙動 |
|---|---|
| 同一 `BlockId` で向き違いのブロックを続けてピック | `SelectedBlockDirection` を変化検知に含めるため `IsSelectionChanged=true` となり、2回目の向きも反映される（本設計の要） |
| UI 上でミドルクリック | `EventSystem.current.IsPointerOverGameObject()` で除外し no-op |
| 地面・エンティティ・列車など非ブロックをミドルクリック | `TryGetCursorOnBlock=false` で no-op |
| PlaceBlock 中のピック | 選択を書き換え、`PlaceSystemStateController` が変化検知して向きを適用。遷移なし |
| GameScreen 中のピック | 選択を書き換え、`OnPicked` 発火で PlaceBlock へ自動遷移。遷移先で向きが適用される |
| テキスト入力フォーカス中（BP名入力等）のミドルクリック | PlaceBlock 側では `!isTextInputFocused` ガードでスキップ |
| ベルト・レール等の特殊配置ブロックをピック | `PlacementSelection.SetSelectedBlock` は `SelectionType=Block` を設定。`PlaceSystemSelector` がブロックマスタの種別に応じて対応 PlaceSystem（ベルト/レール/歯車ポール/通常）へ自動振り分けする。向きの適用は `CommonBlockPlaceSystem` のみで行うため、特殊系では向きコピーは効かない（それらは独自の向き決定ロジックを持つ）。通常ブロックで向きコピーが機能すれば要件を満たす |

## 6. 自己反証（最重要反例）

**反例**: 「向き East で設置した鉄チェスト A をピック → 向き North で設置した鉄チェスト B をピック」。両者の `BlockId` は同一。
- もし向きを変化検知に含めなければ、2回目のピックで `SelectedBlockId` が不変のため `IsSelectionChanged=false` となり、`_currentBlockDirection` が更新されず **B の向き（North）がコピーされない**。
- 本設計は `PlaceSystemStateController` の比較に `SelectedBlockDirection` を追加してこれを弾く。これが「向きをフィールド追加し変化検知に含める」を必須とする根拠である。

## 7. テスト観点

- 単体（EditMode 相当が難しいクライアント入力系のため、主に PlayMode/手動確認）:
  - GameScreen で通常ブロックを中ボタンピック → PlaceBlock へ遷移し、選択が該当 `BlockId`・該当向きになる。
  - PlaceBlock 中に別ブロックを中ボタンピック → 選択と `_currentBlockDirection` が切り替わる。
  - 同 `BlockId`・向き違いを連続ピック → 2回目の向きも反映される（反例の回帰確認）。
  - UI 上・地面・エンティティでの中ボタン → 何も起きない。
- 注記: クライアントの入力・レイキャストを直接検証する自動テスト基盤は薄いため、本機能の受け入れは PlayMode（`unity-playmode-recorded-playtest` DSL）または手動確認を主とする。`PlacementSelection` の向きフィールド追加と `PlaceSystemStateController` の変化検知拡張はロジックとして分離しているため、必要ならそこだけ EditMode で検証可能。

## 8. 変更ファイル一覧

- 新規: `Client.Game/InGame/BlockSystem/PlaceSystem/BlockEyedropperService.cs`
- 変更: `Client.Game/InGame/BlockSystem/PlaceSystem/PlacementSelection.cs`
- 変更: `Client.Game/InGame/BlockSystem/PlaceSystem/IPlaceSystem.cs`（`PlaceSystemUpdateContext`）
- 変更: `Client.Game/InGame/BlockSystem/PlaceSystem/PlaceSystemStateController.cs`
- 変更: `Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs`
- 変更: `Client.Game/InGame/UI/UIState/State/GameScreenState.cs`
- 変更: `Client.Game/InGame/UI/UIState/State/PlaceBlockState.cs`
- 変更: `Client.Starter/MainGameStarter.cs`（DI 登録）
