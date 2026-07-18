# イベント配信push型一本化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** サーバー→クライアントのイベント配信をpull型（50msポーリング）からpush型の単一FIFOストリームに置換し、train初期同期・resyncも同ストリームに統合する。

**Architecture:** サーバーは接続ごとの `SendQueueProcessor`（FIFO）に応答もイベントも生成順で積む。`EventProtocolProvider` はキューを廃止して「playerId→sinkへの即時ルーター」になり、接続登録イベントを起点にtrainのfull snapshotが同ストリームでpushされる。クライアントは受信を到着順に直列ディスパッチし、`va:event` タグをイベントストリームとして配信、full snapshotは到着順に即時適用する。

**Tech Stack:** C# / Unity / MessagePack / UniRx / UniTask / VContainer / uloop CLI

**Spec:** `docs/superpowers/specs/2026-07-18-event-push-unified-stream-design.md`

## Global Constraints

- partial禁止・1ファイル200行以下・try-catch原則禁止（外部境界のみ、根拠コメント必須）（AGENTS.md）
- イベントはUniRx（`Subject<T>` private保持 + `IObservable<T>`公開）。C# `event Action` 禁止
- コメントは「// 日本語 → // English」の2行セット、各1行厳守。自明コメント禁止
- 単純getter/setter禁止、値のセットは `SetHoge` メソッド。デフォルト引数禁止
- MessagePack: Request/Responseは `ProtocolMessagePackBase` 継承でKey(2)から、EventペイロードはKey(0)から。`[Obsolete]`付き引数なしコンストラクタ必須
- .cs変更後は必ず `uloop compile --project-path ./moorestech_client` を実行（サーバーコードもクライアントプロジェクトから同時コンパイルされる）
- テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`
- `Server.Protocol/PacketResponse/` 直下にはIPacketResponse実装以外を置かない
- 作業開始時に必ず `pwd` 確認。各タスク末尾で必ずコミット
- 実装は新ブランチ（例: `event-push-unified-stream`）で行う。web-uiブランチのwebui差分と混ぜない

## 配置と前例（spec-architecture-review済み）

| 新規/変更 | 配置 | 前例・根拠 |
|---|---|---|
| `IPlayerEventSink` | `Server.Event`（interface定義）、実装は `Server.Boot/Loop/PacketProcessing` | 下位asmdefがinterface定義・上位が実装。Server.Boot→Server.Event参照は既存 |
| `EventStreamMessagePack` | `Server.Protocol` 直下（PacketResponse外） | `ProtocolMessagePackBase` 継承のためServer.Protocol必須（Server.Event→Server.Protocol参照は循環になり不可）。直下配置は `ServerConst.cs`・`PacketResponseContext.cs` が前例 |
| `EventProtocolProvider` 書き換え | `Server.Event`（現位置） | UniRx Subject公開は `GameUnlockStateDatastoreController` 前例 |
| `TrainFullSnapshotEventPacket` | `Server.Event/EventReceive` + DI AddSingleton + eager init | 全EventPacketと同型（`TrainUnitSnapshotEventPacket` 等） |
| `TrainResyncProtocol` | `Server.Protocol/PacketResponse`。2旧プロトコルを1本に統合 | プロトコルは1ドメイン1本・リクエスト内フラグ分岐（`ElectricWireConnectionEditProtocol` 前例） |
| `GetCurrentTickSequenceId()` | `Game.Train/Unit/TrainUpdateService` | 採番カウンタの所有ドメイン |
| `TrainFullSnapshotEventNetworkHandler` | `Client.Game/InGame/Train/Network` | `TrainUnitSnapshotEventNetworkHandler` と同型（SubscribeEventResponse購読ハンドラ） |
| 受信直列化・イベントルーティング | `Client.Network/API/PacketExchangeManager` | 既存の受信ディスパッチ責務の明示化 |

---

### Task 1: サーバーpush送信路の追加（additive）

既存挙動を変えずに、push送信に必要な型と配線だけを足す。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Event/IPlayerEventSink.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/EventStreamMessagePack.cs`
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Loop/PacketProcessing/ConnectionPlayerEventSink.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseContext.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Loop/ServerListenAcceptor.cs:30-33`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/EventStreamMessagePackTest.cs`

**Interfaces:**
- Produces: `IPlayerEventSink.EnqueueEvent(EventMessagePack)` / `EventStreamMessagePack(EventMessagePack)`（`ProtocolTag = "va:event"`、`[Key(2)] EventMessagePack Event`）/ `PacketResponseContext.EventSink`（`SetEventSink`でセット）— Task 2以降が使用

- [ ] **Step 1: IPlayerEventSink を作成**

```csharp
namespace Server.Event
{
    // プレイヤー1接続分のイベント送信先
    // Per-connection sink that receives events for one player
    public interface IPlayerEventSink
    {
        // イベント1件を接続の送信キューへ積む
        // Enqueue one event into the connection's send queue
        void EnqueueEvent(EventMessagePack eventMessagePack);
    }
}
```

- [ ] **Step 2: EventStreamMessagePack を作成**

```csharp
using System;
using MessagePack;
using Server.Event;

namespace Server.Protocol
{
    // サーバーからpush配信されるイベント1件のenvelope
    // Envelope for one server-pushed event on the unified stream
    [MessagePackObject]
    public class EventStreamMessagePack : ProtocolMessagePackBase
    {
        public const string ProtocolTag = "va:event";

        [Key(2)] public EventMessagePack Event { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public EventStreamMessagePack() { }

        public EventStreamMessagePack(EventMessagePack eventMessagePack)
        {
            Tag = ProtocolTag;
            Event = eventMessagePack;
        }
    }
}
```

注意: この時点では既存 `EventProtocol` も `"va:event"` タグを使っているが、Task 2で削除されるまで両者が同時にワイヤに乗ることはない（本タスクでは送信元をまだ作らない）。

- [ ] **Step 3: ConnectionPlayerEventSink を作成**

```csharp
using MessagePack;
using Server.Event;
using Server.Protocol;
using Server.Util;

namespace Server.Boot.Loop.PacketProcessing
{
    // 接続のSendQueueProcessorへenvelope+長さヘッダ付きでイベントを積むsink
    // Sink that wraps a connection's SendQueueProcessor with envelope and length header
    public class ConnectionPlayerEventSink : IPlayerEventSink
    {
        private readonly SendQueueProcessor _sendQueueProcessor;

        public ConnectionPlayerEventSink(SendQueueProcessor sendQueueProcessor)
        {
            _sendQueueProcessor = sendQueueProcessor;
        }

        public void EnqueueEvent(EventMessagePack eventMessagePack)
        {
            var body = MessagePackSerializer.Serialize(new EventStreamMessagePack(eventMessagePack));

            // 応答と同じ長さヘッダ形式で積み、FIFOで応答との順序を保つ
            // Use the same length header as responses so FIFO order holds across the stream
            var header = ToByteArray.Convert(body.Length);
            var sendData = new byte[header.Length + body.Length];
            header.CopyTo(sendData, 0);
            body.CopyTo(sendData, header.Length);
            _sendQueueProcessor.EnqueueSendData(sendData);
        }
    }
}
```

- [ ] **Step 4: PacketResponseContext に EventSink を追加**

`PacketResponseContext` に以下を追加（既存の `_lock`/`PlayerId` はそのまま）:

```csharp
        // 接続生成時に一度だけセットされ、以後読み取り専用（受信スレッド起動前にセットされるためlock不要）
        // Set once at connection creation before the receive thread starts; read-only afterwards
        public IPlayerEventSink EventSink { get; private set; }

        public void SetEventSink(IPlayerEventSink eventSink)
        {
            EventSink = eventSink;
        }
```

`using Server.Event;` をファイル先頭に追加。

- [ ] **Step 5: ServerListenAcceptor で sink を生成して context にセット**

`ServerListenAcceptor.cs:30-33` を以下に変更:

