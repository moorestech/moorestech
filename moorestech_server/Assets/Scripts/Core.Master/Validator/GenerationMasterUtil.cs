using Mooresmaster.Model.GenerationModule;
using Mooresmaster.Model.MapModule;

namespace Core.Master.Validator
{
    public static class GenerationMasterUtil
    {
        public static bool Validate(Generation generation, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += VeinTypeValidation();
            return string.IsNullOrEmpty(errorLogs);

            #region Internal

            string VeinTypeValidation()
            {
                // algorithm!=VanillaGeneratorはoreConfigを持たないため検証対象外
                // Skip when algorithm isn't VanillaGenerator (no oreConfig to validate)
                if (generation.AlgorithmParam is not VanillaGeneratorAlgorithmParam vanillaGenerator)
                {
                    return "";
                }

                var logs = "";

                // OreEntry.VeinGuidはveinType==itemのmapVeinsのみを参照できる
                // OreEntry.VeinGuid may only reference mapVeins entries whose veinType is item
                foreach (var oreEntry in vanillaGenerator.OreConfig.Entries)
                {
                    var vein = MasterHolder.MapVeinMaster.GetElementOrNull(oreEntry.VeinGuid);
                    if (vein == null)
                    {
                        logs += $"[GenerationMaster] OreEntry has invalid VeinGuid:{oreEntry.VeinGuid}\n";
                    }
                    else if (vein.VeinParam is not ItemVeinParam)
                    {
                        logs += $"[GenerationMaster] OreEntry VeinGuid:{oreEntry.VeinGuid} references a non-item vein (veinName:{vein.VeinName})\n";
                    }
                }

                // FluidVeinEntry.VeinGuidはveinType==fluidのmapVeinsのみを参照できる
                // FluidVeinEntry.VeinGuid may only reference mapVeins entries whose veinType is fluid
                foreach (var fluidEntry in vanillaGenerator.OreConfig.FluidEntries)
                {
                    var vein = MasterHolder.MapVeinMaster.GetElementOrNull(fluidEntry.VeinGuid);
                    if (vein == null)
                    {
                        logs += $"[GenerationMaster] FluidVeinEntry has invalid VeinGuid:{fluidEntry.VeinGuid}\n";
                    }
                    else if (vein.VeinParam is not FluidVeinParam)
                    {
                        logs += $"[GenerationMaster] FluidVeinEntry VeinGuid:{fluidEntry.VeinGuid} references a non-fluid vein (veinName:{vein.VeinName})\n";
                    }
                }

                return logs;
            }

            #endregion
        }
    }
}
