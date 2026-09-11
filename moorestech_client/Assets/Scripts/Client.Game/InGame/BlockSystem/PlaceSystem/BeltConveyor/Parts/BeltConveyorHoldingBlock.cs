using System;
using Core.Master;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    /// 選択ブロックからファミリー・手持ちブロック・坂勾配を解決する
    /// Resolves the family, held block and slope grade from the selected block
    /// </summary>
    public class BeltConveyorHoldingBlock
    {
        public readonly BeltConveyorFamily Family;
        public readonly BlockId BlockId;
        public readonly BlockMasterElement BlockMaster;

        // null は直線選択（高低差からの自動坂判定）。分岐器選択も null だが RunUp/RunDown が null なので坂は入らない
        // Null means a straight selection (auto slopes from height); splitter selection is also null but RunUp/RunDown are null so no slope is inserted
        public readonly BeltSlopeGrade? SlopeGrade;

        // 直線ドラッグで坂セルに割り当てるブロック。null は分岐器手持ちのときだけで、坂手持ちでも family の坂が入る（一定勾配経路なので読まれない）
        // Blocks assigned to slope cells in a straight drag; null only for a held splitter, while a held slope still carries the family slopes (unread on the constant-grade path)
        public readonly BlockId? RunUpBlockId;
        public readonly BlockId? RunDownBlockId;

        private BeltConveyorHoldingBlock(BeltConveyorFamily family, BlockId blockId, BlockMasterElement blockMaster, BeltSlopeGrade? slopeGrade, BlockId? runUpBlockId, BlockId? runDownBlockId)
        {
            Family = family;
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
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(selectedBlockId, out var family))
                throw new InvalidOperationException($"BeltConveyorHoldingBlock: block belongs to no beltConveyorFamily. BlockId:{selectedBlockId}");
            if (!family.TryGetRole(selectedBlockId, out var role))
                throw new InvalidOperationException($"BeltConveyorHoldingBlock: block has no role in its beltConveyorFamily. BlockId:{selectedBlockId}");

            // 直線だけが代表へ寄る。坂・分岐器は選択そのものを手持ちにしないと別ブロックの設置に化ける
            // Only a straight selection collapses to the representative; a slope or splitter must hold itself or it silently places another block
            var holdingBlockId = role == BeltConveyorRole.Straight ? family.StraightBlockId : selectedBlockId;
            var slopeGrade = role switch
            {
                BeltConveyorRole.Up => BeltSlopeGrade.Up,
                BeltConveyorRole.Down => BeltSlopeGrade.Down,
                _ => (BeltSlopeGrade?)null,
            };

            // 分岐器手持ちは坂を自動挿入しないので坂ブロックを持たせない
            // A held splitter never inserts slopes, so it carries no slope blocks
            var isSplitter = role == BeltConveyorRole.Splitter;
            var runUpBlockId = isSplitter ? null : family.UpBlockId;
            var runDownBlockId = isSplitter ? null : family.DownBlockId;

            return new BeltConveyorHoldingBlock(family, holdingBlockId, MasterHolder.BlockMaster.GetBlockMaster(holdingBlockId), slopeGrade, runUpBlockId, runDownBlockId);
        }
    }
}
