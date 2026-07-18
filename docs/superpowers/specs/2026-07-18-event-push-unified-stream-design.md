# サーバー→クライアント イベント配信のpush型一本化 設計

作成: 2026-07-18 ／ 対象: moorestech_server・moorestech_client

## 背景と解決する問題

現行のイベント配信は「クライアントが50msごとに `va:event` をポーリングし、サーバーが溜めたイベントを応答で全返しする」pull型。request/response（即応）とイベント（ポーリング遅延）の配達経路が2本に分かれていることが、train同期資料の事象1〜5すべての共通根になっている。

| 事象 | 内容 | 本設計での解消 |
|---|---|---|
| 1 | 到着順と生成順の逆転 | 応答もイベントも接続ごとの単一送信FIFOに生成順で積む（到着順=生成順を輸送層で保証） |
| 2 | snapshot適用ルール2本併存 | 初期同期・resyncのsnapshotをイベント経路でpushし、適用を`TrainUnitFutureMessageBuffer`経由の1本に統合 |
| 3 | 初回同期でイベント消失（実測13tick） | クライアントは購読開始まで全イベントをバッファしreplay。snapshotがストリーム内の位置を持つため「続き」が自明 |
| 4 | snapshot要求が他クライアントにseq穴を作る | full snapshotは`NextTickSequenceId()`を消費せず、発行済み最新IDをwatermarkとして記録するだけにする |
| 5 | 無事故が暗黙の実装詳細に全依存 | 順序保証を明文化した契約とし、PacketTestで固定する |

## 全体像

TCP 1本・接続ごとの送信FIFO（`SendQueueProcessor`）に、request/responseの応答もイベントも**生成した瞬間に**積む。ポーリングは廃止。

```
サーバー(メインスレッド直列)                      クライアント
  AddEvent ──┐                                ┌─ 受信キュー(到着順に直列ディスパッチ)
  応答enqueue ─┴→ SendQueueProcessor(FIFO) → TCP →┤   Tag=va:event → VanillaApiEvent
                                              └   それ以外     → SequenceId応答待ち
```

`AddEvent` 呼び出しと応答enqueueはすべてメインスレッド（`GameUpdater.LateUpdate` のパケット処理・tick Update）で直列に起こるため、enqueue順=生成順が成立する。

## サーバー側の変更

### 1. EventProtocolProvider: キューから即時ルーターへ

- `AddEvent(playerId, tag, payload)` / `AddBroadcastEvent(tag, payload)` の呼び出し側シグネチャは維持。内部を「per-playerキューに蓄積」から「登録済みsinkへ即時送信」に置換
- `RegisterPlayer(playerId, IPlayerEventSink sink)` に変更。未登録プレイヤー宛・切断済み宛のイベントは破棄する（handshakeで全量を取り直すため正しい。現行の「切断プレイヤーのキューが無限成長する」問題も同時に消える）
- 登録解除は `PlayerConnectionRegistry.OnPlayerDisconnected` を購読して行う（boot時にDI配線）
- 登録完了直後に `IObservable<int> OnPlayerEventStreamRegistered` を発火する（→初期同期の起点）

### 2. IPlayerEventSink（新設・Server.Event）

- 1メソッド: シリアライズ済みパケットを送信キューへenqueue
- 実装は `Server.Boot/Loop/PacketProcessing` に置き、接続の `SendQueueProcessor` をラップ（パケット長ヘッダ付与も担う）
- `PacketResponseContext` が接続のsink参照を保持し、`InitialHandshakeProtocol` が `RegisterPlayer(data.PlayerId, context.EventSink)` を呼ぶ（現行 `InitialHandshakeProtocol.cs:45` の置換。ここが唯一の登録点）
- スレッド安全性: `AddEvent` はメインスレッド限定（契約）。sink実体は `ConcurrentQueue` へのenqueueで安全。登録/解除は既存lockで保護

### 3. 送出envelope: EventStreamMessagePack（新設・Server.Event）

