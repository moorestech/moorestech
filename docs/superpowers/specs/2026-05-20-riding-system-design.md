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
- 座席は1人乗り。1つの乗り物が複数座席を持つ。運転席と乗客席の区別は無く、乗車中の誰でも操舵入力を送れる。
- 乗り物はエンティティ（`IEntity`）ではない。列車などはそれぞれ専用 ID（`TrainCarInstanceId`）で管理・同期されている。よって「エンティティに乗る」ではなく「**識別子で指す何かに乗る**」概念として設計する。識別子は moorestech 既存の `ISubInventoryIdentifier` / `InventoryIdentifierMessagePack` パターンに合わせる。
- 「乗れるもの」の抽象単位は `TrainUnit`（編成）ではなく `TrainCar`（車両）単位とする。

## 3. コアデータモデル

乗り物を指す識別子は、`ISubInventoryIdentifier`（`Game.PlayerInventory.Interface/Subscription`）/ `InventoryIdentifierMessagePack`（`Server.Util/MessagePack`）と同じ構造を採用する。

- **`RidableType`** … enum（byte）。`TrainCar` から開始。将来 `Car` / `Boat` 等を追加。`InventoryType` に倣う。
- **`IRidableIdentifier`** … 乗り物を指す識別子インターフェース。`RidableType Type { get; }` を持ち、`Equals` / `GetHashCode` をオーバーライドする（Dictionary / HashSet のキーに使うため）。`ISubInventoryIdentifier` に倣う。
- **`TrainCarRidableIdentifier : IRidableIdentifier`** … `Type => RidableType.TrainCar`、フィールド `TrainCarInstanceId TrainCarInstanceId`。`Equals` / `GetHashCode` は `TrainCarInstanceId` ベース。`TrainInventorySubInventoryIdentifier` に倣う。
- **`RidableIdentifierMessagePack`** … ネットワーク用。enum discriminator ＋ ペイロード方式（MessagePackUnion は使わない）。`[MessagePackObject]`、`[Key(0)] RidableType`、`[Key(1)] string TrainCarInstanceId`（TrainCar の場合のみ設定、`long.ToString()`）。ファクトリ `CreateTrainCarMessage(TrainCarInstanceId)`。`InventoryIdentifierMessagePack` に倣う。
- **識別子の相互変換** … `IRidableIdentifier.ToMessagePack()` 拡張メソッドと、`RidableIdentifierMessagePack` → `IRidableIdentifier` の逆変換（enum 判定の switch）。`ISubInventoryIdentifierExtension` および `SubscribeInventoryProtocol.ConvertIdentifier` に倣う。
- **`RidingState`** … プレイヤー1人の乗車状態。`{ IRidableIdentifier identifier, int seatIndex }`。
- **`IRidable`**（サーバー側、乗り物の実体が実装するインターフェース）
  - `IRidableIdentifier Identifier`
  - `int SeatCount` … 座席数。空席割当・占有判定にのみ使う。
  - サーバーは座席のワールド座標を計算しない（後述、Codex 指摘⑦）。座席オフセットはマスタデータでクライアントのみが使用する。

## 4. サーバー側コンポーネント

### 4.1 PlayerRidingDatastore
- `playerId ⇄ RidingState` を双方向管理する新規データストア。
- 乗り物識別子 → 乗員一覧 の逆引きも提供（`IRidableIdentifier` の `Equals` / `GetHashCode` をキーに使う）。
- `PlayerEntity`（Game.Entity.Interface）には乗車フィールドを持たせない。責務分離のため独立コンポーネントとする。
- セーブ対象（後述）。
- 凍結降車座標は持たない。降車座標が必要な場合は `EntitiesDatastore` の値（＝最後にクライアントが送った座標）を使う。

### 4.2 RidableResolver
- `IRidableIdentifier` から乗り物の実体（`IRidable`）を解決する。
- 中央レジストリは設けない。`InventoryItemMoveProtocol.ResolveSubInventory` と同じく、`RidableType` の enum 判定 ＋ 既存データストアのルックアップで解決する。
- `RidableType.TrainCar` → 既存 `TrainUnitLookupDatastore.TryGetTrainCar(TrainCarInstanceId)` で `TrainCar` を取得（車両単位）。
- 解決できない（乗り物が存在しない）場合は null を返す。

