# FPS建設モード 設計書

作成日: 2026-07-07
ブランチ想定: feature/replace-palce-system-with-electric 以後の新ブランチ

## 背景・目的

現在の建設モード（`PlaceBlockState`）は俯瞰カメラ（70°見下ろし・距離9）＋マウスカーソル基準のレイキャストで動作する。ここに一人称視点（FPS）での建設を追加し、狭所・高所の精密設置や没入感のある建設体験を可能にする。

## 決定事項（壁打ち確定分）

| # | 論点 | 決定 |
|---|---|---|
| 1 | 俯瞰との関係 | 切替式で共存。どちらも廃止しない |
| 2 | 切替方法 | 建設系モード中いつでもトグルキーで即時切替。最後に使ったモードを記憶し、次回もそのモードで開始 |
| 3 | 設置リーチ | 両モード共通（現行 `PlaceableMaxDistance = 100f` のまま）。FPSは純粋に視点の違いだけでルール差分なし |
| 4 | 適用範囲 | 設置モード（PlaceBlock）と削除モード（DeleteBar）の両方。「建設系視点モード」として記憶を共有 |
| 5 | Shift+B | 廃止。カメラ挙動を「俯瞰」「FPS」の2系統に整理（モード記憶がShift+Bの動機を上位互換でカバー） |

### 自己解決した前提

- FPS中は**カーソルロック＋画面中央クロスヘア照準**。右クリック押下による視点回転は使わず、マウスが常時視点操作になる
- レイ取得は `PlaceSystemUtil` の `ScreenPointToRay(Input.mousePosition)` を照準点プロバイダ経由に差し替えて一元対応。全設置システム（通常ブロック・ベルト・レール・電線接続・削除）がFPSでそのまま動く
- FPS中もWASD移動・ジャンプは有効（現行建設モードも移動を止めていない）
- 自機モデルはFPS中非表示
- モード記憶はセッション内のみ（設定ファイル永続化はYAGNI）
- トグルキーは **V**（既存ステートのTab/B/Gと同じ`UnityEngine.Input.GetKeyDown`直接読み＋`//TODO InputSystemのリファクタ対象`コメント方式。前例準拠）
- クロスヘアは中央ドットの最小UI

## アーキテクチャ

### 全体像

```
UIState層
  PlaceBlockState ──┐
  DeleteObjectState ─┼─→ BuildViewModeController（新規・DIシングルトン）
  BuildMenuState ────┘        │ 現在モード保持（TopDown / FirstPerson）
                              │ Vキートグル・カメラ適用・カーソル制御
                              │ 建設系ステート間のカメラセッション維持
                              ↓
                    InGameCameraController（既存）
                              ↑
PlaceSystem層                 │
  各IPlaceSystem → PlaceSystemUtil → AimPointProvider（新規）
                                       │ TopDown: Input.mousePosition
                                       │ FirstPerson: 画面中央
```

### 新規コンポーネント

#### 1. `BuildViewMode`（enum）
`TopDown` / `FirstPerson` の2値。

- 置き場所: `Client.Game/InGame/Control/BuildView/BuildViewMode.cs`

#### 2. `BuildViewModeController`（DIシングルトン・中核）
建設系視点モードの唯一の所有者。責務は以下。

- **モード記憶**: 現在の `BuildViewMode` を保持（初期値 TopDown）。セッション内のみ
- **トグル処理**: 建設系ステートがアクティブな間、Vキー入力でモード切替。切替時にカメラTween・カーソル状態・クロスヘア表示・自機モデル表示を一括更新
- **カメラセッション管理**: 建設系ステート（BuildMenu / PlaceBlock / DeleteBar）に入った時点の三人称カメラを保存し、**建設系以外のステートへ抜けた時のみ**復帰Tweenを実行する。建設系ステート間の遷移（Tab→ビルドメニュー→再設置、G↔Bなど）ではカメラを動かさない
- **モードごとのカメラ適用**:
  - TopDown: 現行挙動を踏襲。俯瞰Tween（`CreateTopDownTweenCameraInfo()`）は**PlaceBlockに入った時のみ**実行し、BuildMenu・DeleteBarではカメラを動かさない。カーソル表示、右クリック押下中のみ回転
  - FirstPerson: 建設セッション開始時点でカメラ距離0・追従オフセットを頭部高さへTween。カーソルロック＋ `SetControllable(true)` 常時。クロスヘア表示、自機モデル非表示。セッション中はステートが変わっても維持

