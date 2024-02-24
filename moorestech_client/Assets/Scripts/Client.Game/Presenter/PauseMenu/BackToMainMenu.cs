using System.Threading;
using Client.Network.NewApi;
using Constant;
using MainGame.Network;
using MainGame.Network.Send;
using MainGame.Network.Settings;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

namespace MainGame.Control.UI.PauseMenu
{
    //ゲームが終了したときかメインメニューに戻るときはサーバーを終了させます
    public class BackToMainMenu : MonoBehaviour
    {
        [SerializeField] private Button backToMainMenuButton;
        private ServerProcessSetting _serverProcessSetting;
        private ISocketSender _socketSender;

        private void Start()
        {
            backToMainMenuButton.onClick.AddListener(Back);
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        [Inject]
        public void Construct(ISocketSender socketSender, ServerProcessSetting serverProcessSetting)
        {
            _socketSender = socketSender;
            _serverProcessSetting = serverProcessSetting;
        }

        private void Back()
        {
            Disconnect();
            SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
        }


        private void Disconnect()
        {
            VanillaApi.SendOnly.Save();
            Thread.Sleep(50);
            if (_serverProcessSetting.isLocal) _serverProcessSetting.localServerProcess.Kill();
            _socketSender.Close();
        }
    }
}