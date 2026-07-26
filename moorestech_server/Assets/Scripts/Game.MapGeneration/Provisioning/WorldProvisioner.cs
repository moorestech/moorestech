using System;
using System.IO;
using Core.Master;
using Game.MapGeneration.Export;
using Game.MapGeneration.Pipeline;
using Game.Paths;
using Newtonsoft.Json;

namespace Game.MapGeneration.Provisioning
{
    // ワールド新規作成を1回だけ行うプロビジョナ。全ファイルを一時ディレクトリに書き切ってから
    // Directory.Moveでリネーム確定する(アトミック)。world.jsonが無いのにRootがあれば破損として例外
    // Provisions a world exactly once. All files are written to a temp dir first, then committed via
    // Directory.Move rename (atomic). Root present without world.json is treated as corruption.
    public static class WorldProvisioner
    {
        // mapModeの唯一の定義。boot(StartServerSettings/ServerInstanceManager)もこれを参照する
        // Single source of truth for map mode names; boot code references these too
        public const string TemplateMapMode = "template";
        public const string GeneratedMapMode = "generated";
        private const string GeneratorVersion = "1.0.0";
        private const string CacheReadmeText = "このディレクトリは削除可能です。削除しても次回起動時に自動で再構築されます。";

        public static void EnsureWorld(WorldProvisionSettings settings)
        {
            var worldDataDirectory = settings.WorldDataDirectory;

            // 前回クラッシュの残骸を先に片付ける(以後の存在判定を汚さないため)
            // Clear crash leftovers first, before they can pollute the existence checks below
            if (Directory.Exists(worldDataDirectory.ProvisioningTempDirectory))
                Directory.Delete(worldDataDirectory.ProvisioningTempDirectory, true);

            // world.jsonはコミット済みワールドの証跡。存在すれば何もしない
            // world.json marks a committed world; if present this call is a no-op
            if (File.Exists(worldDataDirectory.WorldMetaFilePath))
                return;

            // Rootだけ存在してworld.jsonが無いのは書き込み途中の破損。無言で再生成しない
            // Root existing without world.json means a mid-write corruption; never silently regenerate
            if (Directory.Exists(worldDataDirectory.Root))
                throw new InvalidOperationException(
                    $"World directory is corrupted: '{worldDataDirectory.Root}' exists but world.json is missing.");

            var tempDataDirectory = WorldDataDirectory.FromWorldRoot(worldDataDirectory.ProvisioningTempDirectory);
            Directory.CreateDirectory(tempDataDirectory.Root);

            var metaJson = settings.MapMode switch
            {
                GeneratedMapMode => BuildGenerated(tempDataDirectory, settings),
                TemplateMapMode => BuildTemplate(tempDataDirectory, settings),
                _ => throw new ArgumentException($"Unknown map mode: '{settings.MapMode}'"),
            };

            // world.jsonはコミットマーカーなので必ず最後に書く
            // world.json is the commit marker, so it must be written last
            File.WriteAllText(tempDataDirectory.WorldMetaFilePath, JsonConvert.SerializeObject(metaJson, Formatting.Indented));

            // 一時ディレクトリ→本番Rootへのリネームで確定をアトミックにする
            // Renaming temp dir -> real root makes the commit atomic
            Directory.Move(tempDataDirectory.Root, worldDataDirectory.Root);

            #region Internal

            static WorldMetaJson BuildGenerated(WorldDataDirectory tempDataDirectory, WorldProvisionSettings settings)
            {
                // 優先度解決済みの1件が未定義ならgenerated modeは実行不能
                // A priority-resolved candidate must exist; generated mode cannot run without it
                var selected = MasterHolder.GenerationMaster.SelectedGeneration;
                if (selected == null)
                    throw new InvalidOperationException(
                        "Cannot provision a generated world: MasterHolder.GenerationMaster.SelectedGeneration is undefined.");

                var output = MapGenerationPipeline.Generate(selected, settings.Seed);

                var mapInfoJson = MapInfoJsonBuilder.Build(output);
                File.WriteAllText(tempDataDirectory.MapJsonFilePath, JsonConvert.SerializeObject(mapInfoJson, Formatting.Indented));
                TerrainFileWriter.Write(tempDataDirectory, output);

                return new WorldMetaJson
                {
                    Seed = settings.Seed,
                    GeneratorVersion = GeneratorVersion,
                    Algorithm = selected.Algorithm,
                    MapMode = GeneratedMapMode,
                    CreatedAt = DateTime.UtcNow.ToString("O"),
                    TerrainResolution = output.Resolution,
                    TerrainTileCount = 1,
                };
            }

            static WorldMetaJson BuildTemplate(WorldDataDirectory tempDataDirectory, WorldProvisionSettings settings)
            {
                var sourceMapJsonPath = WorldDataDirectory.ServerDataMapJsonPath(settings.ServerDataDirectory);
                Directory.CreateDirectory(tempDataDirectory.CacheDirectory);
                File.Copy(sourceMapJsonPath, tempDataDirectory.MapJsonFilePath);
                File.WriteAllText(tempDataDirectory.CacheReadmeFilePath, CacheReadmeText);

                return new WorldMetaJson
                {
                    Seed = settings.Seed,
                    GeneratorVersion = GeneratorVersion,
                    Algorithm = null,
                    MapMode = TemplateMapMode,
                    CreatedAt = DateTime.UtcNow.ToString("O"),
                    TerrainResolution = 0,
                    TerrainTileCount = 0,
                };
            }

            #endregion
        }
    }
}
