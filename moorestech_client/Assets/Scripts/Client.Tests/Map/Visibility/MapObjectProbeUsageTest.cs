using System.Collections.Generic;
using Client.Tests.Support;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.Rendering;

namespace Client.Tests.Map.Visibility
{
    // wrapperはgitignoreされたPersonalAssets内prefabのvariantなのでローカルUnityだけで検証する
    // Wrappers are variants of prefabs under gitignored PersonalAssets, so verify them only in local Unity
    [Category("IgnoreCI")]
    public class MapObjectProbeUsageTest
    {
        private const string MasterMapPath = "server_v8/mods/moorestechAlphaMod_8/master/map.json";
        private const int ExpectedMapObjectCount = 195;

        [Test]
        public void 全mapObjectのRendererはprobeサンプリングを使わない()
        {
            var mapJson = JObject.Parse(PinnedMasterRepository.ReadPinnedFile(MasterMapPath));
            var assetPathByAddress = CollectAddressableAssetPaths();
            var mapObjects = (JArray)mapJson["mapObjects"];
            Assert.AreEqual(ExpectedMapObjectCount, mapObjects.Count);
            var rendererCount = 0;

            // 出荷masterの全addressを正引きし、0件素通りと未登録を同時に防ぐ
            // Resolve every shipped master address, preventing both empty passes and missing registrations
            foreach (var mapObject in mapObjects)
            {
                var address = (string)mapObject["addressablePath"];
                Assert.IsTrue(assetPathByAddress.TryGetValue(address, out var assetPath), $"address is not registered: {address}");
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                Assert.IsNotNull(prefab, $"prefab is missing: {assetPath}");
                var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                Assert.IsNotEmpty(renderers, $"prefab has no Renderer: {assetPath}");

                foreach (var renderer in renderers)
                {
                    rendererCount++;
                    Assert.AreEqual(LightProbeUsage.Off, renderer.lightProbeUsage, $"light probe is enabled: {assetPath}/{renderer.name}");
                    Assert.AreEqual(ReflectionProbeUsage.Off, renderer.reflectionProbeUsage, $"reflection probe is enabled: {assetPath}/{renderer.name}");
                }
            }

            Assert.Greater(rendererCount, 0);
        }

        private static Dictionary<string, string> CollectAddressableAssetPaths()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.IsNotNull(settings, "AddressableAssetSettings is missing");
            var assetPathByAddress = new Dictionary<string, string>();

            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                foreach (var entry in group.entries)
                    assetPathByAddress[entry.address] = AssetDatabase.GUIDToAssetPath(entry.guid);
            }

            return assetPathByAddress;
        }
    }
}
