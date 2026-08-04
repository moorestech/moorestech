using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Client.Game.Skit.Localization;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

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
            await UniTask.WaitUntil(() => loader.GetLoadCount("japanese") == 1)
                .Timeout(TimeSpan.FromSeconds(2));
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
            await UniTask.WaitUntil(() => loader.GetLoadCount("japanese") == 1)
                .Timeout(TimeSpan.FromSeconds(2));
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
            await UniTask.WaitUntil(() => loader.GetLoadCount("japanese") == 1)
                .Timeout(TimeSpan.FromSeconds(2));
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
            await UniTask.WaitUntil(() => loader.GetLoadCount("japanese") == 1)
                .Timeout(TimeSpan.FromSeconds(2));
            try
            {
                source.SetLanguage("french");
                initialGate.TrySetException(new InvalidOperationException("load failed"));
                Assert.ThrowsAsync<InvalidOperationException>(async () => await prepare);
                await UniTask.WaitUntil(() => Resolve(resolver) == "French")
                    .Timeout(TimeSpan.FromSeconds(2));
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

        [Test]
        public async Task FailedReloadKeepsPublishedScopeAndRecoversOnNextLanguageChange()
        {
            var loader = CreateLoader();
            loader.Set("french", SkitKey, "French");
            loader.Set("german", SkitKey, "German");
            var source = new FakeSkitLocalizationSource();
            using var resolver = new SkitLocalizationResolver(loader, source);
            await resolver.PrepareAsync("opening");
            var frenchGate = loader.GateNext("french");

            // 再ロード失敗は公開済みscopeを壊さず、次の言語変更で再試行できる
            // A failed reload leaves the published scope intact and retries on the next language change
            LogAssert.Expect(LogType.Exception, new Regex(".*reload failed.*"));
            source.SetLanguage("french");
            await UniTask.WaitUntil(() => loader.GetLoadCount("french") == 1)
                .Timeout(TimeSpan.FromSeconds(2));
            frenchGate.TrySetException(new InvalidOperationException("reload failed"));
            await UniTask.DelayFrame(5);
            Assert.AreEqual("Japanese", Resolve(resolver));

            source.SetLanguage("german");
            await UniTask.WaitUntil(() => Resolve(resolver) == "German")
                .Timeout(TimeSpan.FromSeconds(2));

            Assert.AreEqual("German", Resolve(resolver));
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
            return resolver.ResolveCommandField(7, "body", "Source");
        }
    }
}
