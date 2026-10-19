using System;
using Core.Master;
using Game.Fluid;
using Mooresmaster.Model.TrainModule;
using Newtonsoft.Json;
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
            var saveData = JsonConvert.DeserializeObject<FluidContainerSaveJsonObject>(saveState);
            return new FluidTrainCarContainer(saveData.ToFluidContainer(master.FluidCapacity));
        }

        public (string containerType, string saveState) GetSaveState()
        {
            return (TrainCarMasterElement.DefaultContainerTypeConst.Fluid, JsonConvert.SerializeObject(new FluidContainerSaveJsonObject(Container)));
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

        private TrainCar _attachedCar;

        public void OnAttachedToCar(TrainCar trainCar)
        {
            _attachedCar = trainCar;
        }

        public void OnDetachedFromCar()
        {
            _attachedCar = null;
        }

        public void NotifyFluidChanged()
        {
            if (_attachedCar == null) return; // silent-ok: 貨車に載っていない容器には開いている貨車画面が無い / 液体PF側は自分の OnChangeBlockState で通知済み
            _attachedCar.NotifyContentRefresh();
        }
    }
}
