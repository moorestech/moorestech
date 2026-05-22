# 乗車システム 設計仕様書

- 日付: 2026-05-20
- ブランチ: feature/create-ride
- 対象: moorestech サーバー / クライアント

## 1. 背景と目的

現状、列車（`TrainCar`）向けの操舵入力バッファ（`TrainCarRidingInputBuffer`）、入力送信プロトコル（`TrainCarRidingInputProtocol`、クライアント→サーバー片方向）、クライアント側の見た目だけの乗車（`TrainCarRidingPlayerController.ForceRide`、デバッグシート専用）は存在する。

しかし「プレイヤーが乗り物に乗っている」という状態そのものをサーバー権威で扱う仕組みが無い。`PlayerEntity` に乗車を表すフィールドは無く、乗車状態を配信するプロトコルも無く、乗車開始・降車の正規の入り口も無い。

本仕様は、この「乗車状態」を第一級のサーバー権威の概念として導入する。列車専用ではなく汎用の `IRidable` 抽象を用意し、`TrainCar` を最初の実装とする。将来 車・船・動物・エレベーター等へ `IRidable` 実装を追加するだけで拡張できる構造を目指す。

## 2. 全体方針

- 「乗車状態」は「プレイヤー ⇄ 乗り物 ⇄ 座席」の**関係のみ**を表す。プレイヤーのワールド座標の権威はクライアントのまま変更しない。
- クライアントは乗車中も自分のワールド座標（乗り物に追従した位置）を送信し続ける。サーバーはプレイヤー座標に応じて配信内容を変えるため座標を必要とする。
- 乗車状態（プレイヤーがどの乗り物のどの座席にいるか）はサーバー権威。クライアントは要求を送るだけで、確定はサーバーが行い配信する。プレイヤーのワールド座標はクライアント権威。両者は直交し、サーバーはプレイヤー座標を上書きしない。
- 座席は1人乗り。1つの乗り物が複数座席を持つ。運転席と乗客席の区別は無い。操舵入力は乗車中のプレイヤーが送る想定だが、入力送信可否のサーバー検証は本仕様では行わない（5.4・セクション12参照）。よって現状は乗車していなくても操舵入力を送れる余地が残るが、これは既知の制限として許容する。
- 乗り物はエンティティ（`IEntity`）ではない。列車などはそれぞれ専用 ID（`TrainCarInstanceId`）で管理・同期されている。よって「エンティティに乗る」ではなく「**識別子で指す何かに乗る**」概念として設計する。識別子は moorestech 既存の `ISubInventoryIdentifier` / `InventoryIdentifierMessagePack` パターンに合わせる。
- 「乗れるもの」の抽象単位は `TrainUnit`（編成）ではなく `TrainCar`（車両）単位とする。

## 3. コアデータモデル

乗り物を指す識別子は、`ISubInventoryIdentifier`（`Game.PlayerInventory.Interface/Subscription`）/ `InventoryIdentifierMessagePack`（`Server.Util/MessagePack`）と同じ構造を採用する。