### 4.3 TrainCar の IRidable 適合
- `TrainCar` を `IRidable` に適合させる（直接実装、または薄いアダプタ）。
- `Identifier` は `new TrainCarRidableIdentifier(TrainCarInstanceId)`。
- `SeatCount` はマスタデータ（座席スキーマ、後述）から取得する。

### 4.4 乗り物破棄の検知
- 破棄検知はイベント粒度に依存しない。既存の列車削除イベント（`TrainUnitSnapshotNotifyEvent.NotifyDeleted`）は編成単位（`TrainInstanceId`）の情報しか持たず、車両1両削除でも編成全体の delete snapshot が出るため、これを直接「乗り物破棄」として扱わない（Codex 指摘②）。
- 代わりに**再検証（reconciliation）方式**を採る。列車の構成変更（車両削除・編成分割・`TrainUnit` 破棄など）が起きた後に、`PlayerRidingDatastore` の全 `RidingState` を `RidableResolver` で再解決し、解決できなくなった（車両が存在しない）状態を検出する。
- 解決できなくなった `RidingState` のうち、**接続中の乗員**: `RidingState` をクリアし `RidingStateEvent`（降車）を broadcast。プレイヤー座標はクライアント権威のため上書きしない（クライアントが車両消失を検知し自己降車する。セクション9）。
- 解決できなくなった `RidingState` のうち、**切断中の乗員**: `RidingState` はそのまま残す。ログイン時に検知され降車処理される（セクション8）。

### 4.5 座標導出は不要
- プレイヤー座標はクライアントが送信し続けるため、サーバー側で乗り物から毎tick座標を導出する処理は設けない。

## 5. プロトコル

### 5.1 RideActionProtocol（C→S、request-response、統合プロトコル）
- 乗車要求と降車要求を1本のプロトコルに統合し、`enum RideActionType { Ride, Dismount }` で区別する。
- リクエストペイロード: `playerId`, `RideActionType action`, `RidableIdentifierMessagePack target`（Ride時のみ有効）。
- レスポンスペイロード: 成否（`enum RideActionResult { Success, NoSeatAvailable, RidableNotFound, AlreadyRiding, NotRiding }` 等）。
- サーバーは受信した `RidableIdentifierMessagePack` を `IRidableIdentifier` に逆変換し、`RidableResolver` で `IRidable` を解決する。
- `Ride` 時: 乗り物が解決できなければ `RidableNotFound`。既に乗車中なら `AlreadyRiding`（移乗は不可、先に降車が必要）。空席（接続中プレイヤーが占有していない座席）が無ければ `NoSeatAvailable`。割当成功なら `RidingState` を設定し `RidingStateEvent` を broadcast、`Success` を返す。
- `Dismount` 時: 乗車中でなければ `NotRiding`。乗車中なら `RidingState` をクリアし `RidingStateEvent` を broadcast、`Success` を返す。
- クライアントは乗車・降車をローカル予測しない。`RideActionResult` と `RidingStateEvent` を受けて初めて見た目を反映する。

### 5.2 RidingStateEventPacket（S→C broadcast）
- `EventProtocolProvider.AddBroadcastEvent` 経由で配信。
- ペイロード: `playerId`, `RidableIdentifierMessagePack? target`, `int? seatIndex`（降車時は null）。
- 乗車・降車の両方を表現する。クライアントは現状、自分の `playerId` のイベントのみを処理する（セクション9）。他プレイヤー分は将来のリモートプレイヤー描画実装時に消費する。

### 5.3 ログイン時の自プレイヤー乗車状態の伝達
- ログイン時のサーバー側評価（セクション8）の結果（復帰した `RidingState`、または無し）を、`InitialHandshakeProtocol` のレスポンスに含めて返す。クライアントはスポーン時に自分が乗車中かを知り、自己を座席に parent できる。
- 全乗車プレイヤーの一括スナップショット配信は、リモートプレイヤー描画システムが無い現状では不要。リモートプレイヤー描画の実装時に併せて設計する。

