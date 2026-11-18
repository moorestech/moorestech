using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Core.Master;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    ///     選択ブロックから解決した手持ちファミリー。経路が各セルの種別を決める
    ///     The held family resolved from the build-menu selection; the route decides each cell's kind
    /// </summary>
    public class BeltConveyorHoldingBlock
    {
        public readonly BeltConveyorFamily Family;
        public readonly BlockId BlockId;
        public readonly BlockMasterElement BlockMaster;

        private BeltConveyorHoldingBlock(BeltConveyorFamily family, BlockMasterElement blockMaster)
        {
            Family = family;
            BlockId = family.StraightBlockId;
            BlockMaster = blockMaster;
        }

        // 上り・下りが両方あるファミリーだけ坂を使う
        // Only a family with both up and down slopes may use slopes
        public bool AllowsSlopes => Family.UpBlockId.HasValue && Family.DownBlockId.HasValue;

        // 経路設置は分岐器でない直線だけ（ADR 0103）
        // Route placement takes only straight, non-splitter blocks (ADR 0103)
        public static bool IsRoutePlacementBlock(BlockId blockId)
        {
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(blockId, out var family)) return false;
            if (family.StraightBlockId != blockId) return false;
            return !BeltSplitterCounterpart.IsSplitter(MasterHolder.BlockMaster.GetBlockMaster(blockId).BlockParam);
        }

        public static BeltConveyorHoldingBlock Resolve(BlockId selectedBlockId)
        {
            // ベルト以外はPlaceSystemSelectorが振り分けないため、ここへ来る時点で契約違反
            // PlaceSystemSelector never routes non-belt blocks here, so reaching this is a contract violation
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(selectedBlockId, out var family))
                throw new InvalidOperationException($"BeltConveyorHoldingBlock: block belongs to no beltConveyorFamily. BlockId:{selectedBlockId}");

            var master = MasterHolder.BlockMaster.GetBlockMaster(family.StraightBlockId);
            return new BeltConveyorHoldingBlock(family, master);
        }

        public bool IsSameFamily(BlockId blockId)
        {
            return BeltConveyorPlaceFamilyUtil.TryGetFamily(blockId, out var family) && family.StraightBlockId == Family.StraightBlockId;
        }

        // 経路セルの種別をファミリーのブロックへ写す。坂は許可済みのファミリーでしか生成されない
        // Maps a route cell kind to the family's block; slopes are only generated for families that allow them
        public BlockId ResolveBlockId(BeltRouteCellKind kind)
        {
            return kind switch
            {
                BeltRouteCellKind.Straight => Family.StraightBlockId,
                BeltRouteCellKind.Up => Family.UpBlockId.Value,
                BeltRouteCellKind.Down => Family.DownBlockId.Value,
                _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
            };
        }
    }
}
