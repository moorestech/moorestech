using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Client.Common;
using Client.Game.InGame.Map.MapObject;
using Client.MapScene.Editor;
using Core.Master;
using Game.Map.Interface.Json;
using Mooresmaster.Model.MapModule;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Client.Tests.UnitTest.MapPreview.Integration
{
    internal static class GeneratedMapPreviewPlacementParity
    {
        internal static void AssertMatches(GameObject root, string mapPath)
        {
            var map = JsonConvert.DeserializeObject<MapInfoJson>(File.ReadAllText(mapPath));
            var objects = root.GetComponentsInChildren<MapObjectGameObject>(true).ToDictionary(x => x.InstanceId);
            Assert.That(objects.Count, Is.EqualTo(map.MapObjects.Count));
            foreach (var expected in map.MapObjects)
            {
                var actual = objects[expected.InstanceId];
                Assert.That(actual.MapObjectGuid, Is.EqualTo(expected.MapObjectGuid));
                Assert.That(Vector3.Distance(actual.transform.position, expected.Position), Is.LessThan(0.0001f));
                Assert.That(Quaternion.Angle(actual.transform.rotation, expected.Rotation), Is.LessThan(0.05f));
                Assert.That(Vector3.Distance(actual.transform.localScale, expected.Scale), Is.LessThan(0.0001f));
            }

            // 配置区分と採掘音区分を照合し、treePlacement内の岩を木へ数えない
            // Combine placement and mining-sound categories so rocks inside treePlacement are not counted as trees
            var generation = JObject.Parse(MasterHolder.GenerationMaster.SourceJsonText);
            var treeGuids = generation.SelectTokens("$..treePlacement.prototypes[*].mapObjects[*].mapObjectGuid").Select(x => Guid.Parse((string)x)).ToHashSet();
            treeGuids.IntersectWith(MasterHolder.MapObjectMaster.Map.MapObjects
                .Where(x => x.MiningParam is IMinableMapObjectParam mining && mining.SoundEffectType == MiningMiningParam.SoundEffectTypeConst.tree).Select(x => x.MapObjectGuid));
            var rockGuids = generation.SelectTokens("$..objectConfig.entries[*]").Where(x => (string)x["terrainSurroundEffectType"] is "rockBareGround" or "rockNoBareGround")
                .SelectMany(x => x["prefabs"].SelectMany(p => p.SelectTokens("$..mapObjectGuid"))).Select(x => Guid.Parse((string)x)).ToHashSet();
            var treeCount = objects.Values.Count(x => treeGuids.Contains(x.MapObjectGuid));
            var rockCount = objects.Values.Count(x => rockGuids.Contains(x.MapObjectGuid));
            Assert.That(treeCount, Is.GreaterThan(0), "The real master fixture must contain trees.");
            Assert.That(rockCount, Is.GreaterThan(0), "The real master fixture must contain rocks.");

            var outcrops = root.transform.Find("VeinOutcrops");
            Assert.That(map.MapVeins.Count, Is.GreaterThan(0), "The real master fixture must contain veins.");
            Assert.That(outcrops.childCount, Is.EqualTo(map.MapVeins.Count));
            var assets = new EditorTerrainAssetLoader();
            var prefabs = new Dictionary<Guid, GameObject>();
            for (var index = 0; index < map.MapVeins.Count; index++)
            {
                var vein = map.MapVeins[index];
                var actual = outcrops.GetChild(index);
                if (!prefabs.TryGetValue(vein.VeinGuid, out var prefab))
                {
                    prefab = assets.LoadAsync<GameObject>(MasterHolder.MapVeinMaster.GetElementOrNull(vein.VeinGuid).OutcropAddressablePath, CancellationToken.None).GetAwaiter().GetResult();
                    prefabs.Add(vein.VeinGuid, prefab);
                }
                // 露頭の位置と表示資産を全鉱脈で照合し、縮小倍率もPrefabと一致させる
                // Check position and source asset for every vein and preserve the prefab's scale
                Assert.That(actual.position, Is.EqualTo(((Vector3)vein.MinPosition + vein.MaxPosition + Vector3.one) * 0.5f));
                Assert.That(Quaternion.Angle(actual.rotation, Quaternion.identity), Is.LessThan(0.001f));
                Assert.That(actual.localScale, Is.EqualTo(prefab.transform.localScale));
                Assert.That(PrefabUtility.GetCorrespondingObjectFromSource(actual.gameObject), Is.SameAs(prefab));
                Assert.That(actual.GetComponentsInChildren<Renderer>(), Is.Not.Empty);
            }
            Assert.That(root.GetComponentInChildren<SpawnPointObject>().transform.position, Is.EqualTo(map.DefaultSpawnPointJson.Position));
            Debug.Log($"[GeneratedMapPreviewIntegration] {objects.Count} object poses, trees {treeCount}, rocks {rockCount}, outcrops {outcrops.childCount} match map.json.");
        }
    }
}
