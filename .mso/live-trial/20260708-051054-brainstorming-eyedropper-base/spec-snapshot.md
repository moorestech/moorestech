# スポイト機能（Eyedropper / Block Pick）設計

日付: 2026-07-08
対象: moorestech_client

## 概要

ワールドに設置済みのブロックを**中クリック**でピックし、そのブロックを建設メニューの選択状態（`PlacementSelection`）にセットして即座に配置できるようにする。ピック時にはブロックの**向き（`BlockDirection`）もコピー**する。

有効範囲:
- **配置モード（`PlaceBlock`ステート）中**: 中クリックで選択ブロック・向きを切り替える（ステート遷移なし）
- **通常プレイ（`GameScreen`ステート）中**: 中クリックでピックに成功したら`PlaceBlock`ステートへ遷移して配置を開始する

## 要件

1. 中クリック（マウス中ボタン押下）でクロスヘア/カーソル先の設置済みブロックをピックする
2. ピック成功時、`PlacementSelection.SetSelectedBlock(blockId)`で選択を更新する
3. ピックしたブロックの`BlockPosInfo.BlockDirection`を配置向きとしてコピーする
4. 未解放ブロック（`IGameUnlockStateData`で`IsUnlocked == false`）はピックしない（no-op）
5. UI要素上のクリック（`EventSystem.current.IsPointerOverGameObject()`）は無視する
6. 配置モード中に照準先へ表示される**配置プレビューゴーストをピックしてはならない**（後述の反例参照）
7. `PlaceBlock`ステートではテキスト入力フィールド編集中はピックしない（既存キーガードと同じ扱い）
8. 効果音・専用UIフィードバックはv1では実装しない（YAGNI）

## アーキテクチャ

### 新規クラス: `EyedropperBlockPickService`

