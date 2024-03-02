using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Client.Game.Context;
using Client.Network.API;
using Cysharp.Threading.Tasks;
using GameConst;
using MainGame.Network;
using MainGame.Network.Send.SocketUtil;
using MainGame.Network.Settings;
using ServerServiceProvider;
using UnityEngine;

namespace Client.Starter
{
    public class InitializeScenePipeline : MonoBehaviour
    {
        private readonly List<List<IInitializeSequence>> _initializeSequences = new();
        
        
        private void Start()
        {
            
        }
        
        private async UniTask Initialize()
        {
            var serverServiceProvider = new MoorestechServerServiceProvider(ServerConst.ServerDirectory);
            
            var vanillaApi = await CreateAndStartVanillaApi();





            new MoorestechContext();
            
            #region Internal

            async UniTask<VanillaApi> CreateAndStartVanillaApi()
            {
                var serverCommunicator = await ConnectionToServer();
                
                var packetSender = new PacketSender(serverCommunicator);
                var exchangeManager = new PacketExchangeManager(packetSender);
                
                Task.Run(() => serverCommunicator.StartCommunicat(exchangeManager));

                var playerConnectionSetting = new PlayerConnectionSetting(ServerConst.DefaultPlayerId);
                return new VanillaApi(exchangeManager, packetSender, serverCommunicator, serverServiceProvider, playerConnectionSetting);
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

            async UniTask LoadModAssets()
            {
                
            }

            #endregion
        }
        
        
        
    }
}