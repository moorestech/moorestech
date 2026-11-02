using System;
using Core.Master;
using Core.Update;
using Game.Fluid;
using UnityEngine;

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
            if (service._currentFuelType == VanillaElectricGeneratorFuelService.FuelType.Item && service._currentFuelItemId != ItemMaster.EmptyItemId)
            {
                saveData.CurrentFuelItemGuidStr = MasterHolder.ItemMaster.GetItemMaster(service._currentFuelItemId).ItemGuid.ToString();
            }

            saveData.CurrentFuelFluidGuidStr = null;
            if (service._currentFuelType == VanillaElectricGeneratorFuelService.FuelType.Fluid && service._currentFuelFluidId != FluidMaster.EmptyFluidId)
            {
                saveData.CurrentFuelFluidGuidStr = MasterHolder.FluidMaster.GetFluidMaster(service._currentFuelFluidId).FluidGuid.ToString();
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
            ValidateSavedFuel();

            service._currentFuelType = Enum.Parse<VanillaElectricGeneratorFuelService.FuelType>(saveData.ActiveFuelType);

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
            #region Internal
            void ValidateSavedFuel()
            {
                if (saveData == null) Fail("fuel state is missing");
                var seconds = saveData.RemainingFuelSeconds;
                if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) Fail($"invalid remainingFuelSeconds:{seconds}");

                // 既消費燃料は現在の許可と分けて復元
                // Restore consumed fuel independently of current ignition permissions
                switch (saveData.ActiveFuelType)
                {
                    case "None":
                        if (seconds != 0 || !IsEmpty(saveData.CurrentFuelItemGuidStr) || !IsEmpty(saveData.CurrentFuelFluidGuidStr)) Fail("inactive fuel has an identity or remaining time");
                        return;
                    case "Item":
                        var itemGuid = saveData.CurrentFuelItemGuid;
                        var itemId = itemGuid.HasValue ? MasterHolder.ItemMaster.GetItemIdOrNull(itemGuid.Value) : null;
                        if (!IsEmpty(saveData.CurrentFuelFluidGuidStr) || !itemId.HasValue)
                            Fail($"active item fuel identity is invalid:{saveData.CurrentFuelItemGuidStr}");
                        if (!service._itemFuelSettings.ContainsKey(itemId.Value)) Debug.LogWarning($"[ElectricGenerator] 許可設定から外れた既消費燃料を0出力で復元します item:{itemGuid}");
                        break;
                    case "Fluid":
                        var fluidGuid = saveData.CurrentFuelFluidGuid;
                        var fluidId = fluidGuid.HasValue ? MasterHolder.FluidMaster.GetFluidIdOrNull(fluidGuid.Value) : null;
                        if (!IsEmpty(saveData.CurrentFuelItemGuidStr) || !fluidId.HasValue)
                            Fail($"active fluid fuel identity is invalid:{saveData.CurrentFuelFluidGuidStr}");
                        if (!service._fluidFuelSettings.ContainsKey(fluidId.Value)) Debug.LogWarning($"[ElectricGenerator] 許可設定から外れた既消費燃料を0出力で復元します fluid:{fluidGuid}");
                        break;
                    default:
                        Fail($"unsupported activeFuelType:{saveData.ActiveFuelType}");
                        break;
                }

                // 残寿命を保ち復元可能範囲を検査
                // Preserve consumed fuel lifetime and validate only the restorable range
                if (seconds <= 0 || GameUpdater.TicksToSeconds(uint.MaxValue) < seconds) Fail($"remaining time is outside the restorable range:{seconds}");

                #region Internal
                bool IsEmpty(string value) => string.IsNullOrEmpty(value) || value == Guid.Empty.ToString();
                void Fail(string reason)
                {
                    var message = $"[ElectricGenerator] 燃料の復元が不整合のためロードを中断します: {reason}";
                    Debug.LogError(message);
                    throw new InvalidOperationException(message);
                }
                #endregion
            }
            #endregion
        }
    }
}
