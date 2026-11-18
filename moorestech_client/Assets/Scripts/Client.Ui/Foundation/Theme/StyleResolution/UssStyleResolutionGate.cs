using System;
using UniRx;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Foundation.Theme.StyleResolution
{
    /// <summary>
    /// USS のトークンを読む常駐の宿主の「解決待ち→解決の通知→配置後も未解決なら開発者へ」を持つ。預かる要求と読むトークンは宿主が持つ
    /// Holds "wait for resolution, announce it, tell developers if still unresolved after layout" for resident hosts reading USS tokens; held requests and token names stay with the host
    /// </summary>
    public sealed class UssStyleResolutionGate
    {
        private readonly string _ownerName;
        private readonly Subject<ICustomStyle> _onResolved = new();
        private bool _laidOutWithoutStyle;

        public bool IsResolved { get; private set; }
        public IObservable<ICustomStyle> OnResolved => _onResolved;

        public UssStyleResolutionGate(VisualElement host, string ownerName)
        {
            _ownerName = ownerName;
            host.RegisterCallback<CustomStyleResolvedEvent>(evt =>
            {
                IsResolved = true;
                _onResolved.OnNext(evt.customStyle);
            });
            host.RegisterCallback<GeometryChangedEvent>(_ =>
            {
                // 配置まで進んでも解決が来ないのは tss の @import 漏れかクラスの付け忘れ
                // Layout without resolution means a missing tss @import or a missing class
                if (IsResolved || _laidOutWithoutStyle) return;
                _laidOutWithoutStyle = true;
                Debug.LogError($"[{_ownerName}] custom style not resolved after layout; is the host's USS imported by GameUiTheme.tss and its class present?");
            });
        }

        // 解決前に預かった要求があるとき宿主が呼ぶ。配置前の預かりは一時的なので黙る
        // Hosts call this when holding a request before resolution; holding before layout is transient, so it stays quiet
        public void ReportHeldWhileUnresolved(string what)
        {
            // silent-ok: 配置前の預かりは解決を待つ正常系 / 配置後も未解決なら GeometryChanged とここが LogError を出す
            if (!_laidOutWithoutStyle) return;
            Debug.LogError($"[{_ownerName}] {what} is held because the custom style never resolved");
        }

        // 解決したがトークンが読めず、動き無しで出すとき宿主が毎回呼ぶ
        // Hosts call this every time they show something without motion because tokens resolved but were unreadable
        public void ReportUnreadable(string what)
        {
            Debug.LogError($"[{_ownerName}] motion tokens are unreadable; showing without motion: {what}");
        }
    }
}
