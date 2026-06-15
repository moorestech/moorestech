using System;
using Core.Master;
using Mooresmaster.Model.TrainModule;
using FluidContainer = Game.Fluid.FluidContainer;

namespace Game.Train.Unit.Containers
{
    public class FluidTrainCarContainer : ITrainCarContainer
    {
        public readonly FluidContainer Container;

        public FluidTrainCarContainer(FluidContainer container)
        {
            Container = container;
        }
        
        public static FluidTrainCarContainer Load(string saveState, TrainCarMasterElement master)
        {
            // TODO 
        }

        public int GetWeight()
        {
            return MasterHolder.TrainUnitMaster.Train.FluidContainer.Weight;
        }
        
        public bool IsFull()
        {
            return Math.Abs(Container.Capacity - Container.Amount) < double.Epsilon;
        }
        
        public bool IsEmpty()
        {
            return Container.Amount < double.Epsilon;
        }

        // 液体コンテナは現状通知バインドを持たないためno-op
        // Fluid container has no notification binding today; both hooks are no-op.
        public void OnAttachedToCar(TrainCar trainCar) { }
        public void OnDetachedFromCar() { }
    }
}