```csharp
                // 送信・受信キュープロセッサを作成
                var packetResponseContext = new PacketResponseContext();
                var sendQueueProcessor = new SendQueueProcessor(client);
                // イベントpush用のsinkを接続生成時に配線する（受信スレッド起動前）
                // Wire the event push sink at connection creation, before the receive thread starts
                packetResponseContext.SetEventSink(new ConnectionPlayerEventSink(sendQueueProcessor));
                var receiveQueueProcessor = new ReceiveQueueProcessor(packetResponseCreator, sendQueueProcessor, packetResponseContext);
```

- [ ] **Step 6: envelopeのシリアライズテストを作成**

`Tests/CombinedTest/Server/PacketTest/Event/EventStreamMessagePackTest.cs`:

```csharp
using MessagePack;
using NUnit.Framework;
using Server.Event;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class EventStreamMessagePackTest
    {
        // envelopeのTag/中身がラウンドトリップで保持されることを確認
        // Verify tag and inner event survive a serialize/deserialize round trip
        [Test]
        public void SerializeDeserializeRoundTrip()
        {
            var payload = new byte[] { 1, 2, 3 };
            var packet = new EventStreamMessagePack(new EventMessagePack("va:event:test", payload));
            var bytes = MessagePackSerializer.Serialize(packet);

            // クライアントのルーティングと同じ手順: base型でTagを読む → envelope型で中身を読む
            // Same as client routing: read Tag via base type, then the full envelope
            var basePacket = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(bytes);
            Assert.AreEqual(EventStreamMessagePack.ProtocolTag, basePacket.Tag);

            var deserialized = MessagePackSerializer.Deserialize<EventStreamMessagePack>(bytes);
            Assert.AreEqual("va:event:test", deserialized.Event.Tag);
            CollectionAssert.AreEqual(payload, deserialized.Event.Payload);
        }
    }
}
```

- [ ] **Step 7: コンパイルとテスト実行**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "EventStreamMessagePackTest"
```

Expected: コンパイルエラー0、テストPASS

- [ ] **Step 8: コミット**

```bash
git add -A moorestech_server/Assets/Scripts
git commit -m "feat(server): push配信用のsink/envelope/接続配線を追加"
```

---

### Task 2: 配信切替 — provider即時ルーター化・ポーリング廃止・テスト移行

pull→pushの切替本体。サーバーとクライアントの両側を同時に切り替える（片側だけでは動作しないため1タスクで行う）。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Event/EventProtocolProvider.cs`（全面書き換え）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/InitialHandshakeProtocol.cs:45`
- Delete: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/EventProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs:36`（EventProtocol登録行を削除）
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`（切断購読の配線）
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/ServerConst.cs`（`PollingRateMillSec` 削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/ServerCommunicator.cs:72`
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/PacketExchangeManager.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiEvent.cs`（全面書き換え）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApi.cs:23`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs:140-151`（StartDispatch呼び出し）
- Modify: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/EventTestUtil.cs`（キャプチャsink化）
- Modify: `va:event` ポーリング依存の全テスト（Step 8で列挙）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/EventProtocolProviderTest.cs`

**Interfaces:**
- Consumes: Task 1の `IPlayerEventSink` / `EventStreamMessagePack` / `PacketResponseContext.EventSink`
- Produces: `EventProtocolProvider.RegisterPlayer(int, IPlayerEventSink)`・`UnregisterPlayer(int)`・`OnPlayerEventStreamRegistered: IObservable<int>`・`ListenDisconnect(IObservable<int>)`（AddEvent/AddBroadcastEventのシグネチャは不変）/ `PacketExchangeManager.OnEventPacket: IObservable<EventMessagePack>`・`EnqueueReceivedPacket(byte[])` / `VanillaApiEvent.StartDispatch()` / テスト用 `EventTestUtil.RegisterCaptureSink(ServiceProvider, int): CapturedEventSink`

- [ ] **Step 1: EventProtocolProvider の新仕様テストを書く**

`Tests/CombinedTest/Server/PacketTest/Event/EventProtocolProviderTest.cs`:

```csharp
using System.Collections.Generic;
using NUnit.Framework;
using Server.Event;
using UniRx;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class EventProtocolProviderTest
    {
        // 登録済みsinkへ即時配信されることを確認
        // Registered sinks receive events immediately
        [Test]
        public void AddEventDispatchesToRegisteredSink()
        {
            var provider = new EventProtocolProvider();
            var sink = new CapturedEventSink();
            provider.RegisterPlayer(1, sink);

            provider.AddEvent(1, "va:event:test", new byte[] { 1 });

            Assert.AreEqual(1, sink.Events.Count);
            Assert.AreEqual("va:event:test", sink.Events[0].Tag);
        }

        // 未登録プレイヤー宛は破棄されることを確認
        // Events for unregistered players are dropped
        [Test]
        public void AddEventForUnregisteredPlayerIsDropped()
        {
            var provider = new EventProtocolProvider();
            var sink = new CapturedEventSink();
            provider.RegisterPlayer(1, sink);

            provider.AddEvent(2, "va:event:test", new byte[] { 1 });

            Assert.AreEqual(0, sink.Events.Count);
        }

        // broadcastが全登録sinkへ届くことを確認
        // Broadcast reaches every registered sink
        [Test]
        public void BroadcastReachesAllRegisteredSinks()
        {
            var provider = new EventProtocolProvider();
            var sink1 = new CapturedEventSink();
            var sink2 = new CapturedEventSink();
            provider.RegisterPlayer(1, sink1);
            provider.RegisterPlayer(2, sink2);

            provider.AddBroadcastEvent("va:event:test", new byte[] { 1 });

            Assert.AreEqual(1, sink1.Events.Count);
            Assert.AreEqual(1, sink2.Events.Count);
        }

        // 登録イベントがsink登録後に同期発火することを確認（初期同期の順序契約）
        // Registration event fires synchronously after the sink becomes usable
        [Test]
        public void RegistrationEventFiresAfterSinkIsUsable()
        {
            var provider = new EventProtocolProvider();
            var sink = new CapturedEventSink();
            provider.OnPlayerEventStreamRegistered.Subscribe(playerId =>
                provider.AddEvent(playerId, "va:event:initial", new byte[] { 1 }));

            provider.RegisterPlayer(1, sink);

            Assert.AreEqual(1, sink.Events.Count);
            Assert.AreEqual("va:event:initial", sink.Events[0].Tag);
        }

        // 切断購読でsinkが解除され、以後のイベントが破棄されることを確認
        // Disconnect subscription unregisters the sink; later events are dropped
        [Test]
        public void ListenDisconnectUnregistersSink()
        {
            var provider = new EventProtocolProvider();
            var sink = new CapturedEventSink();
            var disconnect = new Subject<int>();
            provider.ListenDisconnect(disconnect);
            provider.RegisterPlayer(1, sink);

            disconnect.OnNext(1);
            provider.AddEvent(1, "va:event:test", new byte[] { 1 });

            Assert.AreEqual(0, sink.Events.Count);
        }
    }
}
```

- [ ] **Step 2: EventTestUtil をキャプチャsinkヘルパーに書き換える**

```csharp
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Server.Event;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    // push型イベントをテストで捕捉するためのsink登録ヘルパー
    // Helper that registers a capturing sink to observe pushed events in tests
    public class EventTestUtil
    {
        public static CapturedEventSink RegisterCaptureSink(ServiceProvider serviceProvider, int playerId)
        {
            var sink = new CapturedEventSink();
            serviceProvider.GetService<EventProtocolProvider>().RegisterPlayer(playerId, sink);
            return sink;
        }
    }

    // 送信されたイベントをListに溜めるテスト用sink
    // Test sink that captures dispatched events into a list
    public class CapturedEventSink : IPlayerEventSink
    {
        public List<EventMessagePack> Events { get; } = new();

        public void EnqueueEvent(EventMessagePack eventMessagePack)
        {
            Events.Add(eventMessagePack);
        }

        // 現在までのイベントを取り出してクリアする（旧ポーリングの「全返し＆Clear」相当）
        // Take all captured events and clear, mirroring the old poll-and-clear semantics
        public List<EventMessagePack> TakeAll()
        {
            var taken = new List<EventMessagePack>(Events);
            Events.Clear();
            return taken;
        }
    }
}
```

