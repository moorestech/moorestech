using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Game.Context;
using Client.Network.API;
using Constant;
using Cysharp.Threading.Tasks;
using GameConst;
using MainGame.ModLoader.Glb;
using MainGame.Network;
using MainGame.Network.Send.SocketUtil;
using MainGame.Network.Settings;
using MainGame.Starter;
using MainGame.UnityView.Item;
using ServerServiceProvider;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Starter
{
    public class InitializeScenePipeline : MonoBehaviour
    {
        [SerializeField] private BlockGameObject nothingIndexBlock;
        
        private InitializeProprieties _proprieties;
        
        public void SetProperty(InitializeProprieties proprieties)
        {
            _proprieties = proprieties;
        }
        
        private async UniTask Start()
        {
            _proprieties ??= new InitializeProprieties(
                false, null, 
                ServerConst.LocalServerIp, ServerConst.LocalServerPort, ServerConst.DefaultPlayerId);

            //Vanilla APIのロードに必要なものを作成
            var serverServiceProvider = new MoorestechServerServiceProvider(ServerConst.ServerDirectory);
            var playerConnectionSetting = new PlayerConnectionSetting(_proprieties.PlayerId);
            VanillaApi vanillaApi = null;
            
            BlockGameObjectContainer blockGameObjectContainer = null;
            
            //Vanilla APIの作成とコネクションの確立、ブロックのロードを並列で行う
            await UniTask.WhenAll(CreateAndStartVanillaApi(), LoadBlockAssets());

            //アイテム画像をロード
            var itemImageContainer = ItemImageContainer.CreateAndLoadItemImageContainer(ServerConst.ServerModsDirectory, serverServiceProvider);
            
            //staticアクセスできるコンテキストの作成
            new MoorestechContext(blockGameObjectContainer, itemImageContainer, playerConnectionSetting, vanillaApi);
            
            //最初に必要なデータを取得
            _handshakeResponse = await vanillaApi.Response.InitialHandShake(playerConnectionSetting.PlayerId, default);
            
            //シーンに遷移し、初期データを渡す
            SceneManager.sceneLoaded += OnMainGameSceneLoaded;
            SceneManager.LoadScene(SceneConstant.MainGameSceneName);
            
            #region Internal

            async UniTask CreateAndStartVanillaApi()
            {
                var serverCommunicator = await ConnectionToServer();
                
                var packetSender = new PacketSender(serverCommunicator);
                var exchangeManager = new PacketExchangeManager(packetSender);
                
                Task.Run(() => serverCommunicator.StartCommunicat(exchangeManager));

                vanillaApi = new VanillaApi(
                    exchangeManager, packetSender, serverCommunicator, serverServiceProvider, playerConnectionSetting,_proprieties.LocalServerProcess);
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
                blockGameObjectContainer = await BlockGameObjectContainer.CreateAndLoadBlockGameObjectContainer(ServerConst.ServerModsDirectory,nothingIndexBlock, serverServiceProvider);
            }

            #endregion
        }
        
        private InitialHandshakeResponse _handshakeResponse;

        //シーンがロードされたら初期データを渡す
        private void OnMainGameSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnMainGameSceneLoaded;
            var starter = FindObjectOfType<MainGameStarter>();
            starter.SetInitialHandshakeResponse(_handshakeResponse);
        }
    }
}