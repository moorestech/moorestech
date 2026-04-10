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
    }
}