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

        // 遷移後は旧シーンの入力欄が破棄されるため、検証済みの接続プロパティを退避する
        // The old scene's inputs are destroyed after the load, so the validated properties are kept here
        private InitializeProprieties _connectedProperties;

        private void Start()
        {
            connectButton.onClick.AddListener(Connect);
        }

        private void Connect()
        {
            var playerId = PlayerPrefs.GetInt(PlayerPrefsKeys.PlayerIdKey);
            if (!InitializeProprieties.TryCreateRemoteConnection(serverIp.text, serverPort.text, playerId, out var properties, out var denyReason))
            {
                serverConnectPopup.SetText(Localize.GetFormatted(denyReason.Key, denyReason.TextParams));
                return;
            }

            if (!TryProbe(properties, out var failureDetail))
            {
                serverConnectPopup.SetText(Localize.GetFormatted(LocalizationKeys.Ui.MainMenu.ConnectFailed, new[] { failureDetail }));
                return;
            }

            _connectedProperties = properties;
            SceneManager.sceneLoaded += OnMainGameSceneLoaded;
            SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);
        }

        // 外部への疎通確認だけを隔離する。Connectは失敗時に必ず例外を投げる
        // Isolate only the outbound probe; Connect always throws on failure
        private static bool TryProbe(InitializeProprieties properties, out string failureDetail)
        {
            // 検証を通った値なのでパースは必ず成功する
            // The values passed validation, so parsing always succeeds
            var remoteEndPoint = new IPEndPoint(IPAddress.Parse(properties.ServerIp), properties.RemoteServerPort.Value);
            using var socket = new Socket(remoteEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            try
            {
                socket.Connect(remoteEndPoint);
            }
            catch (Exception e)
            {
                failureDetail = e.ToString();
                return false;
            }

            failureDetail = null;
            return true;
        }

        private void OnMainGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnMainGameSceneLoaded;
            var starter = FindObjectOfType<InitializeScenePipeline>();

            starter.SetProperty(_connectedProperties);
        }
    }
}
