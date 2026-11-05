using System;
using System.Collections.Generic;
using Client.Localization;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// ローカライズキーと引数を持ち、パネルにいる間は言語変更で解決し直すラベル（plan.md §3.9）
    /// Label holding a localization key and params that re-resolves on language change while on a panel (plan.md §3.9)
    /// </summary>
    public class LocalizedLabel : Label
    {
        private static readonly IReadOnlyList<LocalizationTextParam> NoParams = Array.Empty<LocalizationTextParam>();
        private LocalizationKey _key;
        private IReadOnlyList<LocalizationTextParam> _textParams = NoParams;
        private bool _hasKey;
        private IDisposable _languageSubscription;

        public LocalizedLabel()
        {
            RegisterCallback<AttachToPanelEvent>(_ => OnAttached());
            RegisterCallback<DetachFromPanelEvent>(_ => OnDetached());
        }

        public void SetKey(LocalizationKey key)
        {
            SetKey(key, NoParams);
        }

        public void SetKey(LocalizationKey key, IReadOnlyList<LocalizationTextParam> textParams)
        {
            _key = key;
            _textParams = textParams;
            _hasKey = true;
            if (panel != null) Resolve();
        }

        private void OnAttached()
        {
            // キー無しで載ったら生の文言を出さず、開発者に知らせる
            // Report a label attached without a key instead of showing raw text
            if (!_hasKey) Debug.LogError($"[LocalizedLabel] attached without a key: {name}");
            _languageSubscription?.Dispose();
            _languageSubscription = Localize.OnLanguageChanged.Subscribe(_ => Resolve());
            Resolve();
        }

        private void OnDetached()
        {
            _languageSubscription?.Dispose();
            _languageSubscription = null;
        }

        private void Resolve()
        {
            if (!_hasKey) return;
            text = _textParams.Count == 0 ? Localize.Get(_key) : Localize.GetFormatted(_key, _textParams);
        }
    }
}
