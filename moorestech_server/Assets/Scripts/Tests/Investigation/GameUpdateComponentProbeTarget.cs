using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Tests.Investigation
{
    public class GameUpdateComponentProbeTarget
    {
        public IBlock Block { get; }
        public IUpdatableBlockComponent Component { get; }
        public string ComponentTypeName { get; }
        public string BlockTypeName { get; }

        public GameUpdateComponentProbeTarget(IBlock block, IUpdatableBlockComponent component)
        {
            Block = block;
            Component = component;
            ComponentTypeName = component.GetType().Name;
            BlockTypeName = block.BlockMasterElement.BlockType;
        }
    }
}
