using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Game.Skit.Localization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;

namespace Client.Tests.Localization.Skit
{
    public class SkitLocalizationResolverTest
    {
        private const string SkitKey = "skit.opening.7.body";

        [TestCase("Mod Target", "Skit Target", "Mod English", "Skit English", "Mod Target")]
        [TestCase("", "Skit Target", "Mod English", "Skit English", "Skit Target")]
        [TestCase("", "", "Mod English", "Skit English", "Mod English")]
        [TestCase("", "", "", "Skit English", "Skit English")]
        [TestCase("", "", "", "", "JSON Source")]
        public async Task ResolveCommandFieldUsesNonEmptyPriorityOrder(
            string modTarget,
            string skitTarget,
            string modEnglish,
            string skitEnglish,
            string expected)
        {
            var loader = new FakeDictionaryLoader(skitTarget, skitEnglish);
            var source = new FakeLocalizationSource(modTarget, modEnglish);
            using var resolver = new SkitLocalizationResolver(loader, source);
            await resolver.PrepareAsync("opening");

            var actual = resolver.ResolveCommandField("opening", 7, "body", "JSON Source");

            Assert.AreEqual(expected, actual);
            Assert.IsNotEmpty(actual);
        }

        [Test]
        public async Task LanguageChangePublishesNewScopeForNextResolve()
        {
            var loader = new FakeDictionaryLoader("Skit Japanese", "Skit English");
            loader.Set("french", SkitKey, "Skit French");
            var source = new FakeLocalizationSource("", "");
            using var resolver = new SkitLocalizationResolver(loader, source);
            await resolver.PrepareAsync("opening");

            source.SetLanguage("french");
            await UniTask.WaitUntil(() =>
                resolver.ResolveCommandField("opening", 7, "body", "Source") == "Skit French");

            Assert.AreEqual(
                "Skit French",
                resolver.ResolveCommandField("opening", 7, "body", "Source"));
        }

        [Test]
        public async Task CharacterNameUsesGuidKeyUnlessCommandOverridesSpeaker()
        {
            var loader = new FakeDictionaryLoader("", "");
            var source = new FakeLocalizationSource("", "");
            source.Set("japanese", "character.01234567-89ab-cdef-0123-456789abcdef.name", "話者");
            source.Set("japanese", "skit.opening.7.overrideCharacterName", "謎の声");
            using var resolver = new SkitLocalizationResolver(loader, source);
            await resolver.PrepareAsync("opening");

            var normal = resolver.ResolveCharacterName(
                "chr_001", "opening", 7, false, "Source Character");
            var overridden = resolver.ResolveCharacterName(
                "chr_001", "opening", 7, true, "???");

            Assert.AreEqual("話者", normal);
            Assert.AreEqual("謎の声", overridden);
        }

        private sealed class FakeDictionaryLoader : ISkitLocalizationDictionaryLoader
        {
            private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _values = new();

            public FakeDictionaryLoader(string target, string english)
            {
                Set("japanese", SkitKey, target);
                Set("english", SkitKey, english);
            }

            public void Set(string languageCode, string key, string value)
            {
                _values[languageCode] = new Dictionary<string, string> { { key, value } };
            }

            public UniTask<IReadOnlyDictionary<string, string>> LoadAsync(string languageCode)
            {
                return UniTask.FromResult(_values[languageCode]);
            }
        }

        private sealed class FakeLocalizationSource : ISkitLocalizationSource
        {
            private readonly Subject<Unit> _languageChanged = new();
            private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _values = new();

            private string _currentLanguageCode = "japanese";

            public FakeLocalizationSource(string target, string english)
            {
                _values["japanese"] = new Dictionary<string, string> { { SkitKey, target } };
                _values["english"] = new Dictionary<string, string> { { SkitKey, english } };
                _values["french"] = new Dictionary<string, string>();
            }

            public string GetCurrentLanguageCode()
            {
                return _currentLanguageCode;
            }

            public IObservable<Unit> GetLanguageChanged()
            {
                return _languageChanged;
            }

            public bool TryGetDictionary(
                string languageCode,
                out IReadOnlyDictionary<string, string> dictionary)
            {
                return _values.TryGetValue(languageCode, out dictionary);
            }

            public void Set(string languageCode, string key, string value)
            {
                var values = new Dictionary<string, string>(_values[languageCode])
                {
                    [key] = value,
                };
                _values[languageCode] = values;
            }

            public SkitCharacterLocalizationIdentity GetCharacterIdentity(string characterId)
            {
                return new SkitCharacterLocalizationIdentity(
                    "character.01234567-89ab-cdef-0123-456789abcdef.name",
                    "Source Character");
            }

            public void SetLanguage(string languageCode)
            {
                _currentLanguageCode = languageCode;
                _languageChanged.OnNext(Unit.Default);
            }
        }
    }
}
