Opus
# 歯車チェーンポール レール式延長設置 設計

## 目的

歯車チェーンポール（GearChainPole）のチェーン接続を、現状の「既存ポール2クリック接続」から、レール（TrainRailConnect）と同じ **起点から連続延長できる設置システム** に進化させる。

- 起点（既存ポール、または新規設置したポール）からチェーンを延ばす
- 延長中はカーソル先に新しいポールを自動設置しながら繋いでいける
- 設置できない状態（距離超過・アイテム不足・接続上限・ポール設置不可）では **プレビューが赤** になり確定できない

参照するお手本は `TrainRailConnectSystem` 一式。判定はレールと同様にクライアント/サーバーで共有し、二重管理を防ぐ。

## 現状

- ポール本体は通常ブロック設置。その後 `GearChainPoleConnectSystem`（`moorestech_client/.../PlaceSystem/GearChainPoleConnect/`）の2クリック方式で既存ポール同士を接続し、`ConnectGearChain(fromPos, toPos, itemId)`（SendOnly）を送る。
- サーバーは `GearChainConnectionEditProtocol` → `GearChainSystemUtil.TryConnect` で距離・重複・接続上限・アイテムを検証し、チェーンアイテムを消費して双方向接続、`GearNetwork` を再構築。
- **プレビュー線・赤色表示・延長ループ・新ポール自動設置は無い。**

レール側には、これらに対応する `TrainRailConnectSystem`（延長ループ本体）／`TrainRailConnectPreviewCalculator`（可否算出＋共有判定呼び出し）／`RailConnectPreviewObject`（色替えプレビュー描画）／`RailConnectionEditProtocol.EvaluatePlacement`（クライアント・サーバー共有判定）／`RailConnectWithPlacePierProtocol`（橋脚設置＋接続の1リクエスト）が既に存在する。本設計はこの構造を歯車チェーンポールへ移植する。

## 操作フロー（ステートマシン）

`GearChainPoleConnectSystem` を `_connectFromPole`（起点ポールのブロック位置）を保持する延長ループに作り替える。

### 起点未選択（`_connectFromPole == null`）
左クリック時:
- カーソルが **既存ポール上**（`IGearChainPoleConnectAreaCollider` ヒット）→ そのポールを起点に設定（設置・消費なし）
- カーソルが **空地** → 通常ブロック設置プロトコルで新ポールを設置（ポールアイテム1消費）、その位置を起点に設定

### 起点選択済み
毎フレーム、起点→カーソルのチェーン線を **直線プレビュー**（青=設置可 / 赤=設置不可）:
- カーソルが **既存ポール上** → 起点→そのポールの接続をプレビュー。左クリックで `ConnectGearChain`（チェーンアイテム消費）。成功後、**その終点ポールを次の起点** にして延長継続。
- カーソルが **空地** → カーソルのグリッド位置に新ポールのゴースト＋チェーン線をプレビュー。左クリックで「ポール設置＋接続を1リクエスト」（`GearChainConnectWithPlacePoleProtocol`）を送信。応答成功を待って **新ポールを次の起点** にして延長継続。
- **右クリック / ESC** で起点を解除しループ離脱。

### 継続方針
レールは「既存ノードへ接続したら終了」だが、本機能は連続延長を優先し、**接続成功後は終点ポールを次の起点にして継続**（右クリック/ESCで終了）に統一する。

## クライアント側コンポーネント

レールの3点セットに対応させる。新規ファイルは `PlaceSystem/GearChainPoleConnect/` 直下に置く（現状2ファイル→合計5ファイルで1ディレクトリ10ファイル制約内）。

| 区分 | クラス | 役割（レール対応物） |
|---|---|---|
| 改修 | `GearChainPoleConnectSystem` | 延長ループ本体（`TrainRailConnectSystem`） |
| 新規 | `GearChainConnectPreviewCalculator` | 直線長・設置可否を算出し共有判定を呼ぶ（`TrainRailConnectPreviewCalculator`） |
| 新規 | `GearChainConnectPreviewObject` (MonoBehaviour) | チェーン線を2点間描画し `MaterialConst.PlaceableColor`/`NotPlaceableColor` で色替え（`RailConnectPreviewObject`）。既存 `GearChainLine.prefab` / `GearChainPoleChainLineViewElement` の描画を流用 |
| 新規 struct | `GearChainConnectPreviewData` | `{ Vector3 Start, Vector3 End, bool IsValid, bool IsPlaceable }`（`TrainRailConnectPreviewData`） |

- ポールのゴースト表示は、レールが `TrainRailPlaceSystemService` を使うのと同様、既存の共通ブロック設置プレビュー（`IPlacementPreviewBlockGameObjectController`）でグリッドにゴーストポールを出す。
- DI登録（`PlaceSystemSelector` / `MainGameStarter`）に `GearChainConnectPreviewObject` を追加。
- チェーンは直線のため、レールのベジェ4制御点ではなく2点（Start/End）で描画する。既存チェーン線の描画API（`GearChainPoleChainLineViewElement` が使うLineRenderer/`GearChainLine.prefab`）に色設定の口を用意して流用する。実装時に当該描画クラスのAPIを確認して最小限で対応する。

