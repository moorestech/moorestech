# 乗車システム Phase 3: プロトコル・接続検知・ハンドシェイク拡張 実装計画

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** クライアントが乗車・降車を要求できる `RideActionProtocol`、乗車状態を配信する `RidingStateEventPacket`、プレイヤー切断検知、ログイン時の乗車復帰（`InitialHandshakeProtocol` 拡張）を実装する。

**Architecture:** プロトコル・ハンドシェイク・車両削除ハンドラは `PlayerRidingDatastore` のメソッドを呼ぶだけ（仕様書セクション4.0）。接続検知は `PlayerConnectionRegistry` を新設し、`UserPacketHandler` に `playerId` を紐付けて切断時にイベントを発火する。`PlayerConnectionRegistry` が `IPlayerConnectionChecker` の実装となり、Phase 2 の暫定 `AlwaysConnectedChecker` を置き換える。

**Tech Stack:** C# / Unity / MessagePack / UniRx / Microsoft.Extensions.DependencyInjection / NUnit。

**前提:** Phase 1・2 完了済み。`PlayerRidingDatastore`（`TryRide` / `TryDismount` / `OnRidableRemoved` / `EvaluateOnLogin` / `TryGetRidingState` / セーブ・ロード）が存在。設計仕様: `docs/superpowers/specs/2026-05-20-riding-system-design.md`（セクション5・7・8）。作業ディレクトリ `/Users/katsumi/moorestech`。

**必ず参照する skill:**
- `creating-server-protocol` — Request-Response 型プロトコル・Event 型パケットの作成手順（プロトコル登録・MessagePack 規約）。
- `csharp-event-pattern` — UniRx の Subject/Observable/Subscribe パターン（このプロジェクトは C# 標準 event ではなく UniRx を使う）。
- `creating-server-tests` — サーバー NUnit テストの雛形・初期化。

**重要な既存事実:**
- `IPacketResponse.GetResponse(byte[]) → ProtocolMessagePackBase`。`ProtocolMessagePackBase` は `[Key(0)] Tag` / `[Key(1)] SequenceId`。リクエスト/レスポンスの MessagePack クラスは `[Key(2)]` から使う。
- プロトコル登録は `Server.Protocol/PacketResponseCreator.cs` のコンストラクタ（`_packetResponseDictionary.Add(Tag, new XxxProtocol(serviceProvider))`）。
- イベントは pull 型。Event パケットクラスは `public const string EventTag = "va:event:xxx";` を持ち、UniRx を `Subscribe` して `EventProtocolProvider.AddBroadcastEvent(EventTag, payload)` を呼ぶ。DI は `MoorestechServerDIContainerGenerator.cs` で `AddSingleton` ＋ 即時 `GetService`。
- `EventProtocolProvider.AddBroadcastEvent` は「一度でも `GetEventBytesList` を呼んだ playerId」にのみ配信する。
- 接続: `Server.Boot/Loop/ServerListenAcceptor.cs` の `Accept()` ループで接続ごとに `UserPacketHandler` を生成。`UserPacketHandler.StartListen` の `Socket.Receive`==0 で `break`（**現状 `Cleanup()` を呼ばない既存バグ**）、例外時のみ `Cleanup()`。`UserPacketHandler` は `playerId` を保持していない。
- `InitialHandshakeProtocol`（Tag `va:initialHandshake`）はコンストラクタで `ServiceProvider` を受け取る。`RequestInitialHandshakeMessagePack` に `[Key(2)] int PlayerId`。
- `TrainUpdateEvent.OnTrainCarRemoved`（`IObservable<TrainCarInstanceId>`、`Game.Train/Event/`）が車両削除で発火する。DI 上の型は `ITrainUpdateEvent`。

---

## ファイル構成

**新規作成:**
- `Game.PlayerRiding/RidingStateChange.cs` — 乗車状態変化の通知データ
- `Game.PlayerRiding/PlayerConnectionRegistry.cs` — 接続中 playerId 管理（`IPlayerConnectionChecker` 実装）
- `Game.PlayerRiding/TrainCarRemovedRidingHandler.cs` — `OnTrainCarRemoved` 購読 → `OnRidableRemoved`
- `Server.Protocol/PacketResponse/RideActionProtocol.cs` — 乗車/降車要求プロトコル
- `Server.Event/EventReceive/RidingStateEventPacket.cs` — 乗車状態配信イベント
- `Tests.UnitTest/PlayerRiding/RideActionProtocolTest.cs`
- `Tests.UnitTest/PlayerRiding/PlayerConnectionRegistryTest.cs`

**変更:**
- `Game.PlayerRiding/PlayerRidingDatastore.cs` — 乗車状態変化の UniRx イベントを追加
- `Server.Protocol/PacketResponseCreator.cs` — `RideActionProtocol` 登録
- `Server.Protocol/PacketResponse/InitialHandshakeProtocol.cs` — ログイン復帰判定とレスポンス拡張
- `Server.Boot/Loop/ServerListenAcceptor.cs` — `PlayerConnectionRegistry` を `UserPacketHandler` へ渡す
- `Server.Boot/Loop/PacketProcessing/UserPacketHandler.cs` — `playerId` 紐付け・切断イベント発火・`Cleanup` 漏れ修正
- `Server.Boot/`（`ServerInstanceManager` 等）— `ServerListenAcceptor.StartServer` への引数追加
- `Server.Boot/MoorestechServerDIContainerGenerator.cs` — 接続レジストリ・イベントパケット・ハンドラの DI 登録、`IPlayerConnectionChecker` 実装差し替え

