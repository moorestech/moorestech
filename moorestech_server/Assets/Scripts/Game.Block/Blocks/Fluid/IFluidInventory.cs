using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Fluid;
using Mooresmaster.Model.FluidInventoryConnectsModule;

namespace Game.Block.Blocks.Fluid
{
    public interface IFluidInventory : IBlockComponent
    {
        FluidContainer FluidContainer { get; }

        /// <summary>
        ///     Insert fluid into this inventory.
        /// </summary>
        /// <param name="stack">Fluid to insert.</param>
        /// <param name="insertFromContainer">Container that is the source of the fluid.</param>
        /// <returns>Remaining fluid that could not be inserted.</returns>
        FluidStack InsertFluid(FluidStack stack, FluidContainer insertFromContainer);

        static BlockConnectorComponent<IFluidInventory> CreateFluidInventoryConnector(FluidInventoryConnects fluidInventoryConnects, BlockPositionInfo blockPositionInfo)
        {
            return new BlockConnectorComponent<IFluidInventory>(
                fluidInventoryConnects.InflowConnects,
                fluidInventoryConnects.OutflowConnects,
                blockPositionInfo
            );
        }
    }
}
