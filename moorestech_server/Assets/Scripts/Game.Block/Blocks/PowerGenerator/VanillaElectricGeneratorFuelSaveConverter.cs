using System;
using Core.Master;
using Core.Update;
using Game.Fluid;

namespace Game.Block.Blocks.PowerGenerator
{
    /// <summary>
    /// 燃料サービスの状態をセーブJSONへ書き出し・復元する。
    /// Writes the fuel service state to save JSON and restores it.
    /// </summary>
    internal static class VanillaElectricGeneratorFuelSaveConverter
    {
        internal static void Write(VanillaElectricGeneratorFuelService service, VanillaElectricGeneratorSaveJsonObject saveData)
        {
            // tickを秒数に変換して保存（tick数の変動に対応）
            // Convert ticks to seconds for storage (to handle tick rate changes)
            saveData.RemainingFuelSeconds = GameUpdater.TicksToSeconds(service.RemainingFuelTicks);
            saveData.ActiveFuelType = service._currentFuelType.ToString();

            saveData.CurrentFuelItemGuidStr = null;
            if (service._currentFuelType == VanillaElectricGeneratorFuelService.FuelType.Item && service._currentFuelItemId != ItemMaster.EmptyItemId && service._itemFuelSettings.TryGetValue(service._currentFuelItemId, out var itemFuel))
            {
                saveData.CurrentFuelItemGuidStr = itemFuel.Setting.ItemGuid.ToString();
            }

            saveData.CurrentFuelFluidGuidStr = null;
            if (service._currentFuelType == VanillaElectricGeneratorFuelService.FuelType.Fluid && service._currentFuelFluidId != FluidMaster.EmptyFluidId && service._fluidFuelSettings.TryGetValue(service._currentFuelFluidId, out var fluidFuel))
            {
                saveData.CurrentFuelFluidGuidStr = fluidFuel.Setting.FluidGuid.ToString();
            }

            saveData.FluidTank = null;
            if (service._fuelFluidContainer != null)
            {
                // 空タンク(EmptyFluidId)はGuid変換できないためnullで保存する。Restore側はnullを空として復元する
                // An empty tank (EmptyFluidId) has no Guid, so persist null; Restore maps null back to empty
                var isEmpty = service._fuelFluidContainer.FluidId == FluidMaster.EmptyFluidId;
                saveData.FluidTank = new VanillaElectricGeneratorFluidSaveJsonObject
                {
                    FluidGuidStr = isEmpty ? null : MasterHolder.FluidMaster.GetFluidMaster(service._fuelFluidContainer.FluidId).FluidGuid.ToString(),
                    Amount = service._fuelFluidContainer.Amount,
                };
            }
        }

        internal static void Restore(VanillaElectricGeneratorFuelService service, VanillaElectricGeneratorSaveJsonObject saveData)
        {
            if (saveData == null) return;

            if (!string.IsNullOrEmpty(saveData.ActiveFuelType) && Enum.TryParse(saveData.ActiveFuelType, out VanillaElectricGeneratorFuelService.FuelType parsedType))
            {
                service._currentFuelType = parsedType;
            }
            else
            {
                service._currentFuelType = VanillaElectricGeneratorFuelService.FuelType.None;
            }

            service._currentFuelItemId = saveData.CurrentFuelItemGuid.HasValue
                ? MasterHolder.ItemMaster.GetItemId(saveData.CurrentFuelItemGuid.Value)
                : ItemMaster.EmptyItemId;

            service._currentFuelFluidId = saveData.CurrentFuelFluidGuid.HasValue
                ? MasterHolder.FluidMaster.GetFluidId(saveData.CurrentFuelFluidGuid.Value)
                : FluidMaster.EmptyFluidId;

            if (saveData.FluidTank != null && service._fuelFluidContainer != null)
            {
                var savedFluidGuid = saveData.FluidTank.FluidGuid;
                service._fuelFluidContainer.FluidId = savedFluidGuid.HasValue
                    ? MasterHolder.FluidMaster.GetFluidId(savedFluidGuid.Value)
                    : FluidMaster.EmptyFluidId;
                service._fuelFluidContainer.Amount = saveData.FluidTank.Amount;
            }
        }
    }
}
