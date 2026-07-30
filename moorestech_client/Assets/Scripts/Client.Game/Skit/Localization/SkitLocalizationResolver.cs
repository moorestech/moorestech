using System;
using System.Threading;
using Client.Localization;
using Client.Skit.Localization;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.Game.Skit.Localization
{
    public sealed class SkitLocalizationResolver : ISkitLocalizationResolver, IDisposable
    {
        private readonly ISkitLocalizationDictionaryLoader _loader;
        private readonly ISkitLocalizationSource _source;
        private readonly SkitLocalizationDictionaryComposer _dictionaryComposer;
        private SkitLocalizationScope _publishedScope = SkitLocalizationScope.Empty;
        private IDisposable _languageSubscription;
        private int _observedRevision;
        private int _publishedRevision = -1;
        private int _highestScheduledRevision = -1;
        private int _prepareRunning;
        private int _disposed;

        public SkitLocalizationResolver()
            : this(
                new SkitLocalizationDictionaryLoader(),
                new LocalizeSkitLocalizationSource())
        {
        }

        public SkitLocalizationResolver(
            ISkitLocalizationDictionaryLoader loader,
            ISkitLocalizationSource source)
        {
            _loader = loader;
            _source = source;
            _dictionaryComposer = new SkitLocalizationDictionaryComposer(source);
        }

        public async UniTask PrepareAsync(string skitTitle)
        {
            if (Interlocked.CompareExchange(ref _prepareRunning, 1, 0) != 0)
            {
                throw new InvalidOperationException(
                    "Skit localization preparation is already running.");
            }
            var attemptedRevision = -1;
            var converged = false;
            try
            {
                _languageSubscription ??= _source.GetLanguageChanged()
                    .Subscribe(_ => RequestReload());
                // Prepare中の通知も含む最新revisionが公開されるまで収束させる
                // Converge until the latest revision, including changes during Prepare, is published
                while (true)
                {
                    if (Volatile.Read(ref _disposed) != 0) return;
                    var revision = Volatile.Read(ref _observedRevision);
                    if (Volatile.Read(ref _publishedRevision) != revision)
                    {
                        attemptedRevision = revision;
                        await BuildAndPublishScopeAsync(revision);
                        if (Volatile.Read(ref _disposed) != 0) return;
                    }
                    if (revision == Volatile.Read(ref _observedRevision) &&
                        revision == Volatile.Read(ref _publishedRevision))
                    {
                        converged = true;
                        return;
                    }
                }
            }
            finally
            {
                Volatile.Write(ref _prepareRunning, 0);
                if (converged ||
                    0 <= attemptedRevision &&
                    attemptedRevision < Volatile.Read(ref _observedRevision))
                {
                    SchedulePendingReload();
                }
            }

            #region Internal

            void RequestReload()
            {
                if (Volatile.Read(ref _disposed) != 0) return;
                Interlocked.Increment(ref _observedRevision);
                SchedulePendingReload();
            }

            void SchedulePendingReload()
            {
                if (Volatile.Read(ref _prepareRunning) != 0 ||
                    Volatile.Read(ref _disposed) != 0)
                {
                    return;
                }
                var revision = Volatile.Read(ref _observedRevision);
                while (true)
                {
                    var scheduledRevision = Volatile.Read(ref _highestScheduledRevision);
                    if (revision <= scheduledRevision ||
                        revision == Volatile.Read(ref _publishedRevision))
                    {
                        return;
                    }

                    if (Interlocked.CompareExchange(
                            ref _highestScheduledRevision,
                            revision,
                            scheduledRevision) == scheduledRevision)
                    {
                        BuildAndPublishScopeAsync(revision).Forget(Debug.LogException);
                        return;
                    }
                }
            }

            async UniTask BuildAndPublishScopeAsync(int revision)
            {
                var targetLanguageCode = _source.GetCurrentLanguageCode();
                var targetSkit = await _loader.LoadAsync(targetLanguageCode);
                if (Volatile.Read(ref _disposed) != 0) return;
                // 英語選択時は同じAddressableを再ロードせず最初の結果を共有する
                // Reuse the first result when English is selected instead of loading the same Addressable twice
                var englishSkit = targetLanguageCode == Localize.DefaultLanguageCode
                    ? targetSkit
                    : await _loader.LoadAsync(Localize.DefaultLanguageCode);
                if (Volatile.Read(ref _disposed) != 0) return;
                // 空mod値を欠落として扱い、Skit値は未定義keyだけへ追加する
                // Treat empty mod values as missing and add Skit values only to absent keys
                var candidate = _dictionaryComposer.Compose(
                    targetLanguageCode,
                    targetSkit,
                    englishSkit);
                if (Volatile.Read(ref _disposed) == 0 &&
                    revision == Volatile.Read(ref _observedRevision))
                {
                    Volatile.Write(ref _publishedScope, candidate);
                    Volatile.Write(ref _publishedRevision, revision);
                }
            }

            #endregion
        }

        public string ResolveCommandField(
            string skitTitle,
            int commandId,
            string field,
            string sourceText)
        {
            var key = SkitCommandLocalization.CreateKey(skitTitle, commandId, field);
            return Volatile.Read(ref _publishedScope).Resolve(key, sourceText);
        }

        public string ResolveCharacterName(
            string characterId,
            string skitTitle,
            int commandId,
            bool useOverride,
            string overrideSource)
        {
            if (useOverride)
            {
                return ResolveCommandField(
                    skitTitle,
                    commandId,
                    SkitCommandLocalization.OverrideCharacterNameField,
                    overrideSource);
            }

            var identity = _source.GetCharacterIdentity(characterId);
            return Volatile.Read(ref _publishedScope).Resolve(identity.Key, identity.SourceText);
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _disposed, 1);
            Interlocked.Increment(ref _observedRevision);
            _languageSubscription?.Dispose();
        }
    }
}
