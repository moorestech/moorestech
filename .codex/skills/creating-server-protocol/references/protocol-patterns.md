# サーバープロトコル実装リファレンス

## 目次
1. [ProtocolMessagePackBase](#protocolmessagepackbase)
2. [Request-Response型の実装例](#request-response型の実装例)
3. [Event型の実装例](#event型の実装例)
4. [PacketResponseCreatorへの登録](#packetresponsecreatorへの登録)
5. [EventProtocolProvider](#eventprotocolprovider)
6. [MessagePackクラスの規約](#messagepackクラスの規約)

---

## ProtocolMessagePackBase

**ファイル:** `moorestech_server/Assets/Scripts/Server.Protocol/ProtocolMessagePackBase.cs`

```csharp
[MessagePackObject]
public class ProtocolMessagePackBase
{
    [Key(0)] public string Tag { get; set; }
    [Key(1)] public int SequenceId { get; set; }
}
```

- `[Key(0)]` = Tag, `[Key(1)]` = SequenceId が予約済み
- サブクラスのカスタムフィールドは **`[Key(2)]`以降** を使用する

---

## Request-Response型の実装例

### InitialHandshakeProtocol

**ファイル:** `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/InitialHandshakeProtocol.cs`

```csharp
public class InitialHandshakeProtocol : IPacketResponse
{
    public const string ProtocolTag = "va:initialHandshake";

    private readonly IEntitiesDatastore _entitiesDatastore;
    private readonly IEntityFactory _entityFactory;

    // ServiceProviderから依存関係を取得
    // Get dependencies from ServiceProvider
    public InitialHandshakeProtocol(ServiceProvider serviceProvider)
    {
        _entitiesDatastore = serviceProvider.GetService<IEntitiesDatastore>();
        _entityFactory = serviceProvider.GetService<IEntityFactory>();
    }

    public ProtocolMessagePackBase GetResponse(List<byte> payload)
    {
        // リクエストをデシリアライズ
        // Deserialize request
        var data = MessagePackSerializer.Deserialize<RequestInitialHandshakeMessagePack>(payload.ToArray());

        // レスポンスを返す
        // Return response
        var response = new ResponseInitialHandshakeMessagePack(GetPlayerPosition(new EntityInstanceId(data.PlayerId)));
        return response;
    }

    [MessagePackObject]
    public class RequestInitialHandshakeMessagePack : ProtocolMessagePackBase
    {
        [Key(2)] public int PlayerId { get; set; }
        [Key(3)] public string PlayerName { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public RequestInitialHandshakeMessagePack() { }

        public RequestInitialHandshakeMessagePack(int playerId, string playerName)
        {
            Tag = ProtocolTag;
            PlayerId = playerId;
            PlayerName = playerName;
        }
    }

    [MessagePackObject]
    public class ResponseInitialHandshakeMessagePack : ProtocolMessagePackBase
    {
        [Key(2)] public Vector3MessagePack PlayerPos { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ResponseInitialHandshakeMessagePack() { }

        public ResponseInitialHandshakeMessagePack(Vector3MessagePack playerPos)
        {
            Tag = ProtocolTag;
            PlayerPos = playerPos;
        }
    }
}
```

**重要ポイント:**
- `IPacketResponse`を実装
- `ProtocolTag`定数でタグを定義（`va:`プレフィックス）
- `ServiceProvider`経由で依存関係を注入
- Request/ResponseはProtocolMessagePackBaseを継承し、Key(2)以降を使用
- Obsolete付きの引数なしコンストラクタが必須（MessagePackデシリアライズ用）
- 操作結果不要の場合は`null`を返却可能

---

## Event型の実装例

### PlaceBlockEventPacket

**ファイル:** `moorestech_server/Assets/Scripts/Server.Event/EventReceive/PlaceBlockEventPacket.cs`

```csharp
public class PlaceBlockEventPacket
{
    public const string EventTag = "va:event:blockPlace";
    private readonly EventProtocolProvider _eventProtocolProvider;

    public PlaceBlockEventPacket(EventProtocolProvider eventProtocolProvider)
    {
        _eventProtocolProvider = eventProtocolProvider;
        // ゲームイベントを購読
        // Subscribe to game event
        ServerContext.WorldBlockUpdateEvent.OnBlockPlaceEvent.Subscribe(OnPlaceBlock);
    }

    private void OnPlaceBlock(BlockPlaceProperties updateProperties)
    {
        // イベントデータをシリアライズ
        // Serialize event data
        var messagePack = new PlaceBlockEventMessagePack(
            updateProperties.Pos,
            blockId,
            direction,
            blockInstanceId
        );
        var payload = MessagePackSerializer.Serialize(messagePack);

        // 全プレイヤーにブロードキャスト
        // Broadcast to all players
        _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
    }

    [MessagePackObject]
    public class PlaceBlockEventMessagePack
    {
        [Key(0)] public BlockDataMessagePack BlockData { get; set; }

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public PlaceBlockEventMessagePack() { }
    }
}
```

**重要ポイント:**
- `EventProtocolProvider`をコンストラクタで受け取る
- UniRxの`.Subscribe()`でゲームイベントを購読（`/csharp-event-pattern`スキル参照）
- イベントデータは`byte[]`にシリアライズしてからProviderに渡す
- `AddBroadcastEvent()` = 全プレイヤー、`AddEvent(playerId, ...)` = 特定プレイヤー
- EventのMessagePackクラスはProtocolMessagePackBaseを継承**しない**（Key(0)から開始）
- EventTagは`va:event:`プレフィックス

---

## PacketResponseCreatorへの登録

**ファイル:** `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`

```csharp
public class PacketResponseCreator
{
    private readonly Dictionary<string, IPacketResponse> _packetResponseDictionary = new();

    public PacketResponseCreator(ServiceProvider serviceProvider)
    {
        _packetResponseDictionary.Add(InitialHandshakeProtocol.ProtocolTag, new InitialHandshakeProtocol(serviceProvider));
        _packetResponseDictionary.Add(AllBlockStateProtocol.ProtocolTag, new AllBlockStateProtocol(serviceProvider));
        // ... 追加登録
    }
}
```

新しいRequest-Responseプロトコルは必ずここに登録する。

---

## EventProtocolProvider

**ファイル:** `moorestech_server/Assets/Scripts/Server.Event/EventProtocolProvider.cs`

主要メソッド:
- `AddEvent(int playerId, string tag, byte[] payload)` - 特定プレイヤーにイベント送信
- `AddBroadcastEvent(string tag, byte[] payload)` - 全プレイヤーにブロードキャスト
- `GetEventBytesList(int playerId)` - プレイヤーのイベントを取得&クリア

スレッドセーフ（`lock`使用）。

---

## MessagePackクラスの規約

1. **クラスに`[MessagePackObject]`属性を付与**
2. **各フィールドに`[Key(N)]`属性を付与**（連番）
3. **Request/Responseクラス**: `ProtocolMessagePackBase`を継承、Key(2)以降を使用
4. **Eventデータクラス**: 継承不要、Key(0)から使用
5. **引数なしコンストラクタ**: `[Obsolete]`付きで必須
6. **ネストOK**: 複雑なデータ構造は別のMessagePackObjectクラスに分離
7. **`[IgnoreMember]`**: シリアライズ不要な計算プロパティに使用
