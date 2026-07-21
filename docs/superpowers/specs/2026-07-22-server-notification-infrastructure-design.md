# サーバー発 汎用通知基盤 設計

日付: 2026-07-22
ブランチ起点: feature/webui-hud-pin 時点の調査に基づく

## 目的

サーバー内の任意のシステムが `Notify(playerId, ...)` を1行呼ぶだけで、プレイヤーの画面（Web UI）にゲーム内通知が表示される汎用経路を作る。

主用途はゲーム内イベント通知（研究完了・チャレンジ達成・アンロック）と、**プレイヤー操作の無言失敗の可視化**（操作拒否通知）。

## 背景: プレイヤー操作プロトコルの無言失敗棚卸し

全 `IPacketResponse` 実装を調査した結果、**プレイヤー可視の失敗理由を表示しているのは `va:removeBlock` ただ1つ**だった。しかもその実装コメント（`BlockGameObjectChild.cs:75`）に「TODO 基盤通知システムができたらそちらに移行」と既に書かれている。

無言失敗は3パターンに分類される:

1. **サーバーが失敗理由を構築済みなのにSendOnly送信で応答が破棄される**
   - `va:railConnectionEdit` — NotEnoughRailItem / RailLengthExceeded / NotUnlocked / NodeInUseByTrain / NotEnoughInventorySpace 等8種の理由を構築して捨てている（`RailConnectionEditProtocol.cs:66-155`）
   - `va:electricWireConnectionEdit`（`ElectricWireConnectionEditProtocol.cs:44-55`）
   - `va:gearChainConnectionEdit`（`GearChainConnectionEditProtocol.cs:42`）
2. **Responseは受けているがクライアントが失敗時に無言return / Debug.Logのみ**
   - `va:electricWireExtend`（`ElectricWireExtendRequestSender.cs:54`）
   - `va:railConnectWithPlacePier`（`TrainRailConnectSystem.cs:158`）
   - `va:machineRecipeSelection` — RecipeLocked / RefundFailed 等（`MachineRecipeSelectionPanel.cs:149,163`）
   - `va:placeTrainCar` / `va:attachTrainCarToUnit`（`TrainCarPlaceSystem.cs:108-120`）
   - `va:completeResearch` — 応答の `Success` をクライアントが無視（`ResearchTreeViewManager.cs:48-51`）
3. **サーバー側がそもそも失敗情報を作らず無言スキップ**
   - `va:placeBlock` — 既存ブロック重複 / 未解放セル / 建設コスト不足 / 電線在庫不足のセルを無言スキップ（`PlaceBlockProtocol.cs:59-98`）
   - `va:removeTrainCar` — インベントリ満杯で撤去中止（`RemoveTrainCarProtocol.cs:60-64`）
   - `va:oneClickCraft` — 素材不足 / 満杯で無言no-op（`OneClickCraft.cs:39-41`）
   - `va:completeBaseCamp`（`CompleteBaseCampProtocol.cs:33-48`）
   - `va:mapObjectInfoAcquisition` — 満杯時に採取アイテムが InsertionCheck なしで消失し得る（`MapObjectAcquisitionProtocol.cs:44`）※通知以前に挙動修正が必要な可能性があり、**別課題として切り出す**

混乱しやすい優先順（上位）: レール接続 > 車両撤去 > 電線接続 > 歯車チェーン接続 > ブロック一括設置 > 1クリッククラフト > 研究完了 > 機械レシピ選択。

## アーキテクチャ

```
[サーバー内の任意のシステム]
    ↓ NotificationService.Notify(playerId, data) / NotifyAll(data)   ← 汎用APIはここ（サーバー）
    ↓ デデュープ/クールダウン（基盤責務・サーバー側＝ワイヤにスパムを乗せない）
    ↓ EventProtocolProvider.AddEvent / AddBroadcastEvent("va:event:notification")   ← 既存配信経路
[クライアント] SubscribeEventResponse ハンドラ1本（薄い中継のみ、ロジックなし）
    ↓ NotificationTopic（WebUiGameBinder に登録する既存Topicパターン）
[Web] features/notification — ゲーム内通知トースト（既存 features/toast とは別コンポーネント）
```

### 設計決定と根拠

- **汎用APIの所在はサーバー**（ユーザー要件）。イベントパケットは `va:event:notification` の1本のみ。操作拒否は独立プロトコルではなく通知の1カテゴリ（`OperationDenied`）。
- **クライアントから Notify する口は作らない**。基盤の入口を1つにしてSSOTを保つ。クライアント都合の表示（bridge内部エラー等）は既存 `features/toast` が担当（責務が別）。
- **ペイロードは構造化データ**。サーバーは表示文言を作らない。文言解決はWeb側。基盤はカテゴリを知るがドメイン語彙（「レール」「研究」）は知らない（汎用基盤にドメイン語彙を持ち込まない原則）。
- **既存の状態同期イベント（`va:event:researchComplete` 等）はそのまま残す**。通知は追加の1本であり、状態同期の置き換えではない。
- **通知は揮発**。保存・履歴・未読管理はしない（通知センターはスコープ外・YAGNI）。切断中に発生した通知の再送もしない。初期ハンドシェイクへの同梱も不要（transientなので3点セットのうちイベントパケットのみで成立。クライアント側は `VanillaApiEvent` の InitializeDispatch 前バッファで取りこぼしが防がれる）。