## サーバー側（共有判定＋設置＋接続プロトコル）

### (a) 共有判定の抽出
`GearChainSystemUtil.TryConnect` 内の距離・接続上限・アイテム判定を純粋関数に切り出す:

```
GearChainPlacementJudgement EvaluateGearChainPlacement(
    float distance,
    float maxDistanceA, float maxDistanceB,
    bool isFromFull, bool isToFull,
    <inventory 抽象>, ItemId chainItemId)
```

- 戻り値 `GearChainPlacementJudgement { bool IsPlaceable, int RequiredCount, GearChainPlacementFailureReason FailureReason }`。
- `TryConnect`（サーバー）と `GearChainConnectPreviewCalculator`（クライアント）が同一メソッドを呼ぶ。レールの `RailConnectionEditProtocol.EvaluatePlacement` と同様、クライアントはこのサーバーアセンブリの静的メソッドを参照する（既に `GearChainConnectionEditRequest` を参照しているため参照経路は確立済み）。
- インベントリ抽象はレールの `EvaluatePlacement` が取る形（クライアント=`ILocalPlayerInventory`、サーバー=プレイヤーインベントリ）に合わせ、アイテム所持数を問い合わせられる最小インターフェースで受ける。

### (b) 設置＋接続プロトコル
レールの `RailConnectWithPlacePierProtocol` に相当する新規 `GearChainConnectWithPlacePoleProtocol`（WithResponse）を追加:
- 要求: 起点pos / 設置先グリッドpos / ポールブロックのインベントリスロット / チェーンアイテムId / playerId
- 処理: ① 設置先グリッドにポールブロックを設置（既存の通常ブロック設置経路を再利用し、ポールアイテムをスロットから消費）→ ② `GearChainSystemUtil.TryConnect(起点pos, 設置先pos, playerId, チェーンアイテム)` → ③ 応答に `Success` と設置先pos。
- クライアントは応答成功を待って `_connectFromPole = 設置先pos` に更新し連続延長（レールの `PlaceRailWithPier` 応答待ちと同型）。
- `PacketResponseCreator` に登録、`VanillaApiWithResponse` にクライアントAPIを追加。

既存の `ConnectGearChain`（SendOnly）と `DisconnectGearChain` は温存する。既存ポール同士の接続は `ConnectGearChain` を流用し、切断は本機能の対象外。

## 赤（設置不可）判定マッピング

すべての条件でプレビューを赤にし、左クリック確定を `if (!IsPlaceable) return;` でブロックする。

| 条件 | 判定場所 | FailureReason |
|---|---|---|
| 接続距離 > `min(maxConnectionDistance)` | 共有判定 `EvaluateGearChainPlacement` | `TooFar` |
| チェーンアイテム不足（必要数 `ceil(距離 / consumptionPerLength)`）＋新ポール設置アイテム不足 | 共有判定 / 設置分は設置プレビュー側 | `NoItem` |
| いずれかのポールが `maxConnectionCount` 上限 | 共有判定 `EvaluateGearChainPlacement` | `ConnectionLimit` |
| 設置先グリッドにポールを置けない（他ブロック占有・地形） | クライアント側占有チェック（レールの曲率ルールに相当するクライアント専用AND条件） | `Blocked` |

最終的なプレビュー色 = `judgement.IsPlaceable && clientCanPlacePole`。

## テスト

- サーバー: `GearChainSystemUtilTest` に `EvaluateGearChainPlacement` の4パターン（距離超過 / アイテム不足 / 接続上限 / 正常）ユニットテスト追加。`GearChainConnectWithPlacePoleProtocol` の「設置＋接続＋アイテム消費」CombinedTestを既存 `ChainProtocolTest` に倣って追加。
- クライアント: `GearChainConnectPreviewCalculator` の可否判定テストをレールの `TrainRailCurvePlacementRuleTest` に倣って追加。

## ファイル構成・制約

- 新規クライアント3ファイル（`GearChainConnectPreviewCalculator` / `GearChainConnectPreviewObject` / `GearChainConnectPreviewData`）は `PlaceSystem/GearChainPoleConnect/` 直下。
- 新規サーバープロトコル `GearChainConnectWithPlacePoleProtocol` は `Server.Protocol/PacketResponse/` 直下、判定struct `GearChainPlacementJudgement` / enum `GearChainPlacementFailureReason` は `Util/GearChain/` 内。
- 各ファイル200行以下、1ディレクトリ10ファイル以下を厳守。`partial` は使用しない。
- `.cs` 変更後は必ず `uloop compile --project-path ./moorestech_client` を実行する。

## スコープ外

- チェーン切断（`DisconnectGearChain`）のUX変更。
- ポールの回転・高さ方向の特殊スナップ（通常ブロック設置の挙動に従う）。
- 後方互換・パフォーマンス最適化・将来拡張（プロジェクト方針により計画段階では考慮しない）。