- [ ] **Step 3: EventProtocolProvider を即時ルーターに書き換える**

全面書き換え（`EventMessagePack` クラスはファイル内にそのまま残す）:

```csharp
using System;
using System.Collections.Generic;
using MessagePack;
using UniRx;

namespace Server.Event
{
    /// <summary>
    ///     サーバー内で起こったイベントを、接続済みプレイヤーのsinkへ即時配信します。
    ///     Immediately dispatches server events to registered per-player sinks.
    /// </summary>
    public class EventProtocolProvider
    {
        private readonly Dictionary<int, IPlayerEventSink> _sinks = new();
        private readonly object _lock = new();
        private readonly Subject<int> _playerEventStreamRegistered = new();

        // sink登録完了直後に発火。購読者は同期的にAddEventすること（初期同期の順序契約）
        // Fires right after registration. Subscribers must AddEvent synchronously (ordering contract).
        public IObservable<int> OnPlayerEventStreamRegistered => _playerEventStreamRegistered;

        public void RegisterPlayer(int playerId, IPlayerEventSink sink)
        {
            // sink未配線のテスト経路ではイベント購読なしとして扱う（本番はacceptorが必ずセットする）
            // Treat null sinks (test-only paths) as no subscription; production always sets one
            if (sink == null) return;

            lock (_lock)
            {
                _sinks[playerId] = sink;
            }

            // 登録完了後に発火し、購読者が初期イベントを同期pushできるようにする
            // Fire after registration so subscribers can push initial events synchronously
            _playerEventStreamRegistered.OnNext(playerId);
        }

        public void UnregisterPlayer(int playerId)
        {
            lock (_lock)
            {
                _sinks.Remove(playerId);
            }
        }

        // 切断通知を購読してsinkを自動解除する（boot時にDIで配線）
        // Subscribe disconnect notifications to auto-unregister sinks (wired at boot)
        public void ListenDisconnect(IObservable<int> onPlayerDisconnected)
        {
            onPlayerDisconnected.Subscribe(UnregisterPlayer);
        }

        public void AddEvent(int playerId, string tag, byte[] payload)
        {
            lock (_lock)
            {
                // 未接続プレイヤー宛は破棄する（handshakeで全量を取り直すため正しい）
                // Drop events for unconnected players; they fully re-sync on handshake
                if (_sinks.TryGetValue(playerId, out var sink))
                {
                    sink.EnqueueEvent(new EventMessagePack(tag, payload));
                }
            }
        }

        public void AddBroadcastEvent(string tag, byte[] payload)
        {
            lock (_lock)
            {
                var eventMessagePack = new EventMessagePack(tag, payload);
                foreach (var sink in _sinks.Values) sink.EnqueueEvent(eventMessagePack);
            }
        }
    }

    [MessagePackObject]
    public class EventMessagePack
    {
        // （既存のまま変更なし）
    }
}
```

`EventMessagePack` は既存定義を一字も変えずに残す（`Key(2) MessagePacks` フィールド含む。掃除はスコープ外）。

- [ ] **Step 4: InitialHandshakeProtocol の登録をsink付きに変更**

`InitialHandshakeProtocol.cs:45` を変更:

```csharp
            _eventProtocolProvider.RegisterPlayer(data.PlayerId, context.EventSink);
```

- [ ] **Step 5: EventProtocol を削除し登録行を除去**

- `Server.Protocol/PacketResponse/EventProtocol.cs` を削除（`.meta` はUnityに任せる。手動作成・編集禁止）
- `PacketResponseCreator.cs:36` の `_packetResponseDictionary.Add(EventProtocol.ProtocolTag, ...)` 行を削除

- [ ] **Step 6: 切断購読を配線し、ServerConst を掃除**

`MoorestechServerDIContainerGenerator.cs` のeager init節（`serviceProvider.GetService<MainInventoryUpdateEventPacket>();` がある箇所の直前）に追加:

```csharp
            // 切断時にイベントsinkを自動解除する配線
            // Wire automatic event sink unregistration on disconnect
            var eventProtocolProvider = serviceProvider.GetService<EventProtocolProvider>();
            var playerConnectionRegistry = (PlayerConnectionRegistry)serviceProvider.GetService<IPlayerConnectionChecker>();
            eventProtocolProvider.ListenDisconnect(playerConnectionRegistry.OnPlayerDisconnected);
```

`ServerConst.cs` から `PollingRateMillSec` を削除する前に利用箇所を確認:

```bash
grep -rn "PollingRateMillSec" moorestech_server moorestech_client --include="*.cs"
```

Expected: `VanillaApiEvent.cs`（本タスクで削除）と `ServerConst.cs` 定義のみ。他に利用があれば削除せず残す。

- [ ] **Step 7: クライアント受信の直列化とイベントルーティング**

`PacketExchangeManager.cs` を変更。`ExchangeReceivedPacket` を削除し、以下を追加:

```csharp
        private readonly ConcurrentQueue<byte[]> _receivedPackets = new();
        private readonly Subject<EventMessagePack> _eventPacketSubject = new();

        // サーバーからpushされたイベントの購読口
        // Subscription point for server-pushed events
        public IObservable<EventMessagePack> OnEventPacket => _eventPacketSubject;

        public PacketExchangeManager(PacketSender packetSender)
        {
            _packetSender = packetSender;
            TimeOutUpdate().Forget();
            DispatchReceivedPacketsLoop().Forget();
        }

        // 受信スレッドから呼ばれる。処理はメインスレッドの直列ループに委譲する
        // Called from the receive thread; processing is deferred to the serial main-thread loop
        public void EnqueueReceivedPacket(byte[] data)
        {
            _receivedPackets.Enqueue(data);
        }

        private async UniTask DispatchReceivedPacketsLoop()
        {
            while (true)
            {
                // 到着順を保ったままメインスレッドで直列ディスパッチする（順序契約）
                // Dispatch serially on the main thread preserving arrival order (ordering contract)
                await UniTask.Yield(PlayerLoopTiming.Update);
                while (_receivedPackets.TryDequeue(out var data))
                {
                    DispatchPacket(data);
                }
            }
        }

        private void DispatchPacket(byte[] data)
        {
            var basePacket = MessagePackSerializer.Deserialize<ProtocolMessagePackBase>(data);

            // イベントpushはSequenceId照合ではなくイベントストリームへ流す
            // Route pushed events to the event stream instead of sequence matching
            if (basePacket.Tag == EventStreamMessagePack.ProtocolTag)
            {
                var eventPacket = MessagePackSerializer.Deserialize<EventStreamMessagePack>(data);
                _eventPacketSubject.OnNext(eventPacket.Event);
                return;
            }

            var sequence = basePacket.SequenceId;
            if (!_responseWaiters.ContainsKey(sequence)) return;
            _responseWaiters[sequence].WaitSubject.OnNext((data, PacketWaitCompletionReason.Received));
            _responseWaiters.Remove(sequence);
        }
```

ファイル先頭に `using System.Collections.Concurrent;` `using Server.Event;` を追加。

`ServerCommunicator.cs:72` を変更:

```csharp
                    foreach (var packet in packets) packetExchangeManager.EnqueueReceivedPacket(packet);
```

- [ ] **Step 8: VanillaApiEvent をポーリングから購読+バッファに書き換える**

全面書き換え:

