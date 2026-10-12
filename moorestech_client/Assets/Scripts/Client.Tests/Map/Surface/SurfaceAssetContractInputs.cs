using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.Map.Surface
{
#if UNITY_EDITOR
    internal static class SurfaceAssetContractInputs
    {
        public static Dictionary<string, string> CollectAddressablePaths()
        {
            var settings = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings;
            Assert.That(settings, Is.Not.Null);
            var result = new Dictionary<string, string>();
            foreach (var group in settings.groups)
            {
                if (group == null) continue;
                foreach (var entry in group.entries)
                    result[entry.address] = UnityEditor.AssetDatabase.GUIDToAssetPath(entry.guid);
            }
            return result;
        }

        public static GameObject LoadPrefab(string path)
        {
            return UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }
    }
#endif
}
