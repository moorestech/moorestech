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
        private int _reloadVersion;

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
            var version = Interlocked.Increment(ref _reloadVersion);
            await BuildAndPublishScopeAsync(version);

            // 実行scopeの準備後だけ言語変更を購読する
            // Subscribe to language changes only after the execution scope is ready
            _languageSubscription ??= _source.GetLanguageChanged()
                .Subscribe(_ => ReloadForLanguageChangeAsync().Forget());
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
            Interlocked.Increment(ref _reloadVersion);
            _languageSubscription?.Dispose();
        }

        private async UniTask BuildAndPublishScopeAsync(int version)
        {
            var targetLanguageCode = _source.GetCurrentLanguageCode();
            var targetSkit = await _loader.LoadAsync(targetLanguageCode);
            var englishSkit = await _loader.LoadAsync(Localize.DefaultLanguageCode);
            var target = CopyModDictionary(targetLanguageCode);
            var english = CopyModDictionary(Localize.DefaultLanguageCode);

            // 空mod値を欠落として扱い、Skit値は未定義keyだけへ追加する
            // Treat empty mod values as missing and add Skit values only to absent keys
            AddMissingNonEmpty(target, targetSkit);
            AddMissingNonEmpty(english, englishSkit);
            var candidate = new SkitLocalizationScope(target, english);
            if (version == Volatile.Read(ref _reloadVersion))
            {
                Volatile.Write(ref _publishedScope, candidate);
            }
        }

        private async UniTaskVoid ReloadForLanguageChangeAsync()
        {
            if (string.IsNullOrEmpty(_skitTitle)) return;

            var version = Interlocked.Increment(ref _reloadVersion);
            await BuildAndPublishScopeAsync(version);
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
