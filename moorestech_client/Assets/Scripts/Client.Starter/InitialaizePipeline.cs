using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Client.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Define;
using Client.Network;
using Client.Network.API;
using Client.Network.Settings;
using Cysharp.Threading.Tasks;
using Server.Boot;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;

namespace Client.Starter
{
    /// <summary>
    ///     シーンのロード、アセットのロード、サーバーとの接続を行う
    ///     TODO 何かが失敗したらそのログを出すようにする
    /// </summary>
    public class InitializeScenePipeline : MonoBehaviour
    {
        [SerializeField] private BlockGameObject missingBlockIdObject;
        [SerializeField] private BlockPrefabContainer blockPrefabContainer;
        
        [SerializeField] private TMP_Text loadingLog;
        [SerializeField] private Button backToMainMenuButton;
        
        private InitializeProprieties _proprieties;
        
        private void Awake()
        {
            backToMainMenuButton.onClick.AddListener(() => SceneManager.LoadScene(SceneConstant.MainMenuSceneName));
        }
        
        private async UniTask Start()
        {
            var loadingStopwatch = new Stopwatch();
            loadingStopwatch.Start();
            
            _proprieties ??= new InitializeProprieties(false, null, ServerConst.LocalServerIp, ServerConst.LocalServerPort, ServerConst.DefaultPlayerId);
            
            // DIコンテナによるServerContextの作成
            new MoorestechServerDIContainerGenerator().Create(ServerConst.ServerDirectory);
            
            //Vanilla APIのロードに必要なものを作成
            var playerConnectionSetting = new PlayerConnectionSetting(_proprieties.PlayerId);
            VanillaApi vanillaApi = null;
            
            //セットされる変数
            BlockGameObjectContainer blockGameObjectContainer = null;
            ItemImageContainer itemImageContainer = null;
            AsyncOperation sceneLoadTask = null;
            InitialHandshakeResponse handshakeResponse = null;
            
            //各種ロードを並列実行
            await UniTask.WhenAll(CreateAndStartVanillaApi(), LoadBlockAssets(), LoadItemAssets(), MainGameSceneLoad());
            
            //staticアクセスできるコンテキストの作成
            new ClientContext(blockGameObjectContainer, itemImageContainer, playerConnectionSetting, vanillaApi);
            
            //シーンに遷移し、初期データを渡す
            SceneManager.sceneLoaded += MainGameSceneLoaded;
            sceneLoadTask.allowSceneActivation = true;
            
            //初期データを渡す処理
            void MainGameSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                SceneManager.sceneLoaded -= MainGameSceneLoaded;
                var starter = FindObjectOfType<MainGameStarter>();
                starter.StartGame(handshakeResponse);
            }
            
            #region Internal
            
            async UniTask CreateAndStartVanillaApi()
            {
                //サーバーとの接続を確立
                var serverCommunicator = await ConnectionToServer();
                
                loadingLog.text += $"\nサーバーとの接続完了  {loadingStopwatch.Elapsed}";
                
                //データの受付開始
                var packetSender = new PacketSender(serverCommunicator);
                var exchangeManager = new PacketExchangeManager(packetSender);
                Task.Run(() => serverCommunicator.StartCommunicat(exchangeManager));
                
                //Vanilla APIの作成
                vanillaApi = new VanillaApi(exchangeManager, packetSender, serverCommunicator, playerConnectionSetting, _proprieties.LocalServerProcess);
                
                //最初に必要なデータを取得
                handshakeResponse = await vanillaApi.Response.InitialHandShake(playerConnectionSetting.PlayerId, default);
                
                loadingLog.text += $"\n初期データ取得完了  {loadingStopwatch.Elapsed}";
            }
            
            async UniTask<ServerCommunicator> ConnectionToServer()
            {
                try
                {
                    var serverConfig = new ConnectionServerConfig(_proprieties.ServerIp, _proprieties.ServerPort);
                    var serverCommunicator = await ServerCommunicator.CreateConnectedInstance(serverConfig);
                    
                    Debug.Log("接続完了");
                    
                    return serverCommunicator;
                }
                catch (Exception e)
                {
                    Debug.LogError("サーバーへの接続に失敗しました");
                    Debug.LogError($"Message {e.Message} StackTrace {e.StackTrace}");
                    throw;
                }
            }
            
            async UniTask LoadBlockAssets()
            {
                blockGameObjectContainer = await BlockGameObjectContainer.CreateAndLoadBlockGameObjectContainer(blockPrefabContainer, missingBlockIdObject);
                loadingLog.text += $"\nブロックロード完了  {loadingStopwatch.Elapsed}";
            }
            
            async UniTask LoadItemAssets()
            {
                //アイテム画像をロード
                //TODO 非同期で実行できるようにする
                itemImageContainer = ItemImageContainer.CreateAndLoadItemImageContainer(ServerConst.ServerModsDirectory);
                loadingLog.text += $"\nアイテムロード完了  {loadingStopwatch.Elapsed}";
            }
            
            async UniTask MainGameSceneLoad()
            {
                sceneLoadTask = SceneManager.LoadSceneAsync(SceneConstant.MainGameSceneName, LoadSceneMode.Single);
                sceneLoadTask.allowSceneActivation = false;
                
                var sceneLoadCts = new CancellationTokenSource();
                
                try
                {
                    await sceneLoadTask.ToUniTask(Progress.Create<float>(
                            x =>
                            {
                                if (x < 0.9f) return;
                                sceneLoadCts.Cancel(); //シーンの読み込みが完了したら終了 allowSceneActivationがfalseの時は0.9fで止まる
                            })
                        , cancellationToken: sceneLoadCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // シーンロード完了
                }
                
                loadingLog.text += $"\nシーンロード完了  {loadingStopwatch.Elapsed}";
            }
            
            #endregion
        }
        
        public void SetProperty(InitializeProprieties proprieties)
        {
            _proprieties = proprieties;
        }
    }
}