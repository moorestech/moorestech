using MainGame.Basic;
using MainGame.Network;
using UniRx;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace MainGame.Presenter.PauseMenu
{
    public class NetworkDisconnectPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject disconnectPanel;

        [SerializeField] private Button goToMainMenuButton;


        [Inject]
        public void Construct(ConnectionServer connectionServer)
        {
            connectionServer.OnDisconnect.Subscribe(_ => { disconnectPanel.gameObject.SetActive(true); }).AddTo(this);
            goToMainMenuButton.onClick.AddListener(() => { SceneManager.LoadScene(SceneConstant.MainMenuSceneName); });
        }
    }
}