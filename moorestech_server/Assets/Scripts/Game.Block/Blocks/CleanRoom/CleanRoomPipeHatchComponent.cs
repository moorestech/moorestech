using System.Collections.Generic;
using Game.Block.Blocks.Fluid;
using Game.Block.Component;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.CleanRoom
{
    // 壁貫通パイプハッチ（Task 4 で本実装に置き換える空実装スタブ）
    // Wall-piercing pipe hatch (empty stub; replaced by the real implementation in Task 4)
    public class CleanRoomPipeHatchComponent : IBlockComponent
    {
        public CleanRoomPipeHatchComponent(float capacity, BlockConnectorComponent<IFluidInventory> connector) { }
        public CleanRoomPipeHatchComponent(Dictionary<string, string> componentStates, float capacity, BlockConnectorComponent<IFluidInventory> connector) { }

        public bool IsDestroy { get; private set; }
        public void Destroy() { IsDestroy = true; }
    }
}
