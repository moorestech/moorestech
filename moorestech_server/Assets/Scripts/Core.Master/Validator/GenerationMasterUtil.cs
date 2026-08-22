using Mooresmaster.Model.BiomeObjectConfigModule;
using Mooresmaster.Model.GenerationModule;
using Mooresmaster.Model.MapModule;

namespace Core.Master.Validator
{
    public static class GenerationMasterUtil
    {
        // AABBは点の±1なので軸差3未満は重なる。丸め1を見込み間隔の下限を4とする
        // An AABB spans the point +/-1, so axis gaps under 3 overlap; allowing one for rounding puts the floor at 4
        private const float MinOreSpacing = 4f;

        public static bool Validate(Generation generation, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += VeinTypeValidation();
            errorLogs += OreSpacingValidation();
            errorLogs += SpawnDistanceBandValidation();
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

            // 最小配置間隔が下限未満の帯を弾く
            // Reject bands whose spacing is under the floor
            string OreSpacingValidation()
            {
                if (generation.AlgorithmParam is not VanillaGeneratorAlgorithmParam vanillaGenerator)
                {
                    return "";
                }

                var logs = "";

                foreach (var oreEntry in vanillaGenerator.OreConfig.Entries)
                foreach (var band in oreEntry.Bands)
                {
                    if (band.MinDistanceBetweenOres >= MinOreSpacing) continue;
                    logs += $"[GenerationMaster] OreEntry VeinGuid:{oreEntry.VeinGuid} has minDistanceBetweenOres:{band.MinDistanceBetweenOres} below the {MinOreSpacing} floor\n";
                }

                foreach (var fluidEntry in vanillaGenerator.OreConfig.FluidEntries)
                foreach (var band in fluidEntry.Bands)
                {
                    if (band.MinDistanceBetweenOres >= MinOreSpacing) continue;
                    logs += $"[GenerationMaster] FluidVeinEntry VeinGuid:{fluidEntry.VeinGuid} has minDistanceBetweenOres:{band.MinDistanceBetweenOres} below the {MinOreSpacing} floor\n";
                }

                return logs;
            }

            // リング化できない帯（空・-1以外の負値・外半径重複）をマスタロード時に弾く
            // Reject bands that cannot become rings (empty, negative other than -1, duplicate outer radius) at master load
            string SpawnDistanceBandValidation()
            {
                if (generation.AlgorithmParam is not VanillaGeneratorAlgorithmParam vanillaGenerator)
                {
                    return "";
                }

                var logs = "";

                foreach (var oreEntry in vanillaGenerator.OreConfig.Entries)
                    logs += DiagnoseBands($"OreEntry VeinGuid:{oreEntry.VeinGuid}", OuterRadiiOf(oreEntry.Bands));

                foreach (var fluidEntry in vanillaGenerator.OreConfig.FluidEntries)
                    logs += DiagnoseBands($"FluidVeinEntry VeinGuid:{fluidEntry.VeinGuid}", OuterRadiiOf(fluidEntry.Bands));

                foreach (var (biomeName, objectConfig) in GenerationBiomeObjectConfigCatalog.Of(vanillaGenerator))
                for (var i = 0; i < objectConfig.Entries.Length; i++)
                    logs += DiagnoseBands($"{biomeName}.objectConfig.entries[{i}]", OuterRadiiOf(objectConfig.Entries[i].Bands));

                return logs;
            }

            #endregion
        }

        private static string DiagnoseBands(string subject, float[] outerRadiusMeters)
        {
            var logs = "";
            foreach (var problem in SpawnDistanceRingPlanner.Diagnose(outerRadiusMeters))
                logs += $"[GenerationMaster] {subject} {problem}\n";
            return logs;
        }

        // 生成型は帯ごとに別クラスになるため、外半径の取り出しだけ型別に用意する
        // The generated model gives each band its own class, so only the radius extraction is written per type
        private static float[] OuterRadiiOf(OreBandElement[] bands)
        {
            var radii = new float[bands.Length];
            for (var i = 0; i < bands.Length; i++) radii[i] = bands[i].OuterRadiusMeters;
            return radii;
        }

        private static float[] OuterRadiiOf(FluidOreBandElement[] bands)
        {
            var radii = new float[bands.Length];
            for (var i = 0; i < bands.Length; i++) radii[i] = bands[i].OuterRadiusMeters;
            return radii;
        }

        private static float[] OuterRadiiOf(ObjectScatterBandElement[] bands)
        {
            var radii = new float[bands.Length];
            for (var i = 0; i < bands.Length; i++) radii[i] = bands[i].OuterRadiusMeters;
            return radii;
        }
    }
}