```csharp
using System;
using System.Collections.Generic;
using Server.Event;
using UniRx;

namespace Client.Network.API
{
    public class VanillaApiEvent
    {
        private readonly Dictionary<string, Subject<byte[]>> _eventResponseSubjects = new();
        private readonly List<EventMessagePack> _bufferedEvents = new();
        private bool _isDispatchStarted;

        public VanillaApiEvent(PacketExchangeManager packetExchangeManager)
        {
            // push配信されたイベントを購読する（ポーリング廃止）
            // Subscribe to pushed events; polling is removed
            packetExchangeManager.OnEventPacket.Subscribe(OnEventPacketReceived);
        }

        private void OnEventPacketReceived(EventMessagePack eventMessagePack)
        {
            // ハンドラ購読完了前は全イベントをバッファする（初回同期の取りこぼし防止）
            // Buffer everything until StartDispatch so no event is lost before handlers subscribe
            if (!_isDispatchStarted)
            {
                _bufferedEvents.Add(eventMessagePack);
                return;
            }

            Dispatch(eventMessagePack);
        }

        // 全ハンドラの購読登録完了後に1回だけ呼ぶ。バッファを到着順にreplayして即時配信へ移行する
        // Call once after all handlers subscribed; replays the buffer in arrival order then goes live
        public void StartDispatch()
        {
            _isDispatchStarted = true;
            foreach (var buffered in _bufferedEvents) Dispatch(buffered);
            _bufferedEvents.Clear();
        }

        private void Dispatch(EventMessagePack eventMessagePack)
        {
            if (!_eventResponseSubjects.TryGetValue(eventMessagePack.Tag, out var subject)) return;
            subject.OnNext(eventMessagePack.Payload);
        }

        public IDisposable SubscribeEventResponse(string tag, Action<byte[]> responseAction)
        {
            if (!_eventResponseSubjects.TryGetValue(tag, out var subject))
            {
                subject = new Subject<byte[]>();
                _eventResponseSubjects.Add(tag, subject);
            }

            return subject.Subscribe(responseAction);
        }
    }
}
```

`VanillaApi.cs:23` を `Event = new VanillaApiEvent(packetExchangeManager);` に変更。

- [ ] **Step 9: StartDispatch を初期化フローに配置**

`InitializeScenePipeline.cs` の `MainGameSceneLoaded` 内、`WebUiHost.Game.WebUiGameBinder.Bind();` の直後（`GameInitializedEvent.FireGameInitialized();` の前）に追加:

```csharp
                // 全ハンドラ購読完了後にバッファ済みイベントをreplayする
                // Replay buffered events after all handlers have subscribed
                serverResult.VanillaApi.Event.StartDispatch();
```

- [ ] **Step 10: ポーリング依存テストを全列挙して移行**

対象を列挙:

```bash
grep -rln "EventProtocolMessagePack\|ResponseEventProtocolMessagePack\|EventProtocol\." moorestech_server/Assets/Scripts/Tests --include="*.cs"
```

既知の対象（グレップ結果と突合すること）: `InitialHandshakeProtocolTest` / `EventTestUtil`（Step 2で対応済み）/ `MapObjectUpdateEventPacketTest` / `PlayerMainInventoryUpdateTest` / `UnlockedEventPacketTest` / `BlockInventoryUpdateEventPacketTest` / `ItemStackLevelUnlockEventPacketTest` / `ChallengeCompletedEventTest` / `ResearchCompleteEventPacketTest` / `ChangeBlockEventPacketTest` / `BlockPlaceEventPacketTest` / `BlockRemoveEventPacketTest` / `RidingStateEventPacketTest` / `ChangeBlockStateEventPacketDedupTest` / `ChainProtocolTest` / `RequestBlockStateProtocolTest`

各ファイルに以下の機械的変換を適用する（`BlockPlaceEventPacketTest` での例）:

変換前のパターン:

```csharp
List<byte[]> response = packetResponse.GetPacketResponse(EventRequestData(0), new PacketResponseContext());
var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0]);
// eventMessagePack.Events を検証
```

変換後のパターン:

```csharp
// テスト開始時に1回だけ:
var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, 0);

// 各「ポーリングして検証」箇所:
var events = sink.TakeAll();
// events（List<EventMessagePack>）を従来のeventMessagePack.Eventsと同様に検証
```

- 「最初にポーリングして空を確認」は `Assert.AreEqual(0, sink.TakeAll().Count)` に置換
- `using static Server.Protocol.PacketResponse.EventProtocol;` と自前の `EventRequestData` メソッドは削除
- `serviceProvider` を受け取っていないテストは `var (packetResponse, serviceProvider) = ...Create(...)` に変更

- [ ] **Step 11: コンパイルとテスト実行**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "EventProtocolProviderTest|EventPacketTest|EventTest|PlayerMainInventoryUpdateTest|InitialHandshakeProtocolTest|ChainProtocolTest|RequestBlockStateProtocolTest"
```

Expected: コンパイルエラー0、全PASS

- [ ] **Step 12: コミット**

```bash
git add -A moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts
git commit -m "feat: イベント配信をpull(50msポーリング)からpush(単一FIFOストリーム)へ切替"
```

---

### Task 3: train初期同期のイベント化（サーバー側・additive）

接続登録イベントを購読してrail→trainのfull snapshotをpushするEventPacketを追加する。この時点ではクライアントは未対応（未知タグとして破棄される）で、既存のrequest/response初期同期と共存する。

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Train/Unit/TrainUpdateService.cs:46`付近
- Create: `moorestech_server/Assets/Scripts/Server.Event/EventReceive/TrainFullSnapshotEventPacket.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/MoorestechServerDIContainerGenerator.cs`（AddSingleton + eager init）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/Event/TrainFullSnapshotEventPacketTest.cs`

**Interfaces:**
- Consumes: `EventProtocolProvider.OnPlayerEventStreamRegistered`（Task 2）、`CapturedEventSink`（Task 2）
- Produces: `TrainUpdateService.GetCurrentTickSequenceId(): uint` / `TrainFullSnapshotEventPacket.PushFullSnapshots(int playerId, bool includeRailGraph)` / イベントタグ `RailGraphFullSnapshotEventTag = "va:event:railGraphFullSnapshot"`・`TrainUnitFullSnapshotEventTag = "va:event:trainUnitFullSnapshot"` / ペイロード `RailGraphFullSnapshotEventMessagePack`（`[Key(0)] RailGraphSnapshotMessagePack Snapshot`）・`TrainUnitFullSnapshotEventMessagePack`（`[Key(0)] List<TrainUnitSnapshotBundleMessagePack> Snapshots, [Key(1)] uint ServerTick, [Key(2)] uint UnitsHash, [Key(3)] uint WatermarkTickSequenceId`）— Task 5・6が使用

- [ ] **Step 1: TrainUpdateService に watermark アクセサを追加**

`NextTickSequenceId()` の直後に追加:

```csharp
        // 発行済み最新のtick内順序ID（snapshotのwatermark用。新規採番しない）
        // Latest issued per-tick sequence id, used as snapshot watermark without consuming a new id
        public uint GetCurrentTickSequenceId() => _tickSequenceId;
