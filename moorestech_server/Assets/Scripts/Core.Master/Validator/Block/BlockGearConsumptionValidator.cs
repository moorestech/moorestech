using Mooresmaster.Model.BlocksModule;
using Mooresmaster.Model.GearConsumptionModule;

namespace Core.Master.Validator.Block
{
    internal static class BlockGearConsumptionValidator
    {
        internal static string Validate(Blocks blocks)
        {
            // 全BlockParamのGearConsumptionを検証する
            // Validate GearConsumption on every block param that has one
            var logs = "";
            foreach (var block in blocks.Data)
            {
                var consumption = ExtractGearConsumption(block.BlockParam);
                if (consumption == null) continue;
                if (!ValidateGearConsumption(consumption, out var error)) continue;
                logs += $"[BlockMaster] Name:{block.Name} has invalid GearConsumption: {error}\n";
            }
            return logs;

            #region Internal

            bool ValidateGearConsumption(GearConsumption c, out string error)
            {
                if (c.BaseRpm <= 0)
                {
                    error = $"baseRpm must be > 0 (got {c.BaseRpm})";
                    return true;
                }
                if (c.MinimumRpm < 0)
                {
                    error = $"minimumRpm must be >= 0 (got {c.MinimumRpm})";
                    return true;
                }
                if (c.MinimumRpm > c.BaseRpm)
                {
                    error = $"minimumRpm ({c.MinimumRpm}) must be <= baseRpm ({c.BaseRpm})";
                    return true;
                }
                error = null;
                return false;
            }
            #endregion
        }

        private static GearConsumption ExtractGearConsumption(object blockParam)
        {
            // gearConsumptionを持つ型の判定はスキーマのIGearConsumptionParamが正本
            // The schema's IGearConsumptionParam is the authority on which params carry a gearConsumption
            return blockParam is IGearConsumptionParam gearConsumptionParam ? gearConsumptionParam.GearConsumption : null;
        }
    }
}
