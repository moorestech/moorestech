using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Client.Game.Skit.Localization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Client.Tests.Localization.Skit
{
    public class SkitLocalizationDynamicLoadContractTest
    {
        private const string EnglishAddress = "Vanilla/Skit/i18n/english";
        private const string JapaneseAddress = "Vanilla/Skit/i18n/japanese";

        [Test]
        public void AddressableSettingsContainOnlySupportedSkitDictionaryAddresses()
        {
            var addresses = new List<string>();
            var groupGuids = AssetDatabase.FindAssets(
                "t:AddressableAssetGroup",
                new[] { "Assets/AddressableAssetsData/AssetGroups" });

            // Editor APIから実登録entryを列挙する
            // Enumerate actual registered entries through the Editor API
            foreach (var groupGuid in groupGuids)
            {
                var group = AssetDatabase.LoadMainAssetAtPath(
                    AssetDatabase.GUIDToAssetPath(groupGuid));
                var entries = new SerializedObject(group).FindProperty("m_SerializeEntries");
                for (var index = 0; index < entries.arraySize; index++)
                {
                    var address = entries.GetArrayElementAtIndex(index)
                        .FindPropertyRelative("m_Address").stringValue;
                    if (address.StartsWith("Vanilla/Skit/i18n/", StringComparison.Ordinal))
                        addresses.Add(address);
                }
            }

            // 言語JSONとmetaをAddressable登録と一体で保持する
            // Keep language JSON files and metadata together with their Addressable entries
            CollectionAssert.AreEquivalent(new[] { EnglishAddress, JapaneseAddress }, addresses);
            Assert.IsTrue(File.Exists(GetI18nPath("english")));
            Assert.IsTrue(File.Exists(GetI18nPath("english") + ".meta"));
            Assert.IsTrue(File.Exists(GetI18nPath("japanese")));
            Assert.IsTrue(File.Exists(GetI18nPath("japanese") + ".meta"));
        }

        [Test]
        public async Task EnglishSkitLoadsEnglishDictionaryOnce()
        {
            var loader = new RecordingDictionaryLoader();
            var source = new FakeSkitLocalizationSource();
            source.SetLanguage("english");
            using var resolver = new SkitLocalizationResolver(loader, source);

            await resolver.PrepareAsync("100_start_game");

            CollectionAssert.AreEqual(new[] { "english" }, loader.RequestedLanguages);
        }

        [Test]
        public async Task JapaneseSkitLoadsOnlyJapaneseAndEnglishDictionaries()
        {
            var loader = new RecordingDictionaryLoader();
            var source = new FakeSkitLocalizationSource();
            using var resolver = new SkitLocalizationResolver(loader, source);

            await resolver.PrepareAsync("100_start_game");

            CollectionAssert.AreEqual(
                new[] { "japanese", "english" },
                loader.RequestedLanguages);
        }

        private static string GetI18nPath(string languageCode)
        {
            return Path.Combine(
                Application.dataPath,
                "AddressableResources",
                "Skit",
                "i18n",
                languageCode + ".json");
        }

        private sealed class RecordingDictionaryLoader : ISkitLocalizationDictionaryLoader
        {
            public readonly List<string> RequestedLanguages = new();

            public UniTask<IReadOnlyDictionary<string, string>> LoadAsync(
                string languageCode)
            {
                RequestedLanguages.Add(languageCode);
                IReadOnlyDictionary<string, string> empty =
                    new Dictionary<string, string>();
                return UniTask.FromResult(empty);
            }
        }
    }
}
