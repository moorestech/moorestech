---
name: creating-server-protocol
description: |
  moorestechサーバーのプロトコル（Request-Response型・Event型）を作成するためのガイド。
  Use when:
  1. 新しいサーバープロトコル（IPacketResponse）を実装する時
  2. 新しいイベントパケット（EventPacket）を実装する時
  3. 「プロトコルを作って」「パケットを追加して」と依頼された時
  4. クライアント-サーバー間の通信を新規追加する時
  5. サーバー側に新しい可変状態（DataStore等）を追加し、それをクライアントに反映したい時（specを書く段階を含む。プロトコル不要と思っていても読むこと）
---

# サーバープロトコル作成ガイド

## プロトコル選択

| 条件 | 種類 |
|------|------|
| クライアントが明示的に情報を要求/操作を実行 | **Request-Response型** |
| サーバー側の状態変化をクライアントに通知 | **Event型** |

詳細パターンとコード例: [references/protocol-patterns.md](references/protocol-patterns.md) を参照。

## サーバー可変状態の同期チェックリスト（3点セット・CRITICAL）

サーバー側に新しい可変状態（DataStore・プレイヤー横断の動的データ）を追加してクライアントに見せる場合、以下3点を**すべて**実装する。1つでも欠けると再接続時の欠損・追従漏れが起きる:

| # | 実装物 | 場所 |
|---|---|---|
| 1 | イベントパケット（DataStoreの`IObservable`購読→`va:event:*`broadcast） | `Server.Event/EventReceive/*EventPacket.cs` + `MoorestechServerDIContainerGenerator`にAddSingleton+eager init |
| 2 | 初期データ（ログイン/再接続時の全量復元） | `InitialHandshakeProtocol`のResponseに同梱 or `va:get*`プロトコル |
| 3 | クライアント購読（受信→クライアント側DataStore反映） | `SubscribeEventResponse(XxxEventPacket.EventTag, ...)`する`*EventHandler`（`IInitializable`/`RegisterEntryPoint`） |

**禁止**: 他プロトコル（研究完了・チャレンジ等）の応答をパースして別状態を間接導出する`*Applier`。「新規プロトコル・イベントは作らない」とspecに書くのは新規パターンでありユーザー裁定が必要（PR988でApplier 2種が全廃され3点セットに作り直された）。
**前例**: `UnlockedEventPacket`+`GetGameUnlockStateProtocol`+`ClientGameUnlockStateDatastore`、`ItemStackLevelUnlockEventPacket`+`InitialHandshakeProtocol.ItemStackLevels`+`ItemStackLevelEventHandler`。
**順序**: 初期適用はそれに依存するデータ（インベントリ等）の取得より**先**に行う（`VanillaApiWithResponse.InitialHandShake`参照）。

## Request-Response型の作成手順

### Step 1: プロトコルクラスを作成

`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/` に新規ファイルを作成。

```csharp
using System;
using System.Collections.Generic;
using MessagePack;
using Server.Protocol;

namespace Server.Protocol.PacketResponse
{
    public class YourProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:yourProtocol";

        private readonly ISomeDependency _dependency;

        public YourProtocol(ServiceProvider serviceProvider)
        {
            _dependency = serviceProvider.GetService<ISomeDependency>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<YourRequestMessagePack>(payload);
            // ビジネスロジック
            return new YourResponseMessagePack(/* result */);
        }

        #region MessagePack

        [MessagePackObject]
        public class YourRequestMessagePack : ProtocolMessagePackBase
        {
            // Key(0)=Tag, Key(1)=SequenceId は基底クラスで予約済み
            [Key(2)] public int SomeField { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public YourRequestMessagePack() { }

            public YourRequestMessagePack(int someField)
            {
                Tag = ProtocolTag;
                SomeField = someField;
            }
        }

        [MessagePackObject]
        public class YourResponseMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public string ResultData { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public YourResponseMessagePack() { }

            public YourResponseMessagePack(string resultData)
            {
                Tag = ProtocolTag;
                ResultData = resultData;
            }
        }

        #endregion
    }
}
```

### Step 2: PacketResponseCreatorに登録

`moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs` のコンストラクタに追加:

```csharp
_packetResponseDictionary.Add(YourProtocol.ProtocolTag, new YourProtocol(serviceProvider));
```

### Step 2.5: クライアント側 VanillaApi にメソッドを追加（**1プロトコル = 1メソッド**）

`moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs` にプロトコル送信用メソッドを **1個だけ** 追加する。

**原則: 1プロトコル = 1 VanillaApi メソッド**。1つのプロトコルに対して複数のラッパーメソッド（`GetXxx` / `SetXxx` / `UpdateXxx` 等）を作ってはいけない。プロトコルが複数の Operation を持つ場合は、`Request` オブジェクトを呼び出し側で構築して渡す形にする。

```csharp
// 良い: 1メソッドだけ。呼び出し側が Request を構築する
public async UniTask<YourProtocol.YourResponseMessagePack> SendYourRequest(
    YourProtocol.YourRequestMessagePack request, CancellationToken ct)
{
    return await _packetExchangeManager.GetPacketResponse<YourProtocol.YourResponseMessagePack>(request, ct);
}

// 単純なプロトコルなら、全プロパティを引数に取って内部で Request を組む形も可
public async UniTask<YourProtocol.YourResponseMessagePack> SendYourRequest(
    Vector3Int position, int someField, CancellationToken ct)
{
    var request = new YourProtocol.YourRequestMessagePack(position, someField);
    return await _packetExchangeManager.GetPacketResponse<YourProtocol.YourResponseMessagePack>(request, ct);
}
```

**禁止例**: 1プロトコルに複数の Operation を持たせて、operation ごとに VanillaApi メソッドを生やす。これは「1プロトコル = N メソッド」になりラッパーが肥大化する。

```csharp
// 禁止: 1プロトコル (FilterSplitterStateProtocol) に対し 3 メソッド生やしている
public async UniTask<...> GetFilterSplitterState(...) { ... }
public async UniTask<...> SetFilterSplitterMode(...) { ... }
public async UniTask<...> SetFilterSplitterItem(...) { ... }
```

このルールにより、プロトコルの追加コストが「1ファイル + 1 PacketResponseCreator 登録行 + 1 VanillaApi メソッド」に固定され、後続の Operation 追加が API 表面に波及しない。

### Step 3: テストを作成

`/creating-server-tests` スキルを使用してテストを作成。配置先: `Tests/CombinedTest/Server/PacketTest/`

### Step 4: コンパイル確認

MCPツールまたは`unity-test.sh`でコンパイルを確認。

---

## Event型の作成手順

### Step 1: イベントパケットクラスを作成

`moorestech_server/Assets/Scripts/Server.Event/EventReceive/` に新規ファイルを作成。

```csharp
using System;
using Game.Context;
using MessagePack;
using Server.Event;

namespace Server.Event.EventReceive
{
    public class YourEventPacket
    {
        public const string EventTag = "va:event:yourEvent";
        private readonly EventProtocolProvider _eventProtocolProvider;

        public YourEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            _eventProtocolProvider = eventProtocolProvider;
            // ゲームイベントを購読（UniRx .Subscribe）
            ServerContext.SomeEvent.OnSomething.Subscribe(OnSomething);
        }

        private void OnSomething(SomeEventData eventData)
        {
            var messagePack = new YourEventMessagePack(eventData.Value);
            var payload = MessagePackSerializer.Serialize(messagePack);

            // 全プレイヤー: AddBroadcastEvent / 特定プレイヤー: AddEvent(playerId, ...)
            _eventProtocolProvider.AddBroadcastEvent(EventTag, payload);
        }

        #region MessagePack

        [MessagePackObject]
        public class YourEventMessagePack
        {
            // EventのMessagePackはProtocolMessagePackBaseを継承しない。Key(0)から開始
            [Key(0)] public string Value { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public YourEventMessagePack() { }

            public YourEventMessagePack(string value)
            {
                Value = value;
            }
        }

        #endregion
    }
}
```

### Step 2: イベントパケットを初期化

イベントを発火する責任を持つシステムのコンストラクタでインスタンス化し、フィールドに保持する（GC防止）。

例: ブロック配置イベント → `BlockUpdateSystem`で初期化、レールノード作成イベント → `RailGraphDatastore`関連で初期化。

### Step 3: テストを作成