置き場所: `Client.Game/InGame/Control/BuildView/BuildViewModeController.cs`

既存 `ScreenClickableCameraController`（各ステートが個別に `new` している）はカメラ保存/復帰とカーソル制御の責務を `BuildViewModeController` に移して**廃止**する。ステート間でカメラセッションを共有するには状態がシングルトンに存在する必要があり、per-state インスタンスでは実現できないため。

#### 3. `AimPointProvider`（照準点プロバイダ）
レイキャストの起点となるスクリーン座標を返す静的クラス。

- TopDown: `Input.mousePosition`
- FirstPerson: `new Vector2(Screen.width / 2f, Screen.height / 2f)`
- モードは `BuildViewModeController` が切替時に `AimPointProvider.SetMode(mode)` で通知する（PlaceSystem層からControl層への依存を作らないため静的+set方式）

置き場所: `Client.Game/InGame/BlockSystem/PlaceSystem/Util/AimPointProvider.cs`

`PlaceSystemUtil.TryGetRayHitPosition` / `TryGetRaySpecifiedComponentHit`、および各PlaceSystem・削除サービス内に散在する `ScreenPointToRay(Input.mousePosition)` 直書きをすべて `AimPointProvider.GetAimScreenPoint()` 経由に置換する。置換漏れは `Input.mousePosition` のgrepで検出する。

#### 4. `CrosshairView`（クロスヘアUI）
中央ドットのみの最小UI。Canvas配下に事前配置し、`BuildViewModeController` から表示/非表示を切り替える。

置き場所: `Client.Game/InGame/UI/Crosshair/CrosshairView.cs`

#### 5. `PlayerObjectController` への追加
`SetModelVisible(bool)` を追加し、FPS中は自機のレンダラーを非表示にする（移動・当たり判定は生かしたまま見た目だけ消す）。

### 変更するコンポーネント

| ファイル | 変更内容 |
|---|---|
| `PlaceBlockState` | Shift+B分岐を削除。`ScreenClickableCameraController` 呼び出しを `BuildViewModeController` の enter/update/exit に置換 |
| `DeleteObjectState` | 同上（`OnEnter(false)` の「カメラ維持」挙動は TopDown モード時の挙動として維持） |
| `BuildMenuState` | `BuildViewModeController` のカメラセッション参加を追加（FPS中にTabでメニューを開いてもカメラが戻らない）。メニュー表示中はモードに関わらずカーソル解放 |
| `InGameCameraController` | FPS用に追従オフセット（頭部高さ）をTween対象へ追加。`Update()`内のF1/F2ズームと距離クランプ（0.6〜10）は毎フレーム距離を上書きするため、FPS中は無効化するフラグを追加（距離0を維持） |
| （入力） | Vキーは `UnityEngine.Input.GetKeyDown(KeyCode.V)` 直接読み（`.inputactions`は変更しない。既存TODOリファクタに乗る） |
| `PlaceSystemUtil` ほかレイ取得箇所 | `Input.mousePosition` → `AimPointProvider.GetAimScreenPoint()` |
| `KeyControlDescription` 表示文言 | 各ステートで「V: 視点切替」を追記 |

## データフロー

### モード切替（建設中にVを押す）
1. `BuildViewModeController` がVキー押下を検知（建設系ステートのupdate中のみ）
2. `BuildViewMode` を反転して記憶
3. `AimPointProvider.SetMode` を更新
4. カメラTween（俯瞰⇔頭部）、カーソルロック状態、クロスヘア表示、自機モデル表示を一括切替

