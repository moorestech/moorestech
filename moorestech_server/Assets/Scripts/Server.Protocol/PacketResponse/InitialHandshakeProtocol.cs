using System;
using Game.PlayerInventory.Interface;
using Game.PlayerRiding.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse.Handshake;
using Server.Util.MessagePack;
using static Server.Event.EventReceive.ItemStackLevelUnlockEventPacket;

namespace Server.Protocol.PacketResponse
{
    public class InitialHandshakeProtocol : IPacketResponse
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
            [Key(2)] public Vector3MessagePack PlayerPos { get; set; }
            [Key(3)] public InitialHandshakeRidingStateType RidingStateType { get; set; }
            [Key(4)] public RidableIdentifierMessagePack RidingTarget { get; set; }
            [Key(5)] public int RidingSeatIndex { get; set; }
            [Key(6)] public ItemStackLevelMessagePack[] ItemStackLevels { get; set; }
            [Key(7)] public Guid[] HotbarAssignments { get; set; }
            [Key(8)] public RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack[] RemainingPlacementCounts { get; set; }

            [Key(9)] public int PlayerId { get; set; }
            [Key(10)] public HandshakeRejection Rejection { get; set; }

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseInitialHandshakeMessagePack() { }

            public ResponseInitialHandshakeMessagePack(
                Vector3MessagePack playerPos,
                RidableIdentifierMessagePack ridingTarget,
                int ridingSeatIndex,
                ItemStackLevelMessagePack[] itemStackLevels,
                Guid[] hotbarAssignments,
                RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack[] remainingPlacementCounts,
                int playerId)
            {
                Tag = ProtocolTag;
                PlayerPos = playerPos;
                RidingStateType = ridingTarget == null ? InitialHandshakeRidingStateType.None : InitialHandshakeRidingStateType.Restored;
                RidingTarget = ridingTarget;
                RidingSeatIndex = ridingSeatIndex;
                ItemStackLevels = itemStackLevels;
                HotbarAssignments = hotbarAssignments;
                RemainingPlacementCounts = remainingPlacementCounts;
                PlayerId = playerId;
                Rejection = HandshakeRejection.None;
            }

            public static ResponseInitialHandshakeMessagePack Rejected(HandshakeRejection rejection)
            {
                return new ResponseInitialHandshakeMessagePack(null, null, -1,
                    Array.Empty<ItemStackLevelMessagePack>(), Array.Empty<Guid>(),
                    Array.Empty<RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack>(), 0)
                {
                    Rejection = rejection,
                };
            }

            [IgnoreMember] public bool HasRidingState => RidingStateType == InitialHandshakeRidingStateType.Restored;
        }
    }

    public enum InitialHandshakeRidingStateType : byte
    {
        None,
        Restored,
    }
}