- **`RidableType`** … enum（byte）。`TrainCar` から開始。将来 `Car` / `Boat` 等を追加。`InventoryType` に倣う。
- **`IRidableIdentifier`** … 乗り物を指す識別子インターフェース。`RidableType Type { get; }` を持ち、`Equals` / `GetHashCode` をオーバーライドする（Dictionary / HashSet のキーに使うため）。`ISubInventoryIdentifier` に倣う。
- **`TrainCarRidableIdentifier : IRidableIdentifier`** … `Type => RidableType.TrainCar`、フィールド `long TrainCarInstanceId`。`Equals` / `GetHashCode` は `TrainCarInstanceId` ベース。`TrainInventorySubInventoryIdentifier` に倣う。識別子の型は `TrainCarInstanceId` 構造体ではなく primitive `long` を使う（`TrainInventorySubInventoryIdentifier` と同じく、識別子アセンブリと `Game.Train` のアセンブリ循環参照を避けるため）。サーバーでの解決時に `new TrainCarInstanceId(long)` へ変換する。
- **`RidableIdentifierMessagePack`** … ネットワーク用。enum discriminator ＋ ペイロード方式（MessagePackUnion は使わない）。`[MessagePackObject]`、`[Key(0)] RidableType`、`[Key(1)] string TrainCarInstanceId`（TrainCar の場合のみ設定、`long.ToString()`）。ファクトリ `CreateTrainCarMessage(TrainCarInstanceId)`。`InventoryIdentifierMessagePack` に倣う。
- **識別子の相互変換** … `IRidableIdentifier.ToMessagePack()` 拡張メソッドと、`RidableIdentifierMessagePack` → `IRidableIdentifier` の逆変換（enum 判定の switch）。`ISubInventoryIdentifierExtension` および `SubscribeInventoryProtocol.ConvertIdentifier` に倣う。
- **`RidingState`** … プレイヤー1人の乗車状態。`{ IRidableIdentifier identifier, int seatIndex }`。
- **`IRidable`**（サーバー側、乗り物の実体が実装するインターフェース）
  - `IRidableIdentifier Identifier`
  - `int SeatCount` … 座席数。空席割当・占有判定にのみ使う。
  - サーバーは座席のワールド座標を計算しない（後述、Codex 指摘⑦）。座席オフセットはマスタデータでクライアントのみが使用する。

## 4. サーバー側コンポーネント

### 4.0 データフローの原則
- 乗車状態の決定ロジック（乗車可否・空席割当・降車・ログイン復帰判定・車両消失時の降車）は **`PlayerRidingDatastore` に集約**する。
- `RidableResolver` を参照するのは `PlayerRidingDatastore` のみ。プロトコル（`RideActionProtocol`）・`InitialHandshakeProtocol`・車両削除ハンドラは `RidableResolver` を直接触らず、すべて `PlayerRidingDatastore` のメソッドを呼ぶだけにする。
- 依存方向: `RideActionProtocol` / `InitialHandshakeProtocol` / 車両削除ハンドラ → `PlayerRidingDatastore` → `RidableResolver` → 既存データストア。一方向で循環しない。

### 4.1 PlayerRidingDatastore
- `playerId ⇄ RidingState` を双方向管理する新規データストア。
- 乗り物識別子 → 乗員一覧 の逆引きも提供（`IRidableIdentifier` の `Equals` / `GetHashCode` をキーに使う）。
- `RidableResolver` を内部依存として保持し、乗車状態の決定ロジックを公開メソッドとして提供する。少なくとも以下を持つ:
  - `TryRide(playerId, IRidableIdentifier) → RideActionResult` … 乗り物解決・乗車中チェック・空席割当を内部で行い、成功時は `RidingState` を設定。成功時は割り当てた `seatIndex` も返す。
  - `TryDismount(playerId) → RideActionResult` … 乗車中チェックののち `RidingState` をクリア。
  - `EvaluateOnLogin(playerId) → 復帰結果` … ログイン復帰判定（セクション8）。
  - `OnRidableRemoved(IRidableIdentifier)` … 車両消失時の乗員降車（セクション4.4）。
- `PlayerEntity`（Game.Entity.Interface）には乗車フィールドを持たせない。責務分離のため独立コンポーネントとする。
- セーブ対象（後述）。
- 凍結降車座標は持たない。降車座標が必要な場合は `EntitiesDatastore` の値（＝最後にクライアントが送った座標）を使う。

