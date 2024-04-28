using Client.Common;
using Client.Game.Context;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MainGame.Presenter.PauseMenu
{
    public class NetworkDisconnectPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject disconnectPanel;

        [SerializeField] private Button goToMainMenuButton;

        private void Start()
        {
            MoorestechContext.VanillaApi.OnDisconnect.Subscribe(_ => { disconnectPanel.gameObject.SetActive(true); }).AddTo(this);
            goToMainMenuButton.onClick.AddListener(() => { SceneManager.LoadScene(SceneConstant.MainMenuSceneName); });
        }
    }
}