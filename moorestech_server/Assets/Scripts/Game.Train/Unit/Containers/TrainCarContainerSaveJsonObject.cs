using Mooresmaster.Model.TrainModule;
using Newtonsoft.Json;
using static Mooresmaster.Model.TrainModule.TrainCarMasterElement;

namespace Game.Train.Unit.Containers
{
    // 列車コンテナの保存オブジェクト。復元用のキーと状態だけもつ
    // Train car container save object. Only has keys and states for restoration.
    public class TrainCarContainerSaveJsonObject
    {
        [JsonProperty("containerType")]
        public string ContainerType;
        
        [JsonProperty("containerState")]
        public string ContainerState;

        public static TrainCarContainerSaveJsonObject FromContainer(ITrainCarContainer container)
        {
            var (type,state) = container.GetSaveState();
            
            return new TrainCarContainerSaveJsonObject
            {
                ContainerType = type,
                ContainerState = state,
            };
        }

        public ITrainCarContainer ToContainer(TrainCarMasterElement master)
        {
            return ContainerType switch
            {
                DefaultContainerTypeConst.Item => ItemTrainCarContainer.Load(ContainerState, master),
                DefaultContainerTypeConst.Fluid => FluidTrainCarContainer.Load(ContainerState, master),
                _ => null,
            };
        }
    }
}