### 4.2 RidableResolver
- `IRidableIdentifier` から乗り物の実体（`IRidable`）を解決する。`PlayerRidingDatastore` からのみ使われる（4.0参照）。
- 中央レジストリは設けない。`InventoryItemMoveProtocol.ResolveSubInventory` と同じく、`RidableType` の enum 判定 ＋ 既存データストアのルックアップで解決する。
- `RidableType.TrainCar` → 既存 `TrainUnitLookupDatastore.TryGetTrainCar(TrainCarInstanceId)` で `TrainCar` を取得（車両単位）。
- 解決できない（乗り物が存在しない）場合は null を返す。

### 4.3 TrainCar の IRidable 適合
- `TrainCar` を `IRidable` に適合させる（直接実装、または薄いアダプタ）。
- `Identifier` は `new TrainCarRidableIdentifier(TrainCarInstanceId)`。
- `SeatCount` はマスタデータ（座席スキーマ、後述）から取得する。

### 4.4 乗り物破棄の検知
- 編成単位の削除イベント（`TrainUnitSnapshotNotifyEvent.NotifyDeleted`）は `TrainUnitInstanceId` しか持たず、車両1両削除でも編成全体の delete snapshot が出るため、これを「乗り物破棄」の検知には使わない（Codex 指摘②）。
- 代わりに、**車両 ID を持つ既存イベント `TrainUpdateEvent.OnTrainCarRemoved` を購読する**。このイベントは削除された `TrainCar` を特定できる。
- 起動タイミング: `TrainUnitDatastore` 等の更新が完了し `RidableResolver` で当該車両が解決できなくなった後にハンドラが走るよう、イベント発火点を確認する（計画段階で `OnTrainCarRemoved` の発火順を検証。datastore 更新前に走るなら順序対策が必要）。
- ハンドラ処理: 削除された `TrainCar` の `TrainCarRidableIdentifier` を引数に `PlayerRidingDatastore.OnRidableRemoved()` を呼ぶ。`PlayerRidingDatastore` は逆引きで該当 `RidingState` を列挙し以下を行う。
  - **接続中の乗員**: `RidingState` をクリアし `RidingStateEvent`（降車）を broadcast。プレイヤー座標はクライアント権威のため上書きしない（クライアントも車両消失を検知し自己降車する。セクション9）。
  - **切断中の乗員**: `RidingState` はそのまま残す。ログイン時に検知され降車処理される（セクション8）。
- 降車処理は冪等とする。既に降車済みの `RidingState` に対する降車は no-op。`OnTrainCarRemoved` と train snapshot の到達順がクライアントで前後しても、二重降車・逆順降車が壊れないようにする（セクション9・11）。

### 4.5 座標導出は不要
- プレイヤー座標はクライアントが送信し続けるため、サーバー側で乗り物から毎tick座標を導出する処理は設けない。

## 5. プロトコル

### 5.1 RideActionProtocol（C→S、request-response、統合プロトコル）
- 乗車要求と降車要求を1本のプロトコルに統合し、`enum RideActionType { Ride, Dismount }` で区別する。
- リクエストペイロード: `playerId`, `RideActionType action`, `RidableIdentifierMessagePack target`（Ride時のみ有効）。
- レスポンスペイロード: 成否（`enum RideActionResult { Success, NoSeatAvailable, RidableNotFound, AlreadyRiding, NotRiding }` 等）。Ride 成功時は割り当てられた `seatIndex` も返す。
- サーバー処理: 受信した `RidableIdentifierMessagePack` を `IRidableIdentifier` に逆変換し、`PlayerRidingDatastore.TryRide()` / `TryDismount()` を呼ぶだけ。`RidableResolver` や空席判定をプロトコル側に書かない（4.0参照）。
- `Ride` 時: `PlayerRidingDatastore.TryRide()` の結果（`RidableNotFound` / `AlreadyRiding` / `NoSeatAvailable` / `Success`）をそのままレスポンスにする。`Success` 時はサーバーが `RidingStateEvent` を broadcast（他プレイヤー・将来のリモート描画用）。
- `Dismount` 時: `PlayerRidingDatastore.TryDismount()` の結果（`NotRiding` / `Success`）をレスポンスにする。`Success` 時はサーバーが `RidingStateEvent` を broadcast。
- 反映の責務分担（broadcast 待ちによる遅延を避けるため）:
  - **プレイヤー起因の乗車・降車**（自分が出した `RideActionProtocol`）: 要求元クライアントは `RideActionResult` レスポンスを確認し、**自分で**見た目を反映する。乗車 `Success` なら返ってきた `seatIndex` の座席へプレイヤーをテレポート（parent ＋ 位置合わせ）。降車 `Success` なら座席から解除。`RidingStateEvent` の自分宛 broadcast を待たない。ローカル予測はしない（レスポンス受信後に反映）。
  - **サーバー起因の降車**（乗り物破棄など）: クライアントは `RidingStateEvent` の受信、または「乗車中の対象車両が削除されたら強制降車」の自己検知で反映する（セクション9）。この時点でクライアントは既にイベントポーリング登録済みのため broadcast は届く。

