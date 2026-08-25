using System;
using System.Net;
using System.Net.Sockets;
using Client.Common;
using Client.Localization;
using Client.MainMenu.PopUp;
using Client.Starter;
using Mooresmaster.Localization.Generated;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Client.MainMenu
{
    public class ConnectServer : MonoBehaviour
    {
        [SerializeField] private TMP_InputField serverIp;
        [SerializeField] private TMP_InputField serverPort;
        
        [SerializeField] private ServerConnectPopup serverConnectPopup;
        
        [SerializeField] private Button connectButton;
        
        private void Start()
        {
            connectButton.onClick.AddListener(Connect);
        }
        
        private void Connect()
        {
            if (!IPAddress.TryParse(serverIp.text, out var address))
            {
                serverConnectPopup.SetText(Localize.Get(LocalizationKeys.Ui.MainMenu.ConnectInvalidIp));
                return;
            }
            
            var port = int.Parse(serverPort.text);
            if (65535 < port)
            {
                serverConnectPopup.SetText(Localize.Get(LocalizationKeys.Ui.MainMenu.ConnectPortTooLarge));
                return;
            }
            
            if (port <= 1024)
            {
                serverConnectPopup.SetText(Localize.Get(LocalizationKeys.Ui.MainMenu.ConnectPortTooSmall));
                return;
            }
            
            try
            {
                var remoteEndPoint = new IPEndPoint(address, port);
                var socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                
                socket.Connect(remoteEndPoint);
                
                if (socket.Connected)
                {
                    //接続が確認出来たのでソケットを閉じて実際にゲームに移行
                    socket.Close();
                    
                    SceneManager.sceneLoaded += OnMainGameSceneLoaded;
                    SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
                }
            }
            catch (Exception e)
            {
                serverConnectPopup.SetText(Localize.GetFormatted(LocalizationKeys.Ui.MainMenu.ConnectFailed, new[] { e.ToString() }));
            }
        }
        
        private void OnMainGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnMainGameSceneLoaded;
            var starter = FindObjectOfType<InitializeScenePipeline>();
            
            var ip = serverIp.text;
            var port = int.Parse(serverPort.text);
            var playerId = PlayerPrefs.GetInt(PlayerPrefsKeys.PlayerIdKey);
            
            var properties = InitializeProprieties.CreateRemoteConnection(ip, port, playerId);
            
            starter.SetProperty(properties);
        }
    }
}