using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.Context;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Client.Game.InGame.Presenter.PauseMenu
{
    public class NetworkDisconnectPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject disconnectPanel;
        
        [SerializeField] private Button goToMainMenuButton;
        
        private void Start()
        {
            ClientContext.VanillaApi.OnDisconnect.Subscribe(_ => { disconnectPanel.gameObject.SetActive(true); }).AddTo(this);
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