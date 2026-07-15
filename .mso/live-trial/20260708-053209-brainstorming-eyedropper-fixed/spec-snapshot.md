# ブロックスポイト（ミドルクリックピック） 設計書

作成日: 2026-07-08
ブランチ想定: 現ブランチ（feature/replace-palce-system-with-electric 系）以後の新ブランチ

## 背景・目的

建設中に「目の前にあるブロックと同じものを置きたい」とき、現状はビルドメニュー（Tab/B）を開いてグリッドから探し直す必要がある。ワールド内のブロックをミドルクリックすると、そのブロックを設置選択として即座に構える「スポイト（eyedropper）」を追加し、建設の往復操作を削減する。

## 決定事項（壁打ち確定分）

| # | 論点 | 決定 |
|---|---|---|
| 1 | トリガー | マウスミドルクリック（押下フレーム） |
| 2 | 有効範囲 | 配置モード（PlaceBlock）中と通常プレイ（GameScreen）中の両方。GameScreen でピックした場合は PlaceBlock へ直接遷移して構える |
| 3 | 向きコピー | ピック時にブロックの向き（`BlockDirection`、垂直回転含む）もコピーし、設置プレビューの初期向きにする |
| 4 | ピック可否 | ビルドメニューのブロックグリッドと同一の可否判定（ベルト隠しバリアントを代表ブロックへ正規化した後の BlockId がアンロック済みであること）。満たさない場合は no-op |

## 自己解決した前提（design-question-triage 適用結果）

- **入力検知は `HybridInput.GetMouseButtonDown(2)`**: `HybridInput` は middleButton 対応済みで、`BuildViewModeController` が右クリックを同経路で直読みする先行例がある。InputActions アセット追加は Unity Editor 経由の再生成が必要で、既存の `//TODO InputSystem対応` 群と同じ扱いに乗る（先行パターン）
- **サービス形状は毎フレーム `ManualUpdate()` 駆動**: 入力検知（ミドルクリック）も対象検知（レイキャスト・BlockId 解決）もサービス内部で行う。`PlaceSystemStateController.ManualUpdate()` / `BuildViewModeController.ManualUpdate()` と同型（moorestech-principles「UI入力・選択モデル」）
- **反映経路は `PlacementSelection` への書き込み一本**: 向きはフィールド追加で運び、変化検知（`IsSelectionChanged` 比較）にも含める。各 PlaceSystem への直接セッター新設はしない（SSOT・moorestech-principles）
- **対象検知は `BlockClickDetectUtil` + `AimPointProvider`**: GameScreen での左クリックインタラクト（`GameScreenSubInventoryInteractService`）と同一経路。レイヤーマスクが `BlockOnlyLayerMask` のため列車・エンティティは構造的にピック対象外（調査結果）
- **ベルト隠しバリアントは代表ブロックへ正規化**: `BeltConveyorPlaceFamilyUtil.GetRepresentativeBlockId` が既存。正規化により「ピックで選べるもの ⊆ ビルドメニューで選べるもの」の不変条件を維持する（調査結果＋先行パターン）
- **インベントリ所持チェックは不要**: 設置は建設コスト方式で、`CommonBlockPlaceSystem` が affordable セル数を制御しサーバーも検証する。スポイトは選択を変えるだけで消費しない（調査結果）
- **UI 上のポインタでは無効**: `EventSystem.current.IsPointerOverGameObject()` ガード。左クリック設置・インタラクトと同じ扱い（先行パターン）
- **GameScreen → PlaceBlock の直接遷移は安全**: `PlaceBlockState.OnEnter` が呼ぶ `BuildViewModeController.OnEnterBuildState` はセッション未開始なら復帰用カメラを保存して開始するため、BuildMenu を経由しない遷移でも整合する（調査結果）
- **テキスト入力中は無効**: PlaceBlock 中の既存入力ガード（`IsTextInputFocused`）の内側で駆動する（先行パターン）
- **ビルドメニューからの選択は現在向きを維持**: 向き未指定（null）の選択では `CommonBlockPlaceSystem` の現在向きを変えない。既存挙動を変えない無料の上位互換
- **音・専用エフェクトは作らない**: 選択が変わりプレビューが構えられること自体がフィードバック（YAGNI）

