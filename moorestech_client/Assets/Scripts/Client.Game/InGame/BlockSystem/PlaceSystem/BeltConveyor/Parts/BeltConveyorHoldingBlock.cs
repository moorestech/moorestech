using System;
using Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Route;
using Core.Master;
using Game.Block.Interface.Extension;
using Mooresmaster.Model.BlocksModule;

namespace Client.Game.InGame.BlockSystem.PlaceSystem.BeltConveyor.Parts
{
    /// <summary>
    ///     選択ブロックから解決した手持ちファミリー。坂を選んでいても直線を代表にし、経路が各セルの種別を決める
    ///     The held family resolved from the build-menu selection; a slope selection still holds the straight block, and the route decides each cell's kind
    /// </summary>
    public class BeltConveyorHoldingBlock
    {
        public readonly BeltConveyorFamily Family;
        public readonly BlockId BlockId;
        public readonly BlockMasterElement BlockMaster;

        // ビルドメニューで選んだブロック。持ち替え検知に使う
        // The block chosen in the build menu, used to detect a switch
        public readonly BlockId SelectedBlockId;

        // 分岐器（出力口が2つ以上）のファミリーは水平経路のみで全セルが分岐器になる
        // A splitter family (two or more output ports) routes horizontally only, with every cell a splitter
        public readonly bool IsSplitter;

        private BeltConveyorHoldingBlock(BeltConveyorFamily family, BlockId selectedBlockId, BlockMasterElement blockMaster, bool isSplitter)
        {
            Family = family;
            BlockId = family.StraightBlockId;
            SelectedBlockId = selectedBlockId;
            BlockMaster = blockMaster;
            IsSplitter = isSplitter;
        }

        // 上り・下りが両方あるファミリーだけ坂を使う
        // Only a family with both up and down slopes may use slopes
        public bool AllowsSlopes => Family.UpBlockId.HasValue && Family.DownBlockId.HasValue;

        public static BeltConveyorHoldingBlock Resolve(BlockId selectedBlockId)
        {
            // ベルト以外はPlaceSystemSelectorが振り分けないため、ここへ来る時点で契約違反
            // PlaceSystemSelector never routes non-belt blocks here, so reaching this is a contract violation
            if (!BeltConveyorPlaceFamilyUtil.TryGetFamily(selectedBlockId, out var family))
                throw new InvalidOperationException($"BeltConveyorHoldingBlock: block belongs to no beltConveyorFamily. BlockId:{selectedBlockId}");

            var master = MasterHolder.BlockMaster.GetBlockMaster(family.StraightBlockId);
            return new BeltConveyorHoldingBlock(family, selectedBlockId, master, BeltSplitterCounterpart.IsSplitter(master.BlockParam));
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
