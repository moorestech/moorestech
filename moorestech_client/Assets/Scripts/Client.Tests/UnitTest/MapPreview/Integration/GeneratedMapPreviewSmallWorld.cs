using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.MapScene.Editor;
using Game.Map.Interface.Json;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UnitTest.MapPreview.Integration
{
    internal sealed class GeneratedMapPreviewSmallWorld : IDisposable
    {
        private readonly string _directory;
        private readonly GeneratedMapPreviewTestFixture _fixture;
        private string _cache;
        private string _masterPath;
        private JObject _mapMaster;
        private MapInfoJson _map;
        internal string ObjectGuid { get; private set; }
        internal const string MissingAddress = "Tests/GeneratedMapPreview/MissingPrefab";

        internal GeneratedMapPreviewSmallWorld()
        {
            _fixture = new GeneratedMapPreviewTestFixture(false, 1);
            _directory = _fixture.ServerDataDirectory;
        }

        internal void Initialize()
        {
            var generationPath = Directory.GetFiles(Path.Combine(_directory, "mods"), "generation.json", SearchOption.AllDirectories).Single();
            var generation = JObject.Parse(File.ReadAllText(generationPath));
            // 毎回固有の指紋を与え、fixtureが新規に作ったキャッシュだけを所有する
            // Give each fixture a unique fingerprint so it owns only the cache it creates
            generation["previewFixtureIdentity"] = Guid.NewGuid().ToString();
            var config = generation["algorithmParam"];
            config["overrideResolution"] = 33;
            config["detailResolution"] = 32;
            config["gridSizeX"] = 1;
            config["gridSizeZ"] = 1;
            config["useSpawnOffsetSearch"] = false;
            config["generateObject"] = false;
            config["generateOre"] = false;
            config["generateDetail"] = false;
            File.WriteAllText(generationPath, generation.ToString());
            _masterPath = Path.Combine(Path.GetDirectoryName(generationPath), "map.json");
            _mapMaster = JObject.Parse(File.ReadAllText(_masterPath));
            ObjectGuid = (string)_mapMaster["mapObjects"][0]["mapObjectGuid"];

            var before = GeneratedMapPreviewObservation.WorldDirectories();
            using var world = GeneratedMapPreviewWorld.Create(_directory);
            _map = world.Map;
            var files = WorldDataDirectory.FromWorldRoot(GeneratedMapPreviewObservation.WorldDirectories().Except(before).Single());
            _cache = WorldDataDirectory.ForWorldCache(TerrainTransferMetaReader.Read(files).WorldId).Root;
            _map.MapVeins.Clear();
        }

        internal void SetObjectCount(int count, bool missingAsset)
        {
            Assert.That(count, Is.InRange(0, 1));
            // 専用生成キャッシュの配置だけを0/1件にし、公開Regenerateで通常経路を通す
            // Set only the fixture-owned snapshot to zero or one placement and use public Regenerate normally
            _map.MapObjects = new List<MapObjectInfoJson>();
            if (count == 1)
                _map.MapObjects.Add(new MapObjectInfoJson { InstanceId = 42, MapObjectGuidStr = ObjectGuid, X = 7, Y = 11, Z = -5, RotationW = 1, ScaleX = 1.2f, ScaleY = 0.8f, ScaleZ = 1.1f });
            File.WriteAllText(Path.Combine(_cache, "map.json"), JsonConvert.SerializeObject(_map));
            var source = (JObject)_mapMaster.DeepClone();
            if (missingAsset) source["mapObjects"][0]["addressablePath"] = MissingAddress;
            File.WriteAllText(_masterPath, source.ToString());
        }

        public void Dispose()
        {
            if (_cache != null && Directory.Exists(_cache)) Directory.Delete(_cache, true);
            _fixture.Dispose();
        }
    }
}
