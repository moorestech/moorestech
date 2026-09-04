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
        // 許容ポート範囲。文言へは{p0}で供給する
        // The allowed port range; the wording receives it through {p0}
        private const int MinExclusivePort = 1024;
        private const int MaxPort = 65535;

        [SerializeField] private TMP_InputField serverIp;
        [SerializeField] private TMP_InputField serverPort;

        [SerializeField] private ServerConnectPopup serverConnectPopup;

        [SerializeField] private Button connectButton;

        private IPAddress _connectedAddress;
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
                serverConnectPopup.SetText(Localize.GetFormatted(LocalizationKeys.Ui.MainMenu.ConnectPortTooLarge, new[] { MaxPort.ToString() }));
                return;
            }

            if (port <= MinExclusivePort)
            {
                serverConnectPopup.SetText(Localize.GetFormatted(LocalizationKeys.Ui.MainMenu.ConnectPortTooSmall, new[] { MinExclusivePort.ToString() }));
                return;
            }

            var remoteEndPoint = new IPEndPoint(address, port);
            using var socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // 外部への疎通確認だけを隔離する。Connectは失敗時に必ず例外を投げる
            // Isolate only the outbound probe; Connect always throws on failure
            try
            {
                socket.Connect(remoteEndPoint);
            }
            catch (Exception e)
            {
                serverConnectPopup.SetText(Localize.GetFormatted(LocalizationKeys.Ui.MainMenu.ConnectFailed, new[] { e.ToString() }));
                return;
            }

            // 遷移後は旧シーンの入力欄が破棄されるため検証済みの値を退避する
            // The old scene's inputs are destroyed after the load, so keep the validated values
            _connectedAddress = address;
            _connectedPort = port;
            SceneManager.sceneLoaded += OnMainGameSceneLoaded;
            SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
        }

        private void OnMainGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnMainGameSceneLoaded;
            var starter = FindObjectOfType<InitializeScenePipeline>();

            var playerId = PlayerPrefs.GetInt(PlayerPrefsKeys.PlayerIdKey);

            var properties = InitializeProprieties.CreateRemoteConnection(_connectedAddress.ToString(), _connectedPort, playerId);
            
            starter.SetProperty(properties);
        }
    }
}