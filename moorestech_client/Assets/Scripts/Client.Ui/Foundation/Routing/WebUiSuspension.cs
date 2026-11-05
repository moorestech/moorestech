using System;
using Client.Input;
using Client.Ui.Foundation.Root;
using Client.Ui.State.Routing;
using UniRx;
using VContainer.Unity;

namespace Client.Ui.Foundation.Routing
{
    /// <summary>
    /// 画面ルータの Web 休止判断に従い、CEF の絵と Unity 側の Web 入力値を切り替える（R1）
    /// Follows the router's web-suspension decision to toggle the CEF picture and the Unity-side web input values (R1)
    /// </summary>
    public class WebUiSuspension : IInitializable, IDisposable
    {
        private readonly UiRoutingState _routingState;
        private readonly GameUiRoot _gameUiRoot;
        private IDisposable _subscription;

        public WebUiSuspension(UiRoutingState routingState, GameUiRoot gameUiRoot)
        {
            _routingState = routingState;
            _gameUiRoot = gameUiRoot;
        }

        public void Initialize()
        {
            // ReactiveProperty は代入の場で同期発火し、ui_state の配信はフレーム末へ遅らせるので、解除の旗はいつも配信より先に下りる
            // ReactiveProperty notifies synchronously on assignment while ui_state publishes at frame end, so the flag always clears before the publish
            _subscription = _routingState.Current
                .Select(snapshot => snapshot.WebUiSuspended)
                .DistinctUntilChanged()
                .Subscribe(Apply);
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }

        private void Apply(bool suspended)
        {
            // cef-unity は RawImage の有効を見ずに入力を CEF へ転送し、止める API も無い。受け手は Web 側の全 unmount で消す（既知の穴）
            // cef-unity forwards input to CEF regardless of RawImage.enabled and offers no stop API; the web's full unmount removes the receiver instead (known gap)
            _gameUiRoot.SetCefVisible(!suspended);
            WebUiInputExclusivity.SetWebUiSuspended(suspended);
        }
    }
}