```

- [ ] **Step 2: 順序契約テストを書く**

`Tests/CombinedTest/Server/PacketTest/Event/TrainFullSnapshotEventPacketTest.cs`:

```csharp
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Game.Train.Unit;
using Microsoft.Extensions.DependencyInjection;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class TrainFullSnapshotEventPacketTest
    {
        // handshake処理中にrail→trainの順でfull snapshotがpushされることを確認
        // Handshake pushes rail then train full snapshots, in that order, before the response returns
        [Test]
        public void HandshakePushesRailThenTrainSnapshotBeforeResponse()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var context = new PacketResponseContext();
            var sink = new CapturedEventSink();
            context.SetEventSink(sink);

            var handshake = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(0, "Player 0"));
            var response = packetResponse.GetPacketResponse(handshake, context);

            // GetPacketResponseが返った時点でsinkに両snapshotが積まれている＝応答より先にワイヤへ載る
            // Both snapshots are already in the sink when the response returns, so they precede it on the wire
            Assert.IsTrue(response.Count > 0);
            Assert.AreEqual(2, sink.Events.Count);
            Assert.AreEqual(TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag, sink.Events[0].Tag);
            Assert.AreEqual(TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag, sink.Events[1].Tag);
        }

        // snapshot pushがtickSequenceIdを新規消費しないことを確認（seq穴の防止）
        // Snapshot push must not consume a new tick sequence id (no gaps for other clients)
        [Test]
        public void SnapshotPushDoesNotConsumeTickSequenceId()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var trainUpdateService = serviceProvider.GetService<TrainUpdateService>();

            var before = trainUpdateService.GetCurrentTickSequenceId();

            var context = new PacketResponseContext();
            context.SetEventSink(new CapturedEventSink());
            var handshake = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(0, "Player 0"));
            packetResponse.GetPacketResponse(handshake, context);

            Assert.AreEqual(before, trainUpdateService.GetCurrentTickSequenceId());
        }
    }
}
```

- [ ] **Step 3: テストが失敗することを確認**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: `TrainFullSnapshotEventPacket` 未定義でコンパイルFAIL（これがredフェーズ）

- [ ] **Step 4: TrainFullSnapshotEventPacket を実装**

`Server.Event/EventReceive/TrainFullSnapshotEventPacket.cs`:

```csharp
using System;
using System.Collections.Generic;
using Game.Train.RailGraph;
using Game.Train.Unit;
using MessagePack;
using Server.Util.MessagePack;
using UniRx;

namespace Server.Event.EventReceive
{
    // 接続登録・resync要求に応じてtrain/railのfull snapshotをイベント経路でpushする
    // Pushes full train/rail snapshots over the event stream on connection or resync request
    public sealed class TrainFullSnapshotEventPacket
    {
        public const string RailGraphFullSnapshotEventTag = "va:event:railGraphFullSnapshot";
        public const string TrainUnitFullSnapshotEventTag = "va:event:trainUnitFullSnapshot";

        private readonly EventProtocolProvider _eventProtocolProvider;
        private readonly IRailGraphDatastore _railGraphDatastore;
        private readonly ITrainUnitLookupDatastore _trainUnitLookupDatastore;
        private readonly TrainUpdateService _trainUpdateService;

        public TrainFullSnapshotEventPacket(
            EventProtocolProvider eventProtocolProvider,
            IRailGraphDatastore railGraphDatastore,
            ITrainUnitLookupDatastore trainUnitLookupDatastore,
            TrainUpdateService trainUpdateService)
        {
            _eventProtocolProvider = eventProtocolProvider;
            _railGraphDatastore = railGraphDatastore;
            _trainUnitLookupDatastore = trainUnitLookupDatastore;
            _trainUpdateService = trainUpdateService;

            // 新規接続の登録完了を購読し、同期的に初期snapshotをpushする（順序契約）
            // Subscribe registration completion and push initial snapshots synchronously (ordering contract)
            eventProtocolProvider.OnPlayerEventStreamRegistered.Subscribe(playerId => PushFullSnapshots(playerId, true));
        }

        // rail→trainの順で対象プレイヤーへfull snapshotをpushする（resyncからも呼ばれる）
        // Push full snapshots (rail first, then train) to the player; also used by resync
        public void PushFullSnapshots(int playerId, bool includeRailGraph)
        {
            if (includeRailGraph) PushRailGraphFullSnapshot(playerId);
            PushTrainUnitFullSnapshot(playerId);

            #region Internal

            void PushRailGraphFullSnapshot(int targetPlayerId)
            {
                var snapshot = _railGraphDatastore.CaptureSnapshot(_trainUpdateService.GetCurrentTick());

                // watermarkは発行済み最新IDを使い、新規採番しない（他クライアントにseq穴を作らない）
                // Use the latest issued id as watermark without consuming a new one (no seq gaps for others)
                var message = new RailGraphSnapshotMessagePack(snapshot, _trainUpdateService.GetCurrentTickSequenceId());
                var payload = MessagePackSerializer.Serialize(new RailGraphFullSnapshotEventMessagePack(message));
                _eventProtocolProvider.AddEvent(targetPlayerId, RailGraphFullSnapshotEventTag, payload);
            }

            void PushTrainUnitFullSnapshot(int targetPlayerId)
            {
                var bundles = new List<TrainUnitSnapshotBundle>();
                var snapshots = new List<TrainUnitSnapshotBundleMessagePack>();
                foreach (var train in _trainUnitLookupDatastore.GetRegisteredTrains())
                {
                    var bundle = TrainUnitSnapshotFactory.CreateSnapshot(train);
                    bundles.Add(bundle);
                    snapshots.Add(new TrainUnitSnapshotBundleMessagePack(bundle));
                }

                var unitsHash = TrainUnitSnapshotHashCalculator.Compute(bundles);
                var payload = MessagePackSerializer.Serialize(new TrainUnitFullSnapshotEventMessagePack(
                    snapshots,
                    _trainUpdateService.GetCurrentTick(),
                    unitsHash,
                    _trainUpdateService.GetCurrentTickSequenceId()));
                _eventProtocolProvider.AddEvent(targetPlayerId, TrainUnitFullSnapshotEventTag, payload);
            }

            #endregion
        }

        #region MessagePack

        [MessagePackObject]
        public class RailGraphFullSnapshotEventMessagePack
        {
            [Key(0)] public RailGraphSnapshotMessagePack Snapshot { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RailGraphFullSnapshotEventMessagePack() { }

            public RailGraphFullSnapshotEventMessagePack(RailGraphSnapshotMessagePack snapshot)
            {
                Snapshot = snapshot;
            }
        }

        [MessagePackObject]
        public class TrainUnitFullSnapshotEventMessagePack
        {
            [Key(0)] public List<TrainUnitSnapshotBundleMessagePack> Snapshots { get; set; }
            [Key(1)] public uint ServerTick { get; set; }
            [Key(2)] public uint UnitsHash { get; set; }
            [Key(3)] public uint WatermarkTickSequenceId { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public TrainUnitFullSnapshotEventMessagePack() { }

            public TrainUnitFullSnapshotEventMessagePack(List<TrainUnitSnapshotBundleMessagePack> snapshots, uint serverTick, uint unitsHash, uint watermarkTickSequenceId)
            {
                Snapshots = snapshots;
                ServerTick = serverTick;
                UnitsHash = unitsHash;
                WatermarkTickSequenceId = watermarkTickSequenceId;
            }
        }

