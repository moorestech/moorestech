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
        // ポート番号の許容範囲（実装とlocalization.csvの文言が二重に持つ値のSSOT）
        // Allowed port range (single source of truth shared with the localization.csv wording)
        private const int MinExclusivePort = 1024;
        private const int MaxPort = 65535;

        [SerializeField] private TMP_InputField serverIp;
        [SerializeField] private TMP_InputField serverPort;

        [SerializeField] private ServerConnectPopup serverConnectPopup;

        [SerializeField] private Button connectButton;

        private int _connectedPort;

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

            if (!int.TryParse(serverPort.text, out var port))
            {
                serverConnectPopup.SetText(Localize.Get(LocalizationKeys.Ui.MainMenu.ConnectInvalidPort));
                return;
            }

            if (MaxPort < port)
            {
                serverConnectPopup.SetText(Localize.Get(LocalizationKeys.Ui.MainMenu.ConnectPortTooLarge));
                return;
            }

            if (port <= MinExclusivePort)
            {
                serverConnectPopup.SetText(Localize.Get(LocalizationKeys.Ui.MainMenu.ConnectPortTooSmall));
                return;
            }

            try
            {
                var remoteEndPoint = new IPEndPoint(address, port);
                var socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

                // Connectは失敗時に必ず例外を投げるため、ここへ来た時点で接続は成立している
                // Connect always throws on failure, so reaching this line means the connection succeeded
                socket.Connect(remoteEndPoint);
                socket.Close();

                _connectedPort = port;
                SceneManager.sceneLoaded += OnMainGameSceneLoaded;
                SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
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
            var playerId = PlayerPrefs.GetInt(PlayerPrefsKeys.PlayerIdKey);

            var properties = InitializeProprieties.CreateRemoteConnection(ip, _connectedPort, playerId);
            
            starter.SetProperty(properties);
        }
    }
}