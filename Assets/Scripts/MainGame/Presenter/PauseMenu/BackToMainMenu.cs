using System;
using System.Threading;
using MainGame.Basic;
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
        private ISocket _socket;
        private ServerProcessSetting _serverProcessSetting;
        private SendSaveProtocol _sendSaveProtocol;

        [Inject]
        public void Construct(ISocket socket,ServerProcessSetting serverProcessSetting,SendSaveProtocol sendSaveProtocol)
        {
            _sendSaveProtocol = sendSaveProtocol;
            _socket = socket;
            _serverProcessSetting = serverProcessSetting;
        }
        
        void Start()
        {
            backToMainMenuButton.onClick.AddListener(Back);
        }

        void Back()
        {
            Disconnect();
            SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
        }

        private void OnDestroy() { Disconnect(); }


        private void Disconnect()
        {
            _sendSaveProtocol.Send();
            Thread.Sleep(50);
            if (_serverProcessSetting.isLocal)
            {
                _serverProcessSetting.localServerProcess.Kill();
            }
            _socket.Close();
        }
    }
}