### 5.2 RidingStateEventPacket（S→C broadcast）
- `EventProtocolProvider.AddBroadcastEvent` 経由で配信。
- ペイロード: `playerId`, `RidableIdentifierMessagePack? target`, `int? seatIndex`（降車時は null）。
- 乗車・降車の両方を表現する。クライアントは現状、自分の `playerId` の **サーバー起因イベント**（主にサーバー起因の降車）のみを処理する（セクション9）。他プレイヤー分は将来のリモートプレイヤー描画実装時に消費する。
- クライアント側のイベント適用は冪等とする（同じ降車イベントを複数回受けても、自己検知降車と重なっても壊れない。セクション4.4・11）。

### 5.3 ログイン時の自プレイヤー乗車状態の伝達
- ログイン時のサーバー側評価（セクション8）の結果（復帰した `RidingState`、または無し）を、`InitialHandshakeProtocol` のレスポンスに含めて返す。クライアントはスポーン時に自分が乗車中かを知り、自己を座席に parent できる。
- 全乗車プレイヤーの一括スナップショット配信は、リモートプレイヤー描画システムが無い現状では不要。リモートプレイヤー描画の実装時に併せて設計する。

### 5.4 既存 TrainCarRidingInputProtocol
- 変更しない。乗車状態に基づく操舵入力の検証（入力の `RidingTrainCarInstanceId` と乗車中車両の一致確認）は、複雑度が増すため本仕様では実施しない（Codex 指摘⑩、対応見送り）。
- この結果、乗車していないプレイヤーが操舵入力を送れる余地が残るが、既知の制限として許容する（セクション2・12）。将来、検証を入れる場合は本プロトコルに `PlayerRidingDatastore` 参照を追加する拡張点として残す。

## 6. マスタデータ

- 座席定義は `train.yml` に直接書かず、専用スキーマ YAML に切り出す。`train.yml` の `trainCars` から `ref` で参照する。
- 専用スキーマには座席セット（各座席 `{ offsetX, offsetY, offsetZ }`）を定義する。座席数（`SeatCount`）と座席オフセットの両方をこのスキーマが供給する。
- YAML スキーマの編集・`ref` 記法は moorestech の YAML 専用 skill に従う。SourceGenerator により `Mooresmaster.Model.*Module` を再生成する（自動生成物は手動編集禁止）。
- サーバーは座席数のみを使う。クライアントは座席オフセットを使ってプレイヤーを車両に対し相対配置する。両者が同一マスタを参照するため座席位置の定義は一元化される。

## 7. 接続状態管理（切断検知・新規）

切断検知は「切断したプレイヤーの座席を他プレイヤーが使えるようにする」ために必須。座席は1人乗りであり、切断プレイヤーの `RidingState` はログイン復帰用に保持され続けるため、座席占有の判定を「接続中プレイヤーのみ」で行う必要がある。

