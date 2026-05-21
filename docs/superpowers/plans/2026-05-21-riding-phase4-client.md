# 乗車システム Phase 4: クライアント 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** プレイヤーが E キーで列車に乗車・降車できるようにし、サーバー権威の乗車状態（プロトコル応答・配信イベント・ログイン復帰）にクライアントを接続する。

**Architecture:** クライアント側スコープはローカルプレイヤーのみ（仕様書セクション9、他プレイヤー描画は未実装）。プレイヤー起因の乗車・降車は `RideActionProtocol` のレスポンスを確認して要求元クライアント自身が座席へテレポートする（broadcast 待ちにしない）。サーバー起因の降車（乗り物破棄）は `va:event:ridingState` または既存の車両消失自己検知で反映する。

**Tech Stack:** C# / Unity / VContainer / UniTask / MessagePack。

**前提:** Phase 1〜3 完了済み。サーバーに `va:rideAction` プロトコル・`va:event:ridingState` イベント・`ResponseInitialHandshakeMessagePack` の乗車フィールド（`RidingTarget` / `RidingSeatIndex`）が存在。設計仕様: `docs/superpowers/specs/2026-05-20-riding-system-design.md`（セクション9）。作業ディレクトリ `/Users/katsumi/moorestech`。

**必ず参照する skill:**
- `playmode-test` — クライアント PlayMode テストの作成。
- `uloop-compile` / `uloop-run-tests` — コンパイル・テスト実行。

**重要な既存事実:**
- `TrainCarRidingPlayerController`（`moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainCarRidingPlayerController.cs`）が乗車表現を持つ。`ForceRide(TrainCarInstanceId)` / `ForceDismount()` は現状デバッグシート（`TrainRidingDebugSheet.cs`）からのみ呼ばれる。プレイヤー Transform を `TrainCarEntityObject` に `SetParent` し `SetControllable(false)`。
- `TrainCarRidingState`（同ディレクトリ）が乗車中の `TrainCarInstanceId?` を保持。
- `TrainCarObjectDatastore`（`Train/View/Object/`）は `TryGetEntity(TrainCarInstanceId, out TrainCarEntityObject)` と `event Action<TrainCarInstanceId> TrainCarEntityRemoving` を持つ。全件列挙の public API は無い。
- request-response 送信は `ClientContext.VanillaApi.Response`（`VanillaApiWithResponse`、`_packetExchangeManager.GetPacketResponse<TResponse>(request, ct)`）。send-only は `ClientContext.VanillaApi.SendOnly`。
- サーバーイベント受信は `ClientContext.VanillaApi.Event.SubscribeEventResponse(string tag, Action<byte[]> handler)`（`VanillaApiEvent`、戻り値 `IDisposable`）。
- 乗車関連クラスは `MainGameStarter.cs`（`moorestech_client/Assets/Scripts/Client.Starter/`）で VContainer 登録され `ITickable` 等で駆動。
- `PlayerPositionSender`（`Presenter/Player/`）は乗車中も停止しない（仕様書セクション9: 変更不要。乗車中はプレイヤーが車両に parent されるため送信座標が自然に追従する）。
- `TrainCarRidingInputSender`（`Train/Network/`）は WASD を送る既存実装。変更しない。

**スコープに関する注記:** 仕様書セクション9は「`TrainCarRidingPlayerController` を乗り物非依存の `RidingPlayerController` にリファクタ」と記すが、クライアント実装は列車のみ・ローカルプレイヤーのみであり、第2の乗り物が存在しない現状で汎用化するのは YAGNI。本 Phase はクラス名を `TrainCarRidingPlayerController` のまま維持し、乗車要求のみ `RidableIdentifierMessagePack` 経由でサーバーとやり取りする。クライアント側の乗り物非依存リファクタは第2の `IRidable` 実装が現れた時点で行う（仕様書セクション12「列車以外はスコープ外」と整合）。

---

## ファイル構成

**新規作成:**
- `Client.Game/InGame/Train/Network/RidingStateEventHandler.cs` — `va:event:ridingState` 受信
- `Client.Game/InGame/Train/Unit/TrainCarRidingInteractInput.cs` — E キーで乗車/降車要求

