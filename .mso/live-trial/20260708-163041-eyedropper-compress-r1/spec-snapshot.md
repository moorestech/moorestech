# ブロックスポイト機能 設計

## 概要

ワールド内の既存ブロックにカーソルを合わせてミドルクリックすると、そのブロックの
種類（BlockId）と向き（BlockDirection）を配置選択にコピーする「スポイト」機能。
Photoshop のスポイトツールに相当し、既存の建造物と同じブロックを素早く並べられる。

有効範囲は配置モード（PlaceBlock）中と通常プレイ（GameScreen）中の両方。
通常プレイ中にピックした場合は配置モードへ遷移して即座に配置可能にする。

## データフロー（この機能の立ち位置）

スポイトは既存の一方向連鎖（入力 → 共有選択モデル `PlacementSelection` → 下流が
選択から挙動を導出）に参加する。実体は「**共有モデルへの書き手が1人増える**」だけで、
新しい制御フロー・状態ストア・通信経路は導入しない。

```
HybridInput ミドルクリック(2)
  → BlockEyedropperInputService.ManualUpdate()          [入力検知＋レイキャスト解決を内包]
      → BlockClickDetectUtil.TryGetCursorOnBlock(out blockGameObject)
      → (blockGameObject.BlockId, blockGameObject.BlockPosInfo.BlockDirection)
  → PlacementSelection.SetSelectedBlock(blockId, direction)   [共有選択モデルへの書き込み一本]
  → PlaceSystemStateController.CreateContext()          [変化検知に direction を追加]
  → PlaceSystemUpdateContext.SelectedBlockDirection
  → CommonBlockPlaceSystem                              [選択変化時に _currentBlockDirection を復元]
```

## 設計判断（前提）

| 決定 | 根拠 |
|---|---|
| サービスは `TryXxx()` bool 戻りでなく毎フレーム `ManualUpdate()` 駆動。入力検知も対象検知もサービス内部 | 共有状態を書く入力サービスの規約。駆動同族: `PlaceSystemStateController.ManualUpdate()` / `BuildViewModeController.ManualUpdate()`。`TryGet` 前例(`GameScreenSubInventoryInteractService`)は共有状態を書かない遷移判定サービス限定 |
| 向きは `PlacementSelection` にフィールド追加して共有モデル経由で反映。`CommonBlockPlaceSystem` へ迂回セッターを新設しない | SSOT。向きの唯一の保持先 `_currentBlockDirection`(private, 注入口なし) を直接叩かず、既存の `PlacementSelection` → `PlaceSystemUpdateContext` 導出を拡張する |
| 向きを変化検知(`IsSelectionChanged`)の比較に含める | 同種ブロックを別向きで再ピックした際に検知漏れさせないため（下記エッジ参照） |
| 入力は `HybridInput.GetMouseButtonDown(2)` | InputSystem にミドルクリックのアクション定義が無く、`BuildViewModeController` が既にマウスボタンを `HybridInput` 直読みしている同型に乗る。新アクション定義は YAGNI |
| 新規プロトコル・状態ストア・同期経路は作らない | クライアント内で完結。サーバー送信もセーブも不要。既存共有モデルの拡張で足りる |
| GameScreen でのピック後は `UIStateEnum.PlaceBlock` へ遷移 | 選択を書くだけでは不可視で死に機能になるため。遷移シグナルはサービスの UniRx イベント `OnPicked` を GameScreenState が購読して同フレームで遷移（C#標準 event ではなく UniRx） |

## コンポーネント

### 1. `BlockEyedropperInputService`（新規）

ワールド内ブロックをミドルクリックでピックし、共有選択モデルへ書き込む入力サービス。
配置場所: `Client.Game/InGame/BlockSystem/PlaceSystem/`（`PlacementSelection` と同層）。