### 建設系ステートへの出入り
1. GameScreen → BuildMenu/PlaceBlock/DeleteBar のいずれかに入る際、`BuildViewModeController.EnterBuildSession()` が現在カメラを保存。FirstPerson記憶ならこの時点でFPSカメラを適用、TopDown記憶ならカメラ適用は上記「モードごとのカメラ適用」の方針に従う（PlaceBlock進入時のみ俯瞰Tween）
2. 建設系ステート間の遷移では `EnterBuildSession` は何もしない（セッション継続）。TopDownモードでは従来Tab遷移のたびにカメラが三人称へ戻ってバウンスしていたが、セッション化により解消される（意図した挙動変更）
3. 建設系以外（GameScreen / PlayerInventory / Story 等）へ抜ける際、`ExitBuildSession()` が保存カメラへ復帰Tween・カーソル/クロスヘア/自機表示を通常状態へ戻す

セッションの開始・終了判定は `UIStateControl.OnStateChanged`（既存UniRxイベント）を購読し、遷移元・遷移先が建設系ステート集合に含まれるかで判定する。各ステートの `OnEnter`/`OnExit` に個別実装を散らばらせない。

### 設置レイキャスト（変更後）
1. 各PlaceSystemが `PlaceSystemUtil.TryGetRayHitPosition` を呼ぶ
2. `AimPointProvider.GetAimScreenPoint()` がモードに応じたスクリーン座標を返す
3. 以降は現行と同一（`ScreenPointToRay` → `Physics.Raycast` → グリッドスナップ → プレビュー → サーバー送信）

## エッジケース

| ケース | 挙動 |
|---|---|
| FPS中にTabでビルドメニュー | カメラはFPSのまま。カーソルのみ解放しクロスヘア非表示。選択確定でPlaceBlockに戻ったら再ロック・クロスヘア再表示 |
| FPS中にインベントリ/ポーズ/スキット遷移 | 建設セッション終了扱い。三人称カメラへ復帰し、自機表示・カーソルを通常状態へ戻す |
| FPSで足元（自機と重なる位置）に照準 | レイヤーマスクは現行 `Without_Player_MapObject_Block_LayerMask` を踏襲し自機には当たらない。地面ヒットすれば通常どおり設置可 |
| FPSで左ドラッグ連続設置 | 視点移動＝照準移動なのでドラッグ範囲設置は現行ロジックのまま成立する |
| 頭部カメラの壁めり込み | Cinemachineの距離が最小のため後方遮蔽は発生しない。近接面のニアクリップは既存カメラ設定のまま（問題が出たら実装時にニアクリップ調整） |
| Vキー連打・Tween中の再切替 | `StartTweenCamera` は既存実装が `_currentSequence?.Kill()` で前Tweenを破棄するため多重Tweenは発生しない |
| 俯瞰記憶状態での削除モード | 現行同様カメラ移動なし（俯瞰Tweenもしない）。FPS記憶状態のみFPSカメラを適用 |

## スコープ外

- 通常移動（GameScreen）のFPS化
- 視点感度・FOV等の設定UI（既存 `sensitivity` SerializeFieldを流用）
- モード記憶の設定ファイル永続化
- リーチ距離のゲームバランス調整

## テスト計画

- **Client.Tests/UIState**: 建設系ステート遷移で `BuildViewModeController` のセッション開始/終了・モード記憶が正しく動くかのユニットテスト（G↔B往復でセッション継続、GameScreen復帰でセッション終了、V切替の記憶保持）
- **AimPointProvider**: TopDown/FirstPersonでの返却座標のユニットテスト
- **プレイテストDSL（unity-playmode-recorded-playtest）**: FPSモードでのブロック設置E2E（V切替→クロスヘア照準→設置→サーバー反映確認）。俯瞰モードの既存設置回帰も同時に確認
- **手動確認**: カメラTweenの見た目、自機非表示、カーソルロックの切替感

## 実装時の検証コマンド

```
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BuildView|PlaceSystem|UIState"
```