### 5.4 既存 TrainCarRidingInputProtocol
- 変更しない。乗車状態に基づく操舵入力の検証（入力の `RidingTrainCarInstanceId` と乗車中車両の一致確認）は、複雑度が増すため本仕様では実施しない（Codex 指摘⑩、対応見送り）。

## 6. マスタデータ

- 座席定義は `train.yml` に直接書かず、専用スキーマ YAML に切り出す。`train.yml` の `trainCars` から `ref` で参照する。
- 専用スキーマには座席セット（各座席 `{ offsetX, offsetY, offsetZ }`）を定義する。座席数（`SeatCount`）と座席オフセットの両方をこのスキーマが供給する。
- YAML スキーマの編集・`ref` 記法は moorestech の YAML 専用 skill に従う。SourceGenerator により `Mooresmaster.Model.*Module` を再生成する（自動生成物は手動編集禁止）。
- サーバーは座席数のみを使う。クライアントは座席オフセットを使ってプレイヤーを車両に対し相対配置する。両者が同一マスタを参照するため座席位置の定義は一元化される。

## 7. 接続状態管理（切断検知・新規）

切断検知は「切断したプレイヤーの座席を他プレイヤーが使えるようにする」ために必須。座席は1人乗りであり、切断プレイヤーの `RidingState` はログイン復帰用に保持され続けるため、座席占有の判定を「接続中プレイヤーのみ」で行う必要がある。

- 接続受付（`ServerListenAcceptor` / `UserPacketHandler`）でクライアント切断は検知可能（`Socket.Receive` が 0 を返す → `Cleanup`）。ただし現状ソケットと `playerId` が紐付いておらず、`Cleanup` で `playerId` の登録解除も行われていない。
- 本仕様で以下を新設する:
  - `InitialHandshakeProtocol` 受信時に、その接続（`UserPacketHandler`）に `playerId` を記録し、`playerId ⇄ 接続` の対応を管理する。
  - 接続の `Cleanup`（切断）時に、記録した `playerId` で「プレイヤー切断イベント」を発火する。
  - サーバーは各プレイヤーの接続中フラグを管理する。「接続中」＝有効な接続が存在すること。
- 座席占有の判定（乗車要求時の空席割当、ログイン復帰時の空席判定）は**接続中プレイヤーの `RidingState` のみ**を対象とする。切断中プレイヤーの `RidingState` は座席占有としてカウントしない。
- 切断プレイヤーの `RidingState` は `PlayerRidingDatastore` に保持し続ける（ログイン復帰用）。
- 同一 `playerId` での二重接続の制御は本仕様の対象外とする。「1 `playerId` = 1 接続」を前提として進める（別タスク）。
- 切断検知は乗車システムが必要とする最小限に留める。汎用的なプレイヤーライフサイクル管理（切断プレイヤーの一般的なゴースト化対策など）は本仕様の対象外とし、別タスクとする。

## 8. ログイン時の復帰（InitialHandshakeProtocol 拡張）

ログイン時、`PlayerRidingDatastore` にそのプレイヤーの `RidingState` があれば以下を判定する。

- **`RidableResolver` で乗り物を解決でき、記録席が空いている** → 乗車復帰。`RidingState` を維持し、レスポンスで自プレイヤーの乗車状態を返す（セクション5.3）。`RidingStateEvent`（乗車）を broadcast。サーバーはプレイヤー座標を設定しない（クライアントが自己を座席に parent し、以後そのワールド座標を送信する）。
- **それ以外（乗り物が消失、または記録席を他プレイヤーが使用中）** → `RidingState` をクリア。レスポンスは「乗車なし」。プレイヤー座標は `EntitiesDatastore` が保持する値（最後にクライアントが送った座標＝実質的に切断時点の座標）をそのまま使う。

「記録席が空いている」＝同じ `(identifier, seatIndex)` を持つ**接続中の別プレイヤー**がいないこと（判定対象からログイン中の本人は除外する）。

## 9. クライアント側

