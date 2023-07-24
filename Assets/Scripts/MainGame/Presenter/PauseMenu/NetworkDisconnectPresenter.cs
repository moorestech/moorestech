using MainGame.Basic;
using MainGame.Network;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace MainGame.Presenter.PauseMenu
{
    public class NetworkDisconnectPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject disconnectPanel;
        
        [SerializeField] private Button goToMainMenuButton;
        
        [SerializeField] private TMP_Text disconnectMessage;
        [SerializeField] private TMP_Text disconnectStackTrace;

        [Inject]
        public void Construct(ConnectionServer connectionServer)
        {
            connectionServer.OnDisconnect.Subscribe(m =>
            {
                var message = m.message;
                var stackTrace = m.stackTrace;
                
                disconnectMessage.text = message;
                disconnectStackTrace.text = stackTrace;
                
                disconnectPanel.SetActive(true);
            }).AddTo(this);
            goToMainMenuButton.onClick.AddListener(() =>
            {
                SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
            });
        }
    }
}