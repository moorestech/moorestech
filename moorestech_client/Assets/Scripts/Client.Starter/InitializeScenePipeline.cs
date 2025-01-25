using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Client.Common;
using Client.Game.GameDebug;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.Define;
using Client.Mod.Texture;
using Client.Network;
using Client.Network.API;
using Client.Network.Settings;
using Core.Master;
using Cysharp.Threading.Tasks;
using Server.Boot;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using BlockObjectInfo = Client.Game.InGame.Context.BlockObjectInfo;
using Debug = UnityEngine.Debug;

namespace Client.Starter
{
    /// <summary>
    ///     シーンのロード、アセットのロード、サーバーとの接続を行う
    ///     TODO 何かが失敗したらそのログを出すようにする
    /// </summary>
    public class InitializeScenePipeline : MonoBehaviour
    {
        public const string OtherDebugServerDirectoryKey = "OtherServerDirectory";
        
        
        [SerializeField] private BlockIconImagePhotographer blockIconImagePhotographer;
        [SerializeField] private BlockGameObject missingBlockIdObject;
        
        [SerializeField] private TMP_Text loadingLog;
        [SerializeField] private Button backToMainMenuButton;
        
        private InitializeProprieties _proprieties;
        
        private void Awake()
        {
            backToMainMenuButton.onClick.AddListener(() => SceneManager.LoadScene(SceneConstant.MainMenuSceneName));
        }
        
        private void Start()
        {
            var serverDirectory = DebugParameters.GetValueOrDefaultString(OtherDebugServerDirectoryKey, ServerConst.DefaultServerDirectory);
            Initialize(serverDirectory).Forget();
        }
        
        private async UniTask Initialize(string serverDirectory)
        {
            var loadingStopwatch = new Stopwatch();
            loadingStopwatch.Start();
            
            _proprieties ??= new InitializeProprieties(false, null, ServerConst.LocalServerIp, ServerConst.LocalServerPort, ServerConst.DefaultPlayerId);
            
            // DIコンテナによるServerContextの作成
            new MoorestechServerDIContainerGenerator().Create(serverDirectory);
            
            //Vanilla APIのロードに必要なものを作成
            var playerConnectionSetting = new PlayerConnectionSetting(_proprieties.PlayerId);
            VanillaApi vanillaApi = null;
            
            //セットされる変数
            BlockGameObjectContainer blockGameObjectContainer = null;
            ItemImageContainer itemImageContainer = null;
            AsyncOperation sceneLoadTask = null;
            InitialHandshakeResponse handshakeResponse = null;
            
            //各種ロードを並列実行
            try
            {
                await UniTask.WhenAll(CreateAndStartVanillaApi(), LoadBlockAndItemAssets(), MainGameSceneLoad());
            }
            catch (Exception e)
            {
                Debug.LogError($"初期化処理中にエラーが発生しました: {e.Message}\n{e.StackTrace}");
                // 初期化に失敗した場合はメインメニューへ戻る
                SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
                return;
            }
            
            //staticアクセスできるコンテキストの作成
            var clientContext = new ClientContext(blockGameObjectContainer, itemImageContainer, playerConnectionSetting, vanillaApi);
            
            //シーンに遷移し、初期データを渡す
            SceneManager.sceneLoaded += MainGameSceneLoaded;
            sceneLoadTask.allowSceneActivation = true;
            
            
            #region Internal
            
            //初期データを渡す処理
            void MainGameSceneLoaded(Scene scene, LoadSceneMode mode)
            {
                SceneManager.sceneLoaded -= MainGameSceneLoaded;
                var starter = FindObjectOfType<MainGameStarter>();
                var resolver = starter.StartGame(handshakeResponse);
                var diContainer = new DIContainer(resolver);
                clientContext.SetDIContainer(diContainer);
            }
            
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
                var serverConfig = new ConnectionServerConfig(_proprieties.ServerIp, _proprieties.ServerPort);
                try
                {
                    // 10秒以内にサーバー接続できなければタイムアウト
                    var serverCommunicator = await ServerCommunicator.CreateConnectedInstance(serverConfig)
                        .Timeout(TimeSpan.FromSeconds(10));
                    
                    Debug.Log("接続完了");
                    return serverCommunicator;
                }
                catch (TimeoutException)
                {
                    Debug.LogError("サーバーへの接続がタイムアウトしました");
                    loadingLog.text += "\nサーバーへの接続がタイムアウトしました。メインメニューに戻ります。";
                    await UniTask.Delay(2000);
                    SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
                    throw; // 再度スローして後続処理中断
                }
                catch (Exception e)
                {
                    Debug.LogError($"サーバーへの接続に失敗しました: {e.Message}");
                    loadingLog.text += "\nサーバーへの接続に失敗しました。メインメニューに戻ります。";
                    await UniTask.Delay(2000);
                    SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
                    throw;
                }
            }
            
            async UniTask LoadBlockAndItemAssets()
            {
                // ブロックとアイテムのアセットをロード
                await UniTask.WhenAll(LoadBlockAssets(), LoadItemAssets());
                
                // アイテム画像がロードされていないブロックのアイテム画像をロードする
                await TakeBlockItemImage();
            }
            
            async UniTask LoadBlockAssets()
            {
                // TODo この辺も必要な時に必要なだけロードする用にしたいなぁ
                blockGameObjectContainer = await BlockGameObjectContainer.CreateAndLoadBlockGameObjectContainer(missingBlockIdObject);
                loadingLog.text += $"\nブロックアセットロード完了  {loadingStopwatch.Elapsed}";
            }
            
            async UniTask LoadItemAssets()
            {
                //通常のアイテム画像をロード
                //TODO 非同期で実行できるようにする
                var modDirectory = ServerConst.CreateServerModsDirectory(serverDirectory);
                itemImageContainer = ItemImageContainer.CreateAndLoadItemImageContainer(modDirectory);
                loadingLog.text += $"\nアイテム画像ロード完了  {loadingStopwatch.Elapsed}";
            }
            
            async UniTask TakeBlockItemImage()
            {
                // スクリーンショットを取る必要があるブロックを集める
                // Collect the blocks that need to be screenshot.
                var takeBlockInfos = new List<BlockObjectInfo>();
                var itemIds = new List<ItemId>();
                foreach (var blockId in MasterHolder.BlockMaster.GetBlockIds())
                {
                    var itemId = MasterHolder.BlockMaster.GetItemId(blockId);
                    var itemViewData = itemImageContainer.GetItemView(itemId);
                    
                    if (itemViewData.ItemImage != null || !blockGameObjectContainer.BlockObjects.TryGetValue(blockId, out var blockObjectInfo)) continue;
                    
                    itemIds.Add(itemId);
                    takeBlockInfos.Add(blockObjectInfo);
                }
                
                // アイコンを設定
                // Set the icon.
                var texture2Ds = await blockIconImagePhotographer.TakeBlockIconImages(takeBlockInfos);
                for (var i = 0; i < itemIds.Count; i++)
                {
                    var itemViewData = new ItemViewData(texture2Ds[i], MasterHolder.ItemMaster.GetItemMaster(itemIds[i]));
                    itemImageContainer.AddItemView(itemIds[i], itemViewData);
                }
                
                loadingLog.text += $"\nブロックスクリーンショット完了  {loadingStopwatch.Elapsed}";
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