**変更:**
- `Client.Network/API/VanillaApiWithResponse.cs` — `RideAction` 送信メソッド追加
- `Client.Game/InGame/Train/Unit/TrainCarRidingPlayerController.cs` — 乗車/降車をサーバー経由にする・サーバー起因降車を反映
- `Client.Game/InGame/Train/View/Object/TrainCarObjectDatastore.cs` — 近傍検索用の列挙 API 追加
- クライアントのハンドシェイク応答処理（`grep -rln "ResponseInitialHandshakeMessagePack" moorestech_client` で特定）— ログイン時の乗車復帰
- `Client.Starter/MainGameStarter.cs` — 新規クラスの VContainer 登録
- `Client.DebugSystem/DebugSheet/TrainRidingDebugSheet.cs` — デバッグボタンを正規経路に合わせる（任意）

---

## Task 1: クライアントの RideAction 送信 API

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`

- [ ] **Step 1: RideAction メソッドを追加**

`VanillaApiWithResponse.cs` に、`GetRailGraphSnapshot` 等と同じ要領でメソッドを追加する。`RideActionProtocol`（Phase 3、サーバー側 `Server.Protocol.PacketResponse`）はクライアントからも参照可能（サーバーの protocol アセンブリはクライアントにインポートされている）。

```csharp
        // 乗車/降車をサーバーに要求し、結果を受け取る（仕様書セクション5.1）。
        // Requests ride/dismount from the server and returns the result.
        public async UniTask<RideActionProtocol.ResponseRideActionMessagePack> RideAction(
            RideActionType action, RidableIdentifierMessagePack target, CancellationToken ct)
        {
            var request = new RideActionProtocol.RequestRideActionMessagePack(_playerId, (byte)action, target);
            return await _packetExchangeManager.GetPacketResponse<RideActionProtocol.ResponseRideActionMessagePack>(request, ct);
        }
```

ファイル先頭の using に `using Server.Protocol.PacketResponse;` / `using Server.Util.MessagePack;` を追加。`_playerId` フィールドの参照名は同ファイル内の既存メソッド（`PlaceTrainOnRail` が `_playerConnectionSetting.PlayerId` を使う）に合わせる。`_playerId` が無ければ `_playerConnectionSetting.PlayerId` を使う。

- [ ] **Step 2: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。

- [ ] **Step 3: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs
git commit -m "乗車Phase4: クライアントに RideAction 送信APIを追加"
```

---

## Task 2: TrainCarObjectDatastore に近傍検索用の列挙 API を追加

E キー乗車のため「プレイヤーに最も近い車両」を探せるようにする。

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/Object/TrainCarObjectDatastore.cs`

- [ ] **Step 1: 列挙プロパティを追加**

`TrainCarObjectDatastore.cs` に、内部 `Dictionary<TrainCarInstanceId, TrainCarEntityObject> _entities` を読み取り専用で列挙する API を追加する:

```csharp
        // 全車両オブジェクトを列挙する（近傍検索用）。
        // Enumerates all train-car objects (for nearest-car search).
        public IEnumerable<TrainCarEntityObject> AllEntities => _entities.Values;
```

`using System.Collections.Generic;` が無ければ追加。

- [ ] **Step 2: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。

- [ ] **Step 3: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/Object/TrainCarObjectDatastore.cs
git commit -m "乗車Phase4: TrainCarObjectDatastore に車両列挙APIを追加"
```

---

## Task 3: TrainCarRidingPlayerController を乗車/降車の反映専用にする

`ForceRide` / `ForceDismount` を「サーバー応答を受けて見た目を反映するメソッド」に整理する。乗車・降車の意思決定（プロトコル送信）は Task 4 の入力クラスが行う。

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainCarRidingPlayerController.cs`

- [ ] **Step 1: 反映用メソッドを整理**

`TrainCarRidingPlayerController` の現状の `ForceRide(TrainCarInstanceId)` は「クライアント側に乗車状態をセットし、プレイヤーを車両に parent して操作不可にする」処理であり、これがそのまま「サーバーが乗車 Success を返したときの反映」に使える。本 Step では公開メソッド名を意図が分かる形に整理する:
- `ForceRide(TrainCarInstanceId targetCarId)` → `ApplyRide(TrainCarInstanceId targetCarId)` にリネーム（中身は変更しない）。
- `ForceDismount()` → `ApplyDismount()` にリネーム（中身は変更しない）。
- `HandleRemovingTrainCar` はそのまま（車両消失の自己検知。仕様書セクション9の安全網）。

リネームに伴い、呼び出し元（`TrainRidingDebugSheet.cs`、および Task 4・5・6 で追加するクラス）を新名に合わせる。

- [ ] **Step 2: TrainRidingDebugSheet の呼び出しを更新**

`Client.DebugSystem/DebugSheet/TrainRidingDebugSheet.cs` の `ForceRide` / `ForceDismount` 呼び出しを `ApplyRide` / `ApplyDismount` に変更する。デバッグシートはクライアント単独で乗車状態を変える（サーバー非経由）ため、デバッグ用途として残してよい。

- [ ] **Step 3: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainCarRidingPlayerController.cs moorestech_client/Assets/Scripts/Client.DebugSystem/DebugSheet/TrainRidingDebugSheet.cs
git commit -m "乗車Phase4: 乗車反映メソッドを ApplyRide/ApplyDismount に整理"
```

