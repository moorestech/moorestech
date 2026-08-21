using System;
using System.IO;
using Core.Master;
using Game.MapGeneration.Export;
using Game.MapGeneration.Identity;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Transfer;
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

        // TerrainTransferMetaReaderが生成ワールドの版照合に参照する。高さの意味(木摂動前後)が変わるたび上げる
        // Referenced by TerrainTransferMetaReader to gate generated worlds; bump whenever the height semantics (pre/post tree perturbation) change
        public const string GeneratorVersion = "2.0.0";
        private const string CacheReadmeText = "このディレクトリは削除可能です。削除しても次回起動時に自動で再構築されます。";

        public static void EnsureWorld(WorldProvisionSettings settings)
        {
            var worldDataDirectory = settings.WorldDataDirectory;

            // 前回クラッシュの残骸を先に片付ける(以後の存在判定を汚さないため)
            // Clear crash leftovers first, before they can pollute the existence checks below
            if (Directory.Exists(worldDataDirectory.ProvisioningTempDirectory))
                Directory.Delete(worldDataDirectory.ProvisioningTempDirectory, true);

            // world.jsonはコミット済みワールドの証跡。作り直しはしないが、使えるワールドかはここで確かめる
            // world.json marks a committed world; provisioning stops here, but whether the world is usable is settled now
            if (File.Exists(worldDataDirectory.WorldMetaFilePath))
            {
                // 版照合はTerrainTransferMetaReaderが唯一持つ。ハンドシェイクまで遅らせるとcatch-allに握り潰されクライアントが無言でハングする
                // TerrainTransferMetaReader owns the sole version check; deferring it to the handshake lets a catch-all swallow it and hang the client silently
                var existingTerrainMeta = TerrainTransferMetaReader.Read(worldDataDirectory);

                // 指紋不一致は台帳がサーバー正本とずれる合図。バージョンと同じくここでfail-fastする
                // A fingerprint mismatch signals the ledger has drifted from the server's truth; fail fast here just as for the version
                if (!existingTerrainMeta.IsTemplate)
                {
                    var selectedGeneration = MasterHolder.GenerationMaster.SelectedGeneration;
                    var currentFingerprint = GenerationMasterFingerprint.Compute(
                        MasterHolder.GenerationMaster.SourceJsonText, selectedGeneration, settings.ServerDataDirectory);
                    if (currentFingerprint != existingTerrainMeta.GenerationMasterFingerprint)
                        throw new InvalidOperationException(
                            $"World '{worldDataDirectory.Root}' was generated with generation master fingerprint " +
                            $"'{existingTerrainMeta.GenerationMasterFingerprint}', but this build's is '{currentFingerprint}'. " +
                            "Delete the world directory and generate the world again.");
                }

                return;
            }

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

                var output = MapGenerationPipeline.Generate(selected, settings.Seed, settings.ServerDataDirectory);

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

                    // 実際に生成したタイル枚数をそのまま書く。gridSizeX×gridSizeZの再計算はしない(出力そのものが唯一の正)
                    // Record the tile count generation actually produced; never recompute gridSizeX x gridSizeZ (the output itself is the sole truth)
                    TerrainTileCount = output.Tiles.Count,

                    // マスタ値ではなく生成が確定させた値を書く。スポーン探索のGはこの瞬間にしか存在しない
                    // Record what generation settled on, not the master values; the spawn-search G exists only at this moment
                    TerrainNoiseOriginX = output.NoiseOrigin.x,
                    TerrainNoiseOriginZ = output.NoiseOrigin.y,
                    TerrainSceneOriginX = output.SceneOrigin.x,
                    TerrainSceneOriginZ = output.SceneOrigin.y,

                    GenerationMasterFingerprint = GenerationMasterFingerprint.Compute(
                        MasterHolder.GenerationMaster.SourceJsonText, selected, settings.ServerDataDirectory),
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

                    // templateは地形を生成しないのでノイズ窓もシーン原点も存在しない。0ではなくnullで不在を表明する
                    // Template generates no terrain, so neither origin exists; null declares that absence instead of 0
                    TerrainNoiseOriginX = null,
                    TerrainNoiseOriginZ = null,
                    TerrainSceneOriginX = null,
                    TerrainSceneOriginZ = null,
                };
            }

            #endregion
        }
    }
}
