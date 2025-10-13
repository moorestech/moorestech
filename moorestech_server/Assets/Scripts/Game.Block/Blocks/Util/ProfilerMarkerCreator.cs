using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;
using Unity.Profiling;

namespace Game.Block.Blocks.Util
{
    public class ProfilerMarkerCreator
    {
        public static ProfilerMarker BlockUpdateMarker { get; } = new("BlockUpdate");
        
        public static ProfilerMarker CreateUpdateMarker(BlockMasterElement blockMasterElement)
        {
            return 
#if ENABLE_PROFILER
                new ProfilerMarker(blockMasterElement.Name);
#else
                default;
#endif
        }
        
        public static ProfilerMarker CreateComponentUpdateMarker(BlockMasterElement blockMasterElement, IUpdatableBlockComponent component)
        {
#if ENABLE_PROFILER
            return new ProfilerMarker($"{blockMasterElement.Name}_{component.GetType().Name}");
#else
            return default;
#endif
        }
    }
}