---

## Task 4: E キーによる乗車/降車入力

仕様書セクション9。E キーで最寄り車両に乗車要求、乗車中の E キーで降車要求。`RideActionProtocol` のレスポンスを確認し要求元自身が反映する（broadcast 待ちにしない）。

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainCarRidingInteractInput.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`

- [ ] **Step 1: TrainCarRidingInteractInput を実装**

`moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainCarRidingInteractInput.cs`:

```csharp
using System.Threading;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.Train.View.Object;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Unit
{
    // E キーで最寄り車両への乗車要求 / 乗車中の降車要求を送る（仕様書セクション9）。
    // Sends a ride request to the nearest car / a dismount request while riding, on the E key.
    public sealed class TrainCarRidingInteractInput : ITickable
    {
        // 乗車できる最大距離（メートル）。
        private const float RideableDistance = 3.0f;

        private readonly TrainCarRidingState _trainCarRidingState;
        private readonly TrainCarRidingPlayerController _ridingPlayerController;
        private readonly TrainCarObjectDatastore _trainCarObjectDatastore;
        private bool _requestInFlight;

        public TrainCarRidingInteractInput(
            TrainCarRidingState trainCarRidingState,
            TrainCarRidingPlayerController ridingPlayerController,
            TrainCarObjectDatastore trainCarObjectDatastore)
        {
            _trainCarRidingState = trainCarRidingState;
            _ridingPlayerController = ridingPlayerController;
            _trainCarObjectDatastore = trainCarObjectDatastore;
        }

        public void Tick()
        {
            // 既存の乗車入力（WASD）が KeyCode 直読みなのに合わせ、E も直読みする。
            // Mirrors the existing direct-KeyCode read used by TrainCarRidingInputSender.
            if (!UnityEngine.Input.GetKeyDown(KeyCode.E)) return;
            if (_requestInFlight) return;

            if (_trainCarRidingState.IsRiding)
            {
                RequestDismount().Forget();
            }
            else
            {
                RequestRideNearestCar().Forget();
            }
        }

        private async UniTask RequestRideNearestCar()
        {
            var nearest = FindNearestCar();
            if (nearest == null) return;

            _requestInFlight = true;
            var target = RidableIdentifierMessagePack.CreateTrainCarMessage(nearest.TrainCarInstanceId.AsPrimitive());
            var response = await ClientContext.VanillaApi.Response.RideAction(RideActionType.Ride, target, CancellationToken.None);
            _requestInFlight = false;

            // 乗車成功なら要求元クライアント自身が座席へテレポートする（仕様書セクション5.1・9）。
            // On success the requesting client teleports itself to the seat.
            if (response != null && (RideActionResult)response.Result == RideActionResult.Success)
            {
                _ridingPlayerController.ApplyRide(nearest.TrainCarInstanceId);
            }
        }

        private async UniTask RequestDismount()
        {
            _requestInFlight = true;
            var response = await ClientContext.VanillaApi.Response.RideAction(RideActionType.Dismount, null, CancellationToken.None);
            _requestInFlight = false;

            if (response != null && (RideActionResult)response.Result == RideActionResult.Success)
            {
                _ridingPlayerController.ApplyDismount();
            }
        }

        private TrainCarEntityObject FindNearestCar()
        {
            var playerPos = PlayerSystemContainer.Instance.PlayerObjectController.Position;
            TrainCarEntityObject nearest = null;
            var nearestSqr = RideableDistance * RideableDistance;
            foreach (var car in _trainCarObjectDatastore.AllEntities)
            {
                var sqr = (car.transform.position - playerPos).sqrMagnitude;
                if (sqr <= nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = car;
                }
            }
            return nearest;
        }
    }
}
```

注: `RideActionResult` は Phase 2 で `Game.PlayerRiding.Interface` に定義済み。using に `using Game.PlayerRiding.Interface;` を追加する。`PlayerObjectController.Position` の参照は既存（`PlayerPositionSender` が利用）。

- [ ] **Step 2: VContainer に登録**

`MainGameStarter.cs` の、`TrainCarRidingPlayerController` や `TrainCarRidingInputSender` を登録している箇所と同じ要領で `TrainCarRidingInteractInput` を `ITickable` として登録する（既存の登録記法に合わせる。`builder.RegisterEntryPoint<TrainCarRidingInteractInput>()` 等）。

- [ ] **Step 3: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。

- [ ] **Step 4: 動作確認（PlayMode）**

`playmode-test` skill に従い、ゲーム起動 → 列車設置 → 車両に近づき E キー → 乗車、再度 E → 降車、を確認する PlayMode テストを作成するか、手動で確認する。最低限、`uloop run-tests` の既存クライアントテストが回帰していないことを確認:

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "StartGameTest"`
Expected: PASS。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Unit/TrainCarRidingInteractInput.cs moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs
git commit -m "乗車Phase4: Eキーによる乗車/降車入力を追加"
```

---

## Task 5: RidingStateEventHandler（サーバー起因の降車を反映）

仕様書セクション5.2・9。`va:event:ridingState` を購読し、自分の `playerId` のサーバー起因イベント（主に乗り物破棄による降車）を反映する。

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/RidingStateEventHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs`

