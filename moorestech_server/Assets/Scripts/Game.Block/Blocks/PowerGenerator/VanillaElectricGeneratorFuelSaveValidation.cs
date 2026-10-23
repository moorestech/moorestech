using System;
using Core.Master;
using Core.Update;
using UnityEngine;

namespace Game.Block.Blocks.PowerGenerator
{
    // 外部セーブの燃焼状態を検査し、現在設定と矛盾する状態を確定させない
    // Validate externally saved combustion before committing any state inconsistent with current settings
    internal static class VanillaElectricGeneratorFuelSaveValidation
    {
        internal static void Validate(VanillaElectricGeneratorFuelService service, VanillaElectricGeneratorSaveJsonObject save)
        {
            if (save == null) Fail("fuel state is missing");
            var seconds = save.RemainingFuelSeconds;
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) Fail($"invalid remainingFuelSeconds:{seconds}");
            uint totalTicks = 0;

            // 種別とIDと許可設定を同じ境界で照合する
            // Check the kind, identity and allowed setting at the same boundary
            switch (save.ActiveFuelType)
            {
                case "None":
                    if (seconds != 0 || !IsEmpty(save.CurrentFuelItemGuidStr) || !IsEmpty(save.CurrentFuelFluidGuidStr)) Fail("inactive fuel has an identity or remaining time");
                    return;
                case "Item":
                    var itemGuid = save.CurrentFuelItemGuid;
                    var itemId = itemGuid.HasValue ? MasterHolder.ItemMaster.GetItemIdOrNull(itemGuid.Value) : null;
                    if (!IsEmpty(save.CurrentFuelFluidGuidStr) || !itemId.HasValue || !service._itemFuelSettings.TryGetValue(itemId.Value, out var itemFuel))
                        Fail($"active item fuel is not configured:{save.CurrentFuelItemGuidStr}");
                    else totalTicks = itemFuel.Ticks;
                    break;
                case "Fluid":
                    var fluidGuid = save.CurrentFuelFluidGuid;
                    var fluidId = fluidGuid.HasValue ? MasterHolder.FluidMaster.GetFluidIdOrNull(fluidGuid.Value) : null;
                    if (!IsEmpty(save.CurrentFuelItemGuidStr) || !fluidId.HasValue || !service._fluidFuelSettings.TryGetValue(fluidId.Value, out var fluidFuel))
                        Fail($"active fluid fuel is not configured:{save.CurrentFuelFluidGuidStr}");
                    else totalTicks = fluidFuel.Ticks;
                    break;
                default:
                    Fail($"unsupported activeFuelType:{save.ActiveFuelType}");
                    break;
            }

            // 秒で保存した値を同じtick換算の総量と比較する
            // Compare saved seconds with the total expressed through the same tick conversion
            if (seconds <= 0 || GameUpdater.TicksToSeconds(totalTicks) < seconds) Fail($"remaining time is outside the configured fuel duration:{seconds}");

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
    }
}
