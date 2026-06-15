using System;
using Core.Master;
using Newtonsoft.Json;

namespace Game.Fluid
{
    // 液体コンテナの永続化用key-value表現。FluidIdは揮発値なので安定したGUIDで保存する
    // Key-value representation for persisting a fluid container; FluidId is volatile so persist the stable GUID
    public class FluidContainerSaveJsonObject
    {
        [JsonProperty("fluidGuid")]
        public string FluidGuidStr;

        [JsonProperty("amount")]
        public double Amount;

        [JsonIgnore]
        public Guid FluidGuid => string.IsNullOrEmpty(FluidGuidStr) ? Guid.Empty : Guid.Parse(FluidGuidStr);

        // GUIDから実行時FluidIdへ解決する。既存コンテナへ詰め直す用途で使う
        // Resolve the runtime FluidId from the GUID; used when restoring into an existing container
        [JsonIgnore]
        public FluidId FluidId => MasterHolder.FluidMaster.GetFluidId(FluidGuid);

        public FluidContainerSaveJsonObject() { }

        public FluidContainerSaveJsonObject(FluidContainer container)
        {
            FluidGuidStr = MasterHolder.FluidMaster.GetFluidGuid(container.FluidId).ToString();
            Amount = container.Amount;
        }

        public FluidContainer ToFluidContainer(double capacity)
        {
            var container = new FluidContainer(capacity)
                {
                    FluidId = FluidId,
                    Amount = capacity < Amount ? capacity : Amount,
                };
            return container;
        }
    }
}
