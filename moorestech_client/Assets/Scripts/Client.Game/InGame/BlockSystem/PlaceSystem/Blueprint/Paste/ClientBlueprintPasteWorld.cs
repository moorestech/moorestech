using System;
using Client.Game.InGame.Block;
using Client.Game.InGame.BlockSystem.PlaceSystem.Targets;
using Common.Debug;
using Game.Block.Interface;
using Server.Protocol.PacketResponse.Util.Blueprint.Planning;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Blueprint.Paste
{
    public class ClientBlueprintPasteWorld : IBlueprintPasteWorld
    {
        private readonly BlockGameObjectDataStore _blocks;
        private readonly PlacementTargetResolver _targets;

        public ClientBlueprintPasteWorld(BlockGameObjectDataStore blocks, PlacementTargetResolver targets)
        {
            _blocks = blocks;
            _targets = targets;
        }

        // 占有と解放は既存のクライアント情報へ委譲する
        // Delegate occupancy and unlock checks to existing client state
        public bool IsOverlapping(BlockPositionInfo positionInfo) => _blocks.IsOverlapPositionInfo(positionInfo);
        public bool IsBlockUnlocked(Guid blockGuid) => _targets.IsBlockUnlocked(blockGuid);
        public bool IsConnectToolUnlocked(Guid connectToolGuid) => _targets.IsConnectToolUnlocked(connectToolGuid);
        public bool IsPaymentWaived => DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement);
    }
}