背景:
- `UserPacketHandler` はソケットから受信した `byte[]` を `ReceiveQueueProcessor` に渡すだけで、当初は接続コンテキストが `PacketResponseCreator` / 各プロトコルに渡らず、「どの接続の handshake か」をプロトコル層から識別できなかった。
- 切断時の挙動: 通常切断（`Socket.Receive` が 0）と例外時で扱いが異なり、`Cleanup` が必ず呼ばれるとは限らなかった。

本仕様で以下を新設・実装した:
- **接続コンテキストの導線（実装済み）**: 接続単位の `PacketResponseContext` を `IPacketResponse.GetResponse(byte[], PacketResponseContext)` の必須引数とし、全プロトコル共通で受け取る。`ServerListenAcceptor` が接続ごとに1つ生成し、`ReceiveQueueProcessor` 経由でプロトコル層へ渡す。`InitialHandshakeProtocol` は handshake 受信時に `context.BindPlayerId(playerId)` で「この接続 ⇄ `playerId`」を記録し、`UserPacketHandler.Cleanup` がそれを読んで `Unregister` する。`PacketResponseContext` は書き込み（メインスレッド）と読み取り（受信スレッド）がスレッドをまたぐため `lock` で保護する。当初検討した「二重インタフェース（`IConnectionAwarePacketResponse`）／handshake を接続層で特別扱い」案は、特殊な経路を残すため不採用とした。
- **切断イベント**: 通常切断・例外切断のいずれの経路でも、接続終了時に登録済み `playerId` で「プレイヤー切断イベント」が必ず1回発火するようにする。
- サーバーは各プレイヤーの接続中フラグを管理する。「接続中」＝有効な接続が存在すること。
- `playerId` の値自体は既存プロトコル同様 payload から受け取る（接続に紐付けた認証済み `playerId` でのなりすまし対策は既存プロトコル全体の課題であり、本仕様の対象外。セクション12）。接続 ⇄ `playerId` の紐付けは切断検知の目的にのみ使う。
- 座席占有の判定（乗車要求時の空席割当、ログイン復帰時の空席判定）は**接続中プレイヤーの `RidingState` のみ**を対象とする。切断中プレイヤーの `RidingState` は座席占有としてカウントしない。
- 切断プレイヤーの `RidingState` は `PlayerRidingDatastore` に保持し続ける（ログイン復帰用）。
- 同一 `playerId` での多重接続は許可しない。「1 `playerId` = 1 接続」を前提とし、接続管理は `PlayerConnectionRegistry` の `HashSet<int>` で行う（参照カウントは不要）。
- 切断検知は乗車システムが必要とする最小限に留める。汎用的なプレイヤーライフサイクル管理（切断プレイヤーの一般的なゴースト化対策など）は本仕様の対象外とし、別タスクとする。

## 8. ログイン時の復帰（InitialHandshakeProtocol 拡張）

`InitialHandshakeProtocol` は `PlayerRidingDatastore.EvaluateOnLogin(playerId)` を呼ぶだけ。判定ロジックは `PlayerRidingDatastore` 内に置く（4.0参照）。`PlayerRidingDatastore` はそのプレイヤーの `RidingState` があれば以下を判定する。

- **`RidableResolver` で乗り物を解決でき、記録席が有効かつ空いている** → 乗車復帰。`RidingState` を維持し、結果（復帰した乗車状態）を返す。`InitialHandshakeProtocol` はその結果をレスポンスに含める（セクション5.3）。`RidingStateEvent`（乗車）を broadcast。サーバーはプレイヤー座標を設定しない（クライアントが自己を座席に parent し、以後そのワールド座標を送信する）。
- **それ以外（乗り物が消失、記録席が範囲外、または記録席を他プレイヤーが使用中）** → `RidingState` をクリア。レスポンスは「乗車なし」。プレイヤー座標は `EntitiesDatastore` が保持する値（最後にクライアントが送った座標＝実質的に切断時点の座標）をそのまま使う。

