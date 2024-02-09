using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;

namespace Client.Network.NewApi
{
    public class VanillaApi
    {
        private readonly ServerConnector _serverConnector;
        public VanillaApi(ServerConnector serverConnector)
        {
            _serverConnector = serverConnector;
        }
        
        /// <summary>
        /// TODO この呼び出しタイミングを考える
        /// </summary>
        public async UniTask<List<MapObjectsInfoMessagePack>> GetMapObjectInfo(CancellationToken ct)
        {
            var request = new RequestMapObjectDestructionInformationMessagePack();
            var response = await _serverConnector.GetInformationData<ResponseMapObjectsMessagePack>(request ,ct);
            return response?.MapObjects;
        }
    }
}