C型（ユーザー判断が必要な）質問は残らなかった。

## 検討した代替案

| 案 | 概要 | 判定 |
|---|---|---|
| **A. ManualUpdate型サービス + PlacementSelection拡張（採用）** | 新設 `BlockEyedropperService` を両ステートから毎フレーム駆動。結果は共有選択モデルに書く | 駆動同族（`PlaceSystemStateController` 等）と一貫し、SSOT を守る |
| B. Try-bool判定サービス | `GameScreenSubInventoryInteractService` 型の `TryGetXxx(out ...)` をステートが呼ぶ | `TryGet` 型前例は「共有状態を書かない遷移判定サービス」限定。本機能は `PlacementSelection` を書くため原則違反 |
| C. 各ステート直書き + `CommonBlockPlaceSystem` に向きセッター | ステートにミドルクリック処理を書き、向きは PlaceSystem へ直接セット | 選択モデルの迂回（SSOT違反）。ベルト等の専用システムに配線が分散する |

## アーキテクチャ

### 全体像

```
UIState層
  GameScreenState ──┐ ManualUpdate() 駆動（ピック成立時のみ PlaceBlock へ遷移）
  PlaceBlockState ──┴─→ BlockEyedropperService（新規・DIシングルトン）
                            │ ミドルクリック検知（HybridInput）
                            │ UIポインタガード・レイキャスト（BlockClickDetectUtil）
                            │ BlockId正規化（ベルト代表）・アンロック判定
                            ↓ 書き込み
                    PlacementSelection（既存・拡張）
                            │ SelectedBlockDirection / SelectionVersion 追加
                            ↓ PlaceSystemUpdateContext 経由
                    PlaceSystemStateController → CommonBlockPlaceSystem
                                                  （IsSelectionChanged 時に向きを適用）
```

### 変更・新規コンポーネント

#### 1. `PlacementSelection`（既存拡張）

- `public BlockDirection? SelectedBlockDirection { get; private set; }` を追加。null は「向き指定なし＝現在向き維持」
- `public int SelectionVersion { get; private set; }` を追加。全ての `Set〜` / `ClearSelection` で単調増加させる（同一内容の再選択でも変化検知を発火させるため）
- `SetSelectedBlock(BlockId blockId, BlockDirection? direction)` に引数追加。デフォルト引数は使わず、既存呼び出し側（`BuildMenuState`）は `null` を明示して更新する

#### 2. `PlaceSystemUpdateContext` / `PlaceSystemStateController`（既存拡張）

- コンテキストに `SelectedBlockDirection` を追加
- 変化検知の比較に `SelectionVersion` を追加（向き・同一再選択の両方をこれで拾う）

#### 3. `CommonBlockPlaceSystem`（既存拡張）

- `ManualUpdate` 冒頭で `context.IsSelectionChanged && context.SelectedBlockDirection.HasValue` のとき `_currentBlockDirection` に反映
- 以後の R / Shift+R 回転は従来どおりローカルに回す

#### 4. `BlockEyedropperService`（新規・中核）

- 置き場所: `Client.Game/InGame/BlockSystem/PlaceSystem/Eyedropper/BlockEyedropperService.cs`
- DI: `MainGameStarter` にシングルトン登録。依存は `PlacementSelection` と `IGameUnlockStateData`
- 公開API: `public bool ManualUpdate()` — ピック成立時のみ true（呼び出し側ステートが自ステート固有の遷移判断にだけ使う。クリック判定・対象解決はサービス内部で完結）
- 処理フロー:
  1. `HybridInput.GetMouseButtonDown(2)` でなければ false
  2. `EventSystem.current.IsPointerOverGameObject()` なら false
  3. `BlockClickDetectUtil.TryGetCursorOnBlock` で `BlockGameObject` を取得（失敗なら false）
  4. BlockId をベルトファミリー代表へ正規化（`BeltConveyorPlaceFamilyUtil`）
  5. アンロック判定（`IGameUnlockStateData.BlockUnlockStateInfos`、ビルドメニューと同一述語）。未解放なら false
  6. `PlacementSelection.SetSelectedBlock(正規化ID, blockGameObject.BlockPosInfo.BlockDirection)` を書いて true

