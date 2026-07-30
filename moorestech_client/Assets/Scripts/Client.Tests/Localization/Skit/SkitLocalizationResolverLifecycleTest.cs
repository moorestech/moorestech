using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Client.Game.Skit.Localization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.Localization.Skit
{
    public class SkitLocalizationResolverLifecycleTest
    {
        private const string SkitKey = "skit.opening.7.body";

        [Test]
        public async Task ConcurrentPrepareFailsFastWithoutDuplicateLoad()
        {
            var loader = CreateLoader();
            var initialGate = loader.GateNext("japanese");
            var source = new FakeSkitLocalizationSource();
            using var resolver = new SkitLocalizationResolver(loader, source);
            var firstPrepare = resolver.PrepareAsync("opening");
            await UniTask.WaitUntil(() => loader.GetLoadCount("japanese") == 1);
            try
            {
                Assert.ThrowsAsync<InvalidOperationException>(
                    async () => await resolver.PrepareAsync("opening"));
                Assert.AreEqual(1, loader.GetLoadCount("japanese"));
            }
            finally
            {
                initialGate.TrySetResult(
                    new Dictionary<string, string> { { SkitKey, "Japanese" } });
                await firstPrepare;
            }
        }

        [Test]
        public async Task DisposeDuringPrepareCompletesWithoutPublishingOrFurtherLoads()
        {
            var loader = CreateLoader();
            var initialGate = loader.GateNext("japanese");
            var source = new FakeSkitLocalizationSource();
            var resolver = new SkitLocalizationResolver(loader, source);
            var prepare = resolver.PrepareAsync("opening");
            await UniTask.WaitUntil(() => loader.GetLoadCount("japanese") == 1);
            try
            {
                resolver.Dispose();
                initialGate.TrySetResult(
                    new Dictionary<string, string> { { SkitKey, "Disposed Japanese" } });
                await prepare;
                Assert.AreEqual(1, loader.GetLoadCount("japanese"));
                Assert.AreEqual(0, loader.GetLoadCount("english"));
                Assert.AreEqual("Source", Resolve(resolver));
            }
            finally
            {
                resolver.Dispose();
                initialGate.TrySetResult(new Dictionary<string, string>());
            }
        }

        [Test]
        public async Task FailedPrepareDoesNotScheduleTheSameRevisionAgain()
        {
            var loader = CreateLoader();
            var initialGate = loader.GateNext("japanese");
            var source = new FakeSkitLocalizationSource();
            using var resolver = new SkitLocalizationResolver(loader, source);
            var prepare = resolver.PrepareAsync("opening");
            await UniTask.WaitUntil(() => loader.GetLoadCount("japanese") == 1);
            try
            {
                initialGate.TrySetException(new InvalidOperationException("load failed"));
                Assert.ThrowsAsync<InvalidOperationException>(async () => await prepare);
                await UniTask.Yield();
                Assert.AreEqual(1, loader.GetLoadCount("japanese"));
            }
            finally
            {
                initialGate.TrySetResult(new Dictionary<string, string>());
            }
        }

        [Test]
        public async Task FailedPrepareSchedulesOnlyNewerObservedRevision()
        {
            var loader = CreateLoader();
            loader.Set("french", SkitKey, "French");
            var initialGate = loader.GateNext("japanese");
            var source = new FakeSkitLocalizationSource();
            using var resolver = new SkitLocalizationResolver(loader, source);
            var prepare = resolver.PrepareAsync("opening");
            await UniTask.WaitUntil(() => loader.GetLoadCount("japanese") == 1);
            try
            {
                source.SetLanguage("french");
                initialGate.TrySetException(new InvalidOperationException("load failed"));
                Assert.ThrowsAsync<InvalidOperationException>(async () => await prepare);
                await UniTask.WaitUntil(() => Resolve(resolver) == "French");
                Assert.AreEqual(1, loader.GetLoadCount("japanese"));
                Assert.AreEqual(1, loader.GetLoadCount("french"));
            }
            finally
            {
                initialGate.TrySetResult(new Dictionary<string, string>());
            }
        }

        [Test]
        public async Task SubscriptionFailureReleasesConcurrentPrepareGuard()
        {
            var loader = CreateLoader();
            var source = new FakeSkitLocalizationSource();
            source.FailNextLanguageChangedSubscription(
                new InvalidOperationException("subscription failed"));
            using var resolver = new SkitLocalizationResolver(loader, source);

            Assert.ThrowsAsync<InvalidOperationException>(
                async () => await resolver.PrepareAsync("opening"));
            await resolver.PrepareAsync("opening");

            Assert.AreEqual("Japanese", Resolve(resolver));
        }

        private static FakeSkitDictionaryLoader CreateLoader()
        {
            var loader = new FakeSkitDictionaryLoader();
            loader.Set("japanese", SkitKey, "Japanese");
            loader.Set("english", SkitKey, "English");
            return loader;
        }

        private static string Resolve(SkitLocalizationResolver resolver)
        {
            return resolver.ResolveCommandField("opening", 7, "body", "Source");
        }
    }
}
