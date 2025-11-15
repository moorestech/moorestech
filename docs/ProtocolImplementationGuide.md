# moorestech サーバープロトコル実装ガイド

このドキュメントでは、moorestechサーバーにおける2種類のプロトコルの実装方法について説明します。

## プロトコルの種類

moorestechサーバーには以下の2種類のプロトコルがあります：

1. **通常のレスポンスプロトコル（Request-Response型）**
   - クライアントからのリクエストに対して即座にレスポンスを返す
   - 同期的な通信パターン

2. **イベントプロトコル（Server-Initiated型）**
   - サーバー側のゲーム状態の変化を非同期的にクライアントに通知
   - プッシュ型の通信パターン

## 1. 通常のレスポンスプロトコルの作成方法

### 手順

#### 1.1 プロトコルクラスの作成

`Server.Protocol.PacketResponse`名前空間に新しいクラスを作成し、`IPacketResponse`インターフェースを実装します。

```csharp
using System.Collections.Generic;
using System.Linq;
using MessagePack;
using Server.Util.MessagePack;

namespace Server.Protocol.PacketResponse
{
    public class YourProtocol : IPacketResponse
    {
        // プロトコルタグは一意である必要があります
        public const string Tag = "va:your_protocol";
        
        public YourProtocol()
        {
            // 必要な依存関係を注入
        }
        
        public ProtocolMessagePackBase GetResponse(List<byte> payload)
        {
            // 1. リクエストをデシリアライズ
            var request = MessagePackSerializer.Deserialize<YourRequestMessagePack>(payload.ToArray());
            
            // 2. ビジネスロジックを実行
            var result = ProcessRequest(request);
            
            // 3. レスポンスを返す
            return new YourResponseMessagePack(result);
        }
        
        #region MessagePack Classes
        
        [MessagePackObject]
        public class YourRequestMessagePack : ProtocolMessagePackBase
        {
            [Key(0)] public string Tag { get; set; }
            [Key(1)] public YourRequestData Data { get; set; }
            
            // MessagePackのデシリアライズ用の引数なしコンストラクタ（必須）
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public YourRequestMessagePack()
            {
            }
            
            public YourRequestMessagePack(YourRequestData data)
            {
                Tag = YourProtocol.Tag;
                Data = data;
            }
        }
        
        [MessagePackObject]
        public class YourResponseMessagePack : ProtocolMessagePackBase
        {
            [Key(0)] public string Tag { get; set; }
            [Key(1)] public YourResponseData Data { get; set; }
            
            // MessagePackのデシリアライズ用の引数なしコンストラクタ（必須）
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public YourResponseMessagePack()
            {
            }
            
            public YourResponseMessagePack(YourResponseData data)
            {
                Tag = YourProtocol.Tag;
                Data = data;
            }
        }
        
        #endregion
    }
}
```

#### 1.2 PacketResponseCreatorへの登録

`PacketResponseCreator.cs`のコンストラクタにプロトコルを登録します：

```csharp
public PacketResponseCreator()
{
    // 既存のプロトコル...
    
    // 新しいプロトコルを追加
    _packetResponseDictionary.Add(YourProtocol.Tag, new YourProtocol());
}
```

#### 1.3 テストの作成

`Tests/CombinedTest/Server/PacketTest`にテストクラスを作成：

```csharp
[TestFixture]
public class YourProtocolTest
{
    [Test]
    public void YourProtocolTest_正常系()
    {
        // Arrange
        var protocol = new YourProtocol();
        var request = new YourRequestMessagePack(new YourRequestData());
        var payload = MessagePackSerializer.Serialize(request).ToList();
        
        // Act
        var response = protocol.GetResponse(payload) as YourResponseMessagePack;
        
        // Assert
        Assert.IsNotNull(response);
        // 追加のアサーション
    }
}
```

### ベストプラクティス

- プロトコルタグは`"va:"`プレフィックスを使用する
- リクエスト/レスポンスクラスは`MessagePackObject`属性を付ける
- 各フィールドには`Key`属性を付け、連番を使用する
- エラーハンドリングを適切に実装する

## 2. イベントプロトコルの作成方法

### 手順

#### 2.1 イベントパケットクラスの作成

`Server.Event.PacketEvent`名前空間に新しいクラスを作成：

