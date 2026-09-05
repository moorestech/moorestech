using System;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// 選択ブロックから解決した手持ちブロックとファミリー
    /// Holding block and family resolved from the build-menu selection
    /// </summary>
    public class BeltConveyorHoldingBlock
    {
        public readonly BeltConveyorFamily Family;
        public readonly BlockId BlockId;
        public readonly BlockMasterElement BlockMaster;

        // 坂を選択中だけ値を持つ。nullが直線選択を表す唯一の印
        // Holds a value only while a slope is selected; null is the sole marker of a straight selection
        public readonly BlockVerticalDirection? SlopeDirection;

        private BeltConveyorHoldingBlock(BeltConveyorFamily family, BlockId blockId, BlockMasterElement blockMaster, BlockVerticalDirection? slopeDirection)
        {
            Family = family;
            BlockId = blockId;
            BlockMaster = blockMaster;
            SlopeDirection = slopeDirection;
        }

        // 坂選択時はその坂、直線選択時は直線を手持ちにする
        // Hold the slope when selected; otherwise hold the straight block
        public static BeltConveyorHoldingBlock Resolve(BlockId selectedBlockId)
        {
            // ベルト以外はPlaceSystemSelectorが振り分けないため、ここへ来る時点で契約違反
            // PlaceSystemSelector never routes non-belt blocks here, so reaching this is a contract violation
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(selectedBlockId, out var family))
                throw new InvalidOperationException($"BeltConveyorHoldingBlock: block belongs to no beltConveyorFamily. BlockId:{selectedBlockId}");

            var slopeDirection = family.TryGetSlopeDirection(selectedBlockId, out var direction) ? direction : (BlockVerticalDirection?)null;
            var holdingBlockId = slopeDirection.HasValue ? selectedBlockId : family.StraightBlockId;
            return new BeltConveyorHoldingBlock(family, holdingBlockId, MasterHolder.BlockMaster.GetBlockMaster(holdingBlockId), slopeDirection);
        }
    }
}