#### 5. ステート配線（既存拡張）

- `GameScreenState.GetNextUpdate`: サービスを駆動し、true なら `new UITransitContext(UIStateEnum.PlaceBlock)` を返す（GameScreen は建設系ステートでないため `OnLeaveBuildState` は不要）
- `PlaceBlockState.GetNextUpdate`: `!isTextInputFocused` ガード内・`_placeSystemStateController.ManualUpdate()` より前に駆動し、戻り値は使わない（既に PlaceBlock のため遷移不要。同フレームで `IsSelectionChanged` が立ちプレビューが切り替わる）
- 両ステートの `KeyControlDescription` に「ミドルクリック: スポイト」を追記

## データフロー（ピック1回の流れ）

1. ミドルクリック押下 → `BlockEyedropperService` がレイキャストで `BlockGameObject` を解決
2. `PlacementSelection` に BlockId＋向きが書かれ、`SelectionVersion` が進む
3. （GameScreen の場合）PlaceBlock へ遷移。`PlaceSystemStateController` は次の `ManualUpdate` で `IsSelectionChanged=true` を検知
4. `PlaceSystemSelector` が BlockId に応じて設置システムを選択（ベルト→専用、レール→専用、他→Common）
5. `CommonBlockPlaceSystem` が向きを取り込み、プレビューがピック元と同じ向きで構えられる

## エッジケース・自己反駁

- **反例（設計を修正させたケース）: 回転後の同一内容再ピック** — ブロックAを北向きでピック→ R で東向きに回転 → 別の北向きAを再ピック。blockId と向きの値比較では選択が「変化なし」となり向きが再適用されない。→ `SelectionVersion` を変化検知に含めることで解決（本設計は値比較のみの案を棄却した）
- **マルチセルブロック**: レイキャストは `BlockGameObjectChild` → 親 `BlockGameObject` に解決されるため、どのセルをクリックしても向き・IDは親のものになる
- **未解放ブロック**: no-op。研究進行の迂回（メニューに出ないブロックの入手）を防ぐ
- **ベルト系・レール系のピック**: 選択は成立するが、向きの初期反映は `CommonBlockPlaceSystem` のみ。ベルト・レール等はドラッグで向きが決まる専用システムのため向きコピーは適用外（制限として明記）
- **列車・エンティティのミドルクリック**: `BlockOnlyLayerMask` により対象外、no-op
- **UI（ホットバー・メニュー）上のミドルクリック**: `IsPointerOverGameObject` ガードで no-op
- **設置素材を持っていないブロックのピック**: 選択は成立する。設置段階で既存の建設コスト判定・サーバー検証が効く

## エラーハンドリング

新規の失敗系はすべて「ピック不成立＝no-op（false 戻り）」に畳む。例外・try-catch は使わない。`MasterHolder` 等のコア参照は存在保証前提（プロジェクト規約）。

## テスト戦略

- **ユニットテスト（EditMode）**: `PlacementSelection` の `SelectionVersion` 単調増加・向き保持・`ClearSelection` 挙動、`PlaceSystemStateController` の変化検知（同一内容再選択で `IsSelectionChanged=true` になること）。純C#のため既存テスト基盤でそのまま書ける
- **E2E（プレイテストDSL）**: PlayMode でブロックを設置 → GameScreen からミドルクリック → PlaceBlock に遷移しプレビューがピック元と同じ BlockId・向きで構えられることを検証（`SemanticInput` のマウス状態注入はミドルボタンのビットを含むことを確認済み。押下コマンドの露出はプラン時に確認し、無ければ追加する）。既存の unity-playmode-recorded-playtest スキルのシナリオとして追加
