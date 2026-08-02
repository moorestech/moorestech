using System;
using Core.Master;
using Game.Block.Interface;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.Targets
{
    public sealed class BlockPlacementTarget : IPlacementTarget
    {
        public readonly BlockId BlockId;

        // スポイト由来の向き（メニュー選択時はnull）
        // Direction picked by the eyedropper (null when selected from the menu)
        public readonly BlockDirection? PickedDirection;

        // 設置対象IDは揮発BlockIdではなくマスタのBlockGuid
        // The placement target id is the master's BlockGuid, not the volatile BlockId
        public Guid Id => MasterHolder.BlockMaster.GetBlockMaster(BlockId).BlockGuid;
        public PlacementTargetKind Kind => PlacementTargetKind.Block;
        public string DisplayName => MasterHolder.BlockMaster.GetBlockMaster(BlockId).Name;

        public BlockPlacementTarget(BlockId blockId, BlockDirection? pickedDirection)
        {
            BlockId = blockId;
            PickedDirection = pickedDirection;
        }

        public bool Equals(IPlacementTarget other)
        {
            return other is BlockPlacementTarget target && BlockId == target.BlockId && PickedDirection == target.PickedDirection;
        }

        public override bool Equals(object obj) => obj is IPlacementTarget target && Equals(target);
        public override int GetHashCode() => HashCode.Combine(BlockId, PickedDirection);
    }
}