---

## Task 1: PlayerRidingDatastore に乗車状態変化イベントを追加

`RidingStateEventPacket` が購読する UniRx イベントを `PlayerRidingDatastore` に追加する。`csharp-event-pattern` skill に従い UniRx の `Subject` を使う。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.PlayerRiding/RidingStateChange.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.PlayerRiding/PlayerRidingDatastore.cs`
- Test: `moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerRidingDatastoreTest.cs`（Phase 2 で作成済み）

- [ ] **Step 1: RidingStateChange を実装**

`Game.PlayerRiding/RidingStateChange.cs`:

```csharp
using Game.PlayerRiding.Interface;

namespace Game.PlayerRiding
{
    // 乗車状態変化の通知データ。State==null は降車を表す。
    // Notification payload for a riding-state change. State==null means dismounted.
    public readonly struct RidingStateChange
    {
        public int PlayerId { get; }
        public RidingState State { get; }

        public RidingStateChange(int playerId, RidingState state)
        {
            PlayerId = playerId;
            State = state;
        }

        public bool IsDismount => State == null;
    }
}
```

- [ ] **Step 2: 失敗するテストを書く**

`PlayerRidingDatastoreTest.cs` に追記:

```csharp
        [Test]
        public void PlayerRidingDatastore_FiresRidingStateChanged_OnRideAndDismount()
        {
            // TryRide/TryDismount で OnRidingStateChanged が発火する
            var (datastore, car) = TrainTestHelper.CreateDatastoreWithOneTrainCar(seatCount: 2);
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());
            var changes = new System.Collections.Generic.List<RidingStateChange>();
            using var sub = datastore.OnRidingStateChanged.Subscribe(changes.Add);

            datastore.TryRide(1, id, out _);
            datastore.TryDismount(1);

            Assert.AreEqual(2, changes.Count);
            Assert.IsFalse(changes[0].IsDismount);
            Assert.AreEqual(1, changes[0].PlayerId);
            Assert.IsTrue(changes[1].IsDismount);
        }
```

ファイル先頭の using に `using UniRx;` を追加。

- [ ] **Step 3: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "FiresRidingStateChanged"`
Expected: コンパイルエラー（`OnRidingStateChanged` 未定義）で FAIL。

- [ ] **Step 4: PlayerRidingDatastore にイベントを追加**

`PlayerRidingDatastore.cs` を変更（using に `using UniRx;` 追加）:
- フィールド追加: `private readonly Subject<RidingStateChange> _ridingStateChangedSubject = new();`
- 公開プロパティ追加: `public IObservable<RidingStateChange> OnRidingStateChanged => _ridingStateChangedSubject;`
- `TryRide` の成功時（`_ridingStateByPlayerId[playerId] = new RidingState(...)` の直後、`return RideActionResult.Success;` の前）に:
  ```csharp
            _ridingStateChangedSubject.OnNext(new RidingStateChange(playerId, _ridingStateByPlayerId[playerId]));
  ```
- `TryDismount` の成功時（`_ridingStateByPlayerId.Remove(playerId);` の直後）に:
  ```csharp
            _ridingStateChangedSubject.OnNext(new RidingStateChange(playerId, null));
  ```
- `OnRidableRemoved` の各 `_ridingStateByPlayerId.Remove(playerId);` 後、降車させた playerId ごとに:
  ```csharp
                _ridingStateChangedSubject.OnNext(new RidingStateChange(playerId, null));
  ```
- `EvaluateOnLogin` の復帰成功時（`return true;` の直前）に、復帰した状態を通知:
  ```csharp
            _ridingStateChangedSubject.OnNext(new RidingStateChange(playerId, state));
  ```

注: using に `System` が無ければ `IObservable<>` のため追加する。

- [ ] **Step 5: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "FiresRidingStateChanged"`
Expected: PASS。

- [ ] **Step 6: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.PlayerRiding moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerRidingDatastoreTest.cs
git commit -m "乗車Phase3: PlayerRidingDatastore に乗車状態変化イベントを追加"
```

---

## Task 2: RideActionProtocol（乗車/降車要求）

`creating-server-protocol` skill の Request-Response 型プロトコル手順に従う。Tag は `va:rideAction`。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RideActionProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`
- Test: `moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/RideActionProtocolTest.cs`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/RideActionProtocolTest.cs` を新規作成。`creating-server-tests` skill の PacketTest 雛形に従う（`PacketResponseCreator` をテスト用に組む方法を含む）。

