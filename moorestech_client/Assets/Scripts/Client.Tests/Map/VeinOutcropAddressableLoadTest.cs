using System;
using System.Collections.Generic;
using Client.Tests.Support;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace Client.Tests.Map
{
    /// <summary>
    ///     v8マスタの全鉱脈について露頭アドレスが実在することを検証
    ///     Verifies every vein in the v8 master resolves to a real outcrop address
    /// </summary>
    public class VeinOutcropAddressableLoadTest
    {
        private const string MapJsonPath = "server_v8/mods/moorestechAlphaMod_8/master/map.json";
        private const string ItemVeinAddressPrefix = "Vanilla/Environment/Vein/Item/";

        // VeinPrefab_Tungstenが未作成のためTungstenだけ旧プレハブを据え置く（ADR-0026）
        // Tungsten alone keeps the legacy prefab because VeinPrefab_Tungsten does not exist yet (ADR-0026)
        private static readonly string[] LegacyItemAddressAllowList = { "Vanilla/Environment/Vein/Item/Tungsten" };

        [Test]
        public void 全鉱脈の露頭アドレスがAddressablesに登録されている()
        {
            var assetPathByAddress = CollectAddressableAssetPaths();

            foreach (var vein in LoadVeins())
            {
                var address = (string)vein["outcropAddressablePath"];
                var veinName = (string)vein["veinName"];
                Assert.IsTrue(assetPathByAddress.TryGetValue(address, out var assetPath), $"address is not registered to Addressables: {address} (vein: {veinName})");

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                Assert.IsNotNull(prefab, $"outcrop prefab does not exist: {assetPath} (vein: {veinName})");
            }
        }

        [Test]
        public void Item系鉱脈の露頭がVeinPrefabシリーズを指している()
        {
            foreach (var vein in LoadVeins())
            {
                var address = (string)vein["outcropAddressablePath"];
                var veinName = (string)vein["veinName"];
                if (!address.StartsWith(ItemVeinAddressPrefix, StringComparison.Ordinal)) continue;
                if (Array.IndexOf(LegacyItemAddressAllowList, address) >= 0) continue;

                StringAssert.StartsWith($"{ItemVeinAddressPrefix}VeinPrefab_", address, $"item vein still points at a legacy outcrop: {veinName}");
            }
        }

        private static List<JObject> LoadVeins()
        {
            var mapJson = JObject.Parse(PinnedMasterRepository.ReadPinnedFile(MapJsonPath));
            var veins = new List<JObject>();
            foreach (var token in (JArray)mapJson["mapVeins"]) veins.Add((JObject)token);

            // 0件だと以降の検証が全て素通りするので先に落とす
            // With zero veins every later assertion would pass vacuously, so fail here first
            Assert.IsNotEmpty(veins, "mapVeins is empty; the test would pass vacuously");
            return veins;
        }

        private static Dictionary<string, string> CollectAddressableAssetPaths()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            Assert.IsNotNull(settings, "AddressableAssetSettings is missing");

            var assetPathByAddress = new Dictionary<string, string>();
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                foreach (var entry in group.entries) assetPathByAddress[entry.address] = AssetDatabase.GUIDToAssetPath(entry.guid);
            }

            return assetPathByAddress;
        }
    }
}