- [ ] **Step 1: RidingStateEventHandler を実装**

`moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/RidingStateEventHandler.cs`。`TrainUnitSnapshotEventNetworkHandler.cs` と同じ `IInitializable + IDisposable` パターンに従う:

```csharp
using System;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.Train.Unit;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Network
{
    // va:event:ridingState を購読し、自分のサーバー起因降車を反映する（仕様書セクション5.2・9）。
    // 自分が出した RideActionProtocol の結果反映には使わない（レスポンスで反映済み）。
    public sealed class RidingStateEventHandler : IInitializable, IDisposable
    {
        private readonly TrainCarRidingState _trainCarRidingState;
        private readonly TrainCarRidingPlayerController _ridingPlayerController;
        private IDisposable _subscription;

        public RidingStateEventHandler(TrainCarRidingState trainCarRidingState, TrainCarRidingPlayerController ridingPlayerController)
        {
            _trainCarRidingState = trainCarRidingState;
            _ridingPlayerController = ridingPlayerController;
        }

        public void Initialize()
        {
            _subscription = ClientContext.VanillaApi.Event.SubscribeEventResponse(RidingStateEventPacket.EventTag, OnEventReceived);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
            _subscription = null;
        }

        private void OnEventReceived(byte[] payload)
        {
            if (payload == null || payload.Length == 0) return;

            var message = MessagePackSerializer.Deserialize<RidingStateEventMessagePack>(payload);
            if (message == null) return;

            // 自分のイベントのみ処理する（仕様書セクション9: ローカルプレイヤーのみ）。
            // Only handle this client's own events (local-player-only scope).
            var localPlayerId = PlayerSystemContainer.Instance.PlayerObjectController.PlayerId;
            if (message.PlayerId != localPlayerId) return;

            // サーバー起因の降車のみ反映する。乗車イベントは自分の RideAction レスポンスで反映済み。
            // Apply server-initiated dismounts only; ride events are already applied via the RideAction response.
            if (message.IsDismount && _trainCarRidingState.IsRiding)
            {
                _ridingPlayerController.ApplyDismount();
            }
        }
    }
}
```

注: `PlayerObjectController` にローカル playerId を返すプロパティが無い場合は、`grep -rn "PlayerId" moorestech_client/Assets/Scripts/Client.Game/InGame/Player` でローカル playerId の取得経路を特定し（`PlayerConnectionSetting` 等）、それを使う。`RidingStateEventMessagePack` / `RidingStateEventPacket` はサーバー側 `Server.Event.EventReceive`（クライアントにインポート済み）。

- [ ] **Step 2: VContainer に登録**

`MainGameStarter.cs` で、`TrainUnitSnapshotEventNetworkHandler` を登録しているのと同じ要領で `RidingStateEventHandler` を `IInitializable`（エントリポイント）として登録する。

- [ ] **Step 3: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。

