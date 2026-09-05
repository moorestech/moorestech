using System;
using Client.Game.InGame.Context;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.Presenter.PauseMenu
{
    /// <summary>
    ///     サーバー切断の論理状態。表示は Web のポーズメニューが pause_menu topic 経由で行う
    ///     Logical disconnect state; the web pause menu renders it through the pause_menu topic
    /// </summary>
    public class NetworkDisconnectState : IInitializable
    {
        private readonly ReactiveProperty<bool> _isDisconnected = new(false);

        public bool IsDisconnected => _isDisconnected.Value;
        public IObservable<bool> OnDisconnectedChanged => _isDisconnected;

        public void Initialize()
        {
            ClientContext.VanillaApi.OnDisconnect.Subscribe(_ => _isDisconnected.Value = true);
        }
    }
}
