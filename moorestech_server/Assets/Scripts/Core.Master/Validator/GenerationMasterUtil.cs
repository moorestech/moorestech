using System.Collections.Generic;
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

        // Unityのdetailパッチ粒度。マスタ検証・生成・適用が同じ規則を見るための正本
        // Unity's detail patch granularity; the single rule master validation, generation and application all read
        public const int DetailResolutionPerPatch = 16;

        // detail解像度が満たすべき条件。パッチ粒度の倍数で、高さのサンプル数(=解像度-1)を超えない
        // The condition a detail resolution must meet: a multiple of the patch granularity, never above the heightmap's sample count (resolution - 1)
        public static bool IsValidDetailResolution(int detailResolution, int heightmapResolution)
        {
            return DetailResolutionPerPatch <= detailResolution &&
                   detailResolution % DetailResolutionPerPatch == 0 &&
                   detailResolution <= heightmapResolution - 1;
        }

        // 規則を破ったときの説明文。判定と文言を同じ持ち主に置き、片方だけ変わる形を作らない
        // The explanation for a violated rule; keeping test and wording with one owner stops either from drifting alone
        public static string DescribeDetailResolutionRule(int heightmapResolution)
        {
            return $"must be at least {DetailResolutionPerPatch}, a multiple of {DetailResolutionPerPatch}, and no greater than {heightmapResolution - 1}";
        }

        public static bool Validate(Generation generation, out string errorLogs)
        {
            errorLogs = "";
            errorLogs += VeinTypeValidation();
            errorLogs += VeinGuidUniquenessValidation();
            errorLogs += OreSpacingValidation();
            errorLogs += SpawnDistanceBandValidation();
            errorLogs += DetailResolutionValidation();
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

            // oreとfluidは独立した設定なので、それぞれの内部だけでveinGuid重複を弾く
            // Ore and fluid are independent configs, so reject duplicate veinGuids only within each collection
            string VeinGuidUniquenessValidation()
            {
                if (generation.AlgorithmParam is not VanillaGeneratorAlgorithmParam vanillaGenerator)
                {
                    return "";
                }

                var logs = "";
                var oreVeinGuids = new HashSet<System.Guid>();
                foreach (var oreEntry in vanillaGenerator.OreConfig.Entries)
                {
                    if (oreVeinGuids.Add(oreEntry.VeinGuid)) continue;
                    logs += $"[GenerationMaster] oreConfig.entries has duplicate VeinGuid:{oreEntry.VeinGuid}\n";
                }

                var fluidVeinGuids = new HashSet<System.Guid>();
                foreach (var fluidEntry in vanillaGenerator.OreConfig.FluidEntries)
                {
                    if (fluidVeinGuids.Add(fluidEntry.VeinGuid)) continue;
                    logs += $"[GenerationMaster] oreConfig.fluidEntries has duplicate VeinGuid:{fluidEntry.VeinGuid}\n";
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
                {
                    // 帯は配置方式ごとのパラメータが持つため、方式で取り出し先を選ぶ
                    // The bands live inside the per-mode placement parameters, so the mode decides where to read them from
                    var placementParam = objectConfig.Entries[i].PlacementParam;
                    var radii = placementParam is ClusterPlacementParam cluster
                        ? OuterRadiiOf(cluster.Bands)
                        : OuterRadiiOf(((ScatterPlacementParam)placementParam).Bands);
                    logs += DiagnoseBands($"{biomeName}.objectConfig.entries[{i}]", radii);
                }

                return logs;
            }

            // Unity受理かつ高さ内のdetailのみ許可
            // Allows only Unity-stable detail sizes within the heightmap.
            string DetailResolutionValidation()
            {
                if (generation.AlgorithmParam is not VanillaGeneratorAlgorithmParam vanillaGenerator)
                {
                    return "";
                }

                var detailResolution = vanillaGenerator.DetailResolution;
                if (detailResolution < DetailResolutionPerPatch)
                    return $"[GenerationMaster] detailResolution:{detailResolution} must be at least {DetailResolutionPerPatch}\n";
                if (detailResolution % DetailResolutionPerPatch != 0)
                    return $"[GenerationMaster] detailResolution:{detailResolution} must be a multiple of {DetailResolutionPerPatch}\n";

                // 未知のpresetを上限0として扱うと、あらゆる解像度が別の理由で弾かれ真因がログから消える
                // Treating an unknown preset as a limit of zero would reject every resolution for another reason, erasing the real cause from the log
                var maximumDetailResolution = vanillaGenerator.OverrideResolution - 1;
                if (vanillaGenerator.OverrideResolution <= 0 && !TryResolvePresetSampleLimit(out maximumDetailResolution))
                    return $"[GenerationMaster] resolutionPreset:'{vanillaGenerator.ResolutionPreset}' is not a recognized preset\n";

                return maximumDetailResolution < detailResolution
                    ? $"[GenerationMaster] detailResolution:{detailResolution} exceeds heightmap sample limit:{maximumDetailResolution}\n"
                    : "";

                bool TryResolvePresetSampleLimit(out int sampleLimit)
                {
                    switch (vanillaGenerator.ResolutionPreset)
                    {
                        case "_256": sampleLimit = 256; return true;
                        case "_512": sampleLimit = 512; return true;
                        case "_1024": sampleLimit = 1024; return true;
                        case "_2048": sampleLimit = 2048; return true;
                        default: sampleLimit = 0; return false;
                    }
                }
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

        private static float[] OuterRadiiOf(ObjectClusterBandElement[] bands)
        {
            var radii = new float[bands.Length];
            for (var i = 0; i < bands.Length; i++) radii[i] = bands[i].OuterRadiusMeters;
            return radii;
        }
    }
}