`/creating-server-tests` スキルを使用。配置先: `Tests/CombinedTest/Server/PacketTest/Event/`

### Step 4: コンパイル確認

MCPツールまたは`unity-test.sh`でコンパイルを確認。

---

## 注意事項

- タグの一意性を必ず確認する（既存タグと重複しないこと）
- MessagePackのKey番号: Request/ResponseはKey(2)から、EventデータはKey(0)から
- `[Obsolete]`付き引数なしコンストラクタは省略不可
- イベント購読はUniRxの`.Subscribe()`を使用（`/csharp-event-pattern`参照）
- コードのコメントは日本語・英語の2行セット

## Request/Response メッセージ設計原則

### enum を int に変換しない
MessagePack は enum 型をそのままシリアライズできる。`int` への変換は型安全性を捨てているだけで、ワイヤ表現も同じバイト列。プロパティ型は enum をそのまま使う。

```csharp
// 良い
[Key(3)] public FilterSplitterOperation Operation { get; set; }
[Key(6)] public FilterSplitterMode Mode { get; set; }

// 禁止: int に変換して受け渡しすると、受け側で `(EnumType)request.Operation` の cast が必要になる
[Key(3)] public int Operation { get; set; }
```

### ItemId / BlockId 等の UnitGenerator 型はそのまま使う
`Core.Master` の `ItemId` `BlockId` 等は UnitGenerator で `MessagePackFormatter` を生成済み。Guid 文字列に変換する必要は一切ない。クライアント・サーバー間で master が同期している前提で `ItemId` を直接送る。

```csharp
// 良い
[Key(7)] public ItemId ItemId { get; set; }

// 禁止: わざわざ Guid 文字列にして受け渡す（master 解決が往復する）
[Key(7)] public string ItemGuidStr { get; set; }
```

### Operation で必要プロパティが変わる場合は static factory に分ける
1 つの Request クラスで複数 Operation を扱い、Operation ごとに必要なフィールドが異なる場合、**呼び出し元が「使わないフィールドに何を入れるか」を悩まされる**設計は禁止。public コンストラクタは禁止し、private コンストラクタ + Operation ごとの `static CreateXxxRequest(...)` 形にする。`RailConnectionEditRequest` がこのパターン。

```csharp
[MessagePackObject]
public class FilterSplitterStateRequest : ProtocolMessagePackBase
{
    [Key(2)] public Vector3IntMessagePack Position { get; set; }
    [Key(3)] public FilterSplitterOperation Operation { get; set; }
    [Key(4)] public int DirectionIndex { get; set; }
    [Key(5)] public int SlotIndex { get; set; }
    [Key(6)] public FilterSplitterMode Mode { get; set; }
    [Key(7)] public ItemId ItemId { get; set; }

    [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
    public FilterSplitterStateRequest() { Tag = ProtocolTag; }

    // private コンストラクタ — 外部は static factory 経由でのみ生成する
    // Private constructor; callers must use the static factories below
    private FilterSplitterStateRequest(Vector3Int position, FilterSplitterOperation operation, int directionIndex, int slotIndex, FilterSplitterMode mode, ItemId itemId) { ... }

    public static FilterSplitterStateRequest CreateGetRequest(Vector3Int position)
        => new(position, FilterSplitterOperation.Get, 0, 0, FilterSplitterMode.Default, ItemMaster.EmptyItemId);

    public static FilterSplitterStateRequest CreateSetModeRequest(Vector3Int position, int directionIndex, FilterSplitterMode mode)
        => new(position, FilterSplitterOperation.SetMode, directionIndex, 0, mode, ItemMaster.EmptyItemId);

    public static FilterSplitterStateRequest CreateSetFilterItemRequest(Vector3Int position, int directionIndex, int slotIndex, ItemId itemId)
        => new(position, FilterSplitterOperation.SetFilterItem, directionIndex, slotIndex, FilterSplitterMode.Default, itemId);
}
```

呼び出し側は `CreateXxxRequest` を見るだけで「この Operation には何が必要か」が型で分かる。public コンストラクタを生やすと「全 Operation のフィールドを全部書く」必要が出てきて、その負担が VanillaApi のラッパーに波及する（→「1プロトコル = N メソッド」原則違反の温床）。

