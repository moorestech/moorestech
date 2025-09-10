using System.Collections.Generic;
using Game.Block.Blocks.Fluid;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;

namespace Game.Block.Blocks.Gear
{
    /// <summary>
    ///     GearPumpの流体インベントリコンポーネント
    ///     内部タンクから流体を提供する
    /// </summary>
    public class GearPumpFluidInventoryComponent : IFluidInventory, IBlockComponent
    {
        private readonly FluidContainer _fluidContainer;
        
        public GearPumpFluidInventoryComponent(FluidContainer fluidContainer)
        {
            _fluidContainer = fluidContainer;
        }
        
        public FluidStack AddLiquid(FluidStack fluidStack, FluidContainer source)
        {
            // ポンプは流体を受け取らない（出力のみ）
            return fluidStack;
        }
        
        public List<FluidStack> GetFluidInventory()
        {
            var fluidStacks = new List<FluidStack>();
            if (_fluidContainer.Amount > 0)
            {
                fluidStacks.Add(new FluidStack(_fluidContainer.Amount, _fluidContainer.FluidId));
            }
            return fluidStacks;
        }
        
        public bool IsDestroy { get; private set; }
        
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}