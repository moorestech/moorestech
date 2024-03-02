using System.Threading;
using Client.Network.API;
using Constant;
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
        public void Construct(ServerProcessSetting serverProcessSetting)
        {
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
            VanillaApi.Disconnect();
        }
    }
}