# 歯車チェーンポールのレール風延長設置システム 設計

## 背景・目的

現在の歯車チェーンポール（`GearChainPole`）は次の2ステップでしか接続できない。

1. ポールアイテムで通常のブロック設置（`CommonBlockPlaceSystem`）を使い、2本のポールを個別に設置する
2. 接続用アイテム（チェーンアイテム、itemGuid `8412fa32-186d-41a9-9627-69874f341c6e`）に持ち替え、`GearChainPoleConnectSystem` で起点ポール→対象ポールの順にクリックして接続する（`ClientContext.VanillaApi.SendOnly.ConnectGearChain`）

この接続操作にはプレビュー（距離線・設置可否の色分け）が一切なく、クリックした瞬間にサーバーへ送信され、`GearChainSystemUtil.TryConnect` の判定（距離超過・接続数上限・チェーンアイテム不足等）に失敗して初めて分かる。

一方、列車レール（`TrainRail`）は起点ピアを選択した状態で未設置の場所にカーソルを合わせると、新しいピアを自動設置しながら接続をプレビュー表示し（設置可否に応じて青/赤に色分け）、設置後はそのピアが次の起点になって連続して延長できる（`TrainRailConnectSystem` / `RailConnectWithPlacePierProtocol`）。

歯車チェーンポールもこのレールと同じ体験にする。

## インタラクション設計（4状態）

チェーンアイテムを持っている間、`GearChainPoleConnectSystem` は以下の4状態を扱う。

| 状態 | カーソル位置 | クリック時の動作 |
|---|---|---|
| ① 起点未選択 | 既存ポールにヒット | 既存動作のまま：そのポールを起点として選択（設置は発生しない） |
| ② 起点未選択 | 空きスペース | **新規**：接続なしの孤立ポールをその場に設置し、自動的にそれを起点にする |
| ③ 起点選択済み | 既存の別ポールにヒット | 既存動作＋プレビュー追加：起点↔対象ポールを接続 |
| ④ 起点選択済み | 空きスペース | **新規**：新規ポールを自動設置しつつ起点とチェーン接続。成功後、新規ポールが次の起点になる（連続延長） |

すべての状態でプレビュー（ゴーストブロック＋接続線）を表示し、設置/接続可能なら青、不可能なら赤（`MaterialConst.PlaceableColor` / `NotPlaceableColor`）に色分けする。①のみ既存ポールへのハイライトだけで、設置プレビューは不要。

起点選択の解除は既存動作と同様にツール切り替え（`Disable()`）で行い、明示的なキャンセル操作は追加しない。

## 判定ロジック（赤くなる条件）

「空きスペースへの延長」（状態②④）の可否判定は、サーバーとクライアントで完全に同じロジックを共有する。サーバーコード（`moorestech_server`）はクライアントプロジェクトからも参照可能なため、クライアントのプレビュー計算から直接サーバー側の判定関数を呼び出せる（レールが `RailConnectionEditProtocol.EvaluatePlacement` を共有しているのと同じパターン）。

具体的には、`GearChainSystemUtil.TryConnect` に現在インライン実装されている判定を `EvaluatePlacement` という純粋な判定メソッドに切り出す。判定内容：

- 距離 `> min(起点.MaxConnectionDistance, 新規ポール.MaxConnectionDistance)` → 不可（`TooFar`）
- 起点が `IsConnectionFull` → 不可（`ConnectionLimit`）
- チェーンアイテムの所持数が距離分（`consumptionPerLength` 計算）に満たない → 不可（`NoItem`）
- ポールアイテムの所持数が1個未満 → 不可（`NoPoleItem`、状態④のみ）
- 設置先ブロック位置が空いていない／地形的に設置不可 → 不可（通常ブロック設置と同じ判定）

`TryConnect`（状態③で使用）と、新設する `PlaceGearChainPoleWithConnect` 系のサーバー処理（状態④で使用）は、どちらもこの `EvaluatePlacement` を内部で呼ぶ。クライアントの `GearChainPoleExtendPreviewCalculator` も同じメソッドを呼んでプレビューの色を決定する。これにより「プレビューは青だったのに実際は置けなかった」という食い違いを構造的に防ぐ。

