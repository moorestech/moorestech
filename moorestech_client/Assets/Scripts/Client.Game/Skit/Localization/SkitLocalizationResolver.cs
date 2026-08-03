using System;
using Client.Localization;
using Client.Skit.Localization;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.Game.Skit.Localization
{
    /// <summary>
    /// 生成から破棄まで全入口がメインスレッド専用のため、同期はboolとintの素の読み書きだけで足りる。
    /// Every entry point from construction to disposal is main-thread only, so plain bool and int access suffices.
    /// </summary>
    public sealed class SkitLocalizationResolver : ISkitLocalizationResolver, IDisposable
    {
        private readonly ISkitLocalizationDictionaryLoader _loader;
        private readonly ISkitLocalizationSource _source;
        private readonly SkitLocalizationDictionaryComposer _dictionaryComposer;
        private SkitLocalizationScope _publishedScope = SkitLocalizationScope.Empty;
        private IDisposable _languageSubscription;
        private string _skitTitle;
        private int _observedRevision;
        private int _publishedRevision = -1;
        private bool _preparing;
        private bool _reloadScheduled;
        private bool _disposed;

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
            if (_preparing)
            {
                throw new InvalidOperationException(
                    "Skit localization preparation is already running.");
            }
            _skitTitle = skitTitle;
            _preparing = true;
            var attemptedRevision = -1;
            try
            {
                _languageSubscription ??= _source.GetLanguageChanged()
                    .Subscribe(_ => RequestReload());
                // Prepare中の通知も含む最新revisionが公開されるまで収束させる
                // Converge until the latest revision, including changes during Prepare, is published
                while (true)
                {
                    if (_disposed) return;
                    var revision = _observedRevision;
                    if (_publishedRevision != revision)
                    {
                        attemptedRevision = revision;
                        await BuildAndPublishScopeAsync(revision);
                        if (_disposed) return;
                    }
                    if (revision == _observedRevision && revision == _publishedRevision) return;
                }
            }
            finally
            {
                // 失敗したrevisionは即再試行せず、Prepare中に観測した新しいrevisionだけ再開する
                // Do not retry the failed revision immediately; resume only revisions observed during Prepare
                _preparing = false;
                if (0 <= attemptedRevision && attemptedRevision < _observedRevision)
                {
                    SchedulePendingReload();
                }
            }
        }

        public string ResolveCommandField(int commandId, string field, string sourceText)
        {
            var key = SkitCommandLocalization.CreateKey(_skitTitle, commandId, field);
            return _publishedScope.Resolve(key, sourceText);
        }

        public string ResolveCharacterName(string characterId)
        {
            var identity = _source.GetCharacterIdentity(characterId);
            return _publishedScope.Resolve(identity.Key, identity.SourceText);
        }

        public string ResolveOverriddenCharacterName(int commandId, string overrideSource)
        {
            return ResolveCommandField(
                commandId,
                SkitCommandLocalization.OverrideCharacterNameField,
                overrideSource);
        }

        public void Dispose()
        {
            _disposed = true;
            _languageSubscription?.Dispose();
        }

        private void RequestReload()
        {
            if (_disposed) return;
            _observedRevision++;
            SchedulePendingReload();
        }

        private void SchedulePendingReload()
        {
            if (_preparing || _disposed || _reloadScheduled) return;
            if (_observedRevision == _publishedRevision) return;
            _reloadScheduled = true;
            ReloadAtEndOfFrameAsync().Forget(Debug.LogException);

            #region Internal

            // 同フレーム多発を畳み込みつつ、ロード失敗後も次の言語変更が再試行できるよう先にフラグを戻す
            // Fold same-frame bursts, and clear the flag first so the next language change can retry after a failed load
            async UniTask ReloadAtEndOfFrameAsync()
            {
                await UniTask.Yield(PlayerLoopTiming.PostLateUpdate);
                _reloadScheduled = false;
                if (_disposed) return;
                await BuildAndPublishScopeAsync(_observedRevision);
            }

            #endregion
        }

        private async UniTask BuildAndPublishScopeAsync(int revision)
        {
            var targetLanguageCode = _source.GetCurrentLanguageCode();
            var targetSkit = await _loader.LoadAsync(targetLanguageCode);
            if (_disposed) return;
            // 英語選択時は同じAddressableを再ロードせず最初の結果を共有する
            // Reuse the first result when English is selected instead of loading the same Addressable twice
            var englishSkit = targetLanguageCode == Localize.DefaultLanguageCode
                ? targetSkit
                : await _loader.LoadAsync(Localize.DefaultLanguageCode);
            if (_disposed) return;
            // 空mod値を欠落として扱い、Skit値は未定義keyだけへ追加する
            // Treat empty mod values as missing and add Skit values only to absent keys
            var candidate = _dictionaryComposer.Compose(
                targetLanguageCode,
                targetSkit,
                englishSkit);
            if (_disposed || revision != _observedRevision) return;
            _publishedScope = candidate;
            _publishedRevision = revision;
        }
    }
}
