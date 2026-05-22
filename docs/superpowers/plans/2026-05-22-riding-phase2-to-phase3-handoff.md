# 乗車システム Phase 2 → Phase 3 申し送り

Phase 3（`docs/superpowers/plans/2026-05-21-riding-phase3-protocol-connection.md`）着手前に必読。
Phase 2 完了後（デュアルレビュー＋クライアント指摘の反映を含む）の実態と、計画からの逸脱をまとめる。

---

## 1. アセンブリ構成（確定）

- `Game.PlayerRiding.Interface` — インターフェースと DTO。参照は `MessagePack.Annotations` のみ。
  `IRidable` / `IRidableIdentifier` / `TrainCarRidableIdentifier` / `IRidableIdentifier` /
  `RidingState` / `RideActionResult` / `RidableType` / `IPlayerRidingDatastore` /
  `RidableIdentifierMessagePack` / `RidableIdentifierConverter` / `PlayerRidingSaveData` を持つ。
- `Game.PlayerRiding` — 実装。`RidableResolver` / `PlayerRidingDatastore`。`Game.Train` 等を参照。
- `Game.PlayerConnection` — 新規。`IPlayerConnectionChecker` と暫定実装 `AlwaysConnectedChecker`。
  接続判定は乗車の関心事でないため独立アセンブリにした（参照なし）。

**原則: 利用側は `.Interface` の抽象に依存する。実装アセンブリへの直接依存は DI 登録を行う
`Server.Boot` のみ。** Phase 3 のプロトコルも `IPlayerRidingDatastore` に依存すること。

## 2. `IPlayerRidingDatastore`（Phase 3 のプロトコルが使う窓口）

`PlayerRidingDatastore` の完全な契約。プロトコル/イベント/セーブシステムはこれ経由で使う:
`TryRide` / `TryDismount` / `OnRidableRemoved` / `EvaluateOnLogin` / `TryGetRidingState` /
`GetSaveData` / `LoadSaveData`。DI に `IPlayerRidingDatastore → PlayerRidingDatastore`
（singleton）登録済み。Phase 3 のプロトコルは `IPlayerRidingDatastore` をコンストラクタ DI で受ける。

## 3. `RidableType` は enum ではなく `[UnitOf(typeof(string))]` 構造体

型安全と mod 拡張性の両立のため string ベースの UnitOf 型。
- ビルトイン値は `RidableType.TrainCar`（`static readonly`）。mod は `new RidableType("...")` で追加可。
- `case RidableType.TrainCar:` は **不可**（`static readonly` は case ラベルにできない）。
  判別は `if (x == RidableType.TrainCar)` で行う。新種別の分岐追加時も if/else。

## 4. 識別子のシリアライズ（2 経路・混同しない）

- **wire（プロトコル）**: `RidableIdentifierConverter.ToMessagePack()` / `FromMessagePack()`。
  `RidableIdentifierMessagePack` は primitive 文字列フィールド（`RidableType` / `TrainCarInstanceId` とも string）。
- **永続化（セーブ）**: `IRidableIdentifier.GetSaveState()`（型ごとに自分のペイロード文字列を返す）と
  `RidableIdentifierConverter.FromSaveState(RidableType, string)`。`PlayerRidingSaveData` は
  `{ PlayerId, RidableType(string), IdentifierState(string), SeatIndex }`。乗り物種別が増えても DTO 不変。
- 新 `RidableType` を足すときは ①`IRidableIdentifier` 実装に `GetSaveState()` ②`FromSaveState`
  ③`ToMessagePack`/`FromMessagePack` ④`RidableResolver.Resolve` に分岐を追加する。

## 5. `IPlayerConnectionChecker` を実接続レジストリへ差し替える（Phase 3 の主要タスク）

Phase 2 は `AlwaysConnectedChecker`（常に true）の暫定実装。**Phase 3 で実接続判定に差し替える。**
- `PlayerRidingDatastore` は座席占有判定（`TryRide` の空席探索）と `OnRidableRemoved` で
  `IPlayerConnectionChecker.IsConnected(playerId)` を使う。
- `OnRidableRemoved` は既に接続状態を見る実装（接続中＝降車、切断中＝`RidingState` 保持）。
  実 checker に差し替えた時点で仕様書§4.4 の挙動が有効になる。
- DI 登録は `MoorestechServerDIContainerGenerator`。`AlwaysConnectedChecker` の登録行を
  実装に差し替える。

## 6. Phase 3 で対応する既知の検討事項（Phase 1→2 申し送り §6 より再掲）

- **`OnTrainCarRemoved` の発火順**: `TrainCar.Destroy()` 由来イベントは `TrainUnitDatastore`
  更新より前に発火する。仕様§4.4 は「`RidableResolver` で解決できなくなった後」を前提。
  購読配線時に発火点を datastore 更新後へ移すか、削除プロトコル側で更新後に呼ぶ。
- **`EvaluateOnLogin` の戻り値**: 現状 `bool` のみ。仕様§8 は復帰した `RidingState` を
  `InitialHandshakeProtocol` のレスポンスに含める想定。`TryGetRidingState` 併用か、
  戻り値を結果 DTO に変えるか Phase 3 で決める。
- **不正な `IRidableIdentifier` 入力**: `RidableResolver.Resolve` / `TryRide` は `null` や
  未知実装で例外（`identifier.Type` / キャスト）。プロトコル入力は外部データなので、
  protocol 層で検証するか `Resolve` 入口で `null`/型不一致を `RidableNotFound` 相当に倒す。
  `FromMessagePack` の `long.Parse` も同様（不正文字列で例外）。

## 7. テストの作法

- `RidingTestHelper` は `SeatedTrainCarGuid` と `RegisterSeatedCarOnNewTrain(environment, railZ)` のみ。
  後者は **DI 上の** `TrainUnitDatastore` に座席付き車両を登録する。
- テストは `TrainTestHelper.CreateEnvironment()` 後、`environment.ServiceProvider.GetService<T>()`
  で `IPlayerRidingDatastore` / `RidableResolver` を解決する。`new TrainUnitDatastore()` 等で
  DI と別インスタンスを作らない（テスト規約違反）。
- 座席付きテスト車両は forUnitTest mod の `train.json` に定義（2席、`trainCarGuid`
  `22222222-2222-2222-2222-222222222222`）。マスタをコードで動的生成しない。
- 接続状態を差し替えたい場合のみ、checker を注入した `PlayerRidingDatastore` を明示構築してよい。

## 8. 検証状態

コンパイル エラー0。`PlayerRidingDatastoreTest`（11）/ `RidableIdentifierTest`（3）/
`TrainCarInstanceIdPersistenceTest`（1）/ セーブロード回帰 全 PASS。
外部監査（Codex、2 周）＋多観点コードレビューを通過。Critical/High/Medium 指摘なし。
