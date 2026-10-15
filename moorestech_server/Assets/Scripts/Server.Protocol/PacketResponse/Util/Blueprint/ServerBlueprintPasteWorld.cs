using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlacementTarget;
using Game.UnlockState;
using Game.World.Interface.DataStore;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;

namespace Server.Protocol.PacketResponse.Util.Blueprint
{
    public class ServerBlueprintPasteWorld : IBlueprintPasteWorld
    {
        private readonly HashSet<(Vector3Int position, BlockDirection direction, Vector3Int size)> _terrainSunkBlockFootprints;
        private readonly PlacementTargetCatalog _catalog;
        private readonly IGameUnlockStateDataController _unlockState;
        public bool IsPaymentWaived { get; }

        public ServerBlueprintPasteWorld(PlacementTargetCatalog catalog, IGameUnlockStateDataController unlockState, bool isPaymentWaived, IReadOnlyCollection<BlockPositionInfo> terrainSunkBlockFootprints)
        {
            _terrainSunkBlockFootprints = new HashSet<(Vector3Int, BlockDirection, Vector3Int)>();
            foreach (var footprint in terrainSunkBlockFootprints)
                _terrainSunkBlockFootprints.Add((footprint.OriginalPos, footprint.BlockDirection, footprint.BlockSize));
            _catalog = catalog;
            _unlockState = unlockState;
            IsPaymentWaived = isPaymentWaived;
        }

        // 同じ最小角の別形状を巻き込まないよう足場の3値で照合する
        // Match all three footprint values so another shape at the same origin remains placeable
        public bool IsTerrainSunk(BlockPositionInfo positionInfo) =>
            _terrainSunkBlockFootprints.Contains((positionInfo.OriginalPos, positionInfo.BlockDirection, positionInfo.BlockSize));

        public bool IsOverlapping(BlockPositionInfo positionInfo)
        {
            return ServerContext.WorldBlockDatastore.IsOverlapExistingBlock(positionInfo);
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
            return _catalog.IsConnectToolUnlocked(connectToolGuid, _unlockState);
        }
    }
}