        #endregion
    }
}
```

`RailGraphSnapshotMessagePack` の名前空間は `GetRailGraphSnapshotProtocol.cs` のusing（`Server.Util.MessagePack` / `Game.Train.RailGraph`）を踏襲し、コンパイルエラーが出たら実際の定義位置に合わせて調整する。

- [ ] **Step 5: DI 登録と eager init を追加**

`MoorestechServerDIContainerGenerator.cs` の `services.AddSingleton<TrainUnitSnapshotEventPacket>();`（line 233付近）の直後に:

```csharp
            services.AddSingleton<TrainFullSnapshotEventPacket>();
```

eager init節の `serviceProvider.GetService<TrainUnitSnapshotEventPacket>();`（line 275付近）の直後に:

```csharp
            serviceProvider.GetService<TrainFullSnapshotEventPacket>();
```

- [ ] **Step 6: コンパイルとテスト実行**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainFullSnapshotEventPacketTest"
```

Expected: PASS（2件）

- [ ] **Step 7: コミット**

```bash
git add -A moorestech_server/Assets/Scripts
git commit -m "feat(server): 接続登録イベントでtrain/rail full snapshotをpushするEventPacketを追加"
```

---

### Task 4: クライアント初期同期の切替（イベント受信＋初期化ゲート）

初期snapshotをイベント受信に切り替え、handshakeでの取得2件を削除し、初期化完了ゲートを入れる。resyncはまだ旧経路のまま（Task 5）。

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainFullSnapshotEventNetworkHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/TrainUnitFutureMessageBuffer.cs`（purge公開メソッド追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/TrainUnitSnapshotApplier.cs`（IInitializable・handshake依存を除去）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/RailGraphSnapshotApplier.cs`（同上）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs:55-68`（rail/train取得2件を削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/Responses.cs`（`RailGraphSnapshot`/`TrainUnitSnapshots` プロパティ削除）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs:255,259,338`（DI変更・RestoreSpecificState公開化）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs:140-151`（初期化ゲート）

**Interfaces:**
- Consumes: Task 3のイベントタグ・ペイロード型、Task 2の `VanillaApiEvent.StartDispatch()`、既存 `TrainUnitSnapshotApplier.ApplySnapshot(TrainUnitSnapshotResponse)`・`RailGraphSnapshotApplier.ApplySnapshot(RailGraphSnapshotMessagePack)`
- Produces: `TrainFullSnapshotEventNetworkHandler.WaitForInitialSnapshotAsync(): UniTask`・`OnFullSnapshotApplied: IObservable<ulong>`（Task 5が使用）/ `TrainUnitFutureMessageBuffer.DiscardEventsAtOrBelow(ulong)` / `MainGameStarter.RestoreLoginState(InitialHandshakeResponse)`

- [ ] **Step 1: TrainUnitFutureMessageBuffer に purge メソッドを追加**

`DiscardHashesOlderThan` の直後に追加:

```csharp
        // full snapshot適用時に、watermark以下の古いイベントを一括破棄する
        // Discard buffered events at or below the applied full-snapshot watermark
        public void DiscardEventsAtOrBelow(ulong tickUnifiedId)
        {
            while (_futureEvents.Count > 0)
            {
                var firstTickUnifiedId = _futureEvents.First().Key;
                if (firstTickUnifiedId > tickUnifiedId) break;
                _futureEvents.Remove(firstTickUnifiedId);
            }
        }
```

（`System.Linq` は既にusing済み）

- [ ] **Step 2: TrainFullSnapshotEventNetworkHandler を作成**

```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Context;
using Client.Game.InGame.Train.Unit;
using Client.Game.InGame.Train.View;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using Game.Train.Unit;
using MessagePack;
using Server.Event.EventReceive;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.Train.Network
{
    // full snapshotイベントをストリーム到着順に即時適用する唯一のsnapshot適用経路
    // The single snapshot-apply path: applies full snapshots immediately in stream arrival order
    public sealed class TrainFullSnapshotEventNetworkHandler : IInitializable, IDisposable
    {
        private readonly RailGraphSnapshotApplier _railGraphSnapshotApplier;
        private readonly TrainUnitSnapshotApplier _trainSnapshotApplier;
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly Subject<ulong> _onFullSnapshotApplied = new();
        private readonly UniTaskCompletionSource _initialSnapshotApplied = new();
        private IDisposable _railSubscription;
        private IDisposable _trainSubscription;

        // full snapshot適用完了通知（resyncゲート解除に使用）
        // Notifies full-snapshot application completion (used to release the resync gate)
        public IObservable<ulong> OnFullSnapshotApplied => _onFullSnapshotApplied;

        public TrainFullSnapshotEventNetworkHandler(
            RailGraphSnapshotApplier railGraphSnapshotApplier,
            TrainUnitSnapshotApplier trainSnapshotApplier,
            TrainUnitFutureMessageBuffer futureMessageBuffer)
        {
            _railGraphSnapshotApplier = railGraphSnapshotApplier;
            _trainSnapshotApplier = trainSnapshotApplier;
            _futureMessageBuffer = futureMessageBuffer;
        }

        // 初期snapshot適用完了までのawait口（通常は即時完了する安全ゲート）
        // Await point for initial snapshot application; normally completes immediately
        public UniTask WaitForInitialSnapshotAsync() => _initialSnapshotApplied.Task;

        public void Initialize()
        {
            var vanillaApiEvent = ClientContext.VanillaApi.Event;
            _railSubscription = vanillaApiEvent.SubscribeEventResponse(TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag, OnRailGraphFullSnapshot);
            _trainSubscription = vanillaApiEvent.SubscribeEventResponse(TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag, OnTrainUnitFullSnapshot);
        }

        public void Dispose()
        {
            _railSubscription?.Dispose();
            _trainSubscription?.Dispose();
        }

        private void OnRailGraphFullSnapshot(byte[] payload)
        {
            var message = MessagePackSerializer.Deserialize<TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventMessagePack>(payload);
            _railGraphSnapshotApplier.ApplySnapshot(message.Snapshot);
        }

        private void OnTrainUnitFullSnapshot(byte[] payload)
        {
            var message = MessagePackSerializer.Deserialize<TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventMessagePack>(payload);

            // MessagePackのbundleをモデルへ変換してapplierの既存入力型に合わせる
            // Convert bundles to models to reuse the applier's existing input type
            var bundles = new List<TrainUnitSnapshotBundle>(message.Snapshots?.Count ?? 0);
            if (message.Snapshots != null)
            {
                foreach (var snapshot in message.Snapshots) bundles.Add(snapshot.ToModel());
            }

            var response = new TrainUnitSnapshotResponse(bundles, message.ServerTick, message.UnitsHash, message.WatermarkTickSequenceId);
            _trainSnapshotApplier.ApplySnapshot(response);

            // watermark以下の古いdiff/hashをpurgeし、以後のイベントが連続適用できる状態にする
            // Purge stale diffs/hashes at or below the watermark so later events continue seamlessly
            var watermarkId = TrainTickUnifiedIdUtility.CreateTickUnifiedId(message.ServerTick, message.WatermarkTickSequenceId);
            _futureMessageBuffer.DiscardEventsAtOrBelow(watermarkId);
            _futureMessageBuffer.DiscardHashesOlderThan(watermarkId);

            _onFullSnapshotApplied.OnNext(watermarkId);
            _initialSnapshotApplied.TrySetResult();
        }
    }
}
```

- [ ] **Step 3: 両applierからIInitializable・handshake依存を除去**

`TrainUnitSnapshotApplier.cs`: `IInitializable` 実装・`Initialize()` メソッド・`InitialHandshakeResponse` フィールドとコンストラクタ引数・`using VContainer.Unity;` を削除。`ApplySnapshot(TrainUnitSnapshotResponse)` はそのまま残す。

`RailGraphSnapshotApplier.cs`: 同様に `IInitializable`・`Initialize()`・`InitialHandshakeResponse` を削除。`ApplySnapshot(RailGraphSnapshotMessagePack)` はそのまま。

- [ ] **Step 4: MainGameStarter の DI を更新**

`MainGameStarter.cs:255,259` を変更:

```csharp
            builder.Register<RailGraphSnapshotApplier>(Lifetime.Singleton);
```

```csharp
            builder.Register<TrainUnitSnapshotApplier>(Lifetime.Singleton);
```

`builder.RegisterEntryPoint<TrainUnitTickDiffBundleEventNetworkHandler>();`（line 189付近）の直後に追加:

```csharp
            builder.Register<TrainFullSnapshotEventNetworkHandler>(Lifetime.Singleton).AsSelf().As<IInitializable>().As<IDisposable>();
```

- [ ] **Step 5: RestoreSpecificState を公開メソッド化して呼び出しを移動**

`MainGameStarter.cs`: `StartGame` 内の `RestoreSpecificState(initialHandshakeResponse);` 呼び出し行を削除し、メソッドを公開に変更（本体は不変）:

```csharp
        // 初期snapshot適用後にログイン時状態（列車乗車等）を復元する
        // Restore login-time state (e.g., riding a train) after the initial snapshot is applied
        public void RestoreLoginState(InitialHandshakeResponse init)
        {
            // （旧RestoreSpecificStateの本体をそのまま）
        }
```

- [ ] **Step 6: InitializeScenePipeline に初期化ゲートを実装**

`MainGameSceneLoaded` を以下に変更（Task 2のStartDispatch配置を包含）:

```csharp
            void MainGameSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                SceneManager.sceneLoaded -= MainGameSceneLoaded;
                FinalizeInitializationAsync().Forget();
            }

