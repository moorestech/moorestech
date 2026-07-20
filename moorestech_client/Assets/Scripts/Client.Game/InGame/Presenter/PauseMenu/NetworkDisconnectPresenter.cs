using System;
using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Client.Game.InGame.Presenter.PauseMenu
{
    public class NetworkDisconnectPresenter : MonoBehaviour
    {
        private readonly ReactiveProperty<bool> _isDisconnected = new(false);

        [SerializeField] private GameObject disconnectPanel;
        
        [SerializeField] private Button goToMainMenuButton;

        public bool IsDisconnected => _isDisconnected.Value;
        public IObservable<bool> OnDisconnectedChanged => _isDisconnected;
        
        private void Start()
        {
            ClientContext.VanillaApi.OnDisconnect.Subscribe(_ =>
            {
                _isDisconnected.Value = true;
                disconnectPanel.gameObject.SetActive(!WebUiScreenGate.IsWebUiMode);
            }).AddTo(this);
            goToMainMenuButton.onClick.AddListener(() =>
            {
                // 遷移前にゲーム終了イベントを発火し WebUiHost 停止と購読解除を行う（BackToMainMenu と同順序・2-D）
                // Fire game shutdown before transitioning so WebUiHost stops and subscriptions release (same order as BackToMainMenu, 2-D)
                GameShutdownEvent.FireGameShutdown();
                SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
            });
        }
    }
}
