using System;
using Client.Localization;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// UiTextを持つ言語追従ラベル
    /// Label holding UiText that follows language
    /// </summary>
    public class LocalizedLabel : Label
    {
        private UiText _text;
        private IDisposable _languageSubscription;

        public LocalizedLabel()
        {
            RegisterCallback<AttachToPanelEvent>(_ => OnAttached());
            RegisterCallback<DetachFromPanelEvent>(_ => OnDetached());
        }

        public void SetKey(LocalizationKey key)
        {
            SetText(new UiText(key));
        }

        public void SetText(UiText text)
        {
            _text = text;
            if (panel != null) Resolve();
        }

        private void OnAttached()
        {
            // 文が無ければ警告
            // Warn when no text
            if (_text == null) Debug.LogError($"[LocalizedLabel] attached without a text: {name}");
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
            // silent-ok: 文が無いときは OnAttached が LogError 済み / 開発者が Console で気づく
            if (_text == null) return;
            text = _text.Resolve();
        }
    }
}