復帰可否の条件:
- 「記録席が有効」＝ `0 <= seatIndex < 解決した IRidable.SeatCount`。マスタ変更やセーブ手編集で範囲外になり得るため必ず検査する（Codex 指摘⑦）。範囲外なら復帰せず降車扱い。
- 「記録席が空いている」＝同じ `(identifier, seatIndex)` を持つ**接続中の別プレイヤー**がいないこと（判定対象からログイン中の本人は除外する）。

## 9. クライアント側

- 本仕様のクライアント側スコープは**ローカルプレイヤーのみ**。moorestech には他プレイヤー（リモートプレイヤー）描画システムが存在しないため、他プレイヤーが乗車している表現は実装しない。サーバーは `RidingStateEvent` を broadcast するが、クライアントは自分の `playerId` のイベントのみを処理する。
- 既存 `TrainCarRidingPlayerController` を乗り物非依存の `RidingPlayerController` にリファクタする（ローカルプレイヤー専用のまま）。
- クライアント側も `IRidableIdentifier` から対象の乗り物オブジェクトを解決する。中央レジストリは設けず、`RidableType` 判定で既存のクライアント側ルックアップを使う（`TrainSubInventorySource` が `TrainCarInstanceId` から `TrainCarEntityObject` を得るのと同じ要領）。`RidableType.TrainCar` → `TrainCarInstanceId` から `TrainCarEntityObject` を解決する。
- 座席位置はマスタの座席オフセットを `TrainCarEntityObject` に対し相対適用して決める。
- `PlayerPositionSender` は乗車中も停止しない。乗車中はプレイヤーが乗り物に parent されるため、送信される座標は自然に乗り物追従座標になる。
- 入力: E キーで最寄りの乗り物を探索し `RideActionProtocol(Ride)` を送信。乗車中に E キーで `RideActionProtocol(Dismount)` を送信。
- **要求元クライアントの反映（broadcast 待ちにしない）**: `RideActionProtocol` のレスポンス（`RideActionResult`）を確認し、要求元クライアント自身が反映する。
  - 乗車 `Success`: レスポンスの `seatIndex` の座席へ自プレイヤーをテレポート（乗り物オブジェクトへ parent ＋ 座席オフセットで位置合わせ）。
  - 降車 `Success`: 座席から解除（parent 解除）。
  - 失敗（`NoSeatAvailable` 等）: 何もしない。
- `RidingStateEventPacket`（自分の `playerId` 分）受信は、**サーバー起因の降車**（乗り物破棄など）の反映にのみ使う。自分が出した `RideActionProtocol` の結果反映には使わない（上記レスポンス経由で済むため）。
- 既存 `TrainCarRidingPlayerController` が持つ「乗車中の対象車両が削除されたら強制降車」の自己検知は安全網として残す。
- 既存 `ForceRide` / `ForceDismount`（デバッグシート）は、この正規経路を呼ぶ形に置き換える。
- 既存の操舵入力送信（`TrainCarRidingInputSender` → `TrainCarRidingInputProtocol`）は維持する。

## 10. セーブ／ロード

