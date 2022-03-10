using MainGame.Basic;
using MainGame.Network;
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

        [Inject]
        public void Construct(ISocket socket)
        {
            _socket = socket;
        }
        
        void Start()
        {
            backToMainMenuButton.onClick.AddListener(Back);
        }

        void Back()
        {
            _socket.Close();
            SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
        }
    }
}
