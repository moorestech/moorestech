using System;
using Game.PlayerInventory.Interface;
using Game.PlayerRiding.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse.Handshake;
using Server.Protocol.PacketResponse.Util.Handshake;
using Server.Util.MessagePack;
using static Server.Event.EventReceive.ItemStackLevelUnlockEventPacket;

namespace Server.Protocol.PacketResponse
{
    // 接続へIDを紐づけるため、唯一このプロトコルだけが接続コンテキストを受け取る
    // Only this protocol receives the connection context, because it binds the assigned player id to the connection
    public class InitialHandshakeProtocol
    {
        public const string ProtocolTag = "va:initialHandshake";
        private readonly InitialHandshakeBinding _binding;
        private readonly InitialHandshakeResponseFactory _responseFactory;
        private readonly IPlayerInventoryDataStore _playerInventoryDataStore;

        public InitialHandshakeProtocol(ServiceProvider serviceProvider)
        {
            _binding = new InitialHandshakeBinding(serviceProvider);
            _responseFactory = new InitialHandshakeResponseFactory(serviceProvider);
            _playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            // 接続確定まで身元の登録と初期装備の付与を保留する
            // Defer identity registration and initial equipment until the connection binds
            var data = MessagePackSerializer.Deserialize<RequestInitialHandshakeMessagePack>(payload);
            var rejection = _binding.Bind(data.PlayerIdentity, context, out var playerId);
            if (rejection != HandshakeRejection.None) return ResponseInitialHandshakeMessagePack.Rejected(rejection);

            // 復元済みの持ち物は維持し、初期データを返す
            // Preserve restored inventories and return the initial state
            _playerInventoryDataStore.GrantInitialEquipmentIfNewPlayer(playerId);
            return _responseFactory.CreateResponse(playerId);
        }

        [MessagePackObject]
        public class RequestInitialHandshakeMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public string PlayerIdentity { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public RequestInitialHandshakeMessagePack() { }

            public RequestInitialHandshakeMessagePack(string playerIdentity)
            {
                Tag = ProtocolTag;
                PlayerIdentity = playerIdentity;
            }
        }

        [MessagePackObject]
        public class ResponseInitialHandshakeMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public HandshakeRejection Rejection { get; set; }
            [Key(3)] public HandshakeAcceptedMessagePack Accepted { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseInitialHandshakeMessagePack() { }

            public ResponseInitialHandshakeMessagePack(HandshakeAcceptedMessagePack accepted)
            {
                Tag = ProtocolTag;
                Rejection = HandshakeRejection.None;
                Accepted = accepted;
            }

            private ResponseInitialHandshakeMessagePack(HandshakeRejection rejection)
            {
                Tag = ProtocolTag;
                Rejection = rejection;
            }

            public static ResponseInitialHandshakeMessagePack Rejected(HandshakeRejection rejection)
            {
                return new ResponseInitialHandshakeMessagePack(rejection);
            }
        }
    }
}
