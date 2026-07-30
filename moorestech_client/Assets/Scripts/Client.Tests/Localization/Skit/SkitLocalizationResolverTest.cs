using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Game.Skit.Localization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

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
            var loader = CreateLoader(skitTarget, skitEnglish);
            var source = CreateSource(modTarget, modEnglish);
            using var resolver = new SkitLocalizationResolver(loader, source);
            await resolver.PrepareAsync("opening");

            var actual = resolver.ResolveCommandField("opening", 7, "body", "JSON Source");

            Assert.AreEqual(expected, actual);
            Assert.IsNotEmpty(actual);
        }

        [Test]
        public async Task LanguageChangePublishesNewScopeForNextResolve()
        {
            var loader = CreateLoader("Skit Japanese", "Skit English");
            loader.Set("french", SkitKey, "Skit French");
            var source = CreateSource("", "");
            using var resolver = new SkitLocalizationResolver(loader, source);
            await resolver.PrepareAsync("opening");

            source.SetLanguage("french");
            await UniTask.WaitUntil(() =>
                    resolver.ResolveCommandField("opening", 7, "body", "Source") == "Skit French")
                .Timeout(TimeSpan.FromSeconds(2));

            Assert.AreEqual(
                "Skit French",
                resolver.ResolveCommandField("opening", 7, "body", "Source"));
        }

        [Test]
        public async Task CharacterNameUsesGuidKeyUnlessCommandOverridesSpeaker()
        {
            var loader = CreateLoader("", "");
            var source = CreateSource("", "");
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

        [Test]
        public async Task PrepareWaitsForLanguageChangeObservedDuringInitialLoad()
        {
            var loader = CreateLoader("Initial Japanese", "English");
            loader.Set("french", SkitKey, "French");
            var initialGate = loader.GateNext("japanese");
            var source = CreateSource("", "");
            using var resolver = new SkitLocalizationResolver(loader, source);

            var prepare = resolver.PrepareAsync("opening");
            await UniTask.WaitUntil(() => loader.GetLoadCount("japanese") == 1)
                .Timeout(TimeSpan.FromSeconds(2));
            source.SetLanguage("french");
            initialGate.TrySetResult(new Dictionary<string, string>
                { { SkitKey, "Stale Japanese" } });
            await prepare;

            Assert.AreEqual("French", Resolve(resolver));
        }

        [Test]
        public async Task SameLanguageDictionaryChangeReloadsScope()
        {
            var loader = CreateLoader("", "English");
            var source = CreateSource("Before", "");
            using var resolver = new SkitLocalizationResolver(loader, source);
            await resolver.PrepareAsync("opening");

            source.Set("japanese", SkitKey, "After");
            source.PublishDictionaryChange();
            await UniTask.WaitUntil(() => Resolve(resolver) == "After")
                .Timeout(TimeSpan.FromSeconds(2));

            Assert.AreEqual("After", Resolve(resolver));
        }

        [Test]
        public async Task StaleDelayedReloadCannotOverwriteNewerLanguage()
        {
            var loader = CreateLoader("Japanese", "English");
            loader.Set("french", SkitKey, "French");
            loader.Set("german", SkitKey, "German");
            var source = CreateSource("", "");
            using var resolver = new SkitLocalizationResolver(loader, source);
            await resolver.PrepareAsync("opening");
            var frenchGate = loader.GateNext("french");

            source.SetLanguage("french");
            await UniTask.WaitUntil(() => loader.GetLoadCount("french") == 1)
                .Timeout(TimeSpan.FromSeconds(2));
            source.SetLanguage("german");
            await UniTask.WaitUntil(() => Resolve(resolver) == "German")
                .Timeout(TimeSpan.FromSeconds(2));
            frenchGate.TrySetResult(new Dictionary<string, string> { { SkitKey, "Stale French" } });
            await UniTask.Yield();

            Assert.AreEqual("German", Resolve(resolver));
        }

        private static FakeSkitDictionaryLoader CreateLoader(string target, string english)
        {
            var loader = new FakeSkitDictionaryLoader();
            loader.Set("japanese", SkitKey, target);
            loader.Set("english", SkitKey, english);
            return loader;
        }

        private static FakeSkitLocalizationSource CreateSource(string target, string english)
        {
            var source = new FakeSkitLocalizationSource();
            source.Set("japanese", SkitKey, target);
            source.Set("english", SkitKey, english);
            return source;
        }

        private static string Resolve(SkitLocalizationResolver resolver)
        {
            return resolver.ResolveCommandField("opening", 7, "body", "Source");
        }
    }
}