配置場所: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/EyedropperBlockPickService.cs`
DI: `MainGameStarter`で`Lifetime.Singleton`登録（`PlacementSelection`等と同じブロック）

責務: スポイトのピック処理全体を1クラスに集約する。
`GameScreenSubInventoryInteractService`（GameScreenからのインタラクト解決）と同型の「UIステートから呼ばれるサービス」パターンに従う。既存サービスはGameScreen専用の責務のため拡張せず、2ステート共用の本サービスを新設する。

```
public bool TryPick()
```

処理フロー:
1. `EventSystem.current.IsPointerOverGameObject()`ならfalse（UI上クリックガード）
2. `AimPointProvider.GetAimScreenPoint()`からのレイで`Physics.RaycastAll`（`LayerConst.BlockOnlyLayerMask`、距離100m = `CommonBlockPlaceSystem.PlaceableMaxDistance`と同値）
3. ヒットを距離昇順に走査し、`BlockGameObjectChild`→親`BlockGameObject`を解決。`BlockPreviewObject`コンポーネントを持つもの（＝配置プレビューゴースト）はスキップし、最初の実ブロックを採用
4. 実ブロックが無ければfalse
5. `BlockMasterElement.BlockGuid`で`IGameUnlockStateData.BlockUnlockStateInfos`を引き、未解放ならfalse
6. `CommonBlockPlaceSystem.SetPlaceDirection(dir)`と`BeltConveyorPlaceSystem.SetPlaceDirection(dir)`を呼ぶ（`dir = BlockGameObject.BlockPosInfo.BlockDirection`）
7. `PlacementSelection.SetSelectedBlock(blockId)`を呼びtrueを返す

依存（コンストラクタ注入）: `PlacementSelection`, `CommonBlockPlaceSystem`, `BeltConveyorPlaceSystem`, `IGameUnlockStateData`
カメラは既存`BlockClickDetectUtil`と同様`Camera.main`を使用する。

### 入力

`HybridInput.GetMouseButtonDown(2)`（中ボタン）。

根拠（先行パターン）: UIステートの状態キー（B/Tab/T/R/F3）は全て`HybridInput`直読みが現行パターンであり、InputSystemの`QueueStateEvent`注入（プレイテストDSL）とも互換。`.inputactions`アセットの編集とC#再生成を要する`InputManager`経由は採らない。

### 既存クラスの変更

| ファイル | 変更 |
|---|---|
| `GameScreenState.cs` | `GetNextUpdate`に「中クリック かつ `TryPick()`成功 → `new UITransitContext(UIStateEnum.PlaceBlock)`」を追加。コンストラクタに`EyedropperBlockPickService`追加。`OnEnter`のキー説明文に「中クリック: スポイト」追記 |
| `PlaceBlockState.cs` | `GetNextUpdate`の`!isTextInputFocused`ブロック内に「中クリック → `TryPick()`」を追加（戻り値による遷移なし。選択変化は次フレームに`PlaceSystemStateController`が`IsSelectionChanged`で検知しシステム切替）。コンストラクタに`EyedropperBlockPickService`追加。キー説明文に「中クリック: スポイト」追記 |
| `CommonBlockPlaceSystem.cs` | `public void SetPlaceDirection(BlockDirection direction)`追加（`_currentBlockDirection = direction;`のみ） |
| `BeltConveyorPlaceSystem.cs` | 同上 |
| `MainGameStarter.cs` | `builder.Register<EyedropperBlockPickService>(Lifetime.Singleton);`追加 |

GameScreen→PlaceBlockの直接遷移は、GameScreen→DeleteBar（`DeleteObjectState`）と同じ先行例に乗る。ビルドセッション（カメラ保存・照準モード適用）は`PlaceBlockState.OnEnter`→`BuildViewModeController.OnEnterBuildState`が開始するため追加対応不要。

### 向きコピーの設計判断

`PlacementSelection`に「初期方向」状態を追加して`PlaceSystemUpdateContext`経由で配る案は**不採用**。
理由: 同一ブロック・別方向の再ピック時に選択内容が変化せず`IsSelectionChanged`が発火しないため、方向が適用されないstaleケースが構造的に生まれる。設置システムへの直接`SetPlaceDirection`呼び出しは冪等で、この問題が起きない（無料の上位互換）。

両システムの`_currentBlockDirection`はシングルトンのフィールドとして`Enable`/`Disable`を跨いで保持されるため、非アクティブなシステムに先に方向をセットしても、次フレームの`PlaceSystemSelector`によるシステム切替後にその方向で配置が始まる。

## 反例と対処（self-refutation）

| してはならない挙動 | 対処 |
|---|---|
| 配置モード中、照準先の**プレビューゴースト**（選択中ブロック自身）をピックしてしまい、奥の実ブロックが拾えない。ゴーストは実ブロックと同じ`CreateBlockGameObject`経路・同じプレハブで生成されるため実ブロックと同レイヤー＋`BlockGameObjectChild`付き、コライダーはisTrigger、かつ`ProjectSettings`は`m_QueriesHitTriggers: 1`（確認済み）のため単発`Physics.Raycast`はゴーストにヒットする | `RaycastAll`を距離順走査し`BlockPreviewObject`マーカー付きをスキップ（要件6） |
| 同一ブロックを別の向きで再ピックしたとき向きが変わらない | 状態経由でなく`SetPlaceDirection`直接呼び出し（常に適用・冪等） |
| ビルドメニュー等のUIの上で中クリックしてピックが走る | `IsPointerOverGameObject`ガード（要件5） |
| 未解放ブロックがスポイトで入手経路になる | unlockゲート（要件4） |
| 列車車両・MapObjectをピックする | `BlockOnlyLayerMask`対象外のためヒットしない |
| マルチセルブロックの子コライダーで誤った位置・IDを拾う | `BlockGameObjectChild.BlockGameObject`で親解決（既存機構） |

既知の許容挙動: ドラッグ連続設置の途中で同一ブロックを別方向にピックした場合、設置プレビューは新しい向きで再計算される（連続設置状態はブロックIDが変わった場合のみリセットされる既存仕様のまま）。

## ブロック種別ごとの挙動

- 通常ブロック → `CommonBlockPlaceSystem`（向きコピー有効）
- ベルトコンベア系 → `BeltConveyorPlaceSystem`（向きコピー有効。隠しバリアントをピックした場合も`SetSelectedBlock`で選択され、既存の`PlaceSystemSelector`ルーティングに従う）
- 列車レール / 歯車チェーンポール → 各専用システムへ既存ルーティング。これらは向き状態を持たないため向きコピーは実質no-op（`SetPlaceDirection`は共通・ベルトの2システムにのみ追加する）

## エラーハンドリング

ピック不成立（ブロック無し・未解放・UI上）は全てno-opでfalseを返すのみ。例外・ログは出さない。try-catchは使わない（プロジェクト規約）。

## テスト

- `uloop compile`でコンパイル確認
- ロジックの大半がUnity物理・シーン依存のため、検証はプレイテストDSL（unity-playmode-recorded-playtestスキル）による実プレイ確認を主とする:
  1. GameScreenで設置済みブロックへ中クリック → PlaceBlockへ遷移し同ブロックのプレビューが出る
  2. PlaceBlock中に別種ブロックへ中クリック → 選択が切り替わる
  3. 向きが東向きのブロックをピック → プレビューが東向きで出る（ゴースト誤ピックが起きていればこのケースで選択が変わらず検出できる）
- 純ロジック単体テストは対象が薄いため追加しない
