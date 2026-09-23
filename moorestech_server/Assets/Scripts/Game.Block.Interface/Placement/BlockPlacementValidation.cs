using System.Collections.Generic;
using Core.Master;
namespace Game.Block.Interface.Placement
{
    public interface IBlockPlacementValidator
    {
        string Validate(BlockId blockId, BlockDirection direction);
    }
    public sealed class BlockPlacementValidation
    {
        private readonly IEnumerable<IBlockPlacementValidator> validators;
        public BlockPlacementValidation(IEnumerable<IBlockPlacementValidator> validators) => this.validators = validators;
        public bool TryValidate(BlockId blockId, BlockDirection direction, out string reason)
        {
            foreach (var validator in validators)
            {
                reason = validator.Validate(blockId, direction);
                if (reason != null) return false;
            }
            reason = null;
            return true;
        }
    }
}