責務:
- 毎フレーム `ManualUpdate()` で以下を実行:
  1. `HybridInput.GetMouseButtonDown(2)` でなければ即 return。
  2. `EventSystem.current.IsPointerOverGameObject()` なら return（UI 越しのピック抑止。`GameScreenSubInventoryInteractService` と同じガード）。
  3. `BlockClickDetectUtil.TryGetCursorOnBlock(out var blockGameObject)` が false なら return。
  4. `var direction = blockGameObject.BlockPosInfo.BlockDirection;`
  5. `_placementSelection.SetSelectedBlock(blockGameObject.BlockId, direction);`
  6. `_onPicked.OnNext(Unit.Default);`（ピック発生を通知）
- 公開 API: `void ManualUpdate()` と `IObservable<Unit> OnPicked`（UniRx `Subject<Unit>` を内部保持）。

依存: `PlacementSelection`（注入）。`BlockClickDetectUtil`（static util）。`HybridInput`（static）。
戻り値・bool の TryXxx は公開しない（ステート側にクリック判定を書かせない）。

### 2. `PlacementSelection`（変更）

向きフィールドを追加する。

- 追加フィールド: `public BlockDirection? SelectedBlockDirection { get; private set; }`
  （`Game.Block.Interface.BlockDirection` を using 追加）
- `SetSelectedBlock` のシグネチャを `SetSelectedBlock(BlockId blockId, BlockDirection? blockDirection)` に変更し、
  内部で `SelectedBlockDirection = blockDirection;` を設定。
  - デフォルト引数は使わない。唯一の既存呼び出し元 `BuildMenuState:41` を
    `SetSelectedBlock(entry.BlockId, null)` に変更する（コンパイルエラー駆動で漏れ検出）。
  - direction が null のとき（ビルドメニュー選択）は下流で向きを上書きしない＝既存挙動維持。
- `ClearSelection()` に `SelectedBlockDirection = null;` を追加。

### 3. `PlaceSystemUpdateContext` / `IPlaceSystem`（変更）

- `PlaceSystemUpdateContext` に `public readonly BlockDirection? SelectedBlockDirection;` を追加し、
  コンストラクタ引数に加える。

### 4. `PlaceSystemStateController`（変更）

- 前フレーム値フィールド `_lastSelectedBlockDirection : BlockDirection?` を追加。
- `CreateContext()` の `isSelectionChanged` 計算に
  `|| _lastSelectedBlockDirection != _placementSelection.SelectedBlockDirection` を追加。
- `PlaceSystemUpdateContext` 生成に `_placementSelection.SelectedBlockDirection` を渡す。
- 末尾で `_lastSelectedBlockDirection` を更新。
- `Disable()` で `_lastSelectedBlockDirection = null;` を初期化（他フィールドと同様）。

### 5. `CommonBlockPlaceSystem`（変更）

- `GroundClickControl(context)` 冒頭（連続設置リセット判定の近辺）で、
  `context.IsSelectionChanged && context.SelectedBlockDirection.HasValue` のとき
  `_currentBlockDirection = context.SelectedBlockDirection.Value;` を適用。
  - 選択変化時に一度だけ適用。以降は既存の `BlockDirectionControl()` による手動回転が優先される。
  - direction が null（メニュー選択）のときは適用せず held 回転を維持＝既存挙動。

### 6. `PlaceBlockState`（変更）

- コンストラクタに `BlockEyedropperInputService` を注入。
- `GetNextUpdate()` 内、テキスト入力中でない場合に `_placeSystemStateController.ManualUpdate()` の
  前で `_eyedropperInputService.ManualUpdate();` を駆動。
  - 配置モード中は既に PlaceBlock なので遷移は不要。ピックで `PlacementSelection` が変化し、
    次フレームの `PlaceSystemStateController` が place system 差し替え＋向き復元を行う。
  - `OnPicked` は購読しない（遷移不要のため）。

### 7. `GameScreenState`（変更）