```csharp
using System;
using Game.Context;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;

namespace Server.Event.PacketEvent
{
    public class YourEventPacket
    {
        public const string EventTag = "va:event:your_event";
        
        public YourEventPacket()
        {
            // ゲームイベントをサブスクライブ
            ServerContext.Event.Subscribe<YourGameEvent>(OnYourGameEvent);
        }
        
        private void OnYourGameEvent(YourGameEvent gameEvent)
        {
            // イベントデータを作成
            var eventData = new YourEventMessagePack(
                EventTag,
                new YourEventData(gameEvent.SomeData)
            );
            
            // イベントをキューに追加
            EventProtocolProvider.EventPackets.Enqueue(eventData);
        }
        
        #region MessagePack Classes
        
        [MessagePackObject]
        public class YourEventMessagePack : ProtocolMessagePackBase
        {
            [Key(0)] public string Tag { get; set; }
            [Key(1)] public YourEventData Data { get; set; }
            
            // MessagePackのデシリアライズ用の引数なしコンストラクタ（必須）
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public YourEventMessagePack()
            {
            }
            
            public YourEventMessagePack(string tag, YourEventData data)
            {
                Tag = tag;
                Data = data;
            }
        }
        
        [MessagePackObject]
        public class YourEventData
        {
            [Key(0)] public string SomeProperty { get; set; }
            
            // MessagePackのデシリアライズ用の引数なしコンストラクタ（必須）
            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public YourEventData()
            {
            }
            
            public YourEventData(string someProperty)
            {
                SomeProperty = someProperty;
            }
        }
        
        #endregion
    }
}
```

#### 2.2 イベントパケットの初期化

イベントパケットは、そのイベントが発生する可能性のあるゲームシステムの初期化時にインスタンス化します。例えば：

- ブロック関連のイベント → `BlockSystem`や`BlockUpdateSystem`の初期化時
- プレイヤー関連のイベント → `PlayerSystem`の初期化時
- アイテム関連のイベント → `ItemSystem`の初期化時

実装例：

```csharp
// 例: ブロック破壊イベントの場合
public class BlockUpdateSystem
{
    private readonly ChangeBlockStateEventPacket _changeBlockStateEventPacket;
    
    public BlockUpdateSystem()
    {
        // このシステムがブロック状態変更イベントを発火する可能性があるため、
        // ここでイベントパケットを初期化
        _changeBlockStateEventPacket = new ChangeBlockStateEventPacket();
    }
}
```

重要な点：
- イベントパケットは、そのイベントを発火する責任を持つシステムで初期化する
- 一度初期化すれば、`ServerContext.Event`を通じてどこからでもイベントを発火できる
- イベントパケットのインスタンスは保持する必要がある（ガベージコレクションを防ぐため）

#### 2.3 テストの作成

`Tests/CombinedTest/Server/PacketTest/Event`にテストクラスを作成：

```csharp
[TestFixture]
public class YourEventPacketTest
{
    [Test]
    public void YourEventPacket_イベント発生時にパケットがキューに追加される()
    {
        // Arrange
        var eventPacket = new YourEventPacket();
        EventProtocolProvider.EventPackets.Clear();
        
        // Act
        ServerContext.Event.Invoke(new YourGameEvent("test data"));
        
        // Assert
        Assert.AreEqual(1, EventProtocolProvider.EventPackets.Count);
        var packet = EventProtocolProvider.EventPackets.Dequeue() as YourEventMessagePack;
        Assert.IsNotNull(packet);
        Assert.AreEqual(YourEventPacket.EventTag, packet.Tag);
    }
}
```

### ベストプラクティス

- イベントタグは`"va:event:"`プレフィックスを使用する
- ゲームイベントのサブスクライブは忘れずに行う
- イベントデータは必要最小限にして、パフォーマンスを考慮する
- テストではイベントキューのクリアを忘れない

## プロトコル選択の指針

### 通常のレスポンスプロトコルを使用する場合
- クライアントが明示的に情報を要求する場合
- 現在の状態を取得する場合（例：インベントリの内容、プレイヤー情報）
- 操作の結果を即座に返す必要がある場合

### イベントプロトコルを使用する場合
- サーバー側の状態変化をクライアントに通知する場合
- 複数のクライアントに同じ情報を配信する場合
- リアルタイム性が重要な更新の場合（例：ブロックの破壊、アイテムの生成）

## コンパイルとテスト

プロトコルを実装した後は、必ずCLAUDE.mdに記述されているコマンドでコンパイルとテストを実行してください。

例：
```bash
./unity-test.sh moorestech_server '[test name regex]'
```

これにより、実装が正しく動作することを確認できます。