- `ProtocolMessagePackBase` 継承、Tag `va:event`、`Key(2)` に単一の `EventMessagePack`
- `EventProtocol`（`va:event` のrequest/response・`Server.Protocol/PacketResponse/EventProtocol.cs`）は削除。envelopeは応答ではないため `Server.Event` に置く（PacketResponse直下はIPacketResponse実装のみの規約）

### 4. 初期同期のイベント化（新設・Server.Event/EventReceive）

- `TrainInitialSyncEventPacket` が `OnPlayerEventStreamRegistered` を購読し、対象プレイヤーへ **railGraph full snapshot → trainUnit full snapshot の順**で `AddEvent(playerId, ...)` する
- 既存イベントパケットの「ゲームイベント購読→AddEvent」パターンと同型。購読対象が接続イベントになっただけ
- **契約: 初期同期の購読者は同期的にAddEventする**。即時push方式では handshake 応答のenqueueは `GetResponse` return後なので、同期購読なら「初期snapshot → handshake応答」のワイヤ順が構造的に保証される
- full snapshotイベントのタグは新設（例: `va:event:railGraphFullSnapshot` / `va:event:trainUnitFullSnapshots`）。payloadは現行 `GetTrainUnitSnapshotsProtocol.ResponseMessagePack` / `GetRailGraphSnapshotProtocol.ResponseMessagePack` の内容物と同等

### 5. resyncのイベント化: va:trainResync（1本に統合）

- `GetTrainUnitSnapshotsProtocol`（`va:getTrainUnitSnapshots`）と `GetRailGraphSnapshotProtocol` を削除し、`va:trainResync` 1本に置換（プロトコルは1ドメイン1本・リクエスト内フラグで分岐の規約）
- Request: `bool IncludeRailGraph`。Response: ackのみ（snapshot本体を含まない）
- サーバーはリクエスト処理中に §4 と同じfull snapshotイベントを要求元プレイヤーへ `AddEvent` する。snapshotはストリーム内の正しい位置（それ以前のdiffの後・それ以後のdiffの前）に物理的に置かれる

### 6. snapshot採番の変更（事象4の解消）

- full snapshot生成時に `NextTickSequenceId()` を**消費しない**（現行 `GetTrainUnitSnapshotsProtocol.cs:42` の廃止）
- 代わりに「発行済み最新の TickUnifiedId」をwatermarkとしてsnapshotに記録する。ストリーム上の物理位置が順序を保証するため、snapshot自身の新規採番は不要
- クライアントはwatermarkを適用済みIDとして記録すれば、以後のdiff（より大きいID）が正しく続く。他クライアントから見えるseq連番に穴が開かない

### 7. 順序契約（明文化しテストで固定）

1. `AddEvent`・`AddBroadcastEvent` はメインスレッドからのみ呼ぶ
2. `OnPlayerEventStreamRegistered` の購読者は同期的に `AddEvent` する（初期snapshotがhandshake応答より先にワイヤに載る根拠）
3. 接続ごとの送信はFIFO（`SendQueueProcessor`）
4. PacketTestで「初期同期イベント→handshake応答」「AddEvent順=送信順」を固定する

## クライアント側の変更

### 1. 受信ディスパッチの直列化

現行 `ServerCommunicator.cs:72` はパケットごとに `ExchangeReceivedPacket(packet).Forget()` で個別に `SwitchToMainThread` しており、処理順がPlayerLoopキューの実装詳細頼み。到着順どおりの単一キュー直列ディスパッチに明示化する（事象5と同種の暗黙前提を作らない）。

### 2. PacketExchangeManager: 無依頼パケットのルーティング

- 受信パケットの Tag が `va:event` なら応答待ちテーブルではなく `IObservable<EventMessagePack>`（UniRx）に流す。それ以外は現行どおりSequenceId照合（不一致は破棄）

### 3. VanillaApiEvent: ポーリング廃止と初回バッファ

- `CollectEvent` ループ（50msポーリング）を削除し、§2の購読に置換。`SubscribeEventResponse(tag, ...)` APIは不変（既存ハンドラ約20箇所は無修正）
- 接続直後は全受信イベントを到着順にバッファし、`StartDispatch()` で順にreplayしてから即時配信モードへ移行。`StartDispatch` はゲームシーンのハンドラ購読完了後（VContainerスコープ初期化完了フック。具体位置は実装計画で特定）に1回呼ぶ
- `StartDispatch` 以降の未購読タグは現行どおり破棄（無制限成長の回避）

