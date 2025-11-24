using MessagePack;
using Server.Util.MessagePack;
using UnityEngine;

namespace Server.Event.EventReceive
{
    public class ChainConnectionEventPacket
    {
        public const string Tag = "va:event:chainConnection";
        private readonly EventProtocolProvider _eventProtocolProvider;

        public ChainConnectionEventPacket(EventProtocolProvider eventProtocolProvider)
        {
            // ブロードキャスト用のプロバイダーを受け取る
            // Receive broadcast provider
            _eventProtocolProvider = eventProtocolProvider;
        }

        public void PublishConnection(Vector3Int posA, Vector3Int posB)
        {
            // 接続イベントを生成して配信する
            // Publish connection event
            var payload = MessagePackSerializer.Serialize(new ChainConnectionEventMessagePack(posA, posB, true));
            _eventProtocolProvider.AddBroadcastEvent(Tag, payload);
        }

        public void PublishDisconnection(Vector3Int posA, Vector3Int posB)
        {
            // 切断イベントを生成して配信する
            // Publish disconnection event
            var payload = MessagePackSerializer.Serialize(new ChainConnectionEventMessagePack(posA, posB, false));
            _eventProtocolProvider.AddBroadcastEvent(Tag, payload);
        }
    }

    [MessagePackObject]
    public class ChainConnectionEventMessagePack
    {
        [Key(0)] public Vector3IntMessagePack PosA { get; set; }
        [Key(1)] public Vector3IntMessagePack PosB { get; set; }
        [Key(2)] public bool IsConnected { get; set; }

        [IgnoreMember] public bool IsDisconnected => !IsConnected;

        [System.Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public ChainConnectionEventMessagePack() { }

        public ChainConnectionEventMessagePack(Vector3Int posA, Vector3Int posB, bool isConnected)
        {
            // イベントに含める座標と状態を設定する
            // Set positions and state for event
            PosA = new Vector3IntMessagePack(posA);
            PosB = new Vector3IntMessagePack(posB);
            IsConnected = isConnected;
        }
    }
}