- **`TrainCarSaveData` に `TrainCarInstanceId` を永続化する**。現状 `TrainCar` は生成のたびに新しい `TrainCarInstanceId` を採番し、`TrainCarSaveData` に ID を保存していないため、保存した `RidingState` の参照先がロード後に解決できない（Codex 指摘①、確定の不具合）。`TrainCarSaveData` に ID を含め、`TrainSaveLoadService` のロード（`RestoreTrainStates` / `RestoreTrainCar` 相当）で保存済み ID を再利用する。AGENTS.md より後方互換は考慮不要。これは列車インベントリ識別子（`TrainInventorySubInventoryIdentifier`）の安定化にも寄与する。
- **ID 一意性**: 永続 ID を導入するため、ロードした `TrainCarInstanceId` の一意性を担保する。`TrainCar` を datastore に登録する箇所で ID 重複を検出したらエラーとする。新規採番（`TrainCarInstanceId.Create()` 相当）は既存登録 ID と衝突しない値を返すようにする（採番ロジックの是正は計画段階で扱う）。
- `WorldSaveAllInfoV1` に `playerRidingStates` を追加する。各要素 `{ playerId, 識別子, seatIndex }`。
- 識別子の保存は `RidableType` ＋ 型別ペイロード（TrainCar なら `TrainCarInstanceId`）で行う。`RidableIdentifierMessagePack` と同じ enum discriminator 構造を JSON でも踏襲する。
- 凍結降車座標は保存しない（座標は既存のエンティティ座標セーブで保持される）。
- **ロード順序**: ワールド全体のロード順序（ワールドブロック → レール → 列車 → エンティティ等の既存順序）は変更しない。`playerRidingStates` のロードのみ追加し、**列車復元より後**に行う。`playerRidingStates` のロードは `PlayerRidingDatastore` を埋めるだけで、参照先（乗り物）の存在検証・整合・席範囲検査は行わない。整合の解決はログイン時（セクション8）まで遅延する。

## 11. エッジケース整理

| ケース | 挙動 |
|---|---|
| 乗車要求時に空席なし | `NoSeatAvailable` で拒否 |
| 乗車要求時に乗り物が存在しない | `RidableNotFound` で拒否 |
| 既に乗車中のプレイヤーが乗車要求 | `AlreadyRiding` で拒否（先に降車が必要） |
| 乗車中でないプレイヤーが降車要求 | `NotRiding` で拒否 |
| 乗車中に乗り物破壊（接続中の乗員） | 再検証で検出 → `RidingState` クリア、`RidingStateEvent` broadcast、クライアントが自己降車 |
| 乗車中に切断 | `RidingState` 保持、座席は接続中判定で他プレイヤーに開放 |
| 切断中に乗り物破壊 | `RidingState` は消えた乗り物を指したまま → ログイン時に検知し降車 |
| ログイン時に乗り物消失 | `RidingState` クリア、`EntitiesDatastore` の座標から開始 |
| ログイン時に記録席を他プレイヤーが使用中 | `RidingState` クリア、`EntitiesDatastore` の座標から開始 |
| ログイン時に記録席が範囲外（`seatIndex >= SeatCount`） | `RidingState` クリア、`EntitiesDatastore` の座標から開始 |
| ログイン時に記録席が有効・空き＆乗り物存在 | 乗車復帰、ハンドシェイク応答で自乗車状態を返す、broadcast |
| 同じ降車を二重に受信（自己検知＋サーバーイベント等） | 冪等処理で no-op、壊れない |

## 12. スコープ外

- 他プレイヤー（リモートプレイヤー）の描画システム。現状 moorestech に存在しないため、他プレイヤーが乗車している表現は実装しない。サーバーの `RidingStateEvent` broadcast は将来の実装に備えた配信のみ。
- 同一 `playerId` の二重接続の制御。「1 `playerId` = 1 接続」を前提とする。
- payload の `playerId` のなりすまし対策（接続に紐付けた認証済み `playerId` の強制）。既存プロトコル全体に共通する課題であり、本仕様だけで是正しない。
- 操舵入力の乗車状態検証（入力車両と乗車中車両の一致確認）。複雑度のため見送り。乗車していないプレイヤーが操舵入力を送れる余地が残る。
- 切断プレイヤーの一般的なゴースト化対策（乗車に関係しない接続管理の作り込み）。
- 運転席／乗客席の権限区別。
- 列車以外の `IRidable` 実装（車・船・動物等）。本仕様は抽象だけ用意し実装は列車のみ。
- 乗車開始・降車の正式な UI（E キー入力のみ。リッチな UI は別タスク）。
