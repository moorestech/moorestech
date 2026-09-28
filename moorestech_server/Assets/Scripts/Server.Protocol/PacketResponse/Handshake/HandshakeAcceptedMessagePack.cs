using System;
using Game.PlayerRiding.Interface;
using MessagePack;
using Server.Event.EventReceive;
using Server.Util.MessagePack;
using static Server.Event.EventReceive.ItemStackLevelUnlockEventPacket;

namespace Server.Protocol.PacketResponse.Handshake
{
    [MessagePackObject]
    public class HandshakeAcceptedMessagePack
    {
        [Key(0)] public int PlayerId { get; set; }
        [Key(1)] public Vector3MessagePack PlayerPos { get; set; }
        [Key(2)] public RidableIdentifierMessagePack RidingTarget { get; set; }
        [Key(3)] public int RidingSeatIndex { get; set; }
        [Key(4)] public ItemStackLevelMessagePack[] ItemStackLevels { get; set; }
        [Key(5)] public Guid[] HotbarAssignments { get; set; }
        [Key(6)] public RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack[] RemainingPlacementCounts { get; set; }

        [IgnoreMember] public bool HasRidingState => RidingTarget != null;
        [IgnoreMember] public InitialHandshakeRidingStateType RidingStateType => HasRidingState ? InitialHandshakeRidingStateType.Restored : InitialHandshakeRidingStateType.None;

        [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
        public HandshakeAcceptedMessagePack() { }

        public HandshakeAcceptedMessagePack(Vector3MessagePack playerPos, RidableIdentifierMessagePack ridingTarget,
            int ridingSeatIndex, ItemStackLevelMessagePack[] itemStackLevels, Guid[] hotbarAssignments,
            RemainingPlacementCountChangedEventPacket.RemainingPlacementCountMessagePack[] remainingPlacementCounts, int playerId)
        {
            PlayerId = playerId;
            PlayerPos = playerPos;
            RidingTarget = ridingTarget;
            RidingSeatIndex = ridingSeatIndex;
            ItemStackLevels = itemStackLevels;
            HotbarAssignments = hotbarAssignments;
            RemainingPlacementCounts = remainingPlacementCounts;
        }
    }
}
