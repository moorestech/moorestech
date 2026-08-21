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
    /// <summary>
    /// ローカルプレイが既存サーバーを一切探さず内蔵サーバーだけで起動し、リモート指定時は内蔵サーバーを起動しないことを実測する（ADR 0013）。
    /// 旧既定ポート11564に他人のサーバーが居る開発機で並列worktreeが別ワールドを壊した事故の再発を、接続試行の有無そのもので固定する。
    /// Empirically verifies that local play boots only the embedded server without probing, and that remote never boots one (ADR 0013).
    /// It pins the absence of the probe itself, which is what let parallel worktrees corrupt another world through a stale server on 11564.
    /// </summary>
    public class LocalPlayEmbeddedServerBootTest
    {
        // 反転前のローカル既定ポート。ここへ接続が来たら接続試行が復活している
        // The pre-inversion local default port; any connection here means the probe came back
        private const int LegacyDefaultServerPort = 11564;

        // 内蔵サーバーの破棄を待つ上限フレーム数
        // Frame budget for waiting on the embedded server teardown
        private const int ShutdownWaitFrameLimit = 600;

        [UnityTest]
        public IEnumerator LocalBoot_NeverProbesLegacyPort_AndShutdownFoldsEmbeddedServer()
        {
            // 占有判定はPlayMode突入前に済ませる。PlayMode中のIgnoreはExitPlayModeへ到達できずEditorを固着させる
            // Decide occupancy before entering PlayMode; an Ignore inside PlayMode never reaches ExitPlayMode and sticks the Editor
            if (IsLegacyPortOccupied()) Assert.Ignore($"{LegacyDefaultServerPort} が他プロセスに使用中のため検証をスキップ");

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
                // 旧既定ポートに別サーバーが居座る状況を作る。acceptしないので接続要求は保留のまま残る
                // Simulate a foreign server squatting on the legacy port; requests stay pending because nothing accepts them
                var legacyPortListener = new TcpListener(IPAddress.Loopback, LegacyDefaultServerPort);
                legacyPortListener.Start();

                // 失敗しても待ち受けを必ず畳む。開きっぱなしだと後続の全ローカル起動を汚染する
                // Always tear the listener down; leaving it open would poison every later local boot
                try
                {
                    await LoadMainGame();

                    // 内蔵サーバーが起動し、その実ポートで世界が立ち上がっている
                    // The embedded server started and the world came up on its actual port
                    Assert.IsNotNull(ClientContext.VanillaApi, "ワールドが起動していない");
                    var embeddedServer = Object.FindFirstObjectByType<ServerStarter>();
                    Assert.IsNotNull(embeddedServer, "内蔵サーバーが起動していない");
                    Assert.AreNotEqual(LegacyDefaultServerPort, embeddedServer.BoundPort);

                    // 誰も11564へ接続していない＝接続試行フォールバックが無い
                    // Nobody connected to 11564, which is the absence of the probe-and-fallback path
                    Assert.IsFalse(legacyPortListener.Pending(), "ローカル起動が11564へ接続を試みた");

                    // 終了通知1本で内蔵サーバーが畳まれる。破棄経路を消すとここが赤くなる
                    // A single shutdown notification folds the embedded server; deleting the teardown path turns this red
                    GameShutdownEvent.FireGameShutdown();
                    var foldedEmbeddedServer = false;
                    for (var frame = 0; frame < ShutdownWaitFrameLimit && !foldedEmbeddedServer; frame++)
                    {
                        foldedEmbeddedServer = Object.FindFirstObjectByType<ServerStarter>() == null;
                        await UniTask.Yield();
                    }
                    Assert.IsTrue(foldedEmbeddedServer, "終了通知後も内蔵サーバーが残っている");
                }
                finally
                {
                    legacyPortListener.Stop();
                }
            }

            // 待ち受け開始はソケット境界。開発機の実サーバーが握っているかを他プロセス占有だけで判定する
            // Starting the listener is a socket boundary; only an in-use address counts as occupancy by another process
            bool IsLegacyPortOccupied()
            {
                var occupancyProbe = new TcpListener(IPAddress.Loopback, LegacyDefaultServerPort);
                try
                {
                    occupancyProbe.Start();
                }
                catch (SocketException e) when (e.SocketErrorCode == SocketError.AddressAlreadyInUse)
                {
                    return true;
                }
                occupancyProbe.Stop();
                return false;
            }

            #endregion
        }

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

                var remoteProperties = InitializeProprieties.CreateRemoteConnection(ServerConst.LocalServerIp, FindClosedPort(), 1);

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