            async UniTask FinalizeInitializationAsync()
            {
                var starter = FindObjectOfType<MainGameStarter>();
                var resolver = starter.StartGame(serverResult.HandshakeResponse);
                new ClientDIContext(new DIContainer(resolver));

                // Web UIをHubへバインドする
                // Bind the Web UI to the hub
                WebUiHost.Game.WebUiGameBinder.Bind();

                // 全ハンドラ購読完了後にバッファ済みイベントをreplayする
                // Replay buffered events after all handlers have subscribed
                serverResult.VanillaApi.Event.StartDispatch();

                // 初期snapshot適用完了を待つ（順序契約により通常は即時完了する安全ゲート）
                // Await initial snapshot application; the ordering contract makes this normally instant
                await resolver.Resolve<TrainFullSnapshotEventNetworkHandler>().WaitForInitialSnapshotAsync();

                // 列車乗車などログイン中の特殊な状態を再現し、初期化完了を通知する
                // Restore login-time special states, then announce initialization completion
                starter.RestoreLoginState(serverResult.HandshakeResponse);
                GameInitializedEvent.FireGameInitialized();
            }
```

`using Client.Game.InGame.Train.Network;` と `using Cysharp.Threading.Tasks;`（未追加なら）をファイル先頭に追加。

- [ ] **Step 7: handshakeからrail/train取得を削除**

`VanillaApiWithResponse.cs:55-65` の `UniTask.WhenAll` から `GetRailGraphSnapshot(ct)` と `GetTrainUnitSnapshots(ct)` の2引数を削除（メソッド自体はTask 5で削除。この時点では未使用のまま残る）。

`Responses.cs`: `InitialHandshakeResponse` から `RailGraphSnapshot`・`TrainUnitSnapshots` プロパティと、コンストラクタのタプル要素 `railGraphSnapshot`・`trainUnitSnapshots` および代入2行を削除。`TrainUnitSnapshotResponse` クラスは**残す**（Step 2のhandlerが使用）。

- [ ] **Step 8: コンパイル**

```bash
uloop compile --project-path ./moorestech_client
```

Expected: エラー0（`InitialHandshakeResponse` のタプル型不一致が出たら `VanillaApiWithResponse.InitialHandShake` の `WhenAll` 結果との対応を確認）

- [ ] **Step 9: サーバーテスト回帰確認**

```bash
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainFullSnapshotEventPacketTest|EventProtocolProviderTest"
```

Expected: PASS

- [ ] **Step 10: コミット**

```bash
git add -A moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts
git commit -m "feat(client): train初期同期をイベント受信+初期化ゲートに切替"
```

---

### Task 5: resyncの一本化（va:trainResync）

resyncを「トリガーrequest→イベント経路でsnapshot push」に置換し、旧2プロトコルと即時適用パスを削除する。

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/TrainResyncProtocol.cs`
- Delete: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetTrainUnitSnapshotsProtocol.cs`
- Delete: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetRailGraphSnapshotProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`（2登録削除・1登録追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`（`GetRailGraphSnapshot`/`GetTrainUnitSnapshots` 削除・`SendTrainResync` 追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/View/TrainUnitHashVerifier.cs`（トリガー化）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/TrainResyncProtocolTest.cs`

**Interfaces:**
- Consumes: `TrainFullSnapshotEventPacket.PushFullSnapshots(int, bool)`（Task 3）、`TrainFullSnapshotEventNetworkHandler.OnFullSnapshotApplied`（Task 4）
- Produces: `TrainResyncProtocol`（Tag `va:trainResync`、Request `[Key(2)] bool IncludeRailGraph`、Responseはack）/ `VanillaApiWithResponse.SendTrainResync(bool, CancellationToken): UniTask<TrainResyncProtocol.ResponseMessagePack>`

- [ ] **Step 1: サーバーテストを書く**

`Tests/CombinedTest/Server/PacketTest/TrainResyncProtocolTest.cs`:

```csharp
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class TrainResyncProtocolTest
    {
        // resync要求でrail+trainのfull snapshotがイベント経路にpushされることを確認
        // Resync request pushes rail+train full snapshots over the event stream
        [Test]
        public void ResyncWithRailGraphPushesBothSnapshots()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var context = new PacketResponseContext();
            var sink = new CapturedEventSink();
            context.SetEventSink(sink);

            // handshakeでsink登録と初期push（ここで捕捉分はクリアする）
            // Handshake registers the sink and pushes initial snapshots; clear those captures
            var handshake = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(0, "Player 0"));
            packetResponse.GetPacketResponse(handshake, context);
            sink.TakeAll();

            var resync = MessagePackSerializer.Serialize(new TrainResyncProtocol.RequestMessagePack(true));
            var response = packetResponse.GetPacketResponse(resync, context);

            Assert.IsTrue(response.Count > 0);
            var events = sink.TakeAll();
            Assert.AreEqual(2, events.Count);
            Assert.AreEqual(TrainFullSnapshotEventPacket.RailGraphFullSnapshotEventTag, events[0].Tag);
            Assert.AreEqual(TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag, events[1].Tag);
        }

        // IncludeRailGraph=falseならtrainのみpushされることを確認
        // With IncludeRailGraph=false only the train snapshot is pushed
        [Test]
        public void ResyncTrainOnlyPushesTrainSnapshot()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var context = new PacketResponseContext();
            var sink = new CapturedEventSink();
            context.SetEventSink(sink);

            var handshake = MessagePackSerializer.Serialize(new InitialHandshakeProtocol.RequestInitialHandshakeMessagePack(0, "Player 0"));
            packetResponse.GetPacketResponse(handshake, context);
            sink.TakeAll();

            var resync = MessagePackSerializer.Serialize(new TrainResyncProtocol.RequestMessagePack(false));
            packetResponse.GetPacketResponse(resync, context);

            var events = sink.TakeAll();
            Assert.AreEqual(1, events.Count);
            Assert.AreEqual(TrainFullSnapshotEventPacket.TrainUnitFullSnapshotEventTag, events[0].Tag);
        }
    }
}
```

- [ ] **Step 2: TrainResyncProtocol を実装**

```csharp
using System;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;

namespace Server.Protocol.PacketResponse
{
    /// <summary>
    ///     train/rail再同期の引き金プロトコル。snapshot本体はイベント経路でpushされる
    ///     Trigger protocol for train/rail resync; snapshots are pushed over the event stream
    /// </summary>
    public sealed class TrainResyncProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:trainResync";

        private readonly TrainFullSnapshotEventPacket _trainFullSnapshotEventPacket;

        public TrainResyncProtocol(ServiceProvider serviceProvider)
        {
            _trainFullSnapshotEventPacket = serviceProvider.GetService<TrainFullSnapshotEventPacket>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var data = MessagePackSerializer.Deserialize<RequestMessagePack>(payload);

            // snapshotはイベント経路でpushし、応答はackのみ返す（適用経路を1本に保つ）
            // Push snapshots via the event stream; the response is a bare ack to keep one apply path
            _trainFullSnapshotEventPacket.PushFullSnapshots(context.PlayerId.Value, data.IncludeRailGraph);

            return new ResponseMessagePack(true);
        }

        [MessagePackObject]
        public class RequestMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public bool IncludeRailGraph { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestMessagePack() { }

            public RequestMessagePack(bool includeRailGraph)
            {
                Tag = ProtocolTag;
                IncludeRailGraph = includeRailGraph;
            }
        }

        [MessagePackObject]
        public class ResponseMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public bool Accepted { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseMessagePack() { }

