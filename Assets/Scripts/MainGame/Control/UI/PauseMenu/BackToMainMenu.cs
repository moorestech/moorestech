using MainGame.Basic;
using MainGame.Network;
using MainGame.Network.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace MainGame.Control.UI.PauseMenu
{
    public class BackToMainMenu : MonoBehaviour
    {
        [SerializeField] private Button backToMainMenuButton;
        private ISocket _socket;
        private ServerProcessSetting _serverProcessSetting;

        [Inject]
        public void Construct(ISocket socket,ServerProcessSetting serverProcessSetting)
        {
            _socket = socket;
            _serverProcessSetting = serverProcessSetting;
        }
        
        void Start()
        {
            backToMainMenuButton.onClick.AddListener(Back);
        }

        void Back()
        {
            if (_serverProcessSetting.isLocal)
            {
                _serverProcessSetting.localServerProcess.Close();
            }
            _socket.Close();
            SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
        }
    }
}