```csharp
using Game.PlayerRiding.Interface;
using MessagePack;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;

namespace Tests.UnitTest.PlayerRiding
{
    public class RideActionProtocolTest
    {
        [Test]
        public void RideAction_Ride_ReturnsSuccessAndSeatIndex()
        {
            // 乗車要求 → Success と seatIndex が返る
            var (packetResponseCreator, car, playerId) = TrainTestHelper.CreateServerWithOneTrainCar(seatCount: 2);
            var target = RidableIdentifierMessagePack.CreateTrainCarMessage(car.TrainCarInstanceId.AsPrimitive());
            var request = new RideActionProtocol.RequestRideActionMessagePack(playerId, (byte)RideActionType.Ride, target);

            var responseBytes = packetResponseCreator.GetPacketResponse(MessagePackSerializer.Serialize(request));
            var response = MessagePackSerializer.Deserialize<RideActionProtocol.ResponseRideActionMessagePack>(responseBytes[0]);

            Assert.AreEqual((byte)RideActionResult.Success, response.Result);
            Assert.AreEqual(0, response.SeatIndex);
        }

        [Test]
        public void RideAction_Dismount_WhenNotRiding_ReturnsNotRiding()
        {
            var (packetResponseCreator, _, playerId) = TrainTestHelper.CreateServerWithOneTrainCar(seatCount: 2);
            var request = new RideActionProtocol.RequestRideActionMessagePack(playerId, (byte)RideActionType.Dismount, null);

            var responseBytes = packetResponseCreator.GetPacketResponse(MessagePackSerializer.Serialize(request));
            var response = MessagePackSerializer.Deserialize<RideActionProtocol.ResponseRideActionMessagePack>(responseBytes[0]);

            Assert.AreEqual((byte)RideActionResult.NotRiding, response.Result);
        }
    }
}
```

`TrainTestHelper.CreateServerWithOneTrainCar(int)` は、DI を組んで `PacketResponseCreator` と登録済み車両・playerId を返すヘルパ。`creating-server-tests` skill の「PacketTest」のサーバー構築手順に従って実装する。

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RideActionProtocolTest"`
Expected: コンパイルエラー（`RideActionProtocol` 未定義）で FAIL。

- [ ] **Step 3: RideActionProtocol を実装**

`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/RideActionProtocol.cs`:

```csharp
using System;
using Game.PlayerRiding;
using Game.PlayerRiding.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    // 乗車種別。Ride=乗車要求、Dismount=降車要求。
    public enum RideActionType : byte
    {
        Ride,
        Dismount,
    }

    // 乗車/降車要求プロトコル（C→S、request-response）。仕様書セクション5.1。
    // プロトコルは PlayerRidingDatastore を呼ぶだけ（仕様書セクション4.0）。
    public class RideActionProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:rideAction";

        private readonly PlayerRidingDatastore _playerRidingDatastore;

        public RideActionProtocol(ServiceProvider serviceProvider)
        {
            _playerRidingDatastore = serviceProvider.GetService<PlayerRidingDatastore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestRideActionMessagePack>(payload);

            var result = (RideActionType)data.Action switch
            {
                RideActionType.Ride => HandleRide(),
                RideActionType.Dismount => _playerRidingDatastore.TryDismount(data.PlayerId),
                _ => RideActionResult.RidableNotFound,
            };

            var seatIndex = -1;
            if ((RideActionType)data.Action == RideActionType.Ride && result == RideActionResult.Success)
            {
                // 乗車成功時の seatIndex を取り出す
                if (_playerRidingDatastore.TryGetRidingState(data.PlayerId, out var state))
                {
                    seatIndex = state.SeatIndex;
                }
            }

            return new ResponseRideActionMessagePack((byte)result, seatIndex);

            #region Internal

            RideActionResult HandleRide()
            {
                if (data.Target == null) return RideActionResult.RidableNotFound;
                var identifier = RidableIdentifierConverter.FromMessagePack(data.Target);
                return _playerRidingDatastore.TryRide(data.PlayerId, identifier, out _);
            }

            #endregion
        }

        [MessagePackObject]
        public class RequestRideActionMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public int PlayerId { get; set; }
            [Key(3)] public byte Action { get; set; }
            [Key(4)] public RidableIdentifierMessagePack Target { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestRideActionMessagePack() { }

            public RequestRideActionMessagePack(int playerId, byte action, RidableIdentifierMessagePack target)
            {
                Tag = ProtocolTag;
                PlayerId = playerId;
                Action = action;
                Target = target;
            }
        }

        [MessagePackObject]
        public class ResponseRideActionMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public byte Result { get; set; }
            [Key(3)] public int SeatIndex { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseRideActionMessagePack() { }

            public ResponseRideActionMessagePack(byte result, int seatIndex)
            {
                Tag = ProtocolTag;
                Result = result;
                SeatIndex = seatIndex;
            }
        }
    }
}
```

- [ ] **Step 4: PacketResponseCreator に登録**

`PacketResponseCreator.cs` のコンストラクタの登録群（`TrainCarRidingInputProtocol` 登録行付近）に追加:

```csharp
            _packetResponseDictionary.Add(RideActionProtocol.ProtocolTag, new RideActionProtocol(serviceProvider));
```

- [ ] **Step 5: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "RideActionProtocolTest"`
Expected: 2件 PASS。

- [ ] **Step 6: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/RideActionProtocolTest.cs
git commit -m "乗車Phase3: RideActionProtocol を追加"
```

---

## Task 3: RidingStateEventPacket（乗車状態の broadcast 配信）