- コンストラクタに `BlockEyedropperInputService` を注入。
- `GetNextUpdate()` 冒頭で `_eyedropperInputService.ManualUpdate();` を駆動。
- `OnEnter` で `OnPicked` を購読し、ピック時にフラグ `_pickedThisFrame = true` を立てる
  （`OnExit` で購読破棄・フラグリセット）。UniRx `Subject` は同期発火のため、
  `ManualUpdate()` 呼び出し中にハンドラが走り、同 `GetNextUpdate()` 内でフラグを確認できる。
- `ManualUpdate()` 駆動直後にフラグが立っていれば `_pickedThisFrame = false;` にして
  `new UITransitContext(UIStateEnum.PlaceBlock)` を返す（選択は `PlacementSelection` に保持済み）。
- ステートは検知もレイキャストも共有状態書き込みも行わず、遷移のためだけにイベントに反応する。

### 8. DI 登録（`MainGameStarter`）

- `BlockEyedropperInputService` を `Singleton` 登録（他の入力サービスと同様、L200-229 付近）。
- `GameScreenState` / `PlaceBlockState` のコンストラクタ注入に追加される。

## エッジケース

| ケース | 挙動 |
|---|---|
| 空中・非ブロックをミドルクリック（レイキャスト外れ） | `TryGetCursorOnBlock` false で何もしない（書き込み・イベントなし） |
| UI 上でミドルクリック | `IsPointerOverGameObject()` ガードでピックしない |
| 同種ブロックを別向きで再ピック（例: North ベルト保持中に East ベルトをピック） | `SelectedBlockDirection` が変わり `IsSelectionChanged=true` → East を復元。**向きを比較に含めることでの解決**（これが本設計の要） |
| 通常プレイ中にピック | 選択を書き、`OnPicked` 経由で PlaceBlock へ遷移し即配置可能 |
| ピックしたブロックがワールドに存在＝配置可能なブロック | 常に有効な BlockId が得られる（ワールドのブロックは配置済み＝配置可能） |

### 受容する制約（YAGNI・非対応）

- **同一ブロック種＋同一向きの再ピックによる held 回転リセットは非対応。**
  例: North ベルトをピック（held=North）→ 手動で East に回転 → 再び別の North ベルトをピック。
  `SelectedBlockId` も `SelectedBlockDirection` も不変のため `IsSelectionChanged=false` となり、
  held 回転は East のまま North にリセットされない。稀なコーナーであり、選択ノンス等の新機構を
  足すのは SSOT を汚すため見送る。手動回転で回避可能。

## テスト方針

ロジックは UI 入力・レイキャスト・UIState 遷移に密結合するため、単体テストより
EditModeInPlayingTest（PlayMode 統合）での検証が適切。

- 配置モード中: ワールドに向き付きブロックを配置 → スポイト入力を注入（InputSystem QueueStateEvent
  でミドルクリック相当、またはサービスの `ManualUpdate()` を直接駆動）→ `PlacementSelection.SelectedBlockId`
  と `SelectedBlockDirection` が対象と一致することを確認。
- 向き復元: 上記後、`CommonBlockPlaceSystem` のプレビュー向きが復元されることを確認。
- 通常プレイ中: GameScreen でピック → `UIStateEnum.PlaceBlock` へ遷移し、選択が引き継がれることを確認。
- 変化検知: 同種ブロックを別向きで連続ピックし、2 回目でも向きが更新されることを確認（要エッジ）。

## 規約チェック

- partial 不使用 / 1 ファイル 200 行以下（新規サービスは小規模）。
- イベントは UniRx（`Subject<Unit>` + `IObservable<Unit>`）、C# 標準 event 不使用。
- try-catch 不使用。null チェックは Try 系 API の out 判定で表現。
- デフォルト引数不使用（`SetSelectedBlock` は呼び出し側を全変更）。
- 新規 .cs は `uloop compile --project-path ./moorestech_client` で必ずコンパイル確認。
