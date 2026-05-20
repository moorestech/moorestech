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
- 乗車状態はサーバー権威。クライアントは要求を送るだけで、確定はサーバーが行い全クライアントへ配信する。
- 座席は1人乗り。1つの乗り物が複数座席を持つ。運転席と乗客席の区別は無く、乗車中の誰でも操舵入力を送れる。
- 乗り物はエンティティ（`IEntity`）ではない。列車などはそれぞれ専用 ID（`TrainCarInstanceId`）で管理・同期されている。よって「エンティティに乗る」ではなく「**識別子で指す何かに乗る**」概念として設計する。識別子は moorestech 既存の `ISubInventoryIdentifier` / `InventoryIdentifierMessagePack` パターンに合わせる。

## 3. コアデータモデル

乗り物を指す識別子は、`ISubInventoryIdentifier`（`Game.PlayerInventory.Interface/Subscription`）/ `InventoryIdentifierMessagePack`（`Server.Util/MessagePack`）と同じ構造を採用する。

- **`RidableType`** … enum（byte）。`TrainCar` から開始。将来 `Car` / `Boat` 等を追加。`InventoryType` に倣う。
- **`IRidableIdentifier`** … 乗り物を指す識別子インターフェース。`RidableType Type { get; }` を持ち、`Equals` / `GetHashCode` をオーバーライドする（Dictionary / HashSet のキーに使うため）。`ISubInventoryIdentifier` に倣う。
- **`TrainCarRidableIdentifier : IRidableIdentifier`** … `Type => RidableType.TrainCar`、フィールド `TrainCarInstanceId TrainCarInstanceId`。`Equals` / `GetHashCode` は `TrainCarInstanceId` ベース。`TrainInventorySubInventoryIdentifier` に倣う。
- **`RidableIdentifierMessagePack`** … ネットワーク用。enum discriminator ＋ ペイロード方式（MessagePackUnion は使わない）。`[MessagePackObject]`、`[Key(0)] RidableType`、`[Key(1)] string TrainCarInstanceId`（TrainCar の場合のみ設定、`long.ToString()`）。ファクトリ `CreateTrainCarMessage(TrainCarInstanceId)`。`InventoryIdentifierMessagePack` に倣う。
- **識別子の相互変換** … `IRidableIdentifier.ToMessagePack()` 拡張メソッドと、`RidableIdentifierMessagePack` → `IRidableIdentifier` の逆変換（enum 判定の switch）。`ISubInventoryIdentifierExtension` および `SubscribeInventoryProtocol.ConvertIdentifier` に倣う。
- **`RidingState`** … プレイヤー1人の乗車状態。`{ IRidableIdentifier identifier, int seatIndex }`。
- **`SeatDefinition`** … 座席1つ。`{ Vector3 localOffset }`（将来 回転等を追加可）。
- **`IRidable`**（サーバー側、乗り物の実体が実装するインターフェース）
  - `IRidableIdentifier Identifier`
  - `IReadOnlyList<SeatDefinition> Seats`
  - `(Vector3 pos, Quaternion rot) GetSeatWorldTransform(int seatIndex)`

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
- `RidableType.TrainCar` → 既存 `TrainUnitLookupDatastore.TryGetTrainCar(TrainCarInstanceId)` で `TrainCar` を取得。
- 解決できない（乗り物が存在しない）場合は null を返す。

### 4.3 TrainCar の IRidable 適合
- `TrainCar` を `IRidable` に適合させる（直接実装、または薄いアダプタ）。
- `Identifier` は `new TrainCarRidableIdentifier(TrainCarInstanceId)`。
- 座席定義はマスタデータ（`train.yml` の座席参照、後述）から取得し `Seats` に供給する。

### 4.4 乗り物破棄時の処理
- 既存の列車削除パス（`RemoveTrainCarProtocol`、`TrainUnitSnapshotNotifyEvent.NotifyDeleted`）に乗車システムが反応する。乗車システムは列車削除イベントを購読し、その車両の全乗員を強制降車させる。
- 接続中の乗員: 現座席位置に配置、`RidingState` をクリアし `RidingStateEvent` を broadcast。
- 切断中の乗員: `RidingState` はそのまま残す（消えた乗り物を指す）。ログイン時に検知され降車処理される（セクション8）。