`creating-server-protocol` skill の Event 型パケット手順に従う。`PlayerRidingDatastore.OnRidingStateChanged` を購読し `va:event:ridingState` を broadcast する。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/RidingStateEventPacket.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`

- [ ] **Step 1: RidingStateEventPacket を実装**

`moorestech_server/Assets/Scripts/Server.Event/EventReceive/RidingStateEventPacket.cs`:

```csharp
using System;
using Game.PlayerRiding;
using Game.PlayerRiding.Interface;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // 乗車状態変化を全クライアントへ broadcast するイベントパケット（仕様書セクション5.2）。
    // Broadcasts riding-state changes to all clients.
    public class RidingStateEventPacket
    {
        public const string EventTag = "va:event:ridingState";

        private readonly EventProtocolProvider _eventProtocolProvider;

        public RidingStateEventPacket(EventProtocolProvider eventProtocolProvider, PlayerRidingDatastore playerRidingDatastore)
        {
            _eventProtocolProvider = eventProtocolProvider;
            playerRidingDatastore.OnRidingStateChanged.Subscribe(OnRidingStateChanged);
        }

        private void OnRidingStateChanged(RidingStateChange change)
        {
            // 降車時は Target/SeatIndex を null（仕様書セクション5.2）
            RidableIdentifierMessagePack target = null;
            int seatIndex = -1;
            if (!change.IsDismount)
            {
                target = change.State.Identifier.ToMessagePack();
                seatIndex = change.State.SeatIndex;
            }

            var messagePack = new RidingStateEventMessagePack(change.PlayerId, target, seatIndex);
            var payload = MessagePackSerializer.Serialize(messagePack);
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }
    }

    [MessagePackObject]
    public class RidingStateEventMessagePack
    {
        [Key(0)] public int PlayerId { get; set; }
        // null のとき降車。non-null のとき乗車。
        [Key(1)] public RidableIdentifierMessagePack Target { get; set; }
        // 降車時は -1。
        [Key(2)] public int SeatIndex { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RidingStateEventMessagePack() { }

        public RidingStateEventMessagePack(int playerId, RidableIdentifierMessagePack target, int seatIndex)
        {
            PlayerId = playerId;
            Target = target;
            SeatIndex = seatIndex;
        }

        // 降車イベントかどうか
        [IgnoreMember] public bool IsDismount => Target == null;
    }
}
```

- [ ] **Step 2: DI 登録**

`MoorestechServerDIContainerGenerator.cs` のイベントレシーバー登録群（`PlaceBlockEventPacket` 等の `AddSingleton` がある箇所、約188〜206行目）に追加:

```csharp
            services.AddSingleton<Server.Event.EventReceive.RidingStateEventPacket>();
```

さらに、イベントレシーバーを即時インスタンス化する群（約220〜242行目の `serviceProvider.GetService<...>()` 群）に追加:

```csharp
            serviceProvider.GetService<Server.Event.EventReceive.RidingStateEventPacket>();
```

（即時 `GetService` はコンストラクタの `Subscribe` を発火させるために必須。`creating-server-protocol` skill 参照。）

- [ ] **Step 3: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。

- [ ] **Step 4: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Event/EventReceive/RidingStateEventPacket.cs moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs
git commit -m "乗車Phase3: RidingStateEventPacket を追加"
```

---

## Task 4: PlayerConnectionRegistry（接続中 playerId 管理）

仕様書セクション7。`IPlayerConnectionChecker` の実装。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.PlayerRiding/PlayerConnectionRegistry.cs`
- Test: `moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerConnectionRegistryTest.cs`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerConnectionRegistryTest.cs`:

```csharp
using System.Collections.Generic;
using Game.PlayerRiding;
using NUnit.Framework;
using UniRx;

namespace Tests.UnitTest.PlayerRiding
{
    public class PlayerConnectionRegistryTest
    {
        [Test]
        public void Register_MakesPlayerConnected_Unregister_FiresDisconnectedAndClears()
        {
            var registry = new PlayerConnectionRegistry();
            var disconnected = new List<int>();
            using var sub = registry.OnPlayerDisconnected.Subscribe(disconnected.Add);

            registry.Register(5);
            Assert.IsTrue(registry.IsConnected(5));
            Assert.IsFalse(registry.IsConnected(6));

            registry.Unregister(5);
            Assert.IsFalse(registry.IsConnected(5));
            Assert.AreEqual(new List<int> { 5 }, disconnected);
        }

        [Test]
        public void Unregister_UnknownPlayer_DoesNotFire()
        {
            var registry = new PlayerConnectionRegistry();
            var disconnected = new List<int>();
            using var sub = registry.OnPlayerDisconnected.Subscribe(disconnected.Add);

            registry.Unregister(99);

            Assert.AreEqual(0, disconnected.Count);
        }
    }
}
```

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerConnectionRegistryTest"`
Expected: コンパイルエラー（`PlayerConnectionRegistry` 未定義）で FAIL。

- [ ] **Step 3: PlayerConnectionRegistry を実装**

`moorestech_server/Assets/Scripts/Game.PlayerRiding/PlayerConnectionRegistry.cs`:

```csharp
using System;
using System.Collections.Generic;
using Game.PlayerRiding.Interface;
using UniRx;

namespace Game.PlayerRiding
{
    // 接続中の playerId を管理する（仕様書セクション7）。IPlayerConnectionChecker の実装。
    // 座席占有判定が「接続中プレイヤーのみ」を対象にするために使う。
    // Tracks connected playerIds. Implements IPlayerConnectionChecker.
    public class PlayerConnectionRegistry : IPlayerConnectionChecker
    {
        private readonly HashSet<int> _connectedPlayerIds = new();
        private readonly object _lock = new();

        private readonly Subject<int> _disconnectedSubject = new();
        public IObservable<int> OnPlayerDisconnected => _disconnectedSubject;

        public void Register(int playerId)
        {
            lock (_lock)
            {
                _connectedPlayerIds.Add(playerId);
            }
        }

        public void Unregister(int playerId)
        {
            bool removed;
            lock (_lock)
            {
                removed = _connectedPlayerIds.Remove(playerId);
            }
            // 登録されていた playerId のみ切断イベントを発火する
            if (removed)
            {
                _disconnectedSubject.OnNext(playerId);
            }
        }

        public bool IsConnected(int playerId)
        {
            lock (_lock)
            {
                return _connectedPlayerIds.Contains(playerId);
            }
        }
    }
}
```

注: `Register` / `Unregister` は受信スレッド（接続スレッド）から呼ばれ得るため `lock` で保護する。`UserPacketHandler` は接続ごとに別スレッド。

- [ ] **Step 4: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerConnectionRegistryTest"`
Expected: 2件 PASS。

- [ ] **Step 5: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.PlayerRiding/PlayerConnectionRegistry.cs moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerConnectionRegistryTest.cs
git commit -m "乗車Phase3: PlayerConnectionRegistry を追加"
```

---

## Task 5: PlayerConnectionRegistry を IPlayerConnectionChecker として DI 登録

Phase 2 の暫定 `AlwaysConnectedChecker` を `PlayerConnectionRegistry` に置き換える。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`

- [ ] **Step 1: DI 登録を差し替え**

Phase 2 Task 8 で追加した行:
```csharp
            services.AddSingleton<Game.PlayerRiding.Interface.IPlayerConnectionChecker, Game.PlayerRiding.AlwaysConnectedChecker>();
```
を次の2行に置き換える:
```csharp
            services.AddSingleton<Game.PlayerRiding.PlayerConnectionRegistry>();
            services.AddSingleton<Game.PlayerRiding.Interface.IPlayerConnectionChecker>(provider => provider.GetService<Game.PlayerRiding.PlayerConnectionRegistry>());
```

これで `PlayerConnectionRegistry` 単体でも `IPlayerConnectionChecker` としても同一インスタンスが取れる（`PlayerRidingDatastore` は `IPlayerConnectionChecker` 経由で接続中判定する）。

- [ ] **Step 2: AlwaysConnectedChecker を削除**

`AlwaysConnectedChecker.cs` を削除する（`git rm`）。Phase 2 のテストで `AlwaysConnectedChecker` を使っていた場合は、テストヘルパ側を `new PlayerConnectionRegistry()` を使う形に変更し、テストではヘルパ内で乗車させる playerId を `Register` しておく。

- [ ] **Step 3: コンパイルとテスト確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerRiding"`
Expected: Phase 2・3 の乗車テストが全件 PASS（テストヘルパが対象 playerId を `Register` していること）。

- [ ] **Step 4: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs moorestech_server/Assets/Scripts/Game.PlayerRiding moorestech_server/Assets/Scripts/Tests.UnitTest
git commit -m "乗車Phase3: IPlayerConnectionChecker を PlayerConnectionRegistry に差し替え"
```

---

## Task 6: 接続検知 — UserPacketHandler に playerId を紐付ける

仕様書セクション7。`UserPacketHandler`（接続1本＝1インスタンス）に `playerId` を覚えさせ、切断時に `PlayerConnectionRegistry.Unregister` を呼ぶ。`Socket.Receive`==0 経路で `Cleanup` が呼ばれない既存バグも修正する。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Loop/PacketProcessing/UserPacketHandler.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Loop/ServerListenAcceptor.cs`
- Modify: `ServerListenAcceptor.StartServer` の呼び出し元（`Server.Boot` 配下、`ServerInstanceManager` 等。`grep -rn "StartServer" moorestech_server/Assets/Scripts/Server.Boot` で特定）

- [ ] **Step 1: ServerListenAcceptor に PlayerConnectionRegistry を渡す**

`ServerListenAcceptor.cs` の `StartServer` シグネチャに引数を追加し、`UserPacketHandler` 生成へ渡す:

```csharp
        public void StartServer(PacketResponseCreator packetResponseCreator, Game.PlayerRiding.PlayerConnectionRegistry connectionRegistry, CancellationToken token)
        {
```

`UserPacketHandler` 生成行を:

```csharp
                var receiveThread = new Thread(() => new UserPacketHandler(client, receiveQueueProcessor, sendQueueProcessor, connectionRegistry).StartListen(token));
```

- [ ] **Step 2: StartServer 呼び出し元を更新**

