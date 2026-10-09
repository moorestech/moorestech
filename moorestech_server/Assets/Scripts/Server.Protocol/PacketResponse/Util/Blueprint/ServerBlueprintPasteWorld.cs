using System;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlacementTarget;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;
using Server.Protocol.PacketResponse.Util.ElectricWire.Connection;

namespace Server.Protocol.PacketResponse.Util.Blueprint
{
    public class ServerBlueprintPasteWorld : IBlueprintPasteWorld
    {
        private readonly PlacementTargetCatalog _catalog;
        private readonly IGameUnlockStateDataController _unlockState;
        public bool IsPaymentWaived { get; }

        public ServerBlueprintPasteWorld(PlacementTargetCatalog catalog, IGameUnlockStateDataController unlockState, bool isPaymentWaived)
        {
            _catalog = catalog;
            _unlockState = unlockState;
            IsPaymentWaived = isPaymentWaived;
        }

        public bool IsOverlapping(BlockPositionInfo positionInfo)
        {
            foreach (var position in positionInfo.EnumeratePositions())
            {
                if (ServerContext.WorldBlockDatastore.Exists(position)) return true;
            }
            return false;
        }

        // 通常設置と同じ無料設置時の解放規則を使う
        // Use the same free-placement unlock rule as ordinary placement
        public bool IsBlockUnlocked(Guid blockGuid)
        {
            return _catalog.IsBlockUnlocked(blockGuid, _unlockState, IsPaymentWaived);
        }

        // 保存配線の線種は無料設置でも解放が必要
        // Saved line tools must be unlocked even with free placement
        public bool IsConnectToolUnlocked(Guid connectToolGuid)
        {
            return ElectricWireSystemUtil.IsConnectToolUnlocked(connectToolGuid);
        }
    }
}