- [ ] **Step 4: コミット**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/RidingStateEventHandler.cs moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs
git commit -m "乗車Phase4: サーバー起因降車を反映する RidingStateEventHandler を追加"
```

---

## Task 6: ログイン時の乗車復帰

仕様書セクション5.3・8。`ResponseInitialHandshakeMessagePack` の `RidingTarget` / `RidingSeatIndex` を見て、ログイン時に乗車中なら自プレイヤーを座席へ parent する。

**Files:**
- Modify: クライアントのハンドシェイク応答処理クラス（`grep -rln "ResponseInitialHandshakeMessagePack" moorestech_client/Assets/Scripts` で特定。`VanillaApiWithResponse` 内の `InitialHandshake` 系メソッド、または起動パイプラインのハンドシェイク受信箇所）

- [ ] **Step 1: ハンドシェイク応答の受信箇所を特定**

`grep -rn "ResponseInitialHandshakeMessagePack" moorestech_client/Assets/Scripts` で、クライアントがハンドシェイク応答を受け取り `PlayerPos` を適用している箇所を特定する。乗車復帰の反映はその直後に足す。

- [ ] **Step 2: 乗車復帰を反映**

特定した箇所で、応答の `RidingTarget` が non-null なら、`RidableType.TrainCar` の場合に `TrainCarInstanceId` を取り出して `TrainCarRidingPlayerController.ApplyRide(...)` を呼ぶ。反映タイミングは、列車オブジェクト（`TrainCarObjectDatastore` の entity）が生成済みである必要がある点に注意する。ハンドシェイクは起動シーケンス序盤のため、列車スナップショット受信より前の可能性がある。

対策: 乗車復帰の `TrainCarInstanceId` を `TrainCarRidingState` に先にセットしておき（`SetRidingTrainCar`）、`TrainCarRidingPlayerController.Tick()` または車両生成時に「乗車状態はあるが未 parent」を検出して parent する。具体的には:
- ハンドシェイク受信時: `RidingTarget` が non-null なら `trainCarRidingState.SetRidingTrainCar(carId)` のみ行う。
- `TrainCarRidingPlayerController` に「`_trainCarRidingState.IsRiding` かつ `_mountedTrainCarInstanceId` 未設定」のとき、対象車両 entity が `TrainCarObjectDatastore.TryGetEntity` で取得できれば parent する処理を `Tick()` に追加する（既存 `Tick()` は乗車中の追従処理を持つので、その分岐に「未 parent なら parent する」ケースを足す）。

実装の具体形は Step 1 で特定したコードに合わせて決め、選択した反映方式をコミットメッセージに残す。

- [ ] **Step 3: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。

- [ ] **Step 4: 動作確認（PlayMode）**

`playmode-test` skill に従い、「乗車状態でセーブ → ロード（再起動）→ ログイン時に座席に着いている」ことを確認する PlayMode テストを作成するか手動確認する。

- [ ] **Step 5: コミット**

```bash
git add moorestech_client/Assets/Scripts
git commit -m "乗車Phase4: ログイン時の乗車復帰を反映"
```

---

## Task 7: 結合確認

**Files:** なし（確認のみ）

- [ ] **Step 1: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。

- [ ] **Step 2: クライアント既存テストの回帰確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "StartGameTest|Train"`
Expected: 既存テストが PASS のまま。

- [ ] **Step 3: end-to-end 手動確認**

`playmode-test` skill または手動で以下を確認:
- 車両に近づき E → 乗車（座席にテレポート、操作不可）。
- 乗車中に WASD → 列車が動く（既存 `TrainCarRidingInputSender`）。
- 乗車中に E → 降車（操作可能に戻る）。
- 乗車中に別プレイヤー/デバッグで列車を撤去 → 自動降車（`RidingStateEventHandler` または車両消失自己検知）。
- 乗車状態でセーブ → 再起動 → ログイン時に座席に復帰。

- [ ] **Step 4: ログ確認**

Run: `uloop get-logs --project-path ./moorestech_client --log-type Error`
Expected: 乗車操作に起因するエラーが無い。

---

## Phase 4 完了確認

- [ ] `uloop compile --project-path ./moorestech_client` がエラー 0。
- [ ] `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "StartGameTest|Train"` で既存テストが回帰なし。
- [ ] E キーで乗車・降車でき、サーバー起因降車・ログイン復帰が反映される。
- [ ] 仕様書セクション9（クライアント側）が実装されている。

## 乗車システム全体の完了

Phase 1〜4 完了で、仕様書 `docs/superpowers/specs/2026-05-20-riding-system-design.md` の全セクションが実装される。スコープ外項目（仕様書セクション12: 他プレイヤー描画・二重接続制御・操舵入力検証・運転席区別・列車以外の乗り物・正式 UI）は別タスク。