`StartServer` を呼ぶ箇所（`ServerInstanceManager` 内、`new ServerListenAcceptor().StartServer(packet, token)` 相当）を特定し、`serviceProvider.GetService<Game.PlayerRiding.PlayerConnectionRegistry>()` を取得して引数に渡す:

```csharp
            var connectionRegistry = serviceProvider.GetService<Game.PlayerRiding.PlayerConnectionRegistry>();
            new Thread(() => new ServerListenAcceptor().StartServer(packet, connectionRegistry, token)) // ...
```

（`serviceProvider` は `MoorestechServerDIContainerGenerator.Create` の戻り値。呼び出し元の実コードに合わせて変数名を調整する。）

- [ ] **Step 3: UserPacketHandler に playerId 紐付けと切断発火を実装**

`UserPacketHandler.cs` を変更:
- using に `using MessagePack;`、`using Server.Protocol;`、`using Server.Protocol.PacketResponse;`、`using Game.PlayerRiding;` を追加。
- フィールド追加: `private readonly PlayerConnectionRegistry _connectionRegistry; private int? _playerId;`
- コンストラクタに `PlayerConnectionRegistry connectionRegistry` 引数を追加し代入。
- `ReceiveProcess` のパケットループで、各パケットを enqueue する前に handshake を覗いて playerId を学習する:

```csharp
            foreach (var packet in packets)
            {
                TryBindPlayerId(packet);
                _receiveQueueProcessor.EnqueuePacket(packet);
            }
```

クラス内に以下のメソッドを追加（try-catch は使わず、AGENTS.md 準拠で条件分岐に留める。MessagePack の Deserialize 失敗は `PacketResponseCreator` 同様に上位の例外ハンドリングに委ねず、ここでは tag 一致時のみ二段デシリアライズする）:

```csharp
        // ハンドシェイクパケットを覗いて、この接続に playerId を紐付ける（仕様書セクション7）。
        // Peeks the handshake packet to bind this connection to a playerId.
        private void TryBindPlayerId(byte[] packet)
        {
            if (_playerId.HasValue) return;

            var basePack = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(packet);
            if (basePack.Tag != InitialHandshakeProtocol.ProtocolTag) return;

            var handshake = MessagePackSerializer.Deserialize<InitialHandshakeProtocol.RequestInitialHandshakeMessagePack>(packet);
            _playerId = handshake.PlayerId;
            _connectionRegistry.Register(handshake.PlayerId);
        }
```

- `StartListen` の正常切断経路（`if (error) { Debug.Log("切断されました"); break; }`）を修正し、`break` 後に `Cleanup()` が必ず通るようにする。最も安全なのは `StartListen` の `try` を抜けたあと（`finally` 相当）で必ず後始末する形。`try` ブロックの後ろに `finally` を追加し、`Cleanup()` をそこへ集約する:

```csharp
        public void StartListen(CancellationToken token)
        {
            var buffer = new byte[4096];
            try
            {
                var parser = new PacketBufferParser();
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    var error = ReceiveProcess(parser, buffer);
                    if (error)
                    {
                        Debug.Log("切断されました");
                        break;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("切断されました");
            }
            catch (Exception e)
            {
                Debug.LogError("moorestech内のロジックによるエラーで切断");
                Debug.LogException(e);
            }
            finally
            {
                Cleanup();
            }
        }
```

`Cleanup()` に playerId の登録解除を追加（多重呼び出しに備え冪等にする。`Unregister` は未登録 playerId に対し no-op）:

```csharp
        private void Cleanup()
        {
            // 接続終了時、紐付いた playerId を登録解除して切断イベントを発火する（仕様書セクション7）。
            // On connection end, unregister the bound playerId and fire the disconnect event.
            if (_playerId.HasValue)
            {
                _connectionRegistry.Unregister(_playerId.Value);
                _playerId = null;
            }
            _receiveQueueProcessor.Dispose();
            _sendQueueProcessor.Dispose();
            _client.Close();
        }
```

注: `ReceiveQueueProcessor.Dispose` / `SendQueueProcessor.Dispose` が二重呼び出しに耐えるか確認すること。耐えない場合は `Cleanup` の冒頭に `bool _cleaned` ガードを足す。

- [ ] **Step 4: コンパイル確認**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラー 0。

- [ ] **Step 5: 接続レジストリの統合テスト**

`PlayerConnectionRegistryTest.cs` に、`UserPacketHandler` の handshake 紐付け→切断で `OnPlayerDisconnected` が出ることを検証するテストを追加できれば追加する（`Socket` を直接使うのが難しいため、最低限 `TryBindPlayerId` 相当のロジックを `internal` 公開してテストする、または手動結合確認とする）。難しければ Step 6 の手動確認に委ねる。

- [ ] **Step 6: 既存の接続・起動テストの回帰確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Packet|StartGame|Handshake"`
Expected: 既存テストが PASS のまま。

- [ ] **Step 7: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Boot
git commit -m "乗車Phase3: 接続にplayerIdを紐付け切断検知を実装"
```

---

## Task 7: TrainCarRemovedRidingHandler（車両削除 → 降車）

仕様書セクション4.4。`TrainUpdateEvent.OnTrainCarRemoved` を購読し `PlayerRidingDatastore.OnRidableRemoved` を呼ぶ。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Game.PlayerRiding/TrainCarRemovedRidingHandler.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`
- Test: `moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerRidingDatastoreTest.cs`

