using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Client.Game.Skit.Localization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace Client.Tests.Localization.Skit
{
    public class SkitLocalizationDynamicLoadContractTest
    {
        private const string EnglishAddress = "Vanilla/Skit/i18n/english";
        private const string JapaneseAddress = "Vanilla/Skit/i18n/japanese";
        private const string EnglishAssetPath =
            "Assets/AddressableResources/Skit/i18n/english.json";
        private const string JapaneseAssetPath =
            "Assets/AddressableResources/Skit/i18n/japanese.json";

        [Test]
        public void AddressableSettingsContainOnlySupportedSkitDictionaryAddresses()
        {
            var addresses = new List<string>();
            var assetPaths = new Dictionary<string, string>(StringComparer.Ordinal);
            var groupGuids = AssetDatabase.FindAssets(
                "t:AddressableAssetGroup",
                new[] { "Assets/AddressableAssetsData/AssetGroups" });

            // 公開APIでaddressとGUID列挙
            // Enumerate addresses and GUIDs through the public API
            foreach (var groupGuid in groupGuids)
            {
                var group = AssetDatabase.LoadAssetAtPath<AddressableAssetGroup>(
                    AssetDatabase.GUIDToAssetPath(groupGuid));
                foreach (var entry in group.entries)
                {
                    if (!entry.address.StartsWith("Vanilla/Skit/i18n/", StringComparison.Ordinal))
                        continue;
                    addresses.Add(entry.address);
                    assetPaths.Add(entry.address, AssetDatabase.GUIDToAssetPath(entry.guid));
                }
            }

            // address対応とGUID差替え拒否
            // Pin address mappings and reject GUID replacement
            CollectionAssert.AreEquivalent(new[] { EnglishAddress, JapaneseAddress }, addresses);
            Assert.AreEqual(EnglishAssetPath, assetPaths[EnglishAddress]);
            Assert.AreEqual(JapaneseAssetPath, assetPaths[JapaneseAddress]);
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

        [Test]
        public void JapaneseAddressableAssetContainsNaturalBackgroundText()
        {
            // address対応を固定した実ファイルを本番parserへ通す
            // Parse the real file pinned by the address mapping through the production parser
            var dictionary = SkitLocalizationDictionaryLoader.Parse(
                JapaneseAddress,
                File.ReadAllText(GetI18nPath("japanese")));

            Assert.AreEqual(
                "ひとまず、周りの石を集めましょう。",
                dictionary["skit.200_star_background.1.body"]);
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
