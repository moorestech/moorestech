---
name: creating-server-protocol
description: |
  moorestechサーバーのプロトコル（Request-Response型・Event型）を作成するためのガイド。
  Use when:
  1. 新しいサーバープロトコル（IPacketResponse）を実装する時
  2. 新しいイベントパケット（EventPacket）を実装する時
  3. 「プロトコルを作って」「パケットを追加して」と依頼された時
  4. クライアント-サーバー間の通信を新規追加する時
---

# サーバープロトコル作成ガイド

## プロトコル選択

| 条件 | 種類 |
|------|------|
| クライアントが明示的に情報を要求/操作を実行 | **Request-Response型** |
| サーバー側の状態変化をクライアントに通知 | **Event型** |

詳細パターンとコード例: [references/protocol-patterns.md](references/protocol-patterns.md) を参照。

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

        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            var data = MessagePackSerializer.Deserialize<YourRequestMessagePack>(payload.ToArray());
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
