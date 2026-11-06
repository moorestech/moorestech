// moorestech_client/Assets/Scripts/Client.Ui/Foundation/Text/LocalizedTextBinding.cs
using System;
using Client.Localization;
using Client.Ui.State.Text;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Text
{
    /// <summary>
    /// 宿主がパネルにいる間 UiText を解く
    /// Resolves UiText while the host is on a panel
    /// </summary>
    /// <remarks>
    /// Label 1 つに入れない部品が使う
    /// For components not using a single Label
    /// </remarks>
    public sealed class LocalizedTextBinding
    {
        private readonly VisualElement _host;
        private readonly Subject<string> _onResolved = new();
        private UiText _text;
        private IDisposable _languageSubscription;

        public IObservable<string> OnResolved => _onResolved;

        public LocalizedTextBinding(VisualElement host)
        {
            _host = host;
            host.RegisterCallback<AttachToPanelEvent>(_ => OnAttached());
            host.RegisterCallback<DetachFromPanelEvent>(_ => OnDetached());
        }

        public void SetText(UiText text)
        {
            _text = text;
            // 宿主が載った後に作られた束縛（C2-b の置き文字）は AttachToPanelEvent を受けないので、ここで購読を始める
            // A binding created after its host attached (C2-b placeholder) never sees AttachToPanelEvent, so subscribe here
            if (_host.panel == null) return;
            EnsureSubscribed();
            Resolve();
        }

        private void OnAttached()
        {
            // 載った後に SetText する使い方を許すため、1 フレーム待っても文が無ければ開発者に知らせる（空のまま黙って出さない）
            // Allow SetText right after mounting: if there is still no text one frame later, report it instead of staying silently empty
            _host.schedule.Execute(ReportMissingText);
            EnsureSubscribed();
            Resolve();
        }

        private void ReportMissingText()
        {
            // silent-ok: 文が届いた / 既に外れた宿主は報告不要
            if (_text != null || _host.panel == null) return;
            Debug.LogError($"[LocalizedTextBinding] attached without a text: {_host.name}");
        }

        private void EnsureSubscribed()
        {
            if (_languageSubscription != null) return;
            _languageSubscription = Localize.OnLanguageChanged.Subscribe(_ => Resolve());
        }

        private void OnDetached()
        {
            _languageSubscription?.Dispose();
            _languageSubscription = null;
        }

        private void Resolve()
        {
            // silent-ok: 文が来るまで流さない / 載って 1 フレーム後も無ければ ReportMissingText が LogError を出す
            if (_text == null) return;
            _onResolved.OnNext(_text.Resolve());
        }
    }
}
