using System;
using Core.Master;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// 選択ブロックからファミリー・ロール・手持ちブロック・坂勾配を解決する
    /// Resolves the family, role, held block and slope grade from the selected block
    /// </summary>
    public class BeltConveyorHoldingBlock
    {
        public readonly BeltConveyorFamily Family;
        public readonly BeltConveyorRole Role;
        public readonly BlockId BlockId;
        public readonly BlockMasterElement BlockMaster;

        // null は直線選択（高低差からの自動坂判定）。分岐器選択も null だが RunUp/RunDown が null なので坂は入らない
        // Null means a straight selection (auto slopes from height); splitter selection is also null but RunUp/RunDown are null so no slope is inserted
        public readonly BeltSlopeGrade? SlopeGrade;

        // 直線ドラッグで坂セルに割り当てるブロック。分岐器手持ちでは坂を持たない
        // Blocks assigned to slope cells in a straight drag; a held splitter has no slopes
        public readonly BlockId? RunUpBlockId;
        public readonly BlockId? RunDownBlockId;

        private BeltConveyorHoldingBlock(BeltConveyorFamily family, BeltConveyorRole role, BlockId blockId, BlockMasterElement blockMaster, BeltSlopeGrade? slopeGrade, BlockId? runUpBlockId, BlockId? runDownBlockId)
        {
            Family = family;
            Role = role;
            BlockId = blockId;
            BlockMaster = blockMaster;
            SlopeGrade = slopeGrade;
            RunUpBlockId = runUpBlockId;
            RunDownBlockId = runDownBlockId;
        }

        public static BeltConveyorHoldingBlock Resolve(BlockId selectedBlockId)
        {
            // ベルト設置系はファミリー所属が前提。無所属はマスタ不整合なので例外で止める
            // Belt placement assumes family membership; a non-member is a master inconsistency, so stop with an exception
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(selectedBlockId, out var family) || !family.TryGetRole(selectedBlockId, out var role))
                throw new InvalidOperationException($"BeltConveyorHoldingBlock: block belongs to no beltConveyorFamily. BlockId:{selectedBlockId}");

            // 直線だけが代表へ寄る。坂・分岐器は選択そのものを手持ちにしないと別ブロックの設置に化ける
            // Only a straight selection collapses to the representative; a slope or splitter must hold itself or it silently places another block
            var holdingBlockId = role == BeltConveyorRole.Straight ? family.StraightBlockId : selectedBlockId;
            var isSplitter = role == BeltConveyorRole.Splitter;
            return new BeltConveyorHoldingBlock(family, role, holdingBlockId, MasterHolder.BlockMaster.GetBlockMaster(holdingBlockId), ResolveSlopeGrade(), isSplitter ? null : family.UpBlockId, isSplitter ? null : family.DownBlockId);

            #region Internal

            BeltSlopeGrade? ResolveSlopeGrade()
            {
                if (role == BeltConveyorRole.Up) return BeltSlopeGrade.Up;
                if (role == BeltConveyorRole.Down) return BeltSlopeGrade.Down;
                return null;
            }

            #endregion
        }
    }
}
