using System;
using System.Collections.Generic;
using System.Threading;
using Client.Localization;
using Client.Skit.Localization;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.Game.Skit.Localization
{
    public sealed class SkitLocalizationResolver : ISkitLocalizationResolver, IDisposable
    {
        private readonly ISkitLocalizationDictionaryLoader _loader;
        private readonly ISkitLocalizationSource _source;
        private SkitLocalizationScope _publishedScope = SkitLocalizationScope.Empty;
        private IDisposable _languageSubscription;
        private string _skitTitle;
        private int _observedRevision;
        private int _publishedRevision = -1;
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
        }

        public async UniTask PrepareAsync(string skitTitle)
        {
            _skitTitle = skitTitle;
            _languageSubscription ??= _source.GetLanguageChanged()
                .Subscribe(_ => RequestReload());

            // Prepare中の通知も含む最新revisionが公開されるまで収束させる
            // Converge until the latest revision, including changes during Prepare, is published
            while (true)
            {
                var revision = Volatile.Read(ref _observedRevision);
                if (Volatile.Read(ref _publishedRevision) != revision)
                {
                    await BuildAndPublishScopeAsync(revision);
                }

                if (revision == Volatile.Read(ref _observedRevision) &&
                    revision == Volatile.Read(ref _publishedRevision))
                {
                    return;
                }
            }
        }

        public string ResolveCommandField(
            string skitTitle,
            int commandId,
            string field,
            string sourceText)
        {
            var key = ContentLocalizationKeys.SkitField(skitTitle, commandId, field);
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
                    "overrideCharacterName",
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

        private async UniTask BuildAndPublishScopeAsync(int revision)
        {
            var targetLanguageCode = _source.GetCurrentLanguageCode();
            var targetSkit = await _loader.LoadAsync(targetLanguageCode);

            // 英語選択時は同じAddressableを再ロードせず最初の結果を共有する
            // Reuse the first result when English is selected instead of loading the same Addressable twice
            var englishSkit = targetLanguageCode == Localize.DefaultLanguageCode
                ? targetSkit
                : await _loader.LoadAsync(Localize.DefaultLanguageCode);
            var target = CopyModDictionary(targetLanguageCode);
            var english = CopyModDictionary(Localize.DefaultLanguageCode);

            // 空mod値を欠落として扱い、Skit値は未定義keyだけへ追加する
            // Treat empty mod values as missing and add Skit values only to absent keys
            AddMissingNonEmpty(target, targetSkit);
            AddMissingNonEmpty(english, englishSkit);
            var candidate = new SkitLocalizationScope(target, english);
            if (Volatile.Read(ref _disposed) == 0 &&
                revision == Volatile.Read(ref _observedRevision))
            {
                Volatile.Write(ref _publishedScope, candidate);
                Volatile.Write(ref _publishedRevision, revision);
            }
        }

        private void RequestReload()
        {
            if (string.IsNullOrEmpty(_skitTitle) || Volatile.Read(ref _disposed) != 0) return;

            var revision = Interlocked.Increment(ref _observedRevision);
            ReloadRevisionAsync(revision).Forget();
        }

        private async UniTaskVoid ReloadRevisionAsync(int revision)
        {
            await BuildAndPublishScopeAsync(revision);
        }

        private Dictionary<string, string> CopyModDictionary(string languageCode)
        {
            var result = new Dictionary<string, string>();
            if (!_source.TryGetDictionary(languageCode, out var dictionary)) return result;

            foreach (var pair in dictionary)
            {
                if (!string.IsNullOrEmpty(pair.Value))
                {
                    result.Add(pair.Key, pair.Value);
                }
            }

            return result;
        }

        private static void AddMissingNonEmpty(
            Dictionary<string, string> destination,
            IReadOnlyDictionary<string, string> skitDictionary)
        {
            foreach (var pair in skitDictionary)
            {
                if (!string.IsNullOrEmpty(pair.Value) && !destination.ContainsKey(pair.Key))
                {
                    destination.Add(pair.Key, pair.Value);
                }
            }
        }
    }
}
