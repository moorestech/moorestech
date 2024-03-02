using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Game.Context;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using GameConst;
using MainGame.ModLoader.Glb;
using MainGame.Network;
using MainGame.Network.Send.SocketUtil;
using MainGame.Network.Settings;
using MainGame.UnityView.Item;
using ServerServiceProvider;
using UnityEngine;

namespace Client.Starter
{
    public class InitializeScenePipeline : MonoBehaviour
    {
        [SerializeField] private BlockGameObject nothingIndexBlock;
        
        private readonly List<List<IInitializeSequence>> _initializeSequences = new();
        
        
        private void Start()
        {
            
        }
        
        private async UniTask Initialize()
        {
            //Vanilla APIのロードに必要なものを作成
            var serverServiceProvider = new MoorestechServerServiceProvider(ServerConst.ServerDirectory);
            var playerConnectionSetting = new PlayerConnectionSetting(ServerConst.DefaultPlayerId);
            VanillaApi vanillaApi = null;
            
            BlockGameObjectContainer blockGameObjectContainer = null;
            
            //Vanilla APIの作成とコネクションの確立、ブロックのロードを並列で行う
            await UniTask.WhenAll(CreateAndStartVanillaApi(), LoadBlockAssets());

            //アイテム画像をロード
            var itemImageContainer = ItemImageContainer.CreateAndLoadItemImageContainer(ServerConst.ServerModsDirectory, serverServiceProvider);
            
            //コンテキストの作成
            new MoorestechContext(blockGameObjectContainer, itemImageContainer, playerConnectionSetting, vanillaApi);
            
            #region Internal

            async UniTask CreateAndStartVanillaApi()
            {
                var serverCommunicator = await ConnectionToServer();
                
                var packetSender = new PacketSender(serverCommunicator);
                var exchangeManager = new PacketExchangeManager(packetSender);
                
                Task.Run(() => serverCommunicator.StartCommunicat(exchangeManager));

                vanillaApi =  new VanillaApi(exchangeManager, packetSender, serverCommunicator, serverServiceProvider, playerConnectionSetting);
            }

            async UniTask<ServerCommunicator> ConnectionToServer()
            {
                try
                {
                    var serverConfig = new ConnectionServerConfig(ServerConst.LocalServerIp, ServerConst.LocalServerPort);
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
        
        
        
    }
}