using System;
using Mooresmaster.Model.BlocksModule;

namespace Core.Master.Validator.Block
{
    internal static class BlockParamValidator
    {
        internal static string Validate(Blocks blocks)
        {
            var logs = "";
            foreach (var block in blocks.Data)
            {
                // ElectricGenerator: fuelItems, fuelFluids
                // ElectricGenerator: fuelItems, fuelFluids
                if (block.BlockParam is ElectricGeneratorBlockParam electricGenerator)
                {
                    if (electricGenerator.FuelItems != null)
                    {
                        foreach (var fuelItem in electricGenerator.FuelItems)
                        {
                            // 無効な燃焼時間を点火前に拒否する
                            // Reject invalid burn durations before ignition
                            if (double.IsNaN(fuelItem.Time) || double.IsInfinity(fuelItem.Time) || fuelItem.Time <= 0)
                                logs += $"[BlockMaster] Name:{block.Name} FuelItem.Time must be finite and > 0 (got {fuelItem.Time})\n";
                            var id = MasterHolder.ItemMaster.GetItemIdOrNull(fuelItem.ItemGuid);
                            if (id == null)
                            {
                                logs += $"[BlockMaster] Name:{block.Name} has invalid FuelItem.ItemGuid:{fuelItem.ItemGuid}\n";
                            }
                        }
                    }
                    if (electricGenerator.FuelFluids != null)
                    {
                        foreach (var fuelFluid in electricGenerator.FuelFluids)
                        {
                            // 無効な燃焼時間を点火前に拒否する
                            // Reject invalid burn durations before ignition
                            if (double.IsNaN(fuelFluid.Time) || double.IsInfinity(fuelFluid.Time) || fuelFluid.Time <= 0)
                                logs += $"[BlockMaster] Name:{block.Name} FuelFluid.Time must be finite and > 0 (got {fuelFluid.Time})\n";
                            var id = MasterHolder.FluidMaster.GetFluidIdOrNull(fuelFluid.FluidGuid);
                            if (id == null)
                            {
                                logs += $"[BlockMaster] Name:{block.Name} has invalid FuelFluid.FluidGuid:{fuelFluid.FluidGuid}\n";
                            }
                        }
                    }
                }

                // FuelGearGenerator: gearFuelItems, requiredFluids
                // FuelGearGenerator: gearFuelItems, requiredFluids
                if (block.BlockParam is FuelGearGeneratorBlockParam fuelGearGenerator)
                {
                    if (fuelGearGenerator.GearFuelItems != null)
                    {
                        foreach (var gearFuelItem in fuelGearGenerator.GearFuelItems)
                        {
                            var id = MasterHolder.ItemMaster.GetItemIdOrNull(gearFuelItem.ItemGuid);
                            if (id == null)
                            {
                                logs += $"[BlockMaster] Name:{block.Name} has invalid GearFuelItem.ItemGuid:{gearFuelItem.ItemGuid}\n";
                            }
                        }
                    }
                    foreach (var requiredFluid in fuelGearGenerator.RequiredFluids)
                    {
                        var id = MasterHolder.FluidMaster.GetFluidIdOrNull(requiredFluid.FluidGuid);
                        if (id == null)
                        {
                            logs += $"[BlockMaster] Name:{block.Name} has invalid RequiredFluid.FluidGuid:{requiredFluid.FluidGuid}\n";
                        }
                    }
                }

                // BaseCamp: requiredItems, upgradBlockGuid
                // BaseCamp: requiredItems, upgradBlockGuid
                if (block.BlockParam is BaseCampBlockParam baseCamp)
                {
                    foreach (var requiredItem in baseCamp.RequiredItems)
                    {
                        var id = MasterHolder.ItemMaster.GetItemIdOrNull(requiredItem.ItemGuid);
                        if (id == null)
                        {
                            logs += $"[BlockMaster] Name:{block.Name} has invalid RequiredItem.ItemGuid:{requiredItem.ItemGuid}\n";
                        }
                    }
                    // 空のGUIDはアップグレードなしを意味するためスキップ
                    // Empty GUID means no upgrade, so skip
                    if (baseCamp.UpgradBlockGuid != Guid.Empty)
                    {
                        if (!Array.Exists(blocks.Data, b => b.BlockGuid == baseCamp.UpgradBlockGuid))
                        {
                            logs += $"[BlockMaster] Name:{block.Name} has invalid UpgradBlockGuid:{baseCamp.UpgradBlockGuid}\n";
                        }
                    }
                }

                // ElectricToGearGenerator: outputModes
                // ElectricToGearGenerator: outputModes
                if (block.BlockParam is ElectricToGearGeneratorBlockParam electricToGear)
                {
                    // teethCount はギア比計算 (connectGear.TeethCount / gear.TeethCount) の除数になるため 0/負値を弾く
                    // teethCount is a divisor in the gear-ratio calc, so reject 0/negative to avoid Infinity/NaN RPM
                    if (electricToGear.TeethCount <= 0)
                    {
                        logs += $"[BlockMaster] Name:{block.Name} teethCount must be > 0 (got {electricToGear.TeethCount})\n";
                    }
                    if (electricToGear.OutputModes == null || electricToGear.OutputModes.Length == 0)
                    {
                        logs += $"[BlockMaster] Name:{block.Name} has empty outputModes\n";
                    }
                    else
                    {
                        foreach (var mode in electricToGear.OutputModes)
                        {
                            if (mode.RequiredPower <= 0)
                                logs += $"[BlockMaster] Name:{block.Name} outputMode requiredPower must be > 0 (got {mode.RequiredPower})\n";
                            if (mode.Rpm < 0 || mode.Torque < 0)
                                logs += $"[BlockMaster] Name:{block.Name} outputMode rpm/torque must be >= 0\n";
                        }
                    }
                }

                // 容量ゼロや非有限値は発電能力を失わせるためロード時に拒否する
                // Reject zero or non-finite capacity at load time because it removes generation capability
                if (block.BlockParam is GearToElectricGeneratorBlockParam gearToElectric)
                {
                    var capacity = gearToElectric.MaxGeneratedPower;
                    if (capacity <= 0f || float.IsNaN(capacity) || float.IsInfinity(capacity))
                        logs += $"[BlockMaster] Name:{block.Name} maxGeneratedPower must be finite and > 0 (got {capacity})\n";
                }

                // CleanRoomAirFilter: filterItemGuid
                // CleanRoomAirFilter: filterItemGuid
                if (block.BlockParam is CleanRoomAirFilterBlockParam cleanRoomAirFilter)
                {
                    var id = MasterHolder.ItemMaster.GetItemIdOrNull(cleanRoomAirFilter.FilterItemGuid);
                    if (id == null)
                    {
                        logs += $"[BlockMaster] Name:{block.Name} has invalid FilterItemGuid:{cleanRoomAirFilter.FilterItemGuid}\n";
                    }
                }
            }

            return logs;
        }
    }
}