### 4.5 座標導出は不要
- プレイヤー座標はクライアントが送信し続けるため、サーバー側で乗り物から毎tick座標を導出する処理は設けない。

## 5. プロトコル

### 5.1 RideActionProtocol（C→S、統合プロトコル）
- 乗車要求と降車要求を1本のプロトコルに統合し、`enum RideActionType { Ride, Dismount }` で区別。
- ペイロード: `playerId`, `RideActionType action`, `RidableIdentifierMessagePack target`（Ride時のみ有効）。
- サーバーは受信した `RidableIdentifierMessagePack` を `IRidableIdentifier` に逆変換し、`RidableResolver` で `IRidable` を解決する。
- `Ride` 時: サーバーが対象乗り物の空席を検証して割り当てる。空席が無ければ要求を拒否（乗車失敗）。割当成功なら `RidingState` を設定し `RidingStateEvent` を broadcast。
- `Dismount` 時: `RidingState` をクリアし `RidingStateEvent` を broadcast。

### 5.2 RidingStateEventPacket（S→C broadcast）
- `EventProtocolProvider.AddBroadcastEvent` 経由で配信。
- ペイロード: `playerId`, `RidableIdentifierMessagePack? target`, `int? seatIndex`（降車時は null）。
- 乗車・降車の両方を表現する。

### 5.3 接続時スナップショット
- クライアント接続時に全乗車状態を初期同期する。`RequestWorldDataProtocol` の拡張、または専用スナップショットプロトコルで全 `RidingState` を送る。

### 5.4 既存 TrainCarRidingInputProtocol
- 維持する。ただしサーバーは入力受信時に `PlayerRidingDatastore` を参照し「そのプレイヤーが本当にその列車に乗っているか」を検証してから受理する。

## 6. マスタデータ

- 座席定義は `train.yml` に直接書かず、専用スキーマ YAML に切り出す。`train.yml` の `trainCars` から `ref` で参照する。
- 専用スキーマには座席セット（各座席 `{ offsetX, offsetY, offsetZ }`）を定義する。
- YAML スキーマの編集・`ref` 記法は moorestech の YAML 専用 skill に従う。SourceGenerator により `Mooresmaster.Model.*Module` を再生成する（自動生成物は手動編集禁止）。
- `TrainCar` は `TrainCarMasterElement` 経由で座席定義を取得し `IRidable.Seats` に供給する。

## 7. 接続状態管理（切断検知・新規）

切断検知は「切断したプレイヤーの座席を他プレイヤーが使えるようにする」ために必須。座席は1人乗りであり、切断プレイヤーの `RidingState` はログイン復帰用に保持され続けるため、座席占有の判定を「接続中プレイヤーのみ」で行う必要がある。

- socket／接続管理層でクライアント切断を検出し「プレイヤー切断イベント」を発火する仕組みを新設する。
- サーバーは各プレイヤーの接続中フラグを管理する。
- 座席占有の判定（乗車要求時の空席割当、ログイン復帰時の空席判定）は**接続中プレイヤーの `RidingState` のみ**を対象とする。切断中プレイヤーの `RidingState` は座席占有としてカウントしない。
- 切断時、その乗車プレイヤーのアバターを他クライアントから除去するため broadcast する。
- 切断プレイヤーの `RidingState` は `PlayerRidingDatastore` に保持し続ける（ログイン復帰用）。
- 切断検知は乗車システムが必要とする最小限に留める。汎用的なプレイヤーライフサイクル管理（切断プレイヤーの一般的なゴースト化対策など）は本仕様の対象外とし、別タスクとする。

## 8. ログイン時の復帰（InitialHandshakeProtocol 拡張）

ログイン時、`PlayerRidingDatastore` にそのプレイヤーの `RidingState` があれば以下を判定する。

- **`RidableResolver` で乗り物を解決でき、記録席が空いている** → 乗車復帰。プレイヤー座標＝座席位置。`RidingStateEvent` を broadcast。
- **それ以外（乗り物が消失、または記録席を他プレイヤーが使用中）** → `RidingState` をクリア。プレイヤー座標＝`EntitiesDatastore` が保持する値（最後にクライアントが送った座標＝実質的に切断時点の座標）。

