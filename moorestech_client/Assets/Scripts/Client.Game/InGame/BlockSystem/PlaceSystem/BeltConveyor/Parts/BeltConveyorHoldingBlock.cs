using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// ビルドメニューの選択ブロックから解決した、設置に使う手持ちブロックとそのファミリー
    /// The holding block used for placement and its family, resolved from the build-menu selection
    /// </summary>
    public class BeltConveyorHoldingBlock
    {
        public readonly BeltConveyorFamily Family;
        public readonly BlockMasterElement BlockMaster;

        // 坂を選択中はその坂の上下向き。直線選択時はHorizontalのまま使われない
        // The slope's vertical direction while a slope is selected; unused Horizontal for a straight selection
        public readonly bool IsSlopeSelected;
        public readonly BlockVerticalDirection SlopeDirection;

        private BeltConveyorHoldingBlock(BeltConveyorFamily family, BlockMasterElement blockMaster, bool isSlopeSelected, BlockVerticalDirection slopeDirection)
        {
            Family = family;
            BlockMaster = blockMaster;
            IsSlopeSelected = isSlopeSelected;
            SlopeDirection = slopeDirection;
        }

        // 坂を選んでいるならその坂を手持ちにし、直線選択時は直線を手持ちにする
        // Hold the selected slope when one is selected; otherwise hold the family's straight block
        public static bool TryResolve(BlockId selectedBlockId, out BeltConveyorHoldingBlock holdingBlock)
        {
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(selectedBlockId, out var family))
            {
                holdingBlock = null;
                return false;
            }

            var isSlopeSelected = family.TryGetSlopeDirection(selectedBlockId, out var slopeDirection);
            var holdingBlockId = isSlopeSelected ? selectedBlockId : family.StraightBlockId;
            holdingBlock = new BeltConveyorHoldingBlock(family, MasterHolder.BlockMaster.GetBlockMaster(holdingBlockId), isSlopeSelected, slopeDirection);
            return true;
        }
    }
}
