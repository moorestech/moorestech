using Mooresmaster.Model.TrainModule;
using FluidContainer = Game.Fluid.FluidContainer;

namespace Game.Train.Unit.Containers
{
    internal static class TrainCarContainerFactory
    {
        public static ITrainCarContainer CreateDefault(TrainCarMasterElement master)
        {
            // 初期容器の形はマスタ指定だけで決める
            // Resolve the initial container solely from the master
            return master.DefaultContainerType switch
            {
                "Item" => ItemTrainCarContainer.CreateWithEmptySlots(master.InventorySlots),
                "Fluid" => new FluidTrainCarContainer(new FluidContainer(master.FluidCapacity)),
                _ => null, // silent-ok: None指定は容器を持たない車両 / 在庫要求はContainerNotFoundを返す
            };
        }
    }
}
