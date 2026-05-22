# 乗車システム Phase 3 継続キックオフ（残タスク申し送り）

Phase 3 の後半（プロトコル・イベント・ログイン復帰）を新セッションで継続するための申し送り。
着手前に必読。実装計画は `docs/superpowers/plans/2026-05-21-riding-phase3-protocol-connection.md`、
設計仕様は `docs/superpowers/specs/2026-05-20-riding-system-design.md`。作業ディレクトリ `/Users/katsumi/moorestech`。

---

## 0. 現在地

Phase 3 は「接続検知」部分（計画 Task 4・5・6）が完了済み。さらに当初設計になかった
**接続コンテキスト導線のリファクタ**を実施し、設計仕様セクション7を実装結果へ更新した。
残るは **Task 1（乗車状態変化イベント）/ Task 2（RideActionProtocol）/ Task 3（RidingStateEventPacket）/
ハンドシェイクのログイン復帰拡張** の4つ。

---

## 1. 完了済み（Phase 3 前半：接続検知＋接続コンテキスト導線）

### 1.1 接続レジストリ

- `Game.PlayerConnection/PlayerConnectionRegistry.cs` — 接続中 playerId を `HashSet<int>` で管理。
  `IPlayerConnectionChecker` 実装。`Register` / `Unregister` / `IsConnected` / `OnPlayerDisconnected`（`IObservable<int>`）。
  `lock` でスレッド保護（`Register` はメイン、`Unregister` は受信スレッドから呼ばれる）。
- `Game.PlayerConnection/AlwaysConnectedChecker.cs` は削除済み（Phase 2 の暫定実装）。
- DI: `MoorestechServerDIContainerGenerator` で `IPlayerConnectionChecker → PlayerConnectionRegistry`（singleton）。
  `PlayerConnectionRegistry` 実体が必要な箇所は `(PlayerConnectionRegistry)provider.GetService<IPlayerConnectionChecker>()` で取得する。

### 1.2 接続コンテキスト導線（当初設計になかったリファクタ）

handshake で得た playerId を切断処理（`UserPacketHandler`）へ届けるため、接続単位の
`PacketResponseContext` を全プロトコル共通の正規引数に昇格した。

- `Server.Protocol/PacketResponseContext.cs` — 接続単位コンテキスト。`BindPlayerId(int)` / `PlayerId`（`int?`）。
  書き込み（メインスレッド）と読み取り（受信スレッド）がスレッドをまたぐため `lock` 保護。
- `IPacketResponse.GetResponse` のシグネチャは **`GetResponse(byte[] payload, PacketResponseContext context)`** に統一。
- `PacketResponseCreator.GetPacketResponse` も **`GetPacketResponse(byte[] payload, PacketResponseContext context)`** の単一メソッド。
- `ServerListenAcceptor` が接続ごとに `PacketResponseContext` を1つ生成 → `ReceiveQueueProcessor` 経由でプロトコル層へ渡す。
- `InitialHandshakeProtocol.GetResponse` が `_connectionRegistry.Register(playerId)` ＋ `context.BindPlayerId(playerId)` を実行。
- `UserPacketHandler.Cleanup`（通常切断・例外切断とも `finally` で必ず通る）が `context.PlayerId` を読んで `Unregister`。
- 当初検討された二重インタフェース `IConnectionAwarePacketResponse` は**不採用・削除済み**（特殊経路を残すため）。

---

## 2. 残タスク

実装計画 `2026-05-21-riding-phase3-protocol-connection.md` の該当タスクに従う。
**ただし計画中のコードスニペットは旧シグネチャ前提なので §3 の申し送りを必ず先に読むこと。**

| 残タスク | 内容 | 計画の該当箇所 |
| --- | --- | --- |
| Task 1 | `Game.PlayerRiding/RidingStateChange.cs` 新設、`PlayerRidingDatastore` に `OnRidingStateChanged`（UniRx `Subject`）を追加。`TryRide`/`TryDismount`/`OnRidableRemoved`/`EvaluateOnLogin` の各成功時に発火 | Task 1 |
| Task 2 | `Server.Protocol/PacketResponse/RideActionProtocol.cs` 新設（Tag `va:rideAction`、乗車/降車要求）。`PacketResponseCreator` に登録 | Task 2 |
| Task 3 | `Server.Event/EventReceive/RidingStateEventPacket.cs` 新設（`va:event:ridingState` を broadcast、`OnRidingStateChanged` を購読）。`TrainCarRemovedRidingHandler`（`OnTrainCarRemoved` → `OnRidableRemoved` 配線）。DI 登録 | Task 3 |
| ハンドシェイク §8 | `InitialHandshakeProtocol` に `EvaluateOnLogin` + `TryGetRidingState` を呼ぶログイン復帰判定を追加。`ResponseInitialHandshakeMessagePack` に `RidingTarget`(`[Key(3)]`) / `RidingSeatIndex`(`[Key(4)]`) を追加 | Task 3 末尾の InitialHandshake 拡張ステップ |

