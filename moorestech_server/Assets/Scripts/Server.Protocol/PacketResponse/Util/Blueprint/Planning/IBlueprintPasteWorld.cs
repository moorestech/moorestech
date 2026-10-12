using System;
using Game.Block.Interface;

namespace Server.Protocol.PacketResponse.Util.Blueprint.Planning
{
    public interface IBlueprintPasteWorld
    {
        bool IsOverlapping(BlockPositionInfo positionInfo);
        bool IsBlockUnlocked(Guid blockGuid);
        bool IsConnectToolUnlocked(Guid connectToolGuid);
        bool IsPaymentWaived { get; }
    }
}
