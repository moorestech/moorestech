using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using GameConst;
using MainGame.Network;
using MainGame.Network.Send.SocketUtil;
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
            var serverCommunicator = await ConnectionToServer();

            #region Internal

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