`EvaluateOnLogin` / `TryGetRidingState` は Phase 2 で `IPlayerRidingDatastore` に実装済み。
プロトコル/イベントは `IPlayerRidingDatastore` の窓口メソッドを呼ぶだけ（仕様書セクション4.0）。

---

## 3. 重要な申し送り（計画との差分）

1. **プロトコルのシグネチャが変わっている。** 新規 `RideActionProtocol` は
   `public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)` で実装する。
   計画 Task 2 / ハンドシェイク拡張のスニペットは旧 `GetResponse(byte[] payload)` 1引数で書かれているので、
   そのまま貼ると `IPacketResponse` 未実装でコンパイルエラーになる。`RideActionProtocol` は接続コンテキストを
   使わないので `context` は受け取るだけでよい。

2. **`InitialHandshakeProtocol` は既に2引数版＋接続登録済み。** §8 拡張は、現状の
   `GetResponse(byte[] payload, PacketResponseContext context)` の本体に乗車復帰判定を**追記**する形で行う。
   計画スニペットのように1引数メソッドを新設しないこと。`_connectionRegistry.Register` / `context.BindPlayerId`
   の既存呼び出しは残す。

3. **テストの packet 呼び出しも2引数。** `PacketResponseCreator.GetPacketResponse(payload)` は廃止済み。
   テストは `GetPacketResponse(payload, new PacketResponseContext())` で呼ぶ（既存 PacketTest 群は全て修正済み・参考になる）。

4. **多重接続は許可しない。** 「1 playerId = 1 接続」が前提。`PlayerConnectionRegistry` は参照カウントを持たない
   `HashSet<int>`。座席占有判定を「接続中プレイヤーのみ」に絞るのが切断検知の目的（仕様書セクション7）。

---

## 4. 既知の検討事項（Phase 2→3 申し送り §6 から未解決のもの）

- **`OnTrainCarRemoved` の発火順**: `TrainCar.Destroy()` 由来イベントは `TrainUnitDatastore` 更新より前に発火する。
  仕様§4.4 は「`RidableResolver` で解決できなくなった後」を前提。Task 3 で `TrainCarRemovedRidingHandler` を
  配線する際、発火点を datastore 更新後にするか、削除プロトコル側で更新後に呼ぶか決める。
- **不正な `IRidableIdentifier` 入力**: `RideActionProtocol` の入力は外部データ。`RidableResolver.Resolve` /
  `TryRide` は `null`・未知実装で例外になりうる。protocol 層で検証するか `Resolve` 入口で
  `RidableNotFound` 相当に倒す。`RidableIdentifierConverter.FromMessagePack` の `long.Parse` も同様。
- **`EvaluateOnLogin` の戻り値**: 現状 `bool`。ハンドシェイク §8 では `TryGetRidingState` を併用して
  復帰した `RidingState` を取得しレスポンスに含める方針（計画スニペット参照）。

---

## 5. 参照する skill / 仕様書セクション

- `creating-server-protocol` — Request-Response 型（`RideActionProtocol`）・Event 型（`RidingStateEventPacket`）の作成手順。
- `csharp-event-pattern` — UniRx の `Subject`/`Observable`/`Subscribe`（標準 `event` ではなく UniRx を使う）。
- `creating-server-tests` — サーバー NUnit テストの雛形・初期化・テスト用 ID。
- 設計仕様: セクション5（プロトコル・イベント）・7（接続検知）・8（ログイン復帰）。

---

## 6. 検証状態と完了確認

Phase 3 前半時点での検証:
- `uloop compile --project-path ./moorestech_client` エラー0・警告0。
- PacketTest 81 件 / 乗車テスト（`PlayerRiding|RidableIdentifier`）17 件 全 PASS。

Phase 3 完了確認（残タスク完了後に実施）:
- `uloop compile --project-path ./moorestech_client` エラー0。
- `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerRiding|RideActionProtocol|PlayerConnectionRegistry"` 全 PASS。
- `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Train|Packet|StartGame"` で既存テスト回帰なし。
- 仕様書セクション5・7・8 が実装されている。

Phase 3 で作るプロトコル・イベント（`va:rideAction` / `va:event:ridingState`）と
`ResponseInitialHandshakeMessagePack` の乗車フィールドを、Phase 4（クライアント）が利用する。
