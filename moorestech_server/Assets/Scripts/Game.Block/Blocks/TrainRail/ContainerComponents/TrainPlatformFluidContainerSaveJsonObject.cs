using Game.Fluid;
using Newtonsoft.Json;

namespace Game.Block.Blocks.TrainRail.ContainerComponents
{
    public class TrainPlatformFluidContainerSaveJsonObject
    {
        [JsonProperty("fluid")]
        public FluidContainerSaveJsonObject Fluid;
    }
}