### ペイロード

```
NotificationMessagePack {
    Category      : string  // "achievement" | "operationDenied" | (将来拡張)
    MessageId     : string  // ローカライズキーの構成要素。例 "rail.notEnoughRailItem", "research.completed"
    Params        : string[] // 文言埋め込み用パラメータ（アイテム名キー・数値等）
    ItemGuid      : string?  // 任意。アイコン表示用のアイテム参照
}
```

- `Category` は表示スタイル（色・アイコン系統・表示位置）の選択にのみ使う。
- Web側は `MessageId` からローカライズキーを組み立てて文言解決（既存の LocalizationTopic / Web側ローカライズ機構に乗せる。キー欠落時はキー文字列をそのまま表示しフォールバック）。

### デデュープ/クールダウン（v1必須）

この基盤の最悪ケースは通知連打（レールドラッグ中の素材不足が毎フレーム級で発火、採掘連打での満杯通知等）。対策として `NotificationService` 内で **同一キー（Category + MessageId + playerId）の通知をクールダウン期間（数秒）内は抑制**する。抑制は送信前（サーバー側）に行い、ワイヤにスパムを乗せない。

### 配線パターン

- **達成系**: サーバーのドメインイベント（`ResearchEvent.OnResearchCompleted` 等）をUniRxで購読して `Notify` を呼ぶ配線クラスを `Server.Event` 側に置く。既存の `EventReceive/*Packet.cs` がドメインイベントを購読してパケット化している先行パターンと同型。プロトコル変更ゼロ。
- **失敗系**: 各プロトコルの失敗判定地点に `_notificationService.Notify(playerId, OperationDenied, ...)` を1行追加。SendOnly構造・既存Responseの形は変更しない。今後の新プロトコルもこの1行で通知対応が完了する。

## スコープ

### v1

- 基盤: サーバー `NotificationService`（デデュープ込み）＋ `va:event:notification` イベントパケット＋クライアント中継＋ `NotificationTopic` ＋ Web `features/notification` 表示
- 達成系配線: 研究完了・チャレンジ達成・アンロック
- 失敗系配線（混乱度上位）: `va:railConnectionEdit`・`va:removeTrainCar`・`va:placeBlock`・`va:oneClickCraft`・`va:completeResearch`

### v2以降（スコープ外）

- 残りの失敗系配線: 機械レシピ選択・電線接続・歯車チェーン接続・電柱/歯車ポール/橋脚延長・列車車両設置/連結・ベースキャンプ昇格
- MapObject採取の満杯時アイテム消失（挙動修正が必要な可能性があるため別課題）
- 通知センター（履歴・未読管理）
- クライアント発通知の口（必要になったら NotificationTopic に直接流す口を足せば済み、本設計は壊れない）

## 自己反証と限界

- **クライアントしか知らない失敗は通れない**（ローカルで事前にはじいた操作等）。棚卸しの結果、混乱の主因は全てサーバー側判定の無言失敗だったため、v1では許容。
- **placeBlock は失敗理由の構築自体がサーバーに存在しない**ため、失敗系で唯一「1行追加」では済まず、スキップ分岐ごとに理由を特定して Notify を呼ぶ変更になる（それでも各分岐に1行ずつ）。
- 研究完了の失敗通知はサーバー側 `CompleteResearchProtocol` に Notify を入れる形であり、クライアントが無視している応答 `Success` はそのまま（応答経路の改修はしない）。

## テスト方針

- サーバー単体: `NotificationService` のデデュープ（同一キー抑制・期間経過後の再送可）、`Notify`/`NotifyAll` が `EventProtocolProvider` へ正しいタグ・ペイロードで積まれること。
- 結合（PacketTest）: 失敗条件を作って操作プロトコルを叩き、`va:event:notification` が該当 MessageId で飛ぶこと（例: レール素材不足で接続 → `rail.notEnoughRailItem`）。
- クライアント: イベント受信 → NotificationTopic 配信の中継テスト（既存Topicテストの形に倣う）。
- Web: 通知表示・カテゴリ別スタイル・キー欠落フォールバックのコンポーネントテスト。

## 検証済み事実の範囲

- 棚卸し表の失敗条件・伝達区分はサーバー/クライアント実装の読解による（実行しての再現確認はしていない）。
- `va:removeBlock` の理由表示・`BlockGameObjectChild.cs:75` のTODOコメントはコード上で確認済み。