            public ResponseMessagePack(bool accepted)
            {
                Tag = ProtocolTag;
                Accepted = accepted;
            }
        }
    }
}
```

- [ ] **Step 3: PacketResponseCreator を更新**

- `GetTrainUnitSnapshotsProtocol` と `GetRailGraphSnapshotProtocol` の `_packetResponseDictionary.Add(...)` 行を削除
- 追加: `_packetResponseDictionary.Add(TrainResyncProtocol.ProtocolTag, new TrainResyncProtocol(serviceProvider));`
- 不要になったコンストラクタ冒頭の `trainUpdateService` / `railGraphDatastore` / `trainUnitLookupDatastore` 取得変数は、他プロトコルが使っていなければ削除

- [ ] **Step 4: 旧2プロトコルを削除**

`GetTrainUnitSnapshotsProtocol.cs` と `GetRailGraphSnapshotProtocol.cs` を削除。

- [ ] **Step 5: VanillaApiWithResponse を更新**

- `GetRailGraphSnapshot` / `GetTrainUnitSnapshots` メソッドを削除
- 追加:

```csharp
        // train/rail再同期の引き金を送る。snapshot本体はイベント経路で届く
        // Send the resync trigger; snapshots arrive over the event stream
        public async UniTask<TrainResyncProtocol.ResponseMessagePack> SendTrainResync(bool includeRailGraph, CancellationToken ct)
        {
            var request = new TrainResyncProtocol.RequestMessagePack(includeRailGraph);
            return await _packetExchangeManager.GetPacketResponse<TrainResyncProtocol.ResponseMessagePack>(request, ct);
        }
```

- [ ] **Step 6: TrainUnitHashVerifier をトリガー+購読解除方式に書き換える**

コンストラクタを変更: `TrainUnitSnapshotApplier`・`RailGraphSnapshotApplier` の注入をやめ、`TrainFullSnapshotEventNetworkHandler` を注入して購読する:

```csharp
        private readonly TrainUnitFutureMessageBuffer _futureMessageBuffer;
        private readonly TrainUnitClientCache _trainCache;
        private readonly RailGraphClientCache _railGraphCache;
        private readonly TrainUnitTickState _tickState;
        private readonly IDisposable _fullSnapshotSubscription;
        private CancellationTokenSource _resyncCancellation;
        private int _resyncInProgress;

        public TrainUnitHashVerifier(
            TrainFullSnapshotEventNetworkHandler fullSnapshotEventNetworkHandler,
            TrainUnitFutureMessageBuffer futureMessageBuffer,
            TrainUnitClientCache trainCache,
            RailGraphClientCache railGraphCache,
            TrainUnitTickState tickState)
        {
            _futureMessageBuffer = futureMessageBuffer;
            _trainCache = trainCache;
            _railGraphCache = railGraphCache;
            _tickState = tickState;

            // full snapshot適用完了でresyncゲートを解除する（適用自体はhandlerが担う）
            // Release the resync gate on full-snapshot application; the handler owns the apply itself
            _fullSnapshotSubscription = fullSnapshotEventNetworkHandler.OnFullSnapshotApplied.Subscribe(_ => ReleaseResyncGate());
        }

        private void ReleaseResyncGate()
        {
            var cts = Interlocked.Exchange(ref _resyncCancellation, null);
            cts?.Dispose();
            Interlocked.Exchange(ref _resyncInProgress, 0);
        }
```

`Dispose()` に `_fullSnapshotSubscription?.Dispose();` を追加。

`RequestSnapshotAsync` を以下に置換（`ValidateCurrentTickHash` からの呼び出し名も合わせる）:

```csharp
            async UniTask RequestResyncAsync(bool includeRailGraph)
            {
                var api = ClientContext.VanillaApi.Response;
                var cts = new CancellationTokenSource();
                _resyncCancellation = cts;

                // 引き金だけ送る。snapshotはイベント経路で届き、適用完了通知でゲートが解除される
                // Send only the trigger; the snapshot arrives via the event stream and releases the gate on apply
                var ackResult = await api.SendTrainResync(includeRailGraph, cts.Token).SuppressCancellationThrow();
                if (ackResult.IsCanceled || ackResult.Result == null)
                {
                    // ack失敗（タイムアウト等）はゲートを解放して次回のhash検証で再試行する
                    // On ack failure (e.g. timeout) release the gate and let the next hash check retry
                    Debug.LogWarning("[TrainUnitHashVerifier] Resync trigger failed. Releasing gate for retry.");
                    ReleaseResyncGate();
                }
            }
```

呼び出し側 `RequestSnapshotAsync(isRailGraphMismatch).Forget();` は `RequestResyncAsync(isRailGraphMismatch).Forget();` に変更。`using UniRx;` を追加。

- [ ] **Step 7: コンパイルとテスト実行**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "TrainResyncProtocolTest|TrainFullSnapshotEventPacketTest"
```

Expected: PASS

- [ ] **Step 8: コミット**

```bash
git add -A moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts
git commit -m "feat: resyncをva:trainResyncトリガー+イベントpushに一本化し旧snapshotプロトコルを削除"
```

---

### Task 6: 統合検証

**Files:**
- なし（検証のみ。発見した問題は修正してコミット）

- [ ] **Step 1: 全体コンパイルとサーバーテスト全実行**

```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Tests.CombinedTest.Server"
```

Expected: コンパイルエラー0、全PASS。「Unity is reloading」エラー時は45秒待ってリトライ。

- [ ] **Step 2: 「応答先着依存」の全数確認**

リクエスト処理中に発火するイベントを列挙:

```bash
grep -rn "AddEvent\|AddBroadcastEvent" moorestech_server/Assets/Scripts/Server.Event/EventReceive moorestech_server/Assets/Scripts/Server.Protocol --include="*.cs"
```

各イベントについて「そのイベントを引き起こすリクエストの応答ハンドラ（クライアント側）が、応答が先に届くことを暗黙に仮定していないか」を確認する。確認方法: クライアントの対応する `SubscribeEventResponse` ハンドラと、同じ操作の `GetPacketResponse` await後の処理を突き合わせ、同一状態を二重更新している箇所（例: 設置応答でブロック生成しつつ設置イベントでも生成）で順序逆転が壊れないか（冪等か・古い方が捨てられるか）をコードリーディングで判定。疑わしい箇所はリストにしてユーザーへ報告する。

- [ ] **Step 3: 実プレイ検証**

unity-playmode-recorded-playtest スキルを起動し、以下を確認:

1. 初回接続でゲームが正常に起動する（初期化ゲートがデッドロックしない）
2. ブロック設置・インベントリ操作がサーバー同期される（イベントpushの実動作）
3. 列車を走らせて `hash mismatch` 警告が0件（`uloop get-logs --project-path ./moorestech_client --log-type Warning` で `TrainUnitHashVerifier` を検索）
4. 初回同期のイベント消失が起きない（接続直後のdiffが適用されること。ログの `1stHash` のtickとsnapshot watermarkのtickが連続していること）

- [ ] **Step 4: 最終コミット**

```bash
git status
git add -A
git commit -m "test: push型イベントストリームの統合検証"
```

（差分がなければコミット不要）

---

## 検証補足: 既知のリスクと観察ポイント

- **イベントが応答より先に届く**: Task 6 Step 2が主対策。実装中に順序依存を見つけたら、クライアント側ハンドラを冪等にする方向で修正する（サーバーの発火順は生成順が正）
- **resync中のtick停止**: `CanAdvanceTick` が false の間も `TrainUnitClientSimulator.Tick()` は毎フレーム回り続け、full snapshot適用は受信ハンドラ（メインスレッド・フレーム駆動）で行われるためデッドロックしない。Task 6 Step 3-3で実挙動確認
- **切断→再接続**: 再handshakeで `RegisterPlayer` が新sinkを上書き登録し、初期pushが再実行される。古い接続のsinkは `ListenDisconnect` 経由で解除されるが、同一playerIdの再接続が切断通知より先に来た場合も上書きで正しい状態になる
