using System;
using System.Net;
using System.Net.Sockets;
using Client.Common;
using Client.MainMenu.PopUp;
using Client.Starter;
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
                serverConnectPopup.SetText("IPアドレスが正しくありません");
                return;
            }
            
            var port = int.Parse(serverPort.text);
            if (65535 < port)
            {
                serverConnectPopup.SetText("ポート番号は65535以下である必要があります");
                return;
            }
            
            if (port <= 1024)
            {
                serverConnectPopup.SetText("ポート番号は1024以上である必要があります");
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
                serverConnectPopup.SetText("サーバーへの接続に失敗しました\n" + e);
            }
        }
        
        private void OnMainGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnMainGameSceneLoaded;
            var starter = FindObjectOfType<InitializeScenePipeline>();
            
            var ip = serverIp.text;
            var port = int.Parse(serverPort.text);
            var playerId = PlayerPrefs.GetInt(PlayerPrefsKeys.PlayerIdKey);
            
            var properties = new InitializeProprieties(null, ip, port, playerId);
            
            starter.SetProperty(properties);
        }
    }
}