### 4. 初期化ゲート

- ローディングフロー: handshake応答受信 → シーン起動・ハンドラ購読 → `StartDispatch()` → **train/rail初期snapshot適用完了をawait** → 初期化完了
- 待ちは明示ゲート: train側ハンドラが `UniTask WaitForInitialSnapshot()` 相当を公開し、ローディングシーケンスがawaitする。順序保証（§サーバー7-2）により通常は即時完了するが、届かなければ初期化が完了しないという性質を明示的に担保する
- `VanillaApiWithResponse.InitialHandShake` から `GetRailGraphSnapshot` / `GetTrainUnitSnapshots` の取得2件（`VanillaApiWithResponse.cs:64-65`）を削除

### 5. snapshot適用の一本化

- full snapshotイベントは既存イベント経路で受信し、`TrainUnitFutureMessageBuffer` にunified-id順で積み、flushで `TrainUnitSnapshotApplier` / `RailGraphSnapshotApplier` を呼んで適用する（適用順はrail → train）
- **即時適用パスを削除**: `TrainUnitHashVerifier` の `ApplySnapshot` 直呼び（`TrainUnitHashVerifier.cs:149-150,173`）を廃止。resyncは `va:trainResync` を送るだけにし、`_resyncInProgress` の解除は「full snapshot適用完了通知」で行う
- これで snapshot 適用は「イベント→buffer→id順flush」の1ルールのみになる

## 削除されるもの一覧

- サーバー: `EventProtocol`（IPacketResponse）と登録行／`EventProtocolProvider` のキュー・`GetEventBytesList`・Clear機構／`GetTrainUnitSnapshotsProtocol`／`GetRailGraphSnapshotProtocol`
- クライアント: `VanillaApiEvent.CollectEvent`（ポーリング）／`InitialHandShake` のtrain・rail取得2件／`TrainUnitHashVerifier` の即時適用・応答待ちロジック／`GetTrainUnitSnapshots`・`GetRailGraphSnapshot` APIメソッド
- 共通: `ServerConst.PollingRateMillSec`（他に利用がなければ）

## テスト

- **既存移行**: `va:event` ポーリングに依存するサーバーテスト13ファイルは、`EventTestUtil` に「キャプチャ用sinkを `RegisterPlayer` で登録し送信イベントをListに捕捉する」ヘルパーを実装して機械的に移行。アサート対象は現行と同じ `EventMessagePack`（tag+payload）なので検証ロジックは温存
- **新規（順序契約の固定）**: ①handshake処理で初期同期イベントがhandshake応答より先に送信キューへ積まれる ②`AddEvent` 順=送信順 ③`va:trainResync` でfull snapshotイベントが送出される ④未登録プレイヤー宛イベントが破棄される

## 検証項目とリスク

- **主リスク: イベントが応答より先に届くようになる**。現行は「応答が先・イベントは最大50ms後」だったが、リクエスト処理中に発火したイベント（ブロック設置broadcast等）はそのリクエストの応答より先に届く（=生成順）。応答先着に暗黙依存するクライアントハンドラが無いか、実装計画で「リクエスト処理中にAddEventする箇所」を全列挙して確認する
- 実プレイ検証: train走行でhash mismatch 0／resync誘発（デバッグ手段でhash不一致を起こす）で復帰確認／ブロック設置・インベントリ同期／初回接続で13tick消失が再発しないこと
- レイテンシ: ポーリング往復が消え純減（イベント到達は送信スレッドの50ms flush間隔のみ）

## スコープ外（残課題）

- `TrainUnitFutureMessageBuffer` 内部の簡素化（stale判定・掃除処理の見直しは順序保証導入後に別途）
- train以外の初期データ（インベントリ・research等）の接続イベント化。機構は汎用なので、必要になったドメインから順次乗せる
- `EventMessagePack` の未使用フィールド（`Key(2) MessagePacks`）掃除
