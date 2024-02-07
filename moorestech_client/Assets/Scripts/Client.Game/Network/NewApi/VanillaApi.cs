using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Server.Protocol.PacketResponse;

namespace Client.Network.NewApi
{
    public class VanillaApi
    {
        private readonly ServerRequester _serverRequester;
        public VanillaApi(ServerRequester serverRequester)
        {
            _serverRequester = serverRequester;
        }
        
        public async UniTask<List<MapObjectDestructionInformationData>> GetMapObjectDestructionInformationData(CancellationToken ct)
        {
            var request = new RequestMapObjectDestructionInformationMessagePack();
            var response = await _serverRequester.GetInformationData<ResponseMapObjectDestructionInformationMessagePack>(request ,ct);
            return response?.MapObjects;
        }
    }
}