- [ ] **Step 1: 失敗するテストを書く**

`PlayerRidingDatastoreTest.cs` に追記:

```csharp
        [Test]
        public void TrainCarRemovedRidingHandler_DismountsRidersWhenCarRemoved()
        {
            // OnTrainCarRemoved 発火で、その車両の乗員が降車することを確認
            var (datastore, trainUpdateEvent, car) = TrainTestHelper.CreateDatastoreWithTrainUpdateEvent(seatCount: 2);
            var handler = new Game.PlayerRiding.TrainCarRemovedRidingHandler(trainUpdateEvent, datastore);
            datastore.TryRide(1, new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive()), out _);

            trainUpdateEvent.InvokeTrainCarRemoved(car.TrainCarInstanceId);

            Assert.IsFalse(datastore.TryGetRidingState(1, out _));
        }
```

`TrainTestHelper.CreateDatastoreWithTrainUpdateEvent` は `TrainUpdateEvent`（具象、`InvokeTrainCarRemoved` を持つ）と `PlayerRidingDatastore` を返すヘルパ。

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainCarRemovedRidingHandler_Dismounts"`
Expected: コンパイルエラー（`TrainCarRemovedRidingHandler` 未定義）で FAIL。

- [ ] **Step 3: TrainCarRemovedRidingHandler を実装**

`moorestech_server/Assets/Scripts/Game.PlayerRiding/TrainCarRemovedRidingHandler.cs`:

```csharp
using Game.PlayerRiding.Interface;
using Game.Train.Event;
using Game.Train.Unit;
using UniRx;

namespace Game.PlayerRiding
{
    // 車両削除イベント（OnTrainCarRemoved）を購読し、その車両の乗員を降車させる（仕様書セクション4.4）。
    // Subscribes to OnTrainCarRemoved and dismounts riders of the removed car.
    public class TrainCarRemovedRidingHandler
    {
        private readonly PlayerRidingDatastore _playerRidingDatastore;

        public TrainCarRemovedRidingHandler(ITrainUpdateEvent trainUpdateEvent, PlayerRidingDatastore playerRidingDatastore)
        {
            _playerRidingDatastore = playerRidingDatastore;
            trainUpdateEvent.OnTrainCarRemoved.Subscribe(OnTrainCarRemoved);
        }

        private void OnTrainCarRemoved(TrainCarInstanceId trainCarInstanceId)
        {
            // OnRidableRemoved は冪等。接続中乗員には OnRidingStateChanged 経由で降車イベントが broadcast される。
            // OnRidableRemoved is idempotent; connected riders get a broadcast via OnRidingStateChanged.
            var identifier = new TrainCarRidableIdentifier(trainCarInstanceId.AsPrimitive());
            _playerRidingDatastore.OnRidableRemoved(identifier);
        }
    }
}
```

注: `ITrainUpdateEvent` に `OnTrainCarRemoved` があることは確認済み。`ITrainUpdateEvent` が `Game.Train.Event` にあるか確認し、`Game.PlayerRiding.asmdef` が `Game.Train` を参照済み（Phase 2 で設定）であることを前提とする。

- [ ] **Step 4: DI 登録**

`MoorestechServerDIContainerGenerator.cs` のイベントレシーバー登録群に追加:

```csharp
            services.AddSingleton<Game.PlayerRiding.TrainCarRemovedRidingHandler>();
```

即時インスタンス化群に追加:

```csharp
            serviceProvider.GetService<Game.PlayerRiding.TrainCarRemovedRidingHandler>();
```

`ITrainUpdateEvent` がメイン `services` に登録済みかを確認する（`grep -n "ITrainUpdateEvent" MoorestechServerDIContainerGenerator.cs`）。未登録なら、`TrainUpdateEvent` を `ITrainUpdateEvent` として登録する行を追加する。

- [ ] **Step 5: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainCarRemovedRidingHandler_Dismounts"`
Expected: PASS。

- [ ] **Step 6: コミット**

```bash
git add moorestech_server/Assets/Scripts/Game.PlayerRiding/TrainCarRemovedRidingHandler.cs moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/PlayerRidingDatastoreTest.cs
git commit -m "乗車Phase3: 車両削除時の降車ハンドラを追加"
```

---

## Task 8: InitialHandshakeProtocol のログイン復帰拡張

仕様書セクション5.3・8。ログイン時に `EvaluateOnLogin` を呼び、復帰した乗車状態をレスポンスに含める。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/InitialHandshakeProtocol.cs`
- Test: `moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/RideActionProtocolTest.cs`

- [ ] **Step 1: 失敗するテストを書く**

`RideActionProtocolTest.cs` に追記:

```csharp
        [Test]
        public void InitialHandshake_RestoresRidingState_WhenSeatValid()
        {
            // ログイン時、保存済み乗車状態が有効なら復帰し、レスポンスに乗車状態が乗る
            var (packetResponseCreator, car, playerId) = TrainTestHelper.CreateServerWithSavedRidingState(seatIndex: 0);
            var request = new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(playerId, "tester");

            var responseBytes = packetResponseCreator.GetPacketResponse(MessagePackSerializer.Serialize(request));
            var response = MessagePackSerializer.Deserialize<InitialHandshakeProtocol.ResponseInitialHandshakeMessagePack>(responseBytes[0]);

            Assert.IsNotNull(response.RidingTarget);
            Assert.AreEqual(0, response.RidingSeatIndex);
        }
