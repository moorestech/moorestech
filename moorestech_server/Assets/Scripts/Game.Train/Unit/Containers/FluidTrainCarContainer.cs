using System;
using Core.Master;
using Game.Fluid;
using MessagePack;

namespace Game.Train.Unit.Containers
{
    [MessagePackObject]
    public class FluidTrainCarContainer : ITrainCarContainer
    {
        [Key(0)] public readonly FluidContainer Container;
        
        [Obsolete]
        public FluidTrainCarContainer() { }

        public FluidTrainCarContainer(FluidContainer container)
        {
            Container = container;
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