using Core.Master;
using Game.Block.Interface.Extension;
namespace Game.Block.Interface.Placement
{
    public sealed class BeltPlacementValidator : IBlockPlacementValidator
    {
        public string Validate(BlockId blockId, BlockDirection direction)
            => BeltConveyorPlaceFamilyUtil.IsPlacementDirectionAllowed(blockId, direction)
                ? null : $"Unsupported belt direction {direction} for {blockId}.";
    }
}