「記録席が空いている」＝同じ `(target, seatIndex)` を持つ**接続中の別プレイヤー**がいないこと（判定対象からログイン中の本人は除外する）。

## 9. クライアント側

- 既存 `TrainCarRidingPlayerController` を乗り物非依存の `RidingPlayerController` にリファクタする。
- クライアント側も `IRidableIdentifier` から座席 Transform を解決する。中央レジストリは設けず、`RidableType` 判定で既存のクライアント側ルックアップを使う（`TrainSubInventorySource` が `TrainCarInstanceId` から `TrainCarEntityObject` を得るのと同じ要領）。`RidableType.TrainCar` → `TrainCarInstanceId` から `TrainCarEntityObject` を解決し、その座席アンカー Transform を得る。
- `PlayerPositionSender` は乗車中も停止しない。乗車中はプレイヤーが乗り物に parent されるため、送信される座標は自然に乗り物追従座標になる。
- 入力: E キーで最寄りの `IRidable` を探索し `RideActionProtocol(Ride)` を送信。乗車中に E キーで `RideActionProtocol(Dismount)` を送信。
- `RidingStateEventPacket` 受信で、対象プレイヤー（自分・他人を問わず）のアバターを座席 Transform に parent / 解除する。
- 既存 `ForceRide` / `ForceDismount`（デバッグシート）は、この正規経路を呼ぶ形に置き換える。
- 既存の操舵入力送信（`TrainCarRidingInputSender` → `TrainCarRidingInputProtocol`）は維持する。

## 10. セーブ／ロード

- `WorldSaveAllInfoV1` に `playerRidingStates` を追加する。各要素 `{ playerId, 識別子, seatIndex }`。
- 識別子の保存は `RidableType` ＋ 型別ペイロード（TrainCar なら `TrainCarInstanceId`）で行う。`RidableIdentifierMessagePack` と同じ enum discriminator 構造を JSON でも踏襲する。
- 凍結降車座標は保存しない（座標は既存のエンティティ座標セーブで保持される）。

### 設計リスク（計画段階で要検証）
TrainCar の識別子に `TrainCarInstanceId` を用いるため、セーブ→ロードで車両インスタンス ID が保たれる必要がある。`TrainCarSaveData` にインスタンス ID が含まれず、ロード時に再生成される場合、永続化した `RidingState` が壊れる。計画段階で `TrainCarSaveData` / `TrainSaveLoadService.RestoreTrainStates()` を確認すること。保たれない場合は、ロード時に車両の並び順などで再マッピングする対策が必要。なお、列車インベントリ（`TrainInventorySubInventoryIdentifier`）も同じ `TrainCarInstanceId` に依存しているため、既存のインベントリ側の扱いも参考になる。

## 11. エッジケース整理

| ケース | 挙動 |
|---|---|
| 乗車要求時に空席なし | 要求を拒否（乗車失敗） |
| 乗車中に乗り物破壊（接続中の乗員） | 強制降車、座席位置に配置、`RidingState` クリア、broadcast |
| 乗車中に切断 | `RidingState` 保持、他クライアントからアバター除去、座席は接続中判定で他者に開放 |
| 切断中に乗り物破壊 | `RidingState` は消えた乗り物を指したまま → ログイン時に検知し降車 |
| ログイン時に乗り物消失 | `RidingState` クリア、`EntitiesDatastore` の座標から開始 |
| ログイン時に記録席を他プレイヤーが使用中 | `RidingState` クリア、`EntitiesDatastore` の座標から開始 |
| ログイン時に記録席が空き＆乗り物存在 | 乗車復帰、座席位置に配置、broadcast |
| 乗車中プレイヤーの操舵入力 | `PlayerRidingDatastore` で乗車を検証できた場合のみ受理 |

## 12. スコープ外

- 切断プレイヤーの一般的なゴースト化対策（乗車に関係しない接続管理の作り込み）。
- 運転席／乗客席の権限区別。
- 列車以外の `IRidable` 実装（車・船・動物等）。本仕様は抽象だけ用意し実装は列車のみ。
- 乗車開始・降車の正式な UI（E キー入力のみ。リッチな UI は別タスク）。