```

`TrainTestHelper.CreateServerWithSavedRidingState` は、`PlayerRidingDatastore` に車両への乗車状態を `InjectRidingStateForTest` で仕込んだうえで `PacketResponseCreator` を返すヘルパ。

- [ ] **Step 2: テストが失敗することを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "InitialHandshake_RestoresRidingState"`
Expected: コンパイルエラー（`RidingTarget` 等未定義）で FAIL。

- [ ] **Step 3: InitialHandshakeProtocol を拡張**

`InitialHandshakeProtocol.cs` を変更:
- using に `using Game.PlayerRiding;`、`using Game.PlayerRiding.Interface;`、`using Server.Util.MessagePack;`（既存なら不要）を追加。
- フィールド追加・コンストラクタで取得: `private readonly PlayerRidingDatastore _playerRidingDatastore;` → コンストラクタに `_playerRidingDatastore = serviceProvider.GetService<PlayerRidingDatastore>();`
- `GetResponse` を、座標取得に加えて乗車復帰判定を行う形に変更:

```csharp
        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<RequestInitialHandshakeMessagePack>(payload);
            var playerId = data.PlayerId;

            var playerPos = GetPlayerPosition(new EntityInstanceId(playerId));

            // ログイン時の乗車復帰判定（仕様書セクション8）。判定ロジックは PlayerRidingDatastore に集約。
            // Login-time riding restore evaluation. Decision logic lives in PlayerRidingDatastore.
            RidableIdentifierMessagePack ridingTarget = null;
            int ridingSeatIndex = -1;
            if (_playerRidingDatastore.EvaluateOnLogin(playerId)
                && _playerRidingDatastore.TryGetRidingState(playerId, out var state))
            {
                ridingTarget = state.Identifier.ToMessagePack();
                ridingSeatIndex = state.SeatIndex;
            }

            return new ResponseInitialHandshakeMessagePack(playerPos, ridingTarget, ridingSeatIndex);
        }
```

- `ResponseInitialHandshakeMessagePack` にフィールドを追加し、コンストラクタを差し替える:

```csharp
        [MessagePackObject]
        public class ResponseInitialHandshakeMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public Vector3MessagePack PlayerPos { get; set; }
            // 乗車復帰した場合の乗り物識別子。未乗車なら null。
            [Key(3)] public RidableIdentifierMessagePack RidingTarget { get; set; }
            // 乗車復帰した場合の座席index。未乗車なら -1。
            [Key(4)] public int RidingSeatIndex { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseInitialHandshakeMessagePack() { }

            public ResponseInitialHandshakeMessagePack(Vector3MessagePack playerPos, RidableIdentifierMessagePack ridingTarget, int ridingSeatIndex)
            {
                Tag = ProtocolTag;
                PlayerPos = playerPos;
                RidingTarget = ridingTarget;
                RidingSeatIndex = ridingSeatIndex;
            }
        }
```

注: `EvaluateOnLogin` が復帰成功時に `OnRidingStateChanged` を発火する（Task 1）ため、`RidingStateEventPacket` 経由で broadcast もされる。クライアント自身はこのレスポンスで乗車状態を知る（仕様書セクション5.3）。

- [ ] **Step 4: テストが通ることを確認**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "InitialHandshake_RestoresRidingState"`
Expected: PASS。

- [ ] **Step 5: クライアント側ハンドシェイク受信のコンパイル確認**

`ResponseInitialHandshakeMessagePack` はクライアントも逆シリアライズする。クライアントのハンドシェイク受信コード（`grep -rln "ResponseInitialHandshakeMessagePack" moorestech_client`）がフィールド追加で壊れないことを `uloop compile --project-path ./moorestech_client` で確認する（追加フィールドのみのため通常は影響なし。クライアントでの乗車状態の利用は Phase 4）。

- [ ] **Step 6: コミット**

```bash
git add moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/InitialHandshakeProtocol.cs moorestech_server/Assets/Scripts/Tests.UnitTest/PlayerRiding/RideActionProtocolTest.cs
git commit -m "乗車Phase3: InitialHandshakeProtocol にログイン復帰判定を追加"
```

---

## Phase 3 完了確認

- [ ] `uloop compile --project-path ./moorestech_client` がエラー 0。
- [ ] `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlayerRiding|RideActionProtocol|PlayerConnectionRegistry"` が全件 PASS。
- [ ] `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Train|Packet|StartGame"` で既存テストが回帰なし。
- [ ] 仕様書セクション5（`RideActionProtocol` / `RidingStateEventPacket`）・7（接続検知）・8（ログイン復帰）が実装されている。

Phase 3 で作ったプロトコル・イベント（`va:rideAction` / `va:event:ridingState`）と `ResponseInitialHandshakeMessagePack` の乗車フィールドを、Phase 4（クライアント）が利用する。