状態②（起点未選択で孤立ポールを空きスペースに置く）は接続を伴わないため、通常のブロック設置バリデーション（地形・当たり判定・ポールアイテム所持数）のみを使う。

## クライアント側の変更

- `GearChainPoleConnectSystem.cs`（既存拡張） — 上記4状態の分岐を実装
- `GearChainPoleExtendPreviewCalculator.cs`（新規） — カーソル位置から接続可否・ゴースト位置を計算する。`TrainRailConnectPreviewCalculator` と同型
- `GearChainPoleExtendPreviewObject.cs`（新規） — ゴーストブロック（既存の `IPlacementPreviewBlockGameObjectController` を流用）と接続線（`GearChainPoleChainLineViewElement` は `BlockInstanceId` 前提のため、任意座標を受け取れる別実装にする）を表示し、判定結果に応じて色分けする。`RailConnectPreviewObject` と同型
- `VanillaApiSendOnly` / `VanillaApiResponse` — 新規プロトコル用のAPIを追加

## サーバー側の変更

新規プロトコル `va:gearChainPoleExtend`（`RailConnectWithPlacePierProtocol` を参考にする）を追加する。

処理順序：

1. 起点ポールを座標から解決（存在しなければ失敗）
2. 設置先座標が空いているか確認（既に何かある場合は失敗）
3. 指定インベントリスロットのポールアイテムからブロックIDを解決し、そのブロックマスタから `MaxConnectionDistance` を取得
4. `EvaluatePlacement` で距離・接続数上限・ポールアイテム所持数・チェーンアイテム所持数を**設置前にすべて検証**
5. 検証をすべて通過した場合のみ：ブロック設置 → チェーン接続（`TryAddChainConnection` 双方向）→ ポールアイテム1個・チェーンアイテム（距離分）を消費 → ギアネットワーク再構築 → 新規ポールの座標／`BlockInstanceId` を返す
6. いずれかの検証に失敗した場合は何も変更せず失敗を返す

参考にした `RailConnectWithPlacePierProtocol` は実装上、ブロックを設置した後にレールアイテムの解決に失敗すると孤立したピアが残ってしまう（設置のロールバックがない）。歯車チェーンポールの新規プロトコルではこれを踏襲せず、手順4で全検証を先に完了させてから手順5で状態を変更することで、失敗時に一切の状態変更が起きないようにする。

状態②（起点なしで孤立ポールを設置）は接続を伴わない通常のブロック設置なので、既存の通常ブロック設置プロトコルをそのまま利用し、新規サーバーロジックは不要。

## アイテム経済

- 状態④（空きスペースへの延長）＝ポールアイテム1個＋距離に応じたチェーンアイテム（既存の `consumptionPerLength` 計算式のまま）
- 状態③（既存ポール同士の接続）＝チェーンアイテムのみ（変更なし）
- 状態②（孤立ポール設置）＝ポールアイテム1個のみ
- 設置するポールの種類（通常／コンパクト）はインベントリ内で見つかったものを自動選択する（レールの橋脚アイテム選択と同じ方式）

## 対象外（スコープ外）

- チェーンポールの種類（通常／コンパクト）をUIで明示的に選択する機能（自動選択のみ）
- 起点選択中の明示的なキャンセル操作（ツール切り替えのみで解除）
- 切断（Disconnect）フローへのプレビュー追加（本設計では新規接続・延長のみを対象とする）

## テスト方針

- サーバー: `EvaluatePlacement` の単体テスト（距離超過／接続数上限／ポールアイテム不足／チェーンアイテム不足の各ケースで不可判定になること）
- サーバー: `va:gearChainPoleExtend` プロトコルの結合テスト（正常系での設置・接続・アイテム消費、および各検証失敗時に状態が一切変化しないこと）
- クライアント: 可能であれば `GearChainPoleExtendPreviewCalculator` の単体テスト
