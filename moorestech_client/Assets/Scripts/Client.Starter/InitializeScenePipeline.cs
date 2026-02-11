using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Client.Common;
using Client.Common.Asset;
using Client.Game.Common;
using Client.Game.InGame.Block;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.Inventory.Common;
using Client.Game.InGame.UI.Modal;
using Client.Mod.Texture;
using Client.Network;
using Client.Network.API;
using Client.Network.Settings;
using Core.Master;
using Cysharp.Threading.Tasks;
using Game.Context;
using Server.Boot;
using Server.Boot.Args;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
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
        [SerializeField] private BlockIconImagePhotographer blockIconImagePhotographer;
        [SerializeField] private BlockGameObject missingBlockIdObject;
        
        [SerializeField] private TMP_Text loadingLog;
        [SerializeField] private Button backToMainMenuButton;
        
        private InitializeProprieties _proprieties = InitializeProprieties.CreateDefault();
        public void SetProperty(InitializeProprieties proprieties)
        {
            _proprieties = proprieties;
        }
        
        private void Awake()
        {
            backToMainMenuButton.onClick.AddListener(() => SceneManager.LoadScene(SceneConstant.MainMenuSceneName));
        }
        
        private void Start()
        {
            Initialize().Forget();
        }
        
        private async UniTask Initialize()
        {
            var args = CliConvert.Parse<StartServerSettings>(_proprieties.CreateLocalServerArgs);
            var serverDirectory = args.ServerDataDirectory;
            
            var loadingStopwatch = new Stopwatch();
            loadingStopwatch.Start();
            
            // Addressablesのロード
            var initializeHandle = Addressables.InitializeAsync();
            await initializeHandle.ToUniTask();
            
            // ---- Addressables事前ロードフェーズ ----
            // ---- Addressables pre-load phase ----
            //
            // 以下のアセットは後続の並列タスク（UniTask.WhenAll）に入れず、ここで事前にロードする。
            // The following assets must be pre-loaded here, NOT inside the parallel UniTask.WhenAll below.
            //
            // 【観察された事実 / Observed facts】
            // - Addressables の "Use Existing Build" モード（ローカルバンドルからロード）で、
            //   並列タスク内から複数のアセットを同時にロードすると、一部のロードが永久にハングする。
            //   In "Use Existing Build" mode (loading from local bundles), loading multiple assets
            //   concurrently from within parallel tasks causes some loads to hang indefinitely.
            //
            // - 具体的には、BlockGameObjectPrefabContainer のブロックプレハブ群（大量）と
            //   ItemSlotView / FluidSlotView を同じ WhenAll 内で並列ロードすると、
            //   後者だけが完了せずハングする。ブロック側は正常に完了する。
            //   Specifically, when block prefabs (many) and ItemSlotView/FluidSlotView are loaded
            //   in the same WhenAll, only the latter hangs. Block loads complete normally.
            //
            // - Addressables 初期化直後にここで事前ロードしておけば、ハングは発生しない。
            //   Pre-loading them here right after Addressables init prevents the hang.
            //
            // - ChestBlockInventory もDisposeせずバンドル参照を維持する必要がある。
            //   解放すると後続のバンドル再取得時にハングする。
            //   ChestBlockInventory must also keep its bundle reference (no Dispose).
            //   Releasing it causes hangs on subsequent bundle re-acquisition.
            //
            // 【根本原因は不明 / Root cause is unknown】
            // - アプリケーションコード側のロード処理はすべて同じ AddressableLoader.LoadAsync を使用しており、
            //   ロード方法自体に違いはない。
            //   All loading goes through the same AddressableLoader.LoadAsync; there is no difference
            //   in how the loads are issued from the application side.
            //
            // - Addressables 内部のバンドルロードスケジューリングやロック機構に起因すると推測されるが、
            //   内部実装まで追跡しておらず、確定的な原因は特定できていない。
            //   It is suspected to be caused by Addressables' internal bundle load scheduling or
            //   locking mechanism, but the internal implementation has not been traced, so the
            //   definitive cause remains unidentified.
            //
            await AddressableLoader.LoadAsync<GameObject>("Vanilla/UI/Block/ChestBlockInventory");
            await UniTask.WhenAll(ItemSlotView.LoadItemSlotViewPrefab(), FluidSlotView.LoadItemSlotViewPrefab());

            _proprieties ??= InitializeProprieties.CreateDefault();
            
            // DIコンテナによるServerContextの作成
            if (!ServerContext.IsInitialized)
            {
                var options = new MoorestechServerDIContainerOptions(serverDirectory);
                new MoorestechServerDIContainerGenerator().Create(options);
            }
            
            //Vanilla APIのロードに必要なものを作成
            var playerConnectionSetting = new PlayerConnectionSetting(_proprieties.PlayerId);
            VanillaApi vanillaApi = null;
            
            //セットされる変数
            BlockGameObjectPrefabContainer blockGameObjectPrefabContainer = null;
            ItemImageContainer itemImageContainer = null;
            FluidImageContainer fluidImageContainer = null;
            AsyncOperation sceneLoadTask = null;
            InitialHandshakeResponse handshakeResponse = null;
            ModalManager modalManager = new ModalManager();
            
            //各種ロードを並列実行
            // Execute various loading tasks in parallel.
            try
            {
                await UniTask.WhenAll(CreateAndStartVanillaApi(), LoadModAssets(), MainGameSceneLoad());
            }
            catch (Exception e)
            {
                Debug.LogError($"初期化処理中にエラーが発生しました: {e.GetType()} {e.Message}\n{e.StackTrace}");
                // 初期化に失敗した場合はメインメニューへ戻る
                SceneManager.LoadScene(SceneConstant.MainMenuSceneName);
                return;
            }
            
            //staticアクセスできるコンテキストの作成
            new ClientContext(blockGameObjectPrefabContainer, itemImageContainer, fluidImageContainer , playerConnectionSetting, vanillaApi, modalManager);
            
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
                new ClientDIContext(new DIContainer(resolver));

                // ゲーム初期化完了イベントを発火
                // Fire game initialization complete event
                GameInitializedEvent.FireGameInitialized();
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
                // Fetch the initial data bundle
                handshakeResponse = await vanillaApi.Response.InitialHandShake(playerConnectionSetting.PlayerId, default);
                
                loadingLog.text += $"\n初期データ取得完了  {loadingStopwatch.Elapsed}";
            }
            
            async UniTask<ServerCommunicator> ConnectionToServer()
            {
                var serverProperties = new ConnectionServerProperties(_proprieties.ServerIp, _proprieties.ServerPort);
                var timeOut = TimeSpan.FromSeconds(3);
                try
                {
                    // 10秒以内にサーバー接続できなければタイムアウト
                    var serverCommunicator = await ServerCommunicator.CreateConnectedInstance(serverProperties).Timeout(timeOut);
                    return serverCommunicator;
                }
                catch (SocketException)
                {
                    loadingLog.text += "\nサーバーの接続が失敗しました。サーバーを起動します。";
                    try
                    {
                        var serverInstanceGameObject = new GameObject("ServerInstance");
                        var serverStarter = serverInstanceGameObject.AddComponent<ServerStarter>();
                        if (_proprieties.CreateLocalServerArgs != null)
                        {
                            serverStarter.SetArgs(_proprieties.CreateLocalServerArgs);
                        }
                        DontDestroyOnLoad(serverInstanceGameObject);

                        await UniTask.Delay(1000);

                        var serverCommunicator = await ServerCommunicator.CreateConnectedInstance(serverProperties).Timeout(timeOut);
                        return serverCommunicator;
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
            }
            
            async UniTask LoadModAssets()
            {
                // ブロックとアイテムのアセットをロード
                // Load block and item assets.
                await UniTask.WhenAll(LoadBlockAssets(), LoadItemAssets(), LoadFluidAssets());
                
                // アイテム画像がロードされていないブロックのアイテム画像をロードする
                await TakeBlockItemImage();
            }
            
            async UniTask LoadBlockAssets()
            {
                // TODo この辺も必要な時に必要なだけロードする用にしたいなぁ
                blockGameObjectPrefabContainer = await BlockGameObjectPrefabContainer.CreateAndLoadBlockGameObjectContainer(missingBlockIdObject);
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
            
            async UniTask LoadFluidAssets()
            {
                //通常の液体画像をロード
                //TODO 非同期で実行できるようにする
                var modDirectory = ServerConst.CreateServerModsDirectory(serverDirectory);
                fluidImageContainer = FluidImageContainer.CreateAndLoadFluidImageContainer(modDirectory);
                loadingLog.text += $"\n液体画像ロード完了  {loadingStopwatch.Elapsed}";
            }
            
            async UniTask TakeBlockItemImage()
            {
                // スクリーンショットを取る必要があるブロックを集める
                // Collect the blocks that need to be screenshot.
                var takeBlockInfos = new List<BlockPrefabInfo>();
                var itemIds = new List<ItemId>();
                foreach (var blockId in MasterHolder.BlockMaster.GetBlockAllIds())
                {
                    var itemId = MasterHolder.BlockMaster.GetItemId(blockId);
                    var itemViewData = itemImageContainer.GetItemView(itemId);
                    
                    if (itemViewData.ItemImage != null || !blockGameObjectPrefabContainer.BlockPrefabInfos.TryGetValue(blockId, out var blockObjectInfo)) continue;
                    
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
                    // Scene load complete.
                }

                loadingLog.text += $"\nシーンロード完了  {loadingStopwatch.Elapsed}";
            }
            
            
            #endregion
        }
    }
}
