using System.Collections;
using System.Net;
using System.Net.Sockets;
using Client.Common;
using Client.Game.Common;
using Client.Game.InGame.Context;
using Client.Starter;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Server.Boot;
using Server.Boot.Args;
using UnityEditor;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    [Category("CiShardClientPlay3")]
    public class RemotePlayServerBootTest
    {
        [UnityTest]
        public IEnumerator RemoteBoot_NeverStartsEmbeddedServer_WhenDestinationIsClosed()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode　は必ず[UnityTest]関数の直下で呼び出すこと。そうでないとなぜかわからないがプレイモードに入らない
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function. Otherwise, for unknown reasons, it will not enter PlayMode.
            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode.
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            yield return new ExitPlayMode();

            // テスト終了後にデバッグオブジェクト無効化フラグをクリア
            // Clear debug objects disabled flag after test.
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                SceneManager.sceneLoaded += SetRemoteProperty;
                SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);

                // シーンのオブジェクトが初期化されるまで1フレーム待機
                // Wait 1 frame for scene objects to initialize
                await UniTask.Yield();

                // 接続失敗からメインメニュー復帰までの全フレームで内蔵サーバーが生えないことを見張る
                // Watch every frame from the failed connection to the main menu return for an embedded server appearing
                var returnedToMainMenu = false;
                for (var i = 0; i < 10000 && !returnedToMainMenu; i++)
                {
                    Assert.IsNull(Object.FindFirstObjectByType<ServerStarter>(), "リモート接続なのに内蔵サーバーが起動した");
                    returnedToMainMenu = SceneManager.GetActiveScene().name == SceneConstant.MainMenuSceneName;
                    await UniTask.Yield();
                }

                Assert.IsTrue(returnedToMainMenu, "リモート接続失敗後にメインメニューへ戻らなかった");
            }

            // 閉じた宛先を指すリモート設定を流し込む
            // Inject a remote configuration pointing at a closed destination
            void SetRemoteProperty(Scene scene, LoadSceneMode mode)
            {
                SceneManager.sceneLoaded -= SetRemoteProperty;

                var remoteProperties = InitializeProprieties.CreateRemoteConnection(ServerConst.LocalServerIp, FindClosedPort());

                // リモートでもクライアント側のserverDirectory解決に使われるためテスト用Modを指す
                // Even remote resolves the client-side serverDirectory from these args, so point them at the test mod
                remoteProperties.CreateLocalServerArgs = CliConvert.Serialize(new StartServerSettings
                {
                    ServerDataDirectory = EditModeInPlayingTestServerDirectoryPath,
                });

                Object.FindFirstObjectByType<InitializeScenePipeline>().SetProperty(remoteProperties);
            }

            // OSに空きポートを採番させ、即座に閉じて誰も待ち受けていない宛先を得る
            // Let the OS assign a free port and close it immediately to obtain an unattended destination
            int FindClosedPort()
            {
                var probeListener = new TcpListener(IPAddress.Loopback, 0);
                probeListener.Start();
                var port = ((IPEndPoint)probeListener.LocalEndpoint).Port;
                probeListener.Stop();
                return port;
            }

            #endregion
        }
    }
}
