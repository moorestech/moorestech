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
    /// UiText（キーと引数）を 1 つ持ち、パネルにいる間は言語変更で解決し直すラベル（plan.md §3.9・設計書 C9）
    /// Label holding one UiText (key and params) that re-resolves on language change while on a panel (plan.md §3.9, design C9)
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
            // 文が無いまま載ったら生の文言を出さず、開発者に知らせる
            // Report a label attached without a text instead of showing raw text
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
