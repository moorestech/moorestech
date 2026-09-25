using System;
using System.IO;
using System.Text.RegularExpressions;
using Common.Debug;
using Newtonsoft.Json.Linq;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.UnitTest.MapPreview.Integration
{
    internal sealed class GeneratedMapPreviewTestFixture : IDisposable
    {
        internal const string MapObjectAddress = "Tests/GeneratedMapPreview/MapObject";
        internal const string OutcropAddress = "Tests/GeneratedMapPreview/Outcrop";
        internal const string TerrainLayerAddress = "Tests/GeneratedMapPreview/TerrainLayer";

        private readonly string _previousDebugDirectory;
        internal string ServerDataDirectory { get; }

        internal GeneratedMapPreviewTestFixture(bool generateOre, int tileGridSize)
        {
            ServerDataDirectory = Path.Combine(Path.GetTempPath(), $"GeneratedMapPreviewTest_{Guid.NewGuid():N}");
            CopyDirectory(TestModDirectory.ForUnitTestModDirectory, ServerDataDirectory);

            // 生成設定を縮小して資産を差し替える
            // Shrink tracked generation inputs and replace production asset addresses with test assets
            var generationPath = Path.Combine(ServerDataDirectory, "mods", "forUnitTest", "master", "generation.json");
            var generation = JObject.Parse(File.ReadAllText(generationPath));
            var config = generation["algorithmParam"];
            config["overrideResolution"] = 33;
            config["detailResolution"] = 32;
            config["gridSizeX"] = tileGridSize;
            config["gridSizeZ"] = tileGridSize;
            config["generateObject"] = false;
            config["generateOre"] = generateOre;
            config["generateDetail"] = false;
            config["useSpawnOffsetSearch"] = false;
            var generationJson = Regex.Replace(generation.ToString(), "test/terrain-layer/[A-Za-z]+(?:/blend)?", TerrainLayerAddress);
            File.WriteAllText(generationPath, generationJson);

            var mapPath = Path.Combine(ServerDataDirectory, "mods", "forUnitTest", "master", "map.json");
            var map = JObject.Parse(File.ReadAllText(mapPath));
            foreach (var entry in (JArray)map["mapObjects"]) entry["addressablePath"] = MapObjectAddress;
            foreach (var entry in (JArray)map["mapVeins"]) entry["outcropAddressablePath"] = OutcropAddress;
            File.WriteAllText(mapPath, map.ToString());
            _previousDebugDirectory = DebugParametersCacheDirectory.GetOverride();
            DebugParametersCacheDirectory.SetOverride(Path.Combine(ServerDataDirectory, "debug"));
            DebugParameters.SaveString(ServerDirectory.DebugServerDirectorySettingKey, ServerDataDirectory);
        }

        public void Dispose()
        {
            DebugParametersCacheDirectory.SetOverride(_previousDebugDirectory);
            if (Directory.Exists(ServerDataDirectory)) Directory.Delete(ServerDataDirectory, true);
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
            foreach (var directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }
}