- 本仕様のクライアント側スコープは**ローカルプレイヤーのみ**。moorestech には他プレイヤー（リモートプレイヤー）描画システムが存在しないため、他プレイヤーが乗車している表現は実装しない。サーバーは `RidingStateEvent` を broadcast するが、クライアントは自分の `playerId` のイベントのみを処理する。
- 既存 `TrainCarRidingPlayerController` を乗り物非依存の `RidingPlayerController` にリファクタする（ローカルプレイヤー専用のまま）。
- クライアント側も `IRidableIdentifier` から対象の乗り物オブジェクトを解決する。中央レジストリは設けず、`RidableType` 判定で既存のクライアント側ルックアップを使う（`TrainSubInventorySource` が `TrainCarInstanceId` から `TrainCarEntityObject` を得るのと同じ要領）。`RidableType.TrainCar` → `TrainCarInstanceId` から `TrainCarEntityObject` を解決する。
- 座席位置はマスタの座席オフセットを `TrainCarEntityObject` に対し相対適用して決める。
- `PlayerPositionSender` は乗車中も停止しない。乗車中はプレイヤーが乗り物に parent されるため、送信される座標は自然に乗り物追従座標になる。
- 入力: E キーで最寄りの乗り物を探索し `RideActionProtocol(Ride)` を送信。乗車中に E キーで `RideActionProtocol(Dismount)` を送信。`RideActionResult` で成否を受ける。
- `RidingStateEventPacket`（自分の `playerId` 分）受信で、自プレイヤーを座席に parent / 解除する。サーバー起因の降車（乗り物破棄など）もこれで反映する。
- 既存 `TrainCarRidingPlayerController` が持つ「乗車中の対象車両が削除されたら強制降車」の自己検知は安全網として残す。
- 既存 `ForceRide` / `ForceDismount`（デバッグシート）は、この正規経路を呼ぶ形に置き換える。
- 既存の操舵入力送信（`TrainCarRidingInputSender` → `TrainCarRidingInputProtocol`）は維持する。

## 10. セーブ／ロード

- **`TrainCarSaveData` に `TrainCarInstanceId` を永続化する**。現状 `TrainCar` は生成のたびに新しい `TrainCarInstanceId` を採番し、`TrainCarSaveData` に ID を保存していないため、保存した `RidingState` の参照先がロード後に解決できない（Codex 指摘①、確定の不具合）。`TrainCarSaveData` に ID を含め、`TrainSaveLoadService` のロード（`RestoreTrainStates` / `RestoreTrainCar` 相当）で保存済み ID を再利用する。AGENTS.md より後方互換は考慮不要。これは列車インベントリ識別子（`TrainInventorySubInventoryIdentifier`）の安定化にも寄与する。
- `WorldSaveAllInfoV1` に `playerRidingStates` を追加する。各要素 `{ playerId, 識別子, seatIndex }`。
- 識別子の保存は `RidableType` ＋ 型別ペイロード（TrainCar なら `TrainCarInstanceId`）で行う。`RidableIdentifierMessagePack` と同じ enum discriminator 構造を JSON でも踏襲する。
- 凍結降車座標は保存しない（座標は既存のエンティティ座標セーブで保持される）。
- **ロード順序を明示する**: ①レール → ②列車（`TrainCarInstanceId` 再利用込み）→ ③エンティティ → ④`playerRidingStates`。`playerRidingStates` のロードは `PlayerRidingDatastore` を埋めるだけで、参照先（乗り物）の存在検証・整合は行わない。整合の解決はログイン時（セクション8）まで遅延する。

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
| ログイン時に記録席が空き＆乗り物存在 | 乗車復帰、ハンドシェイク応答で自乗車状態を返す、broadcast |

## 12. スコープ外

- 他プレイヤー（リモートプレイヤー）の描画システム。現状 moorestech に存在しないため、他プレイヤーが乗車している表現は実装しない。サーバーの `RidingStateEvent` broadcast は将来の実装に備えた配信のみ。
- 同一 `playerId` の二重接続の制御。「1 `playerId` = 1 接続」を前提とする。
- 操舵入力の乗車状態検証（入力車両と乗車中車両の一致確認）。複雑度のため見送り。
- 切断プレイヤーの一般的なゴースト化対策（乗車に関係しない接続管理の作り込み）。
- 運転席／乗客席の権限区別。
- 列車以外の `IRidable` 実装（車・船・動物等）。本仕様は抽象だけ用意し実装は列車のみ。
- 乗車開始・降車の正式な UI（E キー入力のみ。リッチな UI は別タスク）。
