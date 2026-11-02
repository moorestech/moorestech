using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Newtonsoft.Json;

namespace Game.Block.Blocks.PowerGenerator
{
    public class VanillaElectricGeneratorSaveJsonObject
    {
        [JsonProperty("currentFuelItemGuid")]
        public string CurrentFuelItemGuidStr;
        [JsonIgnore]
        public Guid? CurrentFuelItemGuid => Guid.TryParse(CurrentFuelItemGuidStr, out var guid) ? guid : null;

        [JsonProperty("currentFuelFluidGuid")]
        public string CurrentFuelFluidGuidStr;
        [JsonIgnore]
        public Guid? CurrentFuelFluidGuid => Guid.TryParse(CurrentFuelFluidGuidStr, out var guid) ? guid : null;

        [JsonProperty("inventory")]
        public List<ItemStackSaveJsonObject> Items;

        // 秒数として保存（tick数の変動に対応）
        // Save as seconds (to handle tick rate changes)
        [JsonProperty("remainingFuelSeconds")]
        public double RemainingFuelSeconds;

        [JsonProperty("activeFuelType")]
        public string ActiveFuelType;

        [JsonProperty("fluidTank")]
        public VanillaElectricGeneratorFluidSaveJsonObject FluidTank;
    }

    public class VanillaElectricGeneratorFluidSaveJsonObject
    {
        [JsonProperty("fluidGuid")]
        public string FluidGuidStr;
        [JsonIgnore]
        public Guid? FluidGuid => Guid.TryParse(FluidGuidStr, out var guid) ? guid : null;

        [JsonProperty("amount")]
        public double Amount;
    }
}
