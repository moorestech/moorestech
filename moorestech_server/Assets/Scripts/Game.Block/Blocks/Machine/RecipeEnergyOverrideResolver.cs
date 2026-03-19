using Game.EnergySystem;
using Mooresmaster.Model.MachineRecipesModule;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    ///     レシピのエネルギーオーバーライド設定からElectricPowerを解決するヘルパー
    ///     Helper to resolve ElectricPower from recipe energy override settings
    /// </summary>
    public static class RecipeEnergyOverrideResolver
    {
        /// <summary>
        ///     ElectricMachine用: レシピのオーバーライドからrequiredPowerを解決する
        ///     For ElectricMachine: resolve requiredPower from recipe override
        /// </summary>
        public static ElectricPower ResolveElectricPower(MachineRecipeMasterElement recipe, ElectricPower blockDefaultPower)
        {
            if (recipe == null) return blockDefaultPower;
            if (recipe.EnergyOverrideType != MachineRecipeMasterElement.EnergyOverrideTypeConst.Electric) return blockDefaultPower;

            // Electricケースの型にキャストしてrequiredPowerを取得
            // Cast to Electric case type and get requiredPower
            var electricOverride = (ElectricEnergyOverride)recipe.EnergyOverride;
            return new ElectricPower(electricOverride.RequiredPower);
        }

        /// <summary>
        ///     GearMachine用: レシピのオーバーライドからrequiredPowerを解決する（RPM * Torque）
        ///     For GearMachine: resolve requiredPower from recipe override (RPM * Torque)
        /// </summary>
        public static ElectricPower ResolveGearPower(MachineRecipeMasterElement recipe, float blockDefaultTorque, float blockDefaultRpm)
        {
            if (recipe == null) return new ElectricPower(blockDefaultTorque * blockDefaultRpm);

            var torque = blockDefaultTorque;
            var rpm = blockDefaultRpm;
            ResolveGearParams(recipe, ref torque, ref rpm);
            return new ElectricPower(torque * rpm);
        }

        /// <summary>
        ///     GearMachine用: レシピのオーバーライドからTorqueとRPMを個別に解決する
        ///     For GearMachine: resolve individual torque and RPM from recipe override
        /// </summary>
        public static void ResolveGearParams(MachineRecipeMasterElement recipe, ref float torque, ref float rpm)
        {
            if (recipe == null) return;
            if (recipe.EnergyOverrideType != MachineRecipeMasterElement.EnergyOverrideTypeConst.Gear) return;

            // Gearケースの型にキャストしてTorque/RPMを取得
            // Cast to Gear case type and get Torque/RPM
            var gearOverride = (GearEnergyOverride)recipe.EnergyOverride;
            if (gearOverride.RequireTorque.HasValue) torque = gearOverride.RequireTorque.Value;
            if (gearOverride.RequiredRpm.HasValue) rpm = gearOverride.RequiredRpm.Value;
